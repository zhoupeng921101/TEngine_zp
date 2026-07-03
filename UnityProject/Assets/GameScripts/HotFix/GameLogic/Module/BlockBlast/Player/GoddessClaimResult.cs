using System;
using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>女神领取裁决码(框架中立):与协议 <c>GoddessClaimResultCode</c> 对齐 + 网络层降级码。</summary>
    public enum GoddessClaimOutcome
    {
        /// <summary>领取成功,已回带发放元素类型 + 奖励档列表。</summary>
        Success = 0,
        /// <summary>未满档 / 并发已被领(服务端 CAS 未命中):计数不变,不发奖。</summary>
        NotFull = 1,
        /// <summary>会话未登录 → 客户端重登。</summary>
        NotLoggedIn = 2,
        /// <summary>未连接网络:未发请求。</summary>
        NetworkDown = 3,
        /// <summary>服务不可用 / 空响应 / 异常 / 奖励表缺失。</summary>
        ServiceUnavailable = 4,
    }

    /// <summary>单档奖励(元素等级 + 数量;元素类型由 <see cref="GoddessClaimResult.ElementType"/> 统一给出)。</summary>
    public readonly struct GoddessRewardEntry
    {
        public readonly int Level;
        public readonly int Count;

        public GoddessRewardEntry(int level, int count)
        {
            Level = level;
            Count = count;
        }
    }

    /// <summary>
    /// 女神领取结果(框架中立):结果码 + 发放元素类型(= 客户端 MergeElement 整数)+ 奖励档列表。
    /// 失败时 ElementType=0、Rewards 空。供 UI 分支提示 + 成功时逐档 AddDirect 入合成区。
    /// </summary>
    public readonly struct GoddessClaimResult
    {
        public readonly GoddessClaimOutcome Outcome;
        public readonly int ElementType;
        public readonly IReadOnlyList<GoddessRewardEntry> Rewards;

        public GoddessClaimResult(GoddessClaimOutcome outcome, int elementType, IReadOnlyList<GoddessRewardEntry> rewards)
        {
            Outcome = outcome;
            ElementType = elementType;
            Rewards = rewards ?? Array.Empty<GoddessRewardEntry>();
        }

        public static GoddessClaimResult Ok(int elementType, IReadOnlyList<GoddessRewardEntry> rewards)
            => new GoddessClaimResult(GoddessClaimOutcome.Success, elementType, rewards);

        public static GoddessClaimResult Rejected(GoddessClaimOutcome outcome)
            => new GoddessClaimResult(outcome, 0, Array.Empty<GoddessRewardEntry>());
    }
}
