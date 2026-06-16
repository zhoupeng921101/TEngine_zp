using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
using GameLogic.Config;
using GameLogic.UI;   // SettingsWindow（齿轮入口，设计 23）所在命名空间

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// Block Blast 主玩法窗口（Classic 模式）。纯几何 UGUI 渲染 + 拖拽落子。
    /// 移植自源项目 Game.ts 的核心交互循环（落子/消除/补充/GameOver）。
    /// </summary>
    [Window(UILayer.UI, location: "GameWindow", fullScreen: true)]
    public sealed class GameWindow : UIWindow
    {
        // ── 塔罗木质换皮（设计 27）。仅 BuildStaticUI 的静态视觉壳贴 Sheet_tarot_mode 子图，玩法逻辑不动。──
        // 子图取自 Sheet_tarot_mode.png（Multiple 精灵表，13 子图，打表工具产；SetSubSprite 寻址，引用计数自管）。
        // 子图名 → 区块映射（读 tarot_mode.png 效果图 + 产出表子图尺寸核实，覆盖设计稿 §4.3 按名推断）：
        //   背景 chessboard（975×975 木纹大图）；棋盘外框 chess（九宫格 border12）；
        //   头像占位 mask；资源条底 resourcebar2（九宫格）+ 图标 gemstone/gemstone2/potion；齿轮 icon_setting；
        //   动作按钮底 Rectangle（九宫格）+ 删除图标 hammer（更换/提示无精准图标 → 文本为主）。
        //   未用：blue/temple（备选，本轮未铺）。源切图 image/tarot_mode/advertisement 超大/不投放，未进打表目录。
        private const string Atlas = "Sheet_tarot_mode";

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

            // 背景：木纹底图（color 设白让木纹原色透出，同设计 26 经验）
            var bg = UGuiFactory.CreateImage(_content, "Bg", BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, Color.white);
            bg.SetSubSprite(Atlas, "chessboard");

            // 棋盘外框（木质九宫格框，贴图 color 设白）：位置/尺寸沿用 BlockLayout 既有值，绝不动（动了落子对位偏）
            float boardCx = BlockLayout.BoardOriginX + BlockLayout.BoardPixels / 2f;
            float boardCy = BlockLayout.BoardOriginY + BlockLayout.BoardPixels / 2f;
            var boardOuter = UGuiFactory.CreateImage(_content, "BoardOuter", boardCx, boardCy,
                BlockLayout.BoardPixels + 16, BlockLayout.BoardPixels + 16, Color.white);
            boardOuter.SetSubSprite(Atlas, "chess");

            // 格子背景：方案 A（设计 27 §5.2）——格底保纯色（外框+背景已出木质风，64 格逐贴开销/视觉杂）。
            // 微调为偏暖的半透深棕，叠在木纹背景上更贴效果图浅格观感。位置/尺寸沿用既有 BlockLayout 值，绝不动。
            var cellBg = new Color32(0x3a, 0x24, 0x14, 0x55);
            for (int r = 0; r < N; r++)
            {
                for (int c = 0; c < N; c++)
                {
                    var center = BlockLayout.CellCenterDesign(c, r);
                    UGuiFactory.CreateImage(_content, $"cellbg_{r}_{c}", center.x, center.y,
                        BlockLayout.CellSize - 6, BlockLayout.CellSize - 6, cellBg);
                }
            }

            _boardLayer = UGuiFactory.CreateNode(_content, "BoardLayer");
            UGuiFactory.PlaceByDesignCenter(_boardLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _ghostLayer = UGuiFactory.CreateNode(_content, "GhostLayer");
            UGuiFactory.PlaceByDesignCenter(_ghostLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);
            _slotLayer = UGuiFactory.CreateNode(_content, "SlotLayer");
            UGuiFactory.PlaceByDesignCenter(_slotLayer, BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f, 0, 0);

            BuildTopBar();

            // 大分数（沿用 Text，对位效果图大白字；位置下移到顶栏下方）。OnUpdate 滚动逻辑不动。
            _scoreText = UGuiFactory.CreateText(_content, "Score", BlockLayout.DesignWidth / 2f, 215, 500, 110, "0", 96,
                Color.white, TextAnchor.MiddleCenter);

            // 最高分（文本逻辑不动：读 _initialHigh，UpdateBest 滚动变色）。挪到分数下方小字，不占顶栏。
            _bestText = UGuiFactory.CreateText(_content, "Best", BlockLayout.DesignWidth / 2f, 280, 300, 36,
                "BEST " + _initialHigh, 26, new Color32(0xf0, 0xe0, 0xc0, 0xFF), TextAnchor.MiddleCenter);

            BuildActionButtons();
        }

        /// <summary>
        /// 静态顶栏（设计 27 §5.3，纯视觉壳 + 占位/接线）：头像占位 + 3 资源条占位 + 齿轮(真接设置窗) + 退出钮(回调不动)。
        /// 顶栏数据层无资源字段（D1）→ 资源条占位；头像无 PlayerInfo（D2）→ 占位图。不往数据层加任何状态。
        /// </summary>
        private void BuildTopBar()
        {
            // ① 头像（占位图，无数据源 D2）
            var avatar = UGuiFactory.CreateImage(_content, "Avatar", 64, 64, 84, 84, Color.white);
            avatar.SetSubSprite(Atlas, "mask");

            // ② 3 资源条（视觉占位 D1）：条底 + 图标 + 数字 + 加号。第 1 条接 HighScore（有真数据），其余占位。
            string[] resIcons = { "gemstone", "gemstone2", "potion" };
            for (int i = 0; i < 3; i++)
            {
                float cx = 230 + i * 150;
                var bar = UGuiFactory.CreateImage(_content, $"ResBar_{i}", cx, 64, 140, 52, Color.white);
                bar.SetSubSprite(Atlas, "resourcebar2");

                var ic = UGuiFactory.CreateImage(_content, $"ResIcon_{i}", cx - 48, 64, 40, 40, Color.white);
                ic.SetSubSprite(Atlas, resIcons[i]);

                // 数字：第 1 条接 HighScore（真数据，只读），其余静态占位。不写回数据层。
                string num = i == 0 ? _initialHigh.ToString() : "0";
                UGuiFactory.CreateText(_content, $"ResNum_{i}", cx + 6, 64, 80, 36, num, 26,
                    new Color32(0x5a, 0x2e, 0x10, 0xFF), TextAnchor.MiddleLeft);

                // 加号 → 占位（去变现，不接购买；点击仅 Log 待建）
                var plus = UGuiFactory.CreateButton(_content, $"ResPlus_{i}", cx + 56, 64, 30, 30,
                    "+", 26, new Color(0, 0, 0, 0), new Color32(0x3a, 0x8a, 0x3a, 0xFF), out _, out _);
                plus.onClick.AddListener(() =>
                    Log.Info("[GameWindow] 资源条加号：待建（无资源系统，设计 27 §十 D1，去变现不接购买）"));
            }

            // ③ 齿轮 → 真接设置窗（设计 23 已建）：叠层弹出，不关本窗、不丢局（R4）
            var gear = UGuiFactory.CreateButton(_content, "Gear", BlockLayout.DesignWidth - 64, 64, 72, 72,
                "", 0, Color.white, Color.white, out var gearBg, out _);
            gearBg.SetSubSprite(Atlas, "icon_setting");
            gear.onClick.AddListener(() => GameModule.UI.ShowUIAsync<SettingsWindow>());

            // ④ 退出钮（保留，回调一字不改 — R4：CloseUI<GameWindow> + ShowUIAsync<MainMenuWindow>）。挪到齿轮左侧。
            var exit = UGuiFactory.CreateButton(_content, "Exit", BlockLayout.DesignWidth - 150, 64, 64, 64, "×", 44,
                new Color(0, 0, 0, 0), new Color32(0x6a, 0x40, 0x20, 0xFF), out _, out _);
            exit.onClick.AddListener(() =>
            {
                GameModule.UI.CloseUI<GameWindow>();
                GameModule.UI.ShowUIAsync<MainMenuWindow>();
            });
        }

        /// <summary>
        /// 底部 3 动作按钮（设计 27 §六，视觉占位 + stub）：更换 / 删除 / 提示。
        /// 这是 3 个新玩法机制，Classic + 数据层均无 → 仅摆视觉 + 点击 Log 待建，绝不实现机制（违纯 UI 补完+零回归）。
        /// </summary>
        private void BuildActionButtons()
        {
            // Y=1288：候选槽 hit 区（SlotY 1100 + SlotZoneHeight 250 → 底 1225）下方留 ~18px 间隙，不挡拖拽落子区。
            string[] actions = { "更换", "删除", "提示" };
            string[] actionIcons = { null, "hammer", null };   // 删除有锤子图标；更换/提示无精准图标 → 纯文本
            for (int i = 0; i < 3; i++)
            {
                float cx = 145 + i * 230;
                var btn = UGuiFactory.CreateButton(_content, $"Action_{i}", cx, 1288, 200, 86,
                    actions[i], 32, Color.white, new Color32(0x5a, 0x2e, 0x10, 0xFF), out var btnBg, out _);
                btnBg.SetSubSprite(Atlas, "Rectangle");

                if (actionIcons[i] != null)
                {
                    var ic = UGuiFactory.CreateImage(_content, $"ActionIcon_{i}", cx, 1268, 42, 42, Color.white);
                    ic.SetSubSprite(Atlas, actionIcons[i]);
                }

                int captured = i;
                btn.onClick.AddListener(() =>
                    Log.Info($"[GameWindow] 动作按钮「{actions[captured]}」：待建（新玩法机制，设计 27 §六 D5）"));
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
                // 容器尺寸 = 整个槽区域（点区域任意处即选中），方块格仍居中
                UGuiFactory.PlaceByDesignCenter(container, slotDx, BlockLayout.SlotY,
                    BlockLayout.SlotZoneWidth, BlockLayout.SlotZoneHeight);

                // 透明 hit 区（覆盖整个槽区域，拖拽 raycast 目标）
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
            int placementScore = BlockScoring.PlacementScore(CountCells(shape));  // 每格 +1
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
                int clearScore = BlockScoring.ClearScore(clearedCells, lines);
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
            // 经典最高分并入元层落盘（设计 29 §5.4）：与元层进度同一节点，跨会话长期指标随元层存储。
            GameLogic.GameContext.Instance.SaveHighScore();
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

            // 不可放置的红色 ghost 受开关控制，默认关闭 → 落点非法时不显示任何预览
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
