using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 进主游戏编排(全栈协议改动·客户端段)。玩家进入融合主游戏时发一次 <c>C2G_EnterMainGameRequest</c>,
    /// 服务端一次性原子响应回带「订单快照」,本类拿到后应用到现有投影路径:订单 → <see cref="OrderSync.OnSnapshotPush"/>。
    ///
    /// 决策②每次进入都重新请求:每次 <see cref="EnterAsync"/> 取最新响应应用,不复用上次缓存。
    /// 防重入:在途时二次调用复用同一在途任务(只发一次)。
    ///
    /// 纯逻辑(经注入的 <see cref="IEnterMainGameGateway"/> 发请求),可 EditMode 单测(注桩 gateway,沿 <see cref="OrderSync"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 失败降级:gateway 契约不抛、失败归一为 <see cref="EnterMainGameResult.Unavailable"/>。订单 null → 不喂 OrderSync(保留已有投影)。
    /// 故任何失败下本方法都正常返回,调用方(进玩法入口闸)不卡死、按本地兜底进游戏。
    /// (局内 cosmetic + 合成经济叠加层改经 C2G_GameStart/GameSnapshot 的 SliceJson 收发,不再由本编排回带。)
    /// </remarks>
    public sealed class EnterMainGameSync
    {
        private readonly IEnterMainGameGateway _gateway;
        private readonly OrderSync _orderSync;
        private readonly Item.ItemBag _bag;
        private readonly TarotCollection _tarot;

        // 防重入:在途的进主游戏任务(Preserve 后可多次 await,供等待期二次调用复用)。保证一次进入只发一次请求。
        private UniTask? _inFlight;

        /// <param name="bag">背包投影(可空;非空时响应的道具持有整份覆盖它)。</param>
        /// <param name="tarot">塔罗收集投影(可空;非空时响应的已合成全集整份覆盖它)。</param>
        public EnterMainGameSync(IEnterMainGameGateway gateway, OrderSync orderSync,
            Item.ItemBag bag = null, TarotCollection tarot = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _orderSync = orderSync;
            _bag = bag;
            _tarot = tarot;
        }

        /// <summary>
        /// 发一次进主游戏请求并应用响应(决策②每次进入重新对齐)。在途时复用在途任务(防重入)。
        /// 完成后:订单快照已喂 OrderSync(若非空)。
        /// 调用方应配看门狗超时兜底(gateway 自带超时降级,此任务必在有限时间完成,看门狗只防极端阻塞)。
        /// </summary>
        public UniTask EnterAsync()
        {
            if (_inFlight.HasValue) return _inFlight.Value;
            // Preserve:UniTask 默认单次 await,Preserve 后才能被在途二次调用复用(否则二次 await 抛)。
            var task = EnterCoreAsync().Preserve();
            // 同步完成路径(桩 / 已连可立即回包):core 已在上一行内跑完并清空 _inFlight,故只在「仍在途」时挂上。
            // 不能依赖 core 的 finally 清 _inFlight——若在此处无条件赋值,会把已完成 task 当在途留住,下次进入误复用旧任务(不重发)。
            if (!task.Status.IsCompleted())
                _inFlight = task;
            return task;
        }

        private async UniTask EnterCoreAsync()
        {
            try
            {
                EnterMainGameResult result;
                try { result = await _gateway.EnterAsync(); }
                catch { result = EnterMainGameResult.Unavailable(); } // 防御:gateway 契约不抛,此处兜底

                // 订单:非空才喂(服务不可用 / 无订单 → 保留已有投影,不清空)。
                if (result.OrderSnapshot != null)
                    _orderSync?.OnSnapshotPush(result.OrderSnapshot);

                // 道具持有 + 塔罗收集:非 null 才整份覆盖(null = 往返失败,保留已有投影;
                // 空数组 = 服务端权威空集,照常覆盖清空——语义区分见 EnterMainGameResult 字段注)。
                if (result.ItemHoldings != null && _bag != null)
                {
                    _bag.ApplyAuthoritativeSnapshot(ToTuples(result.ItemHoldings));
                }
                if (result.CollectedTarotIds != null)
                    _tarot?.ApplyCollectedSnapshot(result.CollectedTarotIds);
            }
            finally
            {
                _inFlight = null; // 异步完成路径:清在途,下次进入重新发(同步完成路径由 EnterAsync 自身不挂在途)
            }
        }

        /// <summary>把持有 DTO 数组转 ItemBag 权威覆盖入口的 (id, count) 序列(惰性,不额外分配列表)。</summary>
        private static System.Collections.Generic.IEnumerable<(int id, long count)> ToTuples(ItemHoldingData[] holdings)
        {
            for (int i = 0; i < holdings.Length; i++)
                yield return (holdings[i].ItemId, holdings[i].Count);
        }
    }
}
