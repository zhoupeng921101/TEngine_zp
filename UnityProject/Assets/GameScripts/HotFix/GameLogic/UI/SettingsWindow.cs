using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.Settings;

namespace GameLogic.UI
{
    /// <summary>
    /// 设置窗（美术换皮，设计 23）。模态弹窗：音乐 / 音效开关 + 各跳转按钮。
    /// 数据走 <see cref="GameContext.Instance"/>.Settings（设计 19 数据层），切图经精灵表子图寻址。
    /// </summary>
    /// <remarks>
    /// 切图寻址：<c>Image.SetSubSprite("Sheet_settings", 子图名)</c>。
    /// 本工程实测：SpriteAtlas v2 资源不向 YooAsset <c>LoadSubAssetsAsync&lt;Sprite&gt;</c> 暴露子精灵
    /// （SubAssets count=0），故改用「单张精灵表 PNG（Sprite Mode=Multiple，含命名子精灵）」作图集资源，
    /// location=<c>Sheet_settings</c>，与工程既有散图（blocks/）的 SubAssets 寻址同源。子图名=源切图文件名。
    /// 引用计数由框架 <c>SubSpriteReference</c> 自动管，无需手动释放（设计 23 §3.3）。
    /// 接钮一律 <c>onClick.AddListener</c>（本工程 UIModule 无 RegisterButtonClick，设计 23 §六 warn）。
    ///
    /// 子图名 → 功能位映射（dev 读 setting.png + 切图缩略图核实，覆盖设计稿 §四按名推断，B6 视觉为准）：
    ///   上面板底 box2；标题文本「SETTING」；关闭 icon_x（圆 + X 一体图）；
    ///   社交行 facebook/twitter/x/youtube/instagram；
    ///   长条底 base_plate3 + 图标 chat（联系）/game（更多游戏）/language（语言）/exit（退登）；
    ///   下面板底 box1；图标按钮底 button；
    ///   下排 5 图标 clear（清存档）/Player_music（音乐♪）/Volume_up（音效🔊）/help（帮助灯泡）/printer（隐私人形）。
    ///   B4：下排实图含「音乐♪」「音效🔊」两位 → 音乐 + 音效双开关进窗。
    /// </remarks>
    [Window(UILayer.Top, location: "SettingsWindow", fullScreen: false)]
    public sealed class SettingsWindow : UIWindow
    {
        private const string Atlas = "Sheet_settings";

        // ── 关闭 / 遮罩 ──
        private Button _btnMask;
        private Button _btnClose;
        private Image _imgClose;

        // ── 上面板 ──
        private Image _imgPanelTopBg;
        private Button _btnFacebook, _btnTwitter, _btnX, _btnYoutube, _btnInstagram;
        private Image _imgFacebook, _imgTwitter, _imgX, _imgYoutube, _imgInstagram;
        private Button _btnContact, _btnMoreGames, _btnLanguage, _btnLogout;
        private Image _imgContactBg, _imgMoreGamesBg, _imgLanguageBg, _imgLogoutBg;
        private Image _imgContactIcon, _imgMoreGamesIcon, _imgLanguageIcon, _imgLogoutIcon;

        // ── 下面板 ──
        private Image _imgPanelBottomBg;
        private Button _btnClearSave, _btnHelp, _btnPrivacy;
        private Image _imgClearSaveBg, _imgClearSaveIcon, _imgHelpBg, _imgHelpIcon, _imgPrivacyBg, _imgPrivacyIcon;
        private Toggle _toggleMusic, _toggleSound;
        private Image _imgMusicBg, _imgMusicIcon, _imgSoundBg, _imgSoundIcon;

        private SettingsService Svc => GameContext.Instance.Settings;

        protected override void ScriptGenerator()
        {
            _btnMask  = FindChildComponent<Button>("m_btn_Mask");
            _btnClose = FindChildComponent<Button>("Root/m_btn_Close");
            _imgClose = FindChildComponent<Image>("Root/m_btn_Close");

            _imgPanelTopBg = FindChildComponent<Image>("Root/PanelTop/m_img_PanelTopBg");
            _btnFacebook   = FindChildComponent<Button>("Root/PanelTop/SocialRow/m_btn_Facebook");
            _btnTwitter    = FindChildComponent<Button>("Root/PanelTop/SocialRow/m_btn_Twitter");
            _btnX          = FindChildComponent<Button>("Root/PanelTop/SocialRow/m_btn_X");
            _btnYoutube    = FindChildComponent<Button>("Root/PanelTop/SocialRow/m_btn_Youtube");
            _btnInstagram  = FindChildComponent<Button>("Root/PanelTop/SocialRow/m_btn_Instagram");
            _imgFacebook   = FindChildComponent<Image>("Root/PanelTop/SocialRow/m_btn_Facebook");
            _imgTwitter    = FindChildComponent<Image>("Root/PanelTop/SocialRow/m_btn_Twitter");
            _imgX          = FindChildComponent<Image>("Root/PanelTop/SocialRow/m_btn_X");
            _imgYoutube    = FindChildComponent<Image>("Root/PanelTop/SocialRow/m_btn_Youtube");
            _imgInstagram  = FindChildComponent<Image>("Root/PanelTop/SocialRow/m_btn_Instagram");

            _btnContact   = FindChildComponent<Button>("Root/PanelTop/m_btn_Contact");
            _btnMoreGames = FindChildComponent<Button>("Root/PanelTop/m_btn_MoreGames");
            _btnLanguage  = FindChildComponent<Button>("Root/PanelTop/m_btn_Language");
            _btnLogout    = FindChildComponent<Button>("Root/PanelTop/m_btn_Logout");
            _imgContactBg   = FindChildComponent<Image>("Root/PanelTop/m_btn_Contact");
            _imgMoreGamesBg = FindChildComponent<Image>("Root/PanelTop/m_btn_MoreGames");
            _imgLanguageBg  = FindChildComponent<Image>("Root/PanelTop/m_btn_Language");
            _imgLogoutBg    = FindChildComponent<Image>("Root/PanelTop/m_btn_Logout");
            _imgContactIcon   = FindChildComponent<Image>("Root/PanelTop/m_btn_Contact/Icon");
            _imgMoreGamesIcon = FindChildComponent<Image>("Root/PanelTop/m_btn_MoreGames/Icon");
            _imgLanguageIcon  = FindChildComponent<Image>("Root/PanelTop/m_btn_Language/Icon");
            _imgLogoutIcon    = FindChildComponent<Image>("Root/PanelTop/m_btn_Logout/Icon");

            _imgPanelBottomBg = FindChildComponent<Image>("Root/PanelBottom/m_img_PanelBottomBg");
            _btnClearSave = FindChildComponent<Button>("Root/PanelBottom/m_btn_ClearSave");
            _btnHelp      = FindChildComponent<Button>("Root/PanelBottom/m_btn_Help");
            _btnPrivacy   = FindChildComponent<Button>("Root/PanelBottom/m_btn_Privacy");
            _imgClearSaveBg   = FindChildComponent<Image>("Root/PanelBottom/m_btn_ClearSave");
            _imgClearSaveIcon = FindChildComponent<Image>("Root/PanelBottom/m_btn_ClearSave/Icon");
            _imgHelpBg        = FindChildComponent<Image>("Root/PanelBottom/m_btn_Help");
            _imgHelpIcon      = FindChildComponent<Image>("Root/PanelBottom/m_btn_Help/Icon");
            _imgPrivacyBg     = FindChildComponent<Image>("Root/PanelBottom/m_btn_Privacy");
            _imgPrivacyIcon   = FindChildComponent<Image>("Root/PanelBottom/m_btn_Privacy/Icon");

            _toggleMusic = FindChildComponent<Toggle>("Root/PanelBottom/m_toggle_Music");
            _toggleSound = FindChildComponent<Toggle>("Root/PanelBottom/m_toggle_Sound");
            _imgMusicBg   = FindChildComponent<Image>("Root/PanelBottom/m_toggle_Music");
            _imgMusicIcon = FindChildComponent<Image>("Root/PanelBottom/m_toggle_Music/Icon");
            _imgSoundBg   = FindChildComponent<Image>("Root/PanelBottom/m_toggle_Sound");
            _imgSoundIcon = FindChildComponent<Image>("Root/PanelBottom/m_toggle_Sound/Icon");

            // ── 接钮（onClick / onValueChanged；监听随 GameObject 销毁自动清，无需手动 Remove）──
            _btnMask.onClick.AddListener(Close);
            _btnClose.onClick.AddListener(Close);

            _btnFacebook.onClick.AddListener(() => OnSocial("facebook"));
            _btnTwitter.onClick.AddListener(() => OnSocial("twitter"));
            _btnX.onClick.AddListener(() => OnSocial("x"));
            _btnYoutube.onClick.AddListener(() => OnSocial("youtube"));
            _btnInstagram.onClick.AddListener(() => OnSocial("instagram"));

            _btnContact.onClick.AddListener(OnContact);
            _btnMoreGames.onClick.AddListener(OnMoreGames);
            _btnLanguage.onClick.AddListener(OnLanguage);
            _btnLogout.onClick.AddListener(OnLogout);

            _btnClearSave.onClick.AddListener(OnClearSave);
            _btnHelp.onClick.AddListener(OnHelp);
            _btnPrivacy.onClick.AddListener(OnPrivacy);

            _toggleMusic.onValueChanged.AddListener(OnMusicToggled);
            _toggleSound.onValueChanged.AddListener(OnSoundToggled);
        }

        protected override void OnCreate()
        {
            // 一次性贴静态图（SetSubSprite 自动管引用计数，无需 OnDestroy 释放）。
            _imgClose.SetSubSprite(Atlas, "icon_x");
            _imgPanelTopBg.SetSubSprite(Atlas, "box2");
            _imgPanelBottomBg.SetSubSprite(Atlas, "box1");

            _imgFacebook.SetSubSprite(Atlas, "facebook");
            _imgTwitter.SetSubSprite(Atlas, "twitter");
            _imgX.SetSubSprite(Atlas, "x");
            _imgYoutube.SetSubSprite(Atlas, "youtube");
            _imgInstagram.SetSubSprite(Atlas, "instagram");

            _imgContactBg.SetSubSprite(Atlas, "base_plate3");
            _imgMoreGamesBg.SetSubSprite(Atlas, "base_plate3");
            _imgLanguageBg.SetSubSprite(Atlas, "base_plate3");
            _imgLogoutBg.SetSubSprite(Atlas, "base_plate3");
            _imgContactIcon.SetSubSprite(Atlas, "chat");
            _imgMoreGamesIcon.SetSubSprite(Atlas, "game");
            _imgLanguageIcon.SetSubSprite(Atlas, "language");
            _imgLogoutIcon.SetSubSprite(Atlas, "exit");

            _imgClearSaveBg.SetSubSprite(Atlas, "button");
            _imgMusicBg.SetSubSprite(Atlas, "button");
            _imgSoundBg.SetSubSprite(Atlas, "button");
            _imgHelpBg.SetSubSprite(Atlas, "button");
            _imgPrivacyBg.SetSubSprite(Atlas, "button");
            _imgClearSaveIcon.SetSubSprite(Atlas, "clear");
            _imgMusicIcon.SetSubSprite(Atlas, "Player_music");
            _imgSoundIcon.SetSubSprite(Atlas, "Volume_up");
            _imgHelpIcon.SetSubSprite(Atlas, "help");
            _imgPrivacyIcon.SetSubSprite(Atlas, "printer");
        }

        protected override void OnRefresh()
        {
            // 每次开窗刷开关态（用户上次的选择）。SetIsOnWithoutNotify 不触发回调、仅刷 UI。
            _toggleMusic.SetIsOnWithoutNotify(Svc.Audio.MusicOn);
            _toggleSound.SetIsOnWithoutNotify(Svc.Audio.SoundOn);
            RefreshAudioIconTint();
        }

        // ── 实做：音频开关（核心验收 W2 / V3）──
        private void OnMusicToggled(bool on)
        {
            Svc.SetMusic(on);   // 数据层：改模型 → 落盘 → 应用音频（设计 19 §3.4）
            RefreshAudioIconTint();
        }

        private void OnSoundToggled(bool on)
        {
            Svc.SetSound(on);
            RefreshAudioIconTint();
        }

        /// <summary>切图无独立静音态图（B3）：以图标颜色区分开 / 关（开=白、关=半透明灰）。TODO 待美术补静音版子图后改替子图。</summary>
        private void RefreshAudioIconTint()
        {
            var on = new Color(1f, 1f, 1f, 1f);
            var off = new Color(1f, 1f, 1f, 0.4f);
            if (_imgMusicIcon != null) _imgMusicIcon.color = Svc.Audio.MusicOn ? on : off;
            if (_imgSoundIcon != null) _imgSoundIcon.color = Svc.Audio.SoundOn ? on : off;
        }

        // ── 实做：协议 / 隐私 OpenURL（V5；占位 URL，替换常量即生效）──
        private void OnPrivacy() => Application.OpenURL(SettingsLinks.PrivacyPolicyUrl);

        // ── 清除存档（B2）：工程无通用二次确认弹窗组件，本轮降级占位 + TODO（防误触清档，不便裸接清除）──
        private void OnClearSave()
            => ShowPlaceholder("清除存档需二次确认弹窗，确认窗组件待建（设计 23 §七 B2）");

        // ── 占位：依赖系统未建 / 真实 URL 待产品（设计 23 §七）──
        private void OnContact()   => ShowPlaceholder("客服形态待定（设计 19 §七 O2）");
        private void OnMoreGames() => ShowPlaceholder("更多游戏待建（去变现方向，不做真实导流）");
        private void OnLanguage()  => ShowPlaceholder("多语言系统待建（全局文案 textId 占位）");
        // ── 登出（设计 36 §三）：断当前会话 + 立即重新走自动登录（=重新登录到同一账号）。
        // 三步顺次：① ShowPlaceholder 反馈现状语义；② FantasyNetwork.Shutdown 清连接 + 心跳 + 四态;
        // ③ FantasyNetwork.Boot 无参重连(账号沿 DefaultAccountName 派生,UUID 不变)。
        // 顺序锁:Shutdown 把 _initialized=false 清掉,Boot 才会走完整初始化;调换则 Boot 内 _initialized 早返、复用旧 Scene 不重连。
        private void OnLogout()
        {
            ShowPlaceholder("已断开连接，正在重新登录…");
#if FANTASY_UNITY
            FantasyClient.FantasyNetwork.Shutdown();
            FantasyClient.FantasyNetwork.Boot();
#endif
        }
        private void OnHelp()      => ShowPlaceholder("帮助 / FAQ 内容系统待建");
        private void OnSocial(string platform)
            => ShowPlaceholder($"社交外链「{platform}」真实账号 URL 待产品提供");

        /// <summary>占位统一反馈（V5：不死按钮）。工程暂无 Toast / 飘字系统 → 临时 Log.Info；待建后替换。</summary>
        private void ShowPlaceholder(string msg) => Log.Info($"[设置窗·待建] {msg}");

        private void Close() => GameModule.UI.CloseUI<SettingsWindow>();
    }
}
