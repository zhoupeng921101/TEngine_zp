using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 收集 Demo 窗口（独立切片，方案 A）。镜像 GameWindow 的几何 UGUI 渲染 + 拖拽落子，
    /// 叠加「元素 overlay + 顶部计数条 + collect 达标判定」。
    /// 全程 CollectMode=on（OnCreate 重置时开启，OnDestroy 关闭），不污染 Classic。
    /// </summary>
    [Window(UILayer.UI, location: "CollectDemoWindow", fullScreen: true)]
    public sealed class CollectDemoWindow : UIWindow
    {
        private const int N = BlockLayout.BoardSize;

        private BlockGameState _state;
        private BinaryBoard _board;

        private RectTransform _content;
        private RectTransform _boardLayer;
        private RectTransform _elemLayer;
        private RectTransform _slotLayer;
        private RectTransform _ghostLayer;

        private readonly Image[,] _cellImages = new Image[N, N];
        private readonly Text[,] _elemCells = new Text[N, N];
        private readonly RectTransform[] _slotContainers = new RectTransform[3];
        private readonly Image[] _ghostPool = new Image[N * N];
        private int _ghostUsed;

        // 顶部计数条
        private readonly List<CollectElement> _activeTypes = new List<CollectElement>();
        private readonly Dictionary<CollectElement, Text> _counterTexts = new Dictionary<CollectElement, Text>();

        private int _draggingShapeId = -1;
        private bool _finished;   // 胜利或 GameOver 后锁输入

        protected override void OnCreate()
        {
            _state = BlockGameState.Instance;
            _board = new BinaryBoard();

            // 确保动态权重已初始化（与 GameWindow 一致；失败则退化随机）
            if (!DynamicWeightDiff.Instance.IsInitialized())
            {
                try { GameLogic.Config.WeightCfgConfigMgr.InitDynamicWeight(); }
                catch (System.Exception e) { Log.Warning($"[CollectDemoWindow] 权重表加载失败，退化随机：{e.Message}"); }
            }
            DynamicWeightDiff.Instance.BeginGame();

            // 进入即重置：空棋盘 + 清 Collected + 重设 Target + 补满 3 块（CollectMode 在此开启）
            _state.ResetForCollectDemo(_board);
            _finished = false;

            // 活跃类型按 DemoTargets 顺序（稳定 UI 排列）
            _activeTypes.Clear();
            foreach (var t in CollectDemo.DemoTargets)
            {
                if (_state.CollectionTargets.ContainsKey(t.Element) && !_activeTypes.Contains(t.Element))
                    _activeTypes.Add(t.Element);
            }

            BuildStaticUI();
            InitGhostPool();
            RenderBoard();
            RenderSlots();
            RefreshCounters();
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildStaticUI()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

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
            _elemLayer = UGuiFactory.CreateNode(_content, "ElemLayer");
            UGuiFactory.PlaceByDesignCenter(_elemLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _ghostLayer = UGuiFactory.CreateNode(_content, "GhostLayer");
            UGuiFactory.PlaceByDesignCenter(_ghostLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _slotLayer = UGuiFactory.CreateNode(_content, "SlotLayer");
            UGuiFactory.PlaceByDesignCenter(_slotLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);

            // 标题
            UGuiFactory.CreateText(_content, "Title", BlockLayout.DesignWidth / 2f, 90, 600, 50, "收集 DEMO", 40,
                new Color32(0xff, 0xe0, 0x66, 0xFF));

            // 顶部计数条：每活跃类型一格（glyph + got/target）
            BuildCounterBar();

            // 退出按钮
            var exit = UGuiFactory.CreateButton(_content, "Exit", BlockLayout.DesignWidth - 60, 90, 70, 70, "×", 48,
                new Color(0, 0, 0, 0), Color.white, out _, out _);
            exit.onClick.AddListener(() =>
            {
                _state.ExitCollectMode();
                GameModule.UI.CloseUI<CollectDemoWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }

        private void BuildCounterBar()
        {
            _counterTexts.Clear();
            int n = _activeTypes.Count;
            if (n == 0) return;
            const float barY = 200f;
            const float slotW = 240f;
            float totalW = n * slotW;
            float startX = BlockLayout.DesignWidth / 2f - totalW / 2f + slotW / 2f;
            for (int i = 0; i < n; i++)
            {
                var type = _activeTypes[i];
                float cx = startX + i * slotW;
                // 元素 glyph 底色块
                UGuiFactory.CreateImage(_content, $"counterIcon_{i}", cx - 60, barY, 60, 60,
                    new Color(0, 0, 0, 0.25f));
                UGuiFactory.CreateText(_content, $"counterGlyph_{i}", cx - 60, barY, 60, 60,
                    CollectDemo.Glyph(type), 44, CollectDemo.ColorOf(type));
                int target = _state.CollectionTargets[type];
                var txt = UGuiFactory.CreateText(_content, $"counter_{i}", cx + 30, barY, 150, 60,
                    $"0/{target}", 40, Color.white, TextAnchor.MiddleLeft);
                _counterTexts[type] = txt;
            }
        }

        private void RefreshCounters()
        {
            foreach (var type in _activeTypes)
            {
                if (!_counterTexts.TryGetValue(type, out var txt)) continue;
                int got = _state.Collected.TryGetValue(type, out var g) ? g : 0;
                int target = _state.CollectionTargets[type];
                txt.text = $"{Mathf.Min(got, target)}/{target}";
                txt.color = got >= target ? new Color32(0x66, 0xee, 0x77, 0xFF) : Color.white;
            }
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

        // ── 渲染棋盘（方块色） ──
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
            RenderElements();
        }

        // ── 渲染元素 overlay（glyph，与方块格一一对应） ──
        private void RenderElements()
        {
            var arr = _state.ElementArr;
            for (int r = 0; r < N; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    var el = arr != null ? arr[r][c] : CollectElement.None;
                    var existing = _elemCells[r, c];
                    if (el == CollectElement.None)
                    {
                        if (existing != null) { Object.Destroy(existing.gameObject); _elemCells[r, c] = null; }
                    }
                    else
                    {
                        if (existing != null)
                        {
                            existing.text = CollectDemo.Glyph(el);
                            existing.color = CollectDemo.ColorOf(el);
                        }
                        else
                        {
                            var center = BlockLayout.CellCenterDesign(c, r);
                            var txt = UGuiFactory.CreateText(_elemLayer, $"elem_{r}_{c}", center.x, center.y,
                                BlockLayout.CellSize, BlockLayout.CellSize, CollectDemo.Glyph(el),
                                (int)(BlockLayout.CellSize * 0.66f), CollectDemo.ColorOf(el));
                            _elemCells[r, c] = txt;
                        }
                    }
                }
            }
        }

        // ── 渲染候选槽（与 GameWindow 同构） ──
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
                UGuiFactory.PlaceByDesignCenter(container, slotDx, BlockLayout.SlotY,
                    BlockLayout.SlotZoneWidth, BlockLayout.SlotZoneHeight);

                var hit = container.gameObject.AddComponent<Image>();
                hit.color = new Color(1, 1, 1, 0);
                hit.raycastTarget = true;

                var color = BlockLayout.ColorOf(piece.Color);
                float offX = -totalW / 2f + BlockLayout.SlotCell / 2f;
                float offY = totalH / 2f - BlockLayout.SlotCell / 2f;
                int cellIdx = 0;
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

                        // 候选块上预览携带的元素 glyph（落子前即可见）
                        if (piece.Elements != null && cellIdx < piece.Elements.Length
                            && piece.Elements[cellIdx] != CollectElement.None)
                        {
                            var el = piece.Elements[cellIdx];
                            var gt = new GameObject($"sg_{r}_{c}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                            var grt = gt.GetComponent<RectTransform>();
                            grt.SetParent(crt, false);
                            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                            var gtx = gt.GetComponent<Text>();
                            gtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                            gtx.text = CollectDemo.Glyph(el);
                            gtx.fontSize = (int)(BlockLayout.SlotCell * 0.7f);
                            gtx.color = CollectDemo.ColorOf(el);
                            gtx.alignment = TextAnchor.MiddleCenter;
                            gtx.horizontalOverflow = HorizontalWrapMode.Overflow;
                            gtx.verticalOverflow = VerticalWrapMode.Overflow;
                            gtx.raycastTarget = false;
                        }
                        cellIdx++;
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

        // ── 拖拽回调（与 GameWindow 同构） ──
        private void OnPieceBegin(int slotIdx)
        {
            if (_finished) { _draggingShapeId = -1; return; }
            var piece = _state.OperaArr[slotIdx];
            _draggingShapeId = piece?.ShapeId ?? -1;
        }

        private void OnPieceDrag(int slotIdx, Vector2 containerAnchored)
        {
            if (_finished) return;
            UpdateGhost(containerAnchored);
        }

        private void OnPieceEnd(int slotIdx)
        {
            ClearGhost();
            int shapeId = _draggingShapeId;
            _draggingShapeId = -1;

            if (_finished)
            {
                _slotContainers[slotIdx]?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
                return;
            }

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

            container?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
        }

        /// <summary>落子 + 元素转移 + 消除收集 + 补充 + 达标/GameOver 判定。</summary>
        private void PlaceAndResolve(int slotIdx, BlockShape shape, int col, int row)
        {
            _state.PlacePiece(slotIdx, _board, col, row);  // 含元素转移（CollectMode 门控）

            if (_slotContainers[slotIdx] != null)
            {
                Object.Destroy(_slotContainers[slotIdx].gameObject);
                _slotContainers[slotIdx] = null;
            }
            RenderBoard();

            // 消除 + 收集
            var clear = _board.CanClearRowCols(true);
            int lines = clear.Rows.Count + clear.Cols.Count;
            if (lines > 0)
            {
                _state.CollectClearedElements(clear.Rows, clear.Cols); // 先收集元素（清 overlay）
                _state.ClearRowsAndCols(clear.Rows, clear.Cols);       // 再清方块色
                _state.Combo += 1;
                RenderBoard();
                RefreshCounters();

                if (_board.IsEmpty())
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, "PERFECT!", 64, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                else if (_state.Combo >= 2)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, $"COMBO x{_state.Combo}", 56, new Color32(0xff, 0x77, 0xbb, 0xFF));
            }
            else
            {
                _state.Combo = 0;
            }

            // 达标判定（凑齐瞬间胜利）
            if (_state.IsCollectionComplete())
            {
                TriggerWin();
                return;
            }

            // 补充
            bool allEmpty = true;
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) { allEmpty = false; break; }
            if (allEmpty)
            {
                _state.RefillPieces(_board);
                RenderSlots();
            }

            // GameOver 判定（无可落子且未达标 → 复用 GameOver）
            var remaining = new List<int>();
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) remaining.Add(_state.OperaArr[i].ShapeId);
            if (remaining.Count > 0 && !_board.CanPutAnyOf(remaining.ToArray()))
            {
                TriggerGameOver();
            }
        }

        private void TriggerWin()
        {
            if (_finished) return;
            _finished = true;
            ClearGhost();
            // 快照各目标达成行（CloseUI 会触发 OnDestroy→ExitCollectMode 清空字典，故先取）
            var lines = new List<string>();
            foreach (var type in _activeTypes)
            {
                int target = _state.CollectionTargets[type];
                lines.Add($"{CollectDemo.Glyph(type)}  {target}/{target}");
            }
            GameModule.UI.CloseUI<CollectDemoWindow>();
            GameModule.UI.ShowUIAsync<CollectWinWindow>(lines);
        }

        private void TriggerGameOver()
        {
            if (_finished) return;
            _finished = true;
            ClearGhost();
            // 复用现有 GameOver 流程（与 Classic 一致）。不调用 Save()，避免污染 Classic 存档。
            _state.ExitCollectMode();
            GameModule.UI.CloseUI<CollectDemoWindow>();
            GameModule.UI.ShowUIAsync<GameOverWindow>(_state.HighScore);
        }

        // ── ghost 落点高亮（与 GameWindow 同构） ──
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

            if (!canPlace && !GameConfigBB.ShowInvalidGhost) return;

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

        protected override void OnDestroy()
        {
            // 安全兜底：离开收集 Demo 必定关闭门控，确保后续 Classic 行为零残留。
            if (_state != null) _state.ExitCollectMode();
        }
    }
}
