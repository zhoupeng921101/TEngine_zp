using UnityEngine;

namespace GameLogic
{
    internal static class VFXEase
    {
        internal static float OutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + c1 * xm * xm;
        }
        internal static float OutCubic(float x) { float r = 1f - x; return 1f - r * r * r; }
        internal static float OutQuad(float x) { float r = 1f - x; return 1f - r * r; }
    }
}
