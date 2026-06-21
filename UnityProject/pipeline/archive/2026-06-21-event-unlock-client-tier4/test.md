# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前任务

Tier 4 活动系统 · 客户端段 · 第 2 子单(EVENT 头像解锁通路 · client 段)

## 总判定:PASS

四类验证全部通过。E1 全栈真往返完整跑通(mongod 27017 + Fantasy Main.exe 均在线)。

---

## 一、编译验证

**结论:PASS**

- `refresh_unity` 触发编译刷新后，`read_console types=[error,warning]` 仅返 1 条 MCP 内部通信错误
  (`Client handler error: Cannot access a disposed object`)，属连接握手期瞬态，非业务编译报错。
- 业务代码编译 0 错 0 警告。

---

## 二、单元测试

**结论:PASS**

- `run_tests assembly_names=["BlockBlast.Tests"] mode=EditMode`
- 结果:`total=444 passed=444 failed=0 skipped=0 durationSeconds=4.06`
- 含本子单新增 9 个测试:
  - `CV2_Resolve_EventUnlock_ProducesPayload`
  - `CV3_ResolveAndApply_EventUnlock_GrantsAvatarIdempotent`
  - `CV4_EventUnlock_EntryNotFound_SilentNoThrow`
  - `CV5_EventUnlock_PlayerNull_SilentNoThrow`
  - `CV6Aux_Resolve_ExistingCases_Unchanged`
  - `ContainsEventUnlock_NullOrEmpty_ReturnsFalse`
  - `ContainsEventUnlock_MixedList_DetectsEvent`
  - `ContainsEventUnlock_NoEvent_ReturnsFalse`
- 既有 435 个零回归。

---

## 三、手动功能验证

**结论:PASS**

### CV7 主菜单零回归

- Play 模式启动约 3-31s 后主菜单出现(`MainMenuWindow=True`)，GameContext.IsValid=True，Player 正常加载。
- `read_console types=[error,warning]` 仅 MCP 内部通信条目，无业务报错。

### CV1 配置层 xlsx 核对

| 字段 | itemdef.xlsx id=30101 实测值 | 设计 §3.2 要求 | 结论 |
| --- | --- | --- | --- |
| use_effect | 5 | 5 | PASS |
| use_value | 3 | 3(avatar id=3 avt_star) | PASS |
| use_num | 1 | 1 | PASS |
| automatic | 1 | 1(立即结算) | PASS |
| type | MATERIAL | MATERIAL | PASS |
| quality | 4 | 4(EPIC) | PASS |
| stacking | 0 | 0 | PASS |

| 字段 | giftrandom.xlsx index=6101 实测值 | 设计 §3.3 / server 共识 | 结论 |
| --- | --- | --- | --- |
| auto_id | 11 | 11(新行主键) | PASS |
| index | 6101 | 6101 | PASS |
| item_id | 30101 | 30101 | PASS |
| num | 1 | 1 | PASS |
| rate | 100 | 100(单项必中) | PASS |

### E1 全栈真往返

**环境**: mongod 27017 LISTENING + Fantasy Main.exe PID 43136 RUNNING

**链路验证:**

1. 账号 `dev_9252830aef3f202988ae4b184f25a4dc13d2af0c` 累计登录 7 次。
2. 服务端日志确认 activity_id=2 发奖:
   ```
   ActivityEvalHelper: account=dev_9252830a... activity_id=2 本周期发奖完成,
   mailId=d258f1711bc2c43a9ac5ada33c8afcedc,reward=6101,periodKey=1
   ```
3. 客户端 `PullInboxAsync` 返 9 封邮件，含 EVENT 活动邮件(mailId=`d258f171...` title=390003)。
4. 客户端 `ClaimAsync` 领取该邮件，`ApplyEventUnlock` 调 `AvatarConfigMgr.GetAvatar(3)` 得 avt_star entry → `AvatarUnlockService.GrantUnlock` → `UnlockedAvatarIds=[1,3]`。
5. `ContainsEventUnlock(granted)=True` → `GameContext.Instance.SavePlayer()` 触发落盘。
6. 重启 Play 模式后 `UnlockedAvatarIds=[1,3]`，`IsUnlocked(avt_star)=True`，**跨会话持久 PASS**。

### 越界试探

全部覆盖，无崩溃:

| 场景 | 操作 | 实际表现 | 结论 |
| --- | --- | --- | --- |
| 无效 avatar id=0 | `GrantOnAcquire(use_value=0)` PlayMode | 不抛，produced.Count=1(EVENT 结构)，无 GrantUnlock 调用 | PASS |
| 重复触发幂等 | 同一 player 两次 `GrantOnAcquire(use_value=3)` | AFTER_1ST=[1,3] AFTER_2ND=[1,3]，长度不变 | PASS |
| ContainsEventUnlock 非 EVENT 道具 | use_effect=1(货币)/use_effect=0(纯持有) | ContainsEventUnlock=False，不误触 SavePlayer | PASS |
| SavePlayer 整链 PlayMode 执行 | ContainsEventUnlock=True 后调 SavePlayer | CALLED_OK，无异常 | PASS |
| MailboxService 构造器 null persist | 传 null persist | 抛参数守卫异常(正确防御行为，非 FAIL) | PASS |
| 跨会话持久 | Stop→Play 后读 UnlockedAvatarIds | [1,3] Contains(3)=True | PASS |

---

## 四、Code Review

**结论:PASS，附 1 条文档同步遗漏记录(非代码缺陷)**

### 4.1 核心红线逐条

| 红线 | 核对 | 结论 |
| --- | --- | --- |
| 异步优先(UniTask) | ItemGrant.cs 全同步纯逻辑；三处领奖路径调用方使用 UniTask，已有 | PASS |
| 模块访问通过 GameModule | 本子单不新建 GameModule，复用 GameContext.Instance | PASS |
| 资源必须释放(LoadAssetAsync/UnloadAsset) | 本子单无资源加载，无泄漏风险 | PASS |
| 热更边界(HotFix 内) | ItemGrant.cs / MailboxService.cs / RemoteMailService.cs / RedeemService.cs / EventUnlockClientTests.cs 均在 `Assets/GameScripts/HotFix/` 下 | PASS |
| 事件解耦(GameEvent 跨模块) | 本子单无新增事件监听，无泄漏/风暴风险 | PASS |

### 4.2 命名 / 节点规范

- `GrantKind.EventUnlock`：大驼峰枚举档，合规。
- `ContainsEventUnlock`：公共静态方法，大驼峰，合规。
- `ApplyEventUnlock`：私有静态方法，大驼峰，合规。
- 无 UI 节点新增，命名规则不适用。

### 4.3 ItemGrant.cs 实现审查

- `GrantKind` 枚举新增 `EventUnlock=5`，与 `Resolve switch case 5` 对齐，无缺口。
- `Resolve` case 5：`new GrantPayload(EventUnlock, def.UseValue, count, 0, 0)`，与设计 §3.4 产出结构一致。
- `ResolveAndApply` case `EventUnlock`：调 `ApplyEventUnlock(payload)` + `produced.Add(payload)`，任何情形不抛，沿设计 §3.5 行为定义。
- `ApplyEventUnlock`：`GameContext.IsValid` 二段防护，`GetAvatar` 查无 null 静默，`GrantUnlock` 幂等——三道防线齐全。
- `ContainsEventUnlock`：O(N) 线性扫描，邮件附件列表上限 ~10 项，无性能问题。
- 既有 5 档(None/Numeric/Pattern/GiftSelect/GiftRandom)代码路径零改动（CV6Aux 单测验证）。

### 4.4 三处领奖路径调用点审查(CV8 D3)

grep 结果：ContainsEventUnlock 调用点共 4 处（3 处领奖路径 + ItemGrant.cs 定义处）：

```
MailboxService.cs:149   if (ItemGrant.ContainsEventUnlock(granted) && GameLogic.GameContext.IsValid)
MailboxService.cs:176   if (ItemGrant.ContainsEventUnlock(all) && GameLogic.GameContext.IsValid)   [ClaimAll]
RemoteMailService.cs:91 if (ItemGrant.ContainsEventUnlock(granted) && GameContext.IsValid)
RedeemService.cs:105    if (ItemGrant.ContainsEventUnlock(granted) && GameContext.IsValid)
```

- MailboxService 覆盖 `Claim`（单封）+ `ClaimAll`（一键领取）两条路径，符合 dev 交接区自治审计说明。
- 条件：`ContainsEventUnlock && IsValid` 双重守卫，无误触风险。
- SavePlayer 在成功分支、GrantOnAcquire 之后执行，调用顺序正确。

### 4.5 conventions §6 交叉检(dev 改过的持久文件)

**design-docs/41-event-unlock-client.md (本子单设计稿)**

- WARNING 块五条边界仍成立，无过时内容。
- 正文无过程性内容、无可推导副本、无拟人比喻。

**design-docs/16-item-system.md (`useeffect-event` 旁注节)**

- `useeffect-event` 旁注节已由 dev 更新，「客户端段由 41 兑现」描述正确，无残留「下一刀实做」表述。

**design-docs/40-event-unlock-relay.md**

- §8.3 第 277 行：「头像真正解锁需客户端段下一刀实做」——此句已过时（41 已兑现），dev 未同步更新。
- §8.3 WARNING 第 289 行：「客户端 UseEffect 解析 + 适配器接线(O5)→ 客户端段下一刀」——已过时，dev 未同步。
- 风险表 301 行：「客户端段下一刀验收点须含」——已过时，dev 未同步。
- **注**：§8.3 E3 验收点（第 283 行）已正确更新为「由 41 兑现」，server 段主验收内容未受影响。

**design-docs/39-activity-server.md**

- §七 O5（第 285 行）：「头像 EVENT 解锁通路(O5)→ Tier 4 第 2 子单」——41 兑现后此描述过时，设计稿 41 §六关系表要求同步改为「由 40 (server) + 41 (client) 联合兑现」，dev 未同步。

**pipeline/state/dev.md**

- 内容清晰，决策表、文件清单、验证点均为事实陈述，无过程性内容，无可推导副本。

### 4.6 Fantasy 仓零 diff 核对

- `D:/work/TEngine_block/Fantasy/` 目录下本子单代码无改动（沿不变量）。
- Git status 确认仅客户端文件有改动。

---

## 可复现清单(FAIL 时填写)

本次总判定 PASS，无可复现清单。

---

## 文档同步遗漏记录(不升 FAIL，记录供 dev 后续跟进)

| 文件 | 行号 | 过时表述 | 应更新为 |
| --- | --- | --- | --- |
| design-docs/40-event-unlock-relay.md | L277 | 「头像真正解锁需客户端段下一刀实做」 | 「头像真正解锁已由设计 41 兑现」 |
| design-docs/40-event-unlock-relay.md | WARNING L289 | 「客户端 UseEffect 解析 + 适配器接线(O5)→ 客户端段下一刀」 | 删除或改为「已由 41 兑现」 |
| design-docs/40-event-unlock-relay.md | 风险表 L301 | 「客户端段下一刀验收点须含」 | 删除或改为「已由 41 CV1-CV8 验收」 |
| design-docs/39-activity-server.md | §七 O5 L285 | 「头像 EVENT 解锁通路(O5)→ Tier 4 第 2 子单」 | 「由 40 (server) + 41 (client) 联合兑现」 |

> 以上均为设计稿内部状态标注过时，属 conventions §6「过时正文已重写或删除」范围。代码行为正确，不升判为 FAIL。
