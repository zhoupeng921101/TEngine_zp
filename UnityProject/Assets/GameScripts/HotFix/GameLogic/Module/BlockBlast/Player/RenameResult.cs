namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 改名 RPC 裁决结果码(客户端侧)。映射服务端 <c>Fantasy.RenameResultCode</c> + 客户端额外网络层失败码。
    /// 改名费用/扣钻/写名/计数全部由服务端一次原子完成,客户端不再本地扣钻、不再本地写名。
    /// </summary>
    public enum RenameOutcome
    {
        /// <summary>成功:服务端已写昵称、RenameCount+1、(若扣费)已扣钻;响应回带最新权威值供客户端对账。</summary>
        Success = 0,
        /// <summary>昵称非法(空 / 全空白 / 超长;服务端基本 sanity 未过)。</summary>
        InvalidName = 1,
        /// <summary>钻石不足,昵称未改。</summary>
        NotEnoughDiamond = 2,
        /// <summary>未登录(会话未挂账号)→ 客户端重登。</summary>
        NotLoggedIn = 3,
        /// <summary>服务不可用(未连接 / 超时 / 写库异常 / 空响应)。</summary>
        ServiceUnavailable = 4,
        /// <summary>网络断 / 未连接(客户端层,未发出请求)。</summary>
        NetworkDown = 5,
    }

    /// <summary>
    /// 改名 RPC 结果(客户端侧)。<see cref="Nickname"/>/<see cref="RenameCount"/>/<see cref="Diamond"/>
    /// 在服务端返回的响应码下(Success / InvalidName / NotEnoughDiamond)是服务端当前权威值,可用于对齐视图;
    /// 纯客户端失败(NetworkDown / ServiceUnavailable 未收到响应)时三者不可信(缺省 0/空串)。
    /// </summary>
    public readonly struct RenameRpcResult
    {
        /// <summary>是否改名成功(等价 <see cref="Outcome"/> = Success)。</summary>
        public readonly bool Success;
        /// <summary>结果码(成功时 = Success)。</summary>
        public readonly RenameOutcome Outcome;
        /// <summary>服务端当前权威昵称(成功 = 新昵称;失败 = 服务端现有昵称,供回退显示)。</summary>
        public readonly string Nickname;
        /// <summary>服务端当前权威改名次数(成功 = +1 后;失败 = 当前次数,供算下次费用)。</summary>
        public readonly int RenameCount;
        /// <summary>服务端当前权威钻石余额(成功且扣费 = 扣后;免费成功 / 钻不足 / 其它 = 当前余额;不可信码下 = 0)。</summary>
        public readonly long Diamond;

        public RenameRpcResult(bool success, RenameOutcome outcome, string nickname, int renameCount, long diamond)
        {
            Success = success;
            Outcome = outcome;
            Nickname = nickname;
            RenameCount = renameCount;
            Diamond = diamond;
        }

        /// <summary>成功:回带服务端最新昵称 / 次数 / 钻石余额。</summary>
        public static RenameRpcResult Ok(string nickname, int renameCount, long diamond)
            => new RenameRpcResult(true, RenameOutcome.Success, nickname, renameCount, diamond);

        /// <summary>
        /// 拒绝:回带服务端当前权威值(InvalidName / NotEnoughDiamond)供对齐;纯客户端失败(NetworkDown /
        /// ServiceUnavailable 未收响应)时缺省空串 / 0(调用方按 <paramref name="outcome"/> 决定是否对齐)。
        /// </summary>
        public static RenameRpcResult Rejected(RenameOutcome outcome, string nickname = null, int renameCount = 0, long diamond = 0L)
            => new RenameRpcResult(false, outcome, nickname ?? string.Empty, renameCount, diamond);
    }
}
