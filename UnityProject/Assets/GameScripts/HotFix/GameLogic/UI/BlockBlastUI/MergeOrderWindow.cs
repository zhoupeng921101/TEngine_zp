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
    /// 融合主玩法窗口（设计 29 + 无尽模型 设计 49）：承载完整经济（体力 / 合成 / 订单 / 盲盒 / 女神 / 神庙）+ 塔罗木质换皮。
    /// 棋盘 / 拖拽 / ghost / 落子流程与 <see cref="GameWindow"/> 同构；叠加体力条 / 订单卡横滑列表（手动交付，可交付优先）/
    /// 合成区面板 / 消除道具按钮。全程 MergeOrderMode=on（OnCreate 开启，OnDestroy 关闭）。唯一主玩法入口（设计 29 §4.2）。
    ///
    /// 无尽模型（设计 49）：无「局」、订单无限、无 GameOver（卡死与体力归零都不结束、不弹面板，窗口保持可交互）。
    /// 两条兜底：体力时基恢复（含离线，进盘后由 <see cref="BlockGameState.ResetForMergeOrder"/> 补算）+
    /// 消除道具（主动清一行一列、代价体力、只受体力门控）。
    /// </summary>
    [Window(UILayer.UI, location: "MergeOrderWindow", fullScreen: true)]
    public sealed partial class MergeOrderWindow : UIWindowMono
    {
        // 静态视觉壳（sprite + tint）烤进 prefab 绑定节点（_Gen.g.cs 的 m_*）的 m_Sprite / m_Color，prefab 为唯一来源，
        // 代码不运行时 SetSprite 这些节点。动态内容（棋盘格 / ghost / 候选块 / 元素图标 / 订单卡 / 合成 token）由代码生成、
        // 运行时 SetSprite 填进空层节点；消除道具 gate 染色由 RefreshClearTool 运行时按体力门控写入。
        private const int N = BlockLayout.BoardSize;

        private BlockGameState _state;
        private MergeOrderState _merge;
        private BinaryBoard _board;

        private readonly Image[,] _cellImages = new Image[N, N];
        private readonly Image[,] _elemCells = new Image[N, N];
        private readonly RectTransform[] _slotContainers = new RectTransform[3];
        private readonly Image[] _ghostPool = new Image[N * N];
        private int _ghostUsed;

        // 消除预览发光池（落子后会满、将被消除的整行整列高亮，叠加在绿 ghost 之上）：
        // 一条「线条」= 横贯整行或纵贯整列的长条 quad，由 UI/GlowCell shader 在带边缘画绿色描边辉光。
        // 容量 2*N（最多同时 N 行 + N 列同时满）。行列交叉处两条线条加色叠加，自然，无需去重。
        // 与 _ghostPool 同构：OnCreate 建好挂 m_rect_BoardLayer，ShowClearPreviewGlow 只取线条 SetActive + 定位 + 选材质，ClearGhost 里 SetActive(false)。
        private const int GlowBandCap = 2 * N;
        // 带 quad 厚度相对真实行/列厚度的放大倍率（留出向外发散柔光的余量）；shader 的 _CoreFrac 须与之对齐为 1/BandScale。
        private const float BandScale = 1.6f;
        private readonly Image[] _glowPool = new Image[GlowBandCap];
        private readonly GlowPulse[] _glowPulses = new GlowPulse[GlowBandCap];
        // 行 / 列两份材质：一份材质只有一个 _Vertical 值（UGUI Image 不便用 MaterialPropertyBlock），故横条 / 竖条各一份。
        private Material _glowMatRow;
        private Material _glowMatCol;
        private int _glowUsed;

        // 消除预览「行内填充」池（与边缘辉光长条池并列）：覆盖可消除行/列真实区域的半透明绿，使各色方块统一偏绿。
        // 普通 UGUI Image（默认材质，不挂 shader、不挂 GlowPulse），保持稳定常亮；呼吸只在边缘辉光上。
        // 容量同为 2*N（最多 N 行 + N 列）。层级：方块格 < 填充 < 边缘辉光（填充先置顶、边缘后置顶，故边缘在填充之上）。
        private readonly Image[] _fillPool = new Image[GlowBandCap];
        private int _fillUsed;

        // 开盒按钮的子组件缓存（背景 Image / 文字 Label），从绑定按钮 m_btn_OpenBox 派生，按钮态刷新时染色用。
        private Image _openBoxBtnBg;
        private Text _openBoxBtnLabel;

        // 订单卡常驻实例（张数 = MergeOrderConfig.ActiveOrders 单一事实源，OnCreate 创建一次注入 OnDeliver 回调，
        // RefreshOrders 只 SetData + 显隐 + 按可交付优先重排 sibling 顺序，不重建实例、不改 slot 映射）。
        private readonly OrderCardWidget[] _orderCards = new OrderCardWidget[MergeOrderConfig.ActiveOrders];
        // 合成区 token 实例池：_synthTokens 是按 (类型→等级) 排序的「当前展示顺序」live 列表（收集飞行/交付飞行落点匹配按它取），
        // _synthByKey 是 (类型,等级) → 稳定 token 实例映射（增删滑动需要稳定身份：同一 (类型,等级) 恒对应同一 token，
        // 才能做「某 token 滑入/滑出/补位」的位置 tween）。两者每次 RefreshSynthesis 同步重建，_synthByKey 为单一事实源、
        // _synthTokens 由它按排序导出。不再用 AdjustIconNum（按数量增减、身份不稳定，无法做补位滑动）。
        private readonly List<SynthTokenWidget> _synthTokens = new();
        private readonly Dictionary<(MergeElement type, int level), SynthTokenWidget> _synthByKey = new();

        // 通用倒计时 widget（OnCreate 各建一个，宿主每秒喂剩余秒数 + 显隐；组件本身业务无关）：
        // 体力倒计时挂体力栏下方锚点 m_rect_EnergyCountdownSlot——体力满（不再恢复）时隐藏；
        // 订单倒计时挂订单区下方锚点 m_rect_OrderCountdownSlot——整批刷新单一时间戳，常显。
        private CountdownWidget _energyCountdown;
        private CountdownWidget _orderCountdown;

        /// <summary>
        /// 交付飞行进行中标志（设计第二批 §B）：点交付后 count 个元素飞向订单卡期间为 true，全部飞达的聚合回调末尾置 false。
        /// 飞行期间锁交互——忽略再次点交付、忽略落子、跳过订单按时轮询刷新——防飞行途中库存被并发改动使「飞达才扣库存」语义错乱。
        /// </summary>
        private bool _deliverFlying;
        // 横滑列表容器（BuildStaticUI 缓存）：订单 = OrderLayer 的 ScrollRect.content；合成 token = ElemBar 的 ScrollRect.content。
        private RectTransform _orderContent;
        private RectTransform _synthContent;

        /// <summary>消除道具「等待玩家指定棋盘格」模式（点过按钮、未点格前为 true）。</summary>
        private bool _clearToolArming;

        /// <summary>
        /// 消除道具提示条自动隐藏倒计时（秒，&gt;0 时由 OnUpdate 递减到 0 后隐藏）。
        /// 只用于「体力不足」这类短提示（toast）；arming 指令提示需常驻直到玩家操作，不设倒计时。
        /// </summary>
        private float _clearHintHideTimer;
        private const float ClearHintAutoHideSeconds = 2.5f;

        private int _draggingShapeId = -1;
        /// <summary>当前拖拽块的方块色（消除预览按此着色，使可消除行列与拖拽块同色，设计 50 §二 一致性）。</summary>
        private BlockColor _draggingColor;

        /// <summary>体力时基恢复轮询累加器（秒）：每满 EnergyTickInterval 调一次 ApplyTimeRegen + 刷新（设计 49 §3.2 在窗期间也恢复）。</summary>
        private float _energyTickAccum;
        private const float EnergyTickInterval = 1f;

        // ── 自适应棋盘几何（单一事实源，设计常量已不参与本窗棋盘定位）──────────────────────
        // 棋盘格尺寸与位置全部由 m_rect_BoardLayer.rect 现算：8×8 填满 BoardLayer（取短边均分、长边居中），
        // 改 prefab 里 BoardLayer 的 sizeDelta 棋盘整体随之缩放。RenderBoard / RenderElements / UpdateGhost /
        // SpawnClearBurstFx / ComputeGridPos / 消除道具点格 全部走下面三个方法，禁止再散用 BlockLayout.CellSize / BoardOrigin。
        // BoardLayer 锚点为居中固定尺寸（非 stretch），OnCreate 时 .rect 即正确，无需 ForceRebuildLayout。

        /// <summary>自适应格尺寸 = min(BoardLayer 宽,高) / 8（8×8 填满、短边均分）。</summary>
        private float BoardCellSize()
        {
            var rect = m_rect_BoardLayer.rect;
            return Mathf.Min(rect.width, rect.height) / N;
        }

        /// <summary>
        /// 棋盘格 (col,row) 中心在 m_rect_BoardLayer 本地的 anchoredPosition（BoardLayer pivot/anchor 居中，X 右正 Y 上正）。
        /// 格阵以 BoardLayer 中心为中心，col 右增、row 下增（与数据数组 row 自上而下一致）。
        /// </summary>
        private Vector2 BoardCellLocalPos(int col, int row)
        {
            float cell = BoardCellSize();
            float grid = cell * N;
            // 左上格 (0,0) 中心相对 BoardLayer 中心 = (-grid/2 + cell/2, +grid/2 - cell/2)，逐格右/下偏移。
            float x = -grid / 2f + cell / 2f + col * cell;
            float y = grid / 2f - cell / 2f - row * cell;
            return new Vector2(x, y);
        }

        /// <summary>
        /// 屏幕点 → 棋盘格 (col,row)（消除道具点格用）。换算到 BoardLayer 本地空间，用与渲染同一套自适应格尺寸/原点反算。
        /// 越界返回的 col/row 可能 &lt;0 或 ≥N，由调用方判（OnBoardTapForClearTool 越界即取消 arming）。
        /// </summary>
        private (int col, int row) ScreenToBoardCell(Vector2 screen)
        {
            var canvas = m_rect_BoardLayer.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(m_rect_BoardLayer, screen, cam, out var local))
                return (-1, -1);
            float cell = BoardCellSize();
            float grid = cell * N;
            // local 原点在 BoardLayer 中心；格阵左上角在 (-grid/2, +grid/2)。col 沿 +X、row 沿 -Y。
            int col = Mathf.FloorToInt((local.x + grid / 2f) / cell);
            int row = Mathf.FloorToInt((grid / 2f - local.y) / cell);
            return (col, row);
        }

        protected override void OnCreate()
        {
            _state = BlockGameState.Instance;
            _board = new BinaryBoard();

            if (!DynamicWeightDiff.Instance.IsInitialized())
            {
                try { GameLogic.Config.WeightCfgConfigMgr.InitDynamicWeight(); }
                catch (System.Exception e) { Log.Warning($"[MergeOrderWindow] 权重表加载失败，退化随机：{e.Message}"); }
            }
            // 隐患 B（设计 29 §5.3 / 开关 #3）：dynamicWeight 每次进盘重置。
            // Reset() 清 _dynamicWeight / _preDynamicWeight / _refillIndex，消除跨局 / 跨模式 / 跨 app 重启的累积，从中位公平起步。
            DynamicWeightDiff.Instance.Reset();
            DynamicWeightDiff.Instance.BeginGame();

            // 进入即重置（隐患 A，设计 29 §5.3）：ResetForMergeOrder 将 BlockGameState.Score / Combo 清零（局内瞬态，不进盘）。
            // 元层进度（灵力 / 虔诚币 / 女神 / 神庙 / 盲盒 / HighScore 等）经存档加载覆盖，与局内瞬态分层不重叠。
            _state.ResetForMergeOrder(_board);
            _merge = _state.MergeState;
            _clearToolArming = false;

            BuildStaticUI();
            CreateOrderCards();
            CreateCountdowns();
            InitGhostPool();
            InitGlowPool();
            RenderBoard();
            RenderSlots();
            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            RefreshBlindBox();
            RefreshPiety();
            RefreshClearTool();
            // 初次绘制倒计时（OnUpdate 每秒轮询前先填一帧，避免首秒显示 prefab 占位文本）。
            RefreshCountdowns(MergeMetaPersistence.NowUnixSec());

            // 跨会话存档兜底（设计 14 §3.4 ③）：移动端切后台 / 杀进程不经 OnDestroy，会丢末次元变更。
            // UIWindow 非 MonoBehaviour，OnApplicationPause / Quit 不在本类触发；订阅 TEngine 驱动器（UpdateDriver，MonoBehaviour）
            // 转播的应用暂停事件，效果等价。退出 / 销毁路径另由 OnDestroy 的 FlushSaveIfDirty 兜底。
            Utility.Unity.AddOnApplicationPauseListener(OnAppPause);
        }

        /// <summary>
        /// 应用暂停 / 恢复回调（设计 14 §3.4 ③）：pause=true 表示进入后台（切后台 / 锁屏 / 被挂起），此时脏则强制落盘。
        /// </summary>
        private void OnAppPause(bool pause)
        {
            if (pause) FlushSaveIfDirty();
        }

        /// <summary>
        /// 体力时基恢复轮询（设计 49 §3.2）：进窗时 ResetForMergeOrder 只补算一次离线恢复，窗口开着期间
        /// 还需周期补算，否则体力条不随时间刷新（要关窗重开才更新）。每秒一次 ApplyTimeRegen，体力真变了
        /// 才刷 UI + 落盘（避免每秒空转写盘）。ApplyTimeRegen 不满一个 tick 时不动状态，调用幂等无害。
        /// </summary>
        protected override void OnUpdate()
        {
            if (_merge == null || _state == null || !_state.MergeOrderMode) return;

            // 消除道具提示条自动隐藏（仅 autoHide 提示设了倒计时；arming 指令提示 _clearHintHideTimer 恒为 0，不在此隐藏）。
            if (_clearHintHideTimer > 0f)
            {
                _clearHintHideTimer -= Time.deltaTime;
                if (_clearHintHideTimer <= 0f) HideClearToolHint();
            }

            _energyTickAccum += Time.deltaTime;
            if (_energyTickAccum < EnergyTickInterval) return;
            _energyTickAccum = 0f;

            long now = MergeMetaPersistence.NowUnixSec();

            int before = _merge.Energy;
            _merge.ApplyTimeRegen(now);
            if (_merge.Energy != before)
            {
                RefreshEnergy();
                RefreshClearTool(); // 体力变 → 消除道具 gate 态须刷新
                MarkAndFlushSave(); // 体力 + lastEnergyRegenTime 进盘（设计 14 §3.7）
            }

            // 订单按时整批刷新：窗开期间也轮询，到点整批换新（含 lastOrderRefreshTime 进盘）。
            // 交付飞行进行中跳过本次轮询：飞行的聚合回调将对正在交付的订单槽 Deliver(扣库存) + TryRefreshIfAllDelivered，
            // 若此刻 ApplyOrderRefresh 整批换新会冲掉正在交付的订单（飞行落点/库存与新订单不匹配，破坏交付）。
            // 跳过只是延后一帧——飞行约 0.5s 即结束、_deliverFlying 复位，下一次轮询照常判定到时刷新（计时未丢，下次 now 仍满足间隔）。
            if (!_deliverFlying && _merge.ApplyOrderRefresh(now))
            {
                RefreshOrders();
                MarkAndFlushSave();
            }

            // 倒计时每秒刷新（在恢复 / 刷新轮询之后，用同一 now 与已推进的记录时刻，显示新周期剩余）。
            RefreshCountdowns(now);
        }

        // 静态壳复用 prefab 绑定节点（_Gen.g.cs 的 m_*）：本方法只取引用、接事件、初始化隐藏态。
        // 静态文字壳：Refresh* 直接写入绑定文字节点 m_text_*（Energy / Goal / BlindBox / Piety，
        // 顶部数字槽 CoinNum=虔诚币 / GemNum=盲盒 / EnergyNum=体力）。
        private void BuildStaticUI()
        {
            // 开盒按钮：缓存背景 Image / 文字 Label，供 RefreshBlindBox 按可用态染色（点击由生成代码接 OnClick_OpenBoxBtn）。
            _openBoxBtnBg = m_btn_OpenBox.GetComponent<Image>();
            _openBoxBtnLabel = m_btn_OpenBox.GetComponentInChildren<Text>();

            // 横滑列表容器：订单卡建到 OrderLayer 的 ScrollRect.content，合成 token 建到 ElemBar 的 ScrollRect.content。
            _orderContent = m_rect_OrderLayer.GetComponent<ScrollRect>()?.content;
            _synthContent = m_img_ElemBar.GetComponent<ScrollRect>()?.content;
            if (_orderContent == null)
                Log.Error("[MergeOrderWindow] m_rect_OrderLayer 上缺少 ScrollRect 或其 Content 未设置，订单区无法渲染，请检查 prefab。");
            if (_synthContent == null)
                Log.Error("[MergeOrderWindow] m_img_ElemBar 上缺少 ScrollRect 或其 Content 未设置，合成区无法渲染，请检查 prefab。");

            // 消除道具按钮（左下角，设计 49 §3.1）：图标 gate 染色由 RefreshClearTool 运行时按体力门控写入 m_btn_ClearTool.image。
            // 关掉 Button 自带 ColorTint 过渡：prefab 上该按钮 Transition=ColorTint 且 TargetGraphic=图标自身，
            // ColorTint 会覆盖 RefreshClearTool 写入的 gate 染色（置灰/arming 高亮失效）。
            // 设为 None 让手动染色成为唯一权威。按钮始终保持 interactable=true（体力门控只染色不拦点击，见 RefreshClearTool），
            // 体力不足时点击仍落到 OnClick_ClearToolBtn 给出文字提示。
            if (m_btn_ClearTool != null) m_btn_ClearTool.transition = Selectable.Transition.None;

            // 消除道具提示条：绑定隐藏节点（prefab 已初始隐藏）。
            m_img_ClearHintBg.gameObject.SetActive(false);
            m_text_ClearHint.gameObject.SetActive(false);

            // 指定格 overlay：prefab 铺满全屏、初始隐藏；运行时挂 BlockBoardTapper 捕获棋盘点击（指定格清行列）。
            m_img_ClearOverlay.color = new Color(0, 0, 0, 0.001f);
            m_img_ClearOverlay.raycastTarget = true;
            var tapper = m_img_ClearOverlay.gameObject.AddComponent<BlockBoardTapper>();
            tapper.OnTapCell = OnBoardTapForClearTool;
            // 自适应棋盘：注入 BoardLayer 本地空间换算，保证「点中格 = 棋盘可见格」（否则按旧固定常量错位）。
            tapper.ScreenToCell = ScreenToBoardCell;
            m_img_ClearOverlay.gameObject.SetActive(false);
        }

        // ghost 池挂在 m_rect_BoardLayer 下（不挂 m_rect_GhostLayer）：GhostLayer 与 BoardLayer 不同父、不同位，
        // 挂 GhostLayer 会让 ghost 高亮格与棋盘格错位。挂 BoardLayer 并用 BoardCellLocalPos 定位，
        // 保证「ghost 高亮格 = 棋盘格视觉位置」。尺寸用自适应格尺寸（略留边），UpdateGhost 每次按当前格尺寸刷新。
        private void InitGhostPool()
        {
            float cell = BoardCellSize();
            for (int i = 0; i < _ghostPool.Length; i++)
            {
                var img = UGuiFactory.CreateImage(m_rect_BoardLayer, $"ghost_{i}", 0, 0,
                    cell - 8, cell - 8, BlockLayout.GhostOkColor);
                img.raycastTarget = false;
                img.gameObject.SetActive(false);
                _ghostPool[i] = img;
            }
        }

        // 消除预览发光池：与 ghost 池同父（m_rect_BoardLayer）、同自适应几何，但叠在 ghost 之上（SetAsLastSibling）。
        // 每条线条 = 横贯整行或纵贯整列的长条 quad，由 UI/GlowCell shader 在带边缘画绿色描边辉光（不依赖 sprite 形状）；
        // 每条线条挂一个 GlowPulse 做呼吸（自门控、零分配，经顶点色 alpha 驱动 shader 的 IN.color.a）。
        // ghost 在 InitGhostPool 已先建好（先挂的在层级靠下），故发光池后建并逐个置顶，保证发光显示在绿 ghost 之上。
        // 行 / 列两份材质 _Vertical 分别为 0 / 1；_CoreFrac = 1/BandScale，使描边落在 quad 拉伸后真实行/列的边缘处。
        private void InitGlowPool()
        {
            _glowMatRow = new Material(Shader.Find("UI/GlowCell"));
            _glowMatRow.SetFloat("_Vertical", 0f);
            _glowMatRow.SetFloat("_CoreFrac", 1f / BandScale);
            _glowMatCol = new Material(Shader.Find("UI/GlowCell"));
            _glowMatCol.SetFloat("_Vertical", 1f);
            _glowMatCol.SetFloat("_CoreFrac", 1f / BandScale);

            float cell = BoardCellSize();
            // 行内填充池：先建（在边缘辉光之前），默认材质纯色半透明绿、不挂 GlowPulse、初始隐藏。
            // 激活时按行 / 列重写 sizeDelta / anchoredPosition；层级在激活时由 ShowClearPreviewGlow 维护（填充 < 边缘辉光）。
            for (int i = 0; i < _fillPool.Length; i++)
            {
                var fill = UGuiFactory.CreateImage(m_rect_BoardLayer, $"clearFill_{i}", 0, 0,
                    cell, cell, BlockLayout.ClearPreviewFillColor);
                fill.raycastTarget = false;
                fill.transform.SetAsLastSibling();     // 置于方块格 / ghost 之上
                fill.gameObject.SetActive(false);
                _fillPool[i] = fill;
            }
            for (int i = 0; i < _glowPool.Length; i++)
            {
                // 初始尺寸占位，激活时按行 / 列重写 sizeDelta / anchoredPosition；材质激活时按行 / 列赋。
                var img = UGuiFactory.CreateImage(m_rect_BoardLayer, $"clearGlow_{i}", 0, 0,
                    cell, cell, BlockLayout.ClearPreviewGlowColor);
                img.raycastTarget = false;
                img.transform.SetAsLastSibling();      // 置于 ghost / 填充之上
                img.gameObject.SetActive(false);
                var pulse = img.gameObject.AddComponent<GlowPulse>();
                pulse.BaseColor = BlockLayout.ClearPreviewGlowColor;
                _glowPulses[i] = pulse;
                _glowPool[i] = img;
            }
        }

        // ── 体力条 / 完成单数 ──
        // 无尽模型（设计 49）：完成单数无终点，作累计计数展示（订单持续刷新，无通关）。
        private void RefreshEnergy()
        {
            m_text_Energy.text = $"{_merge.Energy}/{MergeOrderConfig.EnergyCap}";
            m_text_Energy.color = _merge.CanAffordPlace
                ? new Color32(0x66, 0xff, 0xaa, 0xFF)
                : new Color32(0xff, 0x66, 0x66, 0xFF);
            m_text_Goal.text = $"完成 {_merge.CompletedOrders} 单";
            // 顶部体力槽数字（EnergyIcon 对应）。
            if (m_text_EnergyNum != null) m_text_EnergyNum.text = $"{_merge.Energy}/{MergeOrderConfig.EnergyCap}";
        }

        // ── 订单卡常驻实例创建（OnCreate 一次性，张数 = MergeOrderConfig.ActiveOrders） ──
        // 卡建到 OrderLayer 横滑列表的 Content 下，由 HorizontalLayoutGroup 横向排布（卡尺寸取 OrderCardWidget 的 LayoutElement）；
        // 交付回调注入对应槽位闭包。卡结构 / 视觉由 OrderCardWidget.prefab 提供，本窗不再绑卡内部节点。
        private void CreateOrderCards()
        {
            if (_orderContent == null)
            {
                Log.Error("[MergeOrderWindow] 订单滚动容器缺失（OrderLayer 的 ScrollRect.content），订单卡未创建。");
                return;
            }
            for (int slot = 0; slot < _orderCards.Length; slot++)
            {
                // 资源定位名 == 类名 "OrderCardWidget"（AssetRaw/UI 走 AddressByFileName），CreateWidgetByType 可加载。
                var card = CreateWidgetByType<OrderCardWidget>(_orderContent);
                if (card == null)
                {
                    Log.Error($"[MergeOrderWindow] OrderCardWidget 加载失败（资源定位名 OrderCardWidget），订单卡 {slot} 未创建。");
                    continue;
                }
                // 横向布局自行定位，卡保持 prefab 原始尺寸（LayoutElement 提供首选宽高），仅校正缩放。
                if (card.rectTransform != null) card.rectTransform.localScale = Vector3.one;

                int captured = slot;
                card.OnDeliver = () => OnDeliverClicked(captured);
                _orderCards[slot] = card;
            }
        }

        // ── 倒计时 widget 创建（OnCreate 一次性，各挂上游搭好的锚点）──
        // 资源定位名 == 类名 "CountdownWidget"（AssetRaw/UI 走 AddressByFileName），CreateWidgetByType 可加载。
        // 体力倒计时挂 m_rect_EnergyCountdownSlot（体力栏下），订单倒计时挂 m_rect_OrderCountdownSlot（订单区下）。
        // 喂数 + 显隐由 OnUpdate 每秒轮询统一处理（复用既有 ApplyTimeRegen/ApplyOrderRefresh 的 now）。
        private void CreateCountdowns()
        {
            if (m_rect_EnergyCountdownSlot != null)
            {
                _energyCountdown = CreateWidgetByType<CountdownWidget>(m_rect_EnergyCountdownSlot);
                if (_energyCountdown == null)
                    Log.Error("[MergeOrderWindow] CountdownWidget（体力）加载失败（资源定位名 CountdownWidget），体力倒计时未创建。");
                else if (_energyCountdown.rectTransform != null)
                    _energyCountdown.rectTransform.localScale = Vector3.one;
            }
            else
            {
                Log.Error("[MergeOrderWindow] m_rect_EnergyCountdownSlot 缺失，体力倒计时未创建，请检查 prefab 锚点绑定。");
            }

            if (m_rect_OrderCountdownSlot != null)
            {
                _orderCountdown = CreateWidgetByType<CountdownWidget>(m_rect_OrderCountdownSlot);
                if (_orderCountdown == null)
                    Log.Error("[MergeOrderWindow] CountdownWidget（订单）加载失败（资源定位名 CountdownWidget），订单倒计时未创建。");
                else if (_orderCountdown.rectTransform != null)
                    _orderCountdown.rectTransform.localScale = Vector3.one;
            }
            else
            {
                Log.Error("[MergeOrderWindow] m_rect_OrderCountdownSlot 缺失，订单倒计时未创建，请检查 prefab 锚点绑定。");
            }
        }

        /// <summary>
        /// 每秒刷新两个倒计时（OnUpdate 轮询调用，<paramref name="now"/> 取与 ApplyTimeRegen/ApplyOrderRefresh 一致的 Unix 秒）。
        /// 体力倒计时 = max(0, interval - (now - LastEnergyRegenTime) % interval)；体力满（≥软上限，不再恢复）时隐藏。
        /// 订单倒计时 = max(0, OrderRefreshIntervalSec - (now - LastOrderRefreshTime))；整批刷新单一时间戳，常显。
        /// 记录时刻为 0（尚无记录 / 首次）或 now 早于记录时刻（玩家回拨时钟）时，剩余按整周期显示（不出现负数 / 错乱，与恢复/刷新「首次以 now 初始化、本次不补」一致）。
        /// </summary>
        private void RefreshCountdowns(long now)
        {
            // 体力倒计时：体力满时不再恢复 → 隐藏；未满则显本周期剩余秒。
            if (_energyCountdown != null)
            {
                if (_merge.Energy >= MergeOrderConfig.EnergyCap)
                {
                    _energyCountdown.SetVisible(false);
                }
                else
                {
                    int interval = (int)MergeOrderConfig.RegenIntervalSec;
                    int remain = CountdownRemain(now, _merge.LastEnergyRegenTime, interval, periodic: true);
                    _energyCountdown.SetVisible(true);
                    _energyCountdown.SetRemainingSeconds(remain);
                }
            }

            // 订单倒计时：到点整批刷新，单一时间戳，常显。
            if (_orderCountdown != null)
            {
                int interval = MergeOrderConfig.OrderRefreshIntervalSec;
                int remain = CountdownRemain(now, _merge.LastOrderRefreshTime, interval, periodic: false);
                _orderCountdown.SetVisible(true);
                _orderCountdown.SetRemainingSeconds(remain);
            }
        }

        /// <summary>
        /// 计算到下一次结算的剩余秒（倒计时显示用，纯函数、不改状态）。
        /// <paramref name="periodic"/>=true（体力）：剩余 = interval - 已过秒 % interval（每满间隔重置，循环恢复）。
        /// <paramref name="periodic"/>=false（订单）：剩余 = interval - 已过秒（到点一次性整批刷新）。
        /// 防御：interval ≤ 0（配置错）→ 返 0；lastTime ≤ 0（尚无记录 / 首次）或 now &lt; lastTime（时钟回拨）→ 显整周期 interval（不出负数）。
        /// </summary>
        private static int CountdownRemain(long now, long lastTime, int interval, bool periodic)
        {
            if (interval <= 0) return 0;
            if (lastTime <= 0 || now < lastTime) return interval; // 尚无记录 / 回拨：显整周期，避免负数错乱
            long elapsed = now - lastTime;
            long remain = periodic ? interval - (elapsed % interval) : interval - elapsed;
            if (remain < 0) remain = 0;       // 订单：已过点（轮询将刷新，此刻先显 0）
            if (remain > interval) remain = interval;
            return (int)remain;
        }

        // ── 订单卡刷新（常驻实例，只 SetData + 显隐 + 可交付优先重排，含交付按钮点亮/置灰） ──
        // 卡面只显「元素图标 + ×数量」（等级由图标分级 {type}_{level} 表现，不再写 Lv 文字，与合成 token "×{count}" 同口径）。
        // 显示排序：可交付（CanDeliver）的卡排在前、不可交付的在后，组内保持 slot 原序（稳定）。
        // 卡是常驻实例、各自 OnDeliver 绑死真实 slot——排序只改 sibling 顺序（HLG 按子节点序排布），
        // 不改 slot 映射，SetData 仍用该卡真实 slot 的 orders[slot]/CanDeliver(slot)。事件驱动刷新，每次重算、稳定无抖动。
        private void RefreshOrders()
        {
            var orders = _merge.ActiveOrders;

            // 补位左滑：刷新前先记录订单容器内各卡旧位（含正在滑动中的实时位置）。
            // 交付后空槽卡 SetActive(false) 退出 HLG → 其余卡 HLG 目标位左移，AnimateReflow 让它们从旧位滑到新位（左滑补位）。
            var reflow = _orderContent != null ? LayoutReflowAnimator.GetOrAdd(_orderContent) : null;
            reflow?.CaptureBefore();

            // 先按真实 slot 填数据 + 显隐，互不依赖排序。
            for (int slot = 0; slot < _orderCards.Length; slot++)
            {
                var card = _orderCards[slot];
                if (card == null) continue;

                // 空槽判定须看 Order.IsValid：交付后该槽置 default(Order)（IsValid==false），数组长度仍为 ActiveOrders（slot<Length 恒真），
                // 仅凭索引在界内会把空槽卡判为「有单」而保持显示——故空槽卡须隐藏退出 HLG，其余卡才左滑补位。
                bool hasOrder = orders != null && slot < orders.Length && orders[slot].IsValid;
                card.Visible = hasOrder;
                if (!hasOrder) continue;

                var o = orders[slot];
                card.SetData(MergeElementVisual.SpriteName(o.Type, o.Level), $"×{o.Count}", _merge.CanDeliver(slot));
            }

            // 可交付优先重排：按 slot 升序两趟扫描（先取可交付、再取不可交付），组内保持 slot 原序（稳定）。
            // 逐张 SetSiblingIndex(展示位)，HLG 按子节点顺序横向排布，即把可交付的卡推到列表前端。
            int siblingIndex = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantDeliverable = pass == 0;
                for (int slot = 0; slot < _orderCards.Length; slot++)
                {
                    var card = _orderCards[slot];
                    if (card == null || card.rectTransform == null) continue;
                    bool hasOrder = orders != null && slot < orders.Length && orders[slot].IsValid;
                    if (!hasOrder) continue; // 无单卡已隐藏，不参与排序（留在尾部）
                    if (_merge.CanDeliver(slot) != wantDeliverable) continue;
                    card.rectTransform.SetSiblingIndex(siblingIndex++);
                }
            }

            // 重排后执行补位滑动（其余订单卡旧位→新位，向左对齐补位）。
            reflow?.AnimateReflow(SlideReflowDuration);
        }

        // ── 合成区面板（SynthTokenWidget 稳定池建于 ElemBar 的 ScrollRect Content = m_rect_SynthLayer，HorizontalLayoutGroup 横向排布、超出可横滑） ──
        // 稳定池：(类型,等级) → 同一 token 实例恒定映射（_synthByKey）。新增键 → 建 token（直接落到 HLG 目标位、不滑入）；
        // 消失键 → 销毁 token；留存键 → 原实例 SetData + 滑动补位。增删/补位的滑动由 LayoutReflowAnimator 统一处理
        // （刷新前 CaptureBefore 记录旧位 → 重排 sibling → AnimateReflow 让留存 token 从旧位滑到新位）。
        // _synthTokens 为按 (类型→等级) 排序的 live 列表（收集飞行/交付飞行落点匹配按它取），由 _synthByKey 同步导出。
        // 条目上限 = 4 类型 × 5 等级 = 20，规模小故全量常驻（不做循环复用）。
        private void RefreshSynthesis()
        {
            // 稳定排序：按类型枚举值、再按等级
            var keys = new List<(MergeElement type, int level)>(_merge.Inventory.Keys);
            keys.Sort((a, b) =>
            {
                int t = ((int)a.type).CompareTo((int)b.type);
                return t != 0 ? t : a.level.CompareTo(b.level);
            });

            // 补位滑动：刷新前先记录容器内各 token 旧位（含正在滑动中的实时位置）。
            var reflow = _synthContent != null ? LayoutReflowAnimator.GetOrAdd(_synthContent) : null;
            reflow?.CaptureBefore();

            var desired = new HashSet<(MergeElement type, int level)>(keys);

            // 消失键 → 销毁 token 并出 map（库存清空 / 该级被合并升走）。
            var toRemove = new List<(MergeElement type, int level)>();
            foreach (var kv in _synthByKey)
                if (!desired.Contains(kv.Key)) toRemove.Add(kv.Key);
            foreach (var key in toRemove)
            {
                var tk = _synthByKey[key];
                _synthByKey.Remove(key);
                if (tk != null && tk.gameObject != null) Object.Destroy(tk.gameObject);
            }

            // 新增键 → 建稳定 token（资源定位名 == 类名 "SynthTokenWidget"，CreateWidgetByType 走 AddressByFileName）。
            foreach (var key in keys)
            {
                if (_synthByKey.ContainsKey(key)) continue;
                var tk = CreateWidgetByType<SynthTokenWidget>(_synthContent);
                if (tk == null)
                {
                    Log.Error($"[MergeOrderWindow] SynthTokenWidget 加载失败（资源定位名 SynthTokenWidget），合成 token {key} 未创建。");
                    continue;
                }
                if (tk.rectTransform != null) tk.rectTransform.localScale = Vector3.one;
                _synthByKey[key] = tk;
            }

            // 按排序重建 live 列表 + SetData + 重排 sibling（HLG 按子节点序排布）。
            _synthTokens.Clear();
            int siblingIndex = 0;
            foreach (var key in keys)
            {
                if (!_synthByKey.TryGetValue(key, out var tk) || tk == null) continue;
                int count = _merge.Inventory[key];
                tk.SetData(key.type, MergeElementVisual.SpriteName(key.type, key.level), key.level, count);
                if (tk.rectTransform != null) tk.rectTransform.SetSiblingIndex(siblingIndex++);
                _synthTokens.Add(tk);
            }

            // 重排后执行补位滑动（留存 token 旧位→新位）。新建 token 无旧位记录，直接落到目标位。
            reflow?.AnimateReflow(SlideReflowDuration);
        }

        /// <summary>列表重排补位滑动时长（订单左滑 / 元素区增删补位共用，手感参数，交用户手测调）。</summary>
        private const float SlideReflowDuration = 0.22f;

        // ── 盲盒计数 + 开盒按钮态（设计 12 §五） ──
        private void RefreshBlindBox()
        {
            // ◈（BMP，LegacyRuntime 字体可渲染）替代 🔮（补充平面 emoji 在该字体下渲染不出，设计 §五允许 🔮 或 ◈）。
            m_text_BlindBox.text = $"◈ ×{_merge.BlindBoxCount}";
            // 顶部盲盒槽数字（GemIcon 对应）。
            if (m_text_GemNum != null) m_text_GemNum.text = $"{_merge.BlindBoxCount}";
            bool can = _merge.CanOpenBlindBox;
            m_btn_OpenBox.interactable = can;
            _openBoxBtnBg.color = can ? new Color32(0x7a, 0x4a, 0xb8, 0xFF) : new Color32(0x3a, 0x33, 0x44, 0xFF);
            _openBoxBtnLabel.color = can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF);
        }

        // ── 虔诚币计数（设计 13 §五）。数字走数值系统格式化:大数自动 K/M（设计 15 §3.5 示范接入 O3）──
        private void RefreshPiety()
        {
            m_text_Piety.text = $"✦ {NumericDisplay.Format(_merge.Piety)}";
            // 顶部虔诚币槽数字（CoinIcon 对应）。
            if (m_text_CoinNum != null) m_text_CoinNum.text = NumericDisplay.Format(_merge.Piety);
        }

        // ── 「神庙」按钮（m_btn_Temple，生成代码接线）：叠层打开 TempleWindow（不关本窗、不丢局），关闭后刷新虔诚币 ──
        private partial void OnClick_TempleBtn()
        {
            CancelClearToolArming(); // 开神庙叠层打断指定格模式
            GameModule.UI.ShowUIAsync<TempleWindow>((System.Action)RefreshPiety);
        }

        // ── 开盒（m_btn_OpenBox，生成代码接线，设计 12 §五）：扣 1 → 掷奖 → 发放 → 内联弹字 + 刷新计数/合成区/体力 ──
        private partial void OnClick_OpenBoxBtn()
        {
            if (!_merge.OpenBlindBox(out var reward)) return;

            BurstText.Spawn(transform, BlockLayout.DesignWidth / 2f, 763, OpenResultLabel(reward), 69,
                new Color32(0xc8, 0x9a, 0xff, 0xFF));

            RefreshSynthesis(); // 图案进了合成区
            RefreshEnergy();    // 可能加了体力
            RefreshBlindBox();  // 计数与按钮态
            RefreshClearTool(); // 体力可能变化，gate 态须刷新

            MarkAndFlushSave(); // 跨会话存档（设计 14 §3.4）：开盒改盲盒计数/灵力/体力元层 → 标脏 + 落盘
        }

        // ── 消除道具（设计 49 §3.1）：主动清一行一列、代价体力、只受体力门控 ──

        /// <summary>
        /// 消除道具按钮态：按钮始终可点击，体力门控只表现为图标染色（可用=白本色，体力不足=暗灰），arming 时高亮（亮橙）。
        /// 体力不足时按钮显灰但仍可点，点击落到 OnClick_ClearToolBtn 给出「体力不足」文字提示——不再用 interactable 拦点击
        /// （拦掉则点击事件不分发，玩家点灰按钮无任何反馈）。
        /// </summary>
        private void RefreshClearTool()
        {
            if (m_btn_ClearTool == null) return;
            bool can = _merge.CanUseClearTool;
            // 按钮始终可点击：体力门控由 OnClick_ClearToolBtn 内部判定（够则进 arming，不够则弹提示），不再 gate interactable。
            m_btn_ClearTool.interactable = true;
            // 图标染色门控：arming 高亮（亮橙）/ 可用（白本色）/ 体力不足显灰（暗灰，仅视觉提示「当前不可用」）。
            m_btn_ClearTool.image.color = _clearToolArming
                ? new Color32(0xff, 0xcc, 0x88, 0xFF)
                : (can ? Color.white : new Color32(0x66, 0x66, 0x66, 0xFF));
        }

        /// <summary>
        /// 点消除道具按钮：体力够则进「指定格」模式（亮按钮 + 提示 + 启用 overlay 等玩家点棋盘格）；
        /// 体力不足则弹「体力不足，等恢复」短提示（数秒后自动消失）不进 arming。再点一次按钮取消 arming（开关式）。
        /// 按钮无论体力是否充足都可点击（体力不足时显灰但仍触发本回调），故这里是体力门控的唯一判定点。
        /// </summary>
        private partial void OnClick_ClearToolBtn()
        {
            if (_clearToolArming) { CancelClearToolArming(); return; } // 开关：再点取消
            if (!_merge.CanUseClearTool)
            {
                // 体力不足：弹短提示（自动消失），不进 arming。
                ShowClearToolHint("体力不足，等恢复", autoHide: true);
                return;
            }
            _clearToolArming = true;
            if (m_img_ClearOverlay != null)
            {
                m_img_ClearOverlay.gameObject.SetActive(true);
                m_img_ClearOverlay.transform.SetAsLastSibling(); // 置顶拦截棋盘/槽点击
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

            // 二次 gate(防 arming 期间体力被其它路径耗低)：不够则取消、弹短提示（自动消失）。
            if (!_merge.CanUseClearTool) { CancelClearToolArming(); ShowClearToolHint("体力不足，等恢复", autoHide: true); return; }

            _merge.SpendClearToolCost();                  // 扣体力（已确认 CanUseClearTool）
            _state.ClearToolRowCol(_board, row, col);     // 清一行一列（同步 SaveArr / ElementArr / BinaryBoard）
            // 设计 49 §四 / §3.1：清一行一列不清空全盘、不触发全清判定——此处不调 ClearSettlement，
            // 全清奖只由正常消除清空棋盘触发，不被消除道具重复领取（B13）。

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
            if (!_clearToolArming && (m_img_ClearOverlay == null || !m_img_ClearOverlay.gameObject.activeSelf))
            {
                HideClearToolHint();
                return;
            }
            _clearToolArming = false;
            if (m_img_ClearOverlay != null) m_img_ClearOverlay.gameObject.SetActive(false);
            HideClearToolHint();
            RefreshClearTool();
        }

        /// <summary>
        /// 显示消除道具提示条。autoHide=true 时设倒计时，OnUpdate 数秒后自动隐藏（用于「体力不足」短提示）；
        /// autoHide=false（默认）则常驻直到玩家操作触发 HideClearToolHint（用于 arming 指令提示「点棋盘任一格…」）。
        /// </summary>
        private void ShowClearToolHint(string msg, bool autoHide = false)
        {
            if (m_img_ClearHintBg == null) return;
            m_text_ClearHint.text = msg;
            m_img_ClearHintBg.gameObject.SetActive(true);
            m_text_ClearHint.gameObject.SetActive(true);
            _clearHintHideTimer = autoHide ? ClearHintAutoHideSeconds : 0f;
        }

        private void HideClearToolHint()
        {
            _clearHintHideTimer = 0f;
            if (m_img_ClearHintBg == null) return;
            m_img_ClearHintBg.gameObject.SetActive(false);
            m_text_ClearHint.gameObject.SetActive(false);
        }

        /// <summary>开盒结果弹字文案（图案：「开出：◆ Lv3 ×1」；体力：「开出：⚡ +10」）。</summary>
        private static string OpenResultLabel(BlindBoxReward reward)
        {
            if (reward.IsEnergy) return $"开出：⚡ +{reward.EnergyGain}";
            if (reward.IsPattern)
                return $"开出：{MergeElementVisual.Glyph(reward.PatternType)} Lv{reward.PatternLevel} ×{reward.PatternCount}";
            return "开出：—";
        }

        // ── 交付编排（设计第二批 §A/§B）：点交付 → 锁交互 → count 个元素从元素区对应 (type,level) token 飞向订单卡图标
        //    → 全部飞达的聚合回调里才真正 Deliver(扣库存) + 刷新 + 庆祝 → 解锁。
        //    「飞达才扣库存」：飞行是纯表现，真实扣减仍是 _merge.Deliver(slot) 那一下，只是延到聚合回调执行。
        private void OnDeliverClicked(int slot)
        {
            if (_deliverFlying) return;      // 飞行进行中：忽略再次点交付（交互锁）
            CancelClearToolArming();         // 交付打断指定格模式
            if (!_merge.CanDeliver(slot)) return; // 不可交付：不启动飞行，直接返回

            var orders = _merge.ActiveOrders;
            if (orders == null || slot < 0 || slot >= orders.Length) return;
            var order = orders[slot];
            int count = order.Count;

            // 终点：订单卡的需求图标世界坐标 → transform 本地空间（与既有收集飞行同坐标换算）。
            var card = slot < _orderCards.Length ? _orderCards[slot] : null;
            var cardGlyph = card != null ? card.GlyphRect : null;
            var root = transform.GetComponent<RectTransform>();

            // 飞行不可行（卡 / 图标 / 根缺失）→ 退化为同步交付（不卡死，行为等价旧版）。
            if (card == null || cardGlyph == null || root == null || count <= 0)
            {
                FinishDeliver(slot);
                return;
            }

            Vector2 endLocal = transform.InverseTransformPoint(cardGlyph.position);

            // 起点：元素区里该订单 (type,level) 对应 token 的图标世界坐标；缺失则退化为从订单卡自身位置起飞（仍能交付）。
            Vector2 startLocal = endLocal;
            if (_synthByKey.TryGetValue((order.Type, order.Level), out var srcToken)
                && srcToken != null && srcToken.GlyphRect != null)
                startLocal = transform.InverseTransformPoint(srcToken.GlyphRect.position);

            _deliverFlying = true; // 置交互锁：飞行期间忽略再次交付 / 落子 / 订单轮询刷新

            float iconSize = BoardCellSize() * 0.7f; // 与棋盘元素图标同尺寸口径，飞行视觉连贯
            const float Stagger = 0.06f;             // count 个图标错开起飞
            string flySprite = MergeElementVisual.SpriteName(order.Type, order.Level);

            // 聚合计数器：count 个飞行各自到达 -1，归 0 时才真正 Deliver + 刷新 + 庆祝 + 解锁。
            int remaining = count;
            for (int i = 0; i < count; i++)
            {
                FlyToTargetFx.Spawn(root, startLocal, endLocal, flySprite, iconSize, i * Stagger,
                    () =>
                    {
                        remaining--;
                        if (remaining > 0) return;
                        // 全部飞达：真正交付（扣库存）+ 刷新 + 庆祝 + 解锁。
                        FinishDeliver(slot);
                        _deliverFlying = false;
                    });
            }
        }

        /// <summary>
        /// 交付落地（飞行全部到达后的聚合回调 / 退化同步路径共用）：真正 Deliver(扣库存) → TryRefreshIfAllDelivered → 刷新 → 庆祝。
        /// 防御：若此刻已不可交付（理论上飞行期间锁住不会发生），Deliver 返回 false 时安全返回、不卡死、不庆祝。
        /// 注意：本方法不动 _deliverFlying（由调用方在末尾解锁），保证退化同步路径与聚合回调路径都正确收尾。
        /// </summary>
        private void FinishDeliver(int slot)
        {
            if (!_merge.Deliver(slot)) return; // 已不可交付（防御）：安全返回，不刷新不庆祝
            // 交付后该槽置空（不补单）；若三槽全部交付完，立即整批补一批新订单并重置到时倒计时。
            _merge.TryRefreshIfAllDelivered(MergeMetaPersistence.NowUnixSec());
            RefreshEnergy();
            RefreshOrders();
            // ③ 交付庆祝：在订单卡位置放金色爆破 + 对常驻卡实例做 scale-punch
            {
                var card = slot < _orderCards.Length ? _orderCards[slot] : null;
                if (card != null && card.rectTransform != null)
                {
                    // 卡世界坐标 → transform 局部点 → 设计坐标（transform 为 DesignWidth×DesignHeight 居中overlay，
                    // 局部点即锚定位，与 BlockLayout.AnchoredToDesign 同坐标系）。金色爆破父层用 transform（不随订单刷新销毁）。
                    Vector2 local = transform.InverseTransformPoint(card.rectTransform.position);
                    Vector2 design = BlockLayout.AnchoredToDesign(local);
                    ClearBurstFx.Spawn(transform, design.x, design.y, new Color32(0xff, 0xe4, 0x44, 0xff));
                    card.Punch();
                }
            }
            RefreshSynthesis();
            RefreshBlindBox();
            RefreshPiety(); // 交付发虔诚币（设计 13 §3.1）
            RefreshClearTool(); // 体力随交付变化，按钮 gate 态须刷新

            MarkAndFlushSave(); // 跨会话存档（设计 14 §3.4）：交付改元层(含体力) → 标脏 + 落盘
            // 无尽模型（设计 49）：交付后该槽置空、不补单；全部交付完则上面 TryRefreshIfAllDelivered 整批补回。无通关终点、不触发任何结算面板。
        }

        /// <summary>
        /// 跨会话存档落盘（设计 14 §3.4）：元变更后标脏并异步落盘。调度策略为每次元动作结束即异步落盘（O5 默认，元动作频率低）。
        /// SaveAsync 内部经 Provider 写入，失败不阻断玩法；落盘后清脏。
        /// </summary>
        private void MarkAndFlushSave()
        {
            if (_merge == null) return;
            _merge.RequestSave();
            FlushSaveIfDirty();
        }

        /// <summary>脏位为真则异步落盘并清脏（退出 / 暂停 / 元动作共用）。Forget 异步写，不阻塞主线程。</summary>
        private void FlushSaveIfDirty()
        {
            if (_merge == null || !_merge.IsSaveDirty) return;
            var dto = _merge.ExportMeta();
            _merge.ClearSaveDirty();
            MergeMetaPersistence.SaveAsync(dto).Forget();

            // 局内态续存（2026-06-22 决定）：与元层同时机落盘，记录上次中断的对局现场（盘面/手牌/合成区/订单/连消）。
            // 仅在 merge-order 现场有效时写（退出按钮已先 ExitMergeOrder 则跳过，避免空盘覆盖有效快照）。
            if (_state != null && _state.MergeOrderMode && _state.MergeState != null)
            {
                var ingame = new MergeIngameSave { version = MergeIngamePersistence.CurrentVersion };
                _state.ExportIngame(ingame);
                MergeIngamePersistence.SaveAsync(ingame).Forget();
            }
        }

        // ── 渲染棋盘（方块色 / 皮肤，设计 50 §二）──
        // 两态都贴图：彩色态按方块类型贴 default_skin 各自纹理（blocks_main_<编号>，B1）；
        // 单色态全盘统一贴当前单色 sprite（blocks_skin_atlas_<编号>，B2/B3）。
        private void RenderBoard()
        {
            bool mono = _merge != null && _merge.Skin.IsMono;
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
                            // 初始纯色作 sprite 异步加载到位前的占位，避免闪空（到位后 ApplyCellSkin 切白 tint + 贴图）。
                            // 自适应：格尺寸与位置按 BoardLayer.rect 现算（BoardCellSize / BoardCellLocalPos），随 BoardLayer 缩放。
                            var placeholder = BlockLayout.ColorOf((BlockColor)colorIdx);
                            float cell = BoardCellSize();
                            // CreateImage 内部按设计坐标摆位（anchor/pivot 已居中），随即用 BoardLayer 本地坐标覆写 anchoredPosition。
                            // 单格视觉边长 = cell - BoardCellGap（内缩间隙走 BlockLayout.BoardCellGap 单一事实源，与候选块同口径）。
                            float boardCellVisual = cell - BlockLayout.BoardCellGap;
                            img = UGuiFactory.CreateImage(m_rect_BoardLayer, $"cell_{r}_{c}", 0, 0,
                                boardCellVisual, boardCellVisual, placeholder);
                            img.rectTransform.anchoredPosition = BoardCellLocalPos(c, r);
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
            img.color = Color.white;                 // 两态 sprite 均自带颜色，tint 用白避免叠色
            // 单色态全盘同图；彩色态按类型取 default_skin 纹理（设计 50 §二）。
            img.SetSprite(mono ? monoLoc : BlockSkinCatalog.ColoredSpriteName(colorIdx));
        }

        // ── 渲染元素 overlay（clip 图标 sprite） ──
        // 元素图标 Image：白 tint 显本色、raycastTarget=false（不挡棋盘点击），尺寸比格略小留边（自适应格尺寸*0.7）。
        // None 不建 Image / 已建则销毁置 null。
        // 父层用 m_rect_BoardLayer（与棋盘格同父同坐标系），不用 m_rect_ElemLayer：ElemLayer 与 BoardLayer 不同父不同位，
        // 挂 ElemLayer 会让元素图标与棋盘格错位。挂 BoardLayer + BoardCellLocalPos 保证「元素 = 所在格视觉位置」。
        private void RenderElements()
        {
            float iconSize = BoardCellSize() * 0.7f;
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
                            existing.SetSprite(MergeElementVisual.SpriteName(el, 1), setNativeSize:true); // 棋盘元素 = Lv1 原料（无等级层），取 Lv1 图
                            existing.transform.SetAsLastSibling(); // 元素图标与棋盘格同父，置顶避免被新建 cell 盖住
                        }
                        else
                        {
                            // 父层 m_rect_BoardLayer + BoardLayer 本地坐标（与棋盘格同源），覆写 CreateImage 的设计坐标摆位。
                            var icon = UGuiFactory.CreateImage(m_rect_BoardLayer, $"elem_{r}_{c}", 0, 0,
                                iconSize, iconSize, Color.white);
                            icon.rectTransform.anchoredPosition = BoardCellLocalPos(c, r);
                            icon.raycastTarget = false;
                            icon.SetSprite(MergeElementVisual.SpriteName(el, 1), setNativeSize:true); // 棋盘元素 = Lv1 原料，取 Lv1 图
                            icon.transform.SetAsLastSibling(); // 同上：置顶于棋盘格之上
                            _elemCells[r, c] = icon;
                        }
                    }
                }
            }
        }

        // ── 渲染候选槽（与 GameWindow 同构） ──
        // 候选块换皮跟随棋盘格相同的皮肤状态（候选预览 = 落到棋盘后的样子，设计 50 §二）：
        // 单色态全部候选块统一贴当前单色 sprite；彩色态按方块类型贴 default_skin 各自纹理。
        private void RenderSlots()
        {
            bool mono = _merge != null && _merge.Skin.IsMono;
            string monoLoc = mono ? BlockSkinCatalog.SpriteName(_merge.Skin.MonoId) : null;

            // 自适应棋盘格尺寸取一次，供本帧所有候选块复用：拖起放大倍数与单格 base 尺寸同源，避免两处重算不一致。
            float boardCell = BoardCellSize();
            // 候选块单格 base 尺寸：本地空间下放大 boardCell/SlotCell 倍后正好 = boardCell - BoardCellGap（与棋盘格本地视觉边长相等）。
            // base = (boardCell - BoardCellGap) / (boardCell/SlotCell) = (boardCell - BoardCellGap) * SlotCell / boardCell。
            float slotCellBase = (boardCell - BlockLayout.BoardCellGap) * BlockLayout.SlotCell / boardCell;

            // 拖起放大倍数需补偿两条分支的世界缩放差（设计基线：拖起块屏幕单格 == 棋盘屏幕单格）。
            // 候选块挂 m_rect_SlotLayer 分支，棋盘格挂 m_rect_BoardLayer 分支；两分支父链 localScale 不同
            // （BoardLayer 经父节点带额外缩放），故各自 lossyScale 不等。OverrideScale 只乘在 SlotLayer 局部缩放上，
            // 补不了世界缩放差——拖起块屏幕尺寸 = 本地尺寸 × SlotLayer.lossyScale，棋盘格 = 本地尺寸 × BoardLayer.lossyScale。
            // 故把两分支 lossyScale 比值乘进 OverrideScale，使「本地尺寸 × OverrideScale × SlotLossy = 棋盘格本地 × BoardLossy」成立。
            // 取运行时 lossyScale（OnCreate 后布局已结算、固定尺寸锚点下稳定），prefab 缩放变化时自纠正。
            float slotLossy = m_rect_SlotLayer.lossyScale.x;
            float boardLossy = m_rect_BoardLayer.lossyScale.x;
            float worldScaleRatio = Mathf.Approximately(slotLossy, 0f) ? 1f : boardLossy / slotLossy;
            float overrideScale = (boardCell / BlockLayout.SlotCell) * worldScaleRatio;

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

                var container = UGuiFactory.CreateNode(m_rect_SlotLayer, $"slot_{i}");
                float totalW = shape.Width * BlockLayout.SlotCell;
                float totalH = shape.Height * BlockLayout.SlotCell;
                // 候选块容器相对父层 m_rect_SlotLayer（底部紫色待选区背景内）本地居中定位，不用绝对设计坐标。
                // 父层中心不在屏幕中心，故旧 PlaceByDesignCenter（锚屏幕中心 + DesignToAnchored）会把容器甩出待选区。
                // 横向按槽间距铺开（中槽 i=1 居中、左右槽 ±SlotSpacing），纵向居中于槽层；
                // 容器尺寸取单槽命中区（SlotZoneWidth≈298 / SlotZoneHeight=360），避免 3 个容器占满整层相互重叠。
                container.anchorMin = container.anchorMax = new Vector2(0.5f, 0.5f);
                container.pivot = new Vector2(0.5f, 0.5f);
                container.sizeDelta = new Vector2(BlockLayout.SlotZoneWidth, BlockLayout.SlotZoneHeight);
                container.anchoredPosition = new Vector2((i - 1) * BlockLayout.SlotSpacing, 0f);

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
                        crt.sizeDelta = new Vector2(slotCellBase, slotCellBase);
                        crt.anchoredPosition = new Vector2(offX + c * BlockLayout.SlotCell, offY - r * BlockLayout.SlotCell);
                        var ci = cell.GetComponent<Image>();
                        // 候选块换皮：白 tint 显本色；彩色态按方块类型 (int)piece.Color 贴 default_skin，单色态统一贴当前单色 sprite。
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
                            // 不可用 setNativeSize：Image.SetNativeSize 会把 anchorMax 收回到 anchorMin（左下角）并改写 sizeDelta 为
                            // sprite 原生像素尺寸，覆盖上面的拉伸充填，导致元素跑到格子左下角且尺寸过大（待选区位置/大小错位的根因）。
                            // 保持拉伸充填（offsets 全 0）= 元素正好铺满候选格、居中，与设计「铺满格子」一致。
                            gimg.SetSprite(MergeElementVisual.SpriteName(el, 1)); // 候选块元素 = Lv1 原料，取 Lv1 图
                        }
                        cellIdx++;
                    }
                }

                var dragger = container.gameObject.AddComponent<BlockPieceDragger>();
                dragger.SlotIndex = i;
                // 拖起放大倍数 = (boardCell/SlotCell) × 两分支世界缩放比，保证拖到棋盘上的块与棋盘格屏幕等大。
                // 本帧统一取值（overrideScale 已含世界缩放补偿），三槽共用，避免逐槽重算不一致。
                dragger.OverrideScale = overrideScale;
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
            // 交付飞行进行中：忽略落子拖拽（不进入拖拽态、不显示 ghost）。配合 OnPieceEnd 的锁，飞行期间落子整体无效。
            if (_deliverFlying) { _draggingShapeId = -1; return; }
            CancelClearToolArming(); // 拖拽落子打断指定格模式（玩家改主意去落子）
            var piece = _state.OperaArr[slotIdx];
            _draggingShapeId = piece?.ShapeId ?? -1;
            _draggingColor = piece?.Color ?? default; // 消除预览按拖拽块色着色（可消除行列与拖拽块同色）
        }

        private void OnPieceDrag(int slotIdx, Vector2 containerAnchored)
        {
            // ghost 落点同样按容器世界坐标换算（见 ComputeGridPos），不用原始 anchoredPosition（父层偏移会错算）。
            var container = slotIdx >= 0 && slotIdx < _slotContainers.Length ? _slotContainers[slotIdx] : null;
            if (container != null) UpdateGhost(container);
        }

        private void OnPieceEnd(int slotIdx)
        {
            ClearGhost();
            int shapeId = _draggingShapeId;
            _draggingShapeId = -1;

            // 交付飞行进行中：忽略落子结算（否则飞行途中落子消除会改库存，破坏「飞达才扣库存」语义）。
            // 候选块归位、不落子、不弹提示（玩家短暂等飞行结束即可再落）。
            if (_deliverFlying)
            {
                _slotContainers[slotIdx]?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
                return;
            }

            var piece = _state.OperaArr[slotIdx];
            var shape = shapeId > 0 ? BlockShapeMap.Get(shapeId) : null;
            var container = _slotContainers[slotIdx];

            if (piece != null && shape != null && container != null)
            {
                var (col, row) = ComputeGridPos(container, shape);
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
                BurstText.Spawn(transform, BlockLayout.DesignWidth / 2f, 950, reason, 63,
                    new Color32(0xff, 0x99, 0x66, 0xFF));
            }

            container?.GetComponent<BlockPieceDragger>()?.ResetToOrigin();
        }

        /// <summary>
        /// 落子 → 扣体力 → 消除返体力 + 元素入合成区（自动升级）→ 结算（连消 / 多消 / 全清 / 女神 / 盲盒 / 皮肤）→ 刷新 → 落盘。
        /// 无尽模型（设计 49）：无通关、无 GameOver——卡死与体力归零都不结束、不弹面板，窗口保持可交互
        /// （卡死用消除道具，体力归零靠时基恢复 + 订单补，两条兜底见设计 49 §三）。
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

            // ② 落子格 scale-punch：迭代被落格、对已存在的 cell Image 添加 ScalePunch
            for (int pr = 0; pr < shape.Height; pr++)
            {
                for (int pc = 0; pc < shape.Width; pc++)
                {
                    if (((shape.Shape[pr] >> (shape.Width - pc - 1)) & 1) == 0) continue;
                    int gr = row + pr, gc = col + pc;
                    if (gr >= 0 && gr < N && gc >= 0 && gc < N && _cellImages[gr, gc] != null)
                        _cellImages[gr, gc].gameObject.AddComponent<ScalePunch>();
                }
            }

            // 本手结算是否改了元层(全清推女神 / 全清或连消阈值发盲盒);为真则落子后须标脏落盘(设计 14 §3.4)。
            bool metaChangedBySettle = false;

            // 收集飞行动画的起点列表（被消元素「类型 + 棋盘格本地坐标」）。在 if 块内捕获（须早于 HarvestClearedElements），
            // 飞行在 RefreshSynthesis 之后才发（token 池稳定），故提到 if 外作用域。无消除时保持 null。
            List<(MergeElement type, Vector2 boardLocal)> flySources = null;

            // 消除 + 返体力 + 元素入合成区
            var clear = _board.CanClearRowCols(true);
            int lines = clear.Rows.Count + clear.Cols.Count;
            if (lines > 0)
            {
                // 收集飞行动画（纯表现层，设计无关）：HarvestClearedElements 会清空 ElementArr，故须在它之前
                // 捕获每个被消元素的「类型 + 棋盘格本地坐标」作飞行起点（去重逻辑与 SpawnClearBurstFx 同，行列交叉格只算一次）。
                flySources = CaptureClearedElementSources(clear.Rows, clear.Cols);

                var cleared = new List<MergeElement>();
                _state.HarvestClearedElements(clear.Rows, clear.Cols, cleared); // 清 overlay + 输出被清元素
                SpawnClearBurstFx(clear.Rows, clear.Cols);                      // 在 SaveArr 被清前读色，逐被消格放爆破粒子
                int clearedCells = _state.ClearRowsAndCols(clear.Rows, clear.Cols); // 清方块色，得被清格数
                foreach (var el in cleared) _merge.IngestElement(el);           // 逐个 Lv1 入合成区（自动升级）
                _merge.RefundEnergy(lines);                                     // 返还体力（受软上限）

                // 连消 / 多消 / 全清结算（设计 11 §5.5 固定流水线）：
                // 连消倍率只乘显示分；元素产出用未乘连消的基础分；多消里程碑直发 Lv2 / Lv3；
                // 全清触发标志为真时发 1 Lv3 + 推进女神（不可连续 2 次）。里程碑 / 全清产物归当前订单所需类型之一。
                var milestoneType = PickMilestoneType();
                var settle = ClearSettlement.Settle(_merge, lines, clearedCells, _board.IsEmpty(), milestoneType);
                _state.Combo = settle.ComboChain >= 2 ? settle.ComboChain : 0; // 镜像到视觉连击（≥2 才显示）

                // 元层判定（设计 14 §3.4）：全清推女神 / 全清或连消阈值发盲盒，都改进盘字段
                // （goddessLevel / goddessRating / blindBoxCount）。AllClearRewarded 隐含女神 + 盲盒；
                // GoddessLeveledUp 与 BlindBoxGained 并列，确保无后续交付 / 开盒时这一手的女神 / 盲盒进度也落盘。
                metaChangedBySettle = settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained > 0;

                // 方块皮肤切换（设计 50 §三）：全清发奖时触发换皮——彩色→单色（首次）/ 单色换一张排除当前（后续）。
                // 纯视觉附加，不改上方全清结算（设计 50 §三 规则 5 / A9）。皮肤态进元层存档（设计 50 §六），
                // 故换皮即标元层脏；metaChangedBySettle 已为 true（AllClearRewarded 蕴含），落盘随之发生。
                if (settle.AllClearRewarded)
                    _merge.Skin.OnAllClear(BlockSkinCatalog.MonoIds);

                RenderBoard();
                // 全清换皮后，待选区残留候选块须与棋盘同步换皮（设计 50，候选预览 = 落盘后样子）。
                // 下方仅在三槽全空时才补块 + RenderSlots；若全清时尚有未落候选块，需在此显式重渲染让其跟随新皮肤态。
                if (settle.AllClearRewarded)
                    RenderSlots();

                if (settle.AllClearRewarded)
                    BurstText.Spawn(transform, BlockLayout.DesignWidth / 2f, 677, "PERFECT!", 92, new Color32(0xff, 0xe4, 0x4a, 0xFF));
                else if (lines >= MergeOrderConfig.MultiClearMilestoneMinLines)
                    BurstText.Spawn(transform, BlockLayout.DesignWidth / 2f, 677, settle.MultiLabel, 81, new Color32(0x55, 0xdd, 0xaa, 0xFF));
                else if (settle.ComboChain >= 2)
                {
                    int comboFs = Mathf.Min(81 + (settle.ComboChain - 2) * 8, 116);
                    BurstText.Spawn(transform, BlockLayout.DesignWidth / 2f, 677, $"COMBO x{settle.ComboChain}", comboFs, new Color32(0xff, 0x77, 0xbb, 0xFF));
                }

                // 获得盲盒（连消阈值 / 全清解锁）弹「+N ◈」（设计 12 §五）
                if (settle.BlindBoxGained > 0)
                    BurstText.Spawn(transform, BlockLayout.DesignWidth / 2f, 850, $"+{settle.BlindBoxGained} ◈", 72,
                        new Color32(0xc8, 0x9a, 0xff, 0xFF));
            }
            else
            {
                ClearSettlement.Settle(_merge, 0, 0, false, MergeElement.None); // 连消链断回 1 + 重置全清触发标志
                _state.Combo = 0;
            }

            RefreshEnergy();
            RefreshOrders();
            RefreshSynthesis();
            // 收集飞行动画（纯表现层）：须在 RefreshSynthesis 之后发——新类型会新建 token、升级会改等级，
            // token 池此刻才稳定，飞行落点（按类型匹配）才能正确定位。数字已在 RefreshSynthesis 立即更新，飞行只叠加视觉。
            SpawnCollectFly(flySources);
            RefreshBlindBox();
            RefreshPiety(); // 女神升档可能改长期主线展示态(保险刷新)
            RefreshClearTool(); // 落子扣体力 → gate 态须刷新

            // 跨会话存档（设计 14 §3.4）：落子必扣体力（体力已进盘，设计 14 §3.7），且本手可能改其它元层
            // （女神升档 / 盲盒 / 皮肤），故每次落子结算后标脏 + 落盘。
            MarkAndFlushSave();

            // 补充候选块（全空才补）
            bool allEmpty = true;
            for (int i = 0; i < 3; i++) if (_state.OperaArr[i] != null) { allEmpty = false; break; }
            if (allEmpty)
            {
                _state.RefillPieces(_board);
                RenderSlots();
            }

            // 无尽模型（设计 49 §一 / §二）：无通关、无 GameOver，故此处无结束判定。
            // 卡死（手持块无处可放）：玩法窗保持可交互，玩家用消除道具清一行一列（设计 49 §3.1）。
            // 体力归零（付不起落子）：等时基恢复 / 订单补 / 用消除道具（设计 49 §3.2）。
            // 两条兜底保证任何状态在有限时间内可恢复操作（设计 49 §3.3）。
        }

        /// <summary>
        /// 对被消的整行/整列里每个已占格放一发消除爆破粒子（设计配方 clear_burst）。
        /// 必须在 <see cref="BlockGameState.ClearRowsAndCols"/> 清 SaveArr 之前调用——颜色从 SaveArr 读。
        /// 碎块层染该格方块色（colorIdx → BlockLayout.ColorOf）；行列交叉格只放一发（去重）。
        /// 粒子挂在棋盘格同层 <c>m_rect_BoardLayer</c>、同坐标系（自适应格中心 BoardCellLocalPos），与 cell Image 对位。
        /// </summary>
        private void SpawnClearBurstFx(IList<int> rows, IList<int> cols)
        {
            if (m_rect_BoardLayer == null || _state?.SaveArr == null) return;

            bool[,] done = new bool[N, N]; // 去重：行 pass 与列 pass 的交叉格只放一发

            void SpawnAt(int r, int c)
            {
                if (r < 0 || r >= N || c < 0 || c >= N) return;
                if (done[r, c]) return;
                int colorIdx = _state.SaveArr[r][c];
                if (colorIdx < 0) return;                 // 空格不放（与 ClearRowsAndCols 同口径：只清/放已占格）
                done[r, c] = true;
                // 自适应格中心（BoardLayer 本地坐标），与棋盘格渲染同源，与 cell Image 精确对位。
                var local = BoardCellLocalPos(c, r);      // 注意 (col,row) 顺序：col=c、row=r
                var color = BlockLayout.ColorOf((BlockColor)colorIdx);
                ClearBurstFx.SpawnAtLocal(m_rect_BoardLayer, local, color);
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

        // ── 收集飞行动画（纯表现层，fly-to-target）──────────────────────────────
        // 被消元素从棋盘格弹起、飞向合成区对应类型的图标落点，到达让目标 punch。不改任何经济/数值。
        // 与 SpawnClearBurstFx 同坐标系口径：起点用棋盘格 BoardLayer 本地坐标；飞行父层统一用 transform。

        /// <summary>
        /// 在 HarvestClearedElements 清 ElementArr 之前，捕获被消行/列上每个已占元素格的「类型 + 棋盘格 BoardLayer 本地坐标」。
        /// 去重与 SpawnClearBurstFx 一致：行列交叉格只算一次。无元素时返回 null。
        /// </summary>
        private List<(MergeElement type, Vector2 boardLocal)> CaptureClearedElementSources(IList<int> rows, IList<int> cols)
        {
            var arr = _state?.ElementArr;
            if (arr == null) return null;

            bool[,] done = new bool[N, N];
            List<(MergeElement, Vector2)> list = null;

            void CaptureAt(int r, int c)
            {
                if (r < 0 || r >= N || c < 0 || c >= N) return;
                if (done[r, c]) return;
                done[r, c] = true;
                var el = arr[r][c];
                if (el == MergeElement.None) return;
                // 棋盘格中心在 BoardLayer 本地坐标（与渲染同源，注意 (col,row) 顺序：col=c、row=r）。
                (list ??= new List<(MergeElement, Vector2)>()).Add((el, BoardCellLocalPos(c, r)));
            }

            if (rows != null)
                for (int i = 0; i < rows.Count; i++)
                    for (int c = 0; c < N; c++) CaptureAt(rows[i], c);
            if (cols != null)
                for (int i = 0; i < cols.Count; i++)
                    for (int r = 0; r < N; r++) CaptureAt(r, cols[i]);
            return list;
        }

        /// <summary>
        /// 为每个被消元素生成一个飞行图标：起点=棋盘格、终点=合成区该类型 token 的图标，错开起飞时间。
        /// 必须在 RefreshSynthesis 之后调用（token 池稳定）。落点按元素类型匹配（升级后等级变、类型不变），
        /// 同类型多等级 token 取第一个（RefreshSynthesis 已按类型→等级排序，即该类型最低等级）。
        /// 两端世界坐标都转到 transform 本地空间再插值，避免父层偏移错算（同交付庆祝爆破的坐标换算）。
        /// </summary>
        private void SpawnCollectFly(List<(MergeElement type, Vector2 boardLocal)> sources)
        {
            if (sources == null || sources.Count == 0) return;
            if (transform == null || m_rect_BoardLayer == null) return;

            // 落点取自合成区 token 的世界坐标，而 token 池由 RefreshSynthesis 同帧新建/重排、HorizontalLayoutGroup
            // 当帧尚未布局，新建 token（尤其全新类型首次出现）的 position 仍是默认值。先强制立即布局，确保落点准确。
            if (m_rect_SynthLayer != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(m_rect_SynthLayer);

            // 类型 → token 映射：同类型取首个（最低等级）。token 池已由 RefreshSynthesis 按类型→等级排序填好。
            var typeToToken = new Dictionary<MergeElement, SynthTokenWidget>();
            foreach (var token in _synthTokens)
            {
                if (token == null) continue;
                var glyph = token.GlyphRect;
                if (glyph == null) continue;
                var type = token.ElementType;
                if (type == MergeElement.None) continue;
                if (!typeToToken.ContainsKey(type)) typeToToken[type] = token;
            }

            // 渐显收集：本次飞行带来的「增量」须在图标飞达后才在元素区出现，飞行前不参与本次飞行的已有存量保持显示。
            // 做法（每个落点 token）：
            //   shown(飞行前显示数) = DisplayCount(库存真实值) - incoming(飞向它的图标数)；
            //   每个图标飞达 → shown+1（数字逐个长上去）；最后一个飞达 → 恢复库存真实值 + punch。
            // shown<=0（该 token 飞行前无存量、本次飞行全新产生）→ 飞行前隐藏内容，首个飞达起才显示并递增。
            // 仅改文字显示与显隐，DisplayCount(库存真实值)恒不变；飞达后必回到真实值，数字不会因延迟显示而最终错/漏。
            var flyState = new Dictionary<SynthTokenWidget, (int shown, int remaining)>();
            foreach (var (type, _) in sources)
            {
                if (type == MergeElement.None) continue;
                if (!typeToToken.TryGetValue(type, out var tk) || tk == null) continue;
                if (flyState.TryGetValue(tk, out var st)) flyState[tk] = (st.shown, st.remaining + 1);
                else flyState[tk] = (0, 1);
            }
            // 算飞行前显示数并落到「飞行前」态（已有存量保持显示、本次增量先不显示）。
            foreach (var tk in new List<SynthTokenWidget>(flyState.Keys))
            {
                int incoming = flyState[tk].remaining;
                int preShown = tk.DisplayCount - incoming; // 飞行前数量 = 真实值 - 本次飞入增量
                flyState[tk] = (preShown, incoming);
                tk.SetFlyingShownCount(preShown);
            }

            float iconSize = BoardCellSize() * 0.7f; // 与棋盘元素图标同尺寸口径，飞行视觉连贯
            const float Stagger = 0.05f;             // 多图标错开起飞，增强层次

            int idx = 0;
            foreach (var (type, boardLocal) in sources)
            {
                if (type == MergeElement.None) continue;
                if (!typeToToken.TryGetValue(type, out var token) || token == null) continue;
                var glyph = token.GlyphRect;
                if (glyph == null) continue;

                // 起点：BoardLayer 本地 → 世界 → Content 本地。
                Vector3 startWorld = m_rect_BoardLayer.TransformPoint(boardLocal);
                Vector2 startLocal = transform.InverseTransformPoint(startWorld);
                // 终点：token 图标世界坐标 → Content 本地。
                Vector2 endLocal = transform.InverseTransformPoint(glyph.position);

                var target = token;
                FlyToTargetFx.Spawn(transform.GetComponent<RectTransform>(), startLocal, endLocal,
                    MergeElementVisual.SpriteName(type, 1), iconSize, idx * Stagger, // 飞行的是 Lv1 原料，取 Lv1 图
                    () =>
                    {
                        if (target == null) return;
                        // 该 token 接收的飞行图标逐个到达：显示数 +1（增量逐个长上来）；
                        // 最后一个到达时恢复库存真实值（兜底防中间增量与级联合并不一致）+ punch（汇入点亮）。
                        if (!flyState.TryGetValue(target, out var st)) return;
                        int shown = st.shown + 1;
                        int remaining = st.remaining - 1;
                        if (remaining <= 0)
                        {
                            flyState.Remove(target);
                            target.RestoreDisplayCount();
                            target.PunchGlyph();
                        }
                        else
                        {
                            flyState[target] = (shown, remaining);
                            target.SetFlyingShownCount(shown);
                        }
                    });
                idx++;
            }
        }

        /// <summary>
        /// 多消里程碑/全清直发图案的归属类型：取当前订单所需类型之一（需求拉动，避免产无关图案）。
        /// 无所需类型时回退 None（ClearSettlement 内再兜底 Star）。
        /// </summary>
        private MergeElement PickMilestoneType()
        {
            var needed = _merge.NeededTypes();
            return needed.Count > 0 ? needed[0] : MergeElement.None;
        }

        // 无尽模型（设计 49）：本窗无通关结算窗 / GameOver 面板，卡死与体力归零都不结束、玩法窗持续可交互。
        // 「累计游戏 N 局」活动（设计 47/48 AccumulatePlayCount）依赖「局」终点，本窗不触发。
        // MergeOrderWinWindow / GameOverWindow 不被本窗引用（Classic GameWindow 仍用 GameOverWindow）。

        // ── ghost 落点高亮（与 GameWindow 同构） ──
        private void UpdateGhost(RectTransform container)
        {
            ClearGhost();
            if (_draggingShapeId < 0) return;
            var shape = BlockShapeMap.Get(_draggingShapeId);
            if (shape == null) return;

            var (col, row) = ComputeGridPos(container, shape);
            if (col + shape.Width <= 0 || row + shape.Height <= 0) return;
            if (col >= N || row >= N) return;

            bool canPlace = col >= 0 && row >= 0 && col + shape.Width <= N && row + shape.Height <= N
                && _board.CanPutBlock(_draggingShapeId, new Vec2Int(col, row));

            if (!canPlace && !GameConfigBB.ShowInvalidGhost) return;

            var color = canPlace ? BlockLayout.GhostOkColor : BlockLayout.GhostBadColor;

            // ghost 池挂在 m_rect_BoardLayer 下，用 BoardCellLocalPos 定位 + 自适应格尺寸，
            // 与棋盘格渲染同源，保证「ghost 高亮格 = 棋盘格视觉位置」。
            float cell = BoardCellSize();
            var ghostSize = new Vector2(cell - 8, cell - 8);
            for (int r = 0; r < shape.Height; r++)
            {
                for (int c = 0; c < shape.Width; c++)
                {
                    if (((shape.Shape[r] >> (shape.Width - c - 1)) & 1) == 0) continue;
                    int gc = col + c, gr = row + r;
                    if (gc < 0 || gc >= N || gr < 0 || gr >= N) continue;
                    if (_ghostUsed >= _ghostPool.Length) break;
                    var img = _ghostPool[_ghostUsed++];
                    img.rectTransform.sizeDelta = ghostSize;
                    img.rectTransform.anchoredPosition = BoardCellLocalPos(gc, gr);
                    img.color = color;
                    img.gameObject.SetActive(true);
                }
            }

            // 消除预览发光（仅可放时）：克隆棋盘模拟落子（绝不改真实 _board / _state），
            // 判出落子后会满、将被消除的整行整列，对其整行整列全部格叠加暖金发光。
            if (canPlace) ShowClearPreviewGlow(col, row);
        }

        /// <summary>
        /// 落子消除预览：克隆当前棋盘 + 模拟落子（只读，绝不触碰真实 _board / _state / 存档），
        /// 算出会满的整行整列，对每条满行铺一条横贯整行的长条、每条满列铺一条纵贯整列的长条，
        /// 由 UI/GlowCell shader 在带边缘画绿色描边辉光。走 _glowPool 线条池，本方法只 SetActive + 定位 + 选材质，无每帧分配。
        /// 行列交叉处两条线条加色叠加，自然，无需去重。
        /// </summary>
        private void ShowClearPreviewGlow(int col, int row)
        {
            if (_draggingShapeId < 0) return;

            // 克隆模拟（只读预览）：sim 是 _board 的拷贝，PutBlock 只改 sim，CanClearRowCols(false) 不消除、不改任何状态。
            var sim = _board.Clone();
            sim.PutBlock(_draggingShapeId, new Vec2Int(col, row));
            var preview = sim.CanClearRowCols(false);
            if (preview.Rows.Count == 0 && preview.Cols.Count == 0) return; // 没有任何满行满列 → 不发光

            float cell = BoardCellSize();
            float boardSpan = N * cell;               // 整棋盘宽 / 高（线条长向铺满）
            float bandThick = cell * BandScale;       // 线条短向（厚度），比真实行/列厚，留发散余量
            // 棋盘中心（行/列长条的长向居中点）：col 0..N-1 / row 0..N-1 中点恒为 BoardLayer 中心 (0,0)。
            float boardCenterX = (BoardCellLocalPos(0, 0).x + BoardCellLocalPos(N - 1, 0).x) * 0.5f;
            float boardCenterY = (BoardCellLocalPos(0, 0).y + BoardCellLocalPos(0, N - 1).y) * 0.5f;

            // 可消除行列与拖拽块同色（用户反馈）：取拖拽块方块色为预览基色（RGB），沿用原填充/辉光各自 alpha。
            // 拖拽块色 = _draggingColor（OnPieceBegin 捕获）。彩色态显本色，使「可消除方块」视觉上与「拖动方块」一致。
            Color pieceRgb = BlockLayout.ColorOf(_draggingColor);
            Color fillColor = new Color(pieceRgb.r, pieceRgb.g, pieceRgb.b, BlockLayout.ClearPreviewFillColor.a);
            Color glowColor = new Color(pieceRgb.r, pieceRgb.g, pieceRgb.b, BlockLayout.ClearPreviewGlowColor.a);

            // 取一条池线条，赋材质 + 定位（长条横贯整行或纵贯整列），激活并置顶。
            void ShowBand(Material mat, Vector2 size, Vector2 pos)
            {
                if (_glowUsed >= _glowPool.Length) return;
                var img = _glowPool[_glowUsed];
                var pulse = _glowPulses[_glowUsed];
                _glowUsed++;
                img.material = mat;
                img.rectTransform.sizeDelta = size;
                img.rectTransform.anchoredPosition = pos;
                img.transform.SetAsLastSibling();                     // 盖在方块格 / ghost / 填充之上
                if (pulse != null) { pulse.BaseColor = glowColor; pulse.Restart(_glowUsed * 0.06f); } // 呼吸基色 = 拖拽块色，错相位
                else img.color = glowColor;
                img.gameObject.SetActive(true);
            }

            // 取一张填充图，定位为正好覆盖该行 / 列真实区域（不放大、不外溢到邻行），激活并置顶。
            // 先于边缘辉光 SetAsLastSibling，使其落在方块格之上、边缘辉光之下（边缘辉光随后置顶）。
            void ShowFill(Vector2 size, Vector2 pos)
            {
                if (_fillUsed >= _fillPool.Length) return;
                var img = _fillPool[_fillUsed];
                _fillUsed++;
                img.rectTransform.sizeDelta = size;
                img.rectTransform.anchoredPosition = pos;
                img.color = fillColor;                                // 覆盖在可消除行列上 → 这些方块呈拖拽块色
                img.transform.SetAsLastSibling();                     // 盖在方块格 / ghost 之上（边缘辉光后续再置顶到其上）
                img.gameObject.SetActive(true);
            }

            // 先铺所有填充（盖在方块格之上）。填充用真实行 / 列尺寸（宽 N*cell × 高 cell，或宽 cell × 高 N*cell），
            // 正好盖住方块、不外溢，与边缘长条同一行 / 列中心。
            for (int i = 0; i < preview.Rows.Count; i++)
            {
                int r = preview.Rows[i];
                float y = BoardCellLocalPos(0, r).y;
                ShowFill(new Vector2(boardSpan, cell), new Vector2(boardCenterX, y));
            }
            for (int i = 0; i < preview.Cols.Count; i++)
            {
                int c = preview.Cols[i];
                float x = BoardCellLocalPos(c, 0).x;
                ShowFill(new Vector2(cell, boardSpan), new Vector2(x, boardCenterY));
            }

            // 再铺边缘辉光长条（每条 SetAsLastSibling 置顶 → 落在填充之上，维持「方块 < 填充 < 边缘辉光」）。
            // 满行：横条，宽 = 棋盘宽，高 = 厚度；位于该行中心（x 居中、y 取该行格中心）。
            for (int i = 0; i < preview.Rows.Count; i++)
            {
                int r = preview.Rows[i];
                float y = BoardCellLocalPos(0, r).y;
                ShowBand(_glowMatRow, new Vector2(boardSpan, bandThick), new Vector2(boardCenterX, y));
            }
            // 满列：竖条，高 = 棋盘高，宽 = 厚度；位于该列中心（y 居中、x 取该列格中心）。
            for (int i = 0; i < preview.Cols.Count; i++)
            {
                int c = preview.Cols[i];
                float x = BoardCellLocalPos(c, 0).x;
                ShowBand(_glowMatCol, new Vector2(bandThick, boardSpan), new Vector2(x, boardCenterY));
            }
        }

        private void ClearGhost()
        {
            for (int i = 0; i < _ghostUsed; i++) _ghostPool[i].gameObject.SetActive(false);
            _ghostUsed = 0;
            ClearGlow();
        }

        /// <summary>隐藏全部消除预览发光 + 行内填充（与 ghost 一起清理，避免拖动残留）。GameObject 隐藏后 GlowPulse 自停摆。</summary>
        private void ClearGlow()
        {
            for (int i = 0; i < _glowUsed; i++) _glowPool[i].gameObject.SetActive(false);
            _glowUsed = 0;
            for (int i = 0; i < _fillUsed; i++) _fillPool[i].gameObject.SetActive(false);
            _fillUsed = 0;
        }

        // 候选块容器落点 → 棋盘格 (col,row)。容器父层 m_rect_SlotLayer 与棋盘格父层 m_rect_BoardLayer
        // 在 Content 内有各自偏移，故必须把容器世界坐标换算到 m_rect_BoardLayer 本地空间，
        // 再用与棋盘格渲染同一套自适应格尺寸/原点（BoardCellSize / BoardCellLocalPos 同源）反算 col/row，
        // 保证「所见位置=落子位置」。直接对容器 anchoredPosition 反算会按 SlotLayer 偏移错算（Y 偏移约 5 格）。
        private (int col, int row) ComputeGridPos(RectTransform container, BlockShape shape)
        {
            // 容器世界坐标 → BoardLayer 本地（原点在 BoardLayer 中心，与 BoardCellLocalPos 同坐标系）。
            Vector2 boardLocal = m_rect_BoardLayer.InverseTransformPoint(container.position);
            float cell = BoardCellSize();
            float grid = cell * N;
            // 容器中心对应块阵中心；块阵 shape.Width×shape.Height，左上格中心 = 容器中心 - ((W-1)/2, -(H-1)/2)*cell。
            float tlCenterX = boardLocal.x - (shape.Width - 1) * cell / 2f;
            float tlCenterY = boardLocal.y + (shape.Height - 1) * cell / 2f; // 本地 Y 上正，块阵向下展开故顶行在上方（+）
            // 格 (0,0) 中心 = (-grid/2 + cell/2, +grid/2 - cell/2)；据此反解列/行索引。
            int col = Mathf.RoundToInt((tlCenterX - (-grid / 2f + cell / 2f)) / cell);
            int row = Mathf.RoundToInt(((grid / 2f - cell / 2f) - tlCenterY) / cell);
            return (col, row);
        }

        protected override void OnDestroyWindow()
        {
            // 发光池行 / 列两份材质为运行时 new，窗口销毁随之销毁，避免材质泄漏。
            if (_glowMatRow != null) Object.Destroy(_glowMatRow);
            if (_glowMatCol != null) Object.Destroy(_glowMatCol);
            // 解除应用暂停事件订阅，避免销毁后的窗口仍被回调（驱动器是常驻 MonoBehaviour，不解订阅会泄漏引用）。
            Utility.Unity.RemoveOnApplicationPauseListener(OnAppPause);
            // 跨会话存档兜底（设计 14 §3.4 ③）：离开前脏则强制落盘，须在 ExitMergeOrder 丢弃 MergeState 之前落盘。
            FlushSaveIfDirty();
            // 离开必定关闭门控，确保后续 Classic / 08 行为无残留。
            if (_state != null) _state.ExitMergeOrder();
        }

        // ── 退出按钮（m_btn_Exit，生成代码接线）：关门控 + 关本窗 + 回主菜单 ──
        private partial void OnClick_ExitBtn()
        {
            // 局内态续存：先落盘对局现场，再 ExitMergeOrder 丢弃 MergeState/ElementArr（顺序不可换，否则写空盘覆盖有效快照）。
            FlushSaveIfDirty();
            _state.ExitMergeOrder();
            GameModule.UI.CloseUI<MergeOrderWindow>();
            GameModule.UI.ShowUIAsync<MainMenuWindow>();
        }
    }
}
