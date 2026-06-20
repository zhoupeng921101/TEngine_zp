#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 服务端登录后下发属性初始快照(设计 37/38 §五接线)→ 经 <see cref="FantasyNetwork.OnPropertyInitSnapshot"/>
    /// 事件转发热更区订阅方(沿 OnLoggedIn 范式,避免 FantasyClient 反向依赖 GameLogic 致循环)。
    /// 客户端 Handler 由源生成器自动注册,无需手动登记(沿 G2C_PushMessageHandler 范式)。
    /// </summary>
    public sealed class G2C_PropertyInitSnapshotHandler : Message<G2C_PropertyInitSnapshot>
    {
        protected override async FTask Run(Session session, G2C_PropertyInitSnapshot message)
        {
            // 协议三属性各一项(Coin/Diamond/Stamina),按 type 散到三个 long;缺项以 0 缺省(沿 37 服务端段默认初值)。
            long coin = 0, diamond = 0, stamina = 0;
            if (message.Properties != null)
            {
                foreach (var item in message.Properties)
                {
                    if (item == null) continue;
                    switch (item.Type)
                    {
                        case PropertyType.Coin:    coin = item.Amount; break;
                        case PropertyType.Diamond: diamond = item.Amount; break;
                        case PropertyType.Stamina: stamina = item.Amount; break;
                    }
                }
            }

            Log.Info($"[Fantasy] 收到属性初始快照 Coin={coin} Diamond={diamond} Stamina={stamina} SchemaVersion={message.SchemaVersion}");
            FantasyNetwork.RaisePropertyInitSnapshot(coin, diamond, stamina, message.SchemaVersion);
            await FTask.CompletedTask;
        }
    }
}
#endif
