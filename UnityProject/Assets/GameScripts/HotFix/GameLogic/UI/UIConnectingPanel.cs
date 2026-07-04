using TEngine;

namespace GameLogic.UI
{
    /// <summary>
    /// 入口连接闸窗(强制联网入口)。登录在途时遮屏,在「登录成功 + 服务端数据就绪 + 预载完成」之前
    /// 阻断进入玩法窗(闸的开/关由 <see cref="GameApp"/> 的入口闸控制)。仅一态「连接中…」,不感知失败——
    /// 登录失败由 GameApp 回退到 <see cref="UILoginPanel"/>。
    /// 视觉(遮屏背景 + 标题 + 状态文字)全部由 prefab 承载,字体随 prefab 异步序列化加载;
    /// 本类无运行时逻辑,仅承载窗定位与生命周期。
    /// </summary>
    [Window(UILayer.Top, location: "UIConnectingPanel", fullScreen: true)]
    public sealed class UIConnectingPanel : UIPanelMono
    {
    }
}
