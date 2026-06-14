using System;
using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 屏蔽字匹配（设计 18 §3.3）。纯逻辑、词表可注入：本轮实现算法 + 注入接缝，单测用夹具词表，
    /// 不阻塞于真实词表缺失（真实词表来源延后 O6）。
    /// </summary>
    /// <remarks>
    /// 默认匹配 = 大小写不敏感子串包含（含任一屏蔽词即不洁）。空词表 = 永远通过（IsClean=true），
    /// 是刻意的安全默认（去变现 / 不阻塞玩家：真实词表未接时不误拦）。更复杂的变形/拼音/间隔符绕过匹配是后续增强，
    /// 可注入接缝使替换匹配策略不动调用方。
    /// </remarks>
    public static class ProfanityFilter
    {
        /// <summary>是否大小写不敏感（默认 true）。</summary>
        public static bool IgnoreCase = true;

        /// <summary>名字是否「干净」（不含任何屏蔽词）。空词表/空名永远通过。</summary>
        public static bool IsClean(string name, IReadOnlyCollection<string> wordList)
        {
            if (wordList == null || wordList.Count == 0) return true;   // 无词表 = 不拦
            if (string.IsNullOrEmpty(name)) return true;
            var cmp = IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            foreach (var w in wordList)
            {
                if (string.IsNullOrEmpty(w)) continue;
                if (name.IndexOf(w, cmp) >= 0) return false;
            }
            return true;
        }
    }
}
