using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 横向布局重排补位动画器（reflow）：让一个挂 <see cref="HorizontalLayoutGroup"/> 的容器在「子节点增删 / 重排」后，
    /// 其余子节点从旧位置滑动到新位置（而非瞬间跳变）。订单区左滑 / 元素区增删补位共用。
    ///
    /// HLG 与位置 tween 冲突的解法：HLG 每帧改写子节点 anchoredPosition，直接 tween 会被它抢位置抖动。
    /// 故本器在 tween 期间临时禁用容器上的 HLG（让 <see cref="LocalMoveFx"/> 成为位置唯一驱动），
    /// tween 全部结束后再启用 HLG——此时子节点已被 tween 落到 HLG 的目标位，启用为视觉无感（不跳）。
    /// 容器宽度由 ContentSizeFitter 维持：目标位在禁用 HLG 前的一次 ForceRebuildLayoutImmediate 已结算，
    /// tween 期间子节点尺寸集合不变、内容宽度不变，故无需让 CSF 重算（保持现状即可）。
    ///
    /// 用法（调用方在数据刷新处）：
    ///   1) 刷新前 <see cref="CaptureBefore"/>：记录当前各活跃子节点 anchoredPosition（含正在滑动中的实时位置）。
    ///   2) 执行数据刷新（SetData + 显隐 SetActive + SetSiblingIndex 重排）。
    ///   3) 刷新后 <see cref="AnimateReflow"/>：结算 HLG 目标位 → 禁用 HLG → 各活跃子节点从旧位滑向新位。
    /// 新出现的子节点（刷新前未记录）直接落到目标位（不滑），避免从无意义起点飞入。
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class LayoutReflowAnimator : MonoBehaviour
    {
        private HorizontalLayoutGroup _hlg;
        // 刷新前各子节点的旧 anchoredPosition 快照（键 = 子节点 RectTransform）。
        private readonly Dictionary<RectTransform, Vector2> _before = new Dictionary<RectTransform, Vector2>();
        // HLG 重新启用倒计时（>0 表示 tween 进行中、HLG 被临时禁用）。
        private float _reenableTimer;

        private void EnsureHlg()
        {
            if (_hlg == null) _hlg = GetComponent<HorizontalLayoutGroup>();
        }

        /// <summary>取容器上的动画器（无则创建），保证单实例。</summary>
        public static LayoutReflowAnimator GetOrAdd(RectTransform container)
        {
            if (container == null) return null;
            var anim = container.GetComponent<LayoutReflowAnimator>();
            if (anim == null) anim = container.gameObject.AddComponent<LayoutReflowAnimator>();
            return anim;
        }

        /// <summary>记录刷新前各活跃子节点的当前 anchoredPosition（reflow 起点）。每次刷新前调用，覆盖上一轮快照。</summary>
        public void CaptureBefore()
        {
            _before.Clear();
            var t = (RectTransform)transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                _before[child] = child.anchoredPosition;
            }
        }

        /// <summary>
        /// 刷新后执行补位滑动：先结算 HLG 目标位，再禁用 HLG，令旧子节点从 CaptureBefore 记录的旧位滑向目标位。
        /// 已记录旧位且与目标位不同的子节点挂 <see cref="LocalMoveFx"/> 滑动；新出现的子节点直接停在目标位。
        /// </summary>
        /// <param name="duration">滑动时长（秒）。</param>
        public void AnimateReflow(float duration)
        {
            EnsureHlg();
            var t = (RectTransform)transform;

            // 结算 HLG 目标位：先确保 HLG 启用并强制立即布局，子节点 anchoredPosition 即为目标位。
            if (_hlg != null) _hlg.enabled = true;
            LayoutRebuilder.ForceRebuildLayoutImmediate(t);

            // 读出每个活跃子节点的目标位（HLG 刚写入）。
            var targets = new List<(RectTransform rt, Vector2 target)>();
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i) as RectTransform;
                if (child == null || !child.gameObject.activeSelf) continue;
                targets.Add((child, child.anchoredPosition));
            }

            // 禁用 HLG，交由 LocalMoveFx 独占 anchoredPosition；tween 结束后再启用。
            if (_hlg != null) _hlg.enabled = false;

            bool anyMoving = false;
            foreach (var (rt, target) in targets)
            {
                if (_before.TryGetValue(rt, out var old))
                {
                    if ((old - target).sqrMagnitude < 0.25f)
                    {
                        rt.anchoredPosition = target; // 几乎没动：直接落位，不挂 tween
                        continue;
                    }
                    var fx = rt.GetComponent<LocalMoveFx>();
                    if (fx == null) fx = rt.gameObject.AddComponent<LocalMoveFx>();
                    fx.Play(old, target, duration);
                    anyMoving = true;
                }
                else
                {
                    rt.anchoredPosition = target; // 新出现的子节点：直接停在目标位，不从无意义起点飞入
                }
            }

            _before.Clear();

            // tween 进行中保持 HLG 禁用，到时再启用（启用时子节点已被 tween 落到目标位，视觉无感）。
            // 无任何节点移动则立即恢复 HLG（保持容器后续正常布局）。
            if (anyMoving) _reenableTimer = duration + 0.02f;
            else { _reenableTimer = 0f; if (_hlg != null) _hlg.enabled = true; }
        }

        private void Update()
        {
            if (_reenableTimer <= 0f) return;
            _reenableTimer -= Time.deltaTime;
            if (_reenableTimer <= 0f)
            {
                _reenableTimer = 0f;
                EnsureHlg();
                if (_hlg != null) _hlg.enabled = true;
            }
        }
    }
}
