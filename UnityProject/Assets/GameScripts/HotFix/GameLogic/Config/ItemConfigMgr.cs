using System.Collections.Generic;
using GameLogic.BlockBlast.Item;

namespace GameLogic.Config
{
    /// <summary>
    /// 道具底层配置管理器（设计 16 §3.5）。
    /// 桥接 Luban 生成的 <c>GameConfig.item.TbItemDef / TbGiftRandom / TbGiftSelect</c> → POCO
    /// （<see cref="ItemDef"/> / <see cref="GiftEntry"/>），让业务侧不直接依赖 Luban 类型；
    /// 按 id 查道具元数据、按 index 聚合礼包池。仿 <see cref="NumericConfigMgr"/>。
    /// </summary>
    /// <remarks>
    /// 加法式：注册表只持有元数据，不读写 <c>MergeOrderState</c>；产出落点由 <c>ItemGrant</c> 接既有系统。
    /// 加载分两路：运行期 <see cref="EnsureLoaded"/> 走 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）；
    /// EditMode 单测经 <see cref="InitForTest"/> 注入，绕 ConfigSystem（纯逻辑可测）。
    /// </remarks>
    public static class ItemConfigMgr
    {
        private static Dictionary<int, ItemDef> _items;                  // id → 道具定义
        private static Dictionary<int, List<GiftEntry>> _giftRandom;     // index → 随机礼包池
        private static Dictionary<int, List<GiftEntry>> _giftSelect;     // index → 自选礼包池

        // ── 单行桥接：Luban 行 → POCO ─────────────────────────────

        /// <summary>道具行：Luban <c>GameConfig.ItemDef</c> → POCO <see cref="ItemDef"/>。</summary>
        public static ItemDef ToItemDef(GameConfig.ItemDef row)
        {
            return new ItemDef
            {
                Id = row.Id,
                Name = row.Name,
                Desc = row.Desc,
                Icon = row.Icon,
                Quality = (int)row.Quality,
                Light = row.Light,
                Automatic = row.Automatic,
                Type = (int)row.Type,
                Param = row.Param,
                UseEffect = row.UseEffect,
                UseValue = row.UseValue,
                UseNum = row.UseNum,
                UseLevel = row.UseLevel,
                Stacking = row.Stacking,
                Term = row.Term,
                TermPrompt = row.TermPrompt,
                TermTime = row.TermTime,
                Compensate = row.Compensate,
                CompensateEmail = row.CompensateEmail,
                JumpList = row.JumpList,
            };
        }

        /// <summary>随机礼包行 → POCO。</summary>
        public static GiftEntry ToGiftEntry(GameConfig.GiftRandom row)
            => new GiftEntry { ItemId = row.ItemId, Num = row.Num, Rate = row.Rate };

        /// <summary>自选礼包行 → POCO。</summary>
        public static GiftEntry ToGiftEntry(GameConfig.GiftSelect row)
            => new GiftEntry { ItemId = row.ItemId, Num = row.Num, Rate = row.Rate };

        // ── 运行期加载（经 ConfigSystem / YooAsset）────────────────

        /// <summary>
        /// 运行期建缓存：经 <c>ConfigSystem</c>（YooAsset，需 Unity 运行时）。首次访问时灌入。
        /// 已灌（含 InitForTest 注入）则直接返回。
        /// </summary>
        public static void EnsureLoaded()
        {
            if (_items != null) return;
            var tables = ConfigSystem.Instance.Tables;

            var itemTable = tables.TbItemDef;
            _items = new Dictionary<int, ItemDef>(itemTable.DataList.Count);
            foreach (var row in itemTable.DataList)
            {
                _items[row.Id] = ToItemDef(row);
            }

            _giftRandom = new Dictionary<int, List<GiftEntry>>();
            foreach (var row in tables.TbGiftRandom.DataList)
            {
                AppendPool(_giftRandom, row.Index, ToGiftEntry(row));
            }

            _giftSelect = new Dictionary<int, List<GiftEntry>>();
            foreach (var row in tables.TbGiftSelect.DataList)
            {
                AppendPool(_giftSelect, row.Index, ToGiftEntry(row));
            }
        }

        private static void AppendPool(Dictionary<int, List<GiftEntry>> pools, int index, GiftEntry entry)
        {
            if (!pools.TryGetValue(index, out var list))
            {
                list = new List<GiftEntry>();
                pools[index] = list;
            }
            list.Add(entry);
        }

        // ── 查询 ───────────────────────────────────────────────

        /// <summary>按 id 查道具；查不到返 null（不抛）。</summary>
        public static ItemDef GetItem(int id)
        {
            EnsureLoaded();
            return _items.TryGetValue(id, out var def) ? def : null;
        }

        /// <summary>按 index 查随机礼包池；查无返空集合（不抛）。</summary>
        public static IReadOnlyList<GiftEntry> GetGiftRandom(int index)
        {
            EnsureLoaded();
            return _giftRandom.TryGetValue(index, out var list)
                ? list
                : System.Array.Empty<GiftEntry>();
        }

        /// <summary>按 index 查自选礼包池；查无返空集合（不抛）。</summary>
        public static IReadOnlyList<GiftEntry> GetGiftSelect(int index)
        {
            EnsureLoaded();
            return _giftSelect.TryGetValue(index, out var list)
                ? list
                : System.Array.Empty<GiftEntry>();
        }

        // ── 测试注入 / 隔离 ─────────────────────────────────────

        /// <summary>
        /// 测试注入口：绕开 ConfigSystem，直接灌 POCO 列表（EditMode / 纯 C# 单测用）。
        /// 灌入后 <see cref="EnsureLoaded"/> 不再触发 ConfigSystem。礼包按 index 聚合。
        /// </summary>
        public static void InitForTest(
            IEnumerable<ItemDef> items,
            IEnumerable<(int index, GiftEntry entry)> randoms = null,
            IEnumerable<(int index, GiftEntry entry)> selects = null)
        {
            _items = new Dictionary<int, ItemDef>();
            if (items != null)
            {
                foreach (var d in items) _items[d.Id] = d;
            }

            _giftRandom = new Dictionary<int, List<GiftEntry>>();
            if (randoms != null)
            {
                foreach (var (index, entry) in randoms) AppendPool(_giftRandom, index, entry);
            }

            _giftSelect = new Dictionary<int, List<GiftEntry>>();
            if (selects != null)
            {
                foreach (var (index, entry) in selects) AppendPool(_giftSelect, index, entry);
            }
        }

        /// <summary>清空缓存（测试隔离用，下次查询会重新走 EnsureLoaded）。</summary>
        public static void ResetForTest()
        {
            _items = null;
            _giftRandom = null;
            _giftSelect = null;
        }
    }
}
