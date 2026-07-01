using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 档案状态(皮肤态 + 神庙装饰厅数)服务端权威上报编排器(皮肤/神庙装饰服务端权威·客户端段)。
    /// 皮肤切换(全清换皮)/ 神庙装饰(修复置位)乐观本地变更后,取当前三态<b>全量</b> SET 上报服务端(低频低危 fire-and-forget)。
    /// </summary>
    /// <remarks>
    /// 纯逻辑(不依赖 UnityEngine):经注入的 <see cref="IProfileStateGateway"/> 发请求,可 EditMode 单测。
    ///
    /// 【SET 语义】每次上报全量带三态(不带增量):皮肤态 = 是否单色(0/1)+ 当前单色 id(彩色态记 <see cref="BlockSkinState.Unselected"/> = -1);
    /// 神庙装饰 = 已装饰厅数标量(客户端 <see cref="MergeOrderState.TempleDecorated"/> bool[] 前缀数组 → true 项数,前缀语义与批 1 TempleRepaired 同口径)。
    ///
    /// 【fire-and-forget】失败(网络断 / 未登录 / 服务不可用 / sanity 拒)不回滚本地、不重试:下次变更再全量报,或登录快照对齐。
    /// 客户端不据响应对账(响应不回带权威三态)。<see cref="Report"/> 即发即忘(内部 .Forget()),不阻塞玩法。
    /// </remarks>
    public sealed class ProfileStateSync
    {
        private readonly IProfileStateGateway _gateway;

        public ProfileStateSync(IProfileStateGateway gateway)
        {
            _gateway = gateway ?? throw new System.ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// 从当前玩法态取三态全量 SET 上报(即发即忘)。<paramref name="state"/> 为 null 直接跳过、不发 RPC。
        /// 皮肤切换 / 神庙装饰变更后由窗口侧调用。
        /// </summary>
        public void Report(MergeOrderState state)
        {
            if (state == null) return;
            ReportAsync(state).Forget();
        }

        /// <summary>取三态、发 SET 上报并返回结果(供单测断言;生产走 <see cref="Report"/> 即发即忘)。</summary>
        public async UniTask<ProfileStateResult> ReportAsync(MergeOrderState state)
        {
            if (state == null)
                return ProfileStateResult.Rejected(ProfileStateOutcome.ServiceUnavailable);

            int skinMono = state.Skin != null && state.Skin.IsMono ? 1 : 0;
            int skinMonoId = state.Skin != null && state.Skin.IsMono ? state.Skin.MonoId : BlockSkinState.Unselected;
            int templeDecoratedCount = CountDecorated(state.TempleDecorated);

            return await _gateway.SetProfileStateAsync(skinMono, skinMonoId, templeDecoratedCount);
        }

        /// <summary>神庙装饰 bool[] → 已装饰厅数标量(true 项数,前缀语义)。null → 0。</summary>
        private static int CountDecorated(bool[] decorated)
        {
            if (decorated == null) return 0;
            int n = 0;
            for (int i = 0; i < decorated.Length; i++) if (decorated[i]) n++;
            return n;
        }
    }
}
