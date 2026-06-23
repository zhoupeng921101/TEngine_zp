using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;
using GameLogic.BlockBlast.Core;
    
namespace GameLogic
{
    /// <summary>
    /// 玩法融合主玩法窗口（设计 29 + 无尽模型 设计 49）：承载完整经济（体力 / 合成 / 订单 / 盲盒 / 女神 / 神庙）+ 塔罗木质换皮。
    /// 棋盘/拖拽/ghost/落子流程与 <see cref="GameWindow"/> 同构；叠加体力条 / 双订单卡（手动交付）/
    /// 合成区面板 / 消除道具按钮。全程 MergeOrderMode=on（OnCreate 重置时开启，OnDestroy/离开时关闭）。
    /// 融合后这是唯一主玩法入口（经典纯无尽 GameWindow 入口下线，代码保留不删，设计 29 §4.2）。
    ///
    /// 无尽模型（设计 49）：无「局」、订单无限、无任何 GameOver（卡死与体力归零都不结束、不弹面板，窗口保持可交互）。
    /// 两条兜底保证「真·无尽」：体力时基恢复（含离线，进盘后由 <see cref="BlockGameState.ResetForMergeOrder"/> 补算）+
    /// 消除道具（主动清一行一列、代价体力、无限可用只 gate 体力）。
    /// </summary>
    [Window(UILayer.UI, location: "MergeOrderWindow", fullScreen: true)]
    public sealed partial class MergeOrderWindow : UIWindow
    {
        // ── game_main 紫金换皮：静态视觉壳的 sprite + tint 直接烤进 prefab 绑定节点（_Gen.g.cs 的 m_*）的 m_Sprite/m_Color，
        //    编辑器内所见即所得，prefab 为静态视觉唯一来源（代码不再运行时 SetSprite 这些节点，避免覆盖美术在 prefab 的调整）。
        //    棋盘框/待选区/HUD 条/订单宝箱卡/消除道具锤子贴带色图（tint 白显本色）；体力/虔诚币/盲盒/神庙图标白剪影染色。
        //    整体背景无 game_main 素材，深紫纯色占位（m_img_Bg 仅烤 m_Color、无 sprite）。
        //    动态内容（棋盘格/ghost/候选块/元素图标/订单卡/合成 token）仍代码生成、运行时 SetSprite 填进空层节点；
        //    消除道具 gate 染色仍由 RefreshClearTool 运行时按体力门控写入。
        //    玩法逻辑（落子/消除/合成/订单/结算/存档）一律不动——换皮只改静态视觉与定位，不碰经济与坐标常量。
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
        private readonly Image[,] _elemCells = new Image[N, N];
        private readonly RectTransform[] _slotContainers = new RectTransform[3];
        private readonly Image[] _ghostPool = new Image[N * N];
        private int _ghostUsed;

        private Text _energyText;
        private Text _goalText;

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
        // 静态壳复用 prefab 绑定节点（_Gen.g.cs 的 m_*），不再 UGuiFactory 动态创建同名节点（避免双份）。
        // 静态视觉（sprite + tint）已烤进 prefab 绑定节点，prefab 为唯一来源，本方法只取引用、接事件、初始化隐藏态，不再运行时 SetSprite/染色这些静态节点。
        // 动态内容（棋盘格 / ghost / 候选块 / 元素图标 / 订单卡 / 合成 token）仍由代码生成 + 运行时 SetSprite，parent 到 prefab 的空层节点；
        // 其寻址走 UIRaw/Atlas 收集器（AddressByFileName + Single 精灵）按文件名 SetSprite。
        private void BuildStaticUI()
        {
            // Content 复用 prefab 既有节点（prefab 已设 1080×1920 / localScale=1 / center 锚点）。
            _content = (RectTransform)transform.Find("Content");

            // 空层节点：prefab 内 center 锚点、anchoredPosition(0,0)，与 DesignToAnchored 同基——动态内容直接填进去。
            _boardLayer = (RectTransform)_content.Find("BoardLayer");
            _elemLayer = (RectTransform)_content.Find("ElemLayer");
            _ghostLayer = (RectTransform)_content.Find("GhostLayer");
            _slotLayer = (RectTransform)_content.Find("SlotLayer");
            _orderLayer = (RectTransform)_content.Find("OrderLayer");
            _synthLayer = (RectTransform)_content.Find("SynthLayer");

            // ── 静态视觉壳（背景占位色 / 背景条 / 棋盘外框 / 待选区背景 / 订单宝箱卡 / 顶部货币 icon）
            // 已烤进 prefab 绑定节点的 m_Sprite + m_Color（编辑器内所见即所得，prefab 为静态视觉唯一来源）。
            // 此处不再运行时 SetSprite/染色——以免覆盖美术在 prefab 上的可视化调整。
            // 仍随状态变化的视觉（棋盘格 / 候选块 / 元素图标 / ClearTool gate 染色）保持运行时，见下文与各 Render*/Refresh*。

            // ── 静态文字壳：复用绑定文字节点（真实数值由 Refresh* 写入，下方各 Refresh 改赋值目标即可）。
            _energyText = m_text_Energy;     // 体力（⚡ x/x）
            _goalText = m_text_Goal;         // 完成单数（完成 N 单）
            _blindBoxText = m_text_BlindBox; // 盲盒（◈ ×N）
            _pietyText = m_text_Piety;       // 虔诚币（✦ N）
            // 顶部槽数值文字与上面 3 个 icon 并排：CoinNum=虔诚币 / GemNum=盲盒 / EnergyNum=体力。
            // 直接复用顶部 3 个数字节点显示真实值（避免 12345 占位与不存在货币）。

            // ── 开盒按钮：复用绑定按钮 + 其 Image/Label。
            _openBoxBtn = m_btn_OpenBox;
            _openBoxBtnBg = m_btn_OpenBox.GetComponent<Image>();
            _openBoxBtnLabel = m_btn_OpenBox.GetComponentInChildren<Text>();
            _openBoxBtn.onClick.AddListener(OnOpenBoxClicked);

            // ── 神庙按钮（右下角）：图标 icon_temple 染金已烤进 prefab；此处只接按钮事件。
            m_btn_Temple.onClick.AddListener(OnTempleClicked);

            // ── 消除道具按钮（左下角，设计 49 §3.1）：图标 消除道具（锤子）已烤进 prefab。
            // gate 视觉为动态染图标（可用=白本色、置灰=暗、arming 高亮），故 _clearToolBtnBg 指向图标 Image；本按钮无 Label。
            // gate 染色由 RefreshClearTool 运行时按体力门控写入，prefab 烤的白本色仅作进窗首帧前的预览底色。
            _clearToolBtn = m_btn_ClearTool;
            // _clearToolBtnBg = m_img_ClearToolIcon;
            _clearToolBtnLabel = null; // 图标按钮无文字 label，RefreshClearTool 内已 null-guard
            _clearToolBtn.onClick.AddListener(OnClearToolClicked);

            // ── 消除道具提示条：复用绑定隐藏节点（prefab 已初始隐藏）。
            _clearToolHintBg = m_img_ClearHintBg;
            _clearToolHintText = m_text_ClearHint;
            _clearToolHintBg.gameObject.SetActive(false);
            _clearToolHintText.gameObject.SetActive(false);

            // ── 指定格 overlay：复用绑定 m_img_ClearOverlay（prefab 铺满设计全屏、初始隐藏）。
            // 运行时挂 BlockBoardTapper 捕获棋盘点击（指定格清行列）。
            _clearToolOverlay = m_img_ClearOverlay;
            _clearToolOverlay.color = new Color(0, 0, 0, 0.001f);
            _clearToolOverlay.raycastTarget = true;
            var tapper = _clearToolOverlay.gameObject.AddComponent<BlockBoardTapper>();
            tapper.OnTapCell = OnBoardTapForClearTool;
            _clearToolOverlay.gameObject.SetActive(false);
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
            // 顶部体力槽数字（EnergyIcon 对应）。
            if (m_text_EnergyNum != null) m_text_EnergyNum.text = $"{_merge.Energy}/{MergeOrderConfig.EnergyCap}";
        }

        // ── 双订单卡（每次刷新重建，含交付按钮点亮/置灰） ──
        private void RefreshOrders()
        {
            for (int i = _orderLayer.childCount - 1; i >= 0; i--)
                Object.Destroy(_orderLayer.GetChild(i).gameObject);

            var orders = _merge.ActiveOrders;
            if (orders == null) return;

            // 重定位（效果图顶部右侧订单区，对齐 prefab m_img_OrderCard）：
            // 2 张订单卡紧凑并排到顶部右侧，缩小尺寸适配新布局。坐标系走 DesignToAnchored（原生 1080 空间），玩法数据/刷新时机不变。
            const float cardW = 252f;
            const float cardH = 144f;
            const float cardY = 216f;
            // 两卡中心：左卡 design x≈677、右卡 design x≈936（落在 OrderCard/BoxCard 框附近的右上区）。
            float[] centers = { 677f, 936f };

            for (int slot = 0; slot < orders.Length && slot < centers.Length; slot++)
            {
                var o = orders[slot];
                float cx = centers[slot];

                UGuiFactory.CreateImage(_orderLayer, $"orderCard_{slot}", cx, cardY, cardW, cardH,
                    new Color32(0x22, 0x2c, 0x3e, 0xCC));

                // 元素图标（clip 图标 sprite，白 tint 显本色）
                var orderIcon = UGuiFactory.CreateImage(_orderLayer, $"orderGlyph_{slot}", cx - 79, cardY - 23, 81, 81,
                    Color.white);
                orderIcon.raycastTarget = false;
                orderIcon.SetSprite(MergeElementVisual.SpriteName(o.Type));
                // 等级 + 数量
                UGuiFactory.CreateText(_orderLayer, $"orderReq_{slot}", cx + 26, cardY - 23, 173, 72,
                    $"Lv{o.Level}\n×{o.Count}", 35, Color.white, TextAnchor.MiddleLeft);

                // 交付按钮
                bool can = _merge.CanDeliver(slot);
                int captured = slot;
                var deliver = UGuiFactory.CreateButton(_orderLayer, $"orderDeliver_{slot}", cx, cardY + 43, cardW - 29, 52,
                    "交付", 35,
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

            // 重定位（效果图元素行，对齐 prefab m_img_ElemBar）：
            // 合成 token 行落在元素行 y≈310（原生 1080 空间）。底条由静态 m_img_ElemBar 提供，不再自建 synthBg。
            const float rowY = 310f;

            // 稳定排序：按类型枚举值、再按等级
            var keys = new List<(MergeElement type, int level)>(_merge.Inventory.Keys);
            keys.Sort((a, b) =>
            {
                int t = ((int)a.type).CompareTo((int)b.type);
                return t != 0 ? t : a.level.CompareTo(b.level);
            });

            if (keys.Count == 0)
            {
                UGuiFactory.CreateText(_synthLayer, "synthEmpty", BlockLayout.DesignWidth / 2f, rowY, 1037, 86,
                    "合成区：空（消除元素入区，自动两两升级）", 35, new Color32(0x88, 0x99, 0xaa, 0xFF));
                return;
            }

            const float tokenW = 187f;
            int n = keys.Count;
            float totalW = n * tokenW;
            float startX = BlockLayout.DesignWidth / 2f - totalW / 2f + tokenW / 2f;
            for (int i = 0; i < n; i++)
            {
                var key = keys[i];
                int count = _merge.Inventory[key];
                float cx = startX + i * tokenW;
                var synthIcon = UGuiFactory.CreateImage(_synthLayer, $"synthGlyph_{i}", cx - 32, rowY, 86, 86,
                    Color.white);
                synthIcon.raycastTarget = false;
                synthIcon.SetSprite(MergeElementVisual.SpriteName(key.type));
                UGuiFactory.CreateText(_synthLayer, $"synthInfo_{i}", cx + 43, rowY, 130, 101,
                    $"L{key.level}\n×{count}", 35, Color.white);
            }
        }

        // ── 盲盒计数 + 开盒按钮态（设计 12 §五） ──
        private void RefreshBlindBox()
        {
            // 用 ◈（BMP，LegacyRuntime 字体可渲染）代 🔮（设计 §五写 🔮 或 ◈，盲盒补充平面 emoji 在该字体下渲不出）
            _blindBoxText.text = $"◈ ×{_merge.BlindBoxCount}";
            // 顶部盲盒槽数字（GemIcon 对应）。
            if (m_text_GemNum != null) m_text_GemNum.text = $"{_merge.BlindBoxCount}";
            bool can = _merge.CanOpenBlindBox;
            _openBoxBtn.interactable = can;
            _openBoxBtnBg.color = can ? new Color32(0x7a, 0x4a, 0xb8, 0xFF) : new Color32(0x3a, 0x33, 0x44, 0xFF);
            _openBoxBtnLabel.color = can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF);
        }

        // ── 虔诚币计数（设计 13 §五）。数字走数值系统格式化:大数自动 K/M（设计 15 §3.5 示范接入 O3）──
        private void RefreshPiety()
        {
            _pietyText.text = $"✦ {NumericDisplay.Format(_merge.Piety)}";
            // 顶部虔诚币槽数字（CoinIcon 对应）。
            if (m_text_CoinNum != null) m_text_CoinNum.text = NumericDisplay.Format(_merge.Piety);
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

            BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 763, OpenResultLabel(reward), 69,
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
            if (_clearToolBtnLabel != null)
                _clearToolBtnLabel.color = can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF);
            // gate 视觉染图标（消除道具锤子）：arming 高亮(亮橙叠白) / 可用(白本色) / 置灰(暗灰)。
            m_btn_ClearTool.image.color = _clearToolArming
                ? new Color32(0xff, 0xcc, 0x88, 0xFF)
                : (can ? Color.white : new Color32(0x66, 0x66, 0x66, 0xFF));
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

            _merge.SpendClearToolCost();                  // 扣体力(已确认 CanUseClearTool)
            _state.ClearToolRowCol(_board, row, col);     // 清一行一列(同步 SaveArr/ElementArr/BinaryBoard)
            // 设计 49 §四 / §3.1：清一行一列不清空全盘、不触发全清判定——此处不调 ClearSettlement,
            // 全清奖仍只由正常消除清空棋盘触发(B13 全清奖不被白嫖)。

            CancelClearToolArming();                      // 退出 arming
            ClearGhost();
            RenderBoard();
            RefreshEnergy();
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

        // ── 渲染元素 overlay（clip 图标 sprite） ──
        // 元素图标 Image：白 tint 显本色（不再用 ColorOf）、raycastTarget=false（不挡棋盘点击）。
        // 尺寸比格略小留边（CellSize*0.7）。None 不建 Image / 已建则销毁置 null。
        private void RenderElements()
        {
            float iconSize = BlockLayout.CellSize * 0.7f;
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
                            existing.SetSprite(MergeElementVisual.SpriteName(el));
                        }
                        else
                        {
                            var center = BlockLayout.CellCenterDesign(c, r);
                            var icon = UGuiFactory.CreateImage(_elemLayer, $"elem_{r}_{c}", center.x, center.y,
                                iconSize, iconSize, Color.white);
                            icon.raycastTarget = false;
                            icon.SetSprite(MergeElementVisual.SpriteName(el));
                            _elemCells[r, c] = icon;
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
                        crt.sizeDelta = new Vector2(BlockLayout.SlotCell - 4, BlockLayout.SlotCell - 4);
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
                            // 候选块上的元素标记：clip 图标 sprite（白 tint 显本色），铺满格子、不挡拖拽。
                            var gt = new GameObject($"sg_{r}_{c}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                            var grt = gt.GetComponent<RectTransform>();
                            grt.SetParent(crt, false);
                            grt.anchorMin = Vector2.zero; grt.anchorMax = Vector2.one;
                            grt.offsetMin = Vector2.zero; grt.offsetMax = Vector2.zero;
                            var gimg = gt.GetComponent<Image>();
                            gimg.color = Color.white;
                            gimg.raycastTarget = false;
                            gimg.SetSprite(MergeElementVisual.SpriteName(el));
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

                // 落子失败：按门槛给出原因。体力优先（付不起则无论位置都落不了，先说体力），
                // 其次越界（块没落在棋盘内），再次占用/形状冲突（位置被占或形状放不下）。
                string reason = !_merge.CanAffordPlace ? "体力不足，等恢复"
                              : !inBounds ? "超出棋盘"
                              : "这里放不下";
                BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 950, reason, 63,
                    new Color32(0xff, 0x99, 0x66, 0xFF));
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
                SpawnClearBurstFx(clear.Rows, clear.Cols);                      // 在 SaveArr 被清前读色，逐被消格放爆破粒子
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
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 677, "PERFECT!", 92, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                else if (lines >= MergeOrderConfig.MultiClearMilestoneMinLines)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 677, settle.MultiLabel, 81, new Color32(0x55, 0xdd, 0xaa, 0xFF));
                else if (settle.ComboChain >= 2)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 677, $"COMBO x{settle.ComboChain}", 81, new Color32(0xff, 0x77, 0xbb, 0xFF));

                // 获得盲盒（连消阈值 / 全清解锁）弹「+N ◈」（设计 12 §五）
                if (settle.BlindBoxGained > 0)
                    BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 850, $"+{settle.BlindBoxGained} ◈", 72,
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
        /// 对被消的整行/整列里每个已占格放一发消除爆破粒子（设计配方 clear_burst）。
        /// 必须在 <see cref="BlockGameState.ClearRowsAndCols"/> 清 SaveArr 之前调用——颜色从 SaveArr 读。
        /// 碎块层染该格方块色（colorIdx → BlockLayout.ColorOf）；行列交叉格只放一发（去重）。
        /// 粒子挂在棋盘格同层 <c>_boardLayer</c>、同坐标系（格中心设计坐标），与 cell Image 对位。
        /// </summary>
        private void SpawnClearBurstFx(IList<int> rows, IList<int> cols)
        {
            if (_boardLayer == null || _state?.SaveArr == null) return;

            bool[,] done = new bool[N, N]; // 去重：行 pass 与列 pass 的交叉格只放一发

            void SpawnAt(int r, int c)
            {
                if (r < 0 || r >= N || c < 0 || c >= N) return;
                if (done[r, c]) return;
                int colorIdx = _state.SaveArr[r][c];
                if (colorIdx < 0) return;                 // 空格不放（与 ClearRowsAndCols 同口径：只清/放已占格）
                done[r, c] = true;
                var center = BlockLayout.CellCenterDesign(c, r); // 注意 (col,row) 顺序：col=c、row=r
                var color = BlockLayout.ColorOf((BlockColor)colorIdx);
                ClearBurstFx.Spawn(_boardLayer, center.x, center.y, color);
            }

            if (rows != null)
                for (int i = 0; i < rows.Count; i++)
                {
                    int r = rows[i];
                    for (int c = 0; c < N; c++) SpawnAt(r, c);
                }
            if (cols != null)
                for (int i = 0; i < cols.Count; i++)
                {
                    int c = cols[i];
                    for (int r = 0; r < N; r++) SpawnAt(r, c);
                }
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

        private partial void OnClick_OpenBoxBtn()
        {
            throw new System.NotImplementedException();
        }

        private partial void OnClick_ClearToolBtn()
        {
            throw new System.NotImplementedException();
        }

        private partial void OnClick_TempleBtn()
        {
            throw new System.NotImplementedException();
        }

        private partial void OnClick_ExitBtn()
        {
            _state.ExitMergeOrder();
            GameModule.UI.CloseUI<MergeOrderWindow>();
            GameModule.UI.ShowUIAsync<MainMenuWindow>();
        }
    }
}
