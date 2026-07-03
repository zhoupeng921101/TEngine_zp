using TEngine;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameLogic
{
    /// <summary>
    /// MonoBehaviour UI 组件基类。
    /// 脚本作为组件挂在 widget prefab 根上，组件即自身、节点访问走 MonoBehaviour 原生，
    /// 引用 / 数值用 <c>[SerializeField]</c> 暴露到 Inspector。
    /// 共享面由 <see cref="UIBaseMono"/> 提供。
    /// </summary>
    public abstract class UIWidgetMono : UIBaseMono
    {
        /// <summary>
        /// 所属的窗口（向上沿父链查找首个 <see cref="UIPanelMono"/>）。
        /// </summary>
        public UIPanelMono OwnerWindow
        {
            get
            {
                var parentUI = _parent;
                while (parentUI != null)
                {
                    if (parentUI is UIPanelMono window)
                    {
                        return window;
                    }

                    parentUI = parentUI.Parent;
                }

                return null;
            }
        }

        /// <summary>
        /// 组件可见性。
        /// </summary>
        public bool Visible
        {
            get => gameObject.activeSelf;
            set
            {
                gameObject.SetActive(value);
                OnSetVisible(value);
            }
        }

        /// <summary>
        /// 逐帧更新（子树遍历复用 <see cref="UIBaseMono.UpdateChildren"/>）。
        /// </summary>
        internal bool InternalUpdate()
        {
            if (!IsPrepare)
            {
                return false;
            }

            return UpdateChildren();
        }

        #region Create

        /// <summary>
        /// 创建窗口内嵌的界面（包裹已存在的根节点）。
        /// </summary>
        public bool Create(UIBaseMono parentUI, GameObject widgetRoot, bool visible = true)
        {
            return CreateImp(parentUI, widgetRoot, visible);
        }

        /// <summary>
        /// 根据资源名创建（取预载模板 Instantiate 实例化）。
        /// 预制须在 <see cref="UIPreloader"/> 预载清单内；缺失时 Instantiate 记 Error 返 null
        /// （不回退同步资源加载——WebGL 运行时禁用同步 LOAD）。
        /// </summary>
        public bool CreateByPath(string resPath, UIBaseMono parentUI, Transform parentTrans = null, bool visible = true)
        {
            GameObject goInst = UIPreloader.Instantiate(resPath, parentTrans);
            if (goInst == null)
            {
                return false;
            }

            if (!Create(parentUI, goInst, visible))
            {
                return false;
            }

            goInst.transform.localScale = Vector3.one;
            goInst.transform.localPosition = Vector3.zero;
            return true;
        }

        /// <summary>
        /// 根据 prefab 实例创建（实例化与 GetComponent 由工厂完成，本方法接管已实例化的根节点）。
        /// <remarks>组件即自身，实例化已在 <see cref="UIBaseMono.CreateWidgetByPrefab{T}"/> 内完成，
        /// 故此处直接接管实例。</remarks>
        /// </summary>
        public bool CreateByPrefab(UIBaseMono parentUI, GameObject widgetRoot, bool visible = true)
        {
            return CreateImp(parentUI, widgetRoot, visible);
        }

        private bool CreateImp(UIBaseMono parentUI, GameObject widgetRoot, bool visible = true)
        {
            if (!CreateBase(widgetRoot))
            {
                return false;
            }

            RestChildCanvas(parentUI);
            _parent = parentUI;
            Parent.ListChild.Add(this);
            Parent.SetUpdateDirty();
            ScriptGenerator();
            RegisterEvent();
            OnCreate();
            OnRefresh();
            IsPrepare = true;

            if (!visible)
            {
                gameObject.SetActive(false);
            }
            else
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
            }

            return true;
        }

        /// <summary>
        /// 校验根节点（Mono 下组件即自身，无需绑 gameObject / transform 字段，仅校验类型）。
        /// </summary>
        private bool CreateBase(GameObject go)
        {
            if (go == null)
            {
                return false;
            }

            // 不维护 name=GetType().Name 影子字段：MonoBehaviour 的 name 即 GameObject 名，
            // 无消费方依赖该影子值，覆盖会误改物体名。
            Log.Assert(rectTransform != null, $"{go.name} ui base element need to be RectTransform");
            return true;
        }

        private void RestChildCanvas(UIBaseMono parentUI)
        {
            if (parentUI == null || parentUI.gameObject == null)
            {
                return;
            }

            Canvas parentCanvas = parentUI.gameObject.GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                return;
            }

            if (gameObject != null)
            {
                var listCanvas = gameObject.GetComponentsInChildren<Canvas>(true);
                for (var index = 0; index < listCanvas.Length; index++)
                {
                    var childCanvas = listCanvas[index];
                    childCanvas.sortingOrder = parentCanvas.sortingOrder + childCanvas.sortingOrder % UIModule.WINDOW_DEEP;
                }
            }
        }

        #endregion

        #region Destroy

        /// <summary>是否已销毁（守卫 OnDestroy 魔法回调与框架销毁的双触发）。</summary>
        private bool _isDestroyed = false;

        /// <summary>
        /// 框架销毁组件（请勿手动调用，由父节点销毁流程驱动）。
        /// 清事件、递归销毁子组件、销毁自身 GameObject。
        /// </summary>
        protected internal void OnDestroyWidget()
        {
            if (_isDestroyed)
            {
                return;
            }

            Parent?.SetUpdateDirty();

            RemoveAllUIEvent();

            foreach (var uiChild in ListChild)
            {
                if (uiChild == null)
                {
                    continue;
                }

                uiChild.OnDestroyWidgetCallback();
                uiChild.OnDestroyWidget();
            }

            _isDestroyed = true;

            if (gameObject != null)
            {
                Object.Destroy(gameObject);
            }
        }

        /// <summary>
        /// 主动销毁组件（从父节点摘除并递归销毁子树）。
        /// </summary>
        public void Destroy()
        {
            if (_parent != null)
            {
                _parent.ListChild.Remove(this);
                OnDestroyWidgetCallback();
                OnDestroyWidget();
            }
        }

        /// <summary>
        /// 用户可重写的销毁回调（避开 Unity 魔法名 OnDestroy）。
        /// protected internal：供同程序集的父节点（窗口 / widget）销毁流程驱动调用。
        /// </summary>
        protected internal virtual void OnDestroyWidgetCallback() { }

        /// <summary>
        /// Unity 魔法回调：外部直接销毁 GameObject（场景卸载 / 手动 Destroy 物体）时的兜底。
        /// 框架正常销毁走 <see cref="OnDestroyWidget"/> / <see cref="Destroy"/> 并已置 <see cref="_isDestroyed"/>，
        /// 二次进入被守卫拦下；仅当框架未先行时，对称摘除父节点 <see cref="UIBaseMono.ListChild"/> 引用，
        /// 避免悬空（计划缺陷 C 的 widget 侧修复）。
        /// </summary>
        private void OnDestroy()
        {
            if (_isDestroyed)
            {
                return;
            }

            _isDestroyed = true;

            if (_parent != null)
            {
                _parent.ListChild.Remove(this);
                _parent.SetUpdateDirty();
            }

            OnDestroyWidgetCallback();
            RemoveAllUIEvent();
        }

        #endregion
    }
}
