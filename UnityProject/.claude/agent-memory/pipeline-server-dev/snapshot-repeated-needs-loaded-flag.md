---
name: snapshot-repeated-needs-loaded-flag
description: 下行协议用 repeated 字段承载「整份覆盖」快照投影时,proto3 无法区分「权威空集」与「降级未取到」;响应必须显式携带 loaded/valid 布尔,否则服务端读库失败的空占位会被客户端当权威空集整份清空本地投影
metadata:
  type: rule
---

服务端往客户端下发「整份覆盖」语义的快照段(道具持有 / 收集集合 / 解锁列表等 repeated 字段)时,**proto3 的 repeated 字段没有「缺失」态**——服务端降级路径(未登录 / MongoDB 读失败 / doc 为 null)不填字段,与「玩家真的一无所有」在线上是同一个空列表编码。客户端按「整份覆盖」应用就会把玩家已有投影清空,直到下次成功同步才恢复(表现为背包/图鉴瞬间清零再回来)。

修法:响应加一个显式布尔(如 `ItemDataLoaded` / `CollectedValid`),**只在真读到玩家文档时置 true**;服务端 helper 用可空返回值(`List<T>?`,null = 未取到)承载该区分,handler 据 null 置标志;客户端 gateway 把 false 映射成框架中立 DTO 的 null,应用层对 null 保留既有投影、对空数组照常覆盖清空。

**Why:** 「失败响应也回带当前态供对齐」是常见好意(AlreadyDelivered/NotEnough 等读过库的失败码确实该带),但它让人误以为所有失败码都带权威态;实际未读库的降级路径带的是空占位,两者在 repeated 编码上不可分。塔罗收集首轮实现即踩此坑:合成时 Mongo 瞬断 → 客户端图鉴全部已合成牌被清空。每新增一段快照投影(道具/收集/解锁/邮件列表)都会重演这个选择,属结构性复发。

**How to apply:** ① 设计下行快照段时,凡客户端按「整份覆盖」应用的 repeated 字段,响应同时加 loaded/valid 布尔,并在字段注释写明「仅 loaded=true 时可信」;② 服务端所有 return 路径自查:该字段是「读过玩家文档得出的值」还是「没读到的占位」,占位路径必须让标志为 false;③ 客户端契约写成三态:null=保留投影 / 空=权威空集清空 / 非空=覆盖,并给三态各写一条 EditMode 单测。
