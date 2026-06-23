using System;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 玩家元层属性客户端账本视图(设计 38 §四)。承接服务端 37 三 RPC(快照 / 通用变更 / 推送)
    /// 的客户端段:内存持有 Coin/Diamond/Stamina 三属性余额视图 + 订阅推送 + 收快照 + 同步发变更请求。
    /// </summary>
    /// <remarks>
    /// 纯逻辑 C# 类(不依赖 UnityEngine,可 EditMode 测,SV2);三属性 <b>不入本地存档</b>
    /// (MergeMetaSave / PlayerPrefs 不加三属性 key,SV4,沿 37 客户端不持权威值红线)。
    /// 视图覆盖语义(SV6):<see cref="ApplyDeltaPush"/> / <see cref="ApplyChangeResponse"/> 按 type 直接 set
    /// 新余额(不做「旧值 + delta」相对推断,沿 37 §5.4 推送是绝对快照)。
    /// 变更负载(SV3):<see cref="TryChangeAsync"/> 经 <see cref="IRpcGateway"/> 发的请求仅含 type + delta + reason
    /// (不传当前余额,沿 37 反作弊红线)。
    /// </remarks>
    public sealed class PlayerAttrService
    {
        private readonly IRpcGateway _gateway;

        /// <summary>金币余额(协议 PropertyType.Coin)。<see cref="IsReady"/>=false 时 = 0 是缺省占位非真实值。</summary>
        public long Coin { get; private set; }
        /// <summary>钻石余额(协议 PropertyType.Diamond)。<see cref="IsReady"/>=false 时 = 0 是缺省占位非真实值。</summary>
        public long Diamond { get; private set; }
        /// <summary>体力余额(协议 PropertyType.Stamina)。<see cref="IsReady"/>=false 时 = 0 是缺省占位非真实值。</summary>
        public long Stamina { get; private set; }
        /// <summary>是否已收到首次 InitSnapshot(true 后三属性视图才是服务端权威值)。UI 据此切「加载中...」与可点态。</summary>
        public bool IsReady { get; private set; }

        /// <summary>属性变化事件。type=All 仅 <see cref="ApplySnapshot"/> 触发一次;type=Coin/Diamond/Stamina 各自变更触发。</summary>
        public event Action<AttrType, long, string> OnAttrChanged;

        /// <summary>构造(注入 RPC 接缝)。生产用 RpcGatewayProd(同目录,#if FANTASY_UNITY);测试用桩。</summary>
        public PlayerAttrService(IRpcGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        /// <summary>
        /// 应用初始快照(收到 G2C_PropertyInitSnapshot 时由分发钩子调)。覆盖三属性 + 置 IsReady=true + 触发一次 All 事件。
        /// </summary>
        public void ApplySnapshot(long coin, long diamond, long stamina)
        {
            Coin = coin;
            Diamond = diamond;
            Stamina = stamina;
            IsReady = true;
            OnAttrChanged?.Invoke(AttrType.All, 0L, "init_snapshot");
        }

        /// <summary>
        /// 应用推送(收到 G2C_PropertyDeltaPush 时由分发钩子调)。按 type 直接 set 新余额 + 触发对应 type 事件。
        /// </summary>
        public void ApplyDeltaPush(AttrType type, long newBalance, string reason)
        {
            SetByType(type, newBalance);
            OnAttrChanged?.Invoke(type, newBalance, reason ?? string.Empty);
        }

        /// <summary>
        /// 应用变更响应(<see cref="TryChangeAsync"/> 内部:成功时回写新余额 + 触发事件)。
        /// 与 <see cref="ApplyDeltaPush"/> 同口径(响应与推送可能乱序到达,各自覆盖即可,沿 38 §6 走查)。
        /// </summary>
        public void ApplyChangeResponse(AttrType type, long newBalance, string reason)
        {
            SetByType(type, newBalance);
            OnAttrChanged?.Invoke(type, newBalance, reason ?? string.Empty);
        }

        /// <summary>
        /// 发起属性变更请求(改名扣钻 / 后续业务玩法阶段的统一入口)。
        /// 同步等响应(沿 D3,非 fire-and-forget):成功时先 <see cref="ApplyChangeResponse"/> 再返结果;
        /// 失败时(余额不足 / 上界溢出)按服务端返的 NewBalance 刷视图(保两端一致);其它失败(网络 / 服务不可用)不动视图。
        /// </summary>
        public async UniTask<ChangeResult> TryChangeAsync(AttrType type, long delta, string reason)
        {
            if (type == AttrType.All)
            {
                // 协议无 All;接缝层直接拒,不浪费一次 RPC。
                return ChangeResult.Rejected(ChangeReject.TypeUnknown);
            }

            var result = await _gateway.SendChangeRequestAsync(type, delta, reason);

            // 成功 + 余额不足 + 上界溢出 三种码下服务端返了真实余额,刷本地视图(保两端一致)
            if (result.Reason == ChangeReject.None
                || result.Reason == ChangeReject.NotEnoughBalance
                || result.Reason == ChangeReject.TypeUpperOverflow)
            {
                SetByType(type, result.NewBalance);
                OnAttrChanged?.Invoke(type, result.NewBalance, reason ?? string.Empty);
            }
            // 其它失败(NetworkDown / ServiceUnavailable / NotLoggedIn / TypeUnknown)→ 不动视图

            return result;
        }

        private void SetByType(AttrType type, long newBalance)
        {
            switch (type)
            {
                case AttrType.Coin:    Coin = newBalance; break;
                case AttrType.Diamond: Diamond = newBalance; break;
                case AttrType.Stamina: Stamina = newBalance; break;
                // All / 未知 type:不动字段(协议层应已保不会出现,此处只防御)
            }
        }
    }
}
