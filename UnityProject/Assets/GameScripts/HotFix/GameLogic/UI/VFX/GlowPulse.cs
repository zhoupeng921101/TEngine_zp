using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 消除预览发光的呼吸脉动（挂在对象池化的发光 Image 上，随 GameObject 显隐自门控：
    /// Update 不在 inactive GameObject 上执行，ClearGhost 隐藏发光层时脉动自然停摆、零开销）。
    /// 在基色（暖金，由窗口在激活时写入 <see cref="BaseColor"/>）的 alpha 上叠加正弦呼吸，
    /// 让玩家更易把「会消除的行列」从静态绿 ghost 中区分出来。仅改 alpha，不改 RGB / scale。
    /// </summary>
    internal sealed class GlowPulse : MonoBehaviour
    {
        // 呼吸周期与 alpha 摆幅：基色 alpha 为中心，±AlphaSwing 内随正弦起伏。
        private const float Period = 0.9f;     // 一次明暗循环约 0.9s
        private const float AlphaSwing = 0.22f; // alpha 上下摆幅（相对基色 alpha）

        private Image _img;
        private float _phase;

        /// <summary>基色（暖金），由窗口在每次激活该发光格时写入。脉动以其 alpha 为中心起伏。</summary>
        public Color BaseColor;

        private void Awake() => _img = GetComponent<Image>();

        // 每次重新激活时错开相位的入口（窗口在 SetActive(true) 前调用），避免整片发光齐刷同步呆板。
        public void Restart(float phaseOffset)
        {
            _phase = phaseOffset;
            ApplyAlpha(0f);
        }

        private void Update()
        {
            _phase += Time.deltaTime;
            ApplyAlpha(_phase);
        }

        private void ApplyAlpha(float t)
        {
            if (_img == null) return;
            float k = Mathf.Sin(t / Period * Mathf.PI * 2f);            // [-1,1]
            float a = Mathf.Clamp01(BaseColor.a + k * AlphaSwing);
            _img.color = new Color(BaseColor.r, BaseColor.g, BaseColor.b, a);
        }
    }
}
