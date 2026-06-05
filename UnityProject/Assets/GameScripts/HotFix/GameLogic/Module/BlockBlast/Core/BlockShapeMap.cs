using System.Collections.Generic;

namespace GameLogic.BlockBlast.Core
{
    /// <summary>
    /// 71 个形状定义（1-42 + 53-70 + 101-111）+ 39 个 COMMON 池 + 首发 [9,39,24] + 早期屏蔽列表。
    /// 来源：原游戏 com.block.juggle v7.7.6 反编译 main_bundle.js。
    /// </summary>
    public static class BlockShapeMap
    {
        public static readonly IReadOnlyDictionary<int, BlockShape> Map = new Dictionary<int, BlockShape>
        {
            { 1,   new BlockShape(1, 1, new[] { 1 }) },
            { 2,   new BlockShape(1, 2, new[] { 1, 1 }) },
            { 3,   new BlockShape(2, 1, new[] { 3 }) },
            { 4,   new BlockShape(1, 3, new[] { 1, 1, 1 }) },
            { 5,   new BlockShape(3, 1, new[] { 7 }) },
            { 6,   new BlockShape(2, 2, new[] { 3, 2 }) },
            { 7,   new BlockShape(1, 4, new[] { 1, 1, 1, 1 }) },
            { 8,   new BlockShape(3, 2, new[] { 4, 7 }) },
            { 9,   new BlockShape(2, 2, new[] { 3, 3 }) },
            { 10,  new BlockShape(3, 2, new[] { 2, 7 }) },
            { 11,  new BlockShape(5, 1, new[] { 31 }) },
            { 12,  new BlockShape(3, 3, new[] { 7, 1, 1 }) },
            { 13,  new BlockShape(3, 3, new[] { 7, 7, 7 }) },
            { 14,  new BlockShape(3, 2, new[] { 3, 6 }) },
            { 15,  new BlockShape(2, 2, new[] { 3, 1 }) },
            { 16,  new BlockShape(2, 3, new[] { 2, 3, 1 }) },
            { 17,  new BlockShape(4, 1, new[] { 15 }) },
            { 18,  new BlockShape(3, 2, new[] { 6, 3 }) },
            { 19,  new BlockShape(2, 3, new[] { 1, 3, 2 }) },
            { 20,  new BlockShape(2, 3, new[] { 2, 3, 2 }) },
            { 21,  new BlockShape(3, 3, new[] { 7, 4, 4 }) },
            { 22,  new BlockShape(1, 5, new[] { 1, 1, 1, 1, 1 }) },
            { 23,  new BlockShape(3, 3, new[] { 4, 4, 7 }) },
            { 24,  new BlockShape(3, 3, new[] { 1, 1, 7 }) },
            { 25,  new BlockShape(2, 3, new[] { 1, 3, 1 }) },
            { 26,  new BlockShape(3, 2, new[] { 7, 2 }) },
            { 27,  new BlockShape(2, 2, new[] { 2, 3 }) },
            { 28,  new BlockShape(2, 2, new[] { 1, 3 }) },
            { 29,  new BlockShape(2, 3, new[] { 1, 1, 3 }) },
            { 30,  new BlockShape(3, 2, new[] { 7, 1 }) },
            { 31,  new BlockShape(2, 3, new[] { 3, 2, 2 }) },
            { 32,  new BlockShape(2, 3, new[] { 3, 1, 1 }) },
            { 33,  new BlockShape(3, 2, new[] { 1, 7 }) },
            { 34,  new BlockShape(3, 2, new[] { 7, 4 }) },
            { 35,  new BlockShape(3, 2, new[] { 7, 7 }) },
            { 36,  new BlockShape(2, 3, new[] { 3, 3, 3 }) },
            { 37,  new BlockShape(2, 2, new[] { 2, 1 }) },
            { 38,  new BlockShape(2, 2, new[] { 1, 2 }) },
            { 39,  new BlockShape(3, 3, new[] { 4, 2, 1 }) },
            { 40,  new BlockShape(3, 3, new[] { 1, 2, 4 }) },
            { 41,  new BlockShape(3, 3, new[] { 1, 2, 4 }) },
            { 42,  new BlockShape(2, 3, new[] { 2, 2, 3 }) },
            { 53,  new BlockShape(2, 3, new[] { 1, 1, 2 }) },
            { 54,  new BlockShape(2, 3, new[] { 2, 2, 1 }) },
            { 55,  new BlockShape(3, 2, new[] { 4, 3 }) },
            { 56,  new BlockShape(3, 2, new[] { 1, 6 }) },
            { 57,  new BlockShape(3, 3, new[] { 7, 2, 2 }) },
            { 58,  new BlockShape(3, 3, new[] { 2, 2, 7 }) },
            { 59,  new BlockShape(3, 3, new[] { 4, 7, 4 }) },
            { 60,  new BlockShape(3, 3, new[] { 1, 7, 1 }) },
            { 61,  new BlockShape(3, 2, new[] { 7, 5 }) },
            { 62,  new BlockShape(3, 2, new[] { 5, 7 }) },
            { 63,  new BlockShape(3, 3, new[] { 3, 2, 3 }) },
            { 64,  new BlockShape(3, 3, new[] { 3, 1, 3 }) },
            { 65,  new BlockShape(3, 3, new[] { 7, 5, 5 }) },
            { 66,  new BlockShape(3, 3, new[] { 5, 5, 7 }) },
            { 67,  new BlockShape(3, 3, new[] { 7, 4, 7 }) },
            { 68,  new BlockShape(3, 3, new[] { 7, 1, 7 }) },
            { 69,  new BlockShape(3, 3, new[] { 2, 7, 2 }) },
            { 70,  new BlockShape(3, 3, new[] { 5, 2, 5 }) },
            { 101, new BlockShape(5, 2, new[] { 31, 31 }) },
            { 102, new BlockShape(2, 5, new[] { 3, 3, 3, 3, 3 }) },
            { 103, new BlockShape(4, 2, new[] { 15, 15 }) },
            { 104, new BlockShape(2, 4, new[] { 3, 3, 3, 3 }) },
            { 105, new BlockShape(5, 3, new[] { 31, 31, 31 }) },
            { 106, new BlockShape(3, 5, new[] { 7, 7, 7, 7, 7 }) },
            { 107, new BlockShape(4, 3, new[] { 15, 15, 15 }) },
            { 108, new BlockShape(3, 4, new[] { 7, 7, 7, 7 }) },
            { 109, new BlockShape(5, 4, new[] { 31, 31, 31, 31 }) },
            { 110, new BlockShape(4, 5, new[] { 15, 15, 15, 15, 15 }) },
            { 111, new BlockShape(5, 5, new[] { 31, 31, 31, 31, 31 }) },
        };

        /// <summary>每个形状包含的格子数（预计算）。</summary>
        public static readonly IReadOnlyDictionary<int, int> BlockNumMap = BuildBlockNumMap();

        /// <summary>所有形状 ID。</summary>
        public static readonly IReadOnlyList<int> AllShapeIds = new List<int>(Map.Keys);

        /// <summary>
        /// 原游戏生产形状池（FirstRoundProTurnPutCtrl.useBlocks），39 个 ID。
        /// 刻意不含 id=1（1×1 单格），避免送给玩家过于简单的"安全块"。
        /// </summary>
        public static readonly IReadOnlyList<int> CommonShapeIds = new[]
        {
            2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
            21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37,
            38, 39, 42,
        };

        /// <summary>
        /// 无尽模式早期屏蔽列表 —— 分数 &lt; EarlyGameBlockScoreThreshold 时
        /// 这些形状会被排除在生产池外，让新手不被难块卡住。
        /// </summary>
        public static readonly HashSet<int> EarlyGameBlockedIds = new HashSet<int>
        {
            8, 30, 33, 34, 37, 38, 39, 40, 53, 54, 55, 56,
        };

        /// <summary>早期屏蔽的分数上限（含），超过此分数不再过滤。</summary>
        public const int EarlyGameBlockScoreThreshold = 10000;

        /// <summary>
        /// 新玩家首发的 3 个形状（来自原游戏 03_board_configs/shapeCfg.json firstIds）。
        /// 9 = 2x2 实心, 39 = 3x3 对角, 24 = 3x3 L
        /// </summary>
        public static readonly IReadOnlyList<int> FirstHandShapeIds = new[] { 9, 39, 24 };

        /// <summary>尝试取形状，未找到返回 null。</summary>
        public static BlockShape Get(int id) => Map.TryGetValue(id, out var s) ? s : null;

        /// <summary>取该形状包含的格子数，未找到返回 0。</summary>
        public static int GetCellCount(int id) => BlockNumMap.TryGetValue(id, out var n) ? n : 0;

        private static Dictionary<int, int> BuildBlockNumMap()
        {
            var dict = new Dictionary<int, int>();
            foreach (var pair in Map)
            {
                int count = 0;
                foreach (var row in pair.Value.Shape)
                {
                    int x = row;
                    while (x != 0) { count += x & 1; x >>= 1; }
                }
                dict[pair.Key] = count;
            }
            return dict;
        }
    }
}
