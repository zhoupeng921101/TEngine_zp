using System.Collections.Generic;
using GameLogic.Config;

namespace GameLogic.BlockBlast.Item
{
    /// <summary>
    /// 基础背包容器（设计 16 §3.8）：与配置弱关联的纯计数容器。
    /// 叠加上限 999、可叠占 1 格 / id、不可叠每个占 1 格、容量上限 100 格。
    /// </summary>
    /// <remarks>
    /// 叠不叠查 <c>ItemConfigMgr.GetItem(id).Stacking</c> 决定（查不到当不可叠，保守占格）。
    /// 溢出（超 999 / 超 100 格）本轮丢弃 + 记 TODO，不发邮件（设计 16 §七 O6）。
    /// 本类不读写 MergeOrderState，可纯逻辑单测。
    ///
    /// 服务端权威投影（塔罗收集·客户端段）：服务端道具持有(PlayerDoc.ItemHoldings)是唯一事实源,
    /// 本容器经 <see cref="ApplyAuthoritativeSnapshot"/>(进主游戏整份覆盖)与 <see cref="SetAuthoritativeCount"/>
    /// (交付/合成响应单条对账)接权威值——该两入口按「id→计数」直落可叠表、不受 999/100 客户端上限钳制
    /// (上限是本地增量 Add 的防灌爆规则,权威对齐以服务端为准;显示仍经 <see cref="FormatCount"/> 收口)。
    /// 本地缓存丢失可由服务端快照重建(可丢缓存,合 data-authority 白名单),不接存档。
    /// </remarks>
    public sealed class ItemBag
    {
        /// <summary>单 id 叠加上限。</summary>
        public const int StackCap = 999;
        /// <summary>背包总格数上限。</summary>
        public const int SlotCap = 100;

        // 可叠：id → 总数（占 1 格 / id）
        private readonly Dictionary<int, int> _stack = new Dictionary<int, int>();
        // 不可叠：id → 个数（每个占 1 格）
        private readonly Dictionary<int, int> _nonStack = new Dictionary<int, int>();

        /// <summary>已用格数 = 可叠种类数 + 不可叠个数总和。</summary>
        public int SlotUsed
        {
            get
            {
                int used = _stack.Count;
                foreach (var n in _nonStack.Values) used += n;
                return used;
            }
        }

        /// <summary>剩余可用格数。</summary>
        public int SlotFree => SlotCap - SlotUsed;

        /// <summary>该 id 的总持有量（可叠取总数 / 不可叠取个数）。</summary>
        public int Count(int id)
        {
            if (_stack.TryGetValue(id, out var s)) return s;
            if (_nonStack.TryGetValue(id, out var n)) return n;
            return 0;
        }

        /// <summary>查该 id 是否可叠（查不到当不可叠，保守）。</summary>
        private static bool IsStackable(int id)
        {
            var def = ItemConfigMgr.GetItem(id);
            return def != null && def.Stacking == 1;
        }

        /// <summary>
        /// 放入 <paramref name="count"/> 个，返回实际放入量（受容量 / 叠加上限约束可能 &lt; count；差额丢弃记 TODO）。
        /// count≤0 返 0。
        /// </summary>
        public int Add(int id, int count)
        {
            if (count <= 0) return 0;
            return IsStackable(id) ? AddStackable(id, count) : AddNonStackable(id, count);
        }

        private int AddStackable(int id, int count)
        {
            bool existed = _stack.TryGetValue(id, out var cur);
            if (!existed)
            {
                if (SlotFree <= 0) return 0; // 无空格，拒绝（TODO: 邮件补发 O6）
                cur = 0;
            }
            int target = cur + count;
            if (target > StackCap) target = StackCap; // 夹 999，差额丢弃（TODO: 邮件补发 O6）
            int placed = target - cur;
            if (placed <= 0) return 0;                // 已满 999，放不进
            _stack[id] = target;
            return placed;
        }

        private int AddNonStackable(int id, int count)
        {
            int canPlace = SlotFree;
            if (canPlace <= 0) return 0;              // 无空格，拒绝（TODO: 邮件补发 O6）
            int placed = count < canPlace ? count : canPlace;
            _nonStack.TryGetValue(id, out var cur);
            _nonStack[id] = cur + placed;
            return placed;                            // 差额丢弃记 TODO（O6）
        }

        /// <summary>
        /// 移除 <paramref name="count"/> 个。移除到 0 后该 id 从背包消失（释放格子）。
        /// 持有不足时返 false 且不改动；count≤0 返 false。
        /// </summary>
        public bool Remove(int id, int count)
        {
            if (count <= 0) return false;
            if (_stack.TryGetValue(id, out var s))
            {
                if (s < count) return false;
                if (s == count) _stack.Remove(id);
                else _stack[id] = s - count;
                return true;
            }
            if (_nonStack.TryGetValue(id, out var n))
            {
                if (n < count) return false;
                if (n == count) _nonStack.Remove(id);
                else _nonStack[id] = n - count;
                return true;
            }
            return false;
        }

        /// <summary>清空背包。</summary>
        public void Clear()
        {
            _stack.Clear();
            _nonStack.Clear();
        }

        // ── 服务端权威对齐入口(塔罗收集·客户端段)────────────────────
        // 与增量 Add/Remove 分开:权威对齐是「set 绝对值」语义,直落可叠表(服务端持有本质是 id→计数),
        // 不受 999/100 客户端上限钳制。count 夹到 int 上限防溢出(服务端 long)。

        /// <summary>
        /// 单条权威绝对值对齐(交付掉碎片 / 合成扣碎片的响应回带余额)。count ≤ 0 → 该 id 移除(释放格子)。
        /// </summary>
        public void SetAuthoritativeCount(int id, long count)
        {
            _nonStack.Remove(id); // 权威对齐统一按计数存可叠表;若此前被本地 Add 误入不可叠表,一并收编
            if (count <= 0)
            {
                _stack.Remove(id);
                return;
            }
            _stack[id] = count > int.MaxValue ? int.MaxValue : (int)count;
        }

        /// <summary>
        /// 整份权威快照覆盖(进主游戏响应的道具持有全集):先清空、再逐条对齐。
        /// holdings 为 null 视作空集(全清)。服务端未含的 id 即被清除,保证投影不残留脏数据。
        /// </summary>
        public void ApplyAuthoritativeSnapshot(IEnumerable<(int id, long count)> holdings)
        {
            Clear();
            if (holdings == null) return;
            foreach (var (id, count) in holdings)
            {
                if (id <= 0) continue;
                SetAuthoritativeCount(id, count);
            }
        }

        /// <summary>显示用计数格式化：≥999 返 "999+"，否则原值（设计 16 §3.8）。纯函数。</summary>
        public static string FormatCount(int n) => n >= StackCap ? StackCap + "+" : n.ToString();
    }
}
