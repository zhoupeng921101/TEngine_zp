#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 服务端属性变更主动推送(沿 37 §3.3.3 + §5.4)→ 经 <see cref="FantasyNetwork.OnPropertyDeltaPush"/>
    /// 事件转发热更区订阅方(沿 OnLoggedIn 范式)。
    /// </summary>
    public sealed class G2C_PropertyDeltaPushHandler : Message<G2C_PropertyDeltaPush>
    {
        protected override async FTask Run(Session session, G2C_PropertyDeltaPush message)
        {
            FantasyNetwork.RaisePropertyDeltaPush((int)message.Type, message.NewAmount, message.Reason);
            await FTask.CompletedTask;
        }
    }
}
#endif
