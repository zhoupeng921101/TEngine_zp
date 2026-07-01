using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 四玩法货币(Soul/Piety/Exp/Energy)本地视图 ↔ 服务端权威 对账器(P2 全栈迁移·客户端段)。
    ///
    /// 背景:四货币历史上住在玩法状态机 <see cref="MergeOrderState"/> 的字段里、本地即权威(PlayerPrefs 记账)。
    /// P2 改服务端权威:本地降为「乐观显示视图 + 缓存」,所有产销经服务端裁定。本类不重写玩法经济
    /// (落子/合成/交付/修缮仍同步改 <see cref="MergeOrderState"/> 字段,保手感),而是在玩法事件边界把
    /// 「本地净变化」聚合成一笔 <c>C2G_PropertyChangeRequest</c> 上报、用服务端权威 NewAmount 校正本地视图。
    ///
    /// 纯逻辑(不依赖 UnityEngine):经注入的 <see cref="IRpcGateway"/> 发请求、经注入的回调把权威值写回
    /// <see cref="MergeOrderState"/>,故可 EditMode 单测。
    /// </summary>
    /// <remarks>
    /// 【聚合上报防频率拦截】服务端对同账号同货币有最小变更间隔(占位 100ms)。连消/级联/连击可能在极短时间产出
    /// 多笔同币奖励,若每消一行就发一次会被 RateLimited 拒、丢奖励。本类按「玩法事件」粒度聚合:每次
    /// <see cref="ReportPending"/>(挂在 MergeMetaPersistence.SaveAsync 落盘边界,= 一次玩法事件结束)对每种货币
    /// 计算「当前字段值 - 上次已上报基线」的净 delta,非零才发一笔(累加后一次发,不逐笔发)。
    ///
    /// 【对账方向】沿 PlayerAttrService 覆盖语义:响应/推送回的 NewAmount 是服务端绝对余额,直接 set 本地字段
    /// (不做「旧值 + delta」相对推断)。NotEnough/OverLimit/InvalidRequest 一律把本地视图校正回服务端权威值
    /// (NewAmount),不崩、不卡玩法。
    ///
    /// 【Soul/Piety/Exp 与 Energy 的差异】Soul/Piety/Exp 是纯计数器(只有玩法产销改它),净 delta 即真实变更,
    /// 可直接基线 diff 上报。Energy 多一条本地预测恢复路径(<see cref="MergeOrderState.ApplyTimeRegen"/> 按时间
    /// 补点,服务端登录/变更时懒结算),预测恢复是「显示用」,不可上报为产出(否则双重发体力)。故 Energy 的预测
    /// 恢复增量须经 <see cref="ExcludeEnergyRegen"/> 从基线一并抬高、不计入待上报 delta;只有玩法真实扣/奖(落子/
    /// 交付/修缮/祈愿/开盒)走上报。
    /// </remarks>
    public sealed class MetaCurrencySync
    {
        private readonly IRpcGateway _gateway;

        /// <summary>已上报基线:某货币「上次成功上报/对账后」的本地值。下次 ReportPending 的 delta = 当前值 - 基线。</summary>
        private long _baseSoul, _basePiety, _baseExp, _baseEnergy;

        /// <summary>
        /// 六元层计数器基线(P 全栈迁移·客户端段:女神等级/评级、章节解锁数、盲盒计数、神庙修缮计数、神庙修缮游标)。
        /// 与四货币同口径「当前值 - 基线」净 delta 上报、服务端 NewAmount 覆盖本地视图 + 基线。
        /// 神庙修缮计数(<see cref="_baseTempleRepaired"/>)对应服务端 <c>PropertyType.TempleRepaired</c>(已修厅数标量),
        /// 客户端活态是 <see cref="MergeOrderState.TempleRepaired"/> 布尔数组,其「已修计数」= true 项数(顺序解锁,恒 = NextRepairIndex)。
        /// </summary>
        private long _baseGoddessLevel, _baseGoddessRating, _baseUnlockedChapter,
                     _baseBlindBoxCount, _baseTempleRepaired, _baseNextRepairIndex;

        /// <summary>是否已收到登录快照对齐过基线(false 时不上报,避免登录前用占位 0 基线发出错误 delta)。</summary>
        public bool IsReady { get; private set; }

        /// <summary>一次上报往返进行中标志:防重入(ReportPending 可能被落盘边界高频触发,避免并发多笔在途撞频率拦截)。</summary>
        private bool _reporting;

        public MetaCurrencySync(IRpcGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// 登录初始化:用服务端快照四货币 + 六计数器权威值覆盖本地视图(<see cref="MergeOrderState"/> 字段)+ 对齐基线。
        /// 验收:登录后四货币 + 六计数器显示 = 服务端快照值(非本地旧值/blob 旧值);此后基线锚到服务端值,后续产销 delta 从此基准算。
        /// state 为 null(玩法窗未开)时仅记基线,待开窗 ImportMeta 后由 <see cref="RebindBaseline"/> 重对齐。
        /// 六计数器与四货币同处理:登录快照是唯一初值源,本地缓存只作开窗前投影兜底(云存档通道已整体退役)。
        /// </summary>
        public void ApplySnapshot(MergeOrderState state, long soul, long piety, long exp, long energy,
            long goddessLevel, long goddessRating, long unlockedChapter,
            long blindBoxCount, long templeRepaired, long nextRepairIndex)
        {
            _baseSoul = soul;
            _basePiety = piety;
            _baseExp = exp;
            _baseEnergy = energy;
            _baseGoddessLevel = goddessLevel;
            _baseGoddessRating = goddessRating;
            _baseUnlockedChapter = unlockedChapter;
            _baseBlindBoxCount = blindBoxCount;
            _baseTempleRepaired = templeRepaired;
            _baseNextRepairIndex = nextRepairIndex;
            IsReady = true;

            if (state == null) return;
            state.Soul = (int)soul;
            state.Piety = (int)piety;
            state.Exp = (int)exp;
            state.Energy = (int)energy;
            state.GoddessLevel = (int)goddessLevel;
            state.GoddessRating = (int)goddessRating;
            state.UnlockedChapter = (int)unlockedChapter;
            state.BlindBoxCount = (int)blindBoxCount;
            SetTempleRepairedCount(state, templeRepaired);
            state.NextRepairIndex = (int)nextRepairIndex;
        }

        /// <summary>
        /// 重对齐基线到给定 state 当前值(不上报、不改 state)。用于玩法窗 ImportMeta 之后:本地从缓存读出的四货币
        /// 是「上次离线时的服务端权威快照」,把基线钉到它,使首次 ReportPending 只上报「本会话开窗后的真实增量」,
        /// 不把缓存值当成新产出重报。
        ///
        /// 未 Ready(登录快照未到)直接跳过:此时活态 Energy 只是本地缓存值,尚无服务端权威值可锚。
        /// 对齐交由随后到达的 <see cref="ApplySnapshot"/> 接管——它同时设基线 + 覆盖窗已开的活态字段。
        /// </summary>
        public void RebindBaseline(MergeOrderState state)
        {
            if (state == null || !IsReady) return;
            _baseSoul = state.Soul;
            _basePiety = state.Piety;
            _baseExp = state.Exp;
            _baseEnergy = state.Energy;
            _baseGoddessLevel = state.GoddessLevel;
            _baseGoddessRating = state.GoddessRating;
            _baseUnlockedChapter = state.UnlockedChapter;
            _baseBlindBoxCount = state.BlindBoxCount;
            _baseTempleRepaired = TempleRepairedCount(state);
            _baseNextRepairIndex = state.NextRepairIndex;
            // 开窗时 ApplyTimeRegen 可能已把离线/在窗恢复累计进 RegenSinceReport,而该恢复量已包含在上面钉入的
            // Energy 基线里。若不清零,首次 ReportPending 会再把它并入基线(_baseEnergy += RegenSinceReport),
            // 使基线超出活态值、算出等额负 delta(又一次虚假扣体力)。基线已含恢复 → 此处一并清零,二者同源对齐。
            state.RegenSinceReport = 0;
        }

        /// <summary>
        /// 在玩法事件边界(落盘时)把四货币本地净变化各聚合成一笔上报(防频率拦截)。
        /// 对每种货币算 delta = 当前 state 值 - 基线,非零才发一笔 <c>C2G_PropertyChangeRequest</c>;
        /// 成功/NotEnough/OverLimit 用服务端 NewAmount 校正本地字段 + 基线;其它失败(网络/服务不可用)不动视图、不动基线
        /// (待下次边界重试,delta 仍在——未对账即未消费)。
        ///
        /// 【体力预测恢复排除】上报 Energy 前先把 <see cref="MergeOrderState.RegenSinceReport"/>(本地预测恢复累计量)
        /// 并入 Energy 基线并清零:恢复是服务端懒结算的显示预测,不可作为客户端产出上报(否则双重发体力,P2 §4)。
        /// 故 Energy 实际上报 delta = (当前体力 - 基线) - 预测恢复量 = 仅玩法真实扣/奖(落子扣 / 交付奖 / 修缮奖 / 祈愿兑 / 开盒)。
        ///
        /// 防重入:一次往返在途时直接返回(下次落盘边界会再聚合,delta 累计不丢)。未 Ready(登录快照未到)不上报。
        /// reasonPrefix 便于服务端 Log 辨识来源(如 "merge_event")。即发即忘 UniTask,不阻塞玩法。
        /// </summary>
        public async UniTask ReportPending(MergeOrderState state, string reasonPrefix)
        {
            if (state == null || !IsReady || _reporting) return;
            _reporting = true;
            try
            {
                // 体力预测恢复并入基线 + 清零(在算 Energy delta 之前),把恢复量排除出待上报量。
                if (state.RegenSinceReport != 0)
                {
                    _baseEnergy += state.RegenSinceReport;
                    state.RegenSinceReport = 0;
                }

                await ReportOne(state, AttrType.SoulPower, () => state.Soul, v => state.Soul = (int)v,
                    () => _baseSoul, v => _baseSoul = v, reasonPrefix);
                await ReportOne(state, AttrType.Piety, () => state.Piety, v => state.Piety = (int)v,
                    () => _basePiety, v => _basePiety = v, reasonPrefix);
                await ReportOne(state, AttrType.GuardianExp, () => state.Exp, v => state.Exp = (int)v,
                    () => _baseExp, v => _baseExp = v, reasonPrefix);
                await ReportOne(state, AttrType.Energy, () => state.Energy, v => state.Energy = (int)v,
                    () => _baseEnergy, v => _baseEnergy = v, reasonPrefix);

                // 六元层计数器(与四货币同「当前值 - 基线」净 delta 上报、NewAmount 覆盖):
                await ReportOne(state, AttrType.GoddessLevel, () => state.GoddessLevel, v => state.GoddessLevel = (int)v,
                    () => _baseGoddessLevel, v => _baseGoddessLevel = v, reasonPrefix);
                await ReportOne(state, AttrType.GoddessRating, () => state.GoddessRating, v => state.GoddessRating = (int)v,
                    () => _baseGoddessRating, v => _baseGoddessRating = v, reasonPrefix);
                await ReportOne(state, AttrType.UnlockedChapter, () => state.UnlockedChapter, v => state.UnlockedChapter = (int)v,
                    () => _baseUnlockedChapter, v => _baseUnlockedChapter = v, reasonPrefix);
                await ReportOne(state, AttrType.BlindBoxCount, () => state.BlindBoxCount, v => state.BlindBoxCount = (int)v,
                    () => _baseBlindBoxCount, v => _baseBlindBoxCount = v, reasonPrefix);
                // 神庙修缮计数:活态是布尔数组,上报/对账用其「已修 true 项数」标量(顺序解锁,恒 = NextRepairIndex)。
                await ReportOne(state, AttrType.TempleRepaired, () => TempleRepairedCount(state), v => SetTempleRepairedCount(state, v),
                    () => _baseTempleRepaired, v => _baseTempleRepaired = v, reasonPrefix);
                await ReportOne(state, AttrType.NextRepairIndex, () => state.NextRepairIndex, v => state.NextRepairIndex = (int)v,
                    () => _baseNextRepairIndex, v => _baseNextRepairIndex = v, reasonPrefix);
            }
            finally
            {
                _reporting = false;
            }
        }

        /// <summary>
        /// 单货币上报 + 对账(纯逻辑核,无 Unity 依赖)。delta=0 直接跳过(不发空请求)。
        /// 成功/NotEnough/OverLimit:服务端 NewBalance 可信 → 写回字段 + 基线(两端一致)。
        /// 其它码(NetworkDown/ServiceUnavailable/NotLoggedIn/TypeUnknown):不动字段、不动基线(delta 留到下次边界重报)。
        /// </summary>
        private async UniTask ReportOne(MergeOrderState state, AttrType type,
            Func<long> getValue, Action<long> setValue,
            Func<long> getBase, Action<long> setBase, string reasonPrefix)
        {
            long delta = getValue() - getBase();
            if (delta == 0) return;

            string reason = reasonPrefix == null ? type.ToString() : $"{reasonPrefix}_{type}";
            var result = await _gateway.SendChangeRequestAsync(type, delta, reason);

            if (result.Reason == ChangeReject.None
                || result.Reason == ChangeReject.NotEnoughBalance
                || result.Reason == ChangeReject.TypeUpperOverflow)
            {
                // 服务端权威 NewBalance:覆盖本地视图 + 基线(NotEnough/OverLimit 即回滚乐观值到真值)。
                setValue(result.NewBalance);
                setBase(result.NewBalance);
            }
            // 其它失败:本地视图与基线都不动 → delta 仍 = 当前值 - 基线,下次 ReportPending 自然重报(未对账=未消费)。
        }

        /// <summary>
        /// 消除道具体力「服务端派生」防双扣:在乐观扣体力(<see cref="MergeOrderState.SpendClearToolCost"/>)的<b>同一同步边界</b>
        /// 把该笔扣减一并抬进 Energy 基线(<c>_baseEnergy += signedDelta</c>,扣为负),使这笔乐观扣减从 <see cref="ReportPending"/> 的
        /// 待上报 delta 中排除、净算为 0——服务端 ClearTool 已权威扣过一次,客户端<b>不得</b>再经 <c>C2G_PropertyChange</c> 重报。
        ///
        /// 时序竞态根治:消除道具乐观扣与后续落盘边界(<c>OnSaved→ReportPending</c>)、服务端 <c>NewEnergy</c> 回带 / <c>G2C_PropertyDeltaPush</c>
        /// 到达三者异步交错。若只依赖响应到达后 <see cref="ApplyDeltaPush"/> 校正,而 ReportPending 抢先在响应前跑,就会把 -cost 当净产出上报
        /// → 服务端双扣。故本方法在<b>扣减发生的同步瞬间</b>就把基线抬平,无论 ReportPending 何时跑,该笔 delta 恒为 0;真实权威值随后由
        /// <see cref="ApplyDeltaPush"/>(响应 NewEnergy 或 delta-push)整体对齐(幂等,重复 set 同值无害)。
        ///
        /// 与 <see cref="MergeOrderState.RegenSinceReport"/> 同源(「已并入活态值、须从上报排除的累加器」),但恢复走 state 字段累积、
        /// 于 ReportPending 内并入;消除道具扣减单笔即时、直接抬基线,故独立一入口。回滚(NotEnoughEnergy 拒绝)时以相反符号再调一次撤销。
        /// 未 Ready(登录快照未到)直接跳过:此刻无权威基线可锚,交随后快照接管。
        /// </summary>
        /// <param name="signedDelta">带符号的本地乐观变更量(消除道具扣体力为 <c>-cost</c>;回滚撤销传 <c>+cost</c>)。</param>
        public void ExcludeEnergySpend(long signedDelta)
        {
            if (!IsReady) return;
            _baseEnergy += signedDelta;
        }

        /// <summary>
        /// 应用服务端主动推送(G2C_PropertyDeltaPush)到四货币之一:按 type 直接 set 本地字段 + 基线为权威 NewAmount。
        /// type 非四货币(Coin/Diamond/Stamina/All)直接忽略(那些由 PlayerAttrService 处理)。state 为 null 仅更基线。
        ///
        /// 命中四货币且 state 非空时置「货币 push 脏标记」(<see cref="MergeOrderState.MarkCurrencyPushed"/>):push 异步晚于
        /// 交付同步流程到达,本地字段虽已 set 但冻结 UI 不会自动重绘(每秒轮询仅在时基恢复改变了体力时才刷)。置脏后由
        /// 玩法窗每秒轮询经 <see cref="MergeOrderState.ConsumeCurrencyPushed"/> 触发货币 HUD 重绘,使交付奖励(体力 +8、虔诚币等)即时显示。
        /// </summary>
        public void ApplyDeltaPush(MergeOrderState state, AttrType type, long newAmount)
        {
            bool hit = false;
            switch (type)
            {
                case AttrType.SoulPower:   _baseSoul = newAmount;   if (state != null) state.Soul = (int)newAmount; hit = true; break;
                case AttrType.Piety:       _basePiety = newAmount;  if (state != null) state.Piety = (int)newAmount; hit = true; break;
                case AttrType.GuardianExp: _baseExp = newAmount;    if (state != null) state.Exp = (int)newAmount; hit = true; break;
                case AttrType.Energy:      _baseEnergy = newAmount; if (state != null) state.Energy = (int)newAmount; hit = true; break;
                // 六计数器:set 本地字段 + 基线(绝对值覆盖,幂等)。不触发四货币 HUD 脏标记(那是 Soul/Piety/Exp/Energy 专用刷新入口);
                // 计数器 UI(女神/盲盒/神庙)由各自玩法事件重绘,delta-push 主要保基线对齐、免下次误报。
                case AttrType.GoddessLevel:    _baseGoddessLevel = newAmount;    if (state != null) state.GoddessLevel = (int)newAmount; break;
                case AttrType.GoddessRating:   _baseGoddessRating = newAmount;   if (state != null) state.GoddessRating = (int)newAmount; break;
                case AttrType.UnlockedChapter: _baseUnlockedChapter = newAmount; if (state != null) state.UnlockedChapter = (int)newAmount; break;
                case AttrType.BlindBoxCount:   _baseBlindBoxCount = newAmount;   if (state != null) state.BlindBoxCount = (int)newAmount; break;
                case AttrType.TempleRepaired:  _baseTempleRepaired = newAmount;  if (state != null) SetTempleRepairedCount(state, newAmount); break;
                case AttrType.NextRepairIndex: _baseNextRepairIndex = newAmount; if (state != null) state.NextRepairIndex = (int)newAmount; break;
                // 其它 type(Coin/Diamond/Stamina/All):非本类职责,忽略
            }
            if (hit && state != null) state.MarkCurrencyPushed();
        }

        // ── 神庙修缮计数 标量 ↔ 布尔数组 转换 ────────────────────────
        // 服务端 PropertyType.TempleRepaired 是「已修厅数」标量;客户端活态 MergeOrderState.TempleRepaired 是布尔数组。
        // 顺序解锁不变量(厅 i 可修 ⟺ i==NextRepairIndex,修完 NextRepairIndex=i+1)保证已修集恒为前缀 [0, count),
        // 故标量 = true 项数,反向 set 时把前 count 项置 true、其余 false。二者与 NextRepairIndex 同源同步,不会漂移。

        /// <summary>取活态已修厅数(布尔数组 true 项数)。state/数组为 null 返 0。</summary>
        private static long TempleRepairedCount(MergeOrderState state)
        {
            var arr = state?.TempleRepaired;
            if (arr == null) return 0;
            int n = 0;
            for (int i = 0; i < arr.Length; i++) if (arr[i]) n++;
            return n;
        }

        /// <summary>
        /// 按权威已修厅数覆盖活态布尔数组:前 <paramref name="count"/> 项置 true、其余 false(顺序解锁前缀语义)。
        /// count 夹到 [0, 数组长];数组为 null(未初始化)按 TempleConfig.HallCount 新建。
        /// </summary>
        private static void SetTempleRepairedCount(MergeOrderState state, long count)
        {
            if (state == null) return;
            var arr = state.TempleRepaired;
            if (arr == null)
            {
                arr = new bool[GameLogic.BlockBlast.TempleConfig.HallCount];
                state.TempleRepaired = arr;
            }
            int c = count < 0 ? 0 : (count > arr.Length ? arr.Length : (int)count);
            for (int i = 0; i < arr.Length; i++) arr[i] = i < c;
        }
    }
}
