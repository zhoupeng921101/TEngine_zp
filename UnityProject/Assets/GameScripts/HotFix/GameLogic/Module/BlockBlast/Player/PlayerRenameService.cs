using System;
using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>改名拒绝原因（设计 18 §3.2）。供 UI 分支提示。</summary>
    public enum RenameReject { None, Empty, TooLong, Profanity, NotEnoughDiamond }

    /// <summary>改名结果（结构化，便于 UI 分支提示，设计 18 §3.2）。</summary>
    public readonly struct RenameResult
    {
        /// <summary>是否成功改名。</summary>
        public readonly bool Success;
        /// <summary>拒绝原因（成功时 None）。</summary>
        public readonly RenameReject Reason;
        /// <summary>本次花费（免费 = 0）。</summary>
        public readonly int Cost;

        public RenameResult(bool success, RenameReject reason, int cost)
        {
            Success = success;
            Reason = reason;
            Cost = cost;
        }

        public static RenameResult Ok(int cost) => new RenameResult(true, RenameReject.None, cost);
        public static RenameResult Rejected(RenameReject reason) => new RenameResult(false, reason, 0);
    }

    /// <summary>
    /// 改名价格配置（设计 18 §3.2）。本轮默认固定价常量；后续要分档或接 Luban 表是局部替换，
    /// 不动 <see cref="PlayerRenameService.TryRename"/> 逻辑。
    /// </summary>
    public static class RenamePriceConfig
    {
        /// <summary>固定改名价（钻石）。首次免费，之后每次同价。</summary>
        public const int RENAME_PRICE = 100;

        /// <summary>按已改名次数取价（本轮固定价；分档时改这里查表）。</summary>
        public static int PriceFor(int renameCount) => RENAME_PRICE;
    }

    /// <summary>
    /// 改名逻辑服务（设计 18 §3.2）。纯逻辑：首次免费、之后读价 + 经数值路径尝试扣钻、屏蔽字匹配。
    /// 判定顺序（任一不过即拒，后续不执行）：合法性 → 屏蔽字 → 计费 → 写名。
    /// </summary>
    /// <remarks>
    /// 扣费接缝（<c>trySpendDiamond</c>）外置使逻辑可测、不硬依赖钻石实装：钻石 num_id=3 无可花费余额字段
    /// （item-system 遗留 #19），生产默认实现返 true（去变现：不靠钻石卡改名），待钻石实装接真实扣减不返工（O8）。
    /// 屏蔽字命中先于计费返回，故不扣费。
    /// </remarks>
    public static class PlayerRenameService
    {
        /// <summary>名字最小长度（可调旋钮）。</summary>
        public const int MinLen = 1;
        /// <summary>名字最大长度（可调旋钮）。</summary>
        public const int MaxLen = 16;

        /// <summary>
        /// 尝试改名。<paramref name="trySpendDiamond"/> 是注入的「尝试扣钻石」回调（返回是否扣成功）。
        /// </summary>
        public static RenameResult TryRename(
            PlayerInfo p,
            string newName,
            IReadOnlyCollection<string> wordList,
            Func<int, bool> trySpendDiamond)
        {
            // ① 合法性：非空、不全空白、长度在 [MinLen, MaxLen]。
            if (string.IsNullOrEmpty(newName) || string.IsNullOrWhiteSpace(newName))
                return RenameResult.Rejected(RenameReject.Empty);
            if (newName.Length < MinLen)
                return RenameResult.Rejected(RenameReject.Empty);
            if (newName.Length > MaxLen)
                return RenameResult.Rejected(RenameReject.TooLong);

            // ② 屏蔽字：不通过直接拒，不扣费（先于计费）。
            if (!ProfanityFilter.IsClean(newName, wordList))
                return RenameResult.Rejected(RenameReject.Profanity);

            // ③ 计费：首次免费，否则读价 + 尝试扣钻。
            int cost = (p.RenameCount == 0) ? 0 : RenamePriceConfig.PriceFor(p.RenameCount);
            if (cost > 0)
            {
                bool spent = trySpendDiamond != null && trySpendDiamond(cost);
                if (!spent)
                    return RenameResult.Rejected(RenameReject.NotEnoughDiamond);
            }

            // ④ 扣费成功 / 免费 → 写名、计数 +1。
            p.Name = newName;
            p.RenameCount++;
            return RenameResult.Ok(cost);
        }
    }
}
