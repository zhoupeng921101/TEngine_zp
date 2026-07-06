using UnityEngine;

namespace GameLogic
{
    /// <summary>
    /// 本地位置滑动（reflow tween）：把一个 RectTransform 的 anchoredPosition 从起点缓动到终点，到时精确落位后转空闲（不自毁，保留组件供同节点复用）。
    /// 纯表现层，自驱（Update 计时），用于「列表重排后其余项滑动补位」（订单区扇形补位 / 元素区增删补位）。
    /// 可选叠加 z 轴旋转缓动（订单扇形卡随位置变化同步倾角），由带旋转的 <see cref="Play(Vector2,Vector2,float,float,float)"/> 重载启用；
    /// 三参重载只缓位置、不动旋转（元素区直线补位复用）。
    ///
    /// 空闲不自毁（而非「到时 Destroy」）：常驻节点（如订单卡池）会在补位期间被反复 Play；若到时自毁，则「同一帧内先 Update 落位排 Destroy、
    /// 后又被 Play 重启」会命中已排队的销毁——重启的 tween 在帧尾随组件一并被删、节点冻在重启起点。转空闲后复用 Play 恒安全，
    /// 组件随宿主 GameObject 销毁而回收（合成 token 销毁其 GameObject，订单卡为固定小池、常驻空闲开销可忽略）。
    ///
    /// 使用约束：调用方须在所属容器的 HorizontalLayoutGroup「不再每帧改写子节点 anchoredPosition」的前提下挂本组件
    /// （否则 HLG 与本组件抢位置抖动）。容器侧由 <c>LayoutReflowAnimator</c> 在 tween 期间临时关闭 HLG 保证这一点；
    /// 订单扇形容器则整段关闭 HLG（代码驱动定位），同样满足该前提。
    /// </summary>
    internal sealed class LocalMoveFx : MonoBehaviour
    {
        private RectTransform _rt;
        private Vector2 _start;
        private Vector2 _end;
        private float _startRotZ;
        private float _endRotZ;
        private bool _animateRot;
        private float _dur;
        private float _t;
        private bool _playing;

        private void Awake() { _rt = (RectTransform)transform; }

        /// <summary>
        /// 启动 / 重启一次纯位置滑动（不改旋转）。可对已在滑动的节点重复调用：从 <paramref name="start"/> 重新缓动到新终点，无累积。
        /// </summary>
        /// <param name="start">起点（容器本地 anchoredPosition）。</param>
        /// <param name="end">终点（容器本地 anchoredPosition）。</param>
        /// <param name="duration">时长（秒，&gt;0）。</param>
        public void Play(Vector2 start, Vector2 end, float duration)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _start = start;
            _end = end;
            _animateRot = false;
            _dur = duration > 0f ? duration : 0.01f;
            _t = 0f;
            _playing = true;
            _rt.anchoredPosition = start;
        }

        /// <summary>
        /// 启动 / 重启一次位置 + z 旋转联动滑动（订单扇形补位用）。旋转以度为单位缓动 <paramref name="startRotZ"/> → <paramref name="endRotZ"/>。
        /// </summary>
        /// <param name="startPos">起点 anchoredPosition。</param>
        /// <param name="endPos">终点 anchoredPosition。</param>
        /// <param name="startRotZ">起点 z 旋转（度）。</param>
        /// <param name="endRotZ">终点 z 旋转（度）。</param>
        /// <param name="duration">时长（秒，&gt;0）。</param>
        public void Play(Vector2 startPos, Vector2 endPos, float startRotZ, float endRotZ, float duration)
        {
            if (_rt == null) _rt = (RectTransform)transform;
            _start = startPos;
            _end = endPos;
            _startRotZ = startRotZ;
            _endRotZ = endRotZ;
            _animateRot = true;
            _dur = duration > 0f ? duration : 0.01f;
            _t = 0f;
            _playing = true;
            _rt.anchoredPosition = startPos;
            _rt.localRotation = Quaternion.Euler(0f, 0f, startRotZ);
        }

        private void Update()
        {
            if (!_playing) return;
            _t += Time.deltaTime;
            if (_t >= _dur)
            {
                _rt.anchoredPosition = _end;
                if (_animateRot) _rt.localRotation = Quaternion.Euler(0f, 0f, _endRotZ);
                _playing = false; // 落位后转空闲，保留组件供复用（不自毁，避免同帧「落位排 Destroy 后又被 Play」的重启竞态）
                return;
            }
            float k = VFXEase.OutCubic(_t / _dur);
            _rt.anchoredPosition = Vector2.LerpUnclamped(_start, _end, k);
            if (_animateRot)
                _rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(_startRotZ, _endRotZ, k));
        }
    }
}
