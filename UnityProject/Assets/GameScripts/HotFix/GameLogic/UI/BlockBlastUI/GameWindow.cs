using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
using GameLogic.Config;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// Block Blast 主玩法窗口（Classic 模式）。纯几何 UGUI 渲染 + 拖拽落子。
    /// 移植自源项目 Game.ts 的核心交互循环（落子/消除/补充/GameOver）。
    /// </summary>
    [Window(UILayer.UI, location: "GameWindow", fullScreen: true)]
    public sealed class GameWindow : UIWindow
    {
        private const int N = BlockLayout.BoardSize;

        private BlockGameState _state;
        private BinaryBoard _board;

        private RectTransform _content;
        private RectTransform _boardLayer;
        private RectTransform _slotLayer;
        private RectTransform _ghostLayer;

        private readonly Image[,] _cellImages = new Image[N, N];
        private readonly RectTransform[] _slotContainers = new RectTransform[3];
        private readonly Image[] _ghostPool = new Image[N * N];
        private int _ghostUsed;

        private Text _scoreText;
        private Text _bestText;
        private int _displayedScore;
        private int _initialHigh;

        private int _draggingShapeId = -1;
        private bool _gameOverTriggered;
        private bool _newBestTriggered;

        protected override void OnCreate()
        {
            _state = BlockGameState.Instance;
            _board = new BinaryBoard();

            // ── 数据层：新开一局 Classic ──
            _state.Load();                 // 读 highScore
            _initialHigh = _state.HighScore;

            // 确保动态权重已初始化（ConfigSystem 懒加载）
            if (!DynamicWeightDiff.Instance.IsInitialized())
            {
                try { WeightCfgConfigMgr.InitDynamicWeight(); }
                catch (System.Exception e) { Log.Warning($"[GameWindow] 权重表加载失败，退化随机：{e.Message}"); }
            }

            _state.SaveArr = MakeEmptyBoard();
            _board.ConvertFromArr(_state.SaveArr);
            DynamicWeightDiff.Instance.BeginGame();
            // 初始 3 块随机（清空槽后走动态调度；空棋盘下回落到随机无死局）
            _state.OperaArr[0] = null;
            _state.OperaArr[1] = null;
            _state.OperaArr[2] = null;
            _state.RefillPieces(_board);
            _state.Score = 0;
            _state.Combo = 0;
            _displayedScore = 0;
            _gameOverTriggered = false;
            _newBestTriggered = false;

            BuildStaticUI();
            InitGhostPool();
            RenderBoard();
            RenderSlots();
        }

        private static int[][] MakeEmptyBoard()
        {
            var b = new int[N][];
            for (int r = 0; r < N; r++) { b[r] = new int[N]; for (int c = 0; c < N; c++) b[r][c] = -1; }
            return b;
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildStaticUI()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

            // 背景
            UGuiFactory.CreateImage(_content, "Bg", BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, BlockLayout.BgColor);

            // 棋盘外框 + 格子背景
            float boardCx = BlockLayout.BoardOriginX + BlockLayout.BoardPixels / 2f;
            float boardCy = BlockLayout.BoardOriginY + BlockLayout.BoardPixels / 2f;
            UGuiFactory.CreateImage(_content, "BoardOuter", boardCx, boardCy,
                BlockLayout.BoardPixels + 16, BlockLayout.BoardPixels + 16, BlockLayout.BoardOuterColor);
            for (int r = 0; r < N; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    var center = BlockLayout.CellCenterDesign(c, r);
                    UGuiFactory.CreateImage(_content, $"cellbg_{r}_{c}", center.x, center.y,
                        BlockLayout.CellSize - 6, BlockLayout.CellSize - 6, BlockLayout.BoardCellBgColor);
                }
            }

            _boardLayer = UGuiFactory.CreateNode(_content, "BoardLayer");
            UGuiFactory.PlaceByDesignCenter(_boardLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _ghostLayer = UGuiFactory.CreateNode(_content, "GhostLayer");
            UGuiFactory.PlaceByDesignCenter(_ghostLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _slotLayer = UGuiFactory.CreateNode(_content, "SlotLayer");
            UGuiFactory.PlaceByDesignCenter(_slotLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);

            // 顶部：BEST + 分数
            UGuiFactory.CreateText(_content, "BestLabel", 90, 120, 160, 30, "BEST", 20,
                new Color32(0x77, 0x88, 0xcc, 0xFF), TextAnchor.MiddleLeft);
            _bestText = UGuiFactory.CreateText(_content, "Best", 120, 155, 220, 40, _initialHigh.ToString(), 32,
                new Color32(0xbb, 0xcc, 0xff, 0xFF), TextAnchor.MiddleLeft);
            _scoreText = UGuiFactory.CreateText(_content, "Score", BlockLayout.DesignWidth / 2f, 200, 400, 90, "0", 84,
                Color.white, TextAnchor.MiddleCenter);

            // 退出按钮
            var exit = UGuiFactory.CreateButton(_content, "Exit", BlockLayout.DesignWidth - 60, 110, 70, 70, "×", 48,
                new Color(0, 0, 0, 0), Color.white, out _, out _);
            exit.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<GameWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }

        private void InitGhostPool()
        {
            for (int i = 0; i < _ghostPool.Length; i++)
            {
                var img = UGuiFactory.CreateImage(_ghostLayer, $"ghost_{i}", 0, 0,
                    BlockLayout.CellSize - 8, BlockLayout.CellSize - 8, BlockLayout.GhostOkColor);
                img.raycastTarget = false;
                img.gameObject.SetActive(false);
                _ghostPool[i] = img;
            }
        }

        // ── 渲染棋盘 ──
        private void RenderBoard()
        {
            for (int r = 0; r < N; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    int colorIdx = _state.SaveArr[r][c];
                    var existing = _cellImages[r, c];
                    if (colorIdx == -1)
                    {
                        if (existing != null) { Object.Destroy(existing.gameObject); _cellImages[r, c] = null; }
                    }
                    else
                    {
                        var color = BlockLayout.ColorOf((BlockColor)colorIdx);
                        if (existing != null) existing.color = color;
                        else
                        {
                            var center = BlockLayout.CellCenterDesign(c, r);
                            var img = UGuiFactory.CreateImage(_boardLayer, $"cell_{r}_{c}", center.x, center.y,
                                BlockLayout.CellSize - 6, BlockLayout.CellSize - 6, color);
                            img.raycastTarget = false;
                            _cellImages[r, c] = img;
                        }
                    }
                }
            }
        }

        // ── 渲染候选槽 ──
        private void RenderSlots()
        {
            for (int i = 0; i < 3; i++)
            {
                if (_slotContainers[i] != null) { Object.Destroy(_slotContainers[i].gameObject); _slotContainers[i] = null; }
            }

            for (int i = 0; i < 3; i++)
            {
                var piece = _state.OperaArr[i];
                if (piece == null) continue;
                var shape = BlockShapeMap.Get(piece.ShapeId);
                if (shape == null) continue;

                float slotDx = BlockLayout.SlotCenterX + (i - 1) * BlockLayout.SlotSpacing;
                var container = UGuiFactory.CreateNode(_slotLayer, $"slot_{i}");
                float totalW = shape.Width * BlockLayout.SlotCell;
                float totalH = shape.Height * BlockLayout.SlotCell;
                UGuiFactory.PlaceByDesignCenter(container, slotDx, BlockLayout.SlotY, Mathf.Max(totalW, 110), Mathf.Max(totalH, 110));

                // 透明 hit 区（拖拽 raycast 目标）
                var hit = container.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                hit.raycastTarget = true;

                // 方块格（中心对齐容器）
                var color = BlockLayout.ColorOf(piece.Color);
                float offX = -totalW / 2f + BlockLayout.SlotCell / 2f;
                float offY = totalH / 2f - BlockLayout.SlotCell / 2f;
                for (int r = 0; r < shape.Height; r++)
                {
                    for (int c = 0; c < shape.Width; c++)
                    {
                        if (((shape.Shape[r] >> (shape.Width - c - 1)) & 1) == 0) continue;
                        var cell = new GameObject($"sc_{r}_{c}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                        var crt = cell.GetComponent<RectTransform>();
                        crt.SetParent(container, false);
                        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
                        crt.pivot = new Vector2(0.5f, 0.5f);
                        crt.sizeDelta = new Vector2(BlockLayout.SlotCell - 3, BlockLayout.SlotCell - 3);
                        crt.anchoredPosition = new Vector2(offX + c * BlockLayout.SlotCell, offY - r * BlockLayout.SlotCell);
                        var ci = cell.GetComponent<Image>();
                        ci.color = color;
                        ci.raycastTarget = false;
                    }
                }

                var dragger = container.gameObject.AddComponent<BlockPieceDragger>();
                dragger.SlotIndex = i;
                dragger.OnBegin = OnPieceBegin;
                dragger.OnDragMove = OnPieceDrag;
                dragger.OnEnd = OnPieceEnd;
                dragger.RecordOrigin();
                _slotContainers[i] = container;
            }
        }

        // ── 拖拽回调 ──
        private void OnPieceBegin(int slotIdx)
        {
            var piece = _state.OperaArr[slotIdx];
            _draggingShapeId = piece?.ShapeId ?? -1;
        }

        private void OnPieceDrag(int slotIdx, Vector2 containerAnchored)
        {
            UpdateGhost(containerAnchored);
        }

        private void OnPieceEnd(int slotIdx)
        {
            ClearGhost();
            int shapeId = _draggingShapeId;
            _draggingShapeId = -1;

            var piece = _state.OperaArr[slotIdx];
            var shape = shapeId > 0 ? BlockShapeMap.Get(shapeId) : null;
            var container = _slotContainers[slotIdx];

            if (piece != null && shape != null && container != null)
            {
                var (col, row) = ComputeGridPos(container.anchoredPosition, shape);
                bool inBounds = col >= 0 && row >= 0 && col + shape.Width <= N && row + shape.Height <= N;
                if (inBounds && _board.CanPutBlock(shapeId, new Vec2Int(col, row)))
                {
                    PlaceAndResolve(slotIdx, shape, col, row);
                    return;
                }
            }

            // 非法 → 回弹
            container?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
        }

        /// <summary>落子 + 消除 + 补充 + GameOver 全流程。</summary>
        private void PlaceAndResolve(int slotIdx, BlockShape shape, int col, int row)
        {
            _state.PlacePiece(slotIdx, _board, col, row);
            int placementScore = CountCells(shape);  // 每格 +1
            _state.AddScore(placementScore);

            if (_slotContainers[slotIdx] != null)
            {
                Object.Destroy(_slotContainers[slotIdx].gameObject);
                _slotContainers[slotIdx] = null;
            }
            RenderBoard();

            // 消除
            var clear = _board.CanClearRowCols(true);
            int lines = clear.Rows.Count + clear.Cols.Count;
            if (lines > 0)
            {
                int clearedCells = _state.ClearRowsAndCols(clear.Rows, clear.Cols);
                _state.Combo += 1;
                int clearScore = clearedCells * 10 + lines * lines * 30;
                _state.AddScore(clearScore);
                RenderBoard();

                // 反馈弹字：PERFECT（清空）> COMBO×N（连击≥2）
                if (_board.IsEmpty())
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, "PERFECT!", 64, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                else if (_state.Combo >= 2)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, $"COMBO x{_state.Combo}", 56, new Color32(0xff, 0x77, 0xbb, 0xFF));
            }
            else
            {
                _state.Combo = 0;
            }

            // 补充
            bool allEmpty = true;
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) { allEmpty = false; break; }
            if (allEmpty)
            {
                _state.RefillPieces(_board);
                RenderSlots();
            }

            UpdateBest();

            // GameOver 判定
            var remaining = new List<int>();
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) remaining.Add(_state.OperaArr[i].ShapeId);
            if (remaining.Count > 0 && !_board.CanPutAnyOf(remaining.ToArray()))
            {
                TriggerGameOver();
            }
        }

        private void TriggerGameOver()
        {
            if (_gameOverTriggered) return;
            _gameOverTriggered = true;
            _state.Save();
            int previousHigh = _initialHigh;
            GameModule.UI.CloseUI<GameWindow>();
            GameModule.UI.ShowUIAsync<GameOverWindow>(previousHigh);
        }

        // ── ghost 落点高亮 ──
        private void UpdateGhost(Vector2 containerAnchored)
        {
            ClearGhost();
            if (_draggingShapeId < 0) return;
            var shape = BlockShapeMap.Get(_draggingShapeId);
            if (shape == null) return;

            var (col, row) = ComputeGridPos(containerAnchored, shape);
            if (col + shape.Width <= 0 || row + shape.Height <= 0) return;
            if (col >= N || row >= N) return;

            bool canPlace = col >= 0 && row >= 0 && col + shape.Width <= N && row + shape.Height <= N
                && _board.CanPutBlock(_draggingShapeId, new Vec2Int(col, row));
            var color = canPlace ? BlockLayout.GhostOkColor : BlockLayout.GhostBadColor;

            for (int r = 0; r < shape.Height; r++)
            {
                for (int c = 0; c < shape.Width; c++)
                {
                    if (((shape.Shape[r] >> (shape.Width - c - 1)) & 1) == 0) continue;
                    int gc = col + c, gr = row + r;
                    if (gc < 0 || gc >= N || gr < 0 || gr >= N) continue;
                    if (_ghostUsed >= _ghostPool.Length) break;
                    var img = _ghostPool[_ghostUsed++];
                    var center = BlockLayout.CellCenterDesign(gc, gr);
                    img.rectTransform.anchoredPosition = BlockLayout.DesignToAnchored(center);
                    img.color = color;
                    img.gameObject.SetActive(true);
                }
            }
        }

        private void ClearGhost()
        {
            for (int i = 0; i < _ghostUsed; i++) _ghostPool[i].gameObject.SetActive(false);
            _ghostUsed = 0;
        }

        /// <summary>容器 anchoredPosition（中心相对）→ 棋盘 grid (col,row)。</summary>
        private (int col, int row) ComputeGridPos(Vector2 containerAnchored, BlockShape shape)
        {
            var designCenter = BlockLayout.AnchoredToDesign(containerAnchored);
            float totalW = shape.Width * BlockLayout.CellSize;
            float totalH = shape.Height * BlockLayout.CellSize;
            float tlX = designCenter.x - totalW / 2f;
            float tlY = designCenter.y - totalH / 2f;
            int col = Mathf.RoundToInt((tlX - BlockLayout.BoardOriginX) / BlockLayout.CellSize);
            int row = Mathf.RoundToInt((tlY - BlockLayout.BoardOriginY) / BlockLayout.CellSize);
            return (col, row);
        }

        private static int CountCells(BlockShape shape)
        {
            int n = 0;
            for (int i = 0; i < shape.Shape.Length; i++)
            {
                int x = shape.Shape[i];
                while (x != 0) { n += x & 1; x >>= 1; }
            }
            return n;
        }

        private void UpdateBest()
        {
            if (_state.Score > _initialHigh)
            {
                _bestText.text = _state.Score.ToString();
                _bestText.color = new Color32(0xff, 0xe0, 0x66, 0xFF);
                // 本局首次破纪录弹字（仅一次）
                if (!_newBestTriggered && _initialHigh > 0)
                {
                    _newBestTriggered = true;
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 300, "NEW BEST!", 56, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                }
            }
        }

        // ── 分数滚动 ──
        protected override void OnUpdate()
        {
            if (_displayedScore != _state.Score)
            {
                int diff = _state.Score - _displayedScore;
                int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(diff) / 8f));
                _displayedScore += diff > 0 ? step : -step;
                if ((diff > 0 && _displayedScore > _state.Score) || (diff < 0 && _displayedScore < _state.Score))
                    _displayedScore = _state.Score;
                _scoreText.text = _displayedScore.ToString();
            }
        }
    }
}
