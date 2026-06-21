using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.BlockBlast.Player;
#if FANTASY_UNITY
using Fantasy; // 协议消息 + NetworkProtocolHelper 扩展方法 C2G_QueryAttrLedger 所在命名空间
#endif

namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 远程 ledger 数据源生产实现(设计 46 §3.2)。
    /// 经 <c>FantasyClient.FantasyNetwork.Session</c> 发 <c>C2G_QueryAttrLedger</c> 同步等响应,
    /// 把协议 <c>G2C_QueryAttrLedgerResponse</c> 反序列化为客户端 <see cref="AttrLedgerPage"/>。
    /// </summary>
    /// <remarks>
    /// 降级(沿 38 RpcGatewayProd + 32 RemoteMailSource 范式):未连接 / 未登录 / 发不出 / 超时 / 任何往返失败 →
    /// 返 <see cref="AttrLedgerPage.NetworkDown"/>(客户端层错误)或服务端返的 ServiceUnavailable,<b>不抛异常</b>。
    /// 程序集边界:网络层 FantasyClient / Fantasy.Unity 受 FANTASY_UNITY 约束;该 define 关闭的平台无网络可用,
    /// 本类同样降级返 NetworkDown,使 GameLogic 在任何平台都可编译。
    /// 把「服务端响应 → 客户端 AttrLedgerPage」的纯转换抽成非 FANTASY_UNITY-gated 静态方法
    /// (<see cref="MapResultCode"/> / <see cref="KindToProtocolInt"/>),可 EditMode 直测;FANTASY_UNITY-gated 的
    /// <see cref="MapResponse"/>(读 Fantasy 协议字段)只薄薄调它,字段读取交真往返核(E1)。
    /// </remarks>
    public sealed class RemoteAttrLedgerSource : IAttrLedgerSource
    {
        public async UniTask<AttrLedgerPage> FetchPageAsync(AttrType? kind, long sinceTs, int limit)
        {
#if FANTASY_UNITY
            var session = FantasyClient.FantasyNetwork.Session;
            if (session == null || !FantasyClient.FantasyNetwork.IsConnected)
            {
                return AttrLedgerPage.NetworkDown; // 未连接:不发请求、不抛
            }
            if (!FantasyClient.FantasyNetwork.IsLoggedIn)
            {
                return AttrLedgerPage.NetworkDown; // 未登录:身份从会话取,未登录则不发(协议层会返 InvalidRequest)
            }

            int protocolKind = KindToProtocolInt(kind);

            G2C_QueryAttrLedgerResponse response;
            try
            {
                // FTask 自带 awaiter,可在 async UniTask 体内直接 await(memory「跨框架通用异步与网络库异步」)
                response = await session.C2G_QueryAttrLedger(protocolKind, sinceTs, limit);
            }
            catch
            {
                return AttrLedgerPage.NetworkDown; // 发不出 / 超时 / 往返异常:降级,不抛
            }

            if (response == null)
            {
                return AttrLedgerPage.ServiceUnavailable;
            }

            return MapResponse(response);
#else
            // FANTASY_UNITY 关闭(无网络平台):降级返 NetworkDown,不抛
            await UniTask.CompletedTask;
            return AttrLedgerPage.NetworkDown;
#endif
        }

        /// <summary>
        /// 客户端 <see cref="AttrType"/> → 协议 kind 整数(45 §3.1):
        /// null → 0(不过滤);Coin/Diamond/Stamina → 1/2/3(协议层错开一位,0 留作 sentinel,见服务端 AttrLedgerQueryHelper)。
        /// </summary>
        public static int KindToProtocolInt(AttrType? kind)
        {
            if (kind == null) return 0;
            switch (kind.Value)
            {
                case AttrType.Coin:    return 1;
                case AttrType.Diamond: return 2;
                case AttrType.Stamina: return 3;
                default:               return 0; // All / 未知 → 不过滤(防御)
            }
        }

        /// <summary>
        /// 协议结果码(int)→ 客户端 <see cref="AttrLedgerQueryCode"/>;未知码按 ServiceUnavailable 兜底,不崩。
        /// 协议层无 NetworkDown(NetworkDown 是客户端独占,RPC 到不了服务端则在调用方层提早返)。
        /// </summary>
        public static AttrLedgerQueryCode MapResultCode(int rawCode)
        {
            switch (rawCode)
            {
                case 0: return AttrLedgerQueryCode.Success;            // AttrLedgerQueryResultCode.Success
                case 1: return AttrLedgerQueryCode.InvalidRequest;     // InvalidRequest
                case 2: return AttrLedgerQueryCode.ServiceUnavailable; // ServiceUnavailable
                default: return AttrLedgerQueryCode.ServiceUnavailable;
            }
        }

#if FANTASY_UNITY
        /// <summary>把服务端查询响应转客户端 <see cref="AttrLedgerPage"/>(薄壳,读 Fantasy 协议字段后调纯转换)。</summary>
        private static AttrLedgerPage MapResponse(G2C_QueryAttrLedgerResponse response)
        {
            var code = MapResultCode((int)response.ResultCode);
            if (code != AttrLedgerQueryCode.Success)
            {
                // 失败分支:不附条目(协议响应虽 Entries 可能空,客户端统一返空)
                return new AttrLedgerPage { Code = code };
            }

            var entries = new List<AttrLedgerEntry>();
            if (response.Entries != null)
            {
                foreach (var e in response.Entries)
                {
                    if (e == null) continue;
                    entries.Add(new AttrLedgerEntry
                    {
                        Timestamp     = e.Timestamp,
                        // PropertyType 整数与 AttrType 一一映射(Coin=0/Diamond=1/Stamina=2)
                        Kind          = (AttrType)(int)e.Kind,
                        BalanceBefore = e.BalanceBefore,
                        BalanceAfter  = e.BalanceAfter,
                        Delta         = e.Delta,
                        // 协议 Source 是 int;客户端按 enum 转,未知整数 → Unknown(0)兜底(FormatSource default 出「其他」)
                        Source        = SafeSource(e.Source),
                        ReasonRaw     = e.ReasonRaw ?? string.Empty,
                    });
                }
            }
            return new AttrLedgerPage
            {
                Code = AttrLedgerQueryCode.Success,
                Entries = entries,
                HasMore = response.HasMore,
            };
        }

        /// <summary>协议 source 整数 → 客户端枚举;未登记的整数返 Unknown(UI 文案降级为「其他」)。</summary>
        private static AttrChangeSource SafeSource(int raw)
        {
            // 已登记 0..9 直接转;超出范围 → Unknown(0)
            if (raw >= 0 && raw <= 9) return (AttrChangeSource)raw;
            return AttrChangeSource.Unknown;
        }
#endif
    }
}
