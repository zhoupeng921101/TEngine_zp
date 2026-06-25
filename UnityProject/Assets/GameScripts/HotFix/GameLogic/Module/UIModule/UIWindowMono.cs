using System;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// MonoBehaviour 窗口基类（可挂 prefab 根、字段用 <c>[SerializeField]</c> 暴露到 Inspector）。
    /// 与经典 <see cref="UIWindow"/> 并行：自身即 panel，引用绑定走序列化字段而非命名前缀 / FindChild；
    /// 共享面（父子链 / FindChild / 事件 / CreateWidget / 子树更新）继承自 <see cref="UIBaseMono"/>。
    /// 生命周期由 <see cref="UIModule"/> 显式驱动，不写 Awake/Start/OnEnable/Update 等魔法方法
    /// （<see cref="OnDestroy"/> 为 Unity 魔法回调，故用 <see cref="IsDestroyed"/> 守卫防双触发）。
    /// </summary>
    public abstract class UIWindowMono : UIBaseMono, IUIWindow
    {
        #region Properties

        private Action<IUIWindow> _prepareCallback;

        private bool _isCreate = false;

        private Canvas _canvas;
        private Canvas[] _childCanvas;
        private GraphicRaycaster _raycaster;
        private GraphicRaycaster[] _childRaycaster;

        public Canvas Canvas => _canvas;
        public GraphicRaycaster GraphicRaycaster => _raycaster;

        public string WindowName { private set; get; }
        public int WindowLayer { private set; get; }
        public string AssetName { private set; get; }
        public virtual bool FullScreen { private set; get; } = false;
        public bool FromResources { private set; get; }
        public int HideTimeToClose { get; set; }
        public int HideTimerId { get; set; }

        /// <summary>是否加载完毕。</summary>
        public bool IsLoadDone { get; private set; }

        /// <summary>UI 是否销毁（守卫 OnDestroy 双触发）。</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>是否隐藏待关闭。</summary>
        public bool IsHide { set; get; } = false;

        /// <summary>窗口深度值（排序）。</summary>
        public int Depth
        {
            get => _canvas != null ? _canvas.sortingOrder : 0;
            set
            {
                if (_canvas == null)
                {
                    return;
                }

                if (_canvas.sortingOrder == value)
                {
                    return;
                }

                var oldOrder = _canvas.sortingOrder;
                _canvas.sortingOrder = value;
                for (int i = 0; i < _childCanvas.Length; i++)
                {
                    var canvas = _childCanvas[i];
                    if (canvas != _canvas)
                    {
                        canvas.sortingOrder = value + (canvas.sortingOrder - oldOrder);
                    }
                }

                if (Visible)
                {
                    _OnSortDepth();
                }
                else
                {
                    _isSortingOrderDirty = true;
                }
            }
        }

        /// <summary>窗口可见性。</summary>
        public bool Visible
        {
            get => _canvas != null && _canvas.gameObject.layer == UIModule.WINDOW_SHOW_LAYER;
            set
            {
                if (_canvas == null)
                {
                    return;
                }

                int setLayer = value ? UIModule.WINDOW_SHOW_LAYER : UIModule.WINDOW_HIDE_LAYER;
                if (_canvas.gameObject.layer == setLayer)
                {
                    return;
                }

                _canvas.gameObject.layer = setLayer;
                for (int i = 0; i < _childCanvas.Length; i++)
                {
                    _childCanvas[i].gameObject.layer = setLayer;
                }

                if (value && _isCreate)
                {
                    _isSortingOrderDirty = false;
                    _OnSortDepth();
                }

                Interactable = value;

                if (_isCreate)
                {
                    OnSetVisible(value);
                }
            }
        }

        /// <summary>窗口交互性。</summary>
        private bool Interactable
        {
            get => _raycaster != null && _raycaster.enabled;
            set
            {
                if (_raycaster == null)
                {
                    return;
                }

                _raycaster.enabled = value;
                for (int i = 0; i < _childRaycaster.Length; i++)
                {
                    _childRaycaster[i].enabled = value;
                }
            }
        }

        #endregion

        public void Init(string name, int layer, bool fullScreen, string assetName, bool fromResources, int hideTimeToClose)
        {
            WindowName = name;
            WindowLayer = layer;
            FullScreen = fullScreen;
            AssetName = assetName;
            FromResources = fromResources;
            HideTimeToClose = hideTimeToClose;
        }

        /// <summary>
        /// 初始化 panel（= 经典 <c>UIWindow.Handle_Completed</c> 的「对自己」版，1:1 复刻 Canvas 设置与就绪流程）。
        /// </summary>
        internal void Setup(GameObject panel, Action<IUIWindow> prepareCallback, object[] userDatas)
        {
            _prepareCallback = prepareCallback;
            _userDatas = userDatas;
            IsLoadDone = true;

            panel.name = GetType().Name;
            panel.transform.localPosition = Vector3.zero;

            _canvas = panel.GetComponent<Canvas>();
            if (_canvas == null)
            {
                throw new Exception($"Not found {nameof(Canvas)} in panel {WindowName}");
            }

            _canvas.overrideSorting = true;
            _canvas.sortingOrder = 0;
            _canvas.sortingLayerName = "Default";

            _raycaster = panel.GetComponent<GraphicRaycaster>();
            _childCanvas = panel.GetComponentsInChildren<Canvas>(true);
            _childRaycaster = panel.GetComponentsInChildren<GraphicRaycaster>(true);

            IsPrepare = true;
            _prepareCallback?.Invoke(this);
        }

        public void TryInvoke(Action<IUIWindow> prepareCallback, object[] userDatas)
        {
            CancelHideToCloseTimer();
            _userDatas = userDatas;
            if (IsPrepare)
            {
                prepareCallback?.Invoke(this);
            }
            else
            {
                _prepareCallback = prepareCallback;
            }
        }

        public void InternalCreate()
        {
            if (_isCreate == false)
            {
                _isCreate = true;
                ScriptGenerator();
                RegisterEvent();
                OnCreate();
            }
        }

        public void InternalRefresh()
        {
            OnRefresh();
        }

        public bool InternalUpdate()
        {
            if (!IsPrepare || !Visible)
            {
                return false;
            }

            return UpdateChildren();
        }

        public void InternalDestroy(bool isShutDown = false)
        {
            if (IsDestroyed)
            {
                return;
            }

            _isCreate = false;
            RemoveAllUIEvent();
            _prepareCallback = null;

            // 销毁子组件（语义对应经典 UIWindow.InternalDestroy 对 ListChild 的遍历）。
            for (int i = 0; i < ListChild.Count; i++)
            {
                var uiChild = ListChild[i];
                if (uiChild == null)
                {
                    continue;
                }

                uiChild.OnDestroyWidgetCallback();
                uiChild.OnDestroyWidget();
            }

            OnDestroyWindow();
            IsDestroyed = true;

            if (!isShutDown)
            {
                CancelHideToCloseTimer();
            }

            // panel 即自身：销毁 GameObject。OnDestroy 魔法回调会再进一次，被 IsDestroyed 守卫拦下。
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        public void CancelHideToCloseTimer()
        {
            IsHide = false;
            if (HideTimerId > 0)
            {
                ModuleSystem.GetModule<ITimerModule>().RemoveTimer(HideTimerId);
                HideTimerId = 0;
            }
        }

        /// <summary>
        /// Unity 魔法回调。框架外部直接销毁 GameObject（场景卸载等）时的兜底清理；
        /// 框架显式 <see cref="InternalDestroy"/> 已先行，二次进入被 <see cref="IsDestroyed"/> 守卫拦下。
        /// </summary>
        private void OnDestroy()
        {
            if (IsDestroyed)
            {
                return;
            }

            _isCreate = false;
            RemoveAllUIEvent();
            _prepareCallback = null;
            OnDestroyWindow();
            IsDestroyed = true;
        }

        protected void Close()
        {
            UIModule.Instance.CloseUI(GetType());
        }

        protected void Hide()
        {
            UIModule.Instance.HideUI(GetType());
        }

        /// <summary>窗口销毁回调（与经典 UIWindow.OnDestroy 语义一致，避开 Unity 魔法名）。</summary>
        protected virtual void OnDestroyWindow() { }
    }
}
