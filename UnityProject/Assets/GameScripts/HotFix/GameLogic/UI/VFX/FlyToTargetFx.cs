using System;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 收集飞行图标（fly-to-target）：一个临时 Image 从起点先轻微弹起（上抛 + 放大起手），
    /// 再沿缓动飞向终点，到达后自毁并触发到达回调（供目标做一次 punch）。纯表现层，不改数值。
    /// 生命周期自驱（Update 计时），到时销毁自身 GameObject；sprite 经 <see cref="SetSpriteExtensions"/> 异步加载、
    /// 引用计数由 SetSpriteObject 随 GameObject 销毁自管，与窗口内其它运行时图标同口径。
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
        private Vector2 _start;     // 飞行父层本地起点
        private Vector2 _control;   // 弹起后、飞行起算点（起点上抛后的位置）
        private Vector2 _end;       // 飞行父层本地终点
        private float _baseScale = 1f;
        private Action _onArrive;
        private float _t;
        private bool _delaying;
        private float _delayLeft;

        /// <summary>
        /// 生成一个飞行图标。<paramref name="parent"/> 为飞行父层（设计居中 overlay）；
        /// <paramref name="localStart"/> / <paramref name="localEnd"/> 为该父层本地 anchoredPosition。
        /// </summary>
        /// <param name="parent">飞行父层（如 m_rect_Content）。</param>
        /// <param name="localStart">起点（父层本地坐标）。</param>
        /// <param name="localEnd">终点（父层本地坐标）。</param>
        /// <param name="spriteName">元素图标 sprite 寻址名（MergeElementVisual.SpriteName）。</param>
        /// <param name="size">图标边长（设计像素）。</param>
        /// <param name="startDelay">起飞前延迟（用于多图标错开 stagger）。</param>
        /// <param name="onArrive">到达终点时回调（目标 punch）。可空。</param>
        public static void Spawn(RectTransform parent, Vector2 localStart, Vector2 localEnd,
            string spriteName, float size, float startDelay, Action onArrive)
        {
            if (parent == null) return;

            var go = new GameObject("flyElem", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = localStart;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling(); // 飞行图标置于 overlay 顶层，避免被其它 overlay 内容遮挡

            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            if (!string.IsNullOrEmpty(spriteName)) img.SetSprite(spriteName);

            var fx = go.AddComponent<FlyToTargetFx>();
            fx._rt = rt;
            fx._start = localStart;
            fx._control = localStart + new Vector2(0f, PopRiseY); // 弹起：本地 Y 上正，向上抛
            fx._end = localEnd;
            fx._onArrive = onArrive;
            fx._t = 0f;
            fx._delaying = startDelay > 0f;
            fx._delayLeft = startDelay;

            // 延迟起飞期间先隐藏，避免在起点闪现一个静止图标。
            if (fx._delaying) img.enabled = false;
            fx._image = img;
        }

        private Image _image;

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_delaying)
            {
                _delayLeft -= dt;
                if (_delayLeft > 0f) return;
                _delaying = false;
                if (_image != null) _image.enabled = true;
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

            // 到达：精确落到终点 → 回调 → 自毁。
            _rt.anchoredPosition = _end;
            var cb = _onArrive;
            _onArrive = null;
            cb?.Invoke();
            UnityEngine.Object.Destroy(gameObject);
        }
    }
}
