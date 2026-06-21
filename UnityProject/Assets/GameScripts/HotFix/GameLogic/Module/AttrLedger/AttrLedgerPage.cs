using System;
using System.Collections.Generic;

namespace GameLogic.AttrLedger
{
    /// <summary>
    /// 客户端 ledger 查询结果码(设计 46 §3.1,沿 45 §3.3 + 38 ChangeReject 范式补 NetworkDown)。
    /// 4 档区分「服务端裁决」与「客户端断网」,便于排错与文案兜底。
    /// </summary>
    public enum AttrLedgerQueryCode
    {
        /// <summary>查询成功(含返空数组的成功:limit=0 / 账号无 ledger / 过滤后无匹配)。</summary>
        Success = 0,
        /// <summary>参数非法(协议层 InvalidRequest;理论上客户端发的参数都合法,出现此码 = 客户端 bug)。</summary>
        InvalidRequest = 1,
        /// <summary>服务不可用(MongoDB 不可达 / 查询抛 Mongo 异常)。</summary>
        ServiceUnavailable = 2,
        /// <summary>客户端断网(Session 未建立 / 未登录 / RPC 抛异常 / 超时)。</summary>
        NetworkDown = 3,
    }

    /// <summary>
    /// 一次查询的结果包(设计 46 §3.1):结果码 + 已 timestamp DESC 排序的流水条目 + 是否还有更旧的行。
    /// 失败时 <see cref="Entries"/> 为空集合(非 null),<see cref="HasMore"/> = false。
    /// </summary>
    public sealed class AttrLedgerPage
    {
        /// <summary>裁决结果码。</summary>
        public AttrLedgerQueryCode Code;
        /// <summary>条目列表(按服务端返的 Timestamp DESC,最新在前);失败时空集合(非 null)。</summary>
        public IReadOnlyList<AttrLedgerEntry> Entries = Array.Empty<AttrLedgerEntry>();
        /// <summary>是否还有更旧的行(45 §3.2 hasMore);失败时 false。</summary>
        public bool HasMore;

        /// <summary>服务不可用快捷构造(无条目)。</summary>
        public static AttrLedgerPage ServiceUnavailable
            => new AttrLedgerPage { Code = AttrLedgerQueryCode.ServiceUnavailable };

        /// <summary>客户端断网快捷构造(无条目)。</summary>
        public static AttrLedgerPage NetworkDown
            => new AttrLedgerPage { Code = AttrLedgerQueryCode.NetworkDown };

        /// <summary>非法请求快捷构造(无条目)。</summary>
        public static AttrLedgerPage InvalidRequest
            => new AttrLedgerPage { Code = AttrLedgerQueryCode.InvalidRequest };
    }
}
