using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// normal 订单本地视图 ↔ 服务端权威 投影器(P1 全栈迁移·客户端段)。
    ///
    /// 背景:normal 订单历史上由客户端 <see cref="MergeOrderState"/> 本地生成(NextOrder/RefreshAllOrders/ApplyOrderRefresh
    /// 走客户端墙钟)+ 本地交付发奖。P1 改服务端权威:服务端持 normal 订单 + 交付裁决 + 发奖的唯一事实源,客户端降为投影——
    /// 登录快照 + 整批刷新推送是 normal 订单唯一来源,交付走 RPC、奖励由服务端发,交付后权威绝对余额随响应回带、经 MetaCurrencySync 对账应用。
    ///
    /// 本类职责:① 缓存 + 应用服务端订单快照到 <see cref="MergeOrderState.ApplyServerOrderSnapshot"/>;② 玩法窗开窗时
    /// (经 <see cref="OnMergeStateReady"/>)把活态切到服务端权威模式 + 接交付 RPC 钩子 + 应用已缓存快照;③ 交付 RPC 编排
    /// (乐观本地扣库存 → 发 RPC → 失败退还库存 + 反馈)。
    ///
    /// 纯逻辑(不依赖 UnityEngine):经注入的 <see cref="IOrderRpcGateway"/> 发请求,故可 EditMode 单测(沿 MetaCurrencySync 范式)。
    /// </summary>
    /// <remarks>
    /// 【双计避免】normal 订单交付的 Energy/Piety 由服务端 RPC 发放,交付后权威绝对余额随交付响应回带、经
    /// <c>MetaCurrencySync.ApplyDeltaPush</c> 应用(同步 set 字段 + 基线到权威值)。客户端交付路径
    /// (<see cref="MergeOrderState.Deliver"/> 服务端权威分支)<b>不本地实发</b> Energy/Piety,故 <c>MetaCurrencySync.ReportPending</c>
    /// 的基线 diff 看不到交付增量、不会重复上报(无需额外排除累加器:不本地加 = 无 delta;对账 set 时字段与基线同步移动,diff 恒 0)。
    ///
    /// 【交付时序·失败不丢库存】乐观扣库存在 <see cref="MergeOrderState.Deliver"/> 同步完成(让冻结 UI 的即时刷新显示已扣 +
    /// 槽位置空,happy path 即时反馈);RPC 失败(AlreadyDelivered / InvalidSlot / 服务不可用 / 网络断)经
    /// <see cref="MergeOrderState.RefundInventoryForFailedDeliver"/> 退还乐观扣减 + 回滚计数,不丢库存。成功则服务端快照回带、
    /// 经 <see cref="MergeOrderState.ApplyServerOrderSnapshot"/> 校正槽位。
    ///
    /// 【快照覆盖时机】登录快照可能早于玩法窗开窗到达 → 缓存到 <see cref="_pending"/>;开窗 <see cref="OnMergeStateReady"/>
    /// (在 ResetForMergeOrder 局内存档加载之后触发)再应用,覆盖 blob 里可能的旧 normal 订单。窗已开时快照到达直接应用活态。
    /// </remarks>
    public sealed class OrderSync
    {
        private readonly IOrderRpcGateway _gateway;

        /// <summary>四货币对账器:交付成功后把响应回带的 Energy/Piety 权威绝对余额经其 ApplyDeltaPush 应用(set 字段 + 基线 + HUD 脏标记)。可为 null(纯逻辑单测不关心货币时)。</summary>
        private readonly MetaCurrencySync _currency;

        /// <summary>背包投影:交付成功后把响应回带的塔罗碎片权威余额对齐到背包计数(-1 哨兵不 set)。可为 null(纯逻辑单测不关心碎片时)。</summary>
        private readonly Item.ItemBag _bag;

        /// <summary>最近一次服务端订单快照(登录初推 / 整批刷新推送)。开窗时若活态尚无快照、用它对齐。</summary>
        private OrderSnapshotData _pending;

        /// <summary>当前活态(开窗后非空)。快照到达时直接应用到它;关窗置 null。</summary>
        private MergeOrderState _state;

        public OrderSync(IOrderRpcGateway gateway, MetaCurrencySync currency = null, Item.ItemBag bag = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _currency = currency;
            _bag = bag;
        }

        /// <summary>
        /// 服务端订单快照到达(登录初推 + 整批刷新到点推 + 也可由交付响应路径复用)。缓存最新一份;
        /// 若玩法窗已开(活态非空)直接覆盖活态 normal 订单,即时反映;窗未开则待开窗 <see cref="OnMergeStateReady"/> 应用。
        /// </summary>
        public void OnSnapshotPush(OrderSnapshotData snapshot)
        {
            if (snapshot == null) return;
            _pending = snapshot;
            _state?.ApplyServerOrderSnapshot(snapshot);
        }

        /// <summary>
        /// 玩法窗开窗、活态就绪(由 BlockGameState.ResetForMergeOrder 末尾经静态钩子触发,即局内存档加载之后)。
        /// ① 把活态切到服务端权威模式(本地不再生成 / 墙钟刷新 normal 订单);② 接交付 RPC 钩子;
        /// ③ 若已缓存快照则应用(覆盖 blob 旧 normal 订单);④ 记活态供后续快照推送直接覆盖。
        /// </summary>
        public void OnMergeStateReady(MergeOrderState state)
        {
            if (state == null) return;
            _state = state;
            state.ServerAuthoritativeOrders = true;
            // 交付钩子:服务端权威分支的 Deliver 同步乐观扣减后,经此钩子发起 RPC(state 不直接依赖网络层)。
            state.DeliverHook = (slot, order) => RequestDeliver(state, slot, order).Forget();
            if (_pending != null) state.ApplyServerOrderSnapshot(_pending);
        }

        /// <summary>关窗:解绑活态(避免快照推送打到已弃用的旧 state)。缓存快照保留,下次开窗续用。</summary>
        public void OnMergeStateClosed(MergeOrderState state)
        {
            if (_state == state) _state = null;
        }

        /// <summary>
        /// 交付 RPC 编排(<see cref="MergeOrderState.Deliver"/> 服务端权威分支经 <see cref="MergeOrderState.DeliverHook"/> 调)。
        /// 调入时本地已乐观扣库存 + 置空槽 + 计数 +1(同步,UI 已显示)。本方法发 RPC:
        /// Success → 应用回带快照校正槽位 + 按响应绝对余额对账 Energy/Piety;其它码 → 退还库存 + 回滚计数(失败不丢库存)。
        /// 即发即忘 UniTask,不抛、不阻塞玩法。
        /// </summary>
        public async UniTask RequestDeliver(MergeOrderState state, int slot, Order consumed)
        {
            var result = await _gateway.DeliverAsync(slot);

            if (result.Code == DeliverCode.Success)
            {
                // 成功:服务端权威快照(含已置空本槽 + 可能的整批刷新)覆盖本地槽位。
                if (result.Snapshot != null) state.ApplyServerOrderSnapshot(result.Snapshot);
                // 交付奖励 Energy/Piety:响应回带的权威绝对余额经对账器应用(set 字段 + 基线 + MarkCurrencyPushed 驱动 HUD 重绘),
                // 取代原对发起方的 delta-push。-1 = 哨兵(该货币本次未取到权威值)→ 跳过,靠快照 / 其它会话推送对齐。
                if (_currency != null)
                {
                    if (result.EnergyBalance >= 0) _currency.ApplyDeltaPush(state, AttrType.Energy, result.EnergyBalance);
                    if (result.PietyBalance >= 0) _currency.ApplyDeltaPush(state, AttrType.Piety, result.PietyBalance);
                }
                // 塔罗碎片:响应回带权威绝对余额,对齐背包投影(-1 哨兵不 set,靠下次进主游戏快照对齐)。
                if (_bag != null && result.FragmentItemId > 0 && result.FragmentBalance >= 0)
                {
                    _bag.SetAuthoritativeCount(result.FragmentItemId, result.FragmentBalance);
                }
                return;
            }

            // 失败(AlreadyDelivered / InvalidSlot / NotLoggedIn / ServiceUnavailable / NetworkDown):
            // 服务端未消费该单 → 退还乐观扣减的库存 + 回滚乐观计数,本地视图与服务端对齐。
            state.RefundInventoryForFailedDeliver(consumed);
            // 服务端若回带了快照(如 AlreadyDelivered 也可能带最新态)则一并对齐槽位。
            if (result.Snapshot != null) state.ApplyServerOrderSnapshot(result.Snapshot);
        }
    }
}
