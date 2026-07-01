using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>改名本地校验拒绝原因(供 UI 分支提示)。仅覆盖客户端本地能判的项(空 / 超长 / 屏蔽字);
    /// 钻石不足由服务端裁决(改名走 RPC,费用服务端派生),不在此枚举。</summary>
    public enum RenameReject { None, Empty, TooLong, Profanity }

    /// <summary>
    /// 改名费用预告配置。改名走服务端权威 RPC,真正费用由服务端按自己的 RenameCount 派生;
    /// 本配置仅供 UI 预告(据服务端权威 RenameCount 预读下次费用),不参与真正扣费。
    /// </summary>
    public static class RenamePriceConfig
    {
        /// <summary>固定改名价(钻石)。首次免费,之后每次同价。</summary>
        public const int RENAME_PRICE = 100;

        /// <summary>按已改名次数取价(本轮固定价):0=首次免费,其余固定价。分档时改这里查表。</summary>
        public static int PriceFor(int renameCount) => renameCount == 0 ? 0 : RENAME_PRICE;
    }

    /// <summary>
    /// 改名本地校验(纯逻辑)。改名走服务端权威 RPC:服务端一次原子做完算费 / 扣钻 / 写名 / 计数 +1;
    /// 客户端只做「先挡明显非法」的本地校验(合法性 / 屏蔽字),减一次无谓往返——不本地扣钻、不本地写名。
    /// </summary>
    /// <remarks>
    /// 本地校验先挡空 / 超长 / 屏蔽字后才发 RPC;服务端另做基本 sanity(空 / 全空白 / 超长)兜底(InvalidName)。
    /// 屏蔽字词表本轮注空表(去变现 / 不阻塞,设计 18 O6),故实际只挡空 / 超长。
    /// </remarks>
    public static class PlayerRenameService
    {
        /// <summary>名字最小长度(可调旋钮)。</summary>
        public const int MinLen = 1;
        /// <summary>名字最大长度(可调旋钮)。</summary>
        public const int MaxLen = 16;

        /// <summary>
        /// 本地校验新昵称(纯函数,无副作用):合法性(非空 / 不全空白 / 长度 [MinLen,MaxLen])→ 屏蔽字。
        /// 通过返 <see cref="RenameReject.None"/>,不通过返对应原因(供 UI 分支提示 + 决定是否发 RPC)。
        /// </summary>
        public static RenameReject ValidateLocal(string newName, IReadOnlyCollection<string> wordList)
        {
            // ① 合法性:非空、不全空白、长度在 [MinLen, MaxLen]。
            if (string.IsNullOrEmpty(newName) || string.IsNullOrWhiteSpace(newName))
                return RenameReject.Empty;
            if (newName.Length < MinLen)
                return RenameReject.Empty;
            if (newName.Length > MaxLen)
                return RenameReject.TooLong;

            // ② 屏蔽字:命中即拒(空词表下不触发)。
            if (!ProfanityFilter.IsClean(newName, wordList))
                return RenameReject.Profanity;

            return RenameReject.None;
        }
    }
}
