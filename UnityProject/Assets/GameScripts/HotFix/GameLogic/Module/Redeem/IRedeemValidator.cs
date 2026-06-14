namespace GameLogic.Redeem
{
    /// <summary>校验结果状态（设计 20 §3.3）。</summary>
    public enum ValidationStatus
    {
        /// <summary>码有效（命中）。</summary>
        Valid,
        /// <summary>码不存在（未命中）。</summary>
        NotFound,
        /// <summary>校验源不可用（远程 stub / 无网络）。</summary>
        SourceUnavailable,
    }

    /// <summary>
    /// 校验结果结构（设计 20 §3.3）。校验只回答「码有效吗、能换什么」，不碰去重 / 不发奖。
    /// </summary>
    public readonly struct ValidationResult
    {
        /// <summary>校验状态。</summary>
        public readonly ValidationStatus Status;
        /// <summary>命中的码定义（仅 <see cref="ValidationStatus.Valid"/> 时非空）。</summary>
        public readonly RedeemCodeDef Def;

        public ValidationResult(ValidationStatus status, RedeemCodeDef def)
        {
            Status = status;
            Def = def;
        }
    }

    /// <summary>
    /// 校验器接缝（设计 20 §2.2「服务器接缝」实义）。
    /// 把「码有效吗、能换什么」这件本应由服务器拍板的事抽象成接口，使 <see cref="RedeemService"/>
    /// 只依赖接口、不依赖校验来源——离线注 <see cref="LocalConfigRedeemValidator"/>，未来切远程注
    /// <see cref="RemoteRedeemValidator"/>，服务层零改动。
    /// </summary>
    public interface IRedeemValidator
    {
        /// <summary>校验规整后的码。返回校验结果（命中 / 未命中 / 校验源不可用 + 命中时的定义）。</summary>
        ValidationResult Validate(string normalizedCode);
    }

    /// <summary>
    /// 离线默认校验器：查本地 Luban 配置表（设计 20 §3.3）。
    /// 策划在配置表登记码 → 奖励，随热更下发，离线可用。
    /// </summary>
    public sealed class LocalConfigRedeemValidator : IRedeemValidator
    {
        public ValidationResult Validate(string normalizedCode)
        {
            var def = GameLogic.Config.RedeemConfigMgr.Get(normalizedCode);
            return def != null
                ? new ValidationResult(ValidationStatus.Valid, def)
                : new ValidationResult(ValidationStatus.NotFound, null);
        }
    }

    /// <summary>
    /// 远程校验 stub（设计 20 §2.2 / §七 O1）。本工程无网络模块、方向去变现，本轮不实接服务器。
    /// 返 <see cref="ValidationStatus.SourceUnavailable"/>（<b>不抛异常</b>，服务层映射成「校验源不可用」结果码，不崩）。
    /// 未来上服务器时在此实现一次（替换方法体），<see cref="RedeemService"/> 零改动换注入。
    /// </summary>
    public sealed class RemoteRedeemValidator : IRedeemValidator
    {
        // TODO（设计 20 §七 O1）：未来上后端时实现真实远程校验。
        // 禁引入 UnityWebRequest / HttpClient（离线方向）；届时校验经异步接口另行设计。
        public ValidationResult Validate(string normalizedCode)
            => new ValidationResult(ValidationStatus.SourceUnavailable, null);
    }
}
