using System.Collections.Generic;

namespace GameLogic.BlockBlast.Core
{
    /// <summary>消除结果：行/列索引列表。</summary>
    public sealed class ClearResult
    {
        public readonly List<int> Rows = new List<int>();
        public readonly List<int> Cols = new List<int>();
    }
}
