---
name: project-luban-string-primary-key
description: Luban 表主键可为 string(兑换码 code),子表(一主多从)用 auto_id 主键 + 外键分组,运行期桥接按外键 Normalize 后聚合进 List。
metadata:
  type: project
---

Luban 表主键可为 string(如兑换码 `code`):`__tables__.xlsx` 注册 `index=<string列>`、`mode=map`,生成的 `TbXxx.DataMap` key 即 string;子表(一主多从,如码→多奖励)用自增 `auto_id` 做主键(`index=auto_id`)、另留一列做外键分组、运行期桥接里按外键 `Normalize` 后聚合进主定义的 List(同 ItemConfigMgr 按 index 聚合礼包)。group=e 的列(策划备注)正确从 client 目标行类剔除,桥接 POCO 不含它。

**Why:** Luban 默认例子多是 int 主键,string 主键 + 一主多从是常见需求但易踩坑;group=e 列(策划备注)若残留进客户端 POCO 会冗余 + 暴露策划文案(2026-06,redeem-code)。

**How to apply:** 新表设计:① 主键非 int 直接写类型(`code,##type=string`);② 一主多从用 auto_id + 外键列;③ 桥接里按外键聚合;④ 策划备注列加 `group=e` 排除客户端;⑤ 校验生成的 POCO 不含 group=e 列。
