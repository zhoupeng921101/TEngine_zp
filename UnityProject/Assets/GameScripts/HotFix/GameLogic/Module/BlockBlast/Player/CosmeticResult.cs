using System.Collections.Generic;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 换装 RPC 裁决结果码(客户端侧)。映射服务端 <c>Fantasy.EquipCosmeticResultCode</c> + 客户端额外网络层失败码。
    /// 佩戴切换由服务端裁定(校验目标已在服务端解锁集),客户端只持投影 + 乐观对账。
    /// </summary>
    public enum EquipCosmeticOutcome
    {
        /// <summary>成功:服务端已切当前佩戴;响应回带最新 CurrentAvatarId/CurrentFrameId 供对账。</summary>
        Success = 0,
        /// <summary>Kind 非法(非 1/2)。</summary>
        InvalidKind = 1,
        /// <summary>目标 id 不在服务端对应解锁集内(未解锁,拒换)→ 客户端回退乐观 set。</summary>
        NotUnlocked = 2,
        /// <summary>未登录(会话未挂账号)→ 客户端重登。</summary>
        NotLoggedIn = 3,
        /// <summary>服务不可用(未连接 / 超时 / 写库异常 / 空响应)。</summary>
        ServiceUnavailable = 4,
        /// <summary>网络断 / 未连接(客户端层,未发出请求)。</summary>
        NetworkDown = 5,
    }

    /// <summary>
    /// 换装 RPC 结果(客户端侧)。<see cref="CurrentAvatarId"/>/<see cref="CurrentFrameId"/> 在服务端返回的响应码下
    /// (Success / NotUnlocked / InvalidKind)是服务端当前权威值,可用于对齐视图(成功 = 切换后;失败 = 回退到服务端当前值);
    /// 纯客户端失败(NetworkDown / ServiceUnavailable 未收响应)时二者不可信(缺省 0)。
    /// </summary>
    public readonly struct EquipCosmeticResult
    {
        /// <summary>是否换装成功(等价 <see cref="Outcome"/> = Success)。</summary>
        public readonly bool Success;
        /// <summary>结果码(成功时 = Success)。</summary>
        public readonly EquipCosmeticOutcome Outcome;
        /// <summary>服务端当前权威佩戴头像 id(响应回带;不可信码下 = 0)。</summary>
        public readonly int CurrentAvatarId;
        /// <summary>服务端当前权威佩戴头像框 id(响应回带;不可信码下 = 0)。</summary>
        public readonly int CurrentFrameId;

        public EquipCosmeticResult(bool success, EquipCosmeticOutcome outcome, int currentAvatarId, int currentFrameId)
        {
            Success = success;
            Outcome = outcome;
            CurrentAvatarId = currentAvatarId;
            CurrentFrameId = currentFrameId;
        }

        /// <summary>成功:回带服务端最新当前头像 / 框 id。</summary>
        public static EquipCosmeticResult Ok(int currentAvatarId, int currentFrameId)
            => new EquipCosmeticResult(true, EquipCosmeticOutcome.Success, currentAvatarId, currentFrameId);

        /// <summary>拒绝:回带服务端当前权威 id(NotUnlocked / InvalidKind)供回退;纯客户端失败时缺省 0。</summary>
        public static EquipCosmeticResult Rejected(EquipCosmeticOutcome outcome, int currentAvatarId = 0, int currentFrameId = 0)
            => new EquipCosmeticResult(false, outcome, currentAvatarId, currentFrameId);
    }

    /// <summary>
    /// 解锁上报 RPC 裁决结果码(客户端侧)。映射服务端 <c>Fantasy.UnlockCosmeticResultCode</c> + 客户端额外网络层失败码。
    /// 解锁走 client-report(客户端按等级配置算出、上报 id,服务端 sanity 后幂等加入集合;集合为服务端权威)。
    /// </summary>
    public enum UnlockCosmeticOutcome
    {
        /// <summary>成功:id 已幂等加入对应解锁集合(响应回带更新后集合)。</summary>
        Success = 0,
        /// <summary>Kind 非法(非 1/2)。</summary>
        InvalidKind = 1,
        /// <summary>id 落在合法段外(服务端 sanity 拒)。</summary>
        InvalidId = 2,
        /// <summary>解锁集合已达上限(拒新增)。</summary>
        SetFull = 3,
        /// <summary>修饰操作频率过密(拒)。</summary>
        RateLimited = 4,
        /// <summary>未登录(会话未挂账号)→ 客户端重登。</summary>
        NotLoggedIn = 5,
        /// <summary>服务不可用(未连接 / 超时 / 写库异常 / 空响应)。</summary>
        ServiceUnavailable = 6,
        /// <summary>网络断 / 未连接(客户端层,未发出请求)。</summary>
        NetworkDown = 7,
    }

    /// <summary>
    /// 解锁上报 RPC 结果(客户端侧)。<see cref="UnlockedIds"/> 仅在 <see cref="Success"/> 下是服务端更新后权威集合,
    /// 可用于覆盖本地投影;失败码下为空/当前集合(不可信,不用于对齐)。
    /// </summary>
    public readonly struct UnlockCosmeticResult
    {
        /// <summary>是否解锁成功(等价 <see cref="Outcome"/> = Success)。</summary>
        public readonly bool Success;
        /// <summary>结果码(成功时 = Success)。</summary>
        public readonly UnlockCosmeticOutcome Outcome;
        /// <summary>本次上报的 Kind(<see cref="AvatarType"/>:1 头像 / 2 框),供调用方定位对齐哪个本地集合。</summary>
        public readonly int Kind;
        /// <summary>服务端更新后的对应已解锁集合(仅 Success 有意义;失败为空)。</summary>
        public readonly IReadOnlyList<int> UnlockedIds;

        public UnlockCosmeticResult(bool success, UnlockCosmeticOutcome outcome, int kind, IReadOnlyList<int> unlockedIds)
        {
            Success = success;
            Outcome = outcome;
            Kind = kind;
            UnlockedIds = unlockedIds;
        }

        /// <summary>成功:回带服务端更新后集合。</summary>
        public static UnlockCosmeticResult Ok(int kind, IReadOnlyList<int> unlockedIds)
            => new UnlockCosmeticResult(true, UnlockCosmeticOutcome.Success, kind, unlockedIds ?? System.Array.Empty<int>());

        /// <summary>拒绝:集合不可信(空)。</summary>
        public static UnlockCosmeticResult Rejected(int kind, UnlockCosmeticOutcome outcome)
            => new UnlockCosmeticResult(false, outcome, kind, System.Array.Empty<int>());
    }

    /// <summary>
    /// 批量解锁上报 RPC 结果(客户端侧)。一次携带多项 (kind,id),服务端逐项 sanity + $addToSet 幂等后回带两个 kind 的
    /// **最终解锁集**(解锁是集合幂等操作,客户端投影以最终集覆盖,不需逐项结果码)。<see cref="AvatarIds"/>/<see cref="FrameIds"/>
    /// 仅 <see cref="Success"/> 下是服务端权威最终集;失败码(RateLimited / NotLoggedIn / ServiceUnavailable / NetworkDown)下为空、不用于对齐。
    /// </summary>
    public readonly struct UnlockCosmeticBatchResult
    {
        /// <summary>是否整批成功(等价 <see cref="Outcome"/> = Success)。</summary>
        public readonly bool Success;
        /// <summary>整体结果码(成功时 = Success)。</summary>
        public readonly UnlockCosmeticOutcome Outcome;
        /// <summary>服务端处理后头像最终解锁集(仅 Success 可信;失败为空)。</summary>
        public readonly IReadOnlyList<int> AvatarIds;
        /// <summary>服务端处理后头像框最终解锁集(仅 Success 可信;失败为空)。</summary>
        public readonly IReadOnlyList<int> FrameIds;

        public UnlockCosmeticBatchResult(bool success, UnlockCosmeticOutcome outcome,
            IReadOnlyList<int> avatarIds, IReadOnlyList<int> frameIds)
        {
            Success = success;
            Outcome = outcome;
            AvatarIds = avatarIds ?? System.Array.Empty<int>();
            FrameIds = frameIds ?? System.Array.Empty<int>();
        }

        /// <summary>成功:回带处理后两个 kind 的最终解锁集。</summary>
        public static UnlockCosmeticBatchResult Ok(IReadOnlyList<int> avatarIds, IReadOnlyList<int> frameIds)
            => new UnlockCosmeticBatchResult(true, UnlockCosmeticOutcome.Success, avatarIds, frameIds);

        /// <summary>拒绝 / 失败:两集合不可信(空),调用方不对齐。</summary>
        public static UnlockCosmeticBatchResult Rejected(UnlockCosmeticOutcome outcome)
            => new UnlockCosmeticBatchResult(false, outcome, System.Array.Empty<int>(), System.Array.Empty<int>());
    }
}
