namespace GameLogic.BlockBlast
{
    /// <summary>
    /// Block Blast 计分公式的单一信息源。Classic（<c>GameWindow</c>）与合成订单切片
    /// （<c>UIMergeOrderPanel</c>）共用，保证两条路径计分同源——改公式只改这一处。
    /// 合成订单切片仅借 <see cref="ClearScore"/> 作元素生成的内部驱动量，不计入玩家订单得分。
    /// </summary>
    public static class BlockScoring
    {
        /// <summary>落子得分：每个填充格 +1。</summary>
        public static int PlacementScore(int cells) => cells;

        /// <summary>消除得分：被清格数 ×10 + 被清行列数² ×30（平方项让多行同消拿更高分）。</summary>
        public static int ClearScore(int clearedCells, int lines) => clearedCells * 10 + lines * lines * 30;
    }
}
