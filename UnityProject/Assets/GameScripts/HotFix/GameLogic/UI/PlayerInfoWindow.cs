using UnityEngine;
using UnityEngine.UI;
using TEngine;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast.Player;

namespace GameLogic.UI
{
    /// <summary>
    /// 个人信息窗（美术换皮，设计 25）。模态弹窗：头像 + 玩家名（可改名）+ 生日（占位）+ 确定。
    /// 数据走 <see cref="GameContext.Instance"/>.Player（设计 18 数据层），切图复用 Sheet_settings 精灵表（设计 23）。
    /// </summary>
    /// <remarks>
    /// 切图寻址：<c>Image.SetSubSprite("Sheet_settings", 子图名)</c>（同设计 23 设置窗，精灵表 Multiple 模式，
    /// location=Sheet_settings，子图名=源切图文件名；引用计数由框架 SubSpriteReference 自动管，无需手动释放）。
    /// 接钮一律 <c>onClick.AddListener</c>（本工程 UIModule 无 RegisterButtonClick，设计 23 已验证）。
    ///
    /// 子图名 → 节点映射（复用 Sheet_settings 22 张子图，圆头像框/铅笔/下拉箭头无图 → 占位 §3.2）：
    ///   标题木牌底 box2；主面板底 box1；关闭圆按钮 icon_x；确定长条底 button。
    ///   头像本体：当前头像 Sprite 无美术 → 占位（按 CurrentAvatarId 取稳定色的纯色块）；
    ///   头像圆框 / 编辑铅笔 / 生日下拉箭头：Sheet_settings 无对应图 → 节点留占位、可点。
    ///
    /// 昵称显示读服务端权威 <see cref="PlayerAttrService.Nickname"/>(登录快照下发),不读本地 blob。
    /// 改名交互（D5）：就地输入框——<see cref="_textName"/>（只读显示）+ <see cref="_inputName"/>（默认隐藏）叠同位，
    /// 点铅笔切到改名态显出并聚焦，确认（onEndEdit）走服务端权威 RPC(<see cref="IRenameGateway"/>):
    /// 本地先校验(空 / 长度 / 屏蔽字)减一次往返 → 发 C2G_RenameRequest → 服务端一次原子算费/扣钻/写名/计数,
    /// 响应回带最新 Nickname/RenameCount/Diamond 覆盖 <see cref="PlayerAttrService"/> 视图。客户端不本地扣钻、不本地写名、不落 blob。
    /// 生日（D2）：3 下拉纯 UI 占位，不绑数据、不入存档（数据层无生日字段）。
    /// </remarks>
    [Window(UILayer.Top, location: "PlayerInfoWindow", fullScreen: false)]
    public sealed class PlayerInfoWindow : UIWindowMono
    {
        private const string Atlas = "Sheet_settings";   // 复用设置窗精灵表（设计 23 §三）

        // 引用走 Inspector 拖拽（[SerializeField]）；prefab 节点路径见 ScriptGenerator 旁注。
        // ── 关闭 / 遮罩 ──
        [SerializeField] private Button _btnMask;
        [SerializeField] private Button _btnClose;
        [SerializeField] private Button _btnConfirm;
        [SerializeField] private Image _imgClose;
        [SerializeField] private Image _imgConfirmBg;
        [SerializeField] private Image _imgPanelBg;
        [SerializeField] private Image _imgTitleBg;

        // ── 头像区 ──
        [SerializeField] private Image _imgAvatar;
        [SerializeField] private Image _imgAvatarFrame;
        [SerializeField] private Button _btnEditAvatar;

        // ── 玩家名区 ──
        [SerializeField] private Text _textName;
        [SerializeField] private InputField _inputName;
        [SerializeField] private Button _btnEditName;

        // ── 钻石余额行(设计 38 §五,钻石面板支撑「钻石不足」可观测)──
        [SerializeField] private Text _textDiamond;

        // ── 生日区（整块占位，不绑数据 §5.3）──
        [SerializeField] private Button _btnBirthYear;
        [SerializeField] private Button _btnBirthMonth;
        [SerializeField] private Button _btnBirthDay;

        // ── 我的流水入口按钮(设计 46 §4.2,代码动态生成挂 NameBlock 下;美术 Tier 2+ 补图)──
        // 运行时代码生成（非 [SerializeField]），引用见 BuildLedgerEntryButton。
        private Button _btnLedgerEntry;

        private PlayerInfo P => GameContext.Instance.Player;
        private PlayerAttrService Attr => GameContext.Instance.PlayerAttr;
        private IRenameGateway RenameGw => GameContext.Instance.RenameGateway;

        /// <summary>当前显示昵称:服务端权威(<see cref="PlayerAttrService.Nickname"/>);快照未到(IsReady=false)时空串占位。</summary>
        private string DisplayName => (Attr != null && Attr.IsReady) ? Attr.Nickname : string.Empty;

        protected override void ScriptGenerator()
        {
            // 引用由 [SerializeField] 在 Inspector 拖入就位（原 FindChild 路径对照，便于校核拖线）：
            //   _btnMask        m_btn_Mask
            //   _btnClose       Root/m_btn_Close (Button)        _imgClose       Root/m_btn_Close (Image)
            //   _btnConfirm     Root/m_btn_Confirm (Button)      _imgConfirmBg   Root/m_btn_Confirm (Image)
            //   _imgPanelBg     Root/m_img_PanelBg               _imgTitleBg     Root/m_img_TitleBg
            //   _imgAvatar      Root/AvatarBlock/m_img_Avatar    _imgAvatarFrame Root/AvatarBlock/m_img_AvatarFrame
            //   _btnEditAvatar  Root/AvatarBlock/m_btn_EditAvatar
            //   _textName       Root/NameBlock/m_text_Name       _inputName      Root/NameBlock/m_input_Name
            //   _btnEditName    Root/NameBlock/m_btn_EditName
            //   _textDiamond    Root/NameBlock/m_text_DiamondBalance（prefab 节点缺失时留 None，运行期 RefreshDiamond null-safe）
            //   _btnBirthYear   Root/BirthdayBlock/m_btn_BirthYear
            //   _btnBirthMonth  Root/BirthdayBlock/m_btn_BirthMonth
            //   _btnBirthDay    Root/BirthdayBlock/m_btn_BirthDay

            // ── 接钮（onClick；监听随 GameObject 销毁自动清，无需手动 Remove——同设计 23）──
            // 遮罩 = 效果图「点击任意位置关闭」；确定 / X 同义：保存并关。
            if (_btnMask != null)    _btnMask.onClick.AddListener(OnConfirmAndClose);
            if (_btnClose != null)   _btnClose.onClick.AddListener(OnConfirmAndClose);
            if (_btnConfirm != null) _btnConfirm.onClick.AddListener(OnConfirmAndClose);
            if (_btnEditAvatar != null) _btnEditAvatar.onClick.AddListener(OnEditAvatar);
            if (_btnEditName != null)   _btnEditName.onClick.AddListener(OnEnterRename);
            if (_inputName != null)     _inputName.onEndEdit.AddListener(OnRenameSubmit);

            // 生日 3 下拉：纯占位，点击给反馈（D2，不绑数据）。
            if (_btnBirthYear != null)  _btnBirthYear.onClick.AddListener(OnBirthdayPlaceholder);
            if (_btnBirthMonth != null) _btnBirthMonth.onClick.AddListener(OnBirthdayPlaceholder);
            if (_btnBirthDay != null)   _btnBirthDay.onClick.AddListener(OnBirthdayPlaceholder);

            // 我的流水入口按钮(设计 46 §4.2):代码动态生成挂 NameBlock 下方,prefab 不需新增节点;
            // NameBlock 节点缺失 → _btnLedgerEntry 为 null,运行期跳过(PV12 null-safe)。
            _btnLedgerEntry = BuildLedgerEntryButton();
            if (_btnLedgerEntry != null)
                _btnLedgerEntry.onClick.AddListener(OnLedgerEntryClicked);
        }

        /// <summary>
        /// 代码动态生成「我的流水」按钮挂 NameBlock 下方(设计 46 §4.2)。
        /// </summary>
        /// <remarks>
        /// 沿 RankWindow 全代码生成范式:不动 25 PlayerInfoWindow prefab 节点(美术 Tier 2+ 补图后即用,本子单不阻塞);
        /// 占位色 + 文案,语义连续(玩家进 PlayerInfoWindow 看个人信息 → 自然顺手看流水)。
        /// NameBlock 父节点缺失时返 null,调用方 null-safe 跳过(PV12)。
        /// </remarks>
        private Button BuildLedgerEntryButton()
        {
            var nameBlock = FindChildComponent<Transform>("Root/NameBlock");
            if (nameBlock == null) return null;

            // 创建占位按钮:240×60,挂在 NameBlock 下方(local y 偏移 -90)
            var go = new GameObject("m_btn_LedgerEntry",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(nameBlock, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(240f, 60f);
            rt.anchoredPosition = new Vector2(0f, -90f);

            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.95f, 0.78f, 0.42f);
            // 沿 25 §三现有按钮范式占位:Sheet_settings 长条底图(美术补节点后可改 SetSubSprite)
            img.SetSubSprite(Atlas, "button");

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            // 文字「我的流水」
            var textGo = new GameObject("Label",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.SetParent(go.transform, false);
            textRt.anchorMin = textRt.anchorMax = textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.sizeDelta = new Vector2(240f, 60f);
            textRt.anchoredPosition = Vector2.zero;
            var txt = textGo.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = "我的流水";
            txt.fontSize = 28;
            txt.color = new Color(0.30f, 0.20f, 0.08f);
            txt.alignment = TextAnchor.MiddleCenter;
            txt.fontStyle = FontStyle.Bold;
            txt.raycastTarget = false;

            return btn;
        }

        /// <summary>点「我的流水」按钮 → 打开 PlayerAttrLedgerWindow(设计 46 §4.2)。</summary>
        private void OnLedgerEntryClicked()
        {
            GameModule.UI.ShowUIAsync<PlayerAttrLedgerWindow>();
        }

        protected override void OnCreate()
        {
            // 一次性贴静态图（SetSubSprite 自动管引用计数，无需 OnDestroy 释放）。
            _imgTitleBg?.SetSubSprite(Atlas, "box2");
            _imgPanelBg?.SetSubSprite(Atlas, "box1");
            _imgClose?.SetSubSprite(Atlas, "icon_x");
            _imgConfirmBg?.SetSubSprite(Atlas, "button");
            // 头像框 / 铅笔 / 下拉箭头：Sheet_settings 无对应图 → 占位（§3.2），节点留位、可点。

            // 改名态默认隐藏（点铅笔切出，§七）。
            if (_inputName != null) _inputName.gameObject.SetActive(false);

            // 订阅属性变化(设计 38 §五):钻石余额刷新 + 改名按钮可点态(IsReady=false 禁改名,D7)。
            if (Attr != null) Attr.OnAttrChanged += OnAttrChangedDispatch;
        }

        protected override void OnDestroyWindow()
        {
            // 解绑事件,防 Window 销毁后留 GC root(沿设计 38 §7.4)。
            if (Attr != null) Attr.OnAttrChanged -= OnAttrChangedDispatch;
        }

        protected override void OnRefresh()
        {
            // 每次开窗刷玩家信息（运行期会变的只有头像 + 玩家名）。
            // 昵称读服务端权威(PlayerAttrService.Nickname),不再读本地 blob(PlayerInfo.Name)。
            if (_textName != null)
            {
                _textName.text = DisplayName;
                _textName.gameObject.SetActive(true);
            }
            if (_inputName != null) _inputName.gameObject.SetActive(false);
            RefreshAvatar();
            RefreshDiamond();
            RefreshRenameInteractable();
        }

        /// <summary>
        /// 属性事件订阅入口(主线程,设计 38 §7.4)。Diamond / All 触发时刷钻石行 + 改名按钮可点态 + 昵称文本。
        /// 改名成功 / 服务端权威对齐经 <see cref="PlayerAttrService.ApplyRenameSuccess"/> 触发 Diamond 事件驱动整体重绘。
        /// </summary>
        private void OnAttrChangedDispatch(AttrType type, long _, string __)
        {
            if (type == AttrType.Diamond || type == AttrType.All)
            {
                RefreshName();
                RefreshDiamond();
                RefreshRenameInteractable();
            }
        }

        /// <summary>刷昵称文本(读服务端权威 <see cref="PlayerAttrService.Nickname"/>);仅当当前处只读态才回写(改名输入态不打断)。</summary>
        private void RefreshName()
        {
            if (_textName == null) return;
            // 改名输入中(_inputName 显出)不打断用户输入;只在只读态刷显示。
            if (_inputName != null && _inputName.gameObject.activeSelf) return;
            _textName.text = DisplayName;
        }

        /// <summary>刷钻石余额行(设计 38 §五):IsReady=false → 「加载中...」;true → 显示真实余额。</summary>
        private void RefreshDiamond()
        {
            if (_textDiamond == null) return;
            if (Attr != null && Attr.IsReady)
            {
                _textDiamond.text = $"钻石: {Attr.Diamond}";
            }
            else
            {
                _textDiamond.text = "钻石: 加载中...";
            }
        }

        /// <summary>刷改名按钮可点态(设计 38 §7.1 D7):IsReady=false → disabled,防服务端快照未到时虚扣或误判余额。</summary>
        private void RefreshRenameInteractable()
        {
            if (_btnEditName == null) return;
            _btnEditName.interactable = (Attr != null && Attr.IsReady);
        }

        /// <summary>
        /// 刷头像（占位：头像 Sprite 无美术 → 按 <see cref="PlayerInfo.CurrentAvatarId"/> 取稳定占位色，§3.2）。
        /// </summary>
        private void RefreshAvatar()
        {
            // TODO(设计 25 §3.2): 接 AvatarConfigMgr.GetAvatar(P.CurrentAvatarId).Image 加载真实头像 Sprite，替占位色。
            if (_imgAvatar == null || P == null) return;
            _imgAvatar.color = StableColorFor(P.CurrentAvatarId);   // 区分度由 id 取色（占位）
        }

        /// <summary>由头像 id 取一个稳定的占位色（同 id 同色，便于人眼区分当前头像，§3.2）。</summary>
        private static Color StableColorFor(int id)
        {
            // 把 id 散列到色相，固定饱和度/明度，保证可读且每 id 稳定。
            float hue = ((id * 0.61803398875f) % 1f + 1f) % 1f;   // 黄金比散列，分布均匀
            return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }

        // ── 改名（实做，贯通数据层 §七 W3）──

        /// <summary>点编辑铅笔：进改名态（输入框显出聚焦、隐藏只读文本）。预填服务端权威昵称。</summary>
        private void OnEnterRename()
        {
            if (_inputName == null) return;
            _inputName.text = DisplayName;
            _inputName.gameObject.SetActive(true);
            if (_textName != null) _textName.gameObject.SetActive(false);
            _inputName.ActivateInputField();
        }

        /// <summary>
        /// 改名提交（onEndEdit）:改走服务端权威 RPC(改名服务端权威·客户端段)。
        /// 本地先校验(空 / 长度 / 屏蔽字,减一次无谓往返)→ 发 C2G_RenameRequest → 据响应对齐视图。
        /// </summary>
        /// <remarks>
        /// onEndEdit 不能直接绑 async UniTask 方法,改包成 UniTaskVoid 入口(沿 HotFix 既有 async UI 范式)。
        /// 同步等响应(非 fire-and-forget):费用 / 扣钻 / 写名 / 计数服务端一次原子做完,客户端不本地扣钻、不本地写名。
        /// </remarks>
        private void OnRenameSubmit(string newName)
        {
            OnRenameSubmitAsync(newName).Forget();
        }

        /// <summary>
        /// 实际的异步改名流程(改名服务端权威·客户端段)。
        /// 顺序:① 本地校验(空 / 长度 / 屏蔽字)—— 明显非法直接拒、不发 RPC;
        /// ② 发 C2G_RenameRequest(仅上报新昵称,费用/次数服务端派生);
        /// ③ 据响应对齐:Success → 用回带 Nickname/RenameCount/Diamond 覆盖 PlayerAttrService(不本地写名);
        ///    InvalidName/NotEnoughDiamond → 提示 + 用回带权威值对齐(不改名);其它(未登录/服务不可用/断网)→ 仅提示,不动视图。
        /// </summary>
        private async UniTaskVoid OnRenameSubmitAsync(string newName)
        {
            // ① 本地校验(空词表 → 实际只挡空 / 超长):明显非法直接拒、不发 RPC。
            var localReject = PlayerRenameService.ValidateLocal(newName, System.Array.Empty<string>());
            if (localReject != RenameReject.None)
            {
                ShowRenameReject(localReject);
                HideRenameInput();
                return;
            }

            if (RenameGw == null)
            {
                ShowRenameOutcome(RenameOutcome.ServiceUnavailable);
                HideRenameInput();
                return;
            }

            // ② 发 RPC,同步等响应。
            var result = await RenameGw.SendRenameAsync(newName);

            // ③ 据响应对齐视图。
            if (result.Success)
            {
                // 用服务端回带权威值覆盖本地视图(Nickname/RenameCount/Diamond)+ 触发事件刷 UI。
                Attr?.ApplyRenameAuthoritative(result.Nickname, result.RenameCount, result.Diamond);
            }
            else
            {
                // InvalidName / NotEnoughDiamond 响应回带了服务端当前权威值 → 对齐,防两端漂移。
                if (result.Outcome == RenameOutcome.InvalidName || result.Outcome == RenameOutcome.NotEnoughDiamond)
                {
                    Attr?.ApplyRenameAuthoritative(result.Nickname, result.RenameCount, result.Diamond);
                }
                ShowRenameOutcome(result.Outcome);
            }
            HideRenameInput();
        }

        private void HideRenameInput()
        {
            if (_inputName != null) _inputName.gameObject.SetActive(false);
            if (_textName != null)
            {
                _textName.text = DisplayName;   // 回到只读态时读服务端权威昵称
                _textName.gameObject.SetActive(true);
            }
        }

        /// <summary>改名本地校验拒绝 → 提示文案(3 分支,RenameReject)。</summary>
        private void ShowRenameReject(RenameReject reason)
        {
            string msg = reason switch
            {
                RenameReject.Empty     => "名字不能为空",
                RenameReject.TooLong   => $"名字过长(上限 {PlayerRenameService.MaxLen})",
                RenameReject.Profanity => "名字含敏感词",
                _                      => "改名失败",
            };
            ShowPlaceholder(msg);
        }

        /// <summary>改名 RPC 结果码 → 提示文案(RenameOutcome 各分支)。</summary>
        private void ShowRenameOutcome(RenameOutcome outcome)
        {
            string msg = outcome switch
            {
                RenameOutcome.InvalidName        => "名字不合法",
                RenameOutcome.NotEnoughDiamond   => "钻石不足",
                RenameOutcome.NotLoggedIn        => "请重新登录",
                RenameOutcome.ServiceUnavailable => "网络异常,请稍后再试",
                RenameOutcome.NetworkDown        => "网络断开,请检查连接",
                _                                => "改名失败",
            };
            ShowPlaceholder(msg);
        }

        // ── 占位项（点击不崩、留接线点 §七 W5）──

        /// <summary>编辑头像：头像三态选择网格属设计 18 后续屏，本屏占位（D3）。</summary>
        private void OnEditAvatar()
            => ShowPlaceholder("头像选择网格待建（设计 18 三态网格属后续屏 §七）");

        /// <summary>生日下拉：数据层无生日字段，本屏纯 UI 占位（D2）。</summary>
        private void OnBirthdayPlaceholder()
            => ShowPlaceholder("生日待接数据层（数据层暂无生日字段，设计 25 §5.3）");

        // ── 确定 / 关闭（实做，§八）──

        /// <summary>确定 / 关闭 X / 点遮罩任意处：保存玩家信息并关窗（效果图三种关闭同此）。</summary>
        private void OnConfirmAndClose()
        {
            GameContext.Instance.SavePlayer();   // 确定再保险存一次（改名时已存过）
            GameModule.UI.CloseUI<PlayerInfoWindow>();
        }

        /// <summary>占位统一反馈（同设计 23：工程暂无 Toast / 飘字 → 临时 Log.Info；待建后替换为真实弹字）。</summary>
        private void ShowPlaceholder(string msg) => Log.Info($"[个人信息窗] {msg}");
    }
}
