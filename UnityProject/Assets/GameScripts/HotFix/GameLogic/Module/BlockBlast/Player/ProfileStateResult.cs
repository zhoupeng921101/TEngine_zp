namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 档案状态 SET 上报裁决结果码(客户端侧)。映射服务端 <c>Fantasy.SetProfileStateResultCode</c> + 客户端额外网络层失败码。
    /// 皮肤态 + 神庙装饰厅数为低频低危 client-report:客户端全量带三态 SET 上报,服务端原子 $set + sanity;
    /// 客户端不据响应对账(fire-and-forget),失败下次变更再报 / 登录快照对齐,故结果仅供日志 / 断言,不回带权威值。
    /// </summary>
    public enum ProfileStateOutcome
    {
        /// <summary>成功:服务端已原子 $set 三态。</summary>
        Success = 0,
        /// <summary>sanity 拒(SkinMono 非 0/1,或 SkinMonoId / TempleDecorated 落合法段外)。</summary>
        InvalidRequest = 1,
        /// <summary>未登录(会话未挂账号)→ 客户端重登。</summary>
        NotLoggedIn = 2,
        /// <summary>服务不可用(未连接 / 超时 / 写库异常 / 空响应)。</summary>
        ServiceUnavailable = 3,
        /// <summary>网络断 / 未连接(客户端层,未发出请求)。</summary>
        NetworkDown = 4,
    }

    /// <summary>
    /// 档案状态 SET 上报 RPC 结果(客户端侧)。fire-and-forget 语义:成功与否不改变客户端本地投影
    /// (乐观变更早已本地应用,权威对齐只走登录快照),故仅承载结果码供日志 / 断言,不回带服务端权威三态。
    /// </summary>
    public readonly struct ProfileStateResult
    {
        /// <summary>是否上报成功(等价 <see cref="Outcome"/> = Success)。</summary>
        public readonly bool Success;
        /// <summary>结果码(成功时 = Success)。</summary>
        public readonly ProfileStateOutcome Outcome;

        public ProfileStateResult(bool success, ProfileStateOutcome outcome)
        {
            Success = success;
            Outcome = outcome;
        }

        /// <summary>成功:服务端已 $set 三态。</summary>
        public static ProfileStateResult Ok() => new ProfileStateResult(true, ProfileStateOutcome.Success);

        /// <summary>拒绝 / 失败(sanity 拒 / 未登录 / 服务不可用 / 网络断)。</summary>
        public static ProfileStateResult Rejected(ProfileStateOutcome outcome)
            => new ProfileStateResult(false, outcome);
    }
}
