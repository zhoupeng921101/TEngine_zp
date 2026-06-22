using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
    
namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 玩法融合主玩法窗口（设计 29 + 无尽模型 设计 49）：承载完整经济（体力 / 合成 / 订单 / 盲盒 / 女神 / 神庙）+ 塔罗木质换皮。
    /// 棋盘/拖拽/ghost/落子流程与 <see cref="GameWindow"/> 同构；叠加体力条 / 双订单卡（手动交付）/
    /// 合成区面板 / 悔棋按钮 / 消除道具按钮。全程 MergeOrderMode=on（OnCreate 重置时开启，OnDestroy/离开时关闭）。
    /// 融合后这是唯一主玩法入口（经典纯无尽 GameWindow 入口下线，代码保留不删，设计 29 §4.2）。
    ///
    /// 无尽模型（设计 49）：无「局」、订单无限、无任何 GameOver（卡死与体力归零都不结束、不弹面板，窗口保持可交互）。
    /// 两条兜底保证「真·无尽」：体力时基恢复（含离线，进盘后由 <see cref="BlockGameState.ResetForMergeOrder"/> 补算）+
    /// 消除道具（主动清一行一列、代价体力、无限可用只 gate 体力）。
    /// </summary>
    [Window(UILayer.UI, location: "MergeOrderWindow", fullScreen: true)]
    public sealed class MergeOrderWindow : UIWindow
    {
        // ── 塔罗木质换皮（设计 27 → 29 §5.2 移植到融合主体）。仅核心区静态视觉壳贴 Sheet_tarot_mode 子图：
        //    背景 chessboard（木纹大图）+ 棋盘外框 chess（九宫格框）。经济 HUD（体力/订单/合成区/盲盒/虔诚币/神庙）
        //    在塔罗精灵表无对应子图，维持现状纯色 + glyph（与 GameWindow 里资源条/动作按钮占位同理）。
        //    玩法逻辑（落子/消除/合成/订单/结算/存档/悔棋）一律不动——换皮只改静态视觉，不碰经济与坐标常量。
        private const string Atlas = "Sheet_tarot_mode";

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

        // 盲盒（设计 12 §五）：顶部计数 🔮 ×N + 开盒按钮（Count=0 置灰）。
        private Text _blindBoxText;
        private Button _openBoxBtn;
        private Image _openBoxBtnBg;
        private Text _openBoxBtnLabel;

        // 长期主线（设计 13 §五）：顶部虔诚币计数 ✦ ×N + 「神庙」按钮（开 TempleWindow 叠层）。
        private Text _pietyText;

        // 消除道具（设计 49 §3.1）：无尽脱困兜底。点按钮进「指定格」模式，再点棋盘任一格清该格所在一行一列。
        // 体力 ≥ ClearToolCost 可用、< 置灰；无限可用、只 gate 体力（无持有计数、不限次数）。
        private Button _clearToolBtn;
        private Image _clearToolBtnBg;
        private Text _clearToolBtnLabel;
        private Image _clearToolHintBg;
        private Text _clearToolHintText;
        /// <summary>铺满棋盘区域的透明 overlay，仅 arming 时启用，捕获棋盘格点击（指定格）。</summary>
        private Image _clearToolOverlay;
        /// <summary>消除道具「等待玩家指定棋盘格」模式（点过按钮、未点格前为 true）。</summary>
        private bool _clearToolArming;

        private int _draggingShapeId = -1;

        protected override void OnCreate()
        {
            _state = BlockGameState.Instance;
            _board = new BinaryBoard();

            if (!DynamicWeightDiff.Instance.IsInitialized())
            {
                try { GameLogic.Config.WeightCfgConfigMgr.InitDynamicWeight(); }
                catch (System.Exception e) { Log.Warning($"[MergeOrderWindow] 权重表加载失败，退化随机：{e.Message}"); }
            }
            // 隐患 B（设计 29 §5.3 / 开关 #3）：dynamicWeight 每局重置。
            // Reset() 清 _dynamicWeight/_preDynamicWeight/_refillIndex（含 BeginGame 的 refillIndex 归零），
            // 消除「跨局 / 跨模式 / 跨 app 重启」累积——每局从橡皮筋中位公平起步。
            // 此前两窗只调 BeginGame()（不清 _dynamicWeight）→ 上一局做局到的权重会带进下一局。
            DynamicWeightDiff.Instance.Reset();
            DynamicWeightDiff.Instance.BeginGame();

            // 进入即重置（隐患 A，设计 29 §5.3）：融合窗口进入路径显式清零局内瞬态分量——
            // ResetForMergeOrder 内将 BlockGameState.Score / Combo 清零（局内瞬态，不进盘），作为单窗口下的不变量。
            // 元层进度（灵力/虔诚币/女神/神庙/盲盒/HighScore 等）经存档加载覆盖，与局内瞬态分层不重叠。
            _state.ResetForMergeOrder(_board);
            _merge = _state.MergeState;
            _clearToolArming = false;

            BuildStaticUI();
            InitGhostPool();
            RenderBoard();
            RenderSlots();
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo();
            RefreshBlindBox();
            RefreshPiety();
            RefreshClearTool();

            // 跨会话存档兜底（设计 14 §3.4 ③）：移动端切后台/杀进程不经 OnDestroy,会丢末次元变更。
            // UIWindow 非 MonoBehaviour,Unity 的 OnApplicationPause/Quit 魔法方法不会在本类触发;
            // 经 TEngine 驱动器(UpdateDriver,真 MonoBehaviour)转播的应用暂停事件订阅,效果等价且确会触发。
            // 退出/销毁路径另由 OnDestroy 的 FlushSaveIfDirty 兜底,两路覆盖切后台与正常关窗。
            Utility.Unity.AddOnApplicationPauseListener(OnAppPause);
        }

        /// <summary>
        /// 应用暂停/恢复回调（设计 14 §3.4 ③ 的「OnApplicationPause(true) 落盘」等价实现）。
        /// pause=true 表示进入后台(移动端切后台/锁屏/被系统挂起),此时脏则强制落盘,保证不丢末次元变更。
        /// </summary>
        private void OnAppPause(bool pause)
        {
            if (pause) FlushSaveIfDirty();
        }

        // ─────────────────────────────────────────────────────────────
        private void BuildStaticUI()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

            // 背景：塔罗木纹底图（color 设白让木纹原色透出，设计 29 §5.2 换皮归属移植自 GameWindow）。
            // 位置/尺寸铺设计全屏，坐标常量不动。
            var bg = UGuiFactory.CreateImage(_content, "Bg", BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, Color.white);
            bg.SetSubSprite(Atlas, "chessboard");

            // 棋盘外框（木质九宫格框，贴图 color 设白）：位置/尺寸沿用 BlockLayout 既有值，绝不动（动了落子对位偏）。
            float boardCx = BlockLayout.BoardOriginX + BlockLayout.BoardPixels / 2f;
            float boardCy = BlockLayout.BoardOriginY + BlockLayout.BoardPixels / 2f;
            var boardOuter = UGuiFactory.CreateImage(_content, "BoardOuter", boardCx, boardCy,
                BlockLayout.BoardPixels + 16, BlockLayout.BoardPixels + 16, Color.white);
            boardOuter.SetSubSprite(Atlas, "chess");

            // 格子背景：方案 A（设计 27 §5.2）——格底偏暖半透深棕，叠在木纹背景上更贴效果图浅格观感。
            // 位置/尺寸沿用既有 BlockLayout 值，绝不动。
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

            // 盲盒计数 + 开盒按钮（第二信息行，y=170；设计 12 §五）
            UGuiFactory.CreateImage(_content, "BoxBg", 175, 170, 190, 52, new Color(0, 0, 0, 0.25f));
            _blindBoxText = UGuiFactory.CreateText(_content, "BlindBox", 175, 170, 190, 52, "", 32,
                new Color32(0xc8, 0x9a, 0xff, 0xFF)); // 紫
            _openBoxBtn = UGuiFactory.CreateButton(_content, "OpenBox", 350, 170, 150, 56, "开盒", 28,
                new Color32(0x7a, 0x4a, 0xb8, 0xFF), Color.white, out _openBoxBtnBg, out _openBoxBtnLabel);
            _openBoxBtn.onClick.AddListener(OnOpenBoxClicked);

            // 长期主线（设计 13 §五）：虔诚币计数 + 「神庙」按钮（第二信息行右侧，y=170）
            UGuiFactory.CreateImage(_content, "PietyBg", 520, 170, 150, 52, new Color(0, 0, 0, 0.25f));
            _pietyText = UGuiFactory.CreateText(_content, "Piety", 520, 170, 150, 52, "", 32,
                new Color32(0xff, 0xcf, 0x5c, 0xFF)); // 金
            var templeBtn = UGuiFactory.CreateButton(_content, "Temple", 660, 170, 140, 56, "神庙", 28,
                new Color32(0xb8, 0x8a, 0x3a, 0xFF), Color.white, out _, out _);
            templeBtn.onClick.AddListener(OnTempleClicked);

            // 悔棋按钮（左上）
            _undoBtn = UGuiFactory.CreateButton(_content, "Undo", 90, 55, 130, 60, "悔棋", 30,
                new Color32(0x55, 0x55, 0x88, 0xFF), Color.white, out _undoBtnBg, out _undoBtnLabel);
            _undoBtn.onClick.AddListener(OnUndoClicked);

            // 消除道具按钮（设计 49 §3.1）：脱困兜底。点后进「指定格」模式 → 点棋盘任一格清该格所在一行一列，代价体力。
            // 体力 ≥ ClearToolCost 可用、< 置灰；放在第三信息行左侧（board 上方留白处，y=245）。
            _clearToolBtn = UGuiFactory.CreateButton(_content, "ClearTool", 150, 245, 240, 60,
                $"消除道具 ⚡{MergeOrderConfig.ClearToolCost}", 26,
                new Color32(0xc0, 0x6a, 0x3a, 0xFF), Color.white, out _clearToolBtnBg, out _clearToolBtnLabel);
            _clearToolBtn.onClick.AddListener(OnClearToolClicked);

            // 消除道具提示条（指定格模式时显示「点棋盘任一格，清整行整列」/ 体力不足时显示「等体力恢复」）。
            _clearToolHintBg = UGuiFactory.CreateImage(_content, "ClearHintBg", BlockLayout.DesignWidth / 2f, 245, 360, 52,
                new Color(0, 0, 0, 0.30f));
            _clearToolHintText = UGuiFactory.CreateText(_content, "ClearHint", BlockLayout.DesignWidth / 2f, 245, 360, 52,
                "", 26, new Color32(0xff, 0xcf, 0x5c, 0xFF));
            _clearToolHintBg.gameObject.SetActive(false);
            _clearToolHintText.gameObject.SetActive(false);

            // 指定格 overlay：铺满设计全屏(中心锚点、与 AnchoredToDesign 同基)，几乎全透明、raycastTarget=on，
            // 仅 arming 时启用拦截棋盘点击。SetAsLastSibling 置顶 → arming 时盖住棋盘/槽，点哪都进 OnBoardTap。
            _clearToolOverlay = UGuiFactory.CreateImage(_content, "ClearOverlay",
                BlockLayout.DesignWidth / 2f, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, new Color(0, 0, 0, 0.001f));
            var tapper = _clearToolOverlay.gameObject.AddComponent<BlockBoardTapper>();
            tapper.OnTapCell = OnBoardTapForClearTool;
            _clearToolOverlay.gameObject.SetActive(false);

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
        // 无尽模型（设计 49）：完成单数无终点、不再「/目标」，作累计计数展示（订单持续刷新，无通关）。
        private void RefreshEnergy()
        {
            _energyText.text = $"⚡ {_merge.Energy}/{MergeOrderConfig.EnergyCap}";
            _energyText.color = _merge.CanAffordPlace
                ? new Color32(0x66, 0xff, 0xaa, 0xFF)
                : new Color32(0xff, 0x66, 0x66, 0xFF);
            _goalText.text = $"完成 {_merge.CompletedOrders} 单";
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
                deliver.interactable = can;
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
            bool can = _merge.CanUndo;
            _undoBtn.interactable = can;
            _undoBtnLabel.text = $"悔棋 {_merge.UndoCharges}";
            _undoBtnBg.color = can ? new Color32(0x55, 0x55, 0x88, 0xFF) : new Color32(0x3a, 0x3a, 0x44, 0xFF);
        }

        private void OnUndoClicked()
        {
            CancelClearToolArming(); // 悔棋打断指定格模式
            if (!_merge.Undo(_state, _board)) return;
            ClearGhost();
            RenderBoard();
            RenderSlots();
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo();
            RefreshBlindBox();
            RefreshPiety(); // 悔棋回滚虔诚币（设计 13 §六 T11）
            RefreshClearTool(); // 悔棋回滚体力，gate 态须刷新
        }

        // ── 盲盒计数 + 开盒按钮态（设计 12 §五） ──
        private void RefreshBlindBox()
        {
            // 用 ◈（BMP，LegacyRuntime 字体可渲染）代 🔮（设计 §五写 🔮 或 ◈，盲盒补充平面 emoji 在该字体下渲不出）
            _blindBoxText.text = $"◈ ×{_merge.BlindBoxCount}";
            bool can = _merge.CanOpenBlindBox;
            _openBoxBtn.interactable = can;
            _openBoxBtnBg.color = can ? new Color32(0x7a, 0x4a, 0xb8, 0xFF) : new Color32(0x3a, 0x33, 0x44, 0xFF);
            _openBoxBtnLabel.color = can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF);
        }

        // ── 虔诚币计数（设计 13 §五）。数字走数值系统格式化:大数自动 K/M（设计 15 §3.5 示范接入 O3）──
        private void RefreshPiety()
        {
            _pietyText.text = $"✦ {NumericDisplay.Format(_merge.Piety)}";
        }

        // ── 「神庙」按钮：叠层打开 TempleWindow（不关本窗、不丢局），关闭后刷新虔诚币 ──
        private void OnTempleClicked()
        {
            CancelClearToolArming(); // 开神庙叠层打断指定格模式
            GameModule.UI.ShowUIAsync<TempleWindow>((System.Action)RefreshPiety);
        }

        // ── 开盒（设计 12 §五）：扣 1 → 掷奖 → 发放 → 内联弹字 + 刷新计数/合成区/体力 ──
        private void OnOpenBoxClicked()
        {
            if (!_merge.OpenBlindBox(out var reward)) return;

            BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 530, OpenResultLabel(reward), 48,
                new Color32(0xc8, 0x9a, 0xff, 0xFF));

            RefreshSynthesis(); // 图案进了合成区
            RefreshEnergy();    // 可能加了体力
            RefreshBlindBox();  // 计数与按钮态
            RefreshClearTool(); // 体力可能变化，gate 态须刷新

            MarkAndFlushSave(); // 跨会话存档（设计 14 §3.4）：开盒改盲盒计数/灵力/体力元层 → 标脏 + 落盘
        }

        // ── 消除道具（设计 49 §3.1）：脱困兜底，主动清一行一列、代价体力、无限可用只 gate 体力 ──

        /// <summary>消除道具按钮态：体力 ≥ ClearToolCost 可用、&lt; 置灰；arming 时高亮。</summary>
        private void RefreshClearTool()
        {
            if (_clearToolBtn == null) return;
            bool can = _merge.CanUseClearTool;
            _clearToolBtn.interactable = can;
            _clearToolBtnLabel.color = can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF);
            // arming 高亮(亮橙) / 可用(橙) / 置灰(暗)。
            _clearToolBtnBg.color = _clearToolArming
                ? new Color32(0xff, 0x99, 0x33, 0xFF)
                : (can ? new Color32(0xc0, 0x6a, 0x3a, 0xFF) : new Color32(0x44, 0x3a, 0x33, 0xFF));
        }

        /// <summary>
        /// 点消除道具按钮：体力够则进「指定格」模式（亮按钮 + 提示 + 启用 overlay 等玩家点棋盘格）；
        /// 体力不足则提示「等体力恢复」不进 arming（设计 49 §3.1 置灰 gate）。再点一次按钮取消 arming（开关式）。
        /// </summary>
        private void OnClearToolClicked()
        {
            if (_clearToolArming) { CancelClearToolArming(); return; } // 开关：再点取消
            if (!_merge.CanUseClearTool)
            {
                // 体力不足：提示等恢复，不进 arming（按钮本已置灰，双保险）。
                ShowClearToolHint("体力不足，等恢复");
                return;
            }
            _clearToolArming = true;
            if (_clearToolOverlay != null)
            {
                _clearToolOverlay.gameObject.SetActive(true);
                _clearToolOverlay.transform.SetAsLastSibling(); // 置顶拦截棋盘/槽点击
            }
            ShowClearToolHint("点棋盘任一格，清整行整列");
            RefreshClearTool();
        }

        /// <summary>玩家在 arming 模式下点棋盘格 (col,row)：清该格所在一行一列、扣体力、刷新；越界则取消 arming。</summary>
        private void OnBoardTapForClearTool(int col, int row)
        {
            if (!_clearToolArming) return;
            // 越界点击(点到棋盘外)：取消 arming，不扣体力(玩家可重新点按钮)。
            if (col < 0 || col >= N || row < 0 || row >= N) { CancelClearToolArming(); return; }

            // 二次 gate(防 arming 期间体力被其它路径耗低)：不够则取消、提示。
            if (!_merge.CanUseClearTool) { CancelClearToolArming(); ShowClearToolHint("体力不足，等恢复"); return; }

            // 落子前打快照：消除道具是可悔棋的玩法动作(与落子同体例,回滚体力 + 棋盘)。
            _merge.CaptureSnapshot(_state, _board);

            _merge.SpendClearToolCost();                  // 扣体力(已确认 CanUseClearTool)
            _state.ClearToolRowCol(_board, row, col);     // 清一行一列(同步 SaveArr/ElementArr/BinaryBoard)
            // 设计 49 §四 / §3.1：清一行一列不清空全盘、不触发全清判定——此处不调 ClearSettlement,
            // 全清奖仍只由正常消除清空棋盘触发(B13 全清奖不被白嫖)。

            CancelClearToolArming();                      // 退出 arming
            ClearGhost();
            RenderBoard();
            RefreshEnergy();
            RefreshUndo();                                // 打了快照，悔棋按钮态变
            RefreshClearTool();                           // 体力变，gate 态刷新

            MarkAndFlushSave();                           // 体力进盘(设计 14 §3.7)：用消除道具后标脏 + 落盘
        }

        /// <summary>退出指定格模式：清 arming 标志 + 隐 overlay/提示 + 刷新按钮态。可重复安全调用。</summary>
        private void CancelClearToolArming()
        {
            if (!_clearToolArming && (_clearToolOverlay == null || !_clearToolOverlay.gameObject.activeSelf))
            {
                HideClearToolHint();
                return;
            }
            _clearToolArming = false;
            if (_clearToolOverlay != null) _clearToolOverlay.gameObject.SetActive(false);
            HideClearToolHint();
            RefreshClearTool();
        }

        private void ShowClearToolHint(string msg)
        {
            if (_clearToolHintBg == null) return;
            _clearToolHintText.text = msg;
            _clearToolHintBg.gameObject.SetActive(true);
            _clearToolHintText.gameObject.SetActive(true);
        }

        private void HideClearToolHint()
        {
            if (_clearToolHintBg == null) return;
            _clearToolHintBg.gameObject.SetActive(false);
            _clearToolHintText.gameObject.SetActive(false);
        }

        /// <summary>开盒结果弹字文案（图案：「开出：◆ Lv3 ×1」；体力：「开出：⚡ +10」）。</summary>
        private static string OpenResultLabel(BlindBoxReward reward)
        {
            if (reward.IsEnergy) return $"开出：⚡ +{reward.EnergyGain}";
            if (reward.IsPattern)
                return $"开出：{MergeElementVisual.Glyph(reward.PatternType)} Lv{reward.PatternLevel} ×{reward.PatternCount}";
            return "开出：—";
        }

        private void OnDeliverClicked(int slot)
        {
            CancelClearToolArming(); // 交付打断指定格模式
            if (!_merge.Deliver(slot)) return;
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshUndo(); // 交付清空悔棋栈，按钮须刷新
            RefreshBlindBox();
            RefreshPiety(); // 交付发虔诚币（设计 13 §3.1）
            RefreshClearTool(); // 体力随交付变化，按钮 gate 态须刷新

            MarkAndFlushSave(); // 跨会话存档（设计 14 §3.4）：交付改元层(含体力) → 标脏 + 落盘
            // 无尽模型（设计 49）：订单交付后照常刷新下一单（Deliver 内已 NextOrder），无通关终点、不触发任何结算面板。
        }

        /// <summary>
        /// 跨会话存档落盘（设计 14 §3.4）：元变更后标脏并异步落盘合并写入。
        /// 调度策略取「每次元动作结束即异步落盘」（O5 默认，元动作频率低，够用）。
        /// SaveAsync 内部经 Provider 写入、失败吞掉不阻断玩法；落盘后清脏。
        /// </summary>
        private void MarkAndFlushSave()
        {
            if (_merge == null) return;
            _merge.RequestSave();
            FlushSaveIfDirty();
        }

        /// <summary>脏位为真则异步落盘并清脏（退出/暂停/元动作共用）。Forget 即发即忘,异步写不阻塞主线程。</summary>
        private void FlushSaveIfDirty()
        {
            if (_merge == null || !_merge.IsSaveDirty) return;
            var dto = _merge.ExportMeta();
            _merge.ClearSaveDirty();
            MergeMetaPersistence.SaveAsync(dto).Forget();
        }

        // ── 渲染棋盘（方块色 / 皮肤，设计 50 §二）──
        // 皮肤态分叉，两态都贴图：彩色态按方块类型贴 default_skin 各自那张纹理（blocks_main_<编号>，B1）；
        // 单色态全盘所有方块不分类型统一贴「当前单色标识」那张 sprite（blocks_skin_atlas_<编号>，B2/B3）。
        private void RenderBoard()
        {
            bool mono = _merge != null && _merge.Skin.IsMono;
            // 单色态:全盘统一 sprite location = blocks_skin_atlas_<编号>(散 PNG 按文件名寻址)。
            string monoLoc = mono ? BlockSkinCatalog.SpriteName(_merge.Skin.MonoId) : null;

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
                        Image img = existing;
                        if (img == null)
                        {
                            // 初始纯色仅作 sprite 异步加载到位前的占位,避免闪空(到位后 ApplyCellSkin 切白 tint+贴图)。
                            var placeholder = BlockLayout.ColorOf((BlockColor)colorIdx);
                            var center = BlockLayout.CellCenterDesign(c, r);
                            img = UGuiFactory.CreateImage(_boardLayer, $"cell_{r}_{c}", center.x, center.y,
                                BlockLayout.CellSize - 6, BlockLayout.CellSize - 6, placeholder);
                            img.raycastTarget = false;
                            _cellImages[r, c] = img;
                        }
                        ApplyCellSkin(img, mono, monoLoc, colorIdx);
                    }
                }
            }
            RenderElements();
        }

        /// <summary>
        /// 给一个棋盘格 Image 施加皮肤（设计 50 §二），两态都贴图、白色 tint 让 sprite 显本色：
        /// 单色态 → 全盘统一贴当前单色 sprite；彩色态 → 按方块类型 colorIdx 贴 default_skin 各自那张纹理。
        /// sprite 加载经既有 SetSprite 异步外壳（散 PNG 按文件名 location），引用计数自管。
        /// </summary>
        private void ApplyCellSkin(Image img, bool mono, string monoLoc, int colorIdx)
        {
            if (img == null) return;
            img.color = Color.white;                 // 两态 sprite 均自带颜色,tint 用白避免叠色
            // 单色态全盘同图;彩色态按类型取 default_skin 纹理(设计 50 §二)。
            img.SetSprite(mono ? monoLoc : BlockSkinCatalog.ColoredSpriteName(colorIdx));
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
        // 候选块换皮：跟随与棋盘格完全相同的皮肤状态贴图（候选预览 = 落到棋盘后的样子，设计 50 §二）。
        // 单色态全部候选块统一贴当前单色 sprite；彩色态按方块类型贴 default_skin 各自纹理。镜像 ApplyCellSkin，不另起寻址。
        private void RenderSlots()
        {
            bool mono = _merge != null && _merge.Skin.IsMono;
            // 单色态:全部候选块统一 sprite location = blocks_skin_atlas_<编号>(与棋盘单色态同口径)。
            string monoLoc = mono ? BlockSkinCatalog.SpriteName(_merge.Skin.MonoId) : null;

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
                        // 候选块换皮:白 tint 让 sprite 显本色,彩色态按方块类型 (int)piece.Color 贴 default_skin,单色态全统一贴当前单色 sprite。
                        ci.color = Color.white;
                        ci.SetSprite(mono ? monoLoc : BlockSkinCatalog.ColoredSpriteName((int)piece.Color));
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
            CancelClearToolArming(); // 拖拽落子打断指定格模式（玩家改主意去落子）
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
                // 体力不足不可落子（#2：体力为 0 时不可再落子）
                if (inBounds && _merge.CanAffordPlace && _board.CanPutBlock(shapeId, new Vec2Int(col, row)))
                {
                    PlaceAndResolve(slotIdx, shape, col, row);
                    return;
                }
            }

            container?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
        }

        /// <summary>
        /// 落子 → 扣体力 → 消除返体力 + 元素入合成区（自动升级）→ 结算（连消/多消/全清/女神/盲盒/皮肤）→ 刷新 → 落盘。
        /// 无尽模型（设计 49）：无通关、无软/硬 GameOver——卡死与体力归零都不结束、不弹面板，窗口保持可交互
        /// （卡死靠消除道具脱困、体力归零靠时基恢复 + 订单补，两条兜底见设计 49 §三）。
        /// </summary>
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

            // 本手结算是否改了元层(全清推女神 / 全清或连消阈值发盲盒);为真则落子后须标脏落盘(设计 14 §3.4)。
            bool metaChangedBySettle = false;

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

                // 元层判定（设计 14 §3.4）：全清推女神(AdvanceGoddess) / 全清或连消阈值发盲盒(AddBlindBox)
                // 都改了进盘字段(goddessLevel/goddessRating/blindBoxCount)。AllClearRewarded 隐含女神+盲盒,
                // GoddessLeveledUp 与 BlindBoxGained 并列保险:无后续交付/开盒/修复时,这一手的女神/盲盒进度也须落盘。
                metaChangedBySettle = settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained > 0;

                // 方块皮肤切换（设计 50 §三）：全清发奖口径（已武装全清，同女神/图案奖励）触发换皮——
                // 彩色→单色（首次）/ 单色换一张排除当前（后续）。纯视觉附加，不改上方全清结算（设计 50 §三 规则 5 / A9）。
                // 皮肤态进元层存档（设计 50 §六），故换皮即标元层脏 → 下方 metaChangedBySettle 已为 true（AllClearRewarded 蕴含），落盘随之发生。
                if (settle.AllClearRewarded)
                    _merge.Skin.OnAllClear(BlockSkinCatalog.MonoIds);

                RenderBoard();
                // 全清换皮后,待选区残留候选块须与棋盘同步换皮(设计 50,候选预览 = 落盘后样子)。
                // 下方仅在「三槽全空」才补块+RenderSlots;若全清时尚有未落候选块,需在此显式重渲染让其跟随新皮肤态。
                if (settle.AllClearRewarded)
                    RenderSlots();

                if (settle.AllClearRewarded)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, "PERFECT!", 64, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                else if (lines >= MergeOrderConfig.MultiClearMilestoneMinLines)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, settle.MultiLabel, 56, new Color32(0x55, 0xdd, 0xaa, 0xFF));
                else if (settle.ComboChain >= 2)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 470, $"COMBO x{settle.ComboChain}", 56, new Color32(0xff, 0x77, 0xbb, 0xFF));

                // 获得盲盒（连消阈值 / 全清解锁）弹「+N ◈」（设计 12 §五）
                if (settle.BlindBoxGained > 0)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 590, $"+{settle.BlindBoxGained} ◈", 50,
                        new Color32(0xc8, 0x9a, 0xff, 0xFF));
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
            RefreshBlindBox();
            RefreshPiety(); // 女神升档可能改长期主线展示态(保险刷新)
            RefreshClearTool(); // 落子扣体力 → gate 态须刷新

            // 跨会话存档（设计 14 §3.4）：落子必扣体力(体力已进盘,设计 14 §3.7),且本手可能改其它元层
            // (女神升档 / 盲盒 / 皮肤),故每次落子结算后标脏 + 落盘,保证体力 + 元层进度可靠落盘。
            MarkAndFlushSave();

            // 补充候选块（全空才补）
            bool allEmpty = true;
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) { allEmpty = false; break; }
            if (allEmpty)
            {
                _state.RefillPieces(_board);
                RenderSlots();
            }

            // 无尽模型（设计 49 §一/§二）：删通关 + 删软/硬 GameOver。
            // 卡死（手持块无处可放）：不弹 GameOver，玩法窗保持可交互——玩家用消除道具清一行一列脱困（设计 49 §3.1）。
            // 体力归零（付不起落子）：不弹「精力耗尽」，等时基恢复 / 订单补 / 用消除道具（设计 49 §3.2）。
            // 两条兜底保证任何状态有限时间内可脱困（设计 49 §3.3），故此处不再有任何结束判定。
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

        // 无尽模型（设计 49）：删 TriggerWin（通关结算窗 MergeOrderWinWindow）+ TriggerGameOver（GameOverWindow）。
        // 卡死 / 体力归零都不结束游戏、不弹任何结算或 GameOver 面板，玩法窗持续可交互。
        // 「累计游戏 N 局」活动（设计 47/48 AccumulatePlayCount）原挂在这两个终点 hook 上，无「局」后失效——
        // 本窗不再触发该活动（善后见 dev 交接区 follow-up）。MergeOrderWinWindow / GameOverWindow 不再被本窗引用
        // （代码 / prefab 保留，Classic GameWindow 仍用 GameOverWindow，故不删窗）。

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
            // 解除应用暂停事件订阅，避免销毁后的窗口仍被回调（驱动器是常驻 MonoBehaviour，不解订阅会泄漏引用）。
            Utility.Unity.RemoveOnApplicationPauseListener(OnAppPause);
            // 跨会话存档兜底（设计 14 §3.4 ③）：离开前脏则强制落盘,保证「随手退出」不丢末次元变更。
            // 须在 ExitMergeOrder 丢弃 MergeState 之前落盘。
            FlushSaveIfDirty();
            // 安全兜底：离开必定关闭门控，确保后续 Classic / 08 行为零残留。
            if (_state != null) _state.ExitMergeOrder();
        }
    }
}
