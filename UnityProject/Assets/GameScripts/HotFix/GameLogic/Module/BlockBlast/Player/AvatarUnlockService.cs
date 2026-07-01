using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>头像/框三态（设计 18 §3.6）：未解锁 / 已解锁未佩戴 / 当前佩戴。</summary>
    public enum AvatarState { Locked, Unlocked, Equipped }

    /// <summary>
    /// 头像/框解锁判定 + 三态（设计 18 §3.6）。纯逻辑：吃 <see cref="PlayerInfo"/> + <see cref="AvatarEntry"/> 产结果。
    /// </summary>
    /// <remarks>
    /// 「是否已解锁」两条来源取或：① 已在 PlayerInfo 已解锁集合里（含活动发放/历史）；
    /// ② 等级条件实时满足（UnlockCond==Level &amp;&amp; player.Level &gt;= UnlockParam）。
    /// 活动发放（EVENT）本轮只留钩子：恒按「集合含才算解锁」判，真实「发放」= 接活动系统调 <see cref="GrantUnlock"/>
    /// 把 id 写进集合（O3），判定逻辑不返工。
    /// </remarks>
    public static class AvatarUnlockService
    {
        /// <summary>取该 entry 对应的已解锁集合（头像 / 框）。可能为 null。</summary>
        private static int[] SetOf(PlayerInfo p, AvatarEntry e)
            => e.Type == AvatarType.Avatar ? p.UnlockedAvatarIds : p.UnlockedFrameIds;

        private static bool SetContains(int[] set, int id)
        {
            if (set == null) return false;
            for (int i = 0; i < set.Length; i++)
                if (set[i] == id) return true;
            return false;
        }

        /// <summary>是否已解锁：集合已含 或 等级条件实时达标。EVENT 未发放 → 未解锁（本轮不判活动）。</summary>
        public static bool IsUnlocked(PlayerInfo p, AvatarEntry e)
        {
            if (SetContains(SetOf(p, e), e.Id)) return true;       // 集合已含（活动发放/历史）
            if (e.UnlockCond == UnlockCond.Level)
                return p.Level >= e.UnlockParam;                   // 等级条件实时判
            return false;                                          // EVENT 未发放 → 未解锁
        }

        /// <summary>三态：未解锁 → Locked；已解锁 + 当前佩戴 → Equipped；已解锁 + 非当前 → Unlocked。</summary>
        public static AvatarState StateOf(PlayerInfo p, AvatarEntry e)
        {
            if (!IsUnlocked(p, e)) return AvatarState.Locked;
            int current = (e.Type == AvatarType.Avatar) ? p.CurrentAvatarId : p.CurrentFrameId;
            return e.Id == current ? AvatarState.Equipped : AvatarState.Unlocked;
        }

        /// <summary>
        /// 升级时把「等级新达标」的头像/框补进已解锁集合（持久化用，避免每次实时算）。去重，不重复加。
        /// </summary>
        public static void SyncLevelUnlocks(PlayerInfo p, IEnumerable<AvatarEntry> all)
        {
            if (all == null) return;
            foreach (var e in all)
            {
                if (e.UnlockCond == UnlockCond.Level && p.Level >= e.UnlockParam)
                    AddToSet(p, e);
            }
        }

        /// <summary>
        /// 活动发放钩子（O3）：把 id 写进对应已解锁集合（去重）。本轮无活动系统调用，接入时由活动系统调。
        /// </summary>
        public static void GrantUnlock(PlayerInfo p, AvatarEntry e) => AddToSet(p, e);

        /// <summary>
        /// 本地换装预检 + 就地 set(纯逻辑)：仅当目标已解锁才佩戴。头像/框服务端权威后,真正的换装走
        /// <see cref="CosmeticService.EquipAsync"/>(乐观 set + 服务端对账);本方法保留为纯逻辑单元(供单测 /
        /// 离线预检),不再是权威换装入口——换装合法性最终由服务端解锁集裁定。
        /// </summary>
        public static bool TryEquip(PlayerInfo p, AvatarEntry e)
        {
            if (!IsUnlocked(p, e)) return false;
            if (e.Type == AvatarType.Avatar) p.CurrentAvatarId = e.Id;
            else p.CurrentFrameId = e.Id;
            return true;
        }

        /// <summary>把 entry id 加进对应集合（去重）。集合 null 时新建。</summary>
        private static void AddToSet(PlayerInfo p, AvatarEntry e)
        {
            if (e.Type == AvatarType.Avatar)
                p.UnlockedAvatarIds = AppendDistinct(p.UnlockedAvatarIds, e.Id);
            else
                p.UnlockedFrameIds = AppendDistinct(p.UnlockedFrameIds, e.Id);
        }

        private static int[] AppendDistinct(int[] set, int id)
        {
            if (SetContains(set, id)) return set;
            int len = set?.Length ?? 0;
            var dst = new int[len + 1];
            if (len > 0) System.Array.Copy(set, dst, len);
            dst[len] = id;
            return dst;
        }
    }
}
