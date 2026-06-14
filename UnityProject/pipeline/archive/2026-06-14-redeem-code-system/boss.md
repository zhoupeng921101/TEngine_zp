# 归档:redeem-code 通用兑换码系统·数据逻辑层 + 服务器接缝

**结论:PASS 交付**。自治·放手默认,baton=full(plan→dev→test),**打回轮次 0**(一轮过,plan 零 blocker)。git 基线 `7b9b788d`(settings 关单,本轮起点工作树干净)。设计基线 `design-docs/20-redeem-code-system.html`(§五 dev 清单 8 项 / §六 验收 23 条 / §七 范围开关 O1–O8 / §八 风险表)。来源 spec `1006通用兑换码系统.xlsx`。

> 本轮经 pipeline-auto 自治工作流(plan→dev→test 三 agent),各环节交接稿见同目录 plan.md/dev.md/test.md;工作流 schema 返回的结构化结论(决策 + 测试计数)汇总于下方。

## 方向决策(2026-06-14 用户拍板)

剩余队列(兑换码/排行榜/邮件)本质是服务器/在线功能,与本作离线·去变现·无服务器方向不合;用户选「继续建底层,留服务器接缝」——当可复用通用底层做,逻辑层可测、服务器侧抽接缝 + TODO,离线版 inert。兑换码即此决策首个落地。

## 关键决策(O1–O8 均取设计稿默认,与 GDD 离线方向一致,无抵触)

- **O1 服务器接缝 = 可注入 `IRedeemValidator` 接口,非真发 HTTP**:工程无网络模块(grep 无 `INetworkModule`/`UnityWebRequest`/`HttpClient`,网络模块文档标待补充)。生产注 `LocalConfigRedeemValidator`(查本地 Luban 表);`RemoteRedeemValidator` 仅 stub 返 `SourceUnavailable`(不抛、零网络调用)。未来上后端实现一次,服务层零改动切换。
- **O2 发奖复用** 16 道具系统 `ItemGrant.GrantOnAcquire`,不在兑换码侧另定奖励类型(避免两套结构漂移)。
- **O3/O5 一次性去重**经既有 `Persistence.Provider` + 专用键 `Redeem.Redeemed`,本地单机判定;全局限量/有限次码不做(需后端计数,离线做不到,留 stub)。
- **O4 限时码**可注入 `NowProvider` 默认系统时钟,`ExpireTime` 空即不限时,parse 失败按不过期。
- **O6 结果文案**给互异非 0 占位 textId 110601–110606,真实多语言查表延后(同 num/item/reward/settings 现状)。
- **O7 UI 延后**(需美术):兑换码输入/结果弹窗;仅更新 `SettingsLinks.OpenRedeemCode()` TODO 注释指向服务入口。
- **O8 规整**:`Normalize` = trim + `ToUpperInvariant`,配置层存 key 与服务层比对走同一规整。
- **命名空间**:服务/校验/去重 `GameLogic.Redeem`(物理目录 `Module/Redeem/`);配置桥接 `RedeemConfigMgr` 归 `GameLogic.Config`;`GrantPayload` 仍在 `GameLogic.BlockBlast.Item`(复用不搬)。
- **Luban**:`redeemcode` 表 string 主键 `code`(index=code,mode=map),`redeemreward` 子表 auto_id 主键 + code 外键按规整码聚合。`DOTNET_ROLL_FORWARD=Major` 直调 Luban.dll 重生成两表。
- **结果码顺序**:空输入 → SourceUnavailable → NotFound → Expired → AlreadyRedeemed → 发奖 → MarkRedeemed;记录已兑换在发奖成功之后(失败不占名额)。

## 测试结论(test agent)

- 单测:隔离跑 `BlockBlast.Tests` 全量 **311/311**,再隔离跑 Redeem fixture 带 include_details **17/17 逐例 Passed**(确认真实执行非静默跳过)。
- C3 Luban 直读 `redeemcode.bytes` 实测通过(行数>0 + 字段映射 + 多奖聚合断言全过),导表工具链可达,判 PASS 非 BLOCKED。
- 手验第 3 类判 N/A:纯逻辑层 + 接缝,EditMode 全覆盖(POCO + InitForTest 注入 + InMemory provider/store + state 可 null 纯解析),无拖拽/窗口路径需 Play 复现;UI(O7)延后轮,非缺口。
- Code Review 5 红线过,重点核「无真实网络调用」「PlayerPrefs 非阻塞不触同步 IO」「发奖复用 16 不复制落点」。

## 遗留(转主 boss.md #25)

真实服务器校验(无网络模块,未来实现 `RemoteRedeemValidator`)/ 兑换码输入窗口 + 结果弹窗 UI + 真实 Sprite / 设置界面兑换码入口按钮接线。

## 附:本轮文档库结构变更

开工期间 design-docs 全库迁到 `assets/nav.js` 数据驱动导航(侧边栏文档树 + 首页卡片单源在 nav.js GROUPS);doc 20 按新 thin-sidebar 骨架写,新增文档只在 GROUPS 加一行即全库同步。旧「逐篇同步 sidebar」经验对本库失效,已更新 `pipeline/memory/plan.md`。
