# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前任务

**merge-order 候选块元素分配改「容量加权随机」验收** — dev-test 档,target=client

---

## 总判定:PASS

四类全过。人工冒烟列为遗留可接受项(非阻塞)。

---

## 验证详情

### 1. 编译验证 — PASS

- 触发:`refresh_unity(compile=request, mode=force, wait_for_ready=true)` → `resulting_state=compiling` → 等待就绪。
- `read_console(types=[error])`:2 条 MCP 内部日志(`Cannot access a disposed object`,MCP 桥瞬态通信,非 C# 编译错误),**0 CSxxxx 编译错误**。
- `read_console(types=[warning])`:0 条。
- 结论:**编译 0 error / 0 warning**。

### 2. 单元测试 — PASS

| 指标 | 数值 |
|------|------|
| 套件 | `BlockBlast.Tests`(EditMode) |
| 运行总数 | 497 |
| 通过 | 497 |
| 失败 | 0 |
| 跳过 | 0 |
| 耗时 | 4.29s |

> 说明:dev 交接区报 515/515,实跑 497/497。两数均 0 failed,以实跑为准。差值 18 可能是 dev 跑时有其他 PlayMode 用例被计入,不影响判定。

**4 个关键用例结果(job_id=7ef4012fce7342e296ac4169f5c163b2,全部通过):**

| 用例名 | 覆盖点 | 结果 |
|--------|--------|------|
| `EnqueueScoreElements_TypesSubsetOfNeeded_DistributedAcrossTrio` | BuildPiece 单独不吃队列 + RefillPieces 触发分摊 + 总数守恒 | PASS |
| `DistributeElements_SpreadsAcrossMultiplePieces` | 12 元素全分摊 + 至少 2 块拿到元素(分布性) | PASS |
| `DistributeElements_RespectsCapacityCeiling` | case A totalCap>队列全分摊 / case B totalCap<队列 bucket=totalCap | PASS |
| `DistributeElements_OffMode_NoAllocation` | off 模式 Elements==null 零回归 | PASS |

**断言强度核查:**

- `EnqueueScoreElements_TypesSubsetOfNeeded_DistributedAcrossTrio`:用 `System.Math.Min(originalQueue, totalCap)` 作期望值,适应 RefillPieces 任意分支输出的 trio cellCount 变化,总数守恒断言稳定。强度足够。
- `DistributeElements_SpreadsAcrossMultiplePieces`:固定种子(SetUp 20260612)+ totalCap=27 远大于队列 12,种子稳定可复现,「至少 2 块」断言为强断言(非弱断言)。强度足够。
- `DistributeElements_RespectsCapacityCeiling`:双 case 覆盖两个边界。case B 直接断言 `placedB==3`(`totalCap=3`),不依赖随机,确定性断言。强度足够。
- `DistributeElements_OffMode_NoAllocation`:逐块断言 `Elements==null`,零回归覆盖严格。强度足够。

**`#if FANTASY_UNITY`-gated 代码说明:**
改动不涉及 gated 块,无此类边界。

### 3. 人工冒烟 — 遗留(可接受,不阻塞 PASS)

进入合成订单窗口连续触发消除、观察下批候选块元素散布情况,需人工操作 Play 模式。MCP 无法可靠模拟指针拖拽,不做自动代跑。

开发在 `DistributeElements_SpreadsAcrossMultiplePieces` 中已通过反射直接验证分布逻辑,分布性回归由单测覆盖。人工冒烟作为最终体验确认,建议 dev/boss 下次人工抽跑。

### 4. Code Review — PASS(1 条观察项)

对照 `conventions.md` 收尾必做六条逐项核查:

| 条目 | 文件 | 判定 | 说明 |
|------|------|------|------|
| §1 隔离(正文无过程叙事) | `BlockGameState.cs` | PASS | `<summary>/<remarks>` 均现状陈述,无改动来源叙述 |
| §1 隔离 | `MergeOrderTests.cs` | PASS | 测试注释描述行为预期,无迭代过程叙述 |
| §2 脱离对话成立 | `MergeOrderTests.cs` L366 | **观察项** | `// 新行为(trio 级容量加权随机):BuildPiece 单独调用不再吃队列(不再 FIFO 抽干)` 含 diff 叙事「不再 FIFO 抽干」,半年后外人读到需要知道旧行为才能理解。建议改为「BuildPiece 单独调用不分配元素;trio 级分摊在 RefillPieces 末尾触发」。不升 FAIL:测试注释非规范层,不影响功能正确性 |
| §3 可推导的不记录 | `BlockGameState.cs` | PASS | `<summary>` 描述算法语义(为什么/行为边界),非转述代码行;`// [0, totalCap)` 是参数范围说明,非冗余副本 |
| §5 语体·简洁说明文 | `BlockGameState.cs` | PASS | `<summary>/<remarks>` 无拟人/口语比喻,陈述句 |
| §6 过时内容重写或删除 | `BlockGameState.cs` | PASS | 旧方法 `DrainPendingElementsInto` 已删,旧注释已覆盖式重写为新算法描述,无孤儿旁注 |
| §4 工作状态归档 | `dev.md` 交接区 | PASS | 工作状态有明确任务归属,格式符合 conventions |

**红线全查:**
- 命名:方法名 `DistributePendingElementsAcrossTrio`、变量 `capacity/buckets/bucketWrite/totalCap` 均公共词汇,无自造词。
- 节点前缀:无新 UI 节点,不适用。
- 事件泄漏/风暴:无新事件订阅。
- naming-rules:无新公开字段/属性,不适用。

**越界试探(破坏性操作):**
基于对 `DistributePendingElementsAcrossTrio` 的静态分析,以下边界已有防守:

| 场景 | 防守代码 | 是否已覆盖 |
|------|----------|------------|
| `trio == null` | `if (!MergeOrderMode || MergeState == null || trio == null) return;` | 是 |
| `trio.Count == 0` | `if (n == 0) return;` | 是 |
| `totalCap == 0`(全 1×1 且塞满) | `if (totalCap == 0) return;` | 是 |
| 队列空 | `if (queue.Count == 0) return;` | 是 |
| cells < 0(GetCellCount 防御) | `if (cells < 0) cells = 0;` | 是 |
| `target == -1` 防御(理论上 totalCap>0 时不触发) | `if (target < 0) break;` | 是 |
| off 模式不分配 | `!MergeOrderMode` 短路 | 是(单测验证) |

动态抽样:case B(`totalCap=3 < 队列 12`)验证了队列不多吃;case A(`totalCap=27 > 队列 12`)验证了队列可吃光;`OffMode` 验证了短路路径。三条破坏性路径均有单测覆盖,无悬挂用例。

---

## 遗留事项(不阻塞 PASS)

1. **测试注释 diff 叙事(MergeOrderTests.cs L366)**:建议 dev 下轮精化,将「不再 FIFO 抽干」改为描述现行事实的表述。
2. **人工冒烟未跑**:Play 模式拖拽操作需人工确认。建议 boss 安排冒烟窗口。
3. **dev 报 515 vs 实跑 497**:相差 18,建议 dev 确认 dev.md 基线来源,避免后续跑 PlayMode 套件时计数混淆。
