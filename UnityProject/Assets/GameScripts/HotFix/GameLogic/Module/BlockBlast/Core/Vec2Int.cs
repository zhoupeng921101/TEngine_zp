namespace GameLogic.BlockBlast.Core
{
    /// <summary>
    /// 棋盘坐标（X=列, Y=行）。与 UnityEngine.Vector2Int 区分开，避免数据层依赖 Unity。
    /// </summary>
    public readonly struct Vec2Int
    {
        public readonly int X;
        public readonly int Y;

        public Vec2Int(int x, int y) { X = x; Y = y; }

        public override string ToString() => $"({X},{Y})";
    }
}
