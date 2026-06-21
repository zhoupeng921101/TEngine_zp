# 关单总结:save-system — MergeOrderState 跨会话存档(2026-06-14;自治模式·放手默认)

**结论:PASS 交付**。两段式:full(plan→dev→test)出存档核心 → dev-test 补时机层缺口。**打回轮次 0**(两段均一轮过)。git 基线 commit `2bfc03cb`。设计基线 `design-docs/14-save-system.html`(A1–A14 + §3.4 时机触发表;详见同目录 plan.md/dev.md/test.md)。

兑现:解决了遗留 #14——MergeOrderState 元进度从单局尺度升为跨会话尺度,虔诚币/神庙/经验/守护者等级/盲盒/女神 退出重进不再清零。

## 运行验证(test 子会话经 MCP 直跑,桥连 `UnityProject@02a6dcaa`)
- 编译 0 CS error。
- EditMode `BlockBlast.Tests` **190/190 全绿**(168 基线 + 20 存档核心 MergeMetaSaveTests + 2 时机层谓词例),零回归。
- Play 手验经生产 PlayerPrefsProvider 真实路径:跨会话往返保真、跨天祈愿重置、全清这一手元进度即便无后续元动作也跨会话保真、pause==true 落盘 / pause==false 不写。截图/取证见 test.md。
- Code Review 5 红线全过;序列化层同步纯方法、磁盘 IO 走 UniTask 异步外壳(禁同步 IO 红线达标)。

## 实现范围
三层分层(序列化纯逻辑 / 存储 UniTask 异步外壳 / 时机层窗口生命周期),复用既有 `Persistence.Provider` 接缝(生产 PlayerPrefs / 测试 InMemory),不自造存储栈。文件:3 新增(`MergeMetaSave.cs` DTO、`MergeMetaPersistence.cs` 序列化+存储、`MergeMetaSaveTests.cs` 22 例)+ 4 改(`MergeOrderState` ExportMeta/ImportMeta/脏位、`BlockGameState.ResetForMergeOrder` 织入加载、`MergeOrderWindow`/`TempleWindow` 元动作标脏+落盘+pause 兜底)。不接 Luban。

## 拍板归属(全 plan/dev/test 放手默认自主,无一上交用户)
- 持久化边界:只存元层 13 字段,局内瞬态(棋盘/手牌/订单)不存、每局重开;断点续玩不做(任务边界)。
- 格式 JSON(JsonUtility);存储介质 PlayerPrefs(经 Persistence.Provider,O4 取降级备选而非沙盒文件——零新存储栈、InMemory 可测;沙盒文件留独立轮次)。
- Energy 不进盘(局内瞬态,防养体力漏洞);TotalScore 进盘当累计。
- 落盘节流:每次元动作即异步落盘(元动作频率低)。
- 加载走同步 Provider 读(PlayerPrefs 非阻塞,与既有 BlockGameState.Load 同口径,不触红线);写盘走 SaveAsync UniTask 外壳。MergeOrderState 保持纯逻辑(不 using UniTask/不碰磁盘)。
- 版本号 + 迁移 + 逐字段保底夹值(神庙数组长 12、等级≥1、索引夹界),对篡改/截断档产出合法不变量;WishUsedToday 存 lastWishResetDate 跨天重置。

## 时机层缺口闭合(dev-test 段,boss 代决本轮补)
- 缺口①(全清→女神/盲盒未标脏):`PlaceAndResolve` 结算后用谓词 `settle.AllClearRewarded || settle.GoddessLeveledUp || settle.BlindBoxGained>0` 判元层变更并落盘,补 2 例单测锁「结算改元层 ⇔ 须落盘」。
- 缺口②(pause/quit 兜底):**关键实现层取舍**——UIWindow 非 MonoBehaviour(UIBase 派生、UIModule 手动驱动),直接加 OnApplicationPause/Quit 魔法方法会编译通过但永不触发=死代码;改为订阅 TEngine `UpdateDriver` 的应用暂停事件(`Utility.Unity.AddOnApplicationPauseListener`,OnCreate 订阅 / OnDestroy 解订阅),是唯一确会触发的机制。正常关窗仍由 OnDestroy 的 FlushSaveIfDirty 兜底。

## 模型档 / 运行登记
- 两段均 plan/dev/test=opus(实际 dev-test 段无 plan)。
- 第一段 Run `wf_01a74c52-492`(Task `wsjah95uq`,full)PASS;第二段 Run `wf_5cde8e8d-840`(Task `wpeamco4r`,dev-test 补缺口)PASS。

## 遗留/观察(转主 boss.md)
- **遗留 #14 已解决**(本轮兑现跨会话存盘)。
- 真机 OnApplicationPause 真实触发未在编辑器验(MCP 不能挂起编辑器),逻辑等价路径已用生产 Provider 跑通,真机切后台落盘留人工冒烟(设计 §3.3 范畴)→ 转主 boss.md 遗留。
- 沙盒文件存储介质(替代 PlayerPrefs)、局内棋盘断点续玩:均为可选独立升级轮次,本轮明确不做。
