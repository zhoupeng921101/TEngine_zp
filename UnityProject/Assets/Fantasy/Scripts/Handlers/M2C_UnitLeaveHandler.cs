#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 接收服务器推送的单位离开（其他玩家下线/离开视野）。仅打印验证；正式项目应销毁对应 GameObject。
    /// </summary>
    public sealed class M2C_UnitLeaveHandler : Message<M2C_UnitLeave>
    {
        protected override async FTask Run(Session session, M2C_UnitLeave message)
        {
            Log.Info($"[Fantasy] 单位离开 M2C_UnitLeave UnitId={message.UnitId}");
            UnitViewManager.Remove(message.UnitId);
            await FTask.CompletedTask;
        }
    }
}
#endif
