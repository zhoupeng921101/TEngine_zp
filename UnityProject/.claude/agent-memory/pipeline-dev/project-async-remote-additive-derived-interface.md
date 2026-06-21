---
name: project-async-remote-additive-derived-interface
description: 给同步源加异步 RPC 远程源且零回归:派生接口 IRemote:IBase 带 async,服务层加可选 remote 参 + 异步入口,远程返 null 降级回本地;降级编排放服务层不放远程源。
metadata:
  type: project
---

给既有「同步数据源接缝 + 服务层排序」加异步 RPC 远程源(占位切真实)且要零回归:**加法式扩接口、不重构同步路径**——既有同步 `IXxxSource.Fetch`(返原始记录)+ 服务层同步 `GetBoard`(过滤/排序/回填)逐字节不动;远程能力做成**派生接口** `IRemoteXxxSource : IXxxSource` 带异步方法(`QueryBoardAsync`/`SubmitScoreAsync` 返 UniTask),服务层加**可选** `IRemoteXxxSource remote=null` 构造参 + 异步入口 `GetBoardAsync`:remote 非 null 走 RPC(服务端已排好→**短路本地排序**,直接采用)、返 null/失败回退已注入的本地源同步路径;remote=null(离线)异步入口直接包同步本地路径。

这样既有单测 + UI 同步调用全不破,远程是纯增量。远程源的 `Fetch` 返空(远程记录在服务端、不走客户端排序路径)。**降级编排放服务层、不放远程源内**(远程返 null→服务层走本地 source),本地源天然是降级回退源。

把「服务端响应→查询快照」的纯转换抽成非 `FANTASY_UNITY`-gated 静态方法(`BuildBoardFromServer(entries,myRank,myScore)`,IsSelf 按名次唯一匹配),可 EditMode 直测;`FANTASY_UNITY`-gated 的 `MapResponse`(读 Fantasy 协议字段)只薄薄调它,字段读取交真往返核。

**Why:** 重构同步路径会引入回归;派生接口 + 可选参 = 老调用方零改动 + 新调用方按需启用;降级逻辑放服务层一处,远程源职责单一(就是 RPC);纯转换抽出 EditMode 测,gate 内只剩协议字段读取(2026-06,rank-client)。

**How to apply:** 给同步源加远程能力:① 派生接口 IRemote:IBase 加 async 方法;② 服务层加 `remote=null` 构造参 + 异步入口;③ 异步入口:remote 优先走 RPC,失败/null 回退同步本地;④ 远程 Fetch 返空;⑤ 纯转换 static 抽出 EditMode 直测;⑥ 协议字段读取放 gate。
