using UnityEngine;
using UnityEngine.UI;

namespace GameLogic
{
    /// <summary>
    /// 轻量自动弹字：scale 弹入 → 停留 → 上移淡出 → 自毁。
    /// 不依赖 DOTween，纯 Update 驱动，挂在文本 GameObject 上。
    /// </summary>
    public sealed class BurstText : MonoBehaviour
    {
        private Text _text;
        private RectTransform _rt;
        private float _t;
        private Vector2 _startPos;

        private const float PopDur = 0.22f;
        private const float HoldDur = 0.25f;
        private const float FadeDur = 0.55f;
        private const float RiseDist = 90f;

        public static void Spawn(Transform parent, float designCx, float designCy, string content, int fontSize, Color color)
        {
            var txt = UGuiFactory.CreateText(parent, "Burst", designCx, designCy, 700, 120, content, fontSize, color);
            var bt = txt.gameObject.AddComponent<BurstText>();
            bt._text = txt;
            bt._rt = txt.rectTransform;
            bt._startPos = bt._rt.anchoredPosition;
            bt._rt.localScale = Vector3.one * 0.3f;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_t < PopDur)
            {
                float k = _t / PopDur;
                _rt.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, EaseOutBack(k));
            }
            else if (_t < PopDur + HoldDur)
            {
                _rt.localScale = Vector3.one * 1.1f;
            }
            else if (_t < PopDur + HoldDur + FadeDur)
            {
                float k = (_t - PopDur - HoldDur) / FadeDur;
                _rt.anchoredPosition = _startPos + Vector2.up * (RiseDist * k);
                var c = _text.color; c.a = Mathf.Lerp(1f, 0f, k); _text.color = c;
            }
            else
            {
                Object.Destroy(gameObject);
            }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
    }
}
