using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 服务端权威发牌的客户端预测/对账引擎(纯逻辑,可 EditMode 单测)。
    ///
    /// 职责:持有一份与服务端 GameSession 同构的「预测权威态」(棋盘位掩码 / 分数 / 步号 / 候选队列 /
    /// 逐局发牌器),用<b>服务端签发的 seed</b> + 服务端镜像权重配置驱动发牌器,使预测发牌序列与服务端逐位一致。
    /// 落子时本地立即推进预测态(乐观,隐藏 RTT),服务端 <c>G2C_Place</c> 回包后对账:一致即确认,
    /// 任何不一致以服务端权威态整体覆盖预测态(含发牌器状态向量恢复)。
    ///
    /// 边界:本引擎只管<b>棋盘几何 + 分数 + 步号 + 候选 shapeId + 发牌器态</b>(服务端权威的全集)。
    /// 颜色 / MergeElement 叠加层 / 体力 / 合成区 / 订单等客户端经济属于 cosmetic 层,叠在 shapeId 序列之上,
    /// 由 <see cref="BlockGameState"/> / <see cref="MergeOrderState"/> 持有,不进本引擎、不上服务端发牌权威。
    /// </summary>
    /// <remarks>
    /// 预测节律与服务端 <c>GameSessionHelper</c> 完全一致(单一事实源是确定性 harness 的脚本对局):
    ///   - 候选 3 个一批;落子消耗对应候选置 0;每次落子 AddWeight(本批算法) 推进调度态;整批消耗才 OfferTrio 续发;
    ///   - 首批由开局走 OfferTrio(空盘, score 0)产生(服务端在 GameStart 内已发,客户端开局时复刻同一序列);
    ///   - 计分用 <see cref="BlockScoring"/>(双端同源)。
    /// 不一致才覆盖、一致即 no-op:确定性下覆盖是逐字段同值,无可见跳变。建局 / 对账 / 快照恢复时,服务端
    /// genState 回带 PRNG 游标(RngS0/RngS1)+ 全标量 + LastAlgo/LastTierId,经 <c>DynamicWeightDiff.ImportFullState</c>
    /// 把预测发牌器<b>完全复位到权威态(含游标)</b>:即便曾真发散,本次复位后即与服务端逐位接续,
    /// 后续整批续发(OfferTrio)产出与服务端一致,不再退化为只靠候选队列覆盖。
    /// </remarks>
    public sealed class ServerDealSync
    {
        private readonly IBlockGameGateway _gateway;

        /// <summary>权威态变更回调(建局成功 / 落子对账发生覆盖 / 快照恢复后触发),宿主据此整屏重绘。</summary>
        public Action OnAuthoritativeChanged;

        public ServerDealSync(IBlockGameGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        // ─── 预测权威态(与服务端 GameSession 同构)────────────────────
        public long GameId { get; private set; }
        public long Seed { get; private set; }
        public int Step { get; private set; }
        public int Score { get; private set; }

        /// <summary>预测棋盘(8 行位掩码,与服务端 BinaryBoard 同构)。</summary>
        public BinaryBoard Board { get; private set; } = new BinaryBoard();

        /// <summary>预测候选队列(shapeId;0 表示已消耗/空槽)。与服务端 CandidateQueue 同构。</summary>
        public readonly List<int> CandidateQueue = new List<int>();

        /// <summary>预测发牌器(服务端 seed 驱动;同 seed 同配置 → 与服务端逐位一致)。</summary>
        private DynamicWeightDiff _gen;

        /// <summary>当前候选批对应的发牌算法(整批消耗后 AddWeight 反馈用;与服务端 LastTrioAlgo 同构)。</summary>
        private AlgorithmKind _lastTrioAlgo = AlgorithmKind.RandomNoDie;

        /// <summary>是否已建局(GameStart / Snapshot 成功后为 true)。未建局时落子不应发生。</summary>
        public bool HasGame { get; private set; }

        /// <summary>上一次对账是否发生了服务端覆盖(预测与权威不一致)。供宿主决定是否整屏重绘。</summary>
        public bool LastReconcileCorrected { get; private set; }

        /// <summary>
        /// 本局是否已终局(服务端落子对账回 <c>GameOver=true</c> 后置位)。终局以<b>服务端信号为准</b>,
        /// 客户端不靠本地判 jam 自行结算。置位后 <see cref="PredictPlace"/> / <see cref="PlaceAsync"/> 一律拒绝,
        /// 避免对已删档的对局继续上报落子。下次 <see cref="StartGameAsync"/> 服务端回 <c>Resumed=false</c> 新建,
        /// <see cref="ApplyGameStart"/> 复位本标志,自然进入新局。
        /// </summary>
        public bool GameOver { get; private set; }

        /// <summary>终局权威最终分(<see cref="GameOver"/>=true 时有效;= 终局 Score)。供宿主结算展示。</summary>
        public int FinalScore { get; private set; }

        /// <summary>终局后该榜当前最佳分(服务端权威,客户端只投影展示;入榜服务不可用时为 0)。</summary>
        public long BestScore { get; private set; }

        // ─── 建局 ────────────────────────────────────────────────

        /// <summary>
        /// 应用 <c>G2C_GameStart</c>(续局语义)。两路统一:以服务端权威态(board / score / step / 候选 / genState)
        /// 对齐预测态,发牌器经 genState 完全复位(含 PRNG 游标)。
        ///   - <see cref="GameStartResult.Resumed"/>=false(新建):board 空、score 0、候选=首批 trio、step 0。
        ///   - Resumed=true(续局):board / score / step / 候选 / genState 全是恢复出的中断前权威态,整屏重绘到中断对局。
        /// </summary>
        public void ApplyGameStart(GameStartResult result)
        {
            GameId = result.GameId;
            Seed = result.Seed;

            // 发牌器:优先用服务端 genState 的 PRNG 游标完全复位(续局逐位接续);genState 缺省(理论不应发生)
            // 才退回按 seed 从头建,复刻开局序列兜底。
            if (result.Gen != null)
                RebuildGeneratorFromGen(result.Gen);
            else
                BuildGenerator(result.Seed);

            // 盘面 / 分数 / 步号以服务端权威为准(续局=恢复值;新建=0 态)。
            LoadBoard(result.Board);
            Score = result.Score;
            Step = result.Step;

            // 候选队列:genState 优先(权威,含 PRNG 复位后的当前批);genState 缺省时退回 InitialTrio。
            CandidateQueue.Clear();
            if (result.Gen != null && result.Gen.CandidateQueue != null && result.Gen.CandidateQueue.Count > 0)
            {
                CandidateQueue.AddRange(result.Gen.CandidateQueue);
            }
            else if (result.InitialTrio != null && result.InitialTrio.Count > 0)
            {
                CandidateQueue.AddRange(result.InitialTrio);
            }
            else
            {
                // 极端兜底:服务端两路候选都缺省时,用复位后的发牌器复刻一批(确定性下与服务端一致)。
                var offer = _gen.OfferTrio(Board, Score);
                _lastTrioAlgo = offer.Algo;
                for (int i = 0; i < 3; i++) CandidateQueue.Add(offer.Ids[i]);
            }

            HasGame = true;
            // 新建/续局都是一局活态的开始:复位终局态(上一局若已终局,服务端已删档、本次走新建)。
            GameOver = false;
            FinalScore = 0;
            BestScore = 0;
            // 续局须整屏重绘到恢复盘面;新建是空盘(宿主开局已建本地兜底空盘,重绘无害)。
            LastReconcileCorrected = result.Resumed;
        }

        // ─── 落子预测 ─────────────────────────────────────────────

        /// <summary>落子预测结果(供宿主即时刷新 UI;权威以随后的对账为准)。</summary>
        public sealed class PredictOutcome
        {
            public bool Accepted;            // 预测层是否接受(候选合法 + 落点合法)
            public int Step;                 // 预测推进后的步号
            public int Score;                // 预测分数
            public int ShapeId;              // 落下的形状(供 cosmetic 层着色/元素转移)
            public int EliminatedLines;      // 本步消除行列数
            public bool Refilled;            // 本步是否整批消耗后补了新批
            public List<int> ClearedRows;    // 被清行(供 cosmetic 收割元素 / 特效)
            public List<int> ClearedCols;    // 被清列
        }

        /// <summary>
        /// 本地预测落子(乐观推进预测态,与服务端 <c>Place</c> 逐操作同口径):
        /// 校验候选 + 落点 → 落子 → 消除计分 → AddWeight → 整批消耗才补批 → step+1。
        /// 不合法返回 Accepted=false 且不改态(与服务端 IllegalPlacement 同口径)。
        /// </summary>
        public PredictOutcome PredictPlace(int candidateIndex, int posX, int posY)
        {
            var outcome = new PredictOutcome { Step = Step, Score = Score, ShapeId = -1 };
            if (!HasGame) return outcome;
            // 终局后拒绝再落子:本局服务端已删档,继续预测会让本地态偏离权威(且无对应可上报的对局)。
            if (GameOver) return outcome;
            if (candidateIndex < 0 || candidateIndex >= CandidateQueue.Count) return outcome;

            int shapeId = CandidateQueue[candidateIndex];
            if (shapeId <= 0) return outcome;

            var pos = new Vec2Int(posX, posY);
            if (!Board.CanPutBlock(shapeId, pos)) return outcome;

            int placedCells = BlockShapeMap.GetCellCount(shapeId);

            Board.PutBlock(shapeId, pos);
            CandidateQueue[candidateIndex] = 0;

            var clear = Board.CanClearRowCols(true);
            int rows = clear.Rows.Count;
            int cols = clear.Cols.Count;
            int lines = rows + cols;
            int clearedCells = rows * BinaryBoard.ColCount + cols * BinaryBoard.RowCount - rows * cols;
            int placeScore = BlockScoring.PlacementScore(placedCells);
            int clearScore = lines > 0 ? BlockScoring.ClearScore(clearedCells, lines) : 0;
            Score += placeScore + clearScore;

            _gen.AddWeight(_lastTrioAlgo);

            bool refilled = false;
            if (AllConsumed())
            {
                RefillTrio();
                refilled = true;
            }

            Step++;

            outcome.Accepted = true;
            outcome.Step = Step;
            outcome.Score = Score;
            outcome.ShapeId = shapeId;
            outcome.EliminatedLines = lines;
            outcome.Refilled = refilled;
            outcome.ClearedRows = clear.Rows;
            outcome.ClearedCols = clear.Cols;
            return outcome;
        }

        // ─── 消除道具预测 ─────────────────────────────────────────

        /// <summary>消除道具预测结果(供宿主即时刷新 UI;权威以随后的对账为准)。</summary>
        public sealed class ClearToolOutcome
        {
            public bool Accepted;   // 预测层是否接受(已建局 + 非终局 + 目标格在界内)
            public int Step;        // 预测推进后的步号(接受时 = 原 step + 1)
            public int PosX;        // 目标格列(回带供宿主/单测核对)
            public int PosY;        // 目标格行
        }

        /// <summary>
        /// 本地预测消除道具(乐观推进预测态,与服务端 <c>GameSessionHelper.ClearTool</c> 逐操作同口径):
        /// 清目标格 (posX,posY) 所在整行整列(整行位掩码清零 + 每行清目标列位)、Step +1。
        /// <b>不动候选队列、不动发牌器、不计分、不触发全清判定</b>(与落子不同,消除道具只是 board-mutating 脱困动作)。
        /// 未建局 / 终局后 / 目标格越界返回 Accepted=false 且不改态(与服务端 OutOfRange / GameNotFound 同口径)。
        /// </summary>
        public ClearToolOutcome PredictClearTool(int posX, int posY)
        {
            var outcome = new ClearToolOutcome { Step = Step, PosX = posX, PosY = posY };
            if (!HasGame) return outcome;
            // 终局后拒绝:本局服务端已删档,继续预测会让本地态偏离权威(且无对应可上报的对局)。
            if (GameOver) return outcome;
            // 越界:目标格必须在 8×8 界内(与服务端 handler 越界回 OutOfRange 同口径,不推进 Step)。
            if (posX < 0 || posX >= BinaryBoard.ColCount || posY < 0 || posY >= BinaryBoard.RowCount)
                return outcome;

            // 清整行:该行位掩码全清零。
            Board.RowBinary[posY] = 0;
            // 清整列:每行清掉目标列对应的那一位(位序与 BinaryBoard 一致:bit (ColCount-col-1))。
            int colClearMask = ~(1 << (BinaryBoard.ColCount - posX - 1)) & BinaryBoard.FullRow;
            for (int r = 0; r < BinaryBoard.RowCount; r++)
                Board.RowBinary[r] &= colClearMask;

            // 消除道具作为 board-mutating 动作推进 Step +1,但不消耗候选、不 AddWeight、不 OfferTrio、不续发。
            Step++;

            outcome.Accepted = true;
            outcome.Step = Step;
            return outcome;
        }

        // ─── 对账 ────────────────────────────────────────────────

        /// <summary>
        /// 应用 <c>G2C_Place</c> 对账:任何与预测不一致(结果码非 StepAdvanced,或 step/score/board/候选/genState 有差)
        /// → 以服务端权威态整体覆盖预测态(含发牌器状态向量恢复),并置 <see cref="LastReconcileCorrected"/>=true。
        /// 一致则确认(覆盖为同值、无可见跳变)。返回是否发生了覆盖(供宿主决定整屏重绘)。
        /// 失败码(GameNotFound / NotLoggedIn / NetworkDown / ServiceUnavailable / IllegalPlacement)不覆盖本地预测,
        /// 由宿主按码处理(失败码下 board/gen 字段无效)。
        /// </summary>
        public bool ReconcilePlace(PlaceResult result)
        {
            LastReconcileCorrected = false;
            if (result == null) return false;

            switch (result.Code)
            {
                case DealResultCode.Ok:
                case DealResultCode.IdempotentReplay:
                case DealResultCode.StepAhead:
                    // 这三类服务端都回带当前权威态;以服务端为准对齐。
                    bool diverged = IsDivergent(result);
                    OverwriteFromPlace(result);
                    // 终局以服务端信号为准:本步若是 jam 终局,记权威最终分/最佳分并置终局态(后续落子被拒)。
                    if (result.GameOver)
                    {
                        GameOver = true;
                        FinalScore = result.FinalScore;
                        BestScore = result.BestScore;
                    }
                    LastReconcileCorrected = diverged;
                    return diverged;
                default:
                    // 失败码:不动预测态(board/gen 无效)。宿主据码决策(如重发 Snapshot / GameStart)。
                    return false;
            }
        }

        /// <summary>
        /// 应用 <c>G2C_ClearTool</c> 对账:三类回带权威态的码(Cleared / IdempotentReplay / StepAhead)以服务端 board/step/genState
        /// 对齐预测态,不一致触发覆盖并置 <see cref="LastReconcileCorrected"/>=true。
        /// 失败码(OutOfRange / NotEnoughEnergy / GameNotFound / NotLoggedIn / NetworkDown / ServiceUnavailable)<b>不覆盖预测态</b>
        /// (board/gen 字段无效或语义上不应覆盖):由宿主按码回滚乐观清(NotEnoughEnergy)或走本地兜底(其余)。
        /// 返回是否发生了覆盖(供宿主决定整屏重绘)。
        /// </summary>
        public bool ReconcileClearTool(ClearToolResult result)
        {
            LastReconcileCorrected = false;
            if (result == null) return false;

            switch (result.Code)
            {
                case DealResultCode.Ok:
                case DealResultCode.IdempotentReplay:
                case DealResultCode.StepAhead:
                    // 这三类服务端都回带当前权威态;以服务端为准对齐(消除道具不动发牌器,gen 回带的是当前态)。
                    bool diverged = IsDivergentClearTool(result);
                    OverwriteFromClearTool(result);
                    LastReconcileCorrected = diverged;
                    return diverged;
                default:
                    // OutOfRange / NotEnoughEnergy / 失败码:不动预测态,宿主据码回滚乐观清或兜底。
                    return false;
            }
        }

        /// <summary>应用 <c>G2C_GameSnapshot</c>:用服务端全态加载预测态 + 经 genState 完全复位发牌器(含 PRNG 游标,恢复 / 重连)。</summary>
        public void ApplySnapshot(SnapshotResult result, long gameId)
        {
            GameId = gameId;

            LoadBoard(result.Board);
            Score = result.Score;
            Step = result.Step;

            CandidateQueue.Clear();
            if (result.CandidateQueue != null) CandidateQueue.AddRange(result.CandidateQueue);

            RestoreGen(result.Gen);
            // genState 候选队列优先(权威);若 genState 缺省则用 snapshot 顶层 CandidateQueue(上面已填)。
            if (result.Gen != null && result.Gen.CandidateQueue != null && result.Gen.CandidateQueue.Count > 0)
            {
                CandidateQueue.Clear();
                CandidateQueue.AddRange(result.Gen.CandidateQueue);
            }
            else if (_gen == null)
            {
                // genState 缺省且尚无发牌器:按 seed 从头建兜底(确定性下与服务端一致)。
                BuildGenerator(Seed);
            }

            HasGame = true;
            // 快照恢复的是一局活态对局(服务端仍持有该对局);复位终局态。
            GameOver = false;
            FinalScore = 0;
            BestScore = 0;
            LastReconcileCorrected = true; // 恢复后宿主须整屏重绘
        }

        // ─── 内部:建器 / 覆盖 / 节律辅助 ─────────────────────────

        /// <summary>
        /// 按服务端 seed 建预测发牌器,与服务端 GameSessionHelper.Init 同口径初始化序列。
        /// persistence=null(逐局预测态不落客户端存储,与服务端逐局态一致)。
        /// </summary>
        private void BuildGenerator(long seed)
        {
            var rng = new XorShift128PlusRng(seed);
            var dyn = new DynamicWeightDiff(rng); // persistence 省略 = null
            dyn.ForceAlgorithm = null;
            dyn.Init(BlockGenWeightConfig.ServerMirror());
            dyn.Reset();
            dyn.BeginGame();
            dyn.LastAlgo = null;
            dyn.LastTierId = null;
            _gen = dyn;
        }

        private void LoadBoard(List<int> rowBinary)
        {
            var b = new BinaryBoard();
            if (rowBinary != null)
            {
                for (int r = 0; r < BinaryBoard.RowCount && r < rowBinary.Count; r++)
                    b.RowBinary[r] = rowBinary[r];
            }
            Board = b;
        }

        private void RefillTrio()
        {
            var offer = _gen.OfferTrio(Board, Score);
            CandidateQueue.Clear();
            for (int i = 0; i < 3; i++) CandidateQueue.Add(offer.Ids[i]);
            _lastTrioAlgo = offer.Algo;
        }

        private bool AllConsumed()
        {
            for (int i = 0; i < CandidateQueue.Count; i++)
                if (CandidateQueue[i] > 0) return false;
            return true;
        }

        /// <summary>预测态与服务端 Place 响应是否有任何分歧(step/score/board/候选/genState)。</summary>
        private bool IsDivergent(PlaceResult result)
        {
            if (Step != result.Step) return true;
            if (Score != result.Score) return true;
            if (!SameBoard(result.Board)) return true;
            if (result.Gen != null)
            {
                if (!SameList(CandidateQueue, result.Gen.CandidateQueue)) return true;
                if (_gen.DynamicWeight != result.Gen.DynamicWeight) return true;
                if (_gen.InternalPreDynamicWeight != result.Gen.PreDynamicWeight) return true;
                if (_gen.InternalRefillIndex != result.Gen.RefillIndex) return true;
                var bc = _gen.GetBoardClearState();
                if (bc.inWindow != result.Gen.BcInWindow) return true;
                if (bc.cooldown != result.Gen.BcCooldown) return true;
            }
            return false;
        }

        /// <summary>预测态与服务端 ClearTool 响应是否有任何分歧(step/board/候选/genState;消除道具不计分,不比 score)。</summary>
        private bool IsDivergentClearTool(ClearToolResult result)
        {
            if (Step != result.Step) return true;
            if (!SameBoard(result.Board)) return true;
            if (result.Gen != null)
            {
                if (!SameList(CandidateQueue, result.Gen.CandidateQueue)) return true;
                if (_gen.DynamicWeight != result.Gen.DynamicWeight) return true;
                if (_gen.InternalPreDynamicWeight != result.Gen.PreDynamicWeight) return true;
                if (_gen.InternalRefillIndex != result.Gen.RefillIndex) return true;
                var bc = _gen.GetBoardClearState();
                if (bc.inWindow != result.Gen.BcInWindow) return true;
                if (bc.cooldown != result.Gen.BcCooldown) return true;
            }
            return false;
        }

        /// <summary>以服务端 ClearTool 权威态整体覆盖预测态(step/board/genState;score 回带当前值一并对齐)。</summary>
        private void OverwriteFromClearTool(ClearToolResult result)
        {
            Step = result.Step;
            Score = result.Score;
            LoadBoard(result.Board);
            ApplyGen(result.Gen);
        }

        private bool SameBoard(List<int> serverRows)
        {
            if (serverRows == null) return true; // 无 board 字段不视为分歧
            for (int r = 0; r < BinaryBoard.RowCount; r++)
            {
                int sv = r < serverRows.Count ? serverRows[r] : 0;
                if (Board.RowBinary[r] != sv) return false;
            }
            return true;
        }

        private static bool SameList(List<int> a, List<int> b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b) || (Count(a) == 0 && Count(b) == 0);
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static int Count(List<int> l) => l == null ? 0 : l.Count;

        /// <summary>以服务端 Place 权威态整体覆盖预测态。</summary>
        private void OverwriteFromPlace(PlaceResult result)
        {
            Step = result.Step;
            Score = result.Score;
            LoadBoard(result.Board);
            ApplyGen(result.Gen);
        }

        /// <summary>把服务端 genState 应用到预测态(候选队列采用权威值 + 经 ImportFullState 完全复位发牌器含游标)。</summary>
        private void ApplyGen(GenStateView gen)
        {
            if (gen == null) return;
            if (gen.CandidateQueue != null)
            {
                CandidateQueue.Clear();
                CandidateQueue.AddRange(gen.CandidateQueue);
            }
            RestoreGen(gen);
        }

        /// <summary>
        /// 用服务端 genState 完全复位预测发牌器:以 PRNG 游标(RngS0/RngS1)重建随机源,Init 同配置,
        /// 再 ImportFullState 注入全标量 + LastAlgo/LastTierId。复位后发牌器与服务端逐位接续(含游标),
        /// 后续 OfferTrio 续发与服务端一致。同步本地 <see cref="_lastTrioAlgo"/>(整批续发前 AddWeight 反馈用)。
        /// </summary>
        private void RestoreGen(GenStateView gen)
        {
            if (gen == null) return;
            RebuildGeneratorFromGen(gen);
        }

        /// <summary>
        /// 按 genState 的 PRNG 游标 + 全标量重建发牌器实例。
        /// 步骤(与 DynamicWeightDiff.ImportFullState 契约一致):new XorShift128PlusRng(s0,s1) → Init(ServerMirror) → ImportFullState。
        /// persistence=null(逐局预测态不落客户端存储,与服务端逐局态一致)。
        /// </summary>
        private void RebuildGeneratorFromGen(GenStateView gen)
        {
            var rng = new XorShift128PlusRng(gen.RngS0, gen.RngS1);
            var dyn = new DynamicWeightDiff(rng); // persistence 省略 = null
            dyn.ForceAlgorithm = null;
            dyn.Init(BlockGenWeightConfig.ServerMirror());
            // BeginGame 会清 refillIndex/bc 窗口态;ImportFullState 随后整体覆盖,故无需先 BeginGame。
            var full = new DynamicWeightDiff.FullState(
                gen.RngS0, gen.RngS1,
                gen.DynamicWeight, gen.PreDynamicWeight, gen.RefillIndex,
                gen.BcInWindow, gen.BcCooldown,
                gen.LastAlgo, gen.LastTierId);
            dyn.ImportFullState(full);
            _gen = dyn;
            // 当前候选批算法:服务端 LastAlgo(=null 哨兵 -1 时退中性 RandomNoDie,与开局 _lastTrioAlgo 缺省同口径)。
            _lastTrioAlgo = gen.LastAlgo == DynamicWeightDiff.FullState.LastAlgoNull
                ? AlgorithmKind.RandomNoDie
                : (AlgorithmKind)gen.LastAlgo;
        }

        // ─── RPC 编排(经注入网关;纯逻辑部分仍可不走网络单测)──────────────

        /// <summary>
        /// 发 <c>C2G_GameStart</c> 开新局并应用权威初态。成功返回 true 并已建局(<see cref="HasGame"/>);
        /// 失败(网络断 / 未登录 / 服务不可用)返回 false 且不建局,宿主据此走本地兜底。成功后触发 <see cref="OnAuthoritativeChanged"/>。
        /// </summary>
        public async UniTask<bool> StartGameAsync()
        {
            var result = await _gateway.GameStartAsync();
            if (result == null || result.Code != DealResultCode.Ok) return false;
            ApplyGameStart(result);
            OnAuthoritativeChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 发 <c>C2G_Place</c> 落子并对账。<b>调用前宿主应已 <see cref="PredictPlace"/> 乐观推进</b>(用预测 step 作 baseStep)。
        /// 本方法用<b>预测前的 baseStep</b>(= 预测 step - 1)上报,服务端按 ==/&lt;/&gt; 分三分支。
        /// 返回服务端结果(含是否覆盖了本地预测);失败码下不动预测态。对账发生覆盖时触发 <see cref="OnAuthoritativeChanged"/>。
        /// </summary>
        /// <param name="baseStep">本次落子前的权威步号(= 预测推进前的 Step)。</param>
        public async UniTask<PlaceResult> PlaceAsync(int baseStep, int candidateIndex, int posX, int posY)
        {
            if (!HasGame) return PlaceResult.Fail(DealResultCode.GameNotFound);
            // 终局后拒发:本局服务端已删档,再上报落子会被回 GameNotFound,直接短路省一次往返。
            if (GameOver) return PlaceResult.Fail(DealResultCode.GameNotFound);
            var result = await _gateway.PlaceAsync(GameId, baseStep, candidateIndex, posX, posY);
            bool corrected = ReconcilePlace(result);
            if (corrected) OnAuthoritativeChanged?.Invoke();
            return result;
        }

        /// <summary>
        /// 发 <c>C2G_ClearTool</c> 上报消除道具输入并对账。<b>调用前宿主应已 <see cref="PredictClearTool"/> 乐观推进</b>
        /// (用预测前的 Step 作 baseStep)。本方法用<b>预测前的 baseStep</b>(= 预测 step - 1)上报,服务端按 ==/&lt;/&gt; 分三分支。
        /// 返回服务端结果(含最新权威态 + 体力绝对值 NewEnergy);对账发生覆盖时触发 <see cref="OnAuthoritativeChanged"/>。
        /// 失败码 / OutOfRange / NotEnoughEnergy 下不动预测态,由宿主按码回滚乐观清。
        /// </summary>
        /// <param name="baseStep">本次消除道具前的权威步号(= 预测推进前的 Step)。</param>
        public async UniTask<ClearToolResult> ClearToolAsync(int baseStep, int posX, int posY)
        {
            if (!HasGame) return ClearToolResult.Fail(DealResultCode.GameNotFound);
            // 终局后拒发:本局服务端已删档,再上报会被回 GameNotFound,直接短路省一次往返。
            if (GameOver) return ClearToolResult.Fail(DealResultCode.GameNotFound);
            var result = await _gateway.ClearToolAsync(GameId, baseStep, posX, posY);
            bool corrected = ReconcileClearTool(result);
            if (corrected) OnAuthoritativeChanged?.Invoke();
            return result;
        }

        /// <summary>发 <c>C2G_GameSnapshot</c> 取权威全态并加载(恢复 / 重连)。成功触发 <see cref="OnAuthoritativeChanged"/>。</summary>
        public async UniTask<bool> RefreshSnapshotAsync()
        {
            if (GameId == 0) return false;
            var result = await _gateway.GameSnapshotAsync(GameId);
            if (result == null || result.Code != DealResultCode.Ok) return false;
            ApplySnapshot(result, GameId);
            OnAuthoritativeChanged?.Invoke();
            return true;
        }

        /// <summary>关局(退窗):清建局标志,避免后续投影读到旧局。预测态字段保留(下次建局覆盖)。</summary>
        public void Close()
        {
            HasGame = false;
            GameId = 0;
        }

        // ─── 测试钩子 ─────────────────────────────────────────────
        /// <summary>导出当前预测发牌器全运行态(含 PRNG 游标),供单测断言复位后与服务端逐位一致。</summary>
        internal DynamicWeightDiff.FullState ExportGenFullState()
            => _gen != null ? _gen.ExportFullState() : default;
    }
}
