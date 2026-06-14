# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:排行榜系统 · 数据逻辑层 + 服务器接缝(离线可测)

验收基线 `design-docs/22-rank-system.html`(§六 26 验收点)。被测改动见 `pipeline/state/dev.md` 交接区。

### 总判定:PASS

四类验证全部通过。编译 0 error;EditMode 359/359 全绿(新增 Rank 24 条 + 既有 335 条零回归);Luban 直读 C3 取 GREEN(工具链可达,bytes 字段映射正确);Code Review 5 红线 + 方向约束(无网络 / 复用邮件 21 不另造发奖 / 复用既有 Provider / 邮件 21 零改动)全过。本轮纯数据逻辑层 + 接缝,无 Play 模式手验点(UI 表现层 + 真实全服榜 + 远程实现转遗留,设计 §七 O1/O2/O8)。

### 一、编译验证 — PASS

- `/unity-check` 三步通过:server 9.7.1 在跑,`active_instance=UnityProject@02a6dcaa` 活着,场景 `main` 加载,工程对得上。
- `refresh_unity(compile=request, mode=force, scope=all)` → 编译完成,`manage_scene get_active` 桥重注册成功。
- `read_console(types=error)` = 0 条;`read_console(types=warning, filter=Rank)` = 0 条。**编译 0 报错 0 警告。**

### 二、单元测试 — PASS

- 隔离跑 `run_tests(EditMode, assembly=BlockBlast.Tests)`:**359/359 passed,0 failed,0 skipped**(job `47291c42…`,0.37s)。与 dev 交接区一致(24 Rank + 335 既有,邮件 21 等既有系统零回归)。
- 单跑 RankSystemTests fixture(`include_details`,job `18e186d5…`):**24/24 全 `state==Passed`**,无静默跳过。逐例确认:
  - C1/C2 配置 + TierForRank 命中 = Passed;**C2b** = Passed(占位 Assert.Pass,说明 EnsureLoaded 走 ConfigSystem 不在 EditMode 测,符合 mail/item 先例)。
  - **C3 LubanDirectRead = Passed(非 Inconclusive)**:`rank_tbrank.bytes` 真实存在,`new TbRank(ByteBuf)` 解出 4 行,聚合 id=1 三档 + id=2 一档、Tiers 按 RankMin 升序(1/2/11)、reward 映射 1002/1005/1006/1007、ValidType Weekly/Always 全中。按 test 记忆「C3 取 GREEN 即同时构成源 xlsx→bytes 一致性最强交叉核验」,导表工具链此处可达、字节布局正确,配置侧不列 BLOCKED。
  - 排序 SO1/SO2/SO3、结算时机 DUE1–4、结算编排 ST1/ST2/ST3/ST3b、每日点赞 DP1/DP2/DP3、红点 RD1/RD2、持久化 P1/P2、接缝 SK1/SK2 = 全 Passed。

### 三、手动功能验证 — PASS(本轮无 Play 手验点,功能经注入式 EditMode 覆盖)

dev 交接区 §四 标注「需进 Play 模式手验的功能点:无」。本轮纯数据逻辑层 + 接缝,无 UI、无资源加载、无指针交互;全部功能路径(查榜 / 排序并列 / 结算时机四档 / 结算编排发邮件 / 幂等 / 每日点赞跨天 / 红点 / 持久化往返 / 接缝换源)经注入 NowProvider + 注入 IMailService(fake `RecordingMailService` 断言「发了挂哪个奖励库 id 的邮件」)+ 注入 IRankSource + InMemory 持久化在 EditMode 完整驱动。逐组对应 §六验收 → 实际表现:

| 验收组 | 验收点 | 对应用例 | 结果 |
|--------|--------|----------|------|
| 配置 C | C1/C2/C3 | C1_*, C2_*, C3_*(+C2b 占位) | PASS(C3 GREEN) |
| 查榜排序 SO | SO1/SO2/SO3 | SO1_*, SO2_*, SO3_* | PASS |
| 结算时机 DUE | DUE1–4 | DUE1–4_* | PASS(周循环 ISO 周 + 下周再 true) |
| 结算编排 ST | ST1/ST2/ST3/ST3b | ST1_*, ST2_*, ST3_*, ST3b_* | PASS(发奖挂对应库 id、幂等、边界兜底) |
| 每日点赞 DP | DP1/DP2/DP3 | DP1_*, DP2_*, DP3_* | PASS(跨天重置、均经邮件) |
| 红点 RD | RD1/RD2 | RD1_*, RD2_* | PASS(结算后交邮件红点不重复亮) |
| 持久化 P | P1/P2 | P1_*, P2_* | PASS(跨实例往返保真、脏数据保底不抛) |
| 接缝 SK | SK1/SK2 | SK1_*, SK2_* | PASS(Remote 返空不连网、换源服务层零改动) |

### 四、Code Review — PASS

文件清单逐条 diff review(磁盘核盘:git status 见全部交付物存在 —— `rank.xlsx`/`rank_tbrank.bytes`/6 个 .cs/测试 + `__tables__.xlsx`、`Tables.cs`、`Rank.cs`、`rank.TbRank.cs`)。

CLAUDE.md 核心原则(编码红线)逐条核对(以正本为准):
1. **异步优先**:本层无 IO 协程,持久化走 PlayerPrefs(非阻塞内存级,同 14/19/20/21 口径),不触红线。
2. **模块访问**:本层纯逻辑 + 注入,运行期配置经 `ConfigSystem.Instance.Tables.TbRank`(EnsureLoaded,与既有 MailConfigMgr 同款),无 `ModuleSystem.GetModule<T>()` 误用。
3. **资源释放**:本层无 `LoadAssetAsync`/GameObject 加载,无释放点。
4. **热更边界**:全部落 `GameScripts/HotFix/GameLogic`(热更区),无 Main 区改动。
5. **事件解耦**:本层无 GameEvent / AddUIEvent(无 UI、无跨模块事件),不触泄漏 / 风暴反模式。

方向约束 + 复用核验(grep + 静态签名核实):
- **无网络**:`grep UnityWebRequest|HttpClient|System.Net|WebClient|TcpClient|Socket` over `Module/Rank/` = 0 命中。`RemoteRankSource.Fetch` 返 `Array.Empty<RankEntry>()` 不连网不抛。
- **复用邮件 21 不另造发奖**:结算 / 每日 / 点赞三处发奖均经注入 `GameLogic.Mail.IMailService.Send(MailDraft)`;`grep MergeOrderState|GiftOpener|ItemGrant` over `Module/Rank/` = 0 命中(排名层不碰)。`IMailService.Send`、`MailDraft.FromTemplate(int,int)`(模板缺返 null,ST3 兜底依赖此)、`MailDraft` 字段(Sender/Title/Body/Expire/RewardPoolId)签名经 grep 核实真实存在。
- **复用既有 Provider**:`RankPersistence` 包 `GameLogic.BlockBlast.Persistence.Provider`(`TryGet/Set` 签名核实),键 `Rank.Progress`(专用、不冲突),JsonUtility + try/catch 保底,未另造存储栈。
- **邮件 21 零改动**:`git status Module/Mail/` + `Persistence.cs` = 工作树干净,确为只调用不修改。
- **按 id 聚合**:`RankConfigMgr.Aggregate` 首遇行定榜级字段、各行追加 Tier、最后按 RankMin 升序,与 16/20 主+子聚合同源;`TbRank.DataMap` 按 RowId(合成行主键)键、`Id` 可重复,Luban 注册正确。

持久文件交叉检(conventions lint + 人工通读 dev 改过的 `state/dev.md` / `memory/dev.md`):lint 0 命中(无指代词 / diff 叙事 / 拟人比喻);memory/dev.md 本轮无内容新增(仅行尾换行触碰)。合规。

### 备注(记录项,不影响判定)

- `RankService.GetMyBest` 用 `RankText.SettleSender` 当占位玩家名 textId,语义上略错位(借了邮件发件人 textId 作展示名),但属 O6 文案占位范畴、非验收项、不影响任何逻辑断言。后续真实多语言查表时一并归位即可,本轮不阻塞。
- 测试覆盖无缺口:24 用例覆盖 §六 26 验收点(C2b 占位说明 + C3 真实表覆盖 EnsureLoaded 聚合,SK1 含 grep 无网络)。新增逻辑均有对应断言。
