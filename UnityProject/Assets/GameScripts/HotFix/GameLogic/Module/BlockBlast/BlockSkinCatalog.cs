using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>
    /// 单色皮肤候选池（设计 50 §四 / §五）：blocks_skin 图集实际存在的 sprite 编号集合。
    ///
    /// 本表由 <c>Assets/AssetRaw/UI/Atlas/blocks/blocks_skin/</c> 目录下 <c>blocks_skin_&lt;编号&gt;.png</c>
    /// 实际文件名导出（编号升序，连续区间 1..374）。运行期 sprite 名 = <c>blocks_skin_&lt;编号&gt;</c>。
    /// 资源增删后须重导本表，保持「编号集 = 真实文件集」。当前共 374 个编号，连续区间 1-374，无留空段。
    /// </summary>
    public static class BlockSkinCatalog
    {
        /// <summary>blocks_skin 图集中实际存在的全部编号（升序，连续区间 1..374）。</summary>
        public static readonly IReadOnlyList<int> MonoIds = BuildIds();

        private static int[] BuildIds()
        {
            var ids = new int[374];
            for (int i = 0; i < 374; i++) ids[i] = i + 1;
            return ids;
        }

        private static HashSet<int> _idSet;

        /// <summary>编号是否在真实候选池内（O(1) 校验，用于加载时单色标识合法性，设计 50 §六 / A6）。</summary>
        public static bool Contains(int id)
        {
            if (_idSet == null)
            {
                _idSet = new HashSet<int>();
                foreach (var n in MonoIds) _idSet.Add(n);
            }
            return _idSet.Contains(id);
        }

        /// <summary>运行期 sprite 名（blocks_skin_&lt;编号&gt;）。</summary>
        public static string SpriteName(int id) => "blocks_skin_" + id;

        /// <summary>
        /// 彩色态各方块类型对应的 default_skin 纹理图编号（设计 50 §二）：彩色态也贴图（不再纯色），
        /// 每种方块类型贴 default_skin 里各自那张。索引 = colorIdx（=BlockColor 枚举 0..7
        /// Blue/Green/Yellow/Orange/Red/Steel/Teal/Purple），值 = blocks_main_&lt;编号&gt; 的编号。
        /// 这 8 个编号与 <c>Assets/AssetRaw/UI/Atlas/blocks/default_skin/blocks_main_&lt;n&gt;.png</c>
        /// 实际文件集（1/6/14/15/16/18/19/21）一一对应。
        /// </summary>
        private static readonly int[] ColoredIds = { 14, 15, 16, 21, 1, 6, 18, 19 };

        /// <summary>
        /// 彩色态某方块类型的 default_skin sprite 名（blocks_main_&lt;编号&gt;，散 PNG 按文件名寻址）。
        /// colorIdx 越界取首项保底（不抛），与 <see cref="BlockLayout.ColorOf"/> 越界归 0 同口径。
        /// </summary>
        public static string ColoredSpriteName(int colorIdx)
        {
            int n = (colorIdx >= 0 && colorIdx < ColoredIds.Length) ? ColoredIds[colorIdx] : ColoredIds[0];
            return "blocks_main_" + n;
        }
    }
}
