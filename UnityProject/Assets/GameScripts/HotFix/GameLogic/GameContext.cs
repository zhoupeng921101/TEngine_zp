using Cysharp.Threading.Tasks;
using GameLogic.Activity;
using GameLogic.AttrLedger;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Player;
using GameLogic.Mail;
using GameLogic.Rank;
using GameLogic.Settings;

namespace GameLogic
{
    /// <summary>
    /// 运行期通用服务上下文（单例，设计 23 §五 方案 B）。
    /// 持有「数据层已建、需运行期持有者」的无主系统服务，提供统一初始化 + 统一接存档接缝。
    /// 玩法态（棋盘 / 得分）仍归 <see cref="BlockGameState"/>，二者分层：
    /// 玩法态随开局 Reset，通用服务随会话长存。
    /// </summary>
    /// <remarks>
    /// 持有 <see cref="SettingsService"/>（设计 23）+ <see cref="PlayerInfo"/>（设计 25 兑现）+ <see cref="RankService"/>（设计 28 兑现末项）；
    /// item / mail 后续逐个挂入，接口预留薄而通用，不投机性预建成员。
    /// 启动接线（AudioSink + 首次 Load）在热更入口 <c>GameApp.StartGameLogic()</c> 完成
    /// （非热更区 ProcedureLaunch 引用不到本类，热更边界所致，设计 23 §五接线落点）。
    /// </remarks>
    public sealed class GameContext : SimpleSingleton<GameContext>
    {
        /// <summary>设置服务（音频开关 + 持久化 + 信息 getter，设计 19 数据层）。</summary>
        public SettingsService Settings { get; private set; }

        /// <summary>玩家个人信息（昵称 / 头像 / 等级 / 解锁集，设计 18 数据层）。</summary>
        public PlayerInfo Player { get; private set; }

        /// <summary>排行榜服务（查榜 / 我的名次 / 每日 + 点赞领取 / 红点查询,设计 22 数据层;结算编排上移服务端,设计 33)。</summary>
        public RankService Rank { get; private set; }

        /// <summary>远程邮件服务（运营来源拉列表 + 领奖走服务端校验，设计 32 客户端段）。</summary>
        public RemoteMailService Mail { get; private set; }

        /// <summary>玩家元层属性服务(Coin/Diamond/Stamina 客户端账本视图,设计 38 客户端段)。</summary>
        public PlayerAttrService PlayerAttr { get; private set; }

        /// <summary>改名 RPC 接缝(改名服务端权威·客户端段):发 C2G_RenameRequest,服务端一次原子算费/扣钻/写名/计数。</summary>
        public IRenameGateway RenameGateway { get; private set; }

        /// <summary>头像/框修饰编排器(头像服务端权威·客户端段):换装乐观 set + 服务端对账、解锁 client-report、登录 bootstrap 上报差集。</summary>
        public CosmeticService Cosmetic { get; private set; }

        /// <summary>四玩法货币(Soul/Piety/Exp/Energy)本地视图 ↔ 服务端权威对账器(P2 全栈迁移·客户端段)。</summary>
        public MetaCurrencySync MetaCurrency { get; private set; }

        /// <summary>祈愿(每日限领体力)服务端权威编排器(祈愿服务端权威·客户端段):发 C2G_WishForEnergyRequest,响应权威值经对账器应用为投影。</summary>
        public WishService Wish { get; private set; }

        /// <summary>档案状态(皮肤态 + 神庙装饰厅数)服务端权威上报编排器(皮肤/神庙装饰服务端权威·客户端段):变更后全量 SET 上报,fire-and-forget。</summary>
        public ProfileStateSync ProfileState { get; private set; }

        /// <summary>道具背包投影(服务端道具持有 PlayerDoc.ItemHoldings 的客户端投影;当前业务方 = 塔罗碎片)。</summary>
        public GameLogic.BlockBlast.Item.ItemBag Items { get; private set; }

        /// <summary>塔罗牌收集投影(已合成集合 + 合成 RPC 编排;碎片进度由 UI 按 TbTarotCard 查 Items 现算)。</summary>
        public TarotCollection Tarot { get; private set; }

        /// <summary>normal 订单本地视图 ↔ 服务端权威投影器(P1 全栈迁移·客户端段:登录快照/推送应用 + 交付 RPC 编排)。</summary>
        public OrderSync OrderSync { get; private set; }

        /// <summary>进主游戏编排(全栈协议改动·客户端段):进融合主游戏时发一次 C2G_EnterMainGameRequest,把订单快照应用到投影。</summary>
        public EnterMainGameSync EnterMainGame { get; private set; }

        /// <summary>服务端权威发牌预测/对账引擎(M3 客户端段:开局 C2G_GameStart / 落子 C2G_Place 预测对账 / 重连 C2G_GameSnapshot)。</summary>
        public ServerDealSync ServerDeal { get; private set; }

        /// <summary>远程 ledger 服务(我的流水查询,设计 46 客户端段)。</summary>
        public RemoteAttrLedgerService AttrLedger { get; private set; }

        /// <summary>远程活动服务(累计 N 局类活动 fire-and-forget +1,设计 48 客户端段)。</summary>
        public RemoteActivityService Activity { get; private set; }

        protected override void OnInit()
        {
            // 生产用框架键存储（PlayerPrefs），启动即从已保存的开关态加载。
            Settings = new SettingsService(new PlayerPrefsSettingsStore());
            Settings.Load();

            // 玩家信息：从既有存档 DTO 同步加载（设计 25 §5.2 B1）。
            // MergeMetaPersistence.Load 经 Persistence.Provider 同步读（PlayerPrefs 非阻塞内存级读，
            // 不触「禁阻塞 IO」红线，与 BlockGameState.Load 同口径），故不需异步外壳、无与异步加载的时序问题。
            LoadPlayer();

            // 排行榜服务（设计 22 数据层，设计 28 §四装配）。
            InitRank();

            // 远程邮件服务（设计 32 客户端段）：运营来源拉列表 + 领奖走服务端校验。
            InitMail();

            // 玩家元层属性服务(设计 38 客户端段):生产用 RpcGatewayProd(经 FantasyNetwork.Session 发协议);
            // FantasyNetwork.On* 事件订阅在 GameApp.StartGameLogic 内挂(GameContext 不直接 using FantasyClient,
            // 沿设计 38 §五接线落点;Fantasy 程序集受 FANTASY_UNITY 约束,事件订阅须在 #if 内)。
            PlayerAttr = new PlayerAttrService(new RpcGatewayProd());

            // 改名 RPC 接缝(改名服务端权威·客户端段):生产用 RenameGatewayProd(经 Session 发 C2G_RenameRequest);
            // 费用/扣钻/写名/计数服务端一次原子做完,客户端不再本地扣钻。响应回带最新 Nickname/RenameCount/Diamond 供对齐。
            RenameGateway = new RenameGatewayProd();

            // 头像/框修饰编排器(头像服务端权威·客户端段):生产用 CosmeticGatewayProd(经 Session 发 C2G_EquipCosmetic/C2G_UnlockCosmetic);
            // 换装乐观 set + 服务端对账、解锁 client-report、登录 bootstrap 上报差集。snapshot 覆盖本地投影 + bootstrap 接线在 GameApp.StartGameLogic。
            Cosmetic = new CosmeticService(new CosmeticGatewayProd());

            // 四货币对账器(P2 客户端段):复用同一 RPC 接缝(RpcGatewayProd 经 Session 发 C2G_PropertyChangeRequest);
            // 登录快照 → ApplySnapshot 覆盖本地视图 + 基线;落盘边界 → ReportPending 聚合上报。接线在 GameApp.StartGameLogic。
            MetaCurrency = new MetaCurrencySync(new RpcGatewayProd());

            // 祈愿服务端权威编排器(祈愿服务端权威·客户端段):生产用 WishGatewayProd(经 Session 发 C2G_WishForEnergyRequest);
            // 祈愿非乐观、等响应,把回带的权威 Soul/Energy 经 MetaCurrency.ApplyDeltaPush 应用(不本地预扣、免双减),WishUsedToday 投影 set。
            Wish = new WishService(new WishGatewayProd(), MetaCurrency);

            // 档案状态服务端权威上报编排器(皮肤/神庙装饰服务端权威·客户端段):生产用 ProfileStateGatewayProd(经 Session 发 C2G_SetProfileStateRequest);
            // 皮肤切换 / 神庙装饰乐观本地变更后,取当前三态全量 SET 上报,fire-and-forget(失败下次变更再报 / 登录快照对齐)。
            // snapshot 覆盖本地投影接线在 GameApp.StartGameLogic。
            ProfileState = new ProfileStateSync(new ProfileStateGatewayProd());

            // 道具背包投影(塔罗收集·客户端段):服务端持有是唯一事实源,本容器经进主游戏快照整份覆盖 +
            // 交付/合成响应权威余额对账保鲜;本地丢失可由服务端重建(可丢缓存),不接存档。
            Items = new GameLogic.BlockBlast.Item.ItemBag();

            // 塔罗收集投影(塔罗收集·客户端段):生产用 TarotRpcGatewayProd(经 Session 发 C2G_TarotSynthesizeRequest);
            // 已合成集合由进主游戏快照 / 合成响应整份覆盖;合成成功的碎片扣减经注入的 Items 对齐。
            Tarot = new TarotCollection(new TarotRpcGatewayProd(), Items);

            // 订单服务端权威投影器(P1 客户端段):生产用 OrderRpcGatewayProd(经 Session 发 C2G_DeliverOrderRequest);
            // 登录/刷新推送 → OnSnapshotPush 应用快照;开窗 → OnMergeStateReady 切权威 + 接交付钩子。接线在 GameApp.StartGameLogic。
            // 注入 MetaCurrency:交付成功后按响应回带的权威绝对余额对账 Energy/Piety(取代原对发起方的 delta-push);
            // 注入 Items:交付掉落的塔罗碎片按响应权威余额对齐背包计数。
            OrderSync = new OrderSync(new OrderRpcGatewayProd(), MetaCurrency, Items);

            // 进主游戏编排(全栈协议改动·客户端段):生产用 EnterMainGameGatewayProd(经 Session 发 C2G_EnterMainGameRequest);
            // 进融合主游戏(MainMenuWindow 开始游戏)时发请求,响应回带订单快照 → OrderSync 统一应用,
            // 道具持有 → Items 整份覆盖,塔罗收集 → Tarot 整份覆盖。
            // (局内 cosmetic + 合成经济叠加层改经 C2G_GameStart/GameSnapshot 的 SliceJson 收发,不再走进主游戏回带的云存档 blob。)
            EnterMainGame = new EnterMainGameSync(new EnterMainGameGatewayProd(), OrderSync, Items, Tarot);

            // 服务端权威发牌预测/对账引擎(M3 客户端段):生产用 BlockGameGatewayProd(经 Session 发 C2G_GameStart/Place/GameSnapshot);
            // 开窗经 OnMergeStateReady 注入到 BlockGameState.ServerDeal,接线在 GameApp.StartGameLogic。
            ServerDeal = new ServerDealSync(new BlockGameGatewayProd());

            // 远程 ledger 服务(设计 46 客户端段):生产用 RemoteAttrLedgerSource(经 FantasyNetwork.Session 发 C2G_QueryAttrLedger);
            // 服务端独占审计完整性(44 §5.4),客户端不持本地副本,每次打开窗实时拉真协议。
            AttrLedger = new RemoteAttrLedgerService(new RemoteAttrLedgerSource());

            // 远程活动服务(设计 48 客户端段):生产用 RemoteActivityIncrementSource(经 FantasyNetwork.Session 发 C2G_ActivityIncrement);
            // GameOver hook fire-and-forget 调,服务端 counter $inc 原子幂等;客户端不持本地状态(沿设计 48 §3.3 不本地放行)。
            Activity = new RemoteActivityService(new RemoteActivityIncrementSource());
        }

        /// <summary>
        /// 装配远程邮件服务（生产接缝：远程来源 <see cref="RemoteMailSource"/>，设计 32 客户端段）。
        /// 运营邮件来源唯一在服务端（设计 32 读前必看第 1 条），客户端不持第二份运营来源；
        /// 领奖走服务端校验（防重 + 抽奖 + 过期），断服不本地放行（设计 32 §四）。
        /// </summary>
        private void InitMail()
        {
            Mail = new RemoteMailService(new RemoteMailSource());
        }

        /// <summary>
        /// 装配排行榜服务（生产接缝：本地源 + 生产持久化 + 邮件服务 + 默认配置源，设计 28 §四）。
        /// 本机成绩来自 <see cref="RankService.GetMyBest"/>（自身进度），陪榜由配置 / 注入基准分（无随机 NPC，设计 22 §3.6）；
        /// 本轮 filler=null，离线榜可能只本机一条（陪榜待运营内容，设计 28 §十一 BLK1）。
        /// </summary>
        /// <remarks>
        /// selfProvider 存在「服务引用源、源引用服务」的循环，用「先声明 svc、闭包捕获、后赋值」打破
        /// （同设计 22 测试 SK1 / 持久化往返用例写法）。邮件服务取真实生产实例
        /// <c>new MailboxService(new MailPersistence())</c>(设计 21 数据层的本机收件接口,设计 28 §四 B1 路②):
        /// 排名层每日 / 点赞奖经它真实下发进收件箱存档(结算奖经设计 33 服务端发奖入口投出,不走此接口);
        /// 邮件表现层落地后此处零改动复用同一持久化键 <c>Mail.Inbox</c>,奖即在邮件窗可见(MailboxService 类头已声明此接法)。
        /// </remarks>
        private void InitRank()
        {
            var persist = new RankPersistence();                       // 键 Rank.Progress，复用 Persistence.Provider
            var mail = new MailboxService(new MailPersistence());      // 真实发奖出口（键 Mail.Inbox，设计 21）
            RankService svc = null;
            var source = new LocalRankSource(
                id => svc.GetMyBest(id),                               // 本机成绩（闭包捕获后赋值的 svc）
                _ => null);                                            // 陪榜：本轮无（待运营内容，设计 28 §十一 BLK1）
            var remote = new RemoteRankSource();                       // 远程源（上后端，设计 31）：发上报 / 查榜 RPC、服务端权威排序
            // cfg 默认包 RankConfigMgr（运行期走 ConfigSystem）；remote 非 null → 异步入口优先 RPC、断服回退 source（设计 31 §四）。
            svc = new RankService(source, persist, mail, cfg: null, remote: remote);
            Rank = svc;
        }

        /// <summary>
        /// 从既有 <see cref="MergeMetaSave"/> 存档同步重建 <see cref="Player"/> + 加载经典最高分（设计 29 §5.4）：
        /// 有档 → <see cref="PlayerInfo.ImportFromMeta"/>（逐字段保底夹值，头像 id 越界退默认）；
        /// 无档 → <see cref="PlayerInfo.CreateDefault"/>（新 id + 系统名 + 默认头像/框）。
        /// 同一份 DTO 同时把 <see cref="BlockGameState.HighScore"/> 并入元层加载（与玩家信息同时机）。
        /// </summary>
        private void LoadPlayer()
        {
            var rng = new System.Random();
            var dto = MergeMetaPersistence.Load();
            Player = (dto != null)
                ? PlayerInfo.ImportFromMeta(dto, rng, avatarValid: AvatarIdValid)
                : PlayerInfo.CreateDefault(rng);
            // 经典最高分并入元层时机（设计 29 §5.4）：与玩家信息同读一份 DTO；无档则保持缺省（首次无最高分）。
            BlockGameState.Instance.ImportHighScoreFromMeta(dto);
        }

        /// <summary>
        /// 头像 id 是否在配置表内（接 <see cref="GameLogic.Config.AvatarConfigMgr"/>）。
        /// 配置经 ConfigSystem / YooAsset 加载，若启动时尚未就绪 / 表缺失会抛——此处吞异常退「视作有效」，
        /// 不因配置时序卡死启动（保底夹值的越界退默认是「正确性兜底」，配置不可达时宁可保留原 id）。
        /// </summary>
        private static bool AvatarIdValid(int id)
        {
            try { return GameLogic.Config.AvatarConfigMgr.GetAvatar(id) != null; }
            catch { return true; }
        }

        /// <summary>
        /// 落地服务端登录签发/认领的权威 playerId（P0 全栈迁移·客户端段）：用服务端值覆盖内存 <see cref="Player"/>.Id，
        /// 并经 <see cref="SavePlayer"/> 持久化到 <see cref="MergeMetaSave.playerId"/>。此后会话内 <see cref="Player"/>.Id 一律以服务端值为准。
        /// </summary>
        /// <remarks>
        /// 调用方为 <c>GameApp.StartGameLogic</c> 订阅 <c>FantasyNetwork.OnPlayerIdIssued</c>（仅 ErrorCode==0 且非空时触发）。
        /// 落盘判据锚到「磁盘上的 playerId」而非内存 <see cref="Player"/>.Id：首装时内存占位 guid 恰与服务端认领值相同，
        /// 但该 guid 尚未落盘（<see cref="LoadPlayer"/> 走 CreateDefault 仅置内存）；只比内存会漏掉这次落盘，
        /// 导致重启时盘上无 playerId、被迫重生成新 guid 再上交（虽最终仍收敛回服务端值，但违「首登后盘上即为服务端值」验收）。
        /// 故只要磁盘值 != 服务端值（含磁盘缺失），即覆盖内存并落盘；磁盘已是服务端值则跳过冗余写盘。
        /// Player 为 null（OnInit 尚未跑）时直接返回——实际时序中登录回包远晚于 OnInit，此守卫仅兜底。
        /// </remarks>
        public void ApplyServerPlayerId(string serverPlayerId)
        {
            if (Player == null || string.IsNullOrEmpty(serverPlayerId)) return;
            Player.Id = serverPlayerId; // 内存权威无条件以服务端值为准
            // 磁盘已是服务端值 → 跳过冗余落盘；否则（含磁盘缺 playerId 字段 / 无档）落盘。
            var dto = MergeMetaPersistence.Load();
            if (dto != null && dto.playerId == serverPlayerId) return;
            SavePlayer(fireSavedHook: false); // 回灌写:身份是服务端回灌值,不触发存档边界钩子(避免无意义空跑上报)
        }

        /// <summary>
        /// 落地服务端登录四货币 + 六元层计数器权威快照(全栈迁移·客户端段)：用服务端值覆盖本地缓存 + 对账器基线 + 已开的玩法态。
        /// 验收:登录后四货币 + 六计数器显示 = 服务端快照值(非本地旧值/blob 旧值);单纯改本地 PlayerPrefs 重登录被服务端覆盖。
        /// </summary>
        /// <remarks>
        /// 三处一并覆盖,保证后续任一读取路径都拿服务端值:
        /// ① 缓存 <see cref="MergeMetaSave"/>(soul/piety/exp/energy + 六计数器字段):玩法窗稍后开窗经 ImportMeta 从缓存读,
        ///    故必须先把权威值写进缓存,否则开窗会用本地旧缓存覆盖显示;
        /// ② 对账器 <see cref="MetaCurrency"/> 基线:钉到服务端值,后续产销 delta 从此基准算;
        /// ③ 若玩法窗已开(MergeState 非空,如挂后台登录重连),直接覆盖活态字段,即时反映。
        /// 体力字段连带把 lastEnergyRegenTime 置 now:服务端值是「此刻权威体力」,本地预测恢复应从此刻起算
        /// (避免用旧记录时刻把已结算过的离线恢复再补一遍)。
        /// 六计数器与四货币同处理:登录快照是权威初值源,缓存只作开窗前投影兜底(云存档通道已整体退役)。
        /// 神庙已修厅数 <paramref name="templeRepaired"/> 是标量,写进缓存的布尔数组 templeRepaired 取「前 N 项 true」(顺序解锁前缀)。
        /// </remarks>
        public void ApplyServerCurrencySnapshot(long soul, long piety, long exp, long energy,
            long goddessLevel, long goddessRating, long unlockedChapter,
            long blindBoxCount, long templeRepaired, long nextRepairIndex)
        {
            var live = BlockGameState.Instance;
            var state = (live != null && live.MergeOrderMode) ? live.MergeState : null;

            // ① + ② 经对账器:set 基线,若活态在则一并覆盖活态字段。
            MetaCurrency?.ApplySnapshot(state, soul, piety, exp, energy,
                goddessLevel, goddessRating, unlockedChapter, blindBoxCount, templeRepaired, nextRepairIndex);

            // ③ 缓存:把四货币 + 六计数器权威值写进 MergeMetaSave(保留其余元层字段),供稍后开窗 ImportMeta 读取。
            //    energy 连带把 lastEnergyRegenTime 置 now(权威体力从此刻起算本地预测恢复)。
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            dto.soul = (int)soul;
            dto.piety = (int)piety;
            dto.exp = (int)exp;
            dto.energy = (int)energy;
            dto.lastEnergyRegenTime = MergeMetaPersistence.NowUnixSec();
            dto.goddessLevel = (int)goddessLevel;
            dto.goddessRating = (int)goddessRating;
            dto.unlockedChapter = (int)unlockedChapter;
            dto.blindBoxCount = (int)blindBoxCount;
            dto.nextRepairIndex = (int)nextRepairIndex;
            dto.templeRepaired = BuildTempleRepairedArray(templeRepaired);
            // 回灌写:货币 + 六计数器 + lastEnergyRegenTime 是服务端回灌值,非玩法事件边界,
            // 触发存档钩子只会空跑一次货币聚合上报,故 fireSavedHook=false 只落本地缓存、不惊动上报。
            MergeMetaPersistence.SaveAsync(dto, fireSavedHook: false).Forget();
        }

        /// <summary>
        /// 落地服务端登录头像/框权威快照(头像服务端权威·客户端段):用服务端当前佩戴 + 解锁集覆盖本地投影 <see cref="Player"/>。
        /// 登录快照是唯一初值源;本地投影只作开窗前兜底(云存档通道已整体退役)。
        /// 解锁集缺省(首登服务端空集)→ 用服务端值原样覆盖(空数组);随后 bootstrap 上报默认解锁补齐。
        /// 当前 id 缺省 1/101 与客户端默认一致,登录不闪默认头像。
        /// </summary>
        /// <remarks>
        /// 覆盖本地投影而非另建载体:<see cref="AvatarUnlockService"/> / <see cref="PlayerInfoWindow"/> 换装 / 显示 均读 <see cref="Player"/>,
        /// 把服务端权威值写进 <see cref="Player"/> 即让所有读取路径拿服务端投影,乐观变更由 <see cref="CosmeticService"/> 对账回写。
        /// 服务端当前 id ≤ 0(异常缺省)时退客户端默认,保显示不空。
        /// </remarks>
        public void ApplyServerCosmeticSnapshot(int currentAvatarId, int currentFrameId,
            int[] unlockedAvatarIds, int[] unlockedFrameIds)
        {
            if (Player == null) return;
            Player.CurrentAvatarId = currentAvatarId > 0 ? currentAvatarId : PlayerInfo.DefaultAvatarId;
            Player.CurrentFrameId = currentFrameId > 0 ? currentFrameId : PlayerInfo.DefaultFrameId;
            Player.UnlockedAvatarIds = unlockedAvatarIds ?? System.Array.Empty<int>();
            Player.UnlockedFrameIds = unlockedFrameIds ?? System.Array.Empty<int>();
        }

        /// <summary>
        /// 落地服务端登录祈愿每日态权威快照(祈愿服务端权威·客户端段):把今日已用次数写进本地缓存 + 已开的玩法态投影。
        /// 客户端 <see cref="MergeOrderState.WishUsedToday"/> 降为投影,唯一初值源 = 服务端快照(懒重置后当日值),不再本地跨天重置。
        /// 登录快照是唯一初值源,缓存只作开窗前投影兜底(云存档通道已整体退役)。
        /// </summary>
        /// <remarks>
        /// 两处一并覆盖(仿 <see cref="ApplyServerCurrencySnapshot"/>):
        /// ① 缓存 <see cref="MergeMetaSave.wishUsedToday"/> + lastWishResetDate(置 today):供稍后开窗 ImportMeta 从缓存读投影;
        /// ② 若玩法窗已开(MergeState 非空),直接覆盖活态 WishUsedToday。
        /// WishDailyLimit(每日上限)客户端另有本地常量 <c>MergeOrderConfig.WishPerDayLimit</c> 与服务端一致,故仅需落已用次数;
        /// 今日剩余次数由 UI 用「上限 - 已用」现算(上限值 UI 可读服务端 <c>WishRpcResult.WishDailyLimit</c> 或本地常量)。
        /// </remarks>
        public void ApplyServerWishSnapshot(int wishUsedToday)
        {
            int used = wishUsedToday < 0 ? 0 : wishUsedToday;

            var live = BlockGameState.Instance;
            var state = (live != null && live.MergeOrderMode) ? live.MergeState : null;
            if (state != null) state.WishUsedToday = used;

            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            dto.wishUsedToday = used;
            // 缓存日期锚到 today:与服务端「懒重置后当日值」语义一致,使开窗 ImportMeta 的跨天判定视作同日、不再本地清零。
            dto.lastWishResetDate = MergeMetaPersistence.Today();
            // 回灌写:祈愿每日态是服务端回灌值,fireSavedHook=false 只落缓存、不触发存档边界钩子(非玩法事件)。
            MergeMetaPersistence.SaveAsync(dto, fireSavedHook: false).Forget();
        }

        /// <summary>
        /// 落地服务端登录皮肤态 + 神庙装饰权威快照(皮肤/神庙装饰服务端权威·客户端段):用服务端值覆盖本地缓存 + 已开的玩法态。
        /// 登录快照是唯一初值源(云存档通道已整体退役,本地缓存只作开窗前投影兜底)。
        /// </summary>
        /// <remarks>
        /// 三态口径:
        /// - <paramref name="skinMono"/> 0/1(非 0/1 视作彩色 0);<paramref name="skinMonoId"/> 彩色态 -1(=未选,勿当有效 id 0)。
        ///   皮肤态经 <see cref="BlockSkinState.Import"/> 校验落地(单色态非法 id → 池内重随机;缺池 → 退彩色),同缓存加载口径。
        /// - <paramref name="templeDecorated"/> 已装饰厅数标量 → 前缀布尔数组(前 N 项 true,顺序解锁前缀,与批 1 templeRepaired 同口径)。
        /// 两处一并覆盖(仿 <see cref="ApplyServerCurrencySnapshot"/>):
        /// ① 若玩法窗已开(MergeState 非空),直接覆盖活态皮肤 + 装饰数组,即时反映;
        /// ② 缓存 <see cref="MergeMetaSave"/>(skinMono/skinMonoId/templeDecorated):供稍后开窗 ImportMeta 读投影。
        /// 回灌写:三态均为服务端回灌值,fireSavedHook=false 只落缓存、不触发存档边界钩子(非玩法事件)。
        /// </remarks>
        public void ApplyServerProfileStateSnapshot(int skinMono, int skinMonoId, long templeDecorated)
        {
            // 构造一份「皮肤态载体」dto 供 BlockSkinState.Import 校验落地(仅用两皮肤字段;templeDecorated 单独处理)。
            bool mono = skinMono == 1;
            var skinDto = new MergeMetaSave { skinMono = mono, skinMonoId = mono ? skinMonoId : BlockSkinState.Unselected };
            var decoratedArr = BuildTempleRepairedArray(templeDecorated); // 前缀布尔数组(与 templeRepaired 同标量→数组转换)

            // ① 活态:窗已开时直接覆盖皮肤 + 装饰数组。
            var live = BlockGameState.Instance;
            var state = (live != null && live.MergeOrderMode) ? live.MergeState : null;
            if (state != null)
            {
                state.Skin.Import(skinDto, BlockSkinCatalog.MonoIds);
                state.TempleDecorated = (bool[])decoratedArr.Clone(); // 深拷贝:活态不与缓存 dto 共享引用
            }

            // ② 缓存:写三态权威值(保留其余元层字段),供开窗 ImportMeta 读。
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            dto.skinMono = mono;
            dto.skinMonoId = mono ? skinMonoId : BlockSkinState.Unselected;
            dto.templeDecorated = decoratedArr;
            MergeMetaPersistence.SaveAsync(dto, fireSavedHook: false).Forget();
        }

        /// <summary>
        /// 按权威已修厅数构造缓存布尔数组:前 <paramref name="count"/> 项 true、其余 false(顺序解锁前缀,与 MetaCurrencySync 同口径)。
        /// count 夹到 [0, HallCount]。供快照回灌写缓存 templeRepaired 字段。
        /// </summary>
        private static bool[] BuildTempleRepairedArray(long count)
        {
            int n = BlockBlast.TempleConfig.HallCount;
            var arr = new bool[n];
            int c = count < 0 ? 0 : (count > n ? n : (int)count);
            for (int i = 0; i < c; i++) arr[i] = true;
            return arr;
        }

        /// <summary>
        /// 把当前 <see cref="Player"/> 落盘（设计 25 §5.2 H1）：先 <see cref="MergeMetaPersistence.Load"/>
        /// 读回既有 DTO（保留玩法元层字段，仅覆写玩家字段），再 <see cref="PlayerInfo.ExportToMeta"/> 写入、
        /// 经异步外壳 <see cref="MergeMetaPersistence.SaveAsync"/> 即发即忘落盘。
        /// 无既有 DTO（首次）→ 现场新建一份 DTO（version 由 Save 内序列化承接）。
        /// <paramref name="fireSavedHook"/>=false 用于服务端→本地的身份回写(<see cref="ApplyServerPlayerId"/>):
        /// 只落盘、不触发存档边界钩子(playerId 是服务端回灌值,非玩法事件)。
        /// </summary>
        public void SavePlayer(bool fireSavedHook = true)
        {
            if (Player == null) return;
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            Player.ExportToMeta(dto);
            MergeMetaPersistence.SaveAsync(dto, fireSavedHook).Forget();
        }

        /// <summary>
        /// 把经典最高分 <see cref="BlockGameState.HighScore"/> 落盘到元层（设计 29 §5.4），与 <see cref="SavePlayer"/>
        /// 同口径：先读回既有 DTO 保留其余元层字段，仅覆写 highScore，再经异步外壳即发即忘落盘。
        /// 在「最高分可能刷新」的结束/退出时机调用，使经典遗产与元层进度同一落盘节点。
        /// </summary>
        public void SaveHighScore()
        {
            var dto = MergeMetaPersistence.Load() ?? new MergeMetaSave { version = MergeMetaPersistence.CurrentVersion };
            BlockGameState.Instance.ExportHighScoreToMeta(dto);
            MergeMetaPersistence.SaveAsync(dto).Forget();
        }

        /// <summary>
        /// 测试 / 注入入口：用指定存储重建 <see cref="Settings"/> 并加载。
        /// 单测经 <see cref="InMemorySettingsStore"/> 断言往返，不污染真实 PlayerPrefs（设计 23 §9.1 H/W 组）。
        /// </summary>
        public void InitSettingsWithStore(ISettingsStore store)
        {
            Settings = new SettingsService(store);
            Settings.Load();
        }

        /// <summary>
        /// 测试 / 注入入口：用指定 DTO + rng 重建 <see cref="Player"/>（仿 <see cref="InitSettingsWithStore"/>）。
        /// dto 为 null → 走 <see cref="PlayerInfo.CreateDefault"/>；非 null → <see cref="PlayerInfo.ImportFromMeta"/>。
        /// 单测断言往返不碰真实 PlayerPrefs（设计 25 §9.1 H2）。<paramref name="avatarValid"/> 可选注入头像 id 校验。
        /// </summary>
        public void InitPlayerFromMeta(MergeMetaSave dto, System.Random rng, System.Func<int, bool> avatarValid = null)
        {
            Player = (dto != null)
                ? PlayerInfo.ImportFromMeta(dto, rng, avatarValid)
                : PlayerInfo.CreateDefault(rng);
        }

        /// <summary>
        /// 测试 / 注入入口：用指定接缝重建 <see cref="Rank"/>（仿 <see cref="InitSettingsWithStore"/> / <see cref="InitPlayerFromMeta"/>）。
        /// EditMode 经它灌入 <see cref="InMemoryRankPersistence"/> + fake <see cref="IMailService"/>（如 RecordingMailService）
        /// + fake 配置源（或先 <c>RankConfigMgr.InitForTest</c> 后用默认源），断言 <see cref="GetBoard"/> 数据贯通，
        /// 不污染真实 PlayerPrefs / 不连网（设计 28 §四 / §九 H2/H3）。
        /// </summary>
        public void InitRankWithDeps(IRankSource source, IRankPersistence persist, IMailService mail,
                                     IRankConfigSource cfg = null, IRemoteRankSource remote = null)
        {
            Rank = new RankService(source, persist, mail, cfg, remote);
        }

        /// <summary>
        /// 测试 / 注入入口：用指定远程来源重建 <see cref="Mail"/>（仿 <see cref="InitRankWithDeps"/>）。
        /// EditMode 经它灌入桩 <see cref="IRemoteMailSource"/>（注桩响应各分支），断言拉列表 / 领奖分发，不连网（设计 32 §8.2 CV）。
        /// </summary>
        public void InitMailWithSource(IRemoteMailSource remote)
        {
            Mail = new RemoteMailService(remote);
        }

        /// <summary>
        /// 测试 / 注入入口：用指定 RPC 接缝重建 <see cref="PlayerAttr"/>(沿 <see cref="InitMailWithSource"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IRpcGateway"/>(注桩响应各分支),断言改名扣钻 + 余额刷新,不连网(设计 38 §9.1 CV9)。
        /// </summary>
        public void InitPlayerAttrWith(IRpcGateway gateway)
        {
            PlayerAttr = new PlayerAttrService(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定改名 RPC 接缝重建 <see cref="RenameGateway"/>(沿 <see cref="InitPlayerAttrWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IRenameGateway"/>(注桩响应各分支),断言改名走 RPC + 视图对齐,不连网。
        /// </summary>
        public void InitRenameWith(IRenameGateway gateway)
        {
            RenameGateway = gateway;
        }

        /// <summary>
        /// 测试 / 注入入口:用指定修饰 RPC 接缝重建 <see cref="Cosmetic"/>(沿 <see cref="InitRenameWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="ICosmeticGateway"/>,断言换装乐观 set + 对账、解锁上报、bootstrap 差集上报,不连网。
        /// </summary>
        public void InitCosmeticWith(ICosmeticGateway gateway)
        {
            Cosmetic = new CosmeticService(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定 RPC 接缝重建 <see cref="MetaCurrency"/>(沿 <see cref="InitPlayerAttrWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IRpcGateway"/>,断言四货币聚合上报 + 对账,不连网。
        /// </summary>
        public void InitMetaCurrencyWith(IRpcGateway gateway)
        {
            MetaCurrency = new MetaCurrencySync(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定祈愿接缝 + 对账器重建 <see cref="Wish"/>(沿 <see cref="InitMetaCurrencyWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IWishGateway"/>,断言祈愿走 RPC + 权威值应用为投影 + 灵力体力不双减,不连网。
        /// </summary>
        public void InitWishWith(IWishGateway gateway, MetaCurrencySync currency)
        {
            Wish = new WishService(gateway, currency);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定 RPC 接缝重建 <see cref="OrderSync"/>(沿 <see cref="InitMetaCurrencyWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IOrderRpcGateway"/>,断言交付 RPC 编排 + 快照应用 + 失败退还库存,不连网。
        /// </summary>
        public void InitOrderSyncWith(IOrderRpcGateway gateway)
        {
            OrderSync = new OrderSync(gateway);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定接缝重建 <see cref="EnterMainGame"/>(沿 <see cref="InitMetaCurrencyWith"/> 范式),
        /// 关联当前 <see cref="OrderSync"/> / <see cref="Items"/> / <see cref="Tarot"/>。
        /// EditMode 经它灌入桩 <see cref="IEnterMainGameGateway"/>,断言响应应用 + 每次进入重对齐 + 失败兜底,不连网。
        /// </summary>
        public void InitEnterMainGameWith(IEnterMainGameGateway gateway)
        {
            EnterMainGame = new EnterMainGameSync(gateway, OrderSync, Items, Tarot);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定合成接缝重建 <see cref="Tarot"/>(沿 <see cref="InitOrderSyncWith"/> 范式)。
        /// **复用既有 <see cref="Items"/> 实例并清空**(仅缺失时新建):OrderSync / EnterMainGame 构造期捕获了
        /// Items 引用,若此处换新实例会造成"交付写旧背包、塔罗读新背包"的投影分叉。
        /// EditMode 经它灌入桩 <see cref="ITarotRpcGateway"/>,断言合成结果应用 + 快照整份覆盖 + 碎片余额对齐,不连网。
        /// </summary>
        public void InitTarotWith(ITarotRpcGateway gateway)
        {
            if (Items == null) Items = new GameLogic.BlockBlast.Item.ItemBag();
            else Items.Clear();
            Tarot = new TarotCollection(gateway, Items);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定数据源重建 <see cref="AttrLedger"/>(沿 <see cref="InitMailWithSource"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IAttrLedgerSource"/>(注桩响应各分支),断言流水查询 + 错误码归一,不连网(设计 46 PV6 / PV10 / PV11)。
        /// </summary>
        public void InitAttrLedgerWith(IAttrLedgerSource source)
        {
            AttrLedger = new RemoteAttrLedgerService(source);
        }

        /// <summary>
        /// 测试 / 注入入口:用指定数据源重建 <see cref="Activity"/>(沿 <see cref="InitAttrLedgerWith"/> 范式)。
        /// EditMode 经它灌入桩 <see cref="IActivityIncrementSource"/>(注桩响应各分支),
        /// 断言 hook fire-and-forget + 错误码归一 + 防重计数,不连网(设计 48 PV6 / PV9 / PV11 / PV12)。
        /// </summary>
        public void InitActivityWith(IActivityIncrementSource source)
        {
            Activity = new RemoteActivityService(source);
        }
    }
}
