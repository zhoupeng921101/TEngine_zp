using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TEngine;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// MonoBehaviour UI 基类：经典 <see cref="UIBase"/> 共享面的 Mono 端口（节点访问走 MonoBehaviour 原生）。
    /// 由 <see cref="UIWindowMono"/> 与 <see cref="UIWidgetMono"/> 共同继承，承载父子链、FindChild、事件、
    /// CreateWidget 工厂家族与子树更新（<see cref="UpdateChildren"/>）。
    /// 与经典路径并行；引用绑定走 <c>[SerializeField]</c> 而非 FindChild / UIBindComponent。
    /// </summary>
    public abstract class UIBaseMono : MonoBehaviour
    {
        #region Node / Parent / Child

        /// <summary>矩阵位置组件（transform 即自身）。</summary>
        // ReSharper disable once InconsistentNaming
        public RectTransform rectTransform => (RectTransform)transform;

        /// <summary>所属 UI 父节点。</summary>
        protected UIBaseMono _parent = null;

        /// <summary>UI 父节点。</summary>
        public UIBaseMono Parent => _parent;

        /// <summary>UI 子组件列表。</summary>
        internal readonly List<UIWidgetMono> ListChild = new List<UIWidgetMono>();

        /// <summary>存在 Update 更新的 UI 子组件列表。</summary>
        protected List<UIWidgetMono> _listUpdateChild = null;

        /// <summary>是否持有有效的 Update 子列表缓存。</summary>
        protected bool _updateListValid = false;

        /// <summary>是否标记脏排序。</summary>
        protected bool _isSortingOrderDirty = false;

        #endregion

        #region UserData / State

        /// <summary>自定义数据集。</summary>
        protected object[] _userDatas;

        /// <summary>自定义数据。</summary>
        public object UserData => _userDatas != null && _userDatas.Length >= 1 ? _userDatas[0] : null;

        /// <summary>自定义数据集。</summary>
        public object[] UserDatas => _userDatas;

        /// <summary>资源是否准备完毕。</summary>
        public bool IsPrepare { protected set; get; }

        /// <summary>是否需要 Update（OnUpdate 未被重写时由其置 false）。</summary>
        protected bool _hasOverrideUpdate = true;

        #endregion

        #region 生命周期虚函数

        /// <summary>代码自动生成绑定 / 事件挂载（Mono 路径：引用已由序列化就位，仅挂事件）。</summary>
        protected virtual void ScriptGenerator() { }

        /// <summary>注册事件。</summary>
        protected virtual void RegisterEvent() { }

        /// <summary>创建。</summary>
        protected virtual void OnCreate() { }

        /// <summary>刷新。</summary>
        protected virtual void OnRefresh() { }

        /// <summary>更新（未重写时置 _hasOverrideUpdate=false，标记无需逐帧驱动）。</summary>
        protected virtual void OnUpdate()
        {
            _hasOverrideUpdate = false;
        }

        /// <summary>层级排序触发。</summary>
        protected virtual void OnSortDepth() { }

        /// <summary>显隐触发。</summary>
        protected virtual void OnSetVisible(bool visible) { }

        #endregion

        #region 子树更新 / 排序

        /// <summary>
        /// 更新子组件树（经典 <c>UIWindow.InternalUpdate</c> 与 <c>UIWidget.InternalUpdate</c> 中
        /// 对 <see cref="ListChild"/> 遍历部分的合并版，供窗口与 widget 复用）。
        /// </summary>
        /// <returns>本节点是否仍需逐帧更新。</returns>
        protected bool UpdateChildren()
        {
            List<UIWidgetMono> listNextUpdateChild = null;
            if (ListChild != null && ListChild.Count > 0)
            {
                listNextUpdateChild = _listUpdateChild;
                var updateListValid = _updateListValid;
                List<UIWidgetMono> listChild;
                if (!updateListValid)
                {
                    if (listNextUpdateChild == null)
                    {
                        listNextUpdateChild = new List<UIWidgetMono>();
                        _listUpdateChild = listNextUpdateChild;
                    }
                    else
                    {
                        listNextUpdateChild.Clear();
                    }

                    listChild = ListChild;
                }
                else
                {
                    listChild = listNextUpdateChild;
                }

                for (int i = 0; i < listChild.Count; i++)
                {
                    var uiWidget = listChild[i];

                    if (uiWidget == null)
                    {
                        continue;
                    }

                    var needValid = uiWidget.InternalUpdate();

                    if (!updateListValid && needValid)
                    {
                        listNextUpdateChild.Add(uiWidget);
                    }
                }

                if (!updateListValid)
                {
                    _updateListValid = true;
                }
            }

            bool needUpdate;
            if (listNextUpdateChild is not { Count: > 0 })
            {
                _hasOverrideUpdate = true;
                OnUpdate();
                needUpdate = _hasOverrideUpdate;
            }
            else
            {
                OnUpdate();
                needUpdate = true;
            }

            return needUpdate;
        }

        internal void SetUpdateDirty()
        {
            _updateListValid = false;
            if (Parent != null)
            {
                Parent.SetUpdateDirty();
            }
        }

        /// <summary>触发自身及子组件的层级排序。</summary>
        protected void _OnSortDepth()
        {
            if (ListChild != null)
            {
                for (int i = 0; i < ListChild.Count; i++)
                {
                    ListChild[i].OnSortDepth();
                }
            }

            OnSortDepth();
        }

        #endregion

        #region FindChild

        public Transform FindChild(string path)
        {
            return FindChildImp(rectTransform, path);
        }

        public Transform FindChild(Transform trans, string path)
        {
            return FindChildImp(trans, path);
        }

        public T FindChildComponent<T>(string path) where T : Component
        {
            return FindChildComponentImp<T>(rectTransform, path);
        }

        public T FindChildComponent<T>(Transform trans, string path) where T : Component
        {
            return FindChildComponentImp<T>(trans, path);
        }

        private static Transform FindChildImp(Transform transform, string path)
        {
            var findTrans = transform.Find(path);
            return findTrans != null ? findTrans : null;
        }

        private static T FindChildComponentImp<T>(Transform transform, string path) where T : Component
        {
            var findTrans = transform.Find(path);
            if (findTrans != null)
            {
                return findTrans.gameObject.GetComponent<T>();
            }

            return null;
        }

        #endregion

        #region UIEvent

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

        protected void AddUIEvent<T, U>(int eventType, Action<T, U> handler)
        {
            EventMgr.AddEvent(eventType, handler);
        }

        protected void AddUIEvent<T, U, V>(int eventType, Action<T, U, V> handler)
        {
            EventMgr.AddEvent(eventType, handler);
        }

        protected void AddUIEvent<T, U, V, W>(int eventType, Action<T, U, V, W> handler)
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

        #region CreateWidget 工厂家族

        /// <summary>
        /// 创建 UIWidgetMono 通过父 UI 位置节点路径（资源实例已存在父物体，无需异步）。
        /// </summary>
        public T CreateWidget<T>(string goPath, bool visible = true) where T : UIWidgetMono
        {
            var goRootTrans = FindChild(goPath);

            if (goRootTrans != null)
            {
                return CreateWidget<T>(goRootTrans.gameObject, visible);
            }

            return null;
        }

        /// <summary>
        /// 创建 UIWidgetMono 通过父 UI 位置节点路径（指定父节点）。
        /// </summary>
        public T CreateWidget<T>(Transform parentTrans, string goPath, bool visible = true) where T : UIWidgetMono
        {
            var goRootTrans = FindChild(parentTrans, goPath);
            if (goRootTrans != null)
            {
                return CreateWidget<T>(goRootTrans.gameObject, visible);
            }

            return null;
        }

        /// <summary>
        /// 创建 UIWidgetMono 通过游戏物体（路径 ①：包裹已存在子节点，组件即取自该节点）。
        /// </summary>
        public T CreateWidget<T>(GameObject goRoot, bool visible = true) where T : UIWidgetMono
        {
            if (goRoot == null)
            {
                return null;
            }

            var widget = goRoot.GetComponent<T>();
            if (widget == null)
            {
                Log.Error($"CreateWidget: GameObject [{goRoot.name}] 缺少组件 {typeof(T).Name}，请检查 prefab 是否已挂脚本。");
                return null;
            }

            if (widget.Create(this, goRoot, visible))
            {
                return widget;
            }

            return null;
        }

        /// <summary>
        /// 创建 UIWidgetMono 通过资源定位地址（路径 ①：LoadGameObject 已实例化、组件在 prefab 根）。
        /// </summary>
        public T CreateWidgetByPath<T>(Transform parentTrans, string assetLocation, bool visible = true) where T : UIWidgetMono
        {
            GameObject goInst = UIModule.Resource.LoadGameObject(assetLocation, parent: parentTrans);
            return CreateWidget<T>(goInst, visible);
        }

        /// <summary>
        /// 创建 UIWidgetMono 通过资源定位地址（异步，路径 ①）。
        /// </summary>
        public async UniTask<T> CreateWidgetByPathAsync<T>(Transform parentTrans, string assetLocation, bool visible = true) where T : UIWidgetMono
        {
            GameObject goInst = await UIModule.Resource.LoadGameObjectAsync(assetLocation, parentTrans, gameObject.GetCancellationTokenOnDestroy());
            return CreateWidget<T>(goInst, visible);
        }

        /// <summary>
        /// 根据 prefab 或模板创建新的 widget（路径 ②：实例化 prefab → GetComponent 取组件）。
        /// </summary>
        public T CreateWidgetByPrefab<T>(GameObject goPrefab, Transform parentTrans = null, bool visible = true) where T : UIWidgetMono
        {
            if (goPrefab == null)
            {
                return null;
            }

            if (parentTrans == null)
            {
                parentTrans = rectTransform;
            }

            GameObject goInst = UnityEngine.Object.Instantiate(goPrefab, parentTrans);
            var widget = goInst.GetComponent<T>();
            if (widget == null)
            {
                Log.Error($"CreateWidgetByPrefab: prefab [{goPrefab.name}] 缺少组件 {typeof(T).Name}，请检查 prefab 是否已挂脚本。");
                UnityEngine.Object.Destroy(goInst);
                return null;
            }

            if (widget.CreateByPrefab(this, goInst, visible))
            {
                return widget;
            }

            return null;
        }

        /// <summary>
        /// 通过 UI 类型来创建 widget（资源定位地址 = 类型名，走路径 ①）。
        /// </summary>
        public T CreateWidgetByType<T>(Transform parentTrans, bool visible = true) where T : UIWidgetMono
        {
            return CreateWidgetByPath<T>(parentTrans, typeof(T).Name, visible);
        }

        /// <summary>
        /// 通过 UI 类型来创建 widget（异步）。
        /// </summary>
        public async UniTask<T> CreateWidgetByTypeAsync<T>(Transform parentTrans, bool visible = true) where T : UIWidgetMono
        {
            return await CreateWidgetByPathAsync<T>(parentTrans, typeof(T).Name, visible);
        }

        #endregion
    }
}
