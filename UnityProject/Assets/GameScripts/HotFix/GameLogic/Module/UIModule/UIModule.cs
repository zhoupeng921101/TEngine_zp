using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameLogic;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// UI管理模块。
    /// 窗口体系统一为 MonoBehaviour（<see cref="UIPanelMono"/>）：脚本挂 prefab 根、引用走 [SerializeField]。
    /// 实例须先加载 prefab 再 GetComponent 取得，故无法像纯 C# 窗口那样加载前同步入栈，
    /// 加载期由 <see cref="_pendingMono"/> 占位去重并承接「加载途中请求关闭」的竞态。
    /// </summary>
    public sealed partial class UIModule : Singleton<UIModule>, IUpdate
    {
        // 核心字段
        private static Transform _instanceRoot = null;          // UI根节点变换组件
        private bool _enableErrorLog = true;                    // 是否启用错误日志
        private Camera _uiCamera = null;                        // UI专用摄像机
        private readonly List<UIPanelMono> _uiStack = new List<UIPanelMono>(128); // 窗口堆栈
        // Mono 窗口加载中状态表。键 = 窗口名（类型 FullName），值 = 加载途中是否已请求关闭。
        // 实例在加载完成后才存在，无法加载前入栈去重，故用此表在加载期占位。
        // 加载途中 CloseUI/CloseAll 命中时把值置 true；LoadMonoWindow 在 await 后据此放弃入栈并销毁实例
        // （等价经典窗口 Handle_Completed 的 IsDestroyed 兜底，修复「加载中关闭被吞、窗口照开」竞态）。
        // 任何结束路径都必须移除键，否则该窗口再也打不开。
        private readonly Dictionary<string, bool> _pendingMono = new Dictionary<string, bool>();
        private ErrorLogger _errorLogger;                       // 错误日志记录器

        // 常量定义
        public const int LAYER_DEEP = 2000;
        public const int WINDOW_DEEP = 100;
        public const int WINDOW_HIDE_LAYER = 2; // Ignore Raycast
        public const int WINDOW_SHOW_LAYER = 5; // UI

        // 资源加载接口
        public static IUIResourceLoader Resource;

        /// <summary>
        /// UI根节点访问属性
        /// </summary>
        public static Transform UIRoot => _instanceRoot;

        /// <summary>
        /// UI摄像机访问属性
        /// </summary>
        public Camera UICamera => _uiCamera;

        /// <summary>
        /// 模块初始化（自动调用）。
        /// 1. 查找场景中的UIRoot
        /// 2. 初始化资源加载器
        /// 3. 配置错误日志系统
        /// </summary>
        protected override void OnInit()
        {
            var uiRoot = GameObject.Find("UIRoot");
            if (uiRoot != null)
            {
                _instanceRoot = uiRoot.GetComponentInChildren<Canvas>()?.transform;
                _uiCamera = uiRoot.GetComponentInChildren<Camera>();
            }
            else
            {
                Log.Fatal("UIRoot not found !");
                return;
            }

            Resource = new UIResourceLoader();

            UnityEngine.Object.DontDestroyOnLoad(_instanceRoot.parent != null ? _instanceRoot.parent : _instanceRoot);

            _instanceRoot.gameObject.layer = LayerMask.NameToLayer("UI");

            if (Debugger.Instance != null)
            {
                switch (Debugger.Instance.ActiveWindowType)
                {
                    case DebuggerActiveWindowType.AlwaysOpen:
                        _enableErrorLog = true;
                        break;

                    case DebuggerActiveWindowType.OnlyOpenWhenDevelopment:
                        _enableErrorLog = Debug.isDebugBuild;
                        break;

                    case DebuggerActiveWindowType.OnlyOpenInEditor:
                        _enableErrorLog = Application.isEditor;
                        break;

                    default:
                        _enableErrorLog = false;
                        break;
                }
                if (_enableErrorLog)
                {
                    _errorLogger = new ErrorLogger(this);
                }
            }
        }

        /// <summary>
        /// 模块释放（自动调用）。
        /// 1. 清理错误日志系统
        /// 2. 关闭所有窗口
        /// 3. 销毁UI根节点
        /// </summary>
        protected override void OnRelease()
        {
            if (_errorLogger != null)
            {
                _errorLogger.Dispose();
                _errorLogger = null;
            }
            CloseAll(isShutDown:true);
            if (_instanceRoot != null && _instanceRoot.parent != null)
            {
                UnityEngine.Object.Destroy(_instanceRoot.parent.gameObject);
            }
        }

        #region 设置安全区域

        /// <summary>
        /// 设置屏幕安全区域（异形屏支持）。
        /// </summary>
        /// <param name="safeRect">安全区域矩形（基于屏幕像素坐标）。</param>
        public static void ApplyScreenSafeRect(Rect safeRect)
        {
            CanvasScaler scaler = UIRoot.GetComponentInParent<CanvasScaler>();
            if (scaler == null)
            {
                Log.Error($"Not found {nameof(CanvasScaler)} !");
                return;
            }

            // Convert safe area rectangle from absolute pixels to UGUI coordinates
            float rateX = scaler.referenceResolution.x / Screen.width;
            float rateY = scaler.referenceResolution.y / Screen.height;
            float posX = (int)(safeRect.position.x * rateX);
            float posY = (int)(safeRect.position.y * rateY);
            float width = (int)(safeRect.size.x * rateX);
            float height = (int)(safeRect.size.y * rateY);

            float offsetMaxX = scaler.referenceResolution.x - width - posX;
            float offsetMaxY = scaler.referenceResolution.y - height - posY;

            // 注意：安全区坐标系的原点为左下角
            var rectTrans = UIRoot.transform as RectTransform;
            if (rectTrans != null)
            {
                rectTrans.offsetMin = new Vector2(posX, posY); //锚框状态下的屏幕左下角偏移向量
                rectTrans.offsetMax = new Vector2(-offsetMaxX, -offsetMaxY); //锚框状态下的屏幕右上角偏移向量
            }
        }

        /// <summary>
        /// 模拟IPhoneX异形屏
        /// </summary>
        public static void SimulateIPhoneXNotchScreen()
        {
            Rect rect;
            if (Screen.height > Screen.width)
            {
                // 竖屏Portrait
                float deviceWidth = 1125;
                float deviceHeight = 2436;
                rect = new Rect(0f / deviceWidth, 102f / deviceHeight, 1125f / deviceWidth, 2202f / deviceHeight);
            }
            else
            {
                // 横屏Landscape
                float deviceWidth = 2436;
                float deviceHeight = 1125;
                rect = new Rect(132f / deviceWidth, 63f / deviceHeight, 2172f / deviceWidth, 1062f / deviceHeight);
            }

            Rect safeArea = new Rect(Screen.width * rect.x, Screen.height * rect.y, Screen.width * rect.width, Screen.height * rect.height);
            ApplyScreenSafeRect(safeArea);
        }

        #endregion

        /// <summary>
        /// 获取所有层级下顶部的窗口名称。
        /// </summary>
        public string GetTopWindow()
        {
            if (_uiStack.Count == 0)
            {
                return string.Empty;
            }

            UIPanelMono topWindow = _uiStack[^1];
            return topWindow.WindowName;
        }

        /// <summary>
        /// 获取指定层级下顶部的窗口名称。
        /// </summary>
        public string GetTopWindow(int layer)
        {
            UIPanelMono lastOne = null;
            for (int i = 0; i < _uiStack.Count; i++)
            {
                if (_uiStack[i].WindowLayer == layer)
                    lastOne = _uiStack[i];
            }

            if (lastOne == null)
                return string.Empty;

            return lastOne.WindowName;
        }

        /// <summary>
        /// 是否有任意窗口正在加载。
        /// </summary>
        public bool IsAnyLoading()
        {
            for (int i = 0; i < _uiStack.Count; i++)
            {
                var window = _uiStack[i];
                if (window.IsLoadDone == false)
                    return true;
            }

            return _pendingMono.Count > 0;
        }

        /// <summary>
        /// 查询窗口是否存在。
        /// </summary>
        /// <typeparam name="T">界面类型。</typeparam>
        /// <returns>是否存在。</returns>
        public bool HasWindow<T>()
        {
            return HasWindow(typeof(T));
        }

        /// <summary>
        /// 查询窗口是否存在。
        /// </summary>
        /// <param name="type">界面类型。</param>
        /// <returns>是否存在。</returns>
        public bool HasWindow(Type type)
        {
            return IsContains(type.FullName);
        }

        /// <summary>
        /// 异步打开窗口。
        /// </summary>
        /// <param name="userDatas">用户自定义数据。</param>
        public void ShowUIAsync<T>(params System.Object[] userDatas) where T : UIPanelMono
        {
            ShowUIImp(typeof(T), true, userDatas);
        }

        /// <summary>
        /// 异步打开窗口。
        /// </summary>
        /// <param name="type">界面类型。</param>
        /// <param name="userDatas">用户自定义数据。</param>
        public void ShowUIAsync(Type type, params System.Object[] userDatas)
        {
            ShowUIImp(type, true, userDatas);
        }

        /// <summary>
        /// 同步打开窗口。
        /// </summary>
        /// <typeparam name="T">窗口类。</typeparam>
        /// <param name="userDatas">用户自定义数据。</param>
        public void ShowUI<T>(params System.Object[] userDatas) where T : UIPanelMono
        {
            ShowUIImp(typeof(T), false, userDatas);
        }

        /// <summary>
        /// 异步打开窗口并等待返回实例。
        /// </summary>
        /// <param name="userDatas">用户自定义数据。</param>
        public async UniTask<T> ShowUIAsyncAwait<T>(params System.Object[] userDatas) where T : UIPanelMono
        {
            UIPanelMono window = await ShowUIAwaitImp(typeof(T), true, userDatas);
            return window as T;
        }

        /// <summary>
        /// 同步打开窗口。
        /// </summary>
        /// <param name="type"></param>
        /// <param name="userDatas"></param>
        public void ShowUI(Type type, params System.Object[] userDatas)
        {
            ShowUIImp(type, false, userDatas);
        }

        /// <summary>
        /// 打开窗口（<see cref="UIPanelMono"/> 子类）：先加载 prefab，再 <c>GetComponent</c> 取实例。
        /// 已在栈中（已加载完成的复用）→ 重新置顶并触发刷新回调；正在加载中 → 去重忽略本次请求。
        /// </summary>
        private void ShowUIImp(Type type, bool isAsync, params System.Object[] userDatas)
        {
            string windowName = type.FullName;

            // 已在栈中（已加载完成的复用）→ 重新置顶并触发刷新回调。
            if (IsContains(windowName))
            {
                UIPanelMono exist = GetWindow(windowName);
                Pop(exist);
                Push(exist);
                exist.TryInvoke(OnWindowPrepare, userDatas);
                return;
            }

            // 正在加载中 → 去重，忽略本次请求（且清掉可能已置位的关闭标记，本次请求覆盖为「要打开」）。
            if (_pendingMono.ContainsKey(windowName))
            {
                _pendingMono[windowName] = false;
                return;
            }

            _pendingMono[windowName] = false;
            LoadMonoWindow(type, windowName, isAsync, userDatas).Forget();
        }

        /// <summary>
        /// 打开窗口并等待加载完成，返回窗口实例（<see cref="ShowUIAsyncAwait{T}"/> 的内部实现）。
        /// 复用 <see cref="ShowUIImp"/> 触发加载，再轮询 _uiStack 直至命中实例且 IsLoadDone（60s 超时兜底）。
        /// 已在栈中则即时返回。
        /// </summary>
        private async UniTask<UIPanelMono> ShowUIAwaitImp(Type type, bool isAsync, System.Object[] userDatas)
        {
            string windowName = type.FullName;
            ShowUIImp(type, isAsync, userDatas);

            float time = 0f;
            while (true)
            {
                UIPanelMono window = GetWindow(windowName);
                if (window != null && window.IsLoadDone)
                {
                    return window;
                }

                // 加载途中被请求关闭：pending 已清、栈中也无 → 不再等待，返回 null。
                if (!_pendingMono.ContainsKey(windowName) && window == null)
                {
                    return null;
                }

                time += Time.deltaTime;
                if (time > 60f)
                {
                    return GetWindow(windowName);
                }
                await UniTask.Yield();
            }
        }

        /// <summary>
        /// 加载 Mono 窗口 prefab，取组件、Init、入栈，再走统一的 OnWindowPrepare。
        /// 任何失败路径（加载失败 / 组件缺失 / 加载途中被请求关闭）都必须从 _pendingMono 移除键。
        /// </summary>
        private async UniTaskVoid LoadMonoWindow(Type type, string windowName, bool isAsync, System.Object[] userDatas)
        {
            try
            {
                WindowAttribute attribute = Attribute.GetCustomAttribute(type, typeof(WindowAttribute)) as WindowAttribute;
                int layer = attribute != null ? attribute.WindowLayer : (int)UILayer.UI;
                bool fullScreen = attribute != null && attribute.FullScreen;
                string assetName = attribute != null && !string.IsNullOrEmpty(attribute.Location) ? attribute.Location : type.Name;
                int hideTimeToClose = attribute != null ? attribute.HideTimeToClose : 10;
                bool fromResources = attribute != null && attribute.FromResources;

                // 加载分支：fromResources 走 UnityEngine.Resources.Load 同步实例化（UILogPanel 等内置资源依赖，
                // 非 YooAsset bundle，不受 WebGL 同步加载限制）；否则一律走 YooAsset 异步加载。
                // 注：isAsync 仅影响调用方是否 await 实例返回（ShowUIAwaitImp），不再切换同步/异步资源加载——
                // 客户端运行时禁用同步资源加载 API（WebGL 不支持），故 YooAsset 路径恒异步。
                GameObject panel;
                if (fromResources)
                {
                    panel = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(assetName), UIRoot);
                }
                else
                {
                    panel = await Resource.LoadGameObjectAsync(assetName, parent: UIRoot);
                }

                if (panel == null)
                {
                    _pendingMono.Remove(windowName);
                    Log.Error($"Load mono window prefab failed: {windowName} (location={assetName})");
                    return;
                }

                // 加载途中被请求关闭（CloseUI/CloseAll 命中 pending 并置位）→ 放弃入栈、销毁实例、清键。
                // 这是「加载中关闭」竞态的兜底：实例加载完成时若关闭请求已先到，不让其入栈成僵尸窗。
                if (_pendingMono.TryGetValue(windowName, out bool closeRequested) && closeRequested)
                {
                    _pendingMono.Remove(windowName);
                    UnityEngine.Object.Destroy(panel);
                    return;
                }

                UIPanelMono window = panel.GetComponent<UIPanelMono>();
                if (window == null)
                {
                    _pendingMono.Remove(windowName);
                    UnityEngine.Object.Destroy(panel);
                    Log.Error($"Mono window prefab has no {nameof(UIPanelMono)} component: {windowName}");
                    return;
                }

                window.Init(windowName, layer, fullScreen, assetName, fromResources, hideTimeToClose);

                // 加载完成，移除 pending。先入栈（OnWindowPrepare 的排序/显隐遍历需窗口已在栈），
                // 再 Setup 抓 Canvas / 置就绪 / 回调 OnWindowPrepare。
                _pendingMono.Remove(windowName);
                Push(window);
                window.Setup(panel, OnWindowPrepare, userDatas);
            }
            catch (Exception e)
            {
                _pendingMono.Remove(windowName);
                Log.Error($"Load mono window exception: {windowName}\n{e}");
            }
        }

        /// <summary>
        /// 关闭窗口。
        /// </summary>
        /// <typeparam name="T">窗口类型</typeparam>
        public void CloseUI<T>() where T : UIPanelMono
        {
            CloseUI(typeof(T));
        }

        public void CloseUI(Type type)
        {
            string windowName = type.FullName;

            // 加载途中关闭：实例尚未存在，置「已请求关闭」标记，由 LoadMonoWindow 在 await 后兜底销毁。
            if (_pendingMono.ContainsKey(windowName))
            {
                _pendingMono[windowName] = true;
                return;
            }

            UIPanelMono window = GetWindow(windowName);
            if (window == null)
                return;

            window.InternalDestroy();
            Pop(window);
            OnSortWindowDepth(window.WindowLayer);
            OnSetWindowVisible();
        }

        public void HideUI<T>() where T : UIPanelMono
        {
            HideUI(typeof(T));
        }

        public void HideUI(Type type)
        {
            string windowName = type.FullName;
            UIPanelMono window = GetWindow(windowName);
            if (window == null)
            {
                return;
            }

            if (window.HideTimeToClose <= 0)
            {
                CloseUI(type);
                return;
            }

            window.CancelHideToCloseTimer();
            window.Visible = false;
            window.IsHide = true;
            window.HideTimerId = GameModule.Timer.AddTimer((arg) =>
            {
                CloseUI(type);
            },window.HideTimeToClose);

            if (window.FullScreen)
            {
                OnSetWindowVisible();
            }
        }

        /// <summary>
        /// 关闭所有窗口。
        /// </summary>
        public void CloseAll(bool isShutDown = false)
        {
            // 加载中的窗口一并标记关闭，避免 await 返回后又入栈成僵尸窗。
            MarkAllPendingClose();

            for (int i = 0; i < _uiStack.Count; i++)
            {
                UIPanelMono window = _uiStack[i];
                window.InternalDestroy(isShutDown);
            }

            _uiStack.Clear();
        }

        /// <summary>
        /// 关闭所有窗口除了。
        /// </summary>
        public void CloseAllWithOut(UIPanelMono withOut)
        {
            MarkAllPendingClose();

            for (int i = _uiStack.Count - 1; i >= 0; i--)
            {
                UIPanelMono window = _uiStack[i];
                if (ReferenceEquals(window, withOut))
                {
                    continue;
                }

                window.InternalDestroy();
                _uiStack.RemoveAt(i);
            }
        }

        /// <summary>
        /// 关闭所有窗口除了。
        /// </summary>
        public void CloseAllWithOut<T>() where T : UIPanelMono
        {
            MarkAllPendingClose();

            for (int i = _uiStack.Count - 1; i >= 0; i--)
            {
                UIPanelMono window = _uiStack[i];
                if (window.GetType() == typeof(T))
                {
                    continue;
                }

                window.InternalDestroy();
                _uiStack.RemoveAt(i);
            }
        }

        /// <summary>
        /// 把所有加载中的窗口标记为「已请求关闭」，使其加载完成后不入栈（CloseAll/CloseAllWithOut 用）。
        /// </summary>
        private void MarkAllPendingClose()
        {
            if (_pendingMono.Count == 0)
            {
                return;
            }

            var keys = new List<string>(_pendingMono.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                _pendingMono[keys[i]] = true;
            }
        }

        /// <summary>
        /// 窗口被外部直接销毁（场景卸载 / 手动 Destroy GameObject）时的兜底出栈。
        /// 由 <see cref="UIPanelMono.OnDestroy"/> 在框架显式 InternalDestroy 未先行时调用：
        /// 从栈摘除、重排同层深度、重算可见性（缺陷 C 的窗口侧修复）。
        /// </summary>
        internal void NotifyWindowDestroyed(UIPanelMono window)
        {
            if (window == null)
            {
                return;
            }

            int index = _uiStack.IndexOf(window);
            if (index < 0)
            {
                return;
            }

            _uiStack.RemoveAt(index);
            OnSortWindowDepth(window.WindowLayer);
            OnSetWindowVisible();
        }

        private void OnWindowPrepare(UIPanelMono window)
        {
            window.InternalCreate();
            window.InternalRefresh();
            OnSortWindowDepth(window.WindowLayer);
            OnSetWindowVisible();
        }

        private void OnSortWindowDepth(int layer)
        {
            int depth = layer * LAYER_DEEP;
            for (int i = 0; i < _uiStack.Count; i++)
            {
                if (_uiStack[i].WindowLayer == layer)
                {
                    _uiStack[i].Depth = depth;
                    depth += WINDOW_DEEP;
                }
            }
        }

        private void OnSetWindowVisible()
        {
            bool isHideNext = false;
            for (int i = _uiStack.Count - 1; i >= 0; i--)
            {
                UIPanelMono window = _uiStack[i];
                if (isHideNext == false)
                {
                    if (window.IsHide)
                    {
                        continue;
                    }
                    window.Visible = true;
                    if (window.IsPrepare && window.FullScreen)
                    {
                        isHideNext = true;
                    }
                }
                else
                {
                    window.Visible = false;
                }
            }
        }

        /// <summary>
        /// 异步获取窗口。
        /// </summary>
        public async UniTask<T> GetUIAsyncAwait<T>(CancellationToken cancellationToken = default) where T : UIPanelMono
        {
            string windowName = typeof(T).FullName;
            var window = GetWindow(windowName);
            if (window == null)
            {
                return null;
            }

            var ret = window as T;

            if (ret == null)
            {
                return null;
            }

            if (ret.IsLoadDone)
            {
                return ret;
            }

            float time = 0f;
            while (!ret.IsLoadDone)
            {
                time += Time.deltaTime;
                if (time > 60f)
                {
                    break;
                }
                await UniTask.Yield(cancellationToken: cancellationToken);
            }
            return ret;
        }

        /// <summary>
        /// 异步获取窗口。
        /// </summary>
        /// <param name="callback">回调。</param>
        public void GetUIAsync<T>(Action<T> callback) where T : UIPanelMono
        {
            string windowName = typeof(T).FullName;
            var window = GetWindow(windowName);
            if (window == null)
            {
                return;
            }

            var ret = window as T;

            if (ret == null)
            {
                return;
            }

            GetUIAsyncImp(callback).Forget();

            async UniTaskVoid GetUIAsyncImp(Action<T> ctx)
            {
                float time = 0f;
                while (!ret.IsLoadDone)
                {
                    time += Time.deltaTime;
                    if (time > 60f)
                    {
                        break;
                    }
                    await UniTask.Yield();
                }
                ctx?.Invoke(ret);
            }
        }

        private UIPanelMono GetWindow(string windowName)
        {
            for (int i = 0; i < _uiStack.Count; i++)
            {
                UIPanelMono window = _uiStack[i];
                if (window.WindowName == windowName)
                {
                    return window;
                }
            }

            return null;
        }

        private bool IsContains(string windowName)
        {
            for (int i = 0; i < _uiStack.Count; i++)
            {
                UIPanelMono window = _uiStack[i];
                if (window.WindowName == windowName)
                {
                    return true;
                }
            }

            return false;
        }

        private void Push(UIPanelMono window)
        {
            // 如果已经存在
            if (IsContains(window.WindowName))
            {
                throw new GameFrameworkException($"Window {window.WindowName} is exist.");
            }

            // 获取插入到所属层级的位置
            int insertIndex = -1;
            for (int i = 0; i < _uiStack.Count; i++)
            {
                if (window.WindowLayer == _uiStack[i].WindowLayer)
                {
                    insertIndex = i + 1;
                }
            }

            // 如果没有所属层级，找到相邻层级
            if (insertIndex == -1)
            {
                for (int i = 0; i < _uiStack.Count; i++)
                {
                    if (window.WindowLayer > _uiStack[i].WindowLayer)
                    {
                        insertIndex = i + 1;
                    }
                }
            }

            // 如果是空栈或没有找到插入位置
            if (insertIndex == -1)
            {
                insertIndex = 0;
            }

            // 最后插入到堆栈
            _uiStack.Insert(insertIndex, window);
        }

        private void Pop(UIPanelMono window)
        {
            // 从堆栈里移除
            _uiStack.Remove(window);
        }

        public void OnUpdate()
        {
            if (_uiStack == null)
            {
                return;
            }

            int count = _uiStack.Count;
            for (int i = 0; i < _uiStack.Count; i++)
            {
                if (_uiStack.Count != count)
                {
                    break;
                }

                var window = _uiStack[i];
                window.InternalUpdate();
            }
        }
    }
}
