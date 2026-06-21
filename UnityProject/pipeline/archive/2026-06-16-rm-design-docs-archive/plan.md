# 状态:策划(plan)

> 开工先读本文件(角色职责在 `.claude/agents/pipeline-plan.md`,spawn 时自动注入)。每完成一步就更新这里。

## 当前任务:rm-design-docs-archive(plan-only,boss 直接验收)

实体清理 design-docs 设计文档归档。plan-only(无 dev/test)。

### 交接区(供 boss 验收)

**改动摘要**

1. 删 4 个归档文件 + 目录(`git rm`,均已 staged 为 `D`):`design-docs/archive/` 下 `03-reference-gap-roadmap.html` / `04-proposal-combo-juice.html` / `05-proposal-adventure.html` / `08-collect-demo-slice.html`。`design-docs/archive/` 目录已不存在(进库前工作树已物理删除,本任务用 `git rm` 记录删除)。

2. `design-docs/assets/nav.js` 拆归档机制(4 处):
   - 删 `GROUPS` 里 `archived: true` 那一组(含 03/04/05/08 四条 docs)。
   - 删两处 `if (g.archived)` 渲染分支(`buildSidebar` 折叠成统一 `tree-group`;`buildCards` 折叠成统一 `cards`),非归档组渲染逻辑保留、无悬空 else。
   - 删 GROUPS 上方归档组说明注释(`// 归档组:archived:true…`)+ 文件头注释里两处 `archive/` 字样(`新增/归档→新增`、`单栏页(11、archive/)→单栏页(11)`)。
   - `.related` CSS 类未孤儿:活跃文档「相关文档」footer 仍用,保留。

3. 5 篇活跃文档清死链:
   - **02**(footer):删 03/04 两条 `<a href="archive/…">`。
   - **06**(footer):删 03 一条。
   - **07**(footer):删 03/08 两条。
   - **09**(重点·4 处):①subtitle、③§一正文 两处 `<a href="archive/08…">` 去链留 `<b>` 纯文字;②立项表「基线」行去链 + 加一处灰字简注「(收集 Demo 切片设计稿已删除,内容见 git 历史;元素层符号现状见 §七)」,`#hook` 为本页活锚(id="hook" 存在),导向 docs-tidy 轮已加的「旧名→现行符号」callout,二者一致不冲突;④footer 删 08/05 两条。**未动 §七 已有 callout**。
   - **10**(2 处):subtitle 把 08(移除对象)去链留 `<b>` 纯文字;footer 删 08 一条。

4. `design-docs/assets/style.css`(注释订正,非范围内但属同源清理):两处注释把「归档页(archive/)专用/沿用」改为真实消费者「单栏遗留页(11-core-loop-completion.html)」——`.topbar/.wrap` 类仍被 11 使用故保留(若删类会断 11),仅注释里指向已删归档页的陈述失实需订正(conventions 规则2 当前事实)。**未 bump `?v=`**:仅注释改动不影响渲染 CSS,无需缓存失效(`?v=` 意图是视觉 CSS 变更时强制重取)。

**自检结果(4 项逐条实测)**

- [1] 死链清零:`grep 'href="archive/'` design-docs 全库 = **0 命中**(PASS)。进一步 `grep "archive/"` 排除 `pipeline/archive` 后 = **0 命中**(含 style.css 注释也清干净)。
- [2] `node --check assets/nav.js` → **exit 0**(PASS)。
- [3] `ls design-docs/archive` → **No such file or directory**(目录不存在,PASS);4 文件 git 状态 = `D `(staged 删除)。
- [4] 09/10 改写段通读 + HTML 结构核:语义完整无「指向空」残句;5 篇 edited footer 各保留 1 个平衡 `related-links` 块(删行未破 HTML);09 新增灰字注的 `#hook` 锚点存在。
- 红线核:`pipeline/archive/` 引用(23/25/28「数据层已交付见 pipeline/archive/…」)**保留未碰**(PASS,与 design-docs archive 区分)。

**diff 范围**:archive-removal 实际触及 02/06/07/09/10 + nav.js + style.css(7 文件)+ 4 archive 删除。注:`01/11/12/13` 显 `M` 是前序 docs-tidy 遗留工作态(无 archive 引用、本任务未编辑),boss 关单时一并处置。

**需 boss 决策项**:无。
**taskFlaw**:无。

---

(最近:docs-tidy 整理旧设计文档,plan-only PASS 关单 2026-06-16,归档 `pipeline/archive/2026-06-16-docs-tidy/`)
