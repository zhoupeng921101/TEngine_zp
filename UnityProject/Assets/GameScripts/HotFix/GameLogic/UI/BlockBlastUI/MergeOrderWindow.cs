using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 合成+订单+体力 Demo 窗口（独立切片）。棋盘/拖拽/ghost/落子流程与 <see cref="GameWindow"/> 同构，
    /// 叠加体力条 / 双订单卡（手动交付）/ 合成区面板 / 悔棋按钮。
    /// 全程 MergeOrderMode=on（OnCreate 重置时开启，OnDestroy/离开时关闭），不污染 Classic。
    /// </summary>
    [Window(UILayer.UI, location: "MergeOrderWindow", fullScreen: true)]
    public sealed class MergeOrderWindow : UIWindow
    {
        private const int N = BlockLayout.BoardSize;

        private BlockGameState _state;
        private MergeOrderState _merge;
        private BinaryBoard _board;

        private RectTransform _content;
        private RectTransform _boardLayer;
        private RectTransform _elemLayer;
        private RectTransform _slotLayer;
        private RectTransform _ghostLayer;
        private RectTransform _orderLayer;
        private RectTransform _synthLayer;

        private readonly Image[,] _cellImages = new Image[N, N];
        private readonly Text[,] _elemCells = new Text[N, N];
        private readonly RectTransform[] _slotContainers = new RectTransform[3];
        private readonly Image[] _ghostPool = new Image[N * N];
        private int _ghostUsed;

        private Text _energyText;
        private Text _goalText;
        private Button _undoBtn;
        private Image _undoBtnBg;
        private Text _undoBtnLabel;

        private int _draggingShapeId = -1;
        private bool _finished;   // 通关或 GameOver 后锁输入

        protected override void OnCreate()
        {
            _state = BlockGameState.Instance;
            _board = new BinaryBoard();

            if (!DynamicWeightDiff.Instance.IsInitialized())
            {
                try { GameLogic.Config.WeightCfgConfigMgr.InitDynamicWeight(); }
                catch (System.Exception e) { Log.Warning($"[MergeOrderWindow] 权重表加载失败，退化随机：{e.Message}"); }
            }
            DynamicWeightDiff.Instance.BeginGame();

            // 进入即重置：空棋盘 + 新 MergeState（起始体力/空合成区/初始订单）+ 补满 3 块（MergeOrderMode 在此开启）
            _state.ResetForMergeOrder(_board);
            _merge = _state.MergeState;
            _finished = false;

            BuildStaticUI();
            InitGhostPool();
            RenderBoard();
            RenderSlots();
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo();
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
            _orderLayer = UGuiFactory.CreateNode(_content, "OrderLayer");
            UGuiFactory.PlaceByDesignCenter(_orderLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _synthLayer = UGuiFactory.CreateNode(_content, "SynthLayer");
            UGuiFactory.PlaceByDesignCenter(_synthLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);

            // 标题
            UGuiFactory.CreateText(_content, "Title", BlockLayout.DesignWidth / 2f, 55, 600, 50, "合成订单 DEMO", 36,
                new Color32(0xff, 0xe0, 0x66, 0xFF));

            // 体力条 + 完成单数（顶部信息行）
            UGuiFactory.CreateImage(_content, "EnergyBg", 220, 120, 280, 56, new Color(0, 0, 0, 0.25f));
            _energyText = UGuiFactory.CreateText(_content, "Energy", 220, 120, 280, 56, "", 36,
                new Color32(0x66, 0xff, 0xaa, 0xFF));
            UGuiFactory.CreateImage(_content, "GoalBg", 520, 120, 240, 56, new Color(0, 0, 0, 0.25f));
            _goalText = UGuiFactory.CreateText(_content, "Goal", 520, 120, 240, 56, "", 34,
                new Color32(0xff, 0xdd, 0x88, 0xFF));

            // 悔棋按钮（左上）
            _undoBtn = UGuiFactory.CreateButton(_content, "Undo", 90, 55, 130, 60, "悔棋", 30,
                new Color32(0x55, 0x55, 0x88, 0xFF), Color.white, out _undoBtnBg, out _undoBtnLabel);
            _undoBtn.onClick.AddListener(OnUndoClicked);

            // 退出按钮（右上）
            var exit = UGuiFactory.CreateButton(_content, "Exit", BlockLayout.DesignWidth - 55, 55, 70, 60, "×", 44,
                new Color(0, 0, 0, 0), Color.white, out _, out _);
            exit.onClick.AddListener(() =>
            {
                _state.ExitMergeOrder();
                GameModule.UI.CloseUI<MergeOrderWindow>();
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

        // ── 体力条 / 完成单数 ──
        private void RefreshEnergy()
        {
            _energyText.text = $"⚡ {_merge.Energy}/{MergeOrderConfig.EnergyCap}";
            _energyText.color = _merge.CanAffordPlace
                ? new Color32(0x66, 0xff, 0xaa, 0xFF)
                : new Color32(0xff, 0x66, 0x66, 0xFF);
            _goalText.text = $"单 {_merge.CompletedOrders}/{MergeOrderConfig.DemoGoalOrders}";
        }

        // ── 双订单卡（每次刷新重建，含交付按钮点亮/置灰） ──
        private void RefreshOrders()
        {
            for (int i = _orderLayer.childCount - 1; i >= 0; i--)
                Object.Destroy(_orderLayer.GetChild(i).gameObject);

            var orders = _merge.ActiveOrders;
            if (orders == null) return;

            const float cardW = 350f;
            const float cardH = 120f;
            const float cardY = 215f;
            float[] centers = { BlockLayout.DesignWidth / 2f - 185f, BlockLayout.DesignWidth / 2f + 185f };

            for (int slot = 0; slot < orders.Length && slot < centers.Length; slot++)
            {
                var o = orders[slot];
                float cx = centers[slot];

                UGuiFactory.CreateImage(_orderLayer, $"orderCard_{slot}", cx, cardY, cardW, cardH,
                    new Color32(0x22, 0x2c, 0x3e, 0xFF));

                // 元素 glyph
                UGuiFactory.CreateText(_orderLayer, $"orderGlyph_{slot}", cx - 120, cardY - 12, 80, 80,
                    MergeElementVisual.Glyph(o.Type), 54, MergeElementVisual.ColorOf(o.Type));
                // 等级 + 数量
                UGuiFactory.CreateText(_orderLayer, $"orderReq_{slot}", cx - 30, cardY - 12, 160, 60,
                    $"Lv{o.Level} ×{o.Count}", 32, Color.white, TextAnchor.MiddleLeft);

                // 交付按钮
                bool can = _merge.CanDeliver(slot);
                int captured = slot;
                var deliver = UGuiFactory.CreateButton(_orderLayer, $"orderDeliver_{slot}", cx, cardY + 38, cardW - 30, 44,
                    "交付", 28,
                    can ? new Color32(0x33, 0xaa, 0x55, 0xFF) : new Color32(0x44, 0x44, 0x4c, 0xFF),
                    can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF), out _, out _);
                deliver.interactable = can && !_finished;
                deliver.onClick.AddListener(() => OnDeliverClicked(captured));
            }
        }

        // ── 合成区面板（底部 token 行，每次刷新重建） ──
        private void RefreshSynthesis()
        {
            for (int i = _synthLayer.childCount - 1; i >= 0; i--)
                Object.Destroy(_synthLayer.GetChild(i).gameObject);

            const float rowY = 1285f;
            UGuiFactory.CreateImage(_synthLayer, "synthBg", BlockLayout.DesignWidth / 2f, rowY, 720, 84,
                new Color(0, 0, 0, 0.22f));

            // 稳定排序：按类型枚举值、再按等级
            var keys = new List<(MergeElement type, int level)>(_merge.Inventory.Keys);
            keys.Sort((a, b) =>
            {
                int t = ((int)a.type).CompareTo((int)b.type);
                return t != 0 ? t : a.level.CompareTo(b.level);
            });

            if (keys.Count == 0)
            {
                UGuiFactory.CreateText(_synthLayer, "synthEmpty", BlockLayout.DesignWidth / 2f, rowY, 720, 60,
                    "合成区：空（消除元素入区，自动两两升级）", 24, new Color32(0x88, 0x99, 0xaa, 0xFF));
                return;
            }

            const float tokenW = 130f;
            int n = keys.Count;
            float totalW = n * tokenW;
            float startX = BlockLayout.DesignWidth / 2f - totalW / 2f + tokenW / 2f;
            for (int i = 0; i < n; i++)
            {
                var key = keys[i];
                int count = _merge.Inventory[key];
                float cx = startX + i * tokenW;
                UGuiFactory.CreateText(_synthLayer, $"synthGlyph_{i}", cx - 22, rowY, 60, 70,
                    MergeElementVisual.Glyph(key.type), 40, MergeElementVisual.ColorOf(key.type));
                UGuiFactory.CreateText(_synthLayer, $"synthInfo_{i}", cx + 30, rowY, 90, 70,
                    $"L{key.level}\n×{count}", 24, Color.white);
            }
        }

        // ── 悔棋按钮态 ──
        private void RefreshUndo()
        {
            bool can = _merge.CanUndo && !_finished;
            _undoBtn.interactable = can;
            _undoBtnLabel.text = $"悔棋 {_merge.UndoCharges}";
            _undoBtnBg.color = can ? new Color32(0x55, 0x55, 0x88, 0xFF) : new Color32(0x3a, 0x3a, 0x44, 0xFF);
        }

        private void OnUndoClicked()
        {
            if (_finished) return;
            if (!_merge.Undo(_state, _board)) return;
            ClearGhost();
            RenderBoard();
            RenderSlots();
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo();
        }

        private void OnDeliverClicked(int slot)
        {
            if (_finished) return;
            if (!_merge.Deliver(slot)) return;
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo(); // 交付清空悔棋栈，按钮须刷新

            if (_merge.IsDemoComplete()) { TriggerWin(); return; }
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

        // ── 渲染元素 overlay（glyph） ──
        private void RenderElements()
        {
            var arr = _state.ElementArr;
            for (int r = 0; r < N; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    var el = arr != null ? arr[r][c] : MergeElement.None;
                    var existing = _elemCells[r, c];
                    if (el == MergeElement.None)
                    {
                        if (existing != null) { Object.Destroy(existing.gameObject); _elemCells[r, c] = null; }
                    }
                    else
                    {
                        if (existing != null)
                        {
                            existing.text = MergeElementVisual.Glyph(el);
                            existing.color = MergeElementVisual.ColorOf(el);
                        }
                        else
                        {
                            var center = BlockLayout.CellCenterDesign(c, r);
                            var txt = UGuiFactory.CreateText(_elemLayer, $"elem_{r}_{c}", center.x, center.y,
                                BlockLayout.CellSize, BlockLayout.CellSize, MergeElementVisual.Glyph(el),
                                (int)(BlockLayout.CellSize * 0.66f), MergeElementVisual.ColorOf(el));
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

                        if (piece.Elements != null && cellIdx < piece.Elements.Length
                            && piece.Elements[cellIdx] != MergeElement.None)
                        {
                            var el = piece.Elements[cellIdx];
                            var gt = new GameObject($"sg_{r}_{c}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                            var grt = gt.GetComponent<RectTransform>();
                            grt.SetParent(crt, false);
                            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                            var gtx = gt.GetComponent<Text>();
                            gtx.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                            gtx.text = MergeElementVisual.Glyph(el);
                            gtx.fontSize = (int)(BlockLayout.SlotCell * 0.7f);
                            gtx.color = MergeElementVisual.ColorOf(el);
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
                // 体力不足不可落子（#2：体力为 0 时不可再落子）
                if (inBounds && _merge.CanAffordPlace && _board.CanPutBlock(shapeId, new Vec2Int(col, row)))
                {
                    PlaceAndResolve(slotIdx, shape, col, row);
                    return;
                }
            }

            container?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
        }

        /// <summary>落子 → 扣体力 → 消除返体力 + 元素入合成区（自动升级）→ 刷新 → 交付/通关/软死亡判定。</summary>
        private void PlaceAndResolve(int slotIdx, BlockShape shape, int col, int row)
        {
            // 落子前打全量快照（供悔棋整体回滚）
            _merge.CaptureSnapshot(_state, _board);

            _state.PlacePiece(slotIdx, _board, col, row);  // 含元素转移（门控）
            _merge.SpendPlaceCost();

            if (_slotContainers[slotIdx] != null)
            {
                Object.Destroy(_slotContainers[slotIdx].gameObject);
                _slotContainers[slotIdx] = null;
            }
            RenderBoard();

            // 消除 + 返体力 + 元素入合成区
            var clear = _board.CanClearRowCols(true);
            int lines = clear.Rows.Count + clear.Cols.Count;
            if (lines > 0)
            {
                var cleared = new List<MergeElement>();
                _state.HarvestClearedElements(clear.Rows, clear.Cols, cleared); // 清 overlay + 输出被清元素
                int clearedCells = _state.ClearRowsAndCols(clear.Rows, clear.Cols); // 清方块色，得被清格数
                foreach (var el in cleared) _merge.IngestElement(el);           // 逐个 Lv1 入合成区（自动升级）
                _merge.RefundEnergy(lines);                                     // 返还体力（受软上限）

                // 连消/多消/全清结算（设计 11 §5.5 固定流水线）：
                // 连消倍率只乘显示分；元素产出用未乘连消的基础分；多消里程碑直发 Lv2/Lv3；
                // 全清武装位发 1 Lv3 + 推进女神（不可连续 2 次）。里程碑/全清产物归当前订单所需类型之一。
                var milestoneType = PickMilestoneType();
                var settle = ClearSettlement.Settle(_merge, lines, clearedCells, _board.IsEmpty(), milestoneType);
                _state.Combo = settle.ComboChain >= 2 ? settle.ComboChain : 0; // 镜像到视觉连击（≥2 才显示）

                RenderBoard();

                if (settle.AllClearRewarded)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, "PERFECT!", 64, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                else if (lines >= MergeOrderConfig.MultiClearMilestoneMinLines)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, settle.MultiLabel, 56, new Color32(0x55, 0xdd, 0xaa, 0xFF));
                else if (settle.ComboChain >= 2)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, $"COMBO x{settle.ComboChain}", 56, new Color32(0xff, 0x77, 0xbb, 0xFF));
            }
            else
            {
                ClearSettlement.Settle(_merge, 0, 0, false, MergeElement.None); // 链断回 1 + 重新武装全清
                _state.Combo = 0;
            }

            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo();

            // 通关判定（完成单数达标）
            if (_merge.IsDemoComplete()) { TriggerWin(); return; }

            // 补充
            bool allEmpty = true;
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) { allEmpty = false; break; }
            if (allEmpty)
            {
                _state.RefillPieces(_board);
                RenderSlots();
            }

            // 手持块清单（两个失败条件共用）
            var remaining = new List<int>();
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) remaining.Add(_state.OperaArr[i].ShapeId);

            // 软死亡（#5）：体力付不起落子且仍有手持块，且当前无单可交付（交付能回体力则不算死）
            if (remaining.Count > 0 && !_merge.CanAffordPlace && !AnyDeliverable())
            {
                TriggerGameOver("精力耗尽");
                return;
            }

            // 硬死亡（#15）：手持块无处可放
            if (remaining.Count > 0 && !_board.CanPutAnyOf(remaining.ToArray()))
            {
                TriggerGameOver("GAME OVER");
            }
        }

        private bool AnyDeliverable()
        {
            var orders = _merge.ActiveOrders;
            if (orders == null) return false;
            for (int i = 0; i < orders.Length; i++) if (_merge.CanDeliver(i)) return true;
            return false;
        }

        /// <summary>
        /// 多消里程碑/全清直发图案的归属类型：取当前订单所需类型之一（需求拉动，避免产无关图案）。
        /// 无所需类型时回退 None（ClearSettlement 内再兜底 Diamond）。
        /// </summary>
        private MergeElement PickMilestoneType()
        {
            var needed = _merge.NeededTypes();
            return needed.Count > 0 ? needed[0] : MergeElement.None;
        }

        private void TriggerWin()
        {
            if (_finished) return;
            _finished = true;
            ClearGhost();
            var lines = new List<string>
            {
                $"完成订单  {_merge.CompletedOrders} 单",
                $"累计得分  {_merge.TotalScore}",
            };
            _state.ExitMergeOrder();
            GameModule.UI.CloseUI<MergeOrderWindow>();
            GameModule.UI.ShowUIAsync<MergeOrderWinWindow>(lines);
        }

        private void TriggerGameOver(string title)
        {
            if (_finished) return;
            _finished = true;
            ClearGhost();
            // 把 demo 累计得分映射给结算窗显示；不写 HighScore（不污染 Classic 最高分）
            _state.Score = _merge.TotalScore;
            _state.ExitMergeOrder();
            GameModule.UI.CloseUI<MergeOrderWindow>();
            GameModule.UI.ShowUIAsync<GameOverWindow>(0);
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
            // 安全兜底：离开必定关闭门控，确保后续 Classic / 08 行为零残留。
            if (_state != null) _state.ExitMergeOrder();
        }
    }
}
