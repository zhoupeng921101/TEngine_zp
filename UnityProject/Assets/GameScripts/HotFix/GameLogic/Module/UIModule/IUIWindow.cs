using System;

namespace GameLogic
{
    /// <summary>
    /// UI 窗口多态接口：UIModule 操作窗口栈所需的最小面。
    /// 经典 <see cref="UIWindow"/>（纯 C#）与 <see cref="UIWindowMono"/>（MonoBehaviour）各自实现，
    /// 使两条窗口路径在 UIModule 的栈管理与生命周期驱动下统一。
    /// </summary>
    /// <remarks>
    /// 仅收 UIModule 对栈成员的多态调用点；窗口自身的资源加载方式不同（经典 InternalLoad / Mono 由 UIModule 自做），
    /// 故加载入口不进本接口。
    /// </remarks>
    public interface IUIWindow
    {
        /// <summary>窗口名称（= 类型 FullName，栈内唯一键）。</summary>
        string WindowName { get; }

        /// <summary>窗口层级。</summary>
        int WindowLayer { get; }

        /// <summary>资源定位地址。</summary>
        string AssetName { get; }

        /// <summary>是否为全屏窗口。</summary>
        bool FullScreen { get; }

        /// <summary>是否加载完毕。</summary>
        bool IsLoadDone { get; }

        /// <summary>资源是否准备完毕。</summary>
        bool IsPrepare { get; }

        /// <summary>是否处于隐藏待关闭态。</summary>
        bool IsHide { get; set; }

        /// <summary>隐藏窗口关闭延时。</summary>
        int HideTimeToClose { get; }

        /// <summary>隐藏关闭定时器 id。</summary>
        int HideTimerId { get; set; }

        /// <summary>窗口深度值（排序）。</summary>
        int Depth { set; }

        /// <summary>窗口可见性。</summary>
        bool Visible { get; set; }

        /// <summary>初始化窗口元数据。</summary>
        void Init(string name, int layer, bool fullScreen, string assetName, bool fromResources, int hideTimeToClose);

        /// <summary>已就绪窗口的复用回调（重复打开时）。</summary>
        void TryInvoke(Action<IUIWindow> prepareCallback, object[] userDatas);

        /// <summary>窗口创建（首次）。</summary>
        void InternalCreate();

        /// <summary>窗口刷新（每次打开）。</summary>
        void InternalRefresh();

        /// <summary>窗口逐帧更新。</summary>
        bool InternalUpdate();

        /// <summary>窗口销毁。</summary>
        void InternalDestroy(bool isShutDown = false);

        /// <summary>取消隐藏关闭定时器。</summary>
        void CancelHideToCloseTimer();
    }
}
