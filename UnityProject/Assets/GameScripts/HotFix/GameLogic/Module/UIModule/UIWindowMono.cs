using System;
using TEngine;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// MonoBehaviour 窗口基类（可挂 prefab 根、字段用 <c>[SerializeField]</c> 暴露到 Inspector）。
    /// 与经典 <see cref="UIWindow"/> 并行：自身即 panel，引用绑定走序列化字段而非命名前缀 / FindChild。
    /// 生命周期由 <see cref="UIModule"/> 显式驱动，不写 Awake/Start/OnEnable/Update 等魔法方法
    /// （<see cref="OnDestroy"/> 为 Unity 魔法回调，故用 <see cref="IsDestroyed"/> 守卫防双触发）。
    /// </summary>
    public abstract class UIWindowMono : MonoBehaviour, IUIWindow
    {
        #region Properties

        private Action<IUIWindow> _prepareCallback;

        private bool _isCreate = false;

        private Canvas _canvas;
        private Canvas[] _childCanvas;
        private GraphicRaycaster _raycaster;
        private GraphicRaycaster[] _childRaycaster;
        private bool _isSortingOrderDirty = false;

        /// <summary>窗口位置矩阵组件（panel 即自身）。</summary>
        // ReSharper disable once InconsistentNaming
        public RectTransform rectTransform => (RectTransform)transform;

        /// <summary>自定义数据集。</summary>
        protected object[] _userDatas;

        /// <summary>自定义数据。</summary>
        public object UserData => _userDatas != null && _userDatas.Length >= 1 ? _userDatas[0] : null;

        /// <summary>自定义数据集。</summary>
        public object[] UserDatas => _userDatas;

        public Canvas Canvas => _canvas;
        public GraphicRaycaster GraphicRaycaster => _raycaster;

        public string WindowName { private set; get; }
        public int WindowLayer { private set; get; }
        public string AssetName { private set; get; }
        public virtual bool FullScreen { private set; get; } = false;
        public bool FromResources { private set; get; }
        public int HideTimeToClose { get; set; }
        public int HideTimerId { get; set; }

        /// <summary>资源是否准备完毕。</summary>
        public bool IsPrepare { protected set; get; }

        /// <summary>是否加载完毕。</summary>
        public bool IsLoadDone { get; private set; }

        /// <summary>UI 是否销毁（守卫 OnDestroy 双触发）。</summary>
        public bool IsDestroyed { get; private set; }

        /// <summary>是否隐藏待关闭。</summary>
        public bool IsHide { set; get; } = false;

        /// <summary>是否需要 Update。</summary>
        protected bool _hasOverrideUpdate = true;

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

            _hasOverrideUpdate = true;
            OnUpdate();
            return _hasOverrideUpdate;
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

        private void _OnSortDepth()
        {
            OnSortDepth();
        }

        protected void Close()
        {
            UIModule.Instance.CloseUI(GetType());
        }

        protected void Hide()
        {
            UIModule.Instance.HideUI(GetType());
        }

        #region 生命周期虚函数

        /// <summary>代码自动生成绑定 / 事件挂载（Mono 路径：引用已由序列化就位，仅挂事件）。</summary>
        protected virtual void ScriptGenerator() { }

        /// <summary>注册事件。</summary>
        protected virtual void RegisterEvent() { }

        /// <summary>窗口创建。</summary>
        protected virtual void OnCreate() { }

        /// <summary>窗口刷新。</summary>
        protected virtual void OnRefresh() { }

        /// <summary>窗口更新。</summary>
        protected virtual void OnUpdate()
        {
            _hasOverrideUpdate = false;
        }

        /// <summary>窗口销毁回调（与经典 UIWindow.OnDestroy 语义一致，避开 Unity 魔法名）。</summary>
        protected virtual void OnDestroyWindow() { }

        /// <summary>层级排序触发。</summary>
        protected virtual void OnSortDepth() { }

        /// <summary>显隐触发。</summary>
        protected virtual void OnSetVisible(bool visible) { }

        #endregion

        #region FindChild / 事件（按需极简版）

        public Transform FindChild(string path)
        {
            var findTrans = rectTransform.Find(path);
            return findTrans != null ? findTrans : null;
        }

        public T FindChildComponent<T>(string path) where T : Component
        {
            var findTrans = rectTransform.Find(path);
            return findTrans != null ? findTrans.gameObject.GetComponent<T>() : null;
        }

        private GameEventMgr _eventMgr;

        protected GameEventMgr EventMgr
        {
            get
            {
                if (_eventMgr == null)
                {
                    _eventMgr = MemoryPool.Acquire<GameEventMgr>();
                }

                return _eventMgr;
            }
        }

        public void AddUIEvent(int eventType, Action handler)
        {
            EventMgr.AddEvent(eventType, handler);
        }

        protected void AddUIEvent<T>(int eventType, Action<T> handler)
        {
            EventMgr.AddEvent(eventType, handler);
        }

        protected void RemoveAllUIEvent()
        {
            if (_eventMgr != null)
            {
                MemoryPool.Release(_eventMgr);
                _eventMgr = null;
            }
        }

        #endregion
    }
}
