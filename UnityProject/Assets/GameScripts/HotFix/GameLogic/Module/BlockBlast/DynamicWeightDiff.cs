using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 主分发器：按 weightcfg 选 tier、按 tier 内 odds 抽算法、调用算法生成 trio。
    /// 模仿原游戏 main_bundle.js 中的 DynamicWeightDiff。
    ///
    /// 逐局实例(无单例):每实例自持随机源 <see cref="IRandomSource"/> + 全部跨手调度态,
    /// 两局各 new 各跑、互不干扰,可被服务端多局并发安全使用。生成路径上无进程级可变状态、无持久化,
    /// 生成核心闭包 0 Unity 依赖,服务端无需 stub 即可链接。跨手调度态经 <see cref="RestoreState"/> /
    /// <see cref="ImportFullState"/> 由服务端权威态注入对账,不落任何本地存储。
    /// </summary>
    public sealed class DynamicWeightDiff
    {
        /// <summary>清屏窗口阈值：低于此分数启用窗口/冷却循环。</summary>
        private const int BoardClearScoreThreshold = 15000;
        private const int BoardClearCooldownMin = 1;
        private const int BoardClearCooldownMax = 2;

        /// <summary>本局随机源:所有发牌随机经此实例取值,不走进程级全局态。</summary>
        private readonly IRandomSource _rng;

        private List<WeightConfigEntry> _weightConfig = new List<WeightConfigEntry>();
        private bool _initialized;
        private int _dynamicWeight;
        private int _preDynamicWeight;

        /// <summary>最近一次调度的算法/tier id（调试/UI 用）。null 表示未初始化或未激活。</summary>
        public AlgorithmKind? LastAlgo;
        public int? LastTierId;

        /// <summary>E1 强制算法（HUD 调试用，非 null 时所有 refill 都用这个算法）。</summary>
        public AlgorithmKind? ForceAlgorithm;

        /// <summary>本局已发过几次 trio（用于 FirstRound 触发判断）。</summary>
        private int _refillIndex;

        /// <summary>清屏窗口：是否在 FILL 持续期内（持续到棋盘清空）。</summary>
        private bool _bcInWindow;
        /// <summary>清屏冷却：窗口结束后强制走普通调度的回合数。</summary>
        private int _bcCooldown;

        /// <summary>
        /// 构造逐局实例。发牌调度态不落任何本地存储(服务端权威发牌下,跨手态经服务端 genState 对账注入)。
        /// </summary>
        /// <param name="rng">本局随机源。null 时退回基于时间种子的 System.Random(客户端本地兜底路径默认行为)。</param>
        public DynamicWeightDiff(IRandomSource rng = null)
        {
            _rng = rng ?? new SystemRandomSource(new Random());
        }

        /// <summary>把 System.Random 适配成 IRandomSource(保留客户端默认时间种子路径,与去单例化前一致)。</summary>
        private sealed class SystemRandomSource : IRandomSource
        {
            private readonly Random _r;
            public SystemRandomSource(Random r) => _r = r;
            public double NextDouble() => _r.NextDouble();
            public int Next(int minInclusive, int maxExclusive) => _r.Next(minInclusive, maxExclusive);
        }

        public bool IsInitialized() => _initialized;

        public int DynamicWeight => _dynamicWeight;

        public string LastAlgorithmName
            => LastAlgo == null ? "(none)" : AlgorithmNames.Of(LastAlgo.Value);

        /// <summary>用 weightcfg 初始化。可重复调用（替换配置）。</summary>
        public void Init(IList<WeightConfigEntry> cfg)
        {
            _weightConfig = cfg == null ? new List<WeightConfigEntry>() : new List<WeightConfigEntry>(cfg);
            _initialized = true;
            OfferOverridesRegistration.RegisterDefaults();
        }

        /// <summary>一局新开始时调用，清零 refillIndex（用于 FirstRound 时机）。</summary>
        public void BeginGame()
        {
            _refillIndex = 0;
            _bcInWindow = false;
            _bcCooldown = 0;
        }

        public (bool inWindow, int cooldown) GetBoardClearState() => (_bcInWindow, _bcCooldown);

        public sealed class OfferResult
        {
            public int[] Ids;
            public AlgorithmKind Algo;
            public int? TierId;
        }

        /// <summary>
        /// 主入口：给定棋盘 + 当前分数，返回 (3 个 shapeId, 实际用的算法)。
        /// 未激活（score &lt; 1000）或未初始化 → 直接 RANDOM_NO_DIE。
        ///
        /// 在 score &lt; 15000 时叠加"清屏窗口"：进入窗口后每回合强制走 FILL/贪心钥匙，
        /// 直到玩家把棋盘真正清空，触发冷却 1~2 回合走普通调度，结束后再开窗口。
        /// </summary>
        public OfferResult OfferTrio(BinaryBoard board, int score)
        {
            OfferResult result;

            if (score < BoardClearScoreThreshold)
            {
                bool boardEmpty = board.IsEmpty();
                if (_bcCooldown > 0)
                {
                    _bcCooldown--;
                    _bcInWindow = false;
                    result = OfferTrioInner(board, score);
                }
                else if (_bcInWindow && boardEmpty)
                {
                    _bcInWindow = false;
                    _bcCooldown = _rng.Next(BoardClearCooldownMin, BoardClearCooldownMax + 1);
                    result = OfferTrioInner(board, score);
                }
                else
                {
                    _bcInWindow = true;
                    var ids = Algorithms.BlockAlgorithms.BoardClearGreedyTrio(board, _rng);
                    LastAlgo = AlgorithmKind.Fill;  // 复用 FILL 标签做权重反馈
                    LastTierId = -3;                 // -3 表示"清屏窗口接管"
                    _refillIndex++;
                    result = new OfferResult { Ids = ids, Algo = AlgorithmKind.Fill, TierId = -3 };
                }
            }
            else
            {
                _bcInWindow = false;
                _bcCooldown = 0;
                result = OfferTrioInner(board, score);
            }

            // 早期屏蔽过滤
            if (score < BlockShapeMap.EarlyGameBlockScoreThreshold)
            {
                result.Ids = ApplyEarlyGameFilter(board, result.Ids);
            }
            // 非清屏窗口期：3 个候选不允许出现重复图形
            if (!_bcInWindow)
            {
                result.Ids = DedupTrioIds(result.Ids, board, score);
            }
            return result;
        }

        /// <summary>
        /// 当前 tier 选择：dynamicWeight 落在 FactorRange 内 AND 分数落在 HighScoreRange 内。
        /// 找不到精确 tier 时退化到"最接近"的；分数低于激活门槛返回 null。
        /// </summary>
        private WeightConfigEntry GetCurrentTier(int score)
        {
            if (_weightConfig.Count == 0) return null;
            int dw = _dynamicWeight;
            var matches = new List<WeightConfigEntry>();
            foreach (var t in _weightConfig)
            {
                int fLow = Math.Min(t.FactorLow, t.FactorHigh);
                int fHigh = Math.Max(t.FactorLow, t.FactorHigh);
                if (dw < fLow || dw > fHigh) continue;
                if (score < t.HighScoreMin) continue;
                if (t.HighScoreMax >= 0 && score > t.HighScoreMax) continue;
                matches.Add(t);
            }
            if (matches.Count > 0) return matches[0];

            var scoreOk = new List<WeightConfigEntry>();
            foreach (var t in _weightConfig)
            {
                if (score < t.HighScoreMin) continue;
                if (t.HighScoreMax >= 0 && score > t.HighScoreMax) continue;
                scoreOk.Add(t);
            }
            var pool = scoreOk.Count > 0 ? scoreOk : _weightConfig;
            WeightConfigEntry best = pool[0];
            double bestDist = double.PositiveInfinity;
            foreach (var t in pool)
            {
                double mid = (t.FactorLow + t.FactorHigh) / 2.0;
                double d = Math.Abs(mid - dw);
                if (d < bestDist) { bestDist = d; best = t; }
            }
            return best;
        }

        /// <summary>在指定 tier 内按 8 个 odds 加权抽算法。</summary>
        private AlgorithmKind PickAlgorithmFromTier(WeightConfigEntry tier)
        {
            var odds = tier.OddsArray();
            int total = 0;
            for (int i = 0; i < odds.Length; i++) total += odds[i];
            if (total <= 0) return AlgorithmKind.RandomNoDie;
            double r = _rng.NextDouble() * total;
            for (int i = 0; i < odds.Length; i++)
            {
                if (odds[i] > 0 && r < odds[i]) return (AlgorithmKind)i;
                r -= odds[i];
            }
            return AlgorithmKind.RandomNoDie;
        }

        /// <summary>
        /// 无尽模式早期屏蔽过滤：把 ids 中命中 EARLY_GAME_BLOCKED 的换成池中其它形状；
        /// 若替换后整组无解，再用过滤后的池子重采样无死亡 trio。
        /// </summary>
        private int[] ApplyEarlyGameFilter(BinaryBoard board, int[] ids)
        {
            var pool = new List<int>();
            foreach (int id in BlockShapeMap.CommonShapeIds)
            {
                if (!BlockShapeMap.EarlyGameBlockedIds.Contains(id)) pool.Add(id);
            }
            if (pool.Count == 0) return ids;

            var replaced = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                replaced[i] = BlockShapeMap.EarlyGameBlockedIds.Contains(ids[i])
                    ? pool[_rng.Index(pool.Count)]
                    : ids[i];
            }
            if (board.CheckPutAllBlocks(replaced)) return replaced;
            for (int i = 0; i < 50; i++)
            {
                var t = new[]
                {
                    pool[_rng.Index(pool.Count)],
                    pool[_rng.Index(pool.Count)],
                    pool[_rng.Index(pool.Count)],
                };
                if (board.CheckPutAllBlocks(t)) return t;
            }
            return new[] { 1, 1, 1 };
        }

        /// <summary>
        /// 去重：trio 内出现重复 shapeId 时，从池里替换为未出现的形状，
        /// 优先选能保持整组可放的替换。
        /// </summary>
        private int[] DedupTrioIds(int[] ids, BinaryBoard board, int score)
        {
            bool blockedEarly = score < BlockShapeMap.EarlyGameBlockScoreThreshold;
            var pool = new List<int>();
            foreach (int id in BlockShapeMap.CommonShapeIds)
            {
                if (blockedEarly && BlockShapeMap.EarlyGameBlockedIds.Contains(id)) continue;
                pool.Add(id);
            }
            var result = new int[ids.Length];
            Array.Copy(ids, result, ids.Length);
            for (int i = 0; i < result.Length; i++)
            {
                // 首次出现，跳过
                bool firstTime = true;
                for (int j = 0; j < i; j++) if (result[j] == result[i]) { firstTime = false; break; }
                if (firstTime) continue;

                var used = new HashSet<int>(result);
                var candidates = new List<int>();
                foreach (int c in pool) if (!used.Contains(c)) candidates.Add(c);
                if (candidates.Count == 0) break;
                _rng.Shuffle(candidates);
                int picked = -1;
                foreach (int c in candidates)
                {
                    var trial = new int[result.Length];
                    Array.Copy(result, trial, result.Length);
                    trial[i] = c;
                    if (board.CheckPutAllBlocks(trial)) { picked = c; break; }
                }
                if (picked == -1) picked = candidates[0];
                result[i] = picked;
            }
            return result;
        }

        private OfferResult OfferTrioInner(BinaryBoard board, int score)
        {
            // E1 强制算法优先（HUD 调试通道）
            if (ForceAlgorithm.HasValue)
            {
                var algo = ForceAlgorithm.Value;
                LastAlgo = algo;
                LastTierId = -1;
                var ids = Algorithms.BlockAlgorithms.GenerateTrio(algo, board, _rng);
                _refillIndex++;
                return new OfferResult { Ids = ids, Algo = algo, TierId = -1 };
            }

            // 优先级覆盖层
            var ctx = new OfferContext
            {
                Trigger = _refillIndex == 0 ? TriggerTiming.FirstRound : TriggerTiming.EmptyBoard,
                Board = board,
                Score = score,
                RefillIndex = _refillIndex,
                LastAlgo = LastAlgo,
                Rng = _rng,
            };
            var hit = OfferRegistry.Instance.Dispatch(ctx.Trigger, ctx);
            if (hit != null)
            {
                // override 不属于动态调度算法，记一个伪 algo 让 addWeight 用中性反馈
                var algo = AlgorithmKind.RandomNoDie;
                LastAlgo = algo;
                LastTierId = -2; // -2 表示"被 override 覆盖"
                _refillIndex++;
                return new OfferResult { Ids = hit.Ids, Algo = algo, TierId = -2 };
            }

            int activation = GameConfigBB.ActivationScore;
            if (!_initialized || score < activation)
            {
                var algo = AlgorithmKind.RandomNoDie;
                LastAlgo = algo;
                LastTierId = null;
                var ids = Algorithms.BlockAlgorithms.GenerateTrio(algo, board, _rng);
                _refillIndex++;
                return new OfferResult { Ids = ids, Algo = algo, TierId = null };
            }
            var tier = GetCurrentTier(score);
            if (tier == null)
            {
                var algo = AlgorithmKind.RandomNoDie;
                LastAlgo = algo;
                LastTierId = null;
                var ids = Algorithms.BlockAlgorithms.GenerateTrio(algo, board, _rng);
                _refillIndex++;
                return new OfferResult { Ids = ids, Algo = algo, TierId = null };
            }
            {
                var algo = PickAlgorithmFromTier(tier);
                var ids = Algorithms.BlockAlgorithms.GenerateTrio(algo, board, _rng);
                LastAlgo = algo;
                LastTierId = tier.Id;
                _refillIndex++;
                return new OfferResult { Ids = ids, Algo = algo, TierId = tier.Id };
            }
        }

        /// <summary>
        /// 调用时机：每次发完 trio 后调用。同向连续走 Consecutive，换向用 Basic。
        /// </summary>
        public void AddWeight(AlgorithmKind algo)
        {
            var f = GameConfigBB.FactorFor(algo);
            if (f == null) return;
            bool sameDirection = _preDynamicWeight * f.Basic > 0;
            int delta = sameDirection ? f.Consecutive : f.Basic;
            _preDynamicWeight = delta;
            _dynamicWeight += delta;
            // 截断到 weightcfg 实际覆盖的 FactorRange 范围。
            // 注：源 TS 用 ±9999 sentinel + Math.min/max，因 sentinel 比配置值还宽 → clamp 实际无效。
            //     这里修复为按真实最紧边界收敛（配置为空时退回 ±9999 兜底）。
            int min = int.MaxValue, max = int.MinValue;
            foreach (var t in _weightConfig)
            {
                int lo = System.Math.Min(t.FactorLow, t.FactorHigh);
                int hi = System.Math.Max(t.FactorLow, t.FactorHigh);
                if (lo < min) min = lo;
                if (hi > max) max = hi;
            }
            if (min == int.MaxValue) { min = -9999; max = 9999; }
            if (_dynamicWeight < min) _dynamicWeight = min;
            if (_dynamicWeight > max) _dynamicWeight = max;
        }

        public void Reset()
        {
            _dynamicWeight = 0;
            _preDynamicWeight = 0;
            _refillIndex = 0;
        }

        // 测试钩子
        internal int InternalRefillIndex => _refillIndex;
        internal int InternalPreDynamicWeight => _preDynamicWeight;
        internal void InternalSetWeight(int dynamicWeight, int preDynamicWeight)
        {
            _dynamicWeight = dynamicWeight;
            _preDynamicWeight = preDynamicWeight;
        }

        /// <summary>
        /// 把跨手累积调度态整体注入(对账用):服务端权威响应回带 <c>BlockGenState</c> 的标量向量
        /// (dynamicWeight/preDynamicWeight/refillIndex/bcInWindow/bcCooldown)后,客户端预测发牌器据此对齐到权威态。
        /// 纯状态注入、不改生成语义(GetCurrentTier / OfferTrio / AddWeight 全程只读这些字段),也不触持久化。
        /// </summary>
        /// <remarks>
        /// 不含 PRNG 游标:<c>BlockGenState</c> 不携带 xorshift 内部字,故无从恢复随机流游标。确定性下
        /// 预测与服务端逐位一致 → 游标天然同步、对账只是覆盖标量(无可见跳变);真发散时本次对齐标量 + 候选队列后,
        /// 下一批预测若仍偏(游标已错位)会再次被服务端响应里的候选队列覆盖纠正——候选队列在每个响应里都权威回带。
        /// 故正确性靠「每次响应都以服务端候选队列为准」保证,本方法只负责把后续预测的调度判据钉到权威值。
        /// </remarks>
        public void RestoreState(int dynamicWeight, int preDynamicWeight, int refillIndex,
            bool bcInWindow, int bcCooldown)
        {
            _dynamicWeight = dynamicWeight;
            _preDynamicWeight = preDynamicWeight;
            _refillIndex = refillIndex;
            _bcInWindow = bcInWindow;
            _bcCooldown = bcCooldown;
        }

        // ─── 全运行态导出 / 导入(续局持久化用) ─────────────────────────
        // RestoreState 只对齐 5 个调度标量,不含 PRNG 游标 → 仅够「确定性下对账钉判据」。
        // 续局(中断后从持久态精确续接发牌序列)还需:① RNG 游标(两个 ulong)② LastAlgo/LastTierId。
        // 下面两个方法把「全运行态」= RNG 游标 + 5 标量 + LastAlgo/LastTierId 一次性导出 / 导入,
        // 使重建实例与中断时刻的实例逐位接续。纯状态搬运,不改生成语义(OfferTrio/AddWeight 全程只读这些字段)。

        /// <summary>发牌器全运行态快照:协议 / 持久化的中立载体。</summary>
        public readonly struct FullState
        {
            public readonly ulong RngS0;
            public readonly ulong RngS1;
            public readonly int DynamicWeight;
            public readonly int PreDynamicWeight;
            public readonly int RefillIndex;
            public readonly bool BcInWindow;
            public readonly int BcCooldown;
            /// <summary>LastAlgo 序号;-1 = null(未激活/未发牌)。</summary>
            public readonly int LastAlgo;
            /// <summary>LastTierId;int.MinValue = null。</summary>
            public readonly int LastTierId;

            public FullState(ulong rngS0, ulong rngS1, int dynamicWeight, int preDynamicWeight,
                int refillIndex, bool bcInWindow, int bcCooldown, int lastAlgo, int lastTierId)
            {
                RngS0 = rngS0;
                RngS1 = rngS1;
                DynamicWeight = dynamicWeight;
                PreDynamicWeight = preDynamicWeight;
                RefillIndex = refillIndex;
                BcInWindow = bcInWindow;
                BcCooldown = bcCooldown;
                LastAlgo = lastAlgo;
                LastTierId = lastTierId;
            }

            public const int LastAlgoNull = -1;
            public const int LastTierNull = int.MinValue;
        }

        /// <summary>
        /// 导出全运行态。RNG 游标取自本实例所持随机源(必须是 <see cref="XorShift128PlusRng"/>,
        /// 续局路径恒以 portable RNG 构造);非 portable 源(客户端历史时间种子兜底)游标置 0,
        /// 此时续局不适用(只有确定性 portable RNG 能逐位接续)。
        /// </summary>
        public FullState ExportFullState()
        {
            ulong s0 = 0, s1 = 0;
            if (_rng is XorShift128PlusRng portable)
            {
                var st = portable.ExportState();
                s0 = st.s0;
                s1 = st.s1;
            }
            return new FullState(
                s0, s1,
                _dynamicWeight, _preDynamicWeight, _refillIndex, _bcInWindow, _bcCooldown,
                LastAlgo.HasValue ? (int)LastAlgo.Value : FullState.LastAlgoNull,
                LastTierId ?? FullState.LastTierNull);
        }

        /// <summary>
        /// 导入全运行态的「标量 + LastAlgo/LastTierId」部分(RNG 游标由调用方在构造本实例时经
        /// <see cref="XorShift128PlusRng(ulong,ulong)"/> 复位,故本方法不触 RNG)。
        /// 续局重建步骤:new DynamicWeightDiff(new XorShift128PlusRng(s0,s1)) → Init(同配置) → ImportFullState(state)。
        /// 纯状态注入,不改生成语义、不触持久化。
        /// </summary>
        public void ImportFullState(in FullState state)
        {
            _dynamicWeight = state.DynamicWeight;
            _preDynamicWeight = state.PreDynamicWeight;
            _refillIndex = state.RefillIndex;
            _bcInWindow = state.BcInWindow;
            _bcCooldown = state.BcCooldown;
            LastAlgo = state.LastAlgo == FullState.LastAlgoNull ? (AlgorithmKind?)null : (AlgorithmKind)state.LastAlgo;
            LastTierId = state.LastTierId == FullState.LastTierNull ? (int?)null : state.LastTierId;
        }
    }
}
