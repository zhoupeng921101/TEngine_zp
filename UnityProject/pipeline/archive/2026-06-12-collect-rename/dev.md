# 状态:开发(dev)

> 开工先读本文件 + state/plan.md 交接区(角色职责在 `.claude/agents/pipeline-dev.md`,spawn 时自动注入)。每完成一项就更新这里。

## 当前任务:collect-rename(dev 完成,待 test 验收)

纯改名重构:把 merge-order 转用的 Collect* 共享设施正名为 merge-order 语义。行为零变化(逻辑、控制流、数值、签名形参顺序均未动,仅符号名/类型名/资源名)。

### 改名清单(旧名 → 新名 → 理由)

| 旧名 | 新名 | 理由 |
|------|------|------|
| `CollectElement`(enum 元素类型) | `MergeElement` | 现服务 merge-order 元素携带/合成;Merge 切中合成语义 |
| `CollectDemo`(静态表现类:Glyph/ColorOf) | `MergeElementVisual` | 只做元素 glyph/纯色表现(零美术);原名 Demo 误导成完整玩法,Visual 准确 |
| `CollectDemo.cs`(文件) | `MergeElementVisual.cs` | 随主类型改名(含 enum + visual helper),.meta GUID 保留 |
| `CollectWinWindow`(UIWindow 类) | `MergeOrderWinWindow` | merge-order 通关面板,与 MergeOrderWindow 同族;保留 WinWindow 后缀(符合 naming-rules XxxWindow) |
| `CollectWinWindow.cs`(文件) | `MergeOrderWinWindow.cs` | .meta GUID 保留 |
| `CollectWinWindow.prefab`(+ .meta + 内部 m_Name) | `MergeOrderWinWindow.prefab` | AssetRaw/UI 按文件名寻址;prefab/.meta 同步 git mv(GUID 不变)、m_Name 同步 |
| `[Window(... location: "CollectWinWindow")]` | `location: "MergeOrderWinWindow"` | location 字符串=寻址键,必须与 prefab 文件名一致 |
| `BlockGameState.CollectClearedElements()` | `HarvestClearedElements()` | 方法语义=收割被清行列上的元素;Collect 与已正名的元素类型冲突,Harvest 避免重叠且表意一致 |
| `BlockGameState.CollectAt()`(私有 helper) | `HarvestAt()` | 随上方方法改名 |
| 测试方法 `CollectClearedElements_*`(×2) | `HarvestClearedElements_*` | 测试名跟踪被测方法 |

> 未改名(刻意保留,非 Collect* 设施):`System.Collections.Generic`(框架命名空间);`BlockGameState.cs:10` 注释"v1 不含 Adventure/Collection"(指游戏模式历史,非本设施);`BlockAlgorithms.cs:436` 注释"收集'清屏钥匙'"(动词用法,与元素设施无关)。

### 文件清单

改名(git mv,GUID 保留):
- `Assets/AssetRaw/UI/Prefabs/CollectWinWindow.prefab` → `MergeOrderWinWindow.prefab`(+ .meta,+ 内部 m_Name)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/CollectDemo.cs.meta` → `MergeElementVisual.cs.meta`(.cs 内容重建为新名)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/CollectWinWindow.cs.meta` → `MergeOrderWinWindow.cs.meta`(.cs 内容重建为新名)

修改(引用同步):
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/BlockGameState.cs`(ElementArr 类型、MakeEmptyElementArr、Drain、Place、HarvestClearedElements/HarvestAt)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderState.cs`(Order/Inventory/PendingElements/NeededTypes/Snapshot 全部 MergeElement)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/MergeOrderConfig.cs`(Order 元素引用 + cref 指向 MergeElementVisual)
- `Assets/GameScripts/HotFix/GameLogic/Module/BlockBlast/PendingPiece.cs`(Elements 字段类型 + 注释 merge-order 化)
- `Assets/GameScripts/HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWindow.cs`(MergeElementVisual.Glyph/ColorOf、HarvestClearedElements、ShowUIAsync<MergeOrderWinWindow>、CloseUI 引用)
- `Assets/Editor/Tests/BlockBlast/MergeOrderTests.cs`(MergeElement、HarvestClearedElements + 2 个测试方法名)

### 验证点(给 test)

1. **编译 0 error**:read_console types=["error"] 为空 —— 已验(本棒 dev 自检通过)。
2. **EditMode 全量 96/96**:run_tests EditMode —— 已验通过(failed=0),含改名后的 `HarvestClearedElements_OutputsList_ClearsOverlay`、`HarvestClearedElements_RowColIntersection_CountedOnce`。请 test 复跑确认。
3. **改名零遗漏**:全工程 grep `CollectElement|CollectDemo|CollectWinWindow|CollectClearedElements|CollectAt` 在 `Assets/**/*.cs` 与 `Assets/**/*.prefab` 应 0 匹配(design-docs 不在本棒范围,会有遗留 Collect 标注)。
4. **GUID 保留**:三对 .meta(2 个 .cs.meta + 1 个 .prefab.meta)git status 显示为 rename(R),guid 字段未变 —— 无孤儿 meta、无引用断裂。
5. **Play 模式窗口加载验证(必做,自动化测不到的寻址链)**:进 Play → 主菜单进入 merge-order 玩法 → 完成 demo 目标单数触发通关 → 确认 `MergeOrderWinWindow` 弹出(标题"通关！"+结算行+两个按钮)。这一步验 location 字符串"MergeOrderWinWindow"↔ prefab 文件名 ↔ m_Name 寻址链通畅;漏改任一处则 LoadGameObjectAsync 找不到资源、窗口空白或报错。再点"再来一局"确认重入 MergeOrderWindow、点"返回主菜单"确认回 MainMenuWindow。

### 标注

- **涉及热更程序集**:是。改动全在 `GameScripts/HotFix/` 下(GameLogic 模块 + UI),属热更范围。
- **Luban**:不涉及(MergeOrderConfig 为硬编码静态配置,不接 Luban,无需重生成)。
- **需 Play 模式手验**:见验证点 5(通关面板寻址链)。
