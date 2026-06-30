using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 发牌器状态向量的中立投影(协议 <c>BlockGenState</c> 的客户端拷贝)。
    /// 协议对象在 await 后会被对象池回收,跨边界后须复制成本类型持有;故网关返回前逐字段拷出。
    /// 含 PRNG 游标(<see cref="RngS0"/>/<see cref="RngS1"/>)+ <see cref="LastAlgo"/>/<see cref="LastTierId"/>
    /// 后,客户端可经 <c>DynamicWeightDiff.ImportFullState</c> 把预测发牌器完全复位到权威态(含游标),
    /// 真发散一次即逐位自愈,不再退化为只靠候选队列覆盖。
    /// </summary>
    public sealed class GenStateView
    {
        /// <summary>当前候选队列(shapeId;0 表示已消耗/空槽)。权威值,对账时直接采用。</summary>
        public readonly List<int> CandidateQueue;
        public readonly int DynamicWeight;
        public readonly int PreDynamicWeight;
        public readonly int RefillIndex;
        public readonly bool BcInWindow;
        public readonly int BcCooldown;
        /// <summary>xorshift128+ 内部状态字 s0(发牌游标;以 ulong 位型承载)。</summary>
        public readonly ulong RngS0;
        /// <summary>xorshift128+ 内部状态字 s1(发牌游标;以 ulong 位型承载)。</summary>
        public readonly ulong RngS1;
        /// <summary>当前候选批所用算法序号;-1 = null(未激活/未发牌)。与 FullState.LastAlgoNull 同口径。</summary>
        public readonly int LastAlgo;
        /// <summary>当前候选批所在 tier id;int.MinValue = null。与 FullState.LastTierNull 同口径。</summary>
        public readonly int LastTierId;

        public GenStateView(List<int> candidateQueue, int dynamicWeight, int preDynamicWeight,
            int refillIndex, bool bcInWindow, int bcCooldown,
            ulong rngS0 = 0, ulong rngS1 = 0,
            int lastAlgo = -1, int lastTierId = int.MinValue)
        {
            CandidateQueue = candidateQueue ?? new List<int>();
            DynamicWeight = dynamicWeight;
            PreDynamicWeight = preDynamicWeight;
            RefillIndex = refillIndex;
            BcInWindow = bcInWindow;
            BcCooldown = bcCooldown;
            RngS0 = rngS0;
            RngS1 = rngS1;
            LastAlgo = lastAlgo;
            LastTierId = lastTierId;
        }
    }

    /// <summary>网关层统一的请求结果码(把协议结果码 + 传输失败收敛成一个枚举,业务层不直接认协议类型)。</summary>
    public enum DealResultCode
    {
        /// <summary>请求成功(GameStart / Snapshot 成功;Place 的 StepAdvanced)。</summary>
        Ok = 0,
        /// <summary>幂等命中(Place:baseStep &lt; 权威 step,服务端回当前权威态)。</summary>
        IdempotentReplay = 1,
        /// <summary>步号超前(Place:baseStep &gt; 权威 step,服务端回当前权威态)。</summary>
        StepAhead = 2,
        /// <summary>落子非法(服务端权威校验拒绝)。</summary>
        IllegalPlacement = 3,
        /// <summary>对局不存在(需重新 GameStart 或 Snapshot)。</summary>
        GameNotFound = 4,
        /// <summary>会话未登录。</summary>
        NotLoggedIn = 5,
        /// <summary>网络不可用 / 未连接。</summary>
        NetworkDown = 6,
        /// <summary>服务不可用(超时 / 异常 / 空响应)。</summary>
        ServiceUnavailable = 7,
    }

    /// <summary>
    /// C2G_GameStart 响应(续局语义:有在局恢复 / 无则新建)。失败时 Code != Ok 且 GameId/Gen 字段无效。
    /// <see cref="Resumed"/>=true 时 Step/Score/InitialTrio/Board/Gen 全是恢复出的中断前权威态;
    /// =false 时为开局 0 态(空盘、score 0、首批 trio、step 0)。
    /// </summary>
    public sealed class GameStartResult
    {
        public readonly DealResultCode Code;
        public readonly long GameId;
        public readonly long Seed;
        public readonly List<int> InitialTrio;
        public readonly int Step;
        public readonly GenStateView Gen;
        /// <summary>是否为续局恢复(true=恢复已有对局;false=新建)。</summary>
        public readonly bool Resumed;
        /// <summary>当前权威分数(新建 = 0;续局 = 恢复值)。</summary>
        public readonly int Score;
        /// <summary>当前权威棋盘 8 行位掩码(新建=空盘;续局=恢复盘面)。</summary>
        public readonly List<int> Board;

        public GameStartResult(DealResultCode code, long gameId, long seed,
            List<int> initialTrio, int step, GenStateView gen,
            bool resumed = false, int score = 0, List<int> board = null)
        {
            Code = code;
            GameId = gameId;
            Seed = seed;
            InitialTrio = initialTrio ?? new List<int>();
            Step = step;
            Gen = gen;
            Resumed = resumed;
            Score = score;
            Board = board ?? new List<int>();
        }

        public static GameStartResult Fail(DealResultCode code)
            => new GameStartResult(code, 0, 0, null, 0, null);
    }

    /// <summary>C2G_Place 响应(落子权威结果)。</summary>
    public sealed class PlaceResult
    {
        public readonly DealResultCode Code;
        public readonly int Step;
        public readonly int Score;
        public readonly int EliminatedLines;
        /// <summary>本步整批消耗后补入的队首新候选 shapeId;未补入为 -1。</summary>
        public readonly int NewCandidate;
        /// <summary>权威棋盘 8 行位掩码。</summary>
        public readonly List<int> Board;
        public readonly GenStateView Gen;

        public PlaceResult(DealResultCode code, int step, int score, int eliminatedLines,
            int newCandidate, List<int> board, GenStateView gen)
        {
            Code = code;
            Step = step;
            Score = score;
            EliminatedLines = eliminatedLines;
            NewCandidate = newCandidate;
            Board = board ?? new List<int>();
            Gen = gen;
        }

        public static PlaceResult Fail(DealResultCode code)
            => new PlaceResult(code, 0, 0, 0, -1, null, null);
    }

    /// <summary>C2G_GameSnapshot 响应(恢复/重连用的权威全态)。</summary>
    public sealed class SnapshotResult
    {
        public readonly DealResultCode Code;
        public readonly List<int> Board;
        public readonly int Score;
        public readonly int Step;
        public readonly List<int> CandidateQueue;
        public readonly GenStateView Gen;

        public SnapshotResult(DealResultCode code, List<int> board, int score, int step,
            List<int> candidateQueue, GenStateView gen)
        {
            Code = code;
            Board = board ?? new List<int>();
            Score = score;
            Step = step;
            CandidateQueue = candidateQueue ?? new List<int>();
            Gen = gen;
        }

        public static SnapshotResult Fail(DealResultCode code)
            => new SnapshotResult(code, null, 0, 0, null, null);
    }
}
