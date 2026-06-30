using System;
using System.Collections.Generic;
using GameLogic.BlockBlast.Algorithms;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// Block Blast 全局游戏状态（单例）。继承 TEngine 的 Singleton&lt;T&gt;。
    /// 仅 Classic 模式（v1 不含 Adventure/Collection）。
    /// </summary>
    public sealed class BlockGameState : SimpleSingleton<BlockGameState>
    {
        private const string StorageKey = "block_blast_save_v1";

        /// <summary>8×8 棋盘：-1=空，0..7=BlockColor 索引。</summary>
        public int[][] SaveArr;

        /// <summary>手持的 3 个候选方块（落子后置 null）。</summary>
        public PendingPiece[] OperaArr = new PendingPiece[3];

        /// <summary>当前局得分。</summary>
        public int Score;
        /// <summary>历史最高分（Classic）。</summary>
        public int HighScore;
        /// <summary>连击数（连续消除 +1，未消除清零）。</summary>
        public int Combo;

        // ─── 合成+订单+体力模式（Merge-Order Demo 切片）─────────────
        // 红线：所有 merge-order 状态/逻辑由 MergeOrderMode 门控；off 时落子/消除/补块/存档
        //       与 Classic 现状逐字节一致（回归硬验收）。

        /// <summary>与 SaveArr 平行的元素叠加层（None=该格无元素）。仅 merge-order 模式分配/使用。</summary>
        public MergeElement[][] ElementArr;

        /// <summary>合成+订单+体力模式开关。off 时所有 merge-order 分支短路，Classic 行为零变化。</summary>
        public bool MergeOrderMode;

        /// <summary>该模式的新系统状态（合成区/订单/体力/保底）。仅 MergeOrderMode 时非空。</summary>
        public MergeOrderState MergeState;

        /// <summary>
        /// 本局发牌调度器(逐局实例,替代旧的进程级单例)。客户端一次只跑一局,故挂在 BlockGameState 上、
        /// 与本局同生命周期;服务端多局并发各自 new 各自的实例。随机源用基于时间种子的 System.Random
        /// (保留去单例化前的默认行为),持久化经 <see cref="Persistence.Provider"/>(沿用旧存储键 + 调度态)。
        /// </summary>
        private DynamicWeightDiff _dynamic;

        /// <summary>本局发牌调度器实例(首次访问时按客户端默认随机源 + 本地持久化创建)。</summary>
        public DynamicWeightDiff Dynamic => _dynamic ??= new DynamicWeightDiff(null, Persistence.Provider);

        /// <summary>
        /// 注入逐局发牌调度器(测试钩子)。确定性 harness 用固定随机源 new 一个实例注入,使经
        /// <see cref="RefillPieces"/> 的客户端发牌路径可复现。生产不调用(走 <see cref="Dynamic"/> 惰性默认)。
        /// </summary>
        internal void InjectDynamic(DynamicWeightDiff dyn) => _dynamic = dyn;

        /// <summary>
        /// 玩法窗活态就绪钩子(P1 全栈迁移·客户端段)。<see cref="ResetForMergeOrder"/> 末尾(局内存档加载之后)以新建的
        /// <see cref="MergeState"/> 触发。接线层(OrderSync 经 GameApp)据此把活态切服务端权威 + 接交付 RPC + 应用已缓存订单快照
        /// (覆盖 blob 旧 normal 订单)。null(纯逻辑单测/无网络)→ 不触发,保持本地行为零回归(沿 MergeMetaPersistence.OnSaved 范式)。
        /// </summary>
        public static Action<MergeOrderState> OnMergeStateReady;

        protected override void OnInit()
        {
            SaveArr = MakeEmptyBoard();
            OperaArr = new PendingPiece[3];
            Score = 0;
            HighScore = 0;
            Combo = 0;
            ElementArr = null;
            MergeOrderMode = false;
            MergeState = null;
        }

        private static MergeElement[][] MakeEmptyElementArr()
        {
            var b = new MergeElement[8][];
            for (int r = 0; r < 8; r++)
            {
                b[r] = new MergeElement[8]; // 默认 None(=0)
            }
            return b;
        }

        private static int[][] MakeEmptyBoard()
        {
            var b = new int[8][];
            for (int r = 0; r < 8; r++)
            {
                b[r] = new int[8];
                for (int c = 0; c < 8; c++) b[r][c] = -1;
            }
            return b;
        }

        /// <summary>重置到新一局开始状态（清棋盘 + 清槽 + 清分数 + 补满 3 块）。</summary>
        public void ResetForLevel(BinaryBoard board)
        {
            SaveArr = MakeEmptyBoard();
            OperaArr = new PendingPiece[3];
            Score = 0;
            Combo = 0;
            RefillPieces(board);
        }

        /// <summary>从 39 个白名单形状 ID 中均匀随机抽。</summary>
        private int RandomShapeId()
        {
            // 无尽模式分数 < 10000 时剔除 EARLY_GAME_BLOCKED
            IReadOnlyList<int> pool = BlockShapeMap.CommonShapeIds;
            if (Score < BlockShapeMap.EarlyGameBlockScoreThreshold)
            {
                var filtered = new List<int>();
                foreach (int id in BlockShapeMap.CommonShapeIds)
                {
                    if (!BlockShapeMap.EarlyGameBlockedIds.Contains(id)) filtered.Add(id);
                }
                pool = filtered;
            }
            return pool[RandomSource.Index(pool.Count)];
        }

        private static BlockColor RandomColor()
        {
            int idx = RandomSource.Range(0, 8);
            return (BlockColor)idx;
        }

        public PendingPiece BuildPiece(int shapeId)
        {
            return new PendingPiece(shapeId, RandomColor());
        }

        /// <summary>
        /// merge-order 模式补牌:把 <see cref="MergeOrderState.PendingElements"/> 队头元素按「trio 级容量加权
        /// 随机」分摊到 3 块候选块。每元素以 1 格 = 1 票的均权抽签落入某块,大块(cellCount 高)统计上拿到更多、
        /// 小块少但每块都有机会;队列吃光或 3 块全满止。各块内部按入桶序填到 Elements 前若干格(余格 None),
        /// 与 <see cref="PlacePiece"/> 行优先转移到 ElementArr 的顺序同源。
        /// </summary>
        /// <remarks>短路条件:模式 off / 无 MergeState / 队列空 / trio 总容量 0。off 时 piece.Elements 全保持
        /// null,经典模式逐字节零回归。</remarks>
        private void DistributePendingElementsAcrossTrio(IList<PendingPiece> trio)
        {
            if (!MergeOrderMode || MergeState == null || trio == null) return;
            var queue = MergeState.PendingElements;
            if (queue.Count == 0) return;

            int n = trio.Count;
            if (n == 0) return;

            // 每块剩余空格(cellCount=0 的块自动不参与抽签)
            var capacity = new int[n];
            int totalCap = 0;
            for (int i = 0; i < n; i++)
            {
                int cells = BlockShapeMap.GetCellCount(trio[i].ShapeId);
                if (cells < 0) cells = 0;
                capacity[i] = cells;
                totalCap += cells;
            }
            if (totalCap == 0) return;

            // 入桶序由抽签顺序决定;桶预设为每块 cellCount(末尾余格保持 None)
            var buckets = new MergeElement[n][];
            var bucketWrite = new int[n];
            for (int i = 0; i < n; i++)
            {
                if (capacity[i] > 0) buckets[i] = new MergeElement[capacity[i]];
            }

            while (queue.Count > 0 && totalCap > 0)
            {
                int pick = RandomSource.Index(totalCap); // [0, totalCap)
                // 按累计区间定位中签块(跳过 capacity==0 的块)
                int target = -1;
                int acc = 0;
                for (int i = 0; i < n; i++)
                {
                    if (capacity[i] == 0) continue;
                    acc += capacity[i];
                    if (pick < acc) { target = i; break; }
                }
                if (target < 0) break; // 防御:理论上 totalCap>0 时必命中

                buckets[target][bucketWrite[target]++] = queue.Dequeue();
                capacity[target]--;
                totalCap--;
            }

            // 写回:命中过的块挂上 Elements(余格已是 None);未命中的块保持 Elements=null
            for (int i = 0; i < n; i++)
            {
                if (bucketWrite[i] > 0) trio[i].Elements = buckets[i];
            }
        }

        /// <summary>3 个形状互不重复的随机 trio（池耗尽时回落到允许重复）。</summary>
        private List<PendingPiece> RandomDistinctTrio()
        {
            IReadOnlyList<int> pool = BlockShapeMap.CommonShapeIds;
            if (Score < BlockShapeMap.EarlyGameBlockScoreThreshold)
            {
                var filtered = new List<int>();
                foreach (int id in BlockShapeMap.CommonShapeIds)
                {
                    if (!BlockShapeMap.EarlyGameBlockedIds.Contains(id)) filtered.Add(id);
                }
                pool = filtered;
            }
            var available = new List<int>(pool);
            var ids = new List<int>(3);
            for (int i = 0; i < 3 && available.Count > 0; i++)
            {
                int idx = RandomSource.Index(available.Count);
                ids.Add(available[idx]);
                available.RemoveAt(idx);
            }
            while (ids.Count < 3) ids.Add(pool[RandomSource.Index(pool.Count)]);
            var trio = new List<PendingPiece>(3);
            for (int i = 0; i < 3; i++) trio.Add(BuildPiece(ids[i]));
            return trio;
        }

        /// <summary>
        /// 当 OperaArr 全空时补满 3 个。
        /// - board=null：纯加权随机
        /// - score &lt; ActivationScore：随机无死亡（加形状去重）
        /// - 分数 ≥ ActivationScore 且 DynamicWeightDiff 已初始化：动态调度
        /// 兜底退到 3 个 1×1。
        /// </summary>
        public void RefillPieces(BinaryBoard board)
        {
            bool allEmpty = true;
            for (int i = 0; i < OperaArr.Length; i++)
            {
                if (OperaArr[i] != null) { allEmpty = false; break; }
            }
            if (!allEmpty) return;

            // 走动态调度
            var dyn = Dynamic;
            if (board != null && dyn.IsInitialized())
            {
                var off = dyn.OfferTrio(board, Score);
                for (int i = 0; i < 3; i++)
                {
                    var p = BuildPiece(off.Ids[i]);
                    p.SetAlgo(off.Algo);
                    OperaArr[i] = p;
                }
                DistributePendingElementsAcrossTrio(OperaArr);
                return;
            }

            // 旧行为：随机无死亡 + 形状去重
            List<PendingPiece> chosen = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                var trio = RandomDistinctTrio();
                if (board == null) { chosen = trio; break; }
                var ids = new int[3];
                for (int i = 0; i < 3; i++) ids[i] = trio[i].ShapeId;
                if (board.CheckPutAllBlocks(ids)) { chosen = trio; break; }
            }
            if (chosen == null)
            {
                chosen = new List<PendingPiece>(3)
                {
                    new PendingPiece(1, RandomColor()),
                    new PendingPiece(1, RandomColor()),
                    new PendingPiece(1, RandomColor()),
                };
            }
            for (int i = 0; i < 3; i++) OperaArr[i] = chosen[i];
            DistributePendingElementsAcrossTrio(OperaArr);
        }

        /// <summary>首发 3 个固定形状（默认 [9,39,24]），颜色随机。不带算法标签。</summary>
        public void SetFirstHand()
        {
            var ids = GameConfigBB.FirstHand;
            for (int i = 0; i < 3; i++) OperaArr[i] = BuildPiece(ids[i]);
            DistributePendingElementsAcrossTrio(OperaArr);
        }

        /// <summary>把指定槽位的方块放置到棋盘上（不做校验）。</summary>
        public void PlacePiece(int slotIdx, BinaryBoard board, int posCol, int posRow)
        {
            var piece = OperaArr[slotIdx];
            if (piece == null) return;

            // 1) 更新二进制棋盘
            board.PutBlock(piece.ShapeId, new Vec2Int(posCol, posRow));

            // 2) 更新 SaveArr（带颜色）
            int colorIdx = (int)piece.Color;
            var shape = BlockShapeMap.Get(piece.ShapeId);
            if (shape != null)
            {
                // merge-order 模式：按相同的「填充格行优先顺序」把 piece.Elements[cellIdx] 转移到 ElementArr。
                // off 时 transferElements=false（ElementArr/Elements 均 null），SaveArr 结果逐字节不变。
                bool transferElements = MergeOrderMode && ElementArr != null && piece.Elements != null;
                int cellIdx = 0;
                for (int r = 0; r < shape.Height; r++)
                {
                    for (int c = 0; c < shape.Width; c++)
                    {
                        int colBit = (shape.Shape[r] >> (shape.Width - c - 1)) & 1;
                        if (colBit != 0)
                        {
                            SaveArr[posRow + r][posCol + c] = colorIdx;
                            if (transferElements && cellIdx < piece.Elements.Length)
                            {
                                var el = piece.Elements[cellIdx];
                                if (el != MergeElement.None)
                                    ElementArr[posRow + r][posCol + c] = el;
                            }
                            cellIdx++;
                        }
                    }
                }
            }

            // 3) 清空槽位
            OperaArr[slotIdx] = null;

            // 4) 动态难度反馈
            if (piece.HasAlgo)
            {
                Dynamic.AddWeight(piece.Algo);
            }
        }

        /// <summary>
        /// 清除棋盘上指定的行/列（在 BinaryBoard.CanClearRowCols 之后调用），返回清掉的格数。
        /// 返回值供计分（Classic 计分、merge-order 得分驱动元素生成）使用。
        /// </summary>
        public int ClearRowsAndCols(IList<int> rows, IList<int> cols)
        {
            int cleared = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int r = rows[i];
                for (int c = 0; c < 8; c++)
                {
                    if (SaveArr[r][c] != -1) { SaveArr[r][c] = -1; cleared++; }
                }
            }
            for (int i = 0; i < cols.Count; i++)
            {
                int c = cols[i];
                for (int r = 0; r < 8; r++)
                {
                    if (SaveArr[r][c] != -1) { SaveArr[r][c] = -1; cleared++; }
                }
            }
            return cleared;
        }

        /// <summary>
        /// 消除道具（设计 49 §3.1）：清掉指定格 (row,col) 所在的「一整行 + 一整列」全部已占格——
        /// 同步清 SaveArr（方块色）、ElementArr（元素 overlay，merge-order 模式）、BinaryBoard（位掩码）。
        /// 用于卡死脱困，让棋盘重新可落（清一行一列后必有贯通空行/空列，设计 49 §3.1「朝可落前进」）。
        /// 返回清掉的格数。清「一行一列」不清空全盘，故不触发全清判定（设计 49 §四：全清奖不被白嫖）。
        /// 越界 row/col 直接返回 0、不动状态。<paramref name="board"/> 为 null 时只清 SaveArr/ElementArr。
        /// </summary>
        public int ClearToolRowCol(BinaryBoard board, int row, int col)
        {
            if (SaveArr == null) return 0;
            if (row < 0 || row >= 8 || col < 0 || col >= 8) return 0;

            int cleared = 0;
            for (int c = 0; c < 8; c++) cleared += ClearToolCellAt(row, c);
            for (int r = 0; r < 8; r++)
            {
                if (r == row) continue; // 交叉格 (row,col) 已在行 pass 清过，跳过避免重复计数
                cleared += ClearToolCellAt(r, col);
            }

            if (board != null) board.ConvertFromArr(SaveArr); // 位掩码与 SaveArr 重新对齐
            return cleared;
        }

        /// <summary>清单格的方块色 + 元素 overlay（消除道具用）；该格已空返 0、不重复计数。</summary>
        private int ClearToolCellAt(int r, int c)
        {
            if (SaveArr[r][c] == -1) return 0;
            SaveArr[r][c] = -1;
            if (MergeOrderMode && ElementArr != null) ElementArr[r][c] = MergeElement.None;
            return 1;
        }

        // ─── merge-order 模式专用方法（全部由 MergeOrderMode 门控）────────────

        /// <summary>
        /// 统计被清行/列上的元素并清该格 overlay，经 <paramref name="output"/> 输出本次被清元素列表
        /// 供合成区摄入。应在窗口拿到 CanClearRowCols 结果后、与 ClearRowsAndCols 配套调用。
        /// 与 ClearRowsAndCols 一致：行列交叉格只计一次（行 pass 已清，列 pass 见 None 跳过）。
        /// 返回本次被清的元素总数。模式 off 时返回 0、不做任何事。
        /// </summary>
        public int HarvestClearedElements(IList<int> rows, IList<int> cols, List<MergeElement> output = null)
        {
            if (!MergeOrderMode || ElementArr == null) return 0;
            int gained = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                int r = rows[i];
                for (int c = 0; c < 8; c++) gained += HarvestAt(r, c, output);
            }
            for (int i = 0; i < cols.Count; i++)
            {
                int c = cols[i];
                for (int r = 0; r < 8; r++) gained += HarvestAt(r, c, output);
            }
            return gained;
        }

        private int HarvestAt(int r, int c, List<MergeElement> output)
        {
            var el = ElementArr[r][c];
            if (el == MergeElement.None) return 0;
            ElementArr[r][c] = MergeElement.None;
            output?.Add(el);
            return 1;
        }

        /// <summary>
        /// 重置进入合成+订单+体力 Demo：开启 MergeOrderMode + 空棋盘 + 清元素层 + 新建 MergeState
        /// （起始体力/空合成区/初始订单/空元素预算队列）+ 补满 3 块。反复进入每次都从初始态开始。
        /// 候选块元素由 PendingElements 队列驱动（开局队空→首手纯方块），DrainPendingElementsInto 读
        /// MergeState，故须在 RefillPieces 之前建好 MergeState。
        /// </summary>
        public void ResetForMergeOrder(BinaryBoard board)
        {
            MergeOrderMode = true;
            SaveArr = MakeEmptyBoard();
            ElementArr = MakeEmptyElementArr();
            OperaArr = new PendingPiece[3];
            Score = 0;
            Combo = 0;
            MergeState = new MergeOrderState();
            MergeState.Reset();
            // 跨会话磁盘存档(设计 14 §3.4):Reset 先跑建好局内瞬态 + 元层缺省,再用存档覆盖元层。
            // 两者字段不重叠(§3.1),ImportMeta 只动元字段、不触局内瞬态。无存档 / 加载失败 → 保持
            // Reset 缺省,等价首次游玩(旧路径零回归)。加载走同步 Provider 读(非阻塞,不触红线,见 MergeMetaPersistence.Load)。
            var meta = MergeMetaPersistence.Load();
            if (meta != null) MergeState.ImportMeta(meta);
            // 进入即按真实时差补算时基恢复(含离线,设计 49 §3.2 / 设计 14 §3.7):
            // 有存档则按「上次记录时刻 → now」补离线恢复;无存档(Reset 后 LastEnergyRegenTime==0)则以 now 初始化、本次不补。
            MergeState.ApplyTimeRegen(MergeMetaPersistence.NowUnixSec());

            // 局内态续存(2026-06-22 决定:真无尽局内态续存):有快照则恢复盘面/元素层/手牌/合成区/订单/连消,
            // 等价从上次落子处继续;无快照(首次/清档)走原缺省路径(空盘 + 补满 3 块)。
            // 须在 Reset + ImportMeta 之后:局内字段不与元层重叠,本步只覆盖局内现场。
            var ingame = MergeIngamePersistence.Load();
            if (ingame == null || !ImportIngame(ingame, board))
            {
                if (board != null) board.ConvertFromArr(SaveArr);
            }

            // 订单按时整批刷新(含离线):须在 ImportIngame 之后——订单与刷新记录时刻属局内层,先恢复再按真实时差判定。
            // 有记录则按「上次刷新时刻 → now」判是否到点整批换新;无记录(Reset 后 LastOrderRefreshTime==0)则以 now 初始化、本次不刷。
            long nowForOrders = MergeMetaPersistence.NowUnixSec();
            MergeState.ApplyOrderRefresh(nowForOrders);
            // 边界保险:存档恰好全空(交付完最后一单瞬间崩溃/退出)重进 → 立即整批补回,避免卡在空订单区干等到时刷新。
            MergeState.TryRefreshIfAllDelivered(nowForOrders);

            RefillPieces(board); // 全空才补:恢复后手牌非空则 no-op;恢复后恰好全空(上次落子未补)则补满

            // 活态就绪钩子(P1):在局内存档加载(ImportIngame)之后触发,接线层据此应用服务端订单快照、覆盖 blob 旧 normal 订单。
            // 钩子内若已缓存登录快照即整份覆盖上面本地建好的订单;无网络/单测时钩子为 null,保持本地订单不变。
            OnMergeStateReady?.Invoke(MergeState);
        }

        /// <summary>
        /// 玩法窗活态退出钩子(P1):<see cref="ExitMergeOrder"/> 丢弃 <see cref="MergeState"/> 前触发,接线层据此解绑旧活态,
        /// 避免后续快照推送打到已弃用的 state。null(单测/无网络)→ 不触发(沿 <see cref="OnMergeStateReady"/> 范式)。
        /// </summary>
        public static Action<MergeOrderState> OnMergeStateClosed;

        /// <summary>退出 merge-order Demo：关闭门控 + 释放元素层 + 丢弃 MergeState，回到 Classic 零残留。</summary>
        public void ExitMergeOrder()
        {
            if (MergeState != null) OnMergeStateClosed?.Invoke(MergeState);
            MergeOrderMode = false;
            ElementArr = null;
            MergeState = null;
        }

        /// <summary>加分。</summary>
        public void AddScore(int amount)
        {
            Score += amount;
            if (Score > HighScore) HighScore = Score;
        }

        // ─── HighScore 并入元层存档（设计 29 §5.4）──────────────────
        // 经典「最高分」是跨会话长期指标，并入 MergeMetaSave 元层、与元层进度同一加载/落盘时机
        // （由 GameContext 在 LoadPlayer / SavePlayer 中枢节点串联，与 PlayerInfo 同口径）。
        // 纯方法、无 IO：在 HighScore 字段与 DTO 平铺字段之间转换，落盘外壳仍是既有 MergeMetaPersistence。
        // 与 block_blast_save_v1（棋盘/手牌/分数 = 局内瞬态）分层：局内瞬态每局重开、不进元层（设计 14 判据）。

        /// <summary>把当前 <see cref="HighScore"/> 写进元层 DTO（增量，不动既有玩法/玩家字段）。纯方法、无 IO。</summary>
        public void ExportHighScoreToMeta(MergeMetaSave dto)
        {
            if (dto == null) return;
            dto.highScore = HighScore;
        }

        /// <summary>
        /// 从元层 DTO 读回 <see cref="HighScore"/>（设计 29 §5.4）。逐字段保底：负值（缺省 0 / 篡改）夹到 0。
        /// dto 为 null 直接返回（保持现有 HighScore，等价首次无元层最高分）。
        /// </summary>
        public void ImportHighScoreFromMeta(MergeMetaSave dto)
        {
            if (dto == null) return;
            HighScore = dto.highScore > 0 ? dto.highScore : 0;
        }

        // ─── 持久化（局内瞬态：棋盘/手牌/分数，键 block_blast_save_v1，每局重建不进元层）────

        [Serializable]
        private sealed class SaveData
        {
            public int[] flatBoard; // 8×8 拍平
            public PendingPieceData[] opera;
            public int score;
            public int highScore;
        }

        [Serializable]
        private sealed class PendingPieceData
        {
            public bool isNull;
            public int shapeId;
            public int color;
            public bool hasAlgo;
            public int algo;
        }

        public void Save()
        {
            try
            {
                var data = new SaveData
                {
                    flatBoard = new int[64],
                    opera = new PendingPieceData[3],
                    score = Score,
                    highScore = HighScore,
                };
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                        data.flatBoard[r * 8 + c] = SaveArr[r][c];
                for (int i = 0; i < 3; i++)
                {
                    var p = OperaArr[i];
                    var pd = new PendingPieceData();
                    if (p == null) pd.isNull = true;
                    else
                    {
                        pd.isNull = false;
                        pd.shapeId = p.ShapeId;
                        pd.color = (int)p.Color;
                        pd.hasAlgo = p.HasAlgo;
                        pd.algo = (int)p.Algo;
                    }
                    data.opera[i] = pd;
                }
                Persistence.Provider.Set(StorageKey, UnityEngine.JsonUtility.ToJson(data));
            }
            catch { /* ignore */ }
        }

        public bool Load()
        {
            try
            {
                if (!Persistence.Provider.TryGet(StorageKey, out string raw) || string.IsNullOrEmpty(raw)) return false;
                var data = UnityEngine.JsonUtility.FromJson<SaveData>(raw);
                if (data == null) return false;
                if (data.flatBoard != null && data.flatBoard.Length == 64)
                {
                    SaveArr = MakeEmptyBoard();
                    for (int r = 0; r < 8; r++)
                        for (int c = 0; c < 8; c++)
                            SaveArr[r][c] = data.flatBoard[r * 8 + c];
                }
                OperaArr = new PendingPiece[3];
                if (data.opera != null)
                {
                    for (int i = 0; i < 3 && i < data.opera.Length; i++)
                    {
                        var pd = data.opera[i];
                        if (pd == null || pd.isNull) { OperaArr[i] = null; continue; }
                        var p = new PendingPiece(pd.shapeId, (BlockColor)pd.color);
                        if (pd.hasAlgo) p.SetAlgo((AlgorithmKind)pd.algo);
                        OperaArr[i] = p;
                    }
                }
                Score = data.score;
                HighScore = data.highScore;
                return true;
            }
            catch { return false; }
        }

        // ─── 局内态续存:导出/导入对局现场(2026-06-22 决定:真无尽局内态续存)──────────
        // 与 block_blast_save_v1(Classic 局内键)分开:merge-order 局内现场含 ElementArr + 候选块元素 + MergeState
        // 经济现场,信息量更大,走独立 DTO/键(MergeIngameSave / MergeIngamePersistence)。本类只管盘面/元素层/手牌/
        // 得分/连击,合成区/订单/连消委托 MergeState.ExportIngame/ImportIngame。

        /// <summary>导出 merge-order 局内现场到 DTO（盘面 + 元素层 + 手牌 + 得分/连击 + 合成区/订单/连消）。纯方法、无 IO。</summary>
        public void ExportIngame(MergeIngameSave dto)
        {
            if (dto == null) return;

            dto.flatBoard = new int[64];
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    dto.flatBoard[r * 8 + c] = SaveArr[r][c];

            dto.flatElements = new int[64];
            if (ElementArr != null)
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                        dto.flatElements[r * 8 + c] = (int)ElementArr[r][c];

            dto.hand = new IngamePieceData[3];
            for (int i = 0; i < 3; i++)
            {
                var p = OperaArr[i];
                var pd = new IngamePieceData();
                if (p == null) pd.isNull = true;
                else
                {
                    pd.isNull = false;
                    pd.shapeId = p.ShapeId;
                    pd.color = (int)p.Color;
                    pd.hasAlgo = p.HasAlgo;
                    pd.algo = (int)p.Algo;
                    if (p.Elements != null)
                    {
                        pd.elements = new int[p.Elements.Length];
                        for (int e = 0; e < p.Elements.Length; e++) pd.elements[e] = (int)p.Elements[e];
                    }
                }
                dto.hand[i] = pd;
            }

            dto.score = Score;
            dto.combo = Combo;

            MergeState?.ExportIngame(dto);
        }

        /// <summary>
        /// 从 DTO 恢复 merge-order 局内现场（设计 14 §3.5 同口径逐字段保底）。纯方法、无 IO（board 仅做位掩码重算）。
        /// flatBoard 非法（null / 长度≠64）直接返回 false（调用方走缺省空盘）。手牌/元素层逐项夹合法，
        /// 合成区/订单/连消委托 <see cref="MergeOrderState.ImportIngame"/>。成功返回 true 并已重算 BinaryBoard。
        /// </summary>
        public bool ImportIngame(MergeIngameSave dto, BinaryBoard board)
        {
            if (dto == null) return false;
            if (dto.flatBoard == null || dto.flatBoard.Length != 64) return false;

            SaveArr = MakeEmptyBoard();
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    SaveArr[r][c] = dto.flatBoard[r * 8 + c];

            ElementArr = MakeEmptyElementArr();
            if (dto.flatElements != null && dto.flatElements.Length == 64)
                for (int r = 0; r < 8; r++)
                    for (int c = 0; c < 8; c++)
                        ElementArr[r][c] = (MergeElement)dto.flatElements[r * 8 + c];

            OperaArr = new PendingPiece[3];
            if (dto.hand != null)
            {
                for (int i = 0; i < 3 && i < dto.hand.Length; i++)
                {
                    var pd = dto.hand[i];
                    if (pd == null || pd.isNull) { OperaArr[i] = null; continue; }
                    var p = new PendingPiece(pd.shapeId, (BlockColor)pd.color);
                    if (pd.hasAlgo) p.SetAlgo((AlgorithmKind)pd.algo);
                    if (pd.elements != null && pd.elements.Length > 0)
                    {
                        var els = new MergeElement[pd.elements.Length];
                        for (int e = 0; e < pd.elements.Length; e++) els[e] = (MergeElement)pd.elements[e];
                        p.Elements = els;
                    }
                    OperaArr[i] = p;
                }
            }

            Score = dto.score > 0 ? dto.score : 0;
            Combo = dto.combo > 0 ? dto.combo : 0;
            if (Score > HighScore) HighScore = Score;

            MergeState?.ImportIngame(dto);

            if (board != null) board.ConvertFromArr(SaveArr);
            return true;
        }
    }
}
