using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 进主游戏编排(全栈协议改动·客户端段)。玩家进入融合主游戏时发一次 <c>C2G_EnterMainGameRequest</c>,
    /// 服务端一次性原子响应回带「订单快照 + 云存档」,本类拿到后统一应用到现有投影路径:
    /// 订单 → <see cref="OrderSync.OnSnapshotPush"/>;云存档 → <see cref="CloudSaveSync.ApplyDownloadResolution"/>。
    ///
    /// 决策②每次进入都重新请求:每次 <see cref="EnterAsync"/> 先把云存档就绪态复位(<see cref="CloudSaveSync.ResetReadyForReentry"/>),
    /// 取最新响应应用后再置位,不复用上次缓存。防重入:在途时二次调用复用同一在途任务(只发一次)。
    ///
    /// 纯逻辑(经注入的 <see cref="IEnterMainGameGateway"/> 发请求),可 EditMode 单测(注桩 gateway,沿 <see cref="OrderSync"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 失败降级:gateway 契约不抛、失败归一为 <see cref="EnterMainGameResult.Unavailable"/>。订单 null → 不喂 OrderSync(保留已有投影);
    /// 云存档 ServiceUnavailable → <see cref="CloudSaveSync.ApplyDownloadResolution"/> 保留本地并置 Ready。
    /// 故任何失败下本方法都正常返回 + 云存档置 Ready,调用方(进玩法入口闸)不卡死、按本地兜底进游戏。
    /// </remarks>
    public sealed class EnterMainGameSync
    {
        private readonly IEnterMainGameGateway _gateway;
        private readonly OrderSync _orderSync;
        private readonly CloudSaveSync _cloudSave;

        // 防重入:在途的进主游戏任务(Preserve 后可多次 await,供等待期二次调用复用)。保证一次进入只发一次请求。
        private UniTask? _inFlight;

        public EnterMainGameSync(IEnterMainGameGateway gateway, OrderSync orderSync, CloudSaveSync cloudSave)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _orderSync = orderSync;
            _cloudSave = cloudSave;
        }

        /// <summary>
        /// 发一次进主游戏请求并应用响应(决策②每次进入重新对齐)。在途时复用在途任务(防重入)。
        /// 完成后:订单快照已喂 OrderSync(若非空)、云存档冲突已解决并置 <see cref="CloudSaveSync.IsReady"/>。
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
            // 进入前复位云存档就绪态:本次响应落地前 WhenReady 重新挂起,确保每次进入都重新对齐(决策②)。
            _cloudSave?.ResetReadyForReentry();

            try
            {
                EnterMainGameResult result;
                try { result = await _gateway.EnterAsync(); }
                catch { result = EnterMainGameResult.Unavailable(); } // 防御:gateway 契约不抛,此处兜底

                // 订单:非空才喂(服务不可用 / 无订单 → 保留已有投影,不清空)。
                if (result.OrderSnapshot != null)
                    _orderSync?.OnSnapshotPush(result.OrderSnapshot);

                // 云存档:无论成功 / 失败都喂一次冲突解决——成功覆盖、失败保留本地,两条都在末尾置 IsReady,
                // 故进玩法入口闸不会因失败而永远等不到就绪。
                _cloudSave?.ApplyDownloadResolution(result.CloudCode, result.CloudServerVersion, result.CloudServerBlob);
            }
            finally
            {
                _inFlight = null; // 异步完成路径:清在途,下次进入重新发(同步完成路径由 EnterAsync 自身不挂在途)
            }
        }
    }
}
