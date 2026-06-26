using System;
using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 收集飞行图标（fly-to-target）：一个临时元素图标从起点先轻微弹起（上抛 + 放大起手），
    /// 再沿缓动飞向终点，到达后自毁并触发到达回调（供目标做一次 punch）。纯表现层，不改数值。
    /// 视觉本体复用棋盘元素 <see cref="ElementWidget"/>（经 owner 窗口的 CreateWidgetByType 创建、SetIcon 贴图），
    /// 与棋盘 / 候选块上的元素图标同一视觉构建口径；本组件只驱动运动时序（计时插值）。
    /// 生命周期自驱（Update 计时），到时经 widget 的 UI 销毁路径（<see cref="UIWidgetMono.Destroy"/>）销毁，
    /// 从 owner 的子组件列表正规摘除，不残留悬空引用。
    ///
    /// 坐标系：起点 / 终点在生成时已换算为「飞行父层本地 anchoredPosition」（父层须为设计居中 overlay，如
    /// MergeOrderWindow 的 m_rect_Content），本组件只在该本地空间内做插值，不再触碰世界坐标，避免父层偏移错算。
    /// </summary>
    public sealed class FlyToTargetFx : MonoBehaviour
    {
        // ── 时序（秒）──
        private const float PopDur = 0.16f;   // 起手弹起段
        private const float FlyDur = 0.42f;   // 飞行段
        private const float PopRiseY = 36f;   // 弹起段额外上抛的本地位移（设计像素）
        private const float PopScale = 1.25f; // 弹起段峰值缩放

        private RectTransform _rt;
        private ElementWidget _widget; // 视觉本体；自毁时经其 Destroy() 走 UI 系统正规销毁
        private Vector2 _start;     // 飞行父层本地起点
        private Vector2 _control;   // 弹起后、飞行起算点（起点上抛后的位置）
        private Vector2 _end;       // 飞行父层本地终点
        private float _baseScale = 1f;
        private Action _onArrive;
        private float _t;
        private bool _delaying;
        private float _delayLeft;

        /// <summary>
        /// 生成一个飞行图标。<paramref name="owner"/> 为创建 widget 的宿主窗口（飞行父层须在其层级内）；
        /// <paramref name="parent"/> 为飞行父层（设计居中 overlay）；
        /// <paramref name="localStart"/> / <paramref name="localEnd"/> 为该父层本地 anchoredPosition。
        /// </summary>
        /// <param name="owner">宿主窗口（UIBaseMono，提供 CreateWidgetByType 创建器）。</param>
        /// <param name="parent">飞行父层（如 m_rect_Content）。</param>
        /// <param name="localStart">起点（父层本地坐标）。</param>
        /// <param name="localEnd">终点（父层本地坐标）。</param>
        /// <param name="spriteName">元素图标 sprite 寻址名（MergeElementVisual.SpriteName）。</param>
        /// <param name="size">图标边长（设计像素）。</param>
        /// <param name="startDelay">起飞前延迟（用于多图标错开 stagger）。</param>
        /// <param name="onArrive">到达终点时回调（目标 punch）。可空。</param>
        public static void Spawn(UIBaseMono owner, RectTransform parent, Vector2 localStart, Vector2 localEnd,
            string spriteName, float size, float startDelay, Action onArrive)
        {
            if (owner == null || parent == null) return;

            // 视觉本体：复用棋盘元素 Widget（资源定位名 == 类名 "ElementWidget"，CreateWidgetByType 走 AddressByFileName 加载）。
            var widget = owner.CreateWidgetByType<ElementWidget>(parent);
            if (widget == null)
            {
                TEngine.Log.Error("[FlyToTargetFx] ElementWidget 加载失败（资源定位名 ElementWidget），飞行图标未创建。");
                return;
            }

            var rt = widget.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // 固定 size 摆根；ElementWidget 图标子节点拉伸填满根，不走 setNativeSize（m_AdaptiveSize=false），
            // 故飞行 size 与 pop/fly 缩放不被原生尺寸覆盖。
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = localStart;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling(); // 飞行图标置于 overlay 顶层，避免被其它 overlay 内容遮挡

            if (!string.IsNullOrEmpty(spriteName)) widget.SetIcon(spriteName);

            var fx = widget.gameObject.AddComponent<FlyToTargetFx>();
            fx._rt = rt;
            fx._widget = widget;
            fx._start = localStart;
            fx._control = localStart + new Vector2(0f, PopRiseY); // 弹起：本地 Y 上正，向上抛
            fx._end = localEnd;
            fx._onArrive = onArrive;
            fx._t = 0f;
            fx._delaying = startDelay > 0f;
            fx._delayLeft = startDelay;

            // 延迟起飞期间先隐藏图标（仅关图标组件，保持根节点 active 使本组件 Update 仍计时），
            // 避免在起点闪现一个静止图标。
            if (fx._delaying) widget.SetIconVisible(false);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_delaying)
            {
                _delayLeft -= dt;
                if (_delayLeft > 0f) return;
                _delaying = false;
                if (_widget != null) _widget.SetIconVisible(true);
            }

            _t += dt;

            if (_t < PopDur)
            {
                // 起手弹起：起点 → 控制点（上抛），同时放大。
                float k = VFXEase.OutQuad(_t / PopDur);
                _rt.anchoredPosition = Vector2.LerpUnclamped(_start, _control, k);
                _rt.localScale = Vector3.one * Mathf.Lerp(_baseScale, _baseScale * PopScale, k);
                return;
            }

            float ft = _t - PopDur;
            if (ft < FlyDur)
            {
                // 飞行：控制点 → 终点，缩放回落到基准。
                float k = VFXEase.OutCubic(ft / FlyDur);
                _rt.anchoredPosition = Vector2.LerpUnclamped(_control, _end, k);
                _rt.localScale = Vector3.one * Mathf.Lerp(_baseScale * PopScale, _baseScale, k);
                return;
            }

            // 到达：精确落到终点 → 回调 → 自毁（经 widget 的 UI 销毁路径，从 owner 子组件列表正规摘除）。
            _rt.anchoredPosition = _end;
            var cb = _onArrive;
            _onArrive = null;
            cb?.Invoke();

            var w = _widget;
            _widget = null;
            if (w != null) w.Destroy();
            else UnityEngine.Object.Destroy(gameObject);
        }
    }
}
