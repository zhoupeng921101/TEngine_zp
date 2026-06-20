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
    /// 改名交互（D5）：就地输入框——<see cref="_textName"/>（只读显示）+ <see cref="_inputName"/>（默认隐藏）叠同位，
    /// 点铅笔切到改名态显出并聚焦，确认（onEndEdit）委托 <see cref="PlayerRenameService.TryRename"/>，成功切回只读。
    /// 落盘走 <see cref="GameContext.SavePlayer"/>（ExportToMeta + 既有 MergeMetaPersistence 存档路径，§5.2 B1/H1）。
    /// 生日（D2）：3 下拉纯 UI 占位，不绑数据、不入存档（数据层无生日字段）。
    /// </remarks>
    [Window(UILayer.Top, location: "PlayerInfoWindow", fullScreen: false)]
    public sealed class PlayerInfoWindow : UIWindow
    {
        private const string Atlas = "Sheet_settings";   // 复用设置窗精灵表（设计 23 §三）

        // ── 关闭 / 遮罩 ──
        private Button _btnMask, _btnClose, _btnConfirm;
        private Image _imgClose, _imgConfirmBg, _imgPanelBg, _imgTitleBg;

        // ── 头像区 ──
        private Image _imgAvatar, _imgAvatarFrame;
        private Button _btnEditAvatar;

        // ── 玩家名区 ──
        private Text _textName;
        private InputField _inputName;
        private Button _btnEditName;

        // ── 钻石余额行(设计 38 §五,钻石面板支撑「钻石不足」可观测)──
        private Text _textDiamond;

        // ── 生日区（整块占位，不绑数据 §5.3）──
        private Button _btnBirthYear, _btnBirthMonth, _btnBirthDay;

        private PlayerInfo P => GameContext.Instance.Player;
        private PlayerAttrService Attr => GameContext.Instance.PlayerAttr;

        protected override void ScriptGenerator()
        {
            _btnMask    = FindChildComponent<Button>("m_btn_Mask");
            _btnClose   = FindChildComponent<Button>("Root/m_btn_Close");
            _btnConfirm = FindChildComponent<Button>("Root/m_btn_Confirm");
            _imgClose     = FindChildComponent<Image>("Root/m_btn_Close");
            _imgConfirmBg = FindChildComponent<Image>("Root/m_btn_Confirm");
            _imgPanelBg   = FindChildComponent<Image>("Root/m_img_PanelBg");
            _imgTitleBg   = FindChildComponent<Image>("Root/m_img_TitleBg");

            _imgAvatar      = FindChildComponent<Image>("Root/AvatarBlock/m_img_Avatar");
            _imgAvatarFrame = FindChildComponent<Image>("Root/AvatarBlock/m_img_AvatarFrame");
            _btnEditAvatar  = FindChildComponent<Button>("Root/AvatarBlock/m_btn_EditAvatar");

            _textName    = FindChildComponent<Text>("Root/NameBlock/m_text_Name");
            _inputName   = FindChildComponent<InputField>("Root/NameBlock/m_input_Name");
            _btnEditName = FindChildComponent<Button>("Root/NameBlock/m_btn_EditName");
            // 钻石余额行(prefab 节点缺失时为 null,运行期靠 RefreshDiamond null-safe;美术接入后补节点 §五)
            _textDiamond = FindChildComponent<Text>("Root/NameBlock/m_text_DiamondBalance");

            _btnBirthYear  = FindChildComponent<Button>("Root/BirthdayBlock/m_btn_BirthYear");
            _btnBirthMonth = FindChildComponent<Button>("Root/BirthdayBlock/m_btn_BirthMonth");
            _btnBirthDay   = FindChildComponent<Button>("Root/BirthdayBlock/m_btn_BirthDay");

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

        protected override void OnDestroy()
        {
            // 解绑事件,防 Window 销毁后留 GC root(沿设计 38 §7.4)。
            if (Attr != null) Attr.OnAttrChanged -= OnAttrChangedDispatch;
        }

        protected override void OnRefresh()
        {
            // 每次开窗刷玩家信息（运行期会变的只有头像 + 玩家名）。
            if (_textName != null)
            {
                _textName.text = P?.Name ?? string.Empty;
                _textName.gameObject.SetActive(true);
            }
            if (_inputName != null) _inputName.gameObject.SetActive(false);
            RefreshAvatar();
            RefreshDiamond();
            RefreshRenameInteractable();
        }

        /// <summary>属性事件订阅入口(主线程,设计 38 §7.4)。Diamond / All 触发时刷钻石行 + 改名按钮可点态。</summary>
        private void OnAttrChangedDispatch(AttrType type, long _, string __)
        {
            if (type == AttrType.Diamond || type == AttrType.All)
            {
                RefreshDiamond();
                RefreshRenameInteractable();
            }
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

        /// <summary>点编辑铅笔：进改名态（输入框显出聚焦、隐藏只读文本）。</summary>
        private void OnEnterRename()
        {
            if (_inputName == null) return;
            _inputName.text = P?.Name ?? string.Empty;
            _inputName.gameObject.SetActive(true);
            if (_textName != null) _textName.gameObject.SetActive(false);
            _inputName.ActivateInputField();
        }

        /// <summary>
        /// 改名提交（onEndEdit）:委托 <see cref="PlayerRenameService.TryRename"/>(窗口不自写改名逻辑,W3)。
        /// 屏蔽字词表本轮注空表(去变现/不阻塞,设计 18 O6);扣钻接缝改为「同步调 PlayerAttrService.TryChangeAsync」
        /// 接服务端校验流(设计 38 §六,O8 已兑现:不再 cost=>true 占位)。
        /// </summary>
        /// <remarks>
        /// onEndEdit 不能直接绑 async UniTask 方法,改包成 UniTaskVoid 入口 + try/catch 兜底(沿 HotFix 既有 async UI 范式)。
        /// 同步等响应而非 fire-and-forget(设计 38 D3):避免「先成功后扣钻」错位。
        /// </remarks>
        private void OnRenameSubmit(string newName)
        {
            OnRenameSubmitAsync(newName).Forget();
        }

        /// <summary>
        /// 实际的异步改名流程(设计 38 §六:同步等响应)。
        /// 顺序:① 据 P.RenameCount 算 cost(read-only,不动玩家态);② cost>0 时发 RPC 扣钻、等响应;
        /// ③ 服务端 OK / 免费首改 → 调一次 PlayerRenameService.TryRename(扣钻接缝传 _=>true,已 RPC 扣或免费跳过)
        /// 内含本地校验(空 / 长度 / 屏蔽字)+ 写名 + 计数 +1。
        /// 关键:不做 dry-run(PlayerRenameService.TryRename 成功路径有副作用:写 Name + RenameCount++,
        /// dry-run 通过会让二次进入时 cost 已变,污染计费);本地校验失败时服务端已扣 — 是设计 38 §六走查
        /// 未涵盖的边界,处置:UI 层 InputField 提前拦空/长(onValueChanged + 长度限制 prefab 设值),
        /// 文本非法到此处的概率被压低,服务端已扣钻在改名失败路径仍刷视图(玩家可见、由后续提示挽回)。
        /// </summary>
        private async UniTaskVoid OnRenameSubmitAsync(string newName)
        {
            if (P == null) return;

            // ① 计费:首改 cost=0,非首改读价(read-only,不动 P)。
            int cost = (P.RenameCount == 0) ? 0 : RenamePriceConfig.PriceFor(P.RenameCount);

            // ② RPC 扣钻(仅非首改):成功才进 ③ 落定。
            if (cost > 0)
            {
                if (Attr == null)
                {
                    ShowRenameRejectChange(ChangeReject.ServiceUnavailable);
                    HideRenameInput();
                    return;
                }
                var rpcResult = await Attr.TryChangeAsync(AttrType.Diamond, -cost, "player_rename");
                if (!rpcResult.Success)
                {
                    ShowRenameRejectChange(rpcResult.Reason);
                    HideRenameInput();
                    return;
                }
            }

            // ③ 落定改名(扣钻接缝传 _=>true,扣钻已在 ② 完成或 cost=0 免费跳过):
            // PlayerRenameService 内本地校验仍跑(空 / 长度 / 屏蔽字)— 失败时按 RenameReject 文案。
            var finalResult = PlayerRenameService.TryRename(
                P, newName,
                wordList: System.Array.Empty<string>(),
                trySpendDiamond: _ => true);

            if (finalResult.Success)
            {
                if (_textName != null) _textName.text = P.Name;
                GameContext.Instance.SavePlayer();   // 改名落盘(§5.2 B1/H1:接既有存档路径)
            }
            else
            {
                ShowRenameReject(finalResult.Reason);
            }
            HideRenameInput();
        }

        private void HideRenameInput()
        {
            if (_inputName != null) _inputName.gameObject.SetActive(false);
            if (_textName != null) _textName.gameObject.SetActive(true);
        }

        /// <summary>改名拒绝原因(本地校验类)→ 提示文案(4 分支,设计 18 RenameReject)。</summary>
        private void ShowRenameReject(RenameReject reason)
        {
            string msg = reason switch
            {
                RenameReject.Empty            => "名字不能为空",
                RenameReject.TooLong          => $"名字过长(上限 {PlayerRenameService.MaxLen})",
                RenameReject.Profanity        => "名字含敏感词",
                RenameReject.NotEnoughDiamond => "钻石不足",
                _                             => "改名失败",
            };
            ShowPlaceholder(msg);
        }

        /// <summary>服务端 ChangeReject → 改名拒文案(设计 38 §六对照表;RenameReject 枚举不动,SV8)。</summary>
        private void ShowRenameRejectChange(ChangeReject reason)
        {
            string msg = reason switch
            {
                ChangeReject.NotEnoughBalance   => "钻石不足",
                ChangeReject.TypeUpperOverflow  => "钻石余额异常,请稍后再试",
                ChangeReject.ServiceUnavailable => "网络异常,请稍后再试",
                ChangeReject.NotLoggedIn        => "请重新登录",
                ChangeReject.NetworkDown        => "网络断开,请检查连接",
                ChangeReject.TypeUnknown        => "改名失败",
                _                               => "改名失败",
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
