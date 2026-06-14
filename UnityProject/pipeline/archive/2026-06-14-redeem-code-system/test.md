# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前被测任务:redeem-code 通用兑换码系统·数据逻辑层 + 服务器接缝(自治·放手默认)

设计稿 `design-docs/20-redeem-code-system.html`。验收判据为该稿 §六(23 条验收点 C1–G3 + R1/R2)。

### 总判定:PASS

四类验证全部通过,零代码缺陷。Unity MCP 连接正常(`UnityProject@02a6dcaa`,scene `main` 响应),四类运行验证全程可达,非 BLOCKED。

### 验证证据

**0. 连接自检(/unity-check)**:`debug_request_context` 返 active_instance=`UnityProject@02a6dcaa`;`manage_scene get_active` 返 scene `main`(响应正常)。按名绑定正确实例,自检通过。

**1. 编译验证 — PASS**
- `refresh_unity(force, compile=request, wait_for_ready)` → 触发编译;编译后 `manage_scene get_active` 正常响应,证编译完成 + 桥重注册。
- `read_console(types=[error])`:仅 1 条 MCP 传输噪声(`Cannot access a disposed object`,refresh 瞬态,非编译错误);无任何 `CS####` 编译错误。**编译 0 报错**。

**2. 单元测试 — PASS**
- 隔离跑 `run_tests(EditMode, assembly=BlockBlast.Tests)`:job `b021083010af488b8254f7442426c53e` → **311/311 passed, 0 failed, 0 skipped**(0.39s)。零回归(原 297 + 新增 Redeem)。
- 再隔离跑 Redeem fixture `run_tests(test_names=[...RedeemCodeSystemTests], include_details)`:job `fbea49c400734fed90c1766e01effeda` → **17/17 passed**(C1/C2/C3/V1/V2/V3/D1/D2/D3/S1/S2/S3/S4/S5/G1/G2/G3,逐例 state=Passed,确认全部真实执行非跳过)。
  > dev 交接区写「14 测试」系按验收组数低计;实际 fixture 含 17 个 `[Test]`(C/V/D/G 各 3 + S 5),全部命中验收点且全绿。非缺陷,记录口径差异。
- **C3 Luban 直读实测通过**(非 BLOCKED):`AssetDatabase.LoadAssetAtPath<TextAsset>(redeem_tbredeemcode.bytes / redeem_tbredeemreward.bytes)` → `new TbRedeemCode/TbRedeemReward(ByteBuf)` 行数 >0、WELCOME2026 行 once=1、MULTIGIFT 聚合 3 奖励项均断言通过。导表工具链可达,字节布局正确。

**3. 手动功能验证 — N/A(本轮无需 Play 手验)**
- 本轮纯逻辑层 + 接缝,EditMode 全覆盖(POCO + InitForTest 注入 + InMemory provider/store + state 可 null 纯解析)。UI 输入/结果弹窗(O7)延后轮,不在本轮。设计稿 §六亦明确「UI 视觉不在本轮」。无拖拽/手势/窗口路径需 Play 复现,故第 3 类不适用——非缺口。

**4. Code Review — PASS**
- 逐文件读全部 7 个新增 C# 文件 + 1 改动(SettingsLinks 注释)+ 2 Luban 行类 + Tables.cs 访问器。
- **5 红线**(项目根 CLAUDE.md「核心原则」全条):
  1. 异步优先/禁阻塞 IO:Redeem 目录 grep 无 `.Result`/`.Wait()`/`Thread.Sleep`;去重经既有非阻塞 PlayerPrefs KV(`Persistence.Provider`),不触红线。✓
  2. 模块访问 `GameModule.XXX`:Redeem 层无 `ModuleSystem.GetModule`(去重走 `Persistence.Provider` 接缝、发奖走 `ItemGrant` 静态调用)。✓
  3. 资源必须释放:本层无 `LoadAssetAsync`/`LoadAsset`/`Resources.Load`,无需释放。✓
  4. 热更边界:全部落 `GameScripts/HotFix/GameLogic`(热更区),独立命名空间 `GameLogic.Redeem` + 桥接归 `GameLogic.Config`。✓
  5. 事件解耦:本层无 `GameEvent`/`AddUIEvent`(纯数据逻辑,UI 投放延后)。✓
- **设计稿 R2 重点核**:
  - 无真实网络/HTTP 调用:Redeem 目录 grep `UnityWebRequest`/`HttpClient`/`WebRequest` 唯一命中是 IRedeemValidator.cs:66 的**禁用注释**(非调用);`RemoteRedeemValidator` 仅返 `SourceUnavailable` 不抛、零网络。✓
  - PlayerPrefs 非阻塞:经既有 `Persistence.Provider` KV 接缝。✓
  - 发奖复用 16 不复制落点:`GrantRewards` 调既有 `ItemGrant.GrantOnAcquire(itemDef, num, state, rng)` + `ItemConfigMgr.GetItem`,不自造落点。✓
- **静态 API 签名交叉核验**(grep 实际签名,全部对得上):
  - `ItemGrant.GrantOnAcquire(ItemDef, int, MergeOrderState, System.Random)` ✓ / `ItemConfigMgr.GetItem(int)`、`InitForTest`、`ResetForTest` ✓
  - `MergeOrderState.Exp` / `.Energy` / `.InventoryCount(MergeElement, int)` ✓
  - `IPersistenceProvider.TryGet/Set/Remove` + `Persistence.Provider`(static)+ `InMemoryPersistenceProvider` ✓
  - Luban `GameConfig.RedeemCode{Code,Name,OncePerPlayer,ExpireTime}` / `RedeemReward{AutoId,Code,ItemId,Num}` ✓;`Tables.TbRedeemCode/TbRedeemReward` 访问器 ✓
- **逻辑核**:服务编排顺序与设计稿 §3.5 一致(空输入→SourceUnavailable→NotFound→Expired→AlreadyRedeemed→发奖→MarkRedeemed);MarkRedeemed 在发奖之后(失败不占名额,S5 断言);仅 once=1 去重(D3 断言);Normalize=trim+ToUpperInvariant 与配置 key 同口径(C2/S2 断言);ExpireTime 空/parse 失败按不过期(S4 + IsExpired 实现)。
- **命名/前缀**:`GameLogic.Redeem` 通用系统命名空间(同 19 `GameLogic.Settings` 体例);专用持久化键 `"Redeem.Redeemed"` 不与框架键冲突;无 UI 节点,naming-rules 前缀不适用。✓
- **持久文件交叉检**(conventions.md lint + 抽查 dev 改过的 `pipeline/state/dev.md` 交接区):lint(指代词/diff 叙事/拟人比喻)0 命中;交接区为本任务工作态、可识别所属任务,合规。✓

### 待拍板范围开关(plan 已记,均取安全默认,test 复核无抵触 spec/GDD)

O1 远程 stub / O2 道具 id×数量 / O3 不接背包实例 / O4 NowProvider 默认系统时钟 / O5 全局限量不做 / O6 textId 占位 / O7 UI 延后 / O8 仅 trim+大写规整。八项均与设计稿 §七默认一致,无非裁决不可推进的方向问题。

### 记录项(非缺陷,不影响判定)
- dev 交接区「14 测试」口径低于实际 17 个 `[Test]`(按验收组数计 vs 按方法数计),无功能影响。
- 测试道具夹具经 `ItemConfigMgr.InitForTest` 自控 `UseNum=1` 使产出 Amount==num,便于精确断言;本轮奖励项均自洽闭环,无源 xlsx 漂移风险点(C3 已实测真实 .bytes 字段映射)。
