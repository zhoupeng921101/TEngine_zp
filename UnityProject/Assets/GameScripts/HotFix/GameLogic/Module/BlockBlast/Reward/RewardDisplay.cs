using UnityEngine;
using GameLogic.Config;
using GameLogic.BlockBlast.Item;
using GameLogic.BlockBlast.Numeric;

namespace GameLogic.BlockBlast.Reward
{
    /// <summary>
    /// 通用奖励展示归一层（设计 17 §3.2/§3.3/§3.4）。把三种异构奖励产出
    /// （道具 <see cref="GrantPayload"/> / 盲盒 <see cref="ChestReward"/> / 裸 num_id+数量）
    /// 归一成统一的 <see cref="RewardView"/>，任何 UI 拿到 RewardView 用同一套渲染。
    /// </summary>
    /// <remarks>
    /// 纯逻辑、可单测：只读既有产出结构与元数据注册表（<see cref="NumericConfigMgr"/> /
    /// <see cref="ItemConfigMgr"/> / <see cref="MergeElementVisual"/>），产出不含 Unity UI 类型的
    /// POCO（唯一 Unity 类型 Color 为值类型），不碰 YooAsset / Unity 运行时。
    /// 加法式：既有产出 / 发奖路径只读不写，旧路径零行为变化（设计 17 读前必看四条边界）。
    /// 本层 6 档品质色是新单一事实源；旧 4 档 <c>NumericDisplay.QualityColor</c> 冻结不删不改、
    /// 不被本层调用，收编是独立任务（设计 17 §七 O3）。
    /// </remarks>
    public static class RewardDisplay
    {
        // ─────────────────────── 各源 → RewardView 转换（§3.2）───────────────────────

        /// <summary>① 道具系统产出 → 展示。</summary>
        public static RewardView From(GrantPayload p)
        {
            switch (p.Kind)
            {
                case GrantKind.Numeric:
                    return FromNumeric(p.TargetId, p.Amount);
                case GrantKind.Pattern:
                    return FromPattern((MergeElement)p.TargetId, p.Level, p.Amount);
                case GrantKind.GiftSelect:
                    return GiftView(p.TargetId, p.Times, select: true);
                case GrantKind.GiftRandom:
                    return GiftView(p.TargetId, p.Times, select: false);
                default:
                    // None：纯持有材料，TargetId 是道具 id（见 ItemGrant.Resolve default 分支 def.Id）。
                    return FromItem(p.TargetId, p.Amount);
            }
        }

        /// <summary>② 盲盒产出 → 展示（ChestRewardKind 走本层私有展示映射，§2.1 注 / §3.2）。</summary>
        public static RewardView From(ChestReward r)
        {
            switch (r.Kind)
            {
                case ChestRewardKind.Soul:
                    // 灵力无 num_id（数值系统约定常量只有 Exp/Piety/Diamond/Energy），走本层私有占位（设计 17 §2.1 注 / §七 O6）。
                    return ChestCurrencyView("Soul", r.Amount);
                case ChestRewardKind.Energy:
                    return FromNumeric(NumericConfigMgr.Energy, r.Amount);
                case ChestRewardKind.Pattern:
                    // 盲盒只带等级不带具体图案，展示用代表图案 Diamond + 等级文案降级（设计 17 §3.2 注 / §七 O7）。
                    return FromPattern(MergeElement.Diamond, r.PatternLevel, r.Amount);
                case ChestRewardKind.UndoCharge:
                    return FunctionView("UndoCharge", r.Amount);
                case ChestRewardKind.WishCharge:
                    return FunctionView("WishCharge", r.Amount);
                default:
                    return default;
            }
        }

        /// <summary>③ 裸货币 num_id + 数量 → 展示（查数值注册表；查无降级不抛）。</summary>
        public static RewardView FromNumeric(int numId, long amount)
        {
            var e = NumericConfigMgr.Get(numId); // 查不到返 null（不抛）
            string icon = e != null ? e.IconName : null;
            int name = e != null ? e.NameTextId : 0;
            int qual = e != null ? e.Quality : 1; // 查无品质退化白
            return new RewardView(icon, name, CountText(amount),
                                  QualityColor(qual), RewardBadge.Currency, amount);
        }

        /// <summary>④ 道具 id + 数量 → 展示（查道具注册表，材料 / 礼包道具；查无降级不抛）。</summary>
        public static RewardView FromItem(int itemId, long count)
        {
            var d = ItemConfigMgr.GetItem(itemId);
            string icon = d != null ? d.Icon : null;
            int name = d != null ? d.Name : 0;
            int qual = d != null ? d.Quality : 1;
            // 道具 Type：5 自选礼包 / 6 随机礼包 → Gift 角标；其余按材料。
            var badge = (d != null && (d.Type == 5 || d.Type == 6)) ? RewardBadge.Gift : RewardBadge.Material;
            return new RewardView(icon, name, CountText(count), QualityColor(qual), badge, count);
        }

        /// <summary>⑤ 图案 + 等级 + 数量 → 展示（图标用约定名；品质按等级映射，§3.3 注）。</summary>
        public static RewardView FromPattern(MergeElement el, int level, long count)
        {
            string icon = "pattern_" + (int)el;
            return new RewardView(icon, 0, PatternCountText(level, count),
                                  QualityColor(PatternQuality(level)), RewardBadge.Pattern, count);
        }

        /// <summary>礼包占位展示（自选 / 随机；CountText 按开启次数 Times，§3.2）。</summary>
        public static RewardView GiftView(int giftIndex, int times, bool select)
        {
            string icon = select ? "gift_select" : "gift_random";
            // 礼包本身是一个待开的盒，展示为「礼包 ×Times」。
            return new RewardView(icon, 0, CountText(times), QualityColor(1), RewardBadge.Gift, times);
        }

        /// <summary>功能性次数占位展示（悔棋 / 祈愿等非实物，无品质语义，品质退化白，§3.2）。</summary>
        public static RewardView FunctionView(string iconName, long amount)
        {
            return new RewardView(iconName, 0, CountText(amount), QualityColor(1), RewardBadge.Function, amount);
        }

        /// <summary>盲盒货币占位展示（灵力 Soul 无 num_id，走本层私有映射不查 num 表，§2.1 注）。</summary>
        public static RewardView ChestCurrencyView(string iconName, long amount)
        {
            return new RewardView(iconName, 0, CountText(amount), QualityColor(1), RewardBadge.Currency, amount);
        }

        // ─────────────────────── 6 档权威品质色（单一事实源，§3.3）───────────────────────

        /// <summary>
        /// 6 档品质色，对齐道具 <c>EItemQuality</c>（1 白 / 2 绿 / 3 蓝 / 4 紫 / 5 橙 / 6 红）。
        /// 越界（含 0 / 负 / &gt;6）退化为白。色值取自设计 17 §3.3 调色板。
        /// </summary>
        public static Color QualityColor(int quality)
        {
            switch (quality)
            {
                case 2: return new Color(0.36f, 0.84f, 0.63f); // 绿（高级 FINE）
                case 3: return new Color(0.42f, 0.55f, 1.00f); // 蓝（精英 ELITE）
                case 4: return new Color(0.69f, 0.49f, 1.00f); // 紫（史诗 EPIC）
                case 5: return new Color(1.00f, 0.66f, 0.30f); // 橙（传说 LEGEND）
                case 6: return new Color(1.00f, 0.48f, 0.54f); // 红（神话 MYTH）
                default: return new Color(0.85f, 0.85f, 0.85f); // 白（普通 COMMON / 越界退化）
            }
        }

        /// <summary>
        /// 图案品质映射（图案无独立品质字段，按等级映射展示品质，§3.3 注 / §七 O7）：
        /// Lv1→3 精英蓝 / Lv2→4 史诗紫 / Lv3→5 传说橙，越高越亮。其余等级退化白(1)。
        /// </summary>
        public static int PatternQuality(int level)
        {
            switch (level)
            {
                case 1: return 3;
                case 2: return 4;
                case 3: return 5;
                default: return 1;
            }
        }

        // ─────────────────────── 数量文本格式化（复用 NumericFormat，§3.4）───────────────────────

        /// <summary>
        /// 数量 → 显示文本。复用 <see cref="NumericFormat.Abbreviate"/>（避免两处格式化漂移），加 "x" 前缀。
        /// 单件（amount==1）不带 "x1"（界面更干净，§七 O8）；amount==0 显 "x0"（防御性）。
        /// </summary>
        public static string CountText(long amount)
        {
            if (amount <= 1) return amount == 1 ? "" : "x" + amount; // 0 显 "x0"，1 显空
            return "x" + NumericFormat.Abbreviate(amount);
        }

        /// <summary>图案数量文本：带等级（"Lv2 x3"）；单个图案只显等级（"Lv1"）；level&lt;1 不显等级前缀（§3.4）。</summary>
        public static string PatternCountText(int level, long count)
        {
            string lv = level >= 1 ? "Lv" + level + " " : "";
            return count > 1 ? lv + "x" + NumericFormat.Abbreviate(count) : lv.TrimEnd();
        }
    }
}
