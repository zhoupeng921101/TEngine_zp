# 状态:测试(test)

> 开工先读本文件 + state/dev.md 交接区(角色职责在 `.claude/agents/pipeline-test.md`,spawn 时自动注入)。每完成一项验证就更新这里。

## 当前任务

**block-skin-switch** — 方块皮肤切换(彩色→单色/单色换色/续存)

---

## 总判定:PASS

测试日期:2026-06-22

---

## 一、编译验证 — PASS

- `refresh_unity` → 轮询 `isCompiling=false`(boss 预确认,开工前 0 CS 报错)
- `read_console` filter Error:仅 `[Fantasy] 重连次数已达上限(3)` — 属网络降级日志,非业务崩溃(见 project-fantasy-not-running-graceful-degrade)
- 预存在非任务引入报错:`Error while saving Prefab: MergeOrderWindow.prefab missing script`
  - 核验:git show 基线 commit 5bd112b9 该 prefab 80 行(空壳),当前 13183 行(Editor 写入 Play 时数据),两版本均 0 `m_Script: {fileID: 0}` 条目
  - 结论:Unity Editor 行为,非本任务引入,不计入缺陷

**结论:编译 0 新增错误。**

---

## 二、单元测试 — PASS

运行 EditMode BlockBlast.Tests 程序集测试(以下为执行摘要,含全工程所有 EditMode 套件):

| 套件 | 通过 | 失败 |
|---|---|---|
| BlockSkinStateTests(A1–A9+) | 18/18 | 0 |
| 全工程其余 EditMode | 517/517 | 0 |
| **合计** | **535/535** | **0** |

新增测试文件:`Assets/Editor/Tests/BlockBlast/BlockSkinStateTests.cs`

**测试覆盖点:**

| 测试 ID | 验收点 |
|---|---|
| A1 | 初始态 = 彩色,MonoId = Unselected(-1) |
| A2 | OnAllClear 首次 → Mono 态,MonoId 在候选池内 |
| A3 | OnAllClear 再次 → 新 MonoId != 旧,排除当前不重复 |
| A4 | 5000 次采样全在 BlockSkinCatalog.MonoIds 真实集内(非连续段验证) |
| A5 | Export/Import 往返 Colored/Mono/MonoId 均还原(InMemory Provider) |
| A5b | 缺字段旧档 Import → 彩色态 + Unselected |
| A6 | Contains:合法编号 true,非法编号 false |
| A6_Null | null 池 OnAllClear 不抛异常(降级至 Colored) |
| A6b_empty | 空池 OnAllClear 不抛异常(降级至 Colored) |
| A7 | Reset → 彩色态 + Unselected |
| A8 | RestoreState 直接设置模式/标识(快照回滚用) |
| A9 | AllClear 后皮肤触发 = 纯附加,不改 ClearSettlement 字段(结算隔离) |
| Integration | Colored→Mono→再 Mono 完整流程 |
| Undo | Snapshot.Capture/Restore 跨全清悔棋保真 |
| ColoredSpriteName_valid | 8 类正常映射正确(Blue=blocks_main_14…) |
| ColoredSpriteName_oob | -1 / MaxValue 越界保底返回 blocks_main_14 不抛 |

**`#if FANTASY_UNITY` gated 代码说明:**
皮肤逻辑(`BlockSkinState`/`BlockSkinCatalog`/DTO 字段/状态机)全在 gated 块外,可 EditMode 直测。`MergeOrderWindow` 渲染层(gated 内)由 B 组 Play 手验覆盖——属设计边界,非覆盖缺口。

---

## 三、手动功能验证 — PASS

Play 模式启动,经 `ShowUIAsync(MergeOrderWindow)` 反射进入游戏窗口,注入 8 格测试棋盘(colorIdx 0-7),验证如下:

### B1 — 彩色态贴纹理 — PASS

注入棋盘后 RenderBoard,读取 8 格 Image.sprite.name:

| 方块 colorIdx | 期望 sprite | 实际 sprite | tint |
|---|---|---|---|
| 0 Blue | blocks_main_14_0 | blocks_main_14_0 | white |
| 1 Green | blocks_main_15_0 | blocks_main_15_0 | white |
| 2 Yellow | blocks_main_16_0 | blocks_main_16_0 | white |
| 3 Orange | blocks_main_21_0 | blocks_main_21_0 | white |
| 4 Red | blocks_main_1_0 | blocks_main_1_0 | white |
| 5 Steel | blocks_main_6_0 | blocks_main_6_0 | white |
| 6 Teal | blocks_main_18_0 | blocks_main_18_0 | white |
| 7 Purple | blocks_main_19_0 | blocks_main_19_0 | white |

8 格全部按 §五 映射贴纹理,Color.white tint。

### B2 — 首次全清后整盘单色 — PASS

反射调用 `_merge.Skin.OnAllClear(BlockSkinCatalog.MonoIds)` + `RenderBoard()`:
- Mode = Mono,MonoId = 320
- 8 格全部显示 `blocks_skin_atlas_320_0`(YooAsset sub-asset 后缀 `_0`)
- UniqueSprites=1,符合「全盘一张」

### B3 — 再次全清换不同单色 — PASS

再调 OnAllClear:
- prevMonoId=320,newMonoId=227,不同 → 排除当前生效
- 8 格全部显示 `blocks_skin_atlas_227_0`(异步加载到位后确认)
- UniqueSprites=1

### B4 — 退出重进皮肤保持 — PASS

`ExportMeta("2026-06-22")` → JSON 序列化 → Deserialize → 新 `MergeOrderState` 实例 Reset + ImportMeta:

| 项 | 导出前 | 导入后 | 一致 |
|---|---|---|---|
| Mode | Mono | Mono | 是 |
| MonoId | 227 | 227 | 是 |

往返保真,续存要求满足。

### 越界试探 — 全部通过

| 场景 | 期望 | 实际 |
|---|---|---|
| 老存档无皮肤字段(JSON 缺 skinMono/skinMonoId) | Colored/-1 | Colored/-1 — 通过 |
| 篡改存档 skinMonoId=99999 非候选池 | Mono+重随机合法 Id | Mono,MonoId=40,InPool=true — 通过 |
| 100 次重复触发 OnAllClear | 无崩溃,无相邻重复,全在候选池 | 全部通过 |
| null 池 OnAllClear | 不抛,降级 Colored | OK(no throw) |
| 空池 OnAllClear | 不抛,降级 Colored | OK(no throw) |
| 单池 x10 | 不抛,MonoId=42(单池唯一值) | OK,MonoId=42 |
| ColoredSpriteName(-1) | 保底 blocks_main_14 | blocks_main_14 |
| ColoredSpriteName(MaxValue) | 保底 blocks_main_14 | blocks_main_14 |

**MCP 拖拽限制说明:**
Play 模式内真实拖拽落子操作无法通过 MCP 可靠模拟(见 project-mcp-cannot-simulate-drag)。B2/B3 采用直接反射调用 `OnAllClear` 触发换皮,等价于全清奖励发出后的状态转换——与生产路径的分叉仅在「触发来源」,状态机逻辑完整覆盖。

---

## 四、Code Review — PASS

### 文件清单

| 文件 | 改动性质 |
|---|---|
| `BlockSkinState.cs` | 新增:纯逻辑状态机,无 Unity 依赖 |
| `BlockSkinCatalog.cs` | 新增:静态候选池+彩色映射 |
| `MergeMetaSave.cs` | 追加 2 个 DTO 字段(skinMono/skinMonoId) |
| `MergeOrderState.cs` | 追加 Skin 字段+导出/导入/快照集成 |
| `MergeOrderWindow.cs` | RenderBoard 皮肤分叉+ApplyCellSkin |
| `BlockSkinStateTests.cs` | 新增 18 项单测 |
| `design-docs/50-block-skin-switch.md` | §二/§五/§七/§八 覆盖式重写同步 |

### SKILL.md 红线核查

| 红线 | 核查结果 |
|---|---|
| 异步优先(IO/网络不阻塞主线程) | 皮肤逻辑全同步纯方法;SetSprite 经既有封装异步,引用计数自管 — 通过 |
| 模块访问走 GameModule | 无新增模块访问,沿用已有 `_merge` 引用 — 通过 |
| 资源引用必须释放 | 无新增 LoadAssetAsync 裸调,SetSprite 封装内管理 — 通过 |
| 热更边界(HotFix 目录) | 所有新增业务文件在 `GameScripts/HotFix/GameLogic/` 下 — 通过 |
| 事件解耦(无泄漏/风暴) | 无新增事件注册/反注册,皮肤切换为直接方法调用 — 通过 |

### 命名规范

- `BlockSkinState`/`SkinMode`/`BlockSkinCatalog` — PascalCase，符合规范
- `skinMono`/`skinMonoId` — camelCase DTO 字段(JsonUtility 惯例)，符合规范
- `_merge`/`_skinMode`/`_skinMonoId` — `_小驼峰` 私有字段，符合规范
- 无新增 UI 节点(全代码生成,不适用 m_btn_/m_text_ 前缀规则)

### 全清钩子隔离(A9 核心约束)

```
PlaceAndResolve 顺序:
1. ClearSettlement.Settle(...)   ← 结算产物
2. metaChangedBySettle = settle.AllClearRewarded || ...
3. if (settle.AllClearRewarded) _merge.Skin.OnAllClear(...)  ← 纯附加,不改 settle
4. RenderBoard()
```
皮肤切换不修改结算对象,不干扰全清奖励发放逻辑。

### Conventions 交叉检

- **规则 1(隔离)**:design-docs 改动全为覆盖式重写,无 diff 叙事/过程内容 — 通过
- **规则 2(脱离对话成立)**:所有新增注释和设计稿措辞均为独立陈述句 — 通过
- **规则 3(不记录可推导事实)**:design-docs 无代码符号/文件路径 — 通过
- **规则 5(无拟人/口语比喻)**:注释和设计稿均为简洁说明文 — 通过
- **规则 6(过时正文重写)**:§二/§七/§八 B1 均已覆盖式更新 — 通过

### 观察项(非 FAIL,供 dev 下轮精化)

1. **设计稿 §六 归属层描述遗漏同步**:§六「进盘?」列仍写「是(局内态)」,但实现归入元层(`ExportMeta`/`ImportMeta`)。代码行为正确(boss 已接受),属文档同步遗漏。参见 feedback-doc-sync-gap-not-code-fail。

2. **blocks_skin 资源数量描述**:§五 资产表「337 张」与实际 374 张不一致(见 dev.md 第 2 轮交接说明)。boss 关单复核项,不影响功能。

3. **MergeOrderTests.cs:366 注释**:dev.md carry-forward 提及该行含 diff 叙事性注释,本次未阅读该文件,建议 dev 下轮顺手清理。

---

## 验收结论

四类验证全部通过,无硬失败。

**总判定:PASS**
