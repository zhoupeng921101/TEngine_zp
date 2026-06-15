#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 接收服务器推送的单位创建（进入地图后服务器会推自己的单位 + 同步场内其他玩家）。
    /// 这里仅打印验证推送链路；正式项目应在此生成/管理对应的 GameObject。
    /// </summary>
    public sealed class M2C_UnitCreateHandler : Message<M2C_UnitCreate>
    {
        protected override async FTask Run(Session session, M2C_UnitCreate message)
        {
            var u = message.Unit;
            Log.Info($"[Fantasy] 收到单位 M2C_UnitCreate UnitId={u.UnitId} Name={u.Name} Type={u.UnitType} IsSelf={message.IsSelf}");
            // Scene 为 MainThread 模式，Handler 在 Unity 主线程执行，可安全操作 GameObject。
            var pos = u.Pos != null ? u.Pos.UnityPosition : UnityEngine.Vector3.zero;
            UnitViewManager.Spawn(u.UnitId, u.Name, pos, message.IsSelf);
            await FTask.CompletedTask;
        }
    }
}
#endif
