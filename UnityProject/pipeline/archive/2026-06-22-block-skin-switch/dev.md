# 状态：开发(dev)

> 开工先读本文件 + state/plan.md 交接区（角色职责在 `.claude/agents/pipeline-dev.md`，spawn 时自动注入）。每完成一项就更新这里。

## 当前任务（client，baton=full，本环节=dev）

`block-skin-switch` 方块皮肤切换（全清触发单色换皮，设计 50）。基线 `5bd112b9`。

### 接缝定位结论（dev 现场推导，3 处关键发现）

1. **渲染接缝**：活动主玩法窗 = `MergeOrderWindow`（`GameWindow` 已退役，无活入口，设计 29 §4.2）。棋盘格当前渲染 = `MergeOrderWindow.RenderBoard` 用 `BlockLayout.ColorOf((BlockColor)colorIdx)` 贴**纯色 Image**——`Atlas_blocks_blocks_main` / `blocks_main_<n>` sprite **并未接入棋盘格渲染**（窗内 `SetSubSprite` 调用都是棋盘外框 / HUD 木纹皮，非方块）。故彩色态 = 保持纯色现状（B1 与引入前一致）；单色态在此分叉贴 sprite。
2. **全清触发点**：`MergeOrderWindow.PlaceAndResolve` 内 `ClearSettlement.Settle(...)` 返回 `settle.AllClearRewarded`（= 已武装全清触发，与女神 / 图案奖励同口径）。皮肤切换挂此（= plan 默认口径 / 设计 50 §九 待拍 #1 默认），纯附加。
3. **续存通道**：见下「关键决策 · 续存归元层」——这是与 plan「归局内态」分歧的实现层决定，需 boss 复核。

### 改动摘要

- **新增纯逻辑皮肤状态机** `BlockSkinState`（设计 50 §三/§四/§六）：皮肤模式（彩色/单色）+ 当前单色标识；`OnAllClear(pool)` 推进状态机（首次彩→单全池随机 / 后续单→单排除当前随机 / 单色不退彩色 / 池空或仅 1 张退化兜底保持不变）；`Export/Import(dto, pool)` 续存 + 加载校验保底（缺字段→彩色；单色态非法标识→默认重随机，池空→退彩色）。用 `RandomSource`（可注入种子，单测确定性）。
- **新增候选池** `BlockSkinCatalog.MonoIds`：从 `blocks_skin/` 目录**实际存在的 blocks_skin_atlas_<n>.png 文件名**导出的真实编号集（374 个，非连续，含留空段），满足设计 50 §四硬约束「候选池=真实存在集，非 1..N 区间」。`Contains(id)` O(1) 校验，`SpriteName(id)` 给运行期 location。
- **DTO 加 2 字段** `MergeMetaSave.skinMono` / `skinMonoId`（平铺，做法同既有玩家字段；`CurrentVersion` 不升）。
- **状态机串联**：`MergeOrderState` 加 `Skin` 字段；`Reset()` 缺省彩色、`ExportMeta` 调 `Skin.Export`、`ImportMeta` 调 `Skin.Import(dto, BlockSkinCatalog.MonoIds)`。续存随既有元层落盘 / 加载链路（`ResetForMergeOrder`→`Load`→`ImportMeta`；`FlushSaveIfDirty`→`ExportMeta`→`SaveAsync`），不新增存储栈。
- **悔棋一致性**：`Skin.Mode`/`MonoId` 入 `MergeOrderState.Snapshot.Capture/Restore`（新增 `RestoreState`）——同一手全清的皮肤切换须与女神/盲盒等其余全清产物一并被悔棋回滚，否则悔棋后皮肤与其余全清效果不一致（既有 `_allClearArmed`/`_goddessRating`/`_blindBoxCount` 已在快照内，本字段随同口径）。
- **全清钩子**：`PlaceAndResolve` 在 `settle.AllClearRewarded` 为真时调 `_merge.Skin.OnAllClear(BlockSkinCatalog.MonoIds)`，纯附加在结算之后，不改结算（A9）；落盘由既有 `metaChangedBySettle`（AllClearRewarded 蕴含为 true）触发，无需额外标脏。
- **渲染分叉**：`RenderBoard` + 新 `ApplyCellSkin`——单色态全盘统一 `img.SetSprite("blocks_skin_atlas_<MonoId>")`（散 PNG 按文件名寻址）+ 白 tint；彩色态清 sprite + 纯色（现状）。

### 文件清单

代码（HotFix/GameLogic）：
- **新增** `Module/BlockBlast/BlockSkinState.cs` — 皮肤状态机（纯逻辑，无 Unity 依赖）。
- **新增** `Module/BlockBlast/BlockSkinCatalog.cs` — 候选池（真实文件集 374 编号）+ 校验/命名。
- `Module/BlockBlast/MergeMetaSave.cs` — 加 `skinMono`/`skinMonoId` 两字段 + 旁注。
- `Module/BlockBlast/MergeOrderState.cs` — 加 `Skin` 字段；Reset/ExportMeta/ImportMeta 串联；Snapshot 加皮肤态 Capture/Restore。
- `UI/BlockBlastUI/MergeOrderWindow.cs` — 全清钩子（PlaceAndResolve）+ 渲染分叉（RenderBoard/ApplyCellSkin）。

测试（Assets/Editor/Tests/BlockBlast）：
- **新增** `BlockSkinStateTests.cs` — A1–A9 + 续存整合 + 悔棋回滚 + 空池/单池边界，全部纯逻辑 + 确定性 RandomSource。

### 关键决策

1. **续存归元层（与 plan「归局内态」分歧，需 boss 复核，但有安全默认 + 行为验收满足）**。
   - **现状事实**：工程当前**只有元层（`MergeMetaSave`/`MergeMetaPersistence` PlayerPrefs `block_blast_merge_meta_v1`）这一条跨会话续存通道**。局内态（棋盘/手牌/全清武装位/连消链）**每次会话由 `ResetForMergeOrder`→`Reset()` 重建、不落盘**。plan 决策 #5「皮肤态归局内态、同全清武装位通道」依据的是设计 49（无局·无尽，整盘续存）——但设计 49 是**已暂停未进 dev 的任务**（boss.md），其「局内态整盘续存」通道**在现行代码里不存在**。
   - **决定**：皮肤态并入**元层 DTO**（同 `highScore`/`goddessLevel` 等只增字段）。理由：① 皮肤态语义 = 「已达成全清」的**单调只增成就标记**（设计 50 §三 规则 4 单色不退彩色 + §三 TIP），与元层只增字段同类，**不同于**每局重建的棋盘态；② 元层是唯一实际跨会话持久的通道，满足用户硬要求「退出重进保持」（A5/B4）；③ 行为验收（A5 往返保真、B4 跨会话保持）与归属层无关，均满足。
   - **可逆 + 三段阶梯①**：取此安全默认（不抵触设计 50 行为验收 / 用户拍板，可逆）。若 boss 复核坚持等设计 49 局内态通道落地后迁移，另开增量即可（DTO 字段可平移）。**不报 designFlaw**（设计 50 行为级验收无误，仅 plan 的归属层标注基于未落地的 49）。

2. **悔棋回滚皮肤切换**（见改动摘要）：与全清 reward 系列同口径，保一致性。这是实现层正确性决定，无设计冲突。

3. **候选池从真实文件导出而非 README**：README 写「337 张 / 段 51-97 / 226-477」，但 `blocks_skin/` 实际有 **374** 个 png，且含 README 标「留空」的 99-114 / 116-136 段。设计 50 §四硬约束「真实存在集」——故 `BlockSkinCatalog` 从实际文件名导出（已脚本核验逐一相等 374）。**README 与实际资源不符是既有事实，非本任务引入**；本任务以真实文件为准（符合设计硬约束），README 偏差留 boss 判是否单开修文档。

4. **单色 sprite 运行期寻址走散 PNG 按文件名，不走 SpriteAtlas**：`Atlas_blocks_blocks_skin` 是 SpriteAtlas v2，记忆 `project-setsubsprite-not-spriteatlas-v2` 实测 SetSubSprite 对 v2 不工作（SubAssets count=0）。散 PNG 在 UIRaw 收集器组（`Assets/AssetRaw/UIRaw/Atlas` 路径下），按文件名 `blocks_skin_atlas_<n>` 寻址、`SetSprite(location)` 加载。**此为 B 组运行期渲染路径，dev 无 Play 环境实证**，留 test 手验（见下验证点）。

### 美术管线核验结论（任务项 5）

- 彩色源 `default_skin/`：8 张 `blocks_main_<n>`（1/6/14/15/16/18/19/21）✓ 与设计一致。**注**：`Atlas_blocks_blocks_main` 图集 packables 只列 6 个 sprite guid（非 8、非整文件夹）——但**本实现彩色态用纯色不用这些 sprite**，故不影响本任务；图集 6 vs 8 偏差留 boss/美术判（非本任务阻塞）。
- 单色源 `blocks_skin/`：374 张 `blocks_skin_atlas_<n>` ✓ 已提交，候选池非空（374）。
- **未阻塞**：A 组逻辑/续存/候选池全不依赖图集打包（候选池从源文件导出、渲染走散 PNG 寻址）。图集打包是否生效仅影响「若改走 SpriteAtlas 渲染」的备选路径，本实现不用。

### 编译自检状态

- **本环境 Unity MCP 不可达**（`read_console` 返回 No Unity Editor instances；编辑器实例未连桥）。按记忆先例 + 简报指引：**编辑器内编译 + EditMode 单测实跑留给 test 在其 MCP 环境执行**，dev 做静态 grep 自检。
- **静态自检通过**：① 全部新增/引用符号均存在（`BlockSkinState`/`SkinMode`/`BlockSkinCatalog.MonoIds`/`Contains`/`SpriteName`/`RandomSource.Index`/`MergeMetaSave.skinMono`/`SimpleSingleton.IsValid/Release`/`InMemoryPersistenceProvider`/`BinaryBoard`/`MergeOrderState.Undo/CaptureSnapshot` 逐一核对存在）；② 候选池 374 编号经脚本核验与 `blocks_skin/*.png` 实际文件集逐一相等；③ 无悬空引用、无重复类定义；④ test asmdef 已引 `GameLogic`（新类可见）。

### 给 test 的可复现验证点

1. **编译 0 报错**：`read_console` 确认 `CSxxxx`=0、`editor_state.isCompiling=false`（BlockBlast.Tests EditMode 能跑起 = 最强编译自检）。
2. **新增单测 `BlockSkinStateTests` 全绿**（`run_tests` EditMode，先确保非 Play 模式）。逐条对设计 50 §八 A 组：
   - `A1_Initial_IsColored_Unselected`（A1 初始彩色）
   - `A2_FirstAllClear_TurnsMono_FromPool`（A2 首次转单色 ∈ 池）
   - `A3_SubsequentAllClear_ChangesAndNeverAdjacentRepeat`（A3 后续换色 + 200 次连续无相邻重复）
   - `A4_RealCatalog_SamplesAllInExistingSet_NoGaps`（A4 真实池 5000 次采样全 ∈ 真实集 + 留空编号 26/48/98/115/200… 不在池 + 池规模=374）
   - `A5_Persistence_RoundTrip_Mono_Fidelity` / `A5b_..._Colored_Fidelity`（A5 续存往返保真）
   - `A6a_MissingFields_FallbackToColored` / `A6b_IllegalMonoId_FallbackToMonoReroll` / `A6b_IllegalMonoId_EmptyPool_FallbackToColored` / `A6_NullDto_KeepsCurrent`（A6 缺字段 + 非法值保底）
   - `A7_OnceMono_NeverRevertsToColored`（A7 单色不退彩色）
   - `A8_SingleCandidate_KeepsCurrent_NoException` / `A8b_EmptyPool_NoChange_NoException`（A8 退化兜底）
   - `A9_SkinSwitch_DoesNotTouchSettlementFields`（A9 皮肤切换不污染全清结算字段）
   - `Integration_MergeOrderState_PersistsSkinViaMeta`（经 ExportMeta/ImportMeta 续存整合）
   - `Undo_RollsBackSkinSwitch_WithSnapshot`（悔棋回滚皮肤切换）
3. **既有回归仍绿**：`MergeMetaSaveTests`（加 2 DTO 字段后 A1 等不受影响，新字段默认彩色不破往返）、`MergeOrderTests`（悔棋/重置链路加皮肤字段后仍绿）。重点核 `MergeMetaSaveTests.A1_RoundTrip_AllMetaFields_Equal_SameDay` 仍绿。
4. **B 组运行手验（Play / 真机，需拖拽触发全清，环境不可达判 BLOCKED 不判 FAIL）**：
   - B1 全新进游戏彩色（纯色，与引入前一致）。
   - B2 一次全清后整盘**统一一张单色 sprite**（不分类型同图）。**重点核运行期单色 sprite 寻址确实加载成功**（`SetSprite("blocks_skin_atlas_<n>")` 散 PNG 路径，见关键决策 #4；若加载失败会贴空图/白块——这是 dev 无 Play 环境未实证的最大风险点，请 test 重点看）。
   - B3 再次全清单色图肉眼可见不同。
   - B4 单色态退出重进保持（续存）。
5. **过异常路径自检（已加防护，交 test 复核）**：候选池空/null（A8b 不抛保持）、单池排除当前后空（A8 保持不变）、非法标识 + 池空（A6b-empty 退彩色不留空标识）、null DTO（A6 保持现状）、悔棋回滚（快照含皮肤态）。崩点：单色态运行期 sprite 加载失败（B 组手验暴露，逻辑层无法覆盖）。

### 自检（conventions 收尾）

- [x] 过程性内容不在代码正文（注释只写「是什么/为什么」，无 diff 叙事/「从 X 改成 Y」）。
- [x] 候选池数据可推导留痕：`BlockSkinCatalog` 注释写明导出来源 + 重导命令，不在别处复制编号集。
- [x] 无拟人/口语比喻。
- [x] 本交接区工作态可识别所属任务（block-skin-switch），覆盖了上一已归档任务残留。
- [x] 设计稿未改（设计 50 行为验收无误，无需 dev 改文档）；归属层分歧在本交接区 + 返回值标明给 boss，不擅改设计稿。

---

## 追加实现：彩色态也贴 default_skin 纹理（用户拍板，非返修）

> 前一轮彩色态用纯色填充，用户拍板改为「彩色态也用 default_skin 的 8 张纹理图」（纯色块 vs 单色纹理视觉不一致；default_skin 8 张就是为彩色准备的纹理）。本次只改渲染层「怎么显示」，不动逻辑层（状态机/候选池/全清钩子/续存/悔棋快照全不变）。

### 改动摘要（追加）

- **`BlockSkinCatalog` 新增彩色映射** `ColoredSpriteName(int colorIdx)`：colorIdx（=BlockColor 枚举 0..7）→ `blocks_main_<编号>` 静态查表（`ColoredIds[8]`）。映射 1:1（boss 看图比色已定），8 个目标编号 = default_skin 实际文件集（已核对：`default_skin/` 恰有 1/6/14/15/16/18/19/21 这 8 个 png，与映射目标逐一相等）。越界 colorIdx 取首项保底不抛（与 `BlockLayout.ColorOf` 越界归 0 同口径）。
- **`MergeOrderWindow.ApplyCellSkin` 彩色分支改贴图**：签名从 `(Image, bool, string, Color coloredFill)` 改为 `(Image, bool, string, int colorIdx)`；两态现都白 tint + `SetSprite`——单色态贴 `monoLoc`（不变），彩色态贴 `BlockSkinCatalog.ColoredSpriteName(colorIdx)`。原纯色填充逻辑移除。
- **`RenderBoard` 占位色保留**：`CreateImage` 初始仍用 `BlockLayout.ColorOf` 纯色，仅作 sprite 异步加载到位前的占位避免闪空（到位后 `ApplyCellSkin` 切白 tint+贴图）；`ColorOf` 引用因此保留，不悬空。

### 映射表（colorIdx → blocks_main 编号，已落 `BlockSkinCatalog.ColoredIds`）

| colorIdx | BlockColor | blocks_main 编号 |
|---|---|---|
| 0 | Blue | 14 |
| 1 | Green | 15 |
| 2 | Yellow | 16 |
| 3 | Orange | 21 |
| 4 | Red | 1 |
| 5 | Steel | 6 |
| 6 | Teal | 18 |
| 7 | Purple | 19 |

### 文件清单（追加）

- `Module/BlockBlast/BlockSkinCatalog.cs` — 加 `ColoredIds[8]` + `ColoredSpriteName`。
- `UI/BlockBlastUI/MergeOrderWindow.cs` — `RenderBoard`/`ApplyCellSkin` 彩色态改贴图（占位纯色保留）。
- `Assets/Editor/Tests/BlockBlast/BlockSkinStateTests.cs` — 加 2 条映射单测。
- `design-docs/50-block-skin-switch.md` — §二渲染表 + 新增「两态都贴纹理」NOTE；§五加「类型→纹理图编号」映射表；§七范围行；§八 B1（纯色→纹理）。

### 给 test 的新/改验证点（追加）

1. **新增映射单测**（EditMode，纯逻辑，应全绿）：
   - `ColoredSpriteName_MapsEachColorIdxToExpectedDefaultSkin`（8 个 colorIdx 各返回预期 `blocks_main_<n>`，对上表）。
   - `ColoredSpriteName_OutOfRange_FallsBackToFirst_NoException`（-1/8/int.MaxValue 不抛、取首项 `blocks_main_14`）。
2. **B1 改判**（B 组运行手验）：原「全新进游戏纯色」→ **全新进游戏彩色态按类型显示 default_skin 纹理图**（不同类型显示各自那张，对上表）。**重点核彩色态运行期 sprite 寻址加载成功**——`SetSprite("blocks_main_<n>")` 走散 PNG 路径（同单色态 B2 的寻址机制，记忆 `project-setsubsprite-not-spriteatlas-v2`：blocks_main 也是散 PNG 在 UIRaw 收集器组，非走 SpriteAtlas v2）。**dev 无 Play 环境未实证，这是本次最大风险点**：若 default_skin 散 PNG 寻址失败会贴空/白块，请 test 重点看彩色态 8 类是否各贴对纹理。
3. **既有 B2/B3/B4 单色态不受影响**（本次只改彩色分支，单色路径逐字节不变）。
4. **逻辑层全不变**：状态机/候选池/全清钩子/续存/悔棋快照单测（A1–A9 + Integration + Undo）应仍全绿（本次未碰这些代码）。

### 编译自检状态（追加）

- **本环境 Unity MCP 仍不可达**（`read_console` 返回 No Unity Editor instances）。静态 grep 自检通过：① `ColoredSpriteName` 定义 1 处、调用点 1 处（`ApplyCellSkin`）+ 8 测试引用，无悬空；② `ColoredIds` 恰 8 项（14/15/16/21/1/6/18/19），与映射表顺序一致；③ `ApplyCellSkin` 签名改 `int colorIdx`、单一调用点已同步、无残留 `coloredFill` 引用；④ default_skin 实际文件集与映射目标编号逐一相等（已 ls 核对）；⑤ 测试用的 `foreach (var (idx,name) in ...)` 元组解构在 C# 9.0 支持，同目录 `AttrLedgerClientTests`/`RedeemCodeSystemTests` 已有同款用法。
- 编辑器编译 + EditMode 实跑留 test 在其 MCP 环境执行。
