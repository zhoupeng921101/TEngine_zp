using System;
using System.Text;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 玩家初始名字生成器（设计 18 §3.2）。纯逻辑：固定前缀 "Player" + 从 62 字符集等概率抽 6 个。
    /// 例 "Player2dfgKL"（前缀 6 + 后缀 6 = 总长 12）。注入 <see cref="System.Random"/> 使单测可固定种子复现。
    /// </summary>
    public static class PlayerNameGenerator
    {
        /// <summary>字符集 = 52 字母 + 10 数字 = 62（spec 逐字）。</summary>
        public const string CHARSET = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        /// <summary>固定前缀。</summary>
        public const string PREFIX = "Player";
        /// <summary>随机后缀长度（可调旋钮）。</summary>
        public const int SUFFIX_LEN = 6;

        /// <summary>生成系统名：前缀 + 6 个从 62 字符集等概率抽取的字符。</summary>
        public static string Generate(System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            var sb = new StringBuilder(PREFIX, PREFIX.Length + SUFFIX_LEN);
            for (int i = 0; i < SUFFIX_LEN; i++)
            {
                sb.Append(CHARSET[rng.Next(CHARSET.Length)]);
            }
            return sb.ToString();
        }
    }
}
