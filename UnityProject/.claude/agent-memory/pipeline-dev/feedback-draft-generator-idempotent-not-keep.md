---
name: feedback-draft-generator-idempotent-not-keep
description: 草稿型配置生成器与 packer「拒覆盖」语义相反:应幂等覆盖刷新但会冲掉人工校正;交接显式标「重跑覆盖人工调参」+ 跑完即删不留库。
metadata:
  type: feedback
---

Editor 工具产「草稿型」配置(自动探测的 border/json,须人工核校的)与 packer 的「拒覆盖」语义相反:草稿生成器应**幂等覆盖**(重跑刷新),但这意味着会冲掉人工已手填的校正——交接必显式标「重跑覆盖人工调参」陷阱 + 「验证用产物跑完即删、不留库」(自动探测值常≠生产手调值,留库会让下次重 Pack 用错 border)。

**Why:** 自动探测的 border/对齐值在大部分图上够用,但策划/美术手调的精细值才是真实生产值;草稿留库会被下次 Pack 当真值用,引入数值漂移(2026-06,border-override)。

**How to apply:** 草稿生成器:① 文档里显式注「重跑覆盖人工调参,使用前确认无人工版本」;② 验证产出在临时目录,验完删除不入 git;③ packer 路径用人工校正版,不读草稿;④ 在生成器输出加 banner 注释「Auto-generated draft, do not commit」。
