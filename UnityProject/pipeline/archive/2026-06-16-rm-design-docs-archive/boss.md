# Boss 关单总结:rm-design-docs-archive 移除设计文档归档(2026-06-16)

## 任务定义
实体清理 `design-docs/archive/`(03/04/05/08 四篇 + 目录)+ 拆 nav.js 归档机制 + 清 02/06/07/09/10 指向 archive 的死链。源起:用户「要删除/重写,不要加注」方向——archive 既被有意删除,正式清理其实体与全部引用,设计脉络断点靠 git 找回。

## 参与环节
plan-only(纯 design-docs 清理,无 Unity 代码)。boss 直接验收。0 打回。

## 用户拍板(2026-06-16)
连 08 一起删;指向 archive 的引用改「见 git 历史」无链文字;接受库内设计脉络断、靠 git 找回(主会话 4 选 1 + 路由 2 选 1 显式确认)。

## 验收结论:PASS(boss 直验)
- 断链:`grep 'href="archive/'` design-docs 全库 0 命中;排除 pipeline/archive 后 `archive/` 全库 0 命中(含 style.css 注释)。
- `node --check assets/nav.js` exit 0。
- `design-docs/archive/` 目录不存在;4 文件 git `D `(staged 删除)。
- 改动:删 4 archive 文件;nav.js 拆 archived 组 + 两处 `if(g.archived)` 渲染分支 + 归档注释;02/06/07/09/10 footer/正文死链改无链文字(09 立项表加灰字简注「收集 Demo 设计稿已删,见 git 历史」指本页 #hook 活锚);style.css 两处注释订正(归档页→单栏遗留页 11)。

## 与 docs-tidy 的关系 / 遗留
- 09/10 的死链清理与 docs-tidy 轮加的「旧名→现行符号」callout 共存不冲突(后者指 #hook 本页活锚)。
- **遗留(交下一步)**:docs-tidy 轮对 01/02/09/10/11/12/13 加的**状态/勘误注**,与用户新方向「不要加注,要重写或删除」抵触,须转为重写/删除。本任务只清 archive 实体,未动这批 annotation——待「rewrite 规则」定稿后统一转换。
- 工作树未提交:design-docs 9 篇 + nav.js + style.css + 4 archive 删除 + pipeline state。用户自行提交。

## 规则侧(主会话先行处理)
归档机制的规则段(pipeline-plan 文档生命周期描述 + nav.js 机制注释 + plan memory 相关条)已由主会话先行拆除/订正。
