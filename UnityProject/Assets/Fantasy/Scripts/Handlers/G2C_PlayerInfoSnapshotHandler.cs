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

            // 属性各一项(Coin/Diamond/Stamina + 四玩法货币 SoulPower/Piety/GuardianExp/Energy + 六元层计数器
            // GoddessLevel/GoddessRating/UnlockedChapter/BlindBoxCount/TempleRepaired/NextRepairIndex),按 type 散到对应 long;
            // 缺项以 0 缺省(沿服务端段默认初值)。
            long coin = 0, diamond = 0, stamina = 0;
            long soulPower = 0, piety = 0, guardianExp = 0, energy = 0;
            long goddessLevel = 0, goddessRating = 0, unlockedChapter = 0;
            long blindBoxCount = 0, templeRepaired = 0, nextRepairIndex = 0;
            if (info.Properties != null)
            {
                foreach (var item in info.Properties)
                {
                    if (item == null) continue;
                    switch (item.Type)
                    {
                        case PropertyType.Coin:            coin = item.Amount; break;
                        case PropertyType.Diamond:         diamond = item.Amount; break;
                        case PropertyType.Stamina:         stamina = item.Amount; break;
                        case PropertyType.SoulPower:       soulPower = item.Amount; break;
                        case PropertyType.Piety:           piety = item.Amount; break;
                        case PropertyType.GuardianExp:     guardianExp = item.Amount; break;
                        case PropertyType.Energy:          energy = item.Amount; break;
                        case PropertyType.GoddessLevel:    goddessLevel = item.Amount; break;
                        case PropertyType.GoddessRating:   goddessRating = item.Amount; break;
                        case PropertyType.UnlockedChapter: unlockedChapter = item.Amount; break;
                        case PropertyType.BlindBoxCount:   blindBoxCount = item.Amount; break;
                        case PropertyType.TempleRepaired:  templeRepaired = item.Amount; break;
                        case PropertyType.NextRepairIndex: nextRepairIndex = item.Amount; break;
                    }
                }
            }

            // 头像/框服务端权威:当前佩戴 id + 已解锁集合。协议 List<int> 用完即回池,复制为独立数组避免引用悬空。
            int[] unlockedAvatarIds = CopyIds(info.UnlockedAvatarIds);
            int[] unlockedFrameIds = CopyIds(info.UnlockedFrameIds);

            // 背包服务端权威(背包系统·客户端段):两轨全量 + 服务端时间基准 + loaded 标志,复制为跨边界视图。
            var inventory = InventoryProtocolMapper.Build(
                info.Holdings, info.Lots, info.ServerNowMs, info.InventoryLoaded, info.LastUseReqSeq);

            var view = new PlayerInfoView(
                info.AccountId, info.Nickname, info.Level, info.Exp, info.RenameCount,
                coin, diamond, stamina, soulPower, piety, guardianExp, energy,
                goddessLevel, goddessRating, unlockedChapter, blindBoxCount, templeRepaired, nextRepairIndex,
                info.CurrentAvatarId, info.CurrentFrameId, unlockedAvatarIds, unlockedFrameIds,
                info.WishUsedToday, info.WishDailyLimit,
                info.SkinMono, info.SkinMonoId, info.TempleDecorated,
                info.SchemaVersion, inventory);

            FantasyNetwork.RaisePlayerInfoSnapshot(view);
            await FTask.CompletedTask;
        }

        /// <summary>把协议 List&lt;int&gt; 复制为独立数组(协议对象用完即回池,跨边界须复制)。null/空 → 空数组。</summary>
        private static int[] CopyIds(System.Collections.Generic.List<int> src)
        {
            if (src == null || src.Count == 0) return System.Array.Empty<int>();
            var dst = new int[src.Count];
            for (int i = 0; i < src.Count; i++) dst[i] = src[i];
            return dst;
        }
    }
}
#endif
