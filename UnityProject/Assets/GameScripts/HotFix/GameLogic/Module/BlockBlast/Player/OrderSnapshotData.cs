using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 单条订单的框架中立投影(对应协议 Fantasy.OrderItem)。Type 整数值与 <see cref="MergeElement"/> 一致
    /// (0=None 空槽 / 1=Butterfly / 2=Chalice / 3=Scroll / 4=Star)。Handler 把协议对象转成本结构再交逻辑层,
    /// 使 <see cref="OrderSync"/> 不直接引用 Fantasy 协议类型、保持 EditMode 可测。
    /// 奖励四字段是服务端按 TbMergeOrder 表派生的展示投影(客户端只显示,实发以交付响应为准;空槽为 0)。
    /// </summary>
    public readonly struct OrderItemData
    {
        public readonly int Type;
        public readonly int Level;
        public readonly int Count;
        /// <summary>交付奖励体力(展示用)。</summary>
        public readonly int EnergyReward;
        /// <summary>交付奖励虔诚币(展示用)。</summary>
        public readonly long PietyReward;
        /// <summary>交付掉落塔罗碎片道具 id(= item.TbItemDef 行;0 = 无碎片)。</summary>
        public readonly int FragmentItemId;
        /// <summary>交付掉落碎片数量(展示用)。</summary>
        public readonly int FragmentCount;

        public OrderItemData(int type, int level, int count,
            int energyReward = 0, long pietyReward = 0L, int fragmentItemId = 0, int fragmentCount = 0)
        {
            Type = type;
            Level = level;
            Count = count;
            EnergyReward = energyReward;
            PietyReward = pietyReward;
            FragmentItemId = fragmentItemId;
            FragmentCount = fragmentCount;
        }
    }

    /// <summary>
    /// 订单系统服务端权威快照的框架中立投影(对应协议 Fantasy.MergeOrderSnapshot)。
    /// 客户端整份覆盖本地 normal 订单视图(P1 全栈迁移·客户端段)。
    /// </summary>
    public sealed class OrderSnapshotData
    {
        /// <summary>当前激活订单(长度 = 服务端 ActiveOrders 配置;空槽 Type=0 占位,槽位次序固定)。</summary>
        public readonly List<OrderItemData> ActiveOrders;
        /// <summary>订单池游标(服务端权威,仅供客户端校验/排错)。</summary>
        public readonly int OrderCursor;
        /// <summary>上次整批刷新时刻(Unix 毫秒,服务端权威时钟;客户端据此 + 间隔显示倒计时)。</summary>
        public readonly long LastOrderRefreshMs;
        /// <summary>整批刷新间隔秒数(客户端读此值算下次刷新时刻,不再读本地配置)。</summary>
        public readonly int OrderRefreshIntervalSec;

        public OrderSnapshotData(List<OrderItemData> activeOrders, int orderCursor,
            long lastOrderRefreshMs, int orderRefreshIntervalSec)
        {
            ActiveOrders = activeOrders ?? new List<OrderItemData>();
            OrderCursor = orderCursor;
            LastOrderRefreshMs = lastOrderRefreshMs;
            OrderRefreshIntervalSec = orderRefreshIntervalSec;
        }
    }

    /// <summary>
    /// 交付裁决结果码(框架中立,映射服务端 <c>Fantasy.DeliverOrderResultCode</c> + 客户端网络层失败码)。
    /// </summary>
    public enum DeliverCode
    {
        /// <summary>成功:服务端已发奖、推 delta、回带最新快照。</summary>
        Success = 0,
        /// <summary>会话未挂账号(登录链路异常)→ 客户端重登。</summary>
        NotLoggedIn = 1,
        /// <summary>槽位越界 / 该槽空(未刷或已交付被空槽占位)。</summary>
        InvalidSlot = 2,
        /// <summary>本轮该槽已交付(服务端幂等;客户端表现为不重复发奖)。</summary>
        AlreadyDelivered = 3,
        /// <summary>服务不可用(MongoDB 不可达 / 服务未就绪 / ChangeProperty 失败)。</summary>
        ServiceUnavailable = 4,
        /// <summary>网络断 / 超时 / RPC 异常(客户端层失败,服务端无对应码)。</summary>
        NetworkDown = 5,
    }

    /// <summary>
    /// 交付结果(框架中立)。<see cref="Snapshot"/> 为服务端回带的最新订单快照(成功必有;失败时可能为占位空快照或 null)。
    /// <see cref="EnergyBalance"/> / <see cref="PietyBalance"/> / <see cref="FragmentBalance"/> = 交付后权威绝对余额,
    /// 供对账 set 本地体力/虔诚币/背包碎片计数(取代原对发起方的 delta-push);
    /// -1 = 哨兵(本次未取到该项权威值,不据此 set,靠快照/其它推送对齐)。
    /// <see cref="FragmentItemId"/> 仅在本次真发碎片(<see cref="FragmentReward"/> &gt; 0)时非 0。
    /// </summary>
    public readonly struct OrderDeliverResult
    {
        public readonly DeliverCode Code;
        public readonly OrderSnapshotData Snapshot;
        public readonly long EnergyBalance;
        public readonly long PietyBalance;
        public readonly int FragmentItemId;
        public readonly long FragmentReward;
        public readonly long FragmentBalance;

        public OrderDeliverResult(DeliverCode code, OrderSnapshotData snapshot,
            long energyBalance = -1L, long pietyBalance = -1L,
            int fragmentItemId = 0, long fragmentReward = 0L, long fragmentBalance = -1L)
        {
            Code = code;
            Snapshot = snapshot;
            EnergyBalance = energyBalance;
            PietyBalance = pietyBalance;
            FragmentItemId = fragmentItemId;
            FragmentReward = fragmentReward;
            FragmentBalance = fragmentBalance;
        }
    }

    /// <summary>
    /// 订单交付 RPC 接缝(P1 全栈迁移·客户端段)。把网络层(<c>FantasyClient.FantasyNetwork.Session</c> + 协议消息)
    /// 抽到接缝后,使 <see cref="OrderSync"/> 保持纯逻辑、可 EditMode 单测(沿 <see cref="IRpcGateway"/> 范式)。
    /// </summary>
    /// <remarks>
    /// 生产实现 = <c>OrderRpcGatewayProd</c>(同目录,#if FANTASY_UNITY 包裹,内部调 Session.C2G_DeliverOrderRequest);
    /// 测试实现 = 桩(返预设 <see cref="OrderDeliverResult"/>,记录调用槽位)。
    /// 失败时(网络断 / 超时 / 异常)以 <see cref="DeliverCode.NetworkDown"/> / <see cref="DeliverCode.ServiceUnavailable"/> 返,
    /// <b>不抛异常</b>(沿 RemoteRankSource / RpcGatewayProd 范式)。
    /// </remarks>
    public interface IOrderRpcGateway
    {
        /// <summary>发起交付请求(仅带槽位,身份从会话取,奖励由服务端定)。失败以失败码返、不抛。</summary>
        UniTask<OrderDeliverResult> DeliverAsync(int slot);
    }
}
