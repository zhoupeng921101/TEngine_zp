#if FANTASY_UNITY
using Fantasy;
using Fantasy.Async;
using Fantasy.Network;
using Fantasy.Network.Interface;

namespace FantasyClient
{
    /// <summary>
    /// 服务端登录后下发玩家信息整份快照(基础档案 + 三属性)→ 经 <see cref="FantasyNetwork.OnPlayerInfoSnapshot"/>
    /// 事件转发热更区订阅方(沿 OnLoggedIn 范式,避免 FantasyClient 反向依赖 GameLogic 致循环)。
    /// 客户端 Handler 由源生成器自动注册,无需手动登记(沿 G2C_PushMessageHandler 范式)。
    /// </summary>
    public sealed class G2C_PlayerInfoSnapshotHandler : Message<G2C_PlayerInfoSnapshot>
    {
        protected override async FTask Run(Session session, G2C_PlayerInfoSnapshot message)
        {
            var info = message.Info;
            if (info == null)
            {
                // 协议保证 Info 非空;此处仅防御异常包,不崩流程。
                Log.Error("[Fantasy] 收到玩家信息快照但 Info 为空,丢弃。");
                await FTask.CompletedTask;
                return;
            }

            // 三属性各一项(Coin/Diamond/Stamina),按 type 散到三个 long;缺项以 0 缺省(沿服务端段默认初值)。
            long coin = 0, diamond = 0, stamina = 0;
            if (info.Properties != null)
            {
                foreach (var item in info.Properties)
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

            var view = new PlayerInfoView(
                info.AccountId, info.Nickname, info.Level, info.Exp,
                coin, diamond, stamina, info.SchemaVersion);

            Log.Info($"[Fantasy] 收到玩家信息快照 Account={view.AccountId} Nickname={view.Nickname} " +
                     $"Level={view.Level} Exp={view.Exp} Coin={view.Coin} Diamond={view.Diamond} " +
                     $"Stamina={view.Stamina} SchemaVersion={view.SchemaVersion}");
            FantasyNetwork.RaisePlayerInfoSnapshot(view);
            await FTask.CompletedTask;
        }
    }
}
#endif
