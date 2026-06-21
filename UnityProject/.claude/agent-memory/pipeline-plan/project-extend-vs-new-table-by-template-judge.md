---
name: project-extend-vs-new-table-by-template-judge
description: 判定「扩展 vs 新建」表先认 TEngine 框架模板示例(demo 内容 + 无业务消费者),冲突时新建别扩展
metadata:
  type: project
---

简报称「工程可能已有 X 表/枚举」需 grep 核实「扩展 vs 新建」时:先看该表是否 TEngine 框架自带模板示例(判据:数据是 demo 内容如服装/test.*,且 grep 表类型名只命中 GameProto 生成代码、无 GameLogic 业务消费者)。

是模板示例 + 字段与 spec 冲突(如带 price/exchange_stream)→ 新建独立表(命名加语义后缀如 TbItemDef 区别 TbItem),不扩展——扩展等于把模板改生产表 + 删 demo 行 + 改被模板引用的枚举,回归面更大。枚举档数/语义不符(如现 EQuality 1-4 vs spec 1-6)同理新建,别改被模板表引用的旧枚举。

**Why:** 2026-06 item-system:item.TbItem 模板示例新建 TbItemDef 先例。

**How to apply:** 设计前 grep 表类型名:只命中生成代码 + 数据是 demo → 判模板;字段冲突时新建独立表+加语义后缀,枚举同理。
