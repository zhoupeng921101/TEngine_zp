#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 接收服务器主动推送的 G2C_PushMessage。
    /// 客户端 Handler 由源生成器自动注册，无需手动登记；放在被 Unity 加载的程序集内即可。
    /// </summary>
    public sealed class G2C_PushMessageHandler : Message<G2C_PushMessage>
    {
        protected override async FTask Run(Session session, G2C_PushMessage message)
        {
            Log.Debug($"[FantasyTest] 收到服务器推送 G2C_PushMessage Tag={message.Tag}");
            await FTask.CompletedTask;
        }
    }
}
#endif
