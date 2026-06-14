using System;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 玩家个人信息数据模型（设计 18 §3.1）。纯 POCO、扁平 [Serializable]，JsonUtility 友好：
    /// 已解锁集合用 <c>int[]</c>（JsonUtility 不序列化 HashSet/Dictionary），运行期服务内部临时转 HashSet 查重。
    /// </summary>
    /// <remarks>
    /// 玩家账号等级是 <see cref="Exp"/> 的纯函数（<see cref="PlayerLevelConfig.LevelFor"/>），不单独存值，
    /// 避免两份状态漂移（同 GuardianLevel 做法）。玩家等级是独立第三条进度线，不复用守护者经验（设计 18 §3.4）。
    /// 持久化经 <see cref="ExportToMeta"/>/<see cref="ImportFromMeta"/> 并入既有 <see cref="MergeMetaSave"/>（§3.8 做法 a 平铺）。
    /// </remarks>
    [Serializable]
    public sealed class PlayerInfo
    {
        /// <summary>spec：初始默认「机器人」头像（表里 id=1 那行）。</summary>
        public const int DefaultAvatarId = 1;
        /// <summary>初始默认头像框（表里框类型起始 id，见设计 18 §3.5 样例）。</summary>
        public const int DefaultFrameId = 101;

        /// <summary>本地生成的玩家 id（本地唯一）。一旦生成不再变（改名不改 id）。</summary>
        public string Id;
        /// <summary>当前昵称（初始系统生成，§3.2）。</summary>
        public string Name;
        /// <summary>已改名次数（0 = 还没改过 → 下次免费）。</summary>
        public int RenameCount;
        /// <summary>玩家账号经验（玩家等级 = <see cref="PlayerLevelConfig.LevelFor"/>(Exp)，§3.4）。</summary>
        public int Exp;
        /// <summary>当前佩戴头像 id（初始 = <see cref="DefaultAvatarId"/>）。</summary>
        public int CurrentAvatarId;
        /// <summary>当前佩戴头像框 id（初始 = <see cref="DefaultFrameId"/>）。</summary>
        public int CurrentFrameId;
        /// <summary>已解锁头像 id 集合（含活动发放的，§3.6）。</summary>
        public int[] UnlockedAvatarIds;
        /// <summary>已解锁头像框 id 集合。</summary>
        public int[] UnlockedFrameIds;

        /// <summary>玩家等级 = Exp 的纯函数，不单独存值（避免两份状态漂移，同 GuardianLevel 做法）。</summary>
        public int Level => PlayerLevelConfig.LevelFor(Exp);

        /// <summary>
        /// 本地唯一 id（适配 spec「服务器规则自动生成」——离线无服务器，本地生成本地唯一即足够）。
        /// 默认 Guid.NewGuid().ToString("N")：32 位十六进制，本地唯一性由 GUID 保证。
        /// </summary>
        public static string NewId() => Guid.NewGuid().ToString("N");

        /// <summary>
        /// 首次创建（无存档时）：新 id + 系统生成名 + RenameCount=0 + Exp=0 + 默认头像/框 +
        /// 已解锁集合含默认头像/框（初始即拥有）。<paramref name="rng"/> 注入使名字生成可在单测复现。
        /// </summary>
        public static PlayerInfo CreateDefault(System.Random rng)
        {
            return new PlayerInfo
            {
                Id = NewId(),
                Name = PlayerNameGenerator.Generate(rng),
                RenameCount = 0,
                Exp = 0,
                CurrentAvatarId = DefaultAvatarId,
                CurrentFrameId = DefaultFrameId,
                UnlockedAvatarIds = new[] { DefaultAvatarId },
                UnlockedFrameIds = new[] { DefaultFrameId },
            };
        }

        // ── 跨会话持久化:并入既有 MergeMetaSave(设计 18 §3.8 做法 a 平铺)─────
        // 纯方法:无 IO、无 ConfigSystem。与 MergeOrderState.ExportMeta/ImportMeta 独立——玩家信息是独立第三
        // 进度线,不挂进玩法状态机/悔棋快照(§3.4)。落盘外壳仍是既有 MergeMetaPersistence.SaveAsync/Load,
        // 玩家字段随同一份 DTO 一并落盘/迁移/夹值,不另造存储栈。

        /// <summary>把玩家信息写进 DTO 的玩家字段(增量,不动既有玩法字段)。纯方法、无 IO。</summary>
        public void ExportToMeta(MergeMetaSave dto)
        {
            if (dto == null) return;
            dto.playerId = Id;
            dto.playerName = Name;
            dto.playerRenameCount = RenameCount;
            dto.playerExp = Exp;
            dto.curAvatarId = CurrentAvatarId;
            dto.curFrameId = CurrentFrameId;
            dto.unlockedAvatarIds = (int[])UnlockedAvatarIds?.Clone();   // 深拷贝:DTO 不与现场共享引用
            dto.unlockedFrameIds = (int[])UnlockedFrameIds?.Clone();
        }

        /// <summary>
        /// 从 DTO 的玩家字段重建 PlayerInfo,逐字段保底夹到合法不变量(设计 18 §3.8 注,同设计 14 红线):
        /// id 空 → 现场生成新 id;name 空 → 生成系统名;renameCount/exp&lt;0 → 夹 0;
        /// curAvatarId/curFrameId ≤0(或经 <paramref name="avatarValid"/>/<paramref name="frameValid"/> 判越界)→ 退默认 1/101;
        /// 已解锁集合 null → 重建为含默认头像/框的数组。<paramref name="avatarValid"/>/<paramref name="frameValid"/>
        /// 可选注入「该 id 是否在表内」判定(运行期接 AvatarConfigMgr;单测不传 = 只夹 ≤0,保持纯逻辑)。
        /// </summary>
        public static PlayerInfo ImportFromMeta(
            MergeMetaSave dto,
            System.Random rng,
            System.Func<int, bool> avatarValid = null,
            System.Func<int, bool> frameValid = null)
        {
            var p = new PlayerInfo();

            p.Id = string.IsNullOrEmpty(dto?.playerId) ? NewId() : dto.playerId;
            p.Name = string.IsNullOrEmpty(dto?.playerName) ? PlayerNameGenerator.Generate(rng) : dto.playerName;
            p.RenameCount = (dto != null && dto.playerRenameCount > 0) ? dto.playerRenameCount : 0;
            p.Exp = (dto != null && dto.playerExp > 0) ? dto.playerExp : 0;

            int avatar = dto?.curAvatarId ?? 0;
            if (avatar <= 0 || (avatarValid != null && !avatarValid(avatar))) avatar = DefaultAvatarId;
            p.CurrentAvatarId = avatar;

            int frame = dto?.curFrameId ?? 0;
            if (frame <= 0 || (frameValid != null && !frameValid(frame))) frame = DefaultFrameId;
            p.CurrentFrameId = frame;

            p.UnlockedAvatarIds = NormalizeSet(dto?.unlockedAvatarIds, DefaultAvatarId);
            p.UnlockedFrameIds = NormalizeSet(dto?.unlockedFrameIds, DefaultFrameId);
            return p;
        }

        /// <summary>已解锁集合保底:null/空 → 重建为含默认 id 的数组;否则确保含默认 id(深拷贝避免共享引用)。</summary>
        private static int[] NormalizeSet(int[] src, int defaultId)
        {
            if (src == null || src.Length == 0) return new[] { defaultId };
            for (int i = 0; i < src.Length; i++)
                if (src[i] == defaultId) return (int[])src.Clone();
            // 不含默认 id → 追加(默认头像/框初始即拥有,不应因篡改丢失)
            var dst = new int[src.Length + 1];
            System.Array.Copy(src, dst, src.Length);
            dst[src.Length] = defaultId;
            return dst;
        }
    }
}
