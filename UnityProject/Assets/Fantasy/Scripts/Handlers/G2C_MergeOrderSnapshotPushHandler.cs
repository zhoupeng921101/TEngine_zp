#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 服务端订单整批快照主动推送(P1 全栈迁移·客户端段:登录初推 + 整批刷新到点推)→ 经
    /// <see cref="FantasyNetwork.OnMergeOrderSnapshotPush"/> 事件转发热更区订阅方(沿 G2C_PropertyDeltaPush 范式)。
    /// 客户端 Handler 由源生成器自动注册,无需手动登记。
    /// 订阅方在事件回调内同步把协议对象转框架中立 DTO 再应用——本 Run 返回后协议对象回池,不可跨帧持有。
    /// </summary>
    public sealed class G2C_MergeOrderSnapshotPushHandler : Message<G2C_MergeOrderSnapshotPush>
    {
        protected override async FTask Run(Session session, G2C_MergeOrderSnapshotPush message)
        {
            if (message.Snapshot == null)
            {
                // 协议保证 Snapshot 非空;此处仅防御异常包,不崩流程。
                Log.Error("[Fantasy] 收到订单快照推送但 Snapshot 为空,丢弃。");
                await FTask.CompletedTask;
                return;
            }

            Log.Info($"[Fantasy] 收到订单快照推送 槽数={message.Snapshot.ActiveOrders?.Count ?? 0} " +
                     $"Cursor={message.Snapshot.OrderCursor} RefreshIntervalSec={message.Snapshot.OrderRefreshIntervalSec}");
            FantasyNetwork.RaiseMergeOrderSnapshotPush(message.Snapshot);
            await FTask.CompletedTask;
        }
    }
}
#endif
