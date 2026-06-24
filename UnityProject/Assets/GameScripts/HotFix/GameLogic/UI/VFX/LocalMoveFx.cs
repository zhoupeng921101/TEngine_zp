using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 本地位置滑动（reflow tween）：把一个 RectTransform 的 anchoredPosition 从起点缓动到终点，到时精确落位并自毁（仅销毁组件、不动 GameObject）。
    /// 纯表现层，自驱（Update 计时），用于「列表重排后其余项滑动补位」（订单区左滑 / 元素区增删补位）。
    ///
    /// 使用约束：调用方须在所属容器的 HorizontalLayoutGroup「不再每帧改写子节点 anchoredPosition」的前提下挂本组件
    /// （否则 HLG 与本组件抢位置抖动）。容器侧由 <c>LayoutReflowAnimator</c> 在 tween 期间临时关闭 HLG 保证这一点。
    /// </summary>
    internal sealed class LocalMoveFx : MonoBehaviour
    {
        private RectTransform _rt;
        private Vector2 _start;
        private Vector2 _end;
        private float _dur;
        private float _t;

        private void Awake() { _rt = (RectTransform)transform; }

        /// <summary>
        /// 启动 / 重启一次滑动。可对已在滑动的节点重复调用：从当前位置（<paramref name="start"/>）重新缓动到新终点，无累积。
        /// </summary>
        /// <param name="start">起点（容器本地 anchoredPosition）。</param>
        /// <param name="end">终点（容器本地 anchoredPosition）。</param>
        /// <param name="duration">时长（秒，&gt;0）。</param>
        public void Play(Vector2 start, Vector2 end, float duration)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _start = start;
            _end = end;
            _dur = duration > 0f ? duration : 0.01f;
            _t = 0f;
            _rt.anchoredPosition = start;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t >= _dur)
            {
                _rt.anchoredPosition = _end;
                Destroy(this); // 仅销毁组件，保留 GameObject
                return;
            }
            float k = VFXEase.OutCubic(_t / _dur);
            _rt.anchoredPosition = Vector2.LerpUnclamped(_start, _end, k);
        }
    }
}
