using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>棋盘皮肤模式（设计 50 §二）。</summary>
    public enum SkinMode
    {
        /// <summary>彩色态：方块按类型各自固定配色（初始态，设计 50 §三 规则 1）。</summary>
        Colored = 0,
        /// <summary>单色态：全盘所有方块不分类型统一显示同一张单色 sprite（设计 50 §二）。</summary>
        Mono = 1,
    }

    /// <summary>
    /// 方块皮肤状态机（设计 50）。纯逻辑、无 Unity 依赖、可单测：
    /// 全清触发的「彩色 → 单色 → 换色（防相邻重复）」状态机 + 单色随机选取 + 续存导出/导入 + 加载校验保底。
    ///
    /// 渲染层（彩色按类型取图 / 单色全盘统一取当前标识那张）由窗口承接；本类只管「皮肤态」这个量本身。
    /// 候选池由调用方注入（设计 50 §四「候选池 = 真实存在标识集」，运行期传 <see cref="BlockSkinCatalog.MonoIds"/>，
    /// 单测传构造池），保证逻辑与具体资源解耦、可测。
    /// </summary>
    public sealed class BlockSkinState
    {
        /// <summary>「未选」哨兵值：彩色态下当前单色标识无意义（设计 50 §二）。</summary>
        public const int Unselected = -1;

        /// <summary>当前皮肤模式。初始 = 彩色（设计 50 §三 规则 1）。</summary>
        public SkinMode Mode { get; private set; } = SkinMode.Colored;

        /// <summary>当前单色标识（单色态全盘在用那张的编号）。彩色态 = <see cref="Unselected"/>。</summary>
        public int MonoId { get; private set; } = Unselected;

        /// <summary>是否单色态（渲染层据此分叉）。</summary>
        public bool IsMono => Mode == SkinMode.Mono;

        /// <summary>重置为初始彩色态（设计 50 §三 规则 1）。调用方在加载存档前先 Reset、再 Import 覆盖（同元层口径）。</summary>
        public void Reset()
        {
            Mode = SkinMode.Colored;
            MonoId = Unselected;
        }

        /// <summary>
        /// 整体置位皮肤态（供悔棋快照 Restore 用，设计 50）。绕过状态机规则直接覆盖——
        /// 仅用于「回滚到落子前的真实历史态」，不经此做正常玩法推进（正常推进走 <see cref="OnAllClear"/>）。
        /// </summary>
        public void RestoreState(SkinMode mode, int monoId)
        {
            Mode = mode;
            MonoId = monoId;
        }

        /// <summary>
        /// 发生一次全清事件时调用（设计 50 §三）。状态机推进：
        /// - 彩色态 → 转单色，从候选池等概率随机选 1 张（首次全池可选）。
        /// - 单色态 → 排除「当前在用那张」后等概率随机选 1 张（防相邻重复，设计 50 §四）。
        /// - 退化兜底：候选池为空 → 不变（无可选）；排除当前后为空（池仅 1 张）→ 保持当前不变。
        /// 单色态永不退回彩色（设计 50 §三 规则 4）。返回 true 表示当前单色标识发生了变化。
        /// </summary>
        public bool OnAllClear(IReadOnlyList<int> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                // 候选池空：彩色态无张可转（保持彩色）；单色态保持当前（不变量兜底）。
                return false;
            }

            int previous = MonoId;
            Mode = SkinMode.Mono;                  // 首次全清转单色；已单色保持单色（规则 4）。
            MonoId = PickExcluding(pool, previous);
            return MonoId != previous;
        }

        /// <summary>
        /// 从候选池等概率随机选 1 张，排除 <paramref name="exclude"/>（防相邻重复）。
        /// exclude 不在池中（首次全清 exclude=Unselected）→ 全池可选。
        /// 排除后可选集为空（池仅 1 张且 = exclude）→ 退化兜底返回 exclude（保持不变，设计 50 §四 退化兜底）。
        /// </summary>
        private static int PickExcluding(IReadOnlyList<int> pool, int exclude)
        {
            // 收集可选项（≠ exclude）。
            var candidates = new List<int>(pool.Count);
            foreach (var id in pool)
                if (id != exclude) candidates.Add(id);

            if (candidates.Count == 0) return exclude; // 退化：池仅 1 张 = 当前 → 无可换，保持。
            return candidates[RandomSource.Index(candidates.Count)];
        }

        // ── 续存（设计 50 §六）──────────────────────────────────
        // 导出/导入挂在皮肤状态自身（纯静态/实例方法，无 IO），由宿主 DTO 平铺两字段承载、走既有落盘外壳。

        /// <summary>把当前皮肤态写进 DTO（设计 50 §六）。纯方法、无 IO。</summary>
        public void Export(MergeMetaSave dto)
        {
            if (dto == null) return;
            dto.skinMono = Mode == SkinMode.Mono;
            dto.skinMonoId = Mode == SkinMode.Mono ? MonoId : Unselected;
        }

        /// <summary>
        /// 从 DTO 读回皮肤态（设计 50 §六，逐字段保底 + 加载校验）。纯方法、无 IO。
        /// - 彩色态（skinMono=false，含旧档缺字段 JsonUtility 给缺省 false）→ 彩色 + 未选（设计 50 §六 缺字段保底 / A6a）。
        /// - 单色态但当前标识不在候选池（旧档 / 篡改 / 资源缺失，设计 50 §六 / A6b）→ 保持单色态、从池重随机选一张；
        ///   池为空（异常）→ 退回彩色 + 未选（不留空标识、不抛异常）。
        /// dto 为 null 直接返回（保持现状缺省 = 彩色态，等价首次）。
        /// </summary>
        public void Import(MergeMetaSave dto, IReadOnlyList<int> pool)
        {
            if (dto == null) return;

            if (!dto.skinMono)
            {
                Mode = SkinMode.Colored;
                MonoId = Unselected;
                return;
            }

            // 单色态：校验当前标识合法性（在候选池内 = 真实存在标识，设计 50 §六）。
            if (pool != null && Contains(pool, dto.skinMonoId))
            {
                Mode = SkinMode.Mono;
                MonoId = dto.skinMonoId;
                return;
            }

            // 非法标识兜底（A6b 默认方向：单色态重随机，保「已达成全清」语义）。
            if (pool != null && pool.Count > 0)
            {
                Mode = SkinMode.Mono;
                MonoId = pool[RandomSource.Index(pool.Count)];
            }
            else
            {
                // 候选池异常为空：无张可选 → 退回彩色 + 未选（不抛异常、不留空标识）。
                Mode = SkinMode.Colored;
                MonoId = Unselected;
            }
        }

        private static bool Contains(IReadOnlyList<int> pool, int id)
        {
            foreach (var n in pool) if (n == id) return true;
            return false;
        }
    }
}
