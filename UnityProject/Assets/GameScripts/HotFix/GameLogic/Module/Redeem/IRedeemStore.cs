using System.Collections.Generic;
using GameLogic.BlockBlast;

namespace GameLogic.Redeem
{
    /// <summary>
    /// 一次性去重存储接缝（设计 20 §3.4）。离线单机，「这个码本机兑过没」查本地已兑换集合。
    /// 仿持久化接缝模式，使往返可单测。只去重 <c>once_per_player=1</c> 的码（调用方按定义决定是否查 / 记）。
    /// </summary>
    public interface IRedeemStore
    {
        /// <summary>本机是否已兑换过该（规整后的）码。</summary>
        bool HasRedeemed(string normalizedCode);
        /// <summary>记录该（规整后的）码已兑换。</summary>
        void MarkRedeemed(string normalizedCode);
    }

    /// <summary>
    /// 生产去重存储：经工程既有 <see cref="Persistence.Provider"/>（单键存码集合）持久化（设计 20 §3.4）。
    /// 不另造存储栈，复用 14/19 同一接缝。
    /// </summary>
    /// <remarks>
    /// 本系统专用键 <see cref="Key"/>，不与框架 / 其它系统键冲突。
    /// 集合序列化为换行分隔串（码本身规整为大写字母数字，不含换行）；
    /// 本地单机文件可被篡改 / 截断，<b>反序列化对任意输入产合法集合</b>（空串 / 无键 → 空集合，不抛；同 14 save-system 保底口径）。
    /// PlayerPrefs 经既有 Provider 为非阻塞 KV，不触「禁阻塞 IO」红线。
    /// </remarks>
    public sealed class PersistenceRedeemStore : IRedeemStore
    {
        /// <summary>本系统专用持久化键。</summary>
        public const string Key = "Redeem.Redeemed";

        private HashSet<string> _set;

        private void Ensure()
        {
            if (_set != null) return;
            _set = new HashSet<string>();
            if (Persistence.Provider.TryGet(Key, out var raw) && !string.IsNullOrEmpty(raw))
            {
                foreach (var token in raw.Split('\n'))
                {
                    var t = token.Trim();
                    if (t.Length > 0) _set.Add(t);
                }
            }
        }

        public bool HasRedeemed(string normalizedCode)
        {
            Ensure();
            return _set.Contains(normalizedCode);
        }

        public void MarkRedeemed(string normalizedCode)
        {
            Ensure();
            if (_set.Add(normalizedCode))
            {
                Persistence.Provider.Set(Key, Serialize(_set));
            }
        }

        private static string Serialize(HashSet<string> set)
            => string.Join("\n", set);
    }

    /// <summary>
    /// 测试去重存储：内存集合，往返断言不污染 PlayerPrefs（设计 20 §3.4）。
    /// </summary>
    public sealed class InMemoryRedeemStore : IRedeemStore
    {
        private readonly HashSet<string> _set = new HashSet<string>();

        public bool HasRedeemed(string normalizedCode) => _set.Contains(normalizedCode);
        public void MarkRedeemed(string normalizedCode) => _set.Add(normalizedCode);
    }
}
