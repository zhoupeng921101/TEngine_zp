namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 属性变更拒绝原因(设计 38 §四)。映射服务端 <c>Fantasy.PropertyChangeResultCode</c> + 客户端额外网络层失败码。
    /// </summary>
    public enum ChangeReject
    {
        /// <summary>变更成功(无拒绝)。</summary>
        None = 0,
        /// <summary>余额不足(服务端 NotEnough)。</summary>
        NotEnoughBalance = 1,
        /// <summary>类型上界溢出(服务端 OverLimit)。</summary>
        TypeUpperOverflow = 2,
        /// <summary>类型枚举未知 / 协议非法(服务端 UnknownType / InvalidRequest 合并)。</summary>
        TypeUnknown = 3,
        /// <summary>服务不可用(服务端 ServiceUnavailable;包括 MongoDB 不可达 / 写入异常)。</summary>
        ServiceUnavailable = 4,
        /// <summary>未登录(服务端 NotLoggedIn;会话未挂账号身份)。</summary>
        NotLoggedIn = 5,
        /// <summary>网络断 / 超时 / RPC 异常(客户端层失败,服务端无对应码)。</summary>
        NetworkDown = 6,
    }

    /// <summary>
    /// 属性变更结果(设计 38 §四)。<see cref="NewBalance"/> 在成功 / 余额不足 / 上界溢出三种码下是服务端实际余额,
    /// 其它码下为 0(不可信)。
    /// </summary>
    public readonly struct ChangeResult
    {
        /// <summary>是否变更成功(等价 <see cref="Reason"/> = None)。</summary>
        public readonly bool Success;
        /// <summary>失败原因(成功时 = None)。</summary>
        public readonly ChangeReject Reason;
        /// <summary>变更后服务端实际余额(成功 / NotEnoughBalance / TypeUpperOverflow 三种码下可信,其它码下 = 0)。</summary>
        public readonly long NewBalance;

        public ChangeResult(bool success, ChangeReject reason, long newBalance)
        {
            Success = success;
            Reason = reason;
            NewBalance = newBalance;
        }

        public static ChangeResult Ok(long newBalance) => new ChangeResult(true, ChangeReject.None, newBalance);
        public static ChangeResult Rejected(ChangeReject reason, long newBalance = 0L) => new ChangeResult(false, reason, newBalance);
    }

    /// <summary>
    /// 批量属性变更的一项输入(类型 + 有符号增量),口径同单条链路(仅声明相对增量,身份从会话取)。
    /// </summary>
    public readonly struct BatchChangeItem
    {
        public readonly AttrType Type;
        public readonly long Delta;

        public BatchChangeItem(AttrType type, long delta)
        {
            Type = type;
            Delta = delta;
        }
    }

    /// <summary>
    /// 批量属性变更的一项裁决结果(口径同 <see cref="ChangeResult"/>)。<see cref="NewBalance"/> 在
    /// None / NotEnoughBalance / TypeUpperOverflow 三种码下是服务端实际余额(可信),其它码下 = 0(不可信)。
    /// </summary>
    public readonly struct BatchChangeResultItem
    {
        public readonly AttrType Type;
        public readonly ChangeReject Reason;
        public readonly long NewBalance;

        public bool Success => Reason == ChangeReject.None;

        public BatchChangeResultItem(AttrType type, ChangeReject reason, long newBalance)
        {
            Type = type;
            Reason = reason;
            NewBalance = newBalance;
        }
    }
}
