namespace GameLogic.BlockBlast.Core
{
    /// <summary>
    /// 方块形状定义。Shape[i] 是第 i 行的位掩码，MSB=最左列。
    /// </summary>
    public sealed class BlockShape
    {
        public readonly int Width;
        public readonly int Height;
        /// <summary>每行的位掩码数组，长度 = Height。</summary>
        public readonly int[] Shape;

        public BlockShape(int width, int height, int[] shape)
        {
            Width = width;
            Height = height;
            Shape = shape;
        }
    }
}
