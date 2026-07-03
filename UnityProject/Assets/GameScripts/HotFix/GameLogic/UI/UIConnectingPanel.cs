using Cysharp.Threading.Tasks;
using UnityEngine;
using TEngine;

namespace GameLogic.UI
{
    /// <summary>
    /// 入口连接闸窗(强制联网入口)。登录在途时摆出、遮住背后,在「登录成功 + 服务端数据就绪 + 预载完成」之前
    /// 阻断进入玩法窗(开/关由 <see cref="GameApp"/> 的入口闸控制)。仅一态「连接中…」:不感知失败——
    /// 登录失败由 GameApp 回退到 <see cref="UILoginPanel"/> 处理(闸窗只负责在途遮屏与等待)。
    /// 纯代码搭 UI,prefab 为空壳根节点,无 m_* 绑定。
    ///
    /// 字体时序(WebGL 关键):本窗在 <see cref="GameApp"/> 同步启动路径上立即摆出(先于字体异步预载完成),
    /// 而 <see cref="UGuiFactory"/> 文本创建首次取字体会把进程级静态字体缓存永久钉死。若在字体预载前建文本,
    /// WebGL 下同步取 GBK 失败 → 回退内置字体被钉死 → 全局中文无字形。故 OnCreate 分两段:
    ///   ① 同步建遮屏背景(不依赖字体),保证摆上即盖屏;
    ///   ② 文本(依赖字体)延后到 <see cref="UIPreloader.PreloadFontsAsync"/> await 完成后再建。
    /// 窗的「显示」仍走同步路径不动(推迟 show 会破坏登录回调 CloseUI 命中、致闸窗永久盖死)。
    /// </summary>
    [Window(UILayer.Top, location: "UIConnectingPanel", fullScreen: true)]
    public sealed class UIConnectingPanel : UIPanelMono
    {
        // 设计坐标系(1080×1920,左上原点、Y 下正),与 UGuiFactory 同口径。
        private const float Cx = 540f;

        private RectTransform _content;

        private static readonly Color BgColor = new Color32(0x12, 0x16, 0x22, 0xFF);
        private static readonly Color StatusConnectingColor = new Color32(0x88, 0xaa, 0xcc, 0xFF);

        protected override void OnCreate()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

            // ① 立即同步建遮屏背景(不依赖字体),保证闸窗一摆上就盖住背后任何已存在画面
            //    (杜绝主菜单/旧数据抢先闪现)。
            var bg = UGuiFactory.CreateImage(_content, "Bg", Cx, 960, 1080, 1920, BgColor);
            bg.raycastTarget = true;

            // ② 文本延后到字体预载完成后再建(防 WebGL 同步取字体失败钉死回退字体)。
            BuildTextsAfterFontReady().Forget();
        }

        /// <summary>
        /// await 字体预载完成后建依赖字体的文本。
        /// race 守卫:await 期间窗可能被关闭/销毁(登录成功 → GameApp.CloseUI&lt;UIConnectingPanel&gt;)。
        /// 返回后触碰任何 GameObject/组件前先判窗是否已销毁,已销毁直接 return。
        /// </summary>
        private async UniTaskVoid BuildTextsAfterFontReady()
        {
            // 本地 bundle 加载、幂等(已驻留快速返回)。失败逐项记 Error 不抛(见 UIPreloader)。
            await GameLogic.UIPreloader.PreloadFontsAsync();

            if (this == null || IsDestroyed)
            {
                return;
            }

            UGuiFactory.CreateText(_content, "Title", Cx, 760, 1008, 158, "BLOCK BLAST", 96,
                new Color32(0xff, 0xe0, 0x66, 0xFF));
            UGuiFactory.CreateText(_content, "Status", Cx, 960, 1008, 80, "连接中…", 48,
                StatusConnectingColor);
        }
    }
}
