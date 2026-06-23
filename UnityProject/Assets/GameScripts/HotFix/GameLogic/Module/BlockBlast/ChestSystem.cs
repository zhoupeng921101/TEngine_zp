using System.Collections.Generic;

namespace GameLogic.BlockBlast
{
    /// <summary>宝箱等级（设计 11 §九）。倒计时与奖池基调随箱级升。</summary>
    public enum ChestTier
    {
        Common = 0, // 普通：≈5 分钟，少量灵力/体力/Lv1，消除掉落
        Rare = 1,   // 稀有：≈30 分钟，中量灵力 + Lv2 + 道具，加急单
        Epic = 2,   // 史诗：≈2 小时，大量灵力 + Lv3 + 多道具，女神满级/活动
    }

    /// <summary>开箱三选一的奖励类型（设计 11 §九·奖池）。</summary>
    public enum ChestRewardKind
    {
        Soul = 0,       // 灵力
        Energy = 1,     // 体力
        Pattern = 2,    // 图案（按箱级给 Lv1~Lv3）
        WishCharge = 4, // 祈愿补体力次数
    }

    /// <summary>一项开箱奖励（三选一中的一张卡）。</summary>
    public readonly struct ChestReward
    {
        public readonly ChestRewardKind Kind;
        public readonly int Amount;       // 灵力/体力数量、图案数量、道具次数
        public readonly int PatternLevel; // 仅 Pattern 有意义（1~3）

        public ChestReward(ChestRewardKind kind, int amount, int patternLevel = 0)
        {
            Kind = kind;
            Amount = amount;
            PatternLevel = patternLevel;
        }
    }

    /// <summary>占箱位的一个宝箱（倒计时进行中 / 已可开）。</summary>
    public sealed class ChestSlot
    {
        public ChestTier Tier;
        public float CountdownSec;   // 剩余倒计时；≤0 = 可开
        public bool Opened;          // 已开（领取后即从箱位移除，此位仅作过渡）

        public bool Ready => CountdownSec <= 0f;
    }

    /// <summary>
    /// 宝箱系统（设计 11 §九）。纯逻辑：4 箱位、占槽倒计时、到点可开、开箱三选一从箱级奖池抽 3 不重复项。
    /// 去变现红线：倒计时只能等，无付费/广告跳过——本系统不提供任何「花钱/看广告减时」入口。
    ///
    /// demo 范围（设计 11 §九·降级）：最小可玩 = 普通箱 + 消除掉落 + 三选一；倒计时可在 demo 大幅缩短。
    /// 奖励发放（把选中的 ChestReward 兑成灵力/体力/图案/道具）由窗口对接 MergeOrderState，本系统只产出选项。
    /// </summary>
    public sealed class ChestSystem
    {
        /// <summary>箱位数（设计 11 §九，对齐原稿界面布局）。</summary>
        public const int SlotCount = 4;

        /// <summary>三选一的卡数。</summary>
        public const int PickCount = 3;

        /// <summary>各箱级默认倒计时秒（普通 5 分 / 稀有 30 分 / 史诗 2 小时）。</summary>
        public static readonly float[] TierCountdownSec = { 300f, 1800f, 7200f };

        private readonly List<ChestSlot> _slots = new List<ChestSlot>();

        public IReadOnlyList<ChestSlot> Slots => _slots;
        public int OccupiedCount => _slots.Count;
        public bool IsFull => _slots.Count >= SlotCount;

        public void Reset() => _slots.Clear();

        /// <summary>
        /// 获取一个宝箱入箱位并开始倒计时。箱位满（4 个）→ 不入位，返回 false（提示先开箱腾位）。
        /// </summary>
        public bool TryAcquire(ChestTier tier)
        {
            if (IsFull) return false;
            _slots.Add(new ChestSlot { Tier = tier, CountdownSec = TierCountdownSec[(int)tier], Opened = false });
            return true;
        }

        /// <summary>推进所有箱位倒计时（按现实时间）。倒计时只减不增，封底 0。</summary>
        public void Tick(float deltaSec)
        {
            if (deltaSec <= 0f) return;
            foreach (var s in _slots)
            {
                if (s.CountdownSec > 0f)
                {
                    s.CountdownSec -= deltaSec;
                    if (s.CountdownSec < 0f) s.CountdownSec = 0f;
                }
            }
        }

        /// <summary>该箱位是否可开（存在且倒计时到点）。</summary>
        public bool CanOpen(int slotIndex)
            => slotIndex >= 0 && slotIndex < _slots.Count && _slots[slotIndex].Ready;

        /// <summary>
        /// 开箱：从该箱位箱级奖池抽 <see cref="PickCount"/> 个不重复项供三选一。
        /// 不在此领取/腾位——调用方拿到选项、玩家选 1 张后再调 <see cref="Claim"/>。
        /// 返回 null 表示不可开。
        /// </summary>
        public List<ChestReward> Open(int slotIndex)
        {
            if (!CanOpen(slotIndex)) return null;
            return RollRewards(_slots[slotIndex].Tier);
        }

        /// <summary>领取（玩家已三选一）：腾空该箱位。返回 false 表示箱位无效或未到点。</summary>
        public bool Claim(int slotIndex)
        {
            if (!CanOpen(slotIndex)) return false;
            _slots.RemoveAt(slotIndex);
            return true;
        }

        /// <summary>从箱级奖池抽 PickCount 个不重复项（按 RewardKind 去重，箱级越高高价值占比越大）。</summary>
        public List<ChestReward> RollRewards(ChestTier tier)
        {
            // 奖池候选（按箱级给量；同 Kind 视为重复，保证三张卡 Kind 互不相同）。
            var pool = BuildPool(tier);
            var picked = new List<ChestReward>();
            var usedKinds = new HashSet<ChestRewardKind>();

            // 随机不重复抽 PickCount 个（确定性随机源 RandomSource，可单测）。
            int guard = 0;
            while (picked.Count < PickCount && pool.Count > 0 && guard++ < 64)
            {
                int idx = RandomSource.Index(pool.Count);
                var cand = pool[idx];
                pool.RemoveAt(idx);
                if (usedKinds.Add(cand.Kind)) picked.Add(cand);
            }
            return picked;
        }

        private static List<ChestReward> BuildPool(ChestTier tier)
        {
            switch (tier)
            {
                case ChestTier.Epic:
                    return new List<ChestReward>
                    {
                        new ChestReward(ChestRewardKind.Soul, 200),
                        new ChestReward(ChestRewardKind.Pattern, 1, 3),
                        new ChestReward(ChestRewardKind.Energy, 20),
                        new ChestReward(ChestRewardKind.WishCharge, 2),
                    };
                case ChestTier.Rare:
                    return new List<ChestReward>
                    {
                        new ChestReward(ChestRewardKind.Soul, 80),
                        new ChestReward(ChestRewardKind.Pattern, 1, 2),
                        new ChestReward(ChestRewardKind.Energy, 12),
                        new ChestReward(ChestRewardKind.WishCharge, 1),
                    };
                default: // Common
                    return new List<ChestReward>
                    {
                        new ChestReward(ChestRewardKind.Soul, 20),
                        new ChestReward(ChestRewardKind.Pattern, 1, 1),
                        new ChestReward(ChestRewardKind.Energy, 6),
                    };
            }
        }
    }
}
