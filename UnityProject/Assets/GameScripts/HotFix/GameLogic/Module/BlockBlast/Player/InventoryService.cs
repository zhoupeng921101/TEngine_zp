using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace GameLogic.BlockBlast.Player
{
    /// <summary>
    /// 背包批次轨客户端投影 + 使用事务入口(背包系统·客户端段)。服务端是唯一事实源,本类只持投影:
    /// 批次轨(有有效期道具)由登录快照 / 背包推送整份覆盖;堆叠轨(itemId→数量)由 <see cref="GameLogic.BlockBlast.Item.ItemBag"/> 承载(接线层分派)。
    /// </summary>
    /// <remarks>
    /// 纯逻辑 C#(不依赖 UnityEngine / 网络库,可 EditMode 测);批次不入本地存档(服务端可重建,合 data-authority 白名单)。
    /// 时间基准:快照/推送带服务端权威时刻 ServerNowMs,登录锚定 offset = ServerNowMs - 本地now;批次剩余 = ExpireMs -(本地now + offset),
    ///   不信客户端本地墙钟(防改系统时间伪造有效期)。
    /// 三态覆盖(防误清):loaded=false(服务端读库降级的空占位)时保留既有投影不清空;loaded=true 才整份覆盖(沿 TarotCollection CollectedValid 范式)。
    /// 使用事务:<see cref="TryUseAsync"/> 经 <see cref="IInventoryRpcGateway"/> 发请求,reqSeq 全账号单调 + 串行(在途闸);
    ///   投影更新不由本方法直接改——成功/重复由服务端背包推送整份覆盖,货币产出由属性推送刷新(沿 PlayerAttrService 权威覆盖语义,避免乐观漂移)。
    /// </remarks>
    public sealed class InventoryService
    {
        private readonly IInventoryRpcGateway _gateway;
        private readonly Func<long> _nowMs;

        private readonly List<InventoryLot> _lots = new List<InventoryLot>();
        private long _serverTimeOffsetMs;   // = ServerNowMs - 本地now(登录/推送时锚定)
        private long _lastReqSeq;            // 已签发的末个使用请求序号(全账号单调,跨会话由墙钟推高)
        private bool _useInFlight;           // 串行闸:一次使用未回来前不发下一个

        /// <summary>是否已收到首份背包(true 后 <see cref="Lots"/> 才是服务端权威投影)。UI 据此切「加载中」。</summary>
        public bool IsReady { get; private set; }

        /// <summary>批次轨投影(只读;含可能已本地倒计时到 0 的批次,UI 据 <see cref="IsExpired"/> 置灰)。</summary>
        public IReadOnlyList<InventoryLot> Lots => _lots;

        /// <summary>背包投影变更事件(快照 / 推送覆盖后触发一次,供 UI 重绘)。</summary>
        public event Action OnInventoryChanged;

        /// <summary>构造(注入 RPC 接缝 + 可选时钟)。生产用 <see cref="InventoryRpcGatewayProd"/>;时钟默认系统 UTC 毫秒,测试注入定值。</summary>
        public InventoryService(IInventoryRpcGateway gateway, Func<long> nowMsProvider = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _nowMs = nowMsProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        /// <summary>
        /// 登录 seed 使用幂等锚底(服务端快照下发的 LastUseReqSeq)。把本地 reqSeq 底抬到不低于服务端已处理值,
        /// 保重登后首个使用序号严格大于服务端持久值——消除「客户端时钟回拨 → 首次使用被误判 Duplicate」边界(墙钟推高的兜底)。
        /// 只增不减(max);背包推送传 0 时为 no-op。
        /// </summary>
        public void SeedReqSeq(long serverLastUseReqSeq)
        {
            if (serverLastUseReqSeq > _lastReqSeq)
            {
                _lastReqSeq = serverLastUseReqSeq;
            }
        }

        /// <summary>服务端权威当前时刻(= 本地now + 锚定 offset)。批次倒计时基准。</summary>
        public long ServerNowMs() => _nowMs() + _serverTimeOffsetMs;

        /// <summary>批次剩余毫秒(可为负 = 已过期)。ExpireMs<=0 视为永不过期,返 long.MaxValue。</summary>
        public long RemainingMs(InventoryLot lot)
            => lot.ExpireMs > 0 ? lot.ExpireMs - ServerNowMs() : long.MaxValue;

        /// <summary>批次是否已过期(基于服务端时间基准,非本地墙钟)。</summary>
        public bool IsExpired(InventoryLot lot) => lot.ExpireMs > 0 && ServerNowMs() >= lot.ExpireMs;

        /// <summary>
        /// 应用背包批次轨快照 / 推送(登录 G2C_PlayerInfoSnapshot 的背包段,或 G2C_InventoryDeltaPush)。
        /// loaded=false → 保留既有投影不清空(服务端读库降级的空占位,防误清);loaded=true → 整份覆盖 + 锚定服务端时间基准 + 置 IsReady。
        /// lots 为 null 且 loaded=true 视作权威空集(全清)。
        /// </summary>
        public void ApplyLotsSnapshot(IReadOnlyList<InventoryLot> lots, long serverNowMs, bool loaded)
        {
            if (!loaded)
            {
                return; // 三态:未取到权威整份 → 保留投影,不覆盖不清空。
            }
            _serverTimeOffsetMs = serverNowMs - _nowMs();
            _lots.Clear();
            if (lots != null)
            {
                for (int i = 0; i < lots.Count; i++) _lots.Add(lots[i]);
            }
            IsReady = true;
            OnInventoryChanged?.Invoke();
        }

        /// <summary>
        /// 发起使用道具(改名扣钻式同步等响应)。reqSeq 全账号单调递增 + 墙钟推高(跨会话不回退,防重登后低序号被服务端误判 Duplicate);
        /// 串行闸:上一次使用未回来前拒新请求(守服务端「reqSeq 单调 + 串行」契约,避免乱序低序号被误判)。
        /// 成功/重复:投影由随后服务端背包推送整份覆盖、货币由属性推送刷新(本方法不改投影);失败:投影不动,UI 据码提示。
        /// </summary>
        public async UniTask<UseItemResult> TryUseAsync(int itemId, long count)
        {
            if (itemId <= 0 || count <= 0)
            {
                return UseItemResult.Fail(UseItemCode.InvalidRequest, itemId);
            }
            if (_useInFlight)
            {
                // 串行契约:UI 正常应在使用期禁用按钮;此闸兜底防并发发号导致服务端乱序误判。
                return UseItemResult.Fail(UseItemCode.ServiceUnavailable, itemId);
            }

            // reqSeq = max(上个+1, 墙钟毫秒):会话内严格递增,跨重登由墙钟跳到当前毫秒(> 服务端持久的上次序号)。
            long seq = Math.Max(_lastReqSeq + 1, _nowMs());
            _lastReqSeq = seq;

            _useInFlight = true;
            try
            {
                return await _gateway.SendUseItemAsync(itemId, count, seq);
            }
            finally
            {
                _useInFlight = false;
            }
        }
    }
}
