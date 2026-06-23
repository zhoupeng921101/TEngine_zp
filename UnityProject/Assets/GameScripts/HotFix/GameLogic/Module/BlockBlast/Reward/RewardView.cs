using UnityEngine;

namespace GameLogic.BlockBlast.Reward
{
    /// <summary>
    /// 通用奖励展示归一结构（设计 17 §3.1）。POCO，字段只为「渲染一项奖励」服务，
    /// 不含产出语义（不知道奖励落哪个系统）。唯一 Unity 类型是 <see cref="Color"/>（值类型，
    /// 可在 EditMode 单测构造），故归一层全程可纯单测、不碰 YooAsset / Unity 运行时。
    /// </summary>
    public readonly struct RewardView
    {
        /// <summary>图标资源名（真实 Sprite 加载交调用方接 UI 时做，本层只给名；无美术时占位）。</summary>
        public readonly string IconName;
        /// <summary>名称多语言文本 id（文本表本轮未接，给 id；与 NumericDisplay 现状一致，设计 17 §七 O4）。</summary>
        public readonly int NameTextId;
        /// <summary>数量显示文本（"x200" / "x999.9K" / "Lv2 x4"，已格式化好，避免各 UI 各拼一次，§3.4）。</summary>
        public readonly string CountText;
        /// <summary>品质色（6 档权威，§3.3）。</summary>
        public readonly Color QualityColor;
        /// <summary>类型角标（货币 / 图案 / 材料 / 礼包 / 功能，与产出枚举解耦，UI 只认本枚举）。</summary>
        public readonly RewardBadge Badge;
        /// <summary>原始数量（供少数需自定义格式 / 排序的 UI 兜底；0 = 无数量语义）。</summary>
        public readonly long RawAmount;

        public RewardView(string iconName, int nameTextId, string countText,
                          Color qualityColor, RewardBadge badge, long rawAmount)
        {
            IconName = iconName;
            NameTextId = nameTextId;
            CountText = countText;
            QualityColor = qualityColor;
            Badge = badge;
            RawAmount = rawAmount;
        }
    }

    /// <summary>
    /// 展示分类角标（渲染左上角小标，与产出 <c>GrantKind</c> / <c>ChestRewardKind</c> 解耦）。
    /// 归一时把 5 种 GrantKind / 5 种 ChestRewardKind 收成本枚举，UI 只认 RewardBadge（设计 17 §3.1）。
    /// </summary>
    public enum RewardBadge
    {
        /// <summary>无角标。</summary>
        None,
        /// <summary>货币（灵力 / 体力 / 经验 / 虔诚币…）。</summary>
        Currency,
        /// <summary>图案。</summary>
        Pattern,
        /// <summary>材料 / 功能材料。</summary>
        Material,
        /// <summary>礼包（自选 / 随机）。</summary>
        Gift,
        /// <summary>功能性次数（祈愿等无图标实物的）。</summary>
        Function,
    }
}
