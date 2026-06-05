using System.Collections.Generic;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 优先级覆盖触发时机 —— 对应原游戏 OfferTriggerTimingEnum 子集。
    /// </summary>
    public enum TriggerTiming
    {
        FirstRound = 0,        // 一局开始的首次 refill
        ByExpectation = 8,     // 根据"期望"重新出块
        AfterQuanZuhe = 11,    // 全组合填空后
        AfterRevive = 12,      // 复活后
        BeforeBottom = 9,      // 棋盘见底前
        EmptyBoard = 100,      // [复刻扩展] 棋盘清空后第一次 refill
    }

    /// <summary>调度上下文 —— OfferBase.CheckIsCanWork(ctx) 用。</summary>
    public sealed class OfferContext
    {
        public TriggerTiming Trigger;
        public BinaryBoard Board;
        public int Score;
        /// <summary>本局已发过几次 trio（首次 refill 时 = 0）。</summary>
        public int RefillIndex;
        /// <summary>上一次 offer 用的算法（可为 null）。</summary>
        public AlgorithmKind? LastAlgo;
    }

    /// <summary>一条优先级覆盖 —— 对应原版 OfferBase 子类。</summary>
    public interface IOfferOverride
    {
        string Name { get; }
        TriggerTiming Trigger { get; }
        /// <summary>同一 trigger 桶内的优先级（高优先级先问）。</summary>
        int Priority { get; }
        bool CheckIsCanWork(OfferContext ctx);
        /// <summary>接管后产出 3 个 shapeId；返回 null 表示"我撤了"。</summary>
        int[] OfferNewBlocks(OfferContext ctx);
    }

    /// <summary>
    /// 全局注册表（单例）。register 后按 Priority desc 排序；
    /// Dispatch(timing, ctx) 返回首个能干活的 override 的输出，没有返回 null。
    /// </summary>
    public sealed class OfferRegistry
    {
        private static OfferRegistry _instance;
        public static OfferRegistry Instance => _instance ??= new OfferRegistry();

        private readonly Dictionary<TriggerTiming, List<IOfferOverride>> _buckets = new();

        /// <summary>上一次 dispatch 命中的 override 名（调试用）。</summary>
        public string LastHitName;

        public void Register(IOfferOverride ov)
        {
            if (!_buckets.TryGetValue(ov.Trigger, out var list))
            {
                list = new List<IOfferOverride>();
                _buckets[ov.Trigger] = list;
            }
            list.Add(ov);
            list.Sort((a, b) => b.Priority - a.Priority);
        }

        public sealed class DispatchHit
        {
            public int[] Ids;
            public string Name;
        }

        public DispatchHit Dispatch(TriggerTiming timing, OfferContext ctx)
        {
            if (!_buckets.TryGetValue(timing, out var list) || list.Count == 0) return null;
            for (int i = 0; i < list.Count; i++)
            {
                var ov = list[i];
                if (!ov.CheckIsCanWork(ctx)) continue;
                var ids = ov.OfferNewBlocks(ctx);
                if (ids != null && ids.Length == 3)
                {
                    LastHitName = ov.Name;
                    return new DispatchHit { Ids = ids, Name = ov.Name };
                }
            }
            return null;
        }

        public void Clear() { _buckets.Clear(); LastHitName = null; }

        public int SizeOf(TriggerTiming timing)
            => _buckets.TryGetValue(timing, out var l) ? l.Count : 0;
    }

    // ─────────────────────────────────────────────────────────────────
    // 默认 override 集合 —— 对应原版 FirstRound_PromoteCombo / RandomRunEmpty
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 一局的第一次 refill 走 FILL 算法，确保玩家开局就能消除，建立"会消行"的预期。
    /// </summary>
    public sealed class FirstRoundPromoteCombo : IOfferOverride
    {
        public string Name => "FirstRound_PromoteCombo";
        public TriggerTiming Trigger => TriggerTiming.FirstRound;
        public int Priority => 2;
        public bool CheckIsCanWork(OfferContext ctx) => ctx.RefillIndex == 0;
        public int[] OfferNewBlocks(OfferContext ctx) => Algorithms.BlockAlgorithms.GenerateTrio(AlgorithmKind.Fill, ctx.Board);
    }

    /// <summary>
    /// 当棋盘是空的时（如刚清盘）出纯随机无死局，避免连续输出"死亡难题"。
    /// </summary>
    public sealed class EmptyBoardRandomRun : IOfferOverride
    {
        public string Name => "RandomRunEmpty";
        public TriggerTiming Trigger => TriggerTiming.EmptyBoard;
        public int Priority => 3;
        public bool CheckIsCanWork(OfferContext ctx) => ctx.Board.IsEmpty();
        public int[] OfferNewBlocks(OfferContext ctx) => Algorithms.BlockAlgorithms.GenerateTrio(AlgorithmKind.RandomNoDie, ctx.Board);
    }

    /// <summary>默认注册（在 DynamicWeightDiff.Init 时调用一次）。</summary>
    public static class OfferOverridesRegistration
    {
        public static void RegisterDefaults()
        {
            var reg = OfferRegistry.Instance;
            reg.Clear();
            reg.Register(new FirstRoundPromoteCombo());
            reg.Register(new EmptyBoardRandomRun());
        }
    }
}
