using UnityEngine;

namespace GameLogic
{
    internal sealed class ScalePunch : MonoBehaviour
    {
        private RectTransform _rt;
        private float _t;

        private const float PeakScale = 1.28f;
        private const float RiseDur   = 0.12f;
        private const float FallDur   = 0.18f;
        private const float TotalDur  = RiseDur + FallDur;

        private void Awake() { _rt = (RectTransform)transform; }

        private void Update()
        {
            _t += Time.deltaTime;
            float s;
            if (_t < RiseDur)
                s = Mathf.Lerp(1f, PeakScale, VFXEase.OutCubic(_t / RiseDur));
            else if (_t < TotalDur)
                s = Mathf.Lerp(PeakScale, 1f, VFXEase.OutCubic((_t - RiseDur) / FallDur));
            else
            {
                _rt.localScale = Vector3.one;
                Destroy(this);   // 仅销毁组件，不销毁 GameObject
                return;
            }
            _rt.localScale = Vector3.one * s;
        }
    }
}
