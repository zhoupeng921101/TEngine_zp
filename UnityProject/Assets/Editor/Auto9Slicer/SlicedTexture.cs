// Vendored from kyubuns/Auto9Slicer v1.1.1 (https://github.com/kyubuns/Auto9Slicer), MIT.
// Border.ToVector4 顺序 = (Left, Bottom, Right, Top)，与 UIAtlasPacker 的 _border_override.json 取值序 [l,b,r,t] 一致。
using UnityEngine;

namespace Auto9Slicer
{
    public class SlicedTexture
    {
        public SlicedTexture(Texture2D texture, Border border)
        {
            Texture = texture;
            Border = border;
        }

        public Texture2D Texture { get; }
        public Border Border { get; }
    }

    public struct Border
    {
        public Border(int left, int bottom, int right, int top)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public Vector4 ToVector4()
        {
            return new Vector4(Left, Bottom, Right, Top);
        }

        public int Left { get; }
        public int Bottom { get; }
        public int Right { get; }
        public int Top { get; }
    }
}
