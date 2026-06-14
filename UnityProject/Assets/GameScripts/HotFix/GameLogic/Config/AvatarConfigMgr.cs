using System.Collections.Generic;
using GameLogic.BlockBlast.Player;

namespace GameLogic.Config
{
    /// <summary>
    /// 头像/框底层配置管理器（设计 18 §3.5 / §五 C2）。
    /// 桥接 Luban 生成的 <c>GameConfig.Avatar</c>（行）/ <c>GameConfig.avatar.TbAvatar</c>（表）→ POCO
    /// （<see cref="AvatarEntry"/>），让业务侧不直接依赖 Luban 类型；按 id 查 / 按 type 列。仿 <see cref="ItemConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 加载分两路：运行期 <see cref="EnsureLoaded"/> 走 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）；
    /// EditMode 单测经 <see cref="InitForTest"/> 注入，绕 ConfigSystem（纯逻辑可测）。
    /// </remarks>
    public static class AvatarConfigMgr
    {
        private static Dictionary<int, AvatarEntry> _avatars;   // id → 头像/框定义

        // ── 单行桥接：Luban 行 → POCO ─────────────────────────────

        /// <summary>头像/框行：Luban <c>GameConfig.Avatar</c> → POCO <see cref="AvatarEntry"/>。</summary>
        public static AvatarEntry ToEntry(GameConfig.Avatar row)
        {
            return new AvatarEntry
            {
                Id = row.Id,
                Type = (int)row.Type,
                Image = row.Image,
                UnlockText = row.UnlockText,
                UnlockCond = (int)row.UnlockCond,
                UnlockParam = row.UnlockParam,
            };
        }

        // ── 运行期加载（经 ConfigSystem / YooAsset）────────────────

        /// <summary>运行期建缓存：经 <c>ConfigSystem</c>（YooAsset）。已灌（含 InitForTest 注入）则直接返回。</summary>
        public static void EnsureLoaded()
        {
            if (_avatars != null) return;
            var table = ConfigSystem.Instance.Tables.TbAvatar;
            _avatars = new Dictionary<int, AvatarEntry>(table.DataList.Count);
            foreach (var row in table.DataList)
            {
                _avatars[row.Id] = ToEntry(row);
            }
        }

        // ── 查询 ───────────────────────────────────────────────

        /// <summary>按 id 查头像/框；查不到返 null（不抛）。</summary>
        public static AvatarEntry GetAvatar(int id)
        {
            EnsureLoaded();
            return _avatars.TryGetValue(id, out var e) ? e : null;
        }

        /// <summary>按 type（<see cref="AvatarType"/>）列出全部该类型项（不抛，查无返空集合）。</summary>
        public static IReadOnlyList<AvatarEntry> GetByType(int type)
        {
            EnsureLoaded();
            var list = new List<AvatarEntry>();
            foreach (var e in _avatars.Values)
                if (e.Type == type) list.Add(e);
            return list;
        }

        /// <summary>全部头像/框项（解锁同步 SyncLevelUnlocks 用）。</summary>
        public static IReadOnlyCollection<AvatarEntry> All()
        {
            EnsureLoaded();
            return _avatars.Values;
        }

        // ── 测试注入 / 隔离 ─────────────────────────────────────

        /// <summary>测试注入口：绕开 ConfigSystem，直接灌 POCO 列表（EditMode / 纯 C# 单测用）。</summary>
        public static void InitForTest(IEnumerable<AvatarEntry> avatars)
        {
            _avatars = new Dictionary<int, AvatarEntry>();
            if (avatars != null)
            {
                foreach (var e in avatars) _avatars[e.Id] = e;
            }
        }

        /// <summary>清空缓存（测试隔离用，下次查询会重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _avatars = null;
        }
    }
}
