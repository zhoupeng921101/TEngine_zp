# 角色记忆:测试(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 agent 定义/设计文档/CLAUDE.md/references 未覆盖的经验。

- UI 经 UICamera(ScreenSpaceCamera)渲染,Play 手验/截图须经它取景(2026-06,collect 实测)
- MCP 无法可靠模拟指针拖拽(BlockPieceDragger),拖拽/手势类验证点一律标「需人工 Play 手验」;其逻辑层改用单测 + state 注入覆盖(2026-06)
- 同一未注册实例跨轮复跑徒劳:首轮已做全量静态核验且代码零改动时,后续轮只需确认阻塞仍在 + 刷新诊断,不重复逐例手推(无新信息)(2026-06)
- 静态核验单测:逐例手工走断言路径(尤其级联/快照回滚类),配合 grep 实际签名,可在无 Editor 时预判 90% 编译与逻辑错;但 HybridCLR 热更编译 + YooAsset 模拟寻址的真导入仍须 Editor 确认,不可代签 PASS(2026-06)
- UIWindow 寻址链验证(尤其 prefab/location 改名)绕开拖拽通关:Play 中用 execute_code 反射调 `GameModule.UI.ShowUIAsync<T>(new object[]{userData})`(T 从已加载 GameLogic 程序集按全名取),再 `GameObject.Find("<WinName>")` 确认实例化 + 子节点齐全,即证 location↔prefab 文件名↔m_Name 三处一致、LoadGameObjectAsync 未断;CloseUI<T> 泛型解析则触发对应按钮 onClick.Invoke 验(2026-06,collect-rename)
- 要驱动 UIWindow 私有字段/方法(注入 state + 调私有刷新/点击 handler)而非仅查 GameObject:UI 模块无同步 getter,从其私有字段 `_uiStack`(List)遍历取类型匹配的托管实例,再反射读写私有字段/Invoke 私有方法;ValueTuple 字典键的元素字段名运行时是 `Item1/Item2` 不是声明的具名(`type/level`),反射取值用 Item* 否则 NRE(2026-06,tarot-blindbox F12/F13 手验)
- code-built 窗口(UGuiFactory 运行时建节点、无 prefab codegen)的节点名是运行时查找名,naming-rules 的 `m_btn_`/`m_text_` 前缀规则不适用,与全窗既有裸名体例一致即合规——别误判前缀缺失为违规(2026-06,tarot-blindbox)
- execute_code 反射收集程序集类型别用裸 `asms.SelectMany(a=>a.GetTypes())`:某程序集中途抛 ReflectionTypeLoadException 会让整段枚举半途终止,导致同一会话两次调用一会找得到 GameLogic.GameModule、一会找不到(采样不稳)。改用 per-assembly try/catch、catch `ReflectionTypeLoadException` 取其 `.Types` 非空项、其他异常 `continue` 的稳健收集器(2026-06,temple 手验)
- 主菜单冷启动直接 ShowUIAsync 开游戏内窗口常 NRE(MergeState/UI 栈未就绪):正路是先点真实入口按钮(如 MainMenuWindow 的 BtnMerge)进对局,等 `GameObject.Find("MergeOrderWindow")` 出现后再注入 state + 点窗内入口按钮开子窗——全程走真实 UI 路径,比反射硬开模块内部稳且更接近用户操作(2026-06,temple 手验)
- 持久化/存档类功能:单测锚 InMemory Provider,Play 模式手验须补走**生产真实 Provider 路径**——execute_code 反射读 `Persistence.Provider` 确认类型是 PlayerPrefsProvider(非 InMemory),再 SaveAsync→读 PlayerPrefs raw→Reset→Load→ImportMeta 跑跨会话往返,验真实存储栈而非测试替身(2026-06,save-system)
- execute_code 不能写顶层 `using`(代码被包进方法体,using 触发 "Identifier expected"):全程用全限定名(`System.Reflection.BindingFlags`/`UnityEngine.PlayerPrefs`);反射取基类静态成员(如 SimpleSingleton<T>.Instance)须带 `FlattenHierarchy` flag,否则 GetProperty 返 null 致 NRE;反射注入私有字段前先确认 `FieldType.Name`——类型不匹配(如 float 值传给 int 字段)会 ArgumentException,需改用精确类型强转(如 `(int)-200`)(2026-06,save-system/fusion-test)
- Luban 配置表桥接(Mgr/POCO)的 EditMode 单测走 AssetDatabase 直读 .bytes 绕 YooAsset,刻意不验真实加载链;Play 手验补走生产真实路径(ResetForTest 清缓存 → Get(id) 强制走 ConfigSystem.Instance.Tables.TbXxx 的 YooAsset 加载 → 反射读 POCO 字段比对源 xlsx),才覆盖 EnsureLoaded 真实 LoadAsset。源↔产物交叉核验:xlsx 表头 ##group 行各列分组 + 生成行类型字段读序(只含 client 组列、按 xlsx 列序)要对得上,否则字节布局错位(2026-06,numeric-system)
- 单测用手写 POCO 夹具(非真实 .bytes 行)的逻辑层,夹具值可能与源 xlsx 漂移而单测全绿——夹具是自洽闭环测不出数据偏差。Play 模式调真实 Get(id) 反射读字段比对源 xlsx 能逮到:实例 ItemSystem 中 itemdef.xlsx 30006 automatic=0 但夹具写 1,因无验收项校验 automatic、无用例调用读它的 GrantOnAcquire,夹具值从未被断言。判据:某字段只被夹具设置、无验收项校验、无用例读取 → 既是覆盖缺口也是数据漂移温床,报记录项不判 FAIL(2026-06,item-system)
- EditMode `run_tests` 触发的域重载会让桥会话短暂注销:`get_test_job` 轮询中途可能报 "No Unity Editor instances found"。先判子类——`Get-Process Unity` 仍 `Responding=True` 且 `active_instance` 服务端记录未变 → 域重载期瞬态注销(可恢复),非环境阻塞;重试 `manage_scene get_active` 待桥重注册即可继续轮询同一 job_id 取回完整结果。别据此瞬态读数判 BLOCKED(2026-06,settings)
- 配置表源 xlsx 仓在 `<repo>/Configs/GameConfig/Datas/`(`UnityProject` 的同级兄弟目录,**不在** UnityProject 内);bytes 产物在 `UnityProject/Assets/AssetRaw/Configs/bytes/`。dev 交接区写 xlsx 路径常按 Configs 仓根相对,核「xlsx 是否存在」别在 UnityProject 内找,用绝对路径 `<repo>/Configs/...` 或 `git status` 看 `?? ../Configs/...`(2026-06,mail)
- 协议生成物在本项目合并成单文件（OuterMessage.cs / OuterEnum.cs），grep 查协议字段时目标须是这两个文件，而非按消息名 glob（2026-06，rank client）
- conventions §6 交叉检判定精度：设计稿「增量自身验收」段（如 SK1/SK2）措辞若有部分仍成立（Fetch 行为）、有部分随后续增量失真（「全系统无真实网络调用」），判为文档同步遗漏而非代码 FAIL；代码行为正确时不升 FAIL，记录供 dev 下轮精化（2026-06，rank client）
- execute_code 异步入口：在 PlayMode 中无法写顶层 await，须用 `UniTask.Void(async () => { ... })` 发起异步调用、在内部 `Debug.Log` 记结果，再用 `read_console(filter_text=...)` 捞特定标签的输出作为断言依据（2026-06，E1 rank 真往返）
- 服务端 Log 是真往返的双边证据之一：Fantasy 服务端对每次 RPC 处理结果写 Develop Debug.log，路径 `Bin/Debug/Logs/Server/YYYYMMDD/Develop/Log.Develop.*.Debug.log`；上报/查榜条目与客户端 console 对照可完整核验往返正确性，mongosh 不可用时可用服务端 log 替代 MongoDB 直查（2026-06，rank E1）
- 接口退役核验可用 PlayMode execute_code 反射：`type.GetMethods(BindingFlags.Public|Instance)` 遍历确认方法名缺席、`type.GetFields/GetProperties` 确认字段名缺席，比 grep 更强（覆盖运行期热更程序集的真实类型表面，而非仅静态源码）；配合 grep 零命中双重保险（2026-06，rank settle retire）
- 退役类改动的越界试探：除正向用例外须专项覆盖「老存档含遗留字段」场景——用真实 JSON 字符串含退役字段喂 JsonUtility.FromJson，断言不抛+其他字段保真；PlayMode execute_code 直接调 JsonUtility 即可，无须单独起测试（2026-06，rank settle retire）
- conventions §6 交叉检漏点：设计稿内 `[!WARNING]`（读前必看）和 `[!NOTE]`（立项信息）两类标注块常被同步改写清单遗漏——这两块不在正文正文中、位于文档顶部，dev 按「§节号」列改写点时容易跳过；test 交叉检须显式对这两块的每个表述做现状核验（2026-06，account-client）
- mongosh 不在本项目 mongodb-portable 包内（portable 包只含 mongod.exe），MongoDB accounts 集合直查须用服务端 Log 替代：AccountServiceComponent Init + 客户端完整登录链路 console 组合构成 E1 真往返的充分证据；若要直查须另行安装 mongosh（2026-06，account-client E1）
- FANTASY_UNITY-gated 的静态方法（FantasyClient.FantasyNetwork.Shutdown/Boot）在 PlayMode execute_code 反射可达：从 Fantasy.Unity 程序集用稳健收集器（per-assembly try/catch）取 FantasyClient.FantasyNetwork 类型，GetMethod 后 Invoke 即可；这是测试登出链路的最短路径（无需找 GameModule.UI 开设置窗）（2026-06，account-client E2）
- UniTask<T> 在 execute_code 中须用 GetAwaiter + IsCompleted 自旋等待（不能用 AsTask/dynamic）：`UniTask<ChangeResult>` 无 AsTask() 方法，dynamic 绑定在此 Unity 环境无 Microsoft.CSharp.RuntimeBinder.Binder；正确路径：`var awaiter = task.GetAwaiter(); while (!awaiter.IsCompleted) { await UniTask.Yield(); } var result = awaiter.GetResult();` 放在 `UniTask.Void(async () => {...})` 内部（2026-06，player-attr E2）
- PlayerInfo 的 RenameCount 是字段（field）不是属性（property）：`GetProperty("RenameCount")` 返 null，须用 `GetField("RenameCount", BindingFlags.Public|NonPublic|Instance)`；在不熟悉时先用 `GetFields(Public|NonPublic|Instance)` 列全字段集再定位（2026-06，player-attr E2）
- conventions §6 交叉检：设计稿顶部 WARNING 块（「读前必看」摘要区）常被 dev 同步清单遗漏，尤其当 §节改写后该块内的对应摘要句过时，test 须显式 grep WARNING 块每个条目与同文件 §节现状对比（2026-06，player-attr Code Review）
- TryChangeAsync 边界测试参考值（design-docs/38 落地数据）：AttrType.All=-1 本地即拒，无 RPC；delta=0 服务端返 Success（余额不动，无推送）；连续两次请求服务端依序处理（无重入锁问题）；E2 真往返路径：用 +500 充入→-100 扣减验改名，可在 PlayMode execute_code 中用 TryChangeAsync 直接触发（2026-06，player-attr E2/E3）
- 返修复检范围精确识别：dev 交接区注明「仅需 grep 复核 + 通读 X 段,编译/单测/真往返不重跑」时，test 只跑指定复检项即可；但 diff 须全量核查，dev 返修可能超出交接区列点（同源过时口径被一并清理），应确认额外改动合规而非报新问题（2026-06，player-attr FAIL-1 返修）
- E1 活动登录计数触发路径：服务端 OnLogin 的 activity 累计计数只在实际登录时触发（不是 GameContext 加载），触发方式是反复 Stop→Play（每次 Play 完整走登录握手）；Play 模式加载完但尚未登录时 UnlockedAvatarIds 可能是持久化历史值，须等 MainMenuWindow 出现后再读；服务端 Develop Info log 有 `ActivityEvalHelper: activity_id=X 本周期发奖完成,mailId=...,reward=...` 一行可确认发奖已触发（2026-06，event-unlock-client E1）
- MailListEntry 客户端字段结构：字段为 MailId/SenderTextId/TitleTextId/ContentTextId/SendUnixMs/HasReward/Claimed（全 public field，无 property；无 RewardPoolId）；领取后无法从列表项直接判断是否是 EVENT 活动邮件，须以「HasReward=True + Claimed=False + 领取后 UnlockedAvatarIds 增量」组合作为 E1 通路验证证据（2026-06，event-unlock-client E1）
- GameModule.UI 的 ShowUIAsync 有两个重载：泛型 `ShowUIAsync<T>(Object[])` 和非泛型 `ShowUIAsync(Type, Object[])`；execute_code 反射调用时，非泛型重载（`GetMethod("ShowUIAsync", new Type[]{typeof(Type), typeof(object[])})`）更稳定，泛型重载需先 `MakeGenericMethod`，两路均可行（2026-06，tarot-hud-attr-bind V1）
- 主菜单 BtnStart 在此项目指向 MergeOrderWindow（合成订单）而非 GameWindow（Classic）；test 需用 ShowUIAsync(Type, Object[]) 直接打开 GameWindow 做 HUD 验证，而非走按钮点击路径（2026-06，tarot-hud-attr-bind V1）
- UIModule 字段名为 `_uiStack`（List 类型，私有）；GameContext 的 Instance 需带 FlattenHierarchy flag 才能从 SimpleSingleton<T> 基类继承的静态属性取到（`BindingFlags.Public|Static|NonPublic|FlattenHierarchy`）（2026-06，tarot-hud-attr-bind）
- conventions §6 交叉检：设计文档局部改写时，同文档内其他段（§一交付清单、效果图拆解表、Mermaid 图 stub 节点）可能保留过时措辞而未被 dev 同步改写；test 须逐节检查文档内部一致性，不只核「目标节是否改写」（2026-06，tarot-hud-attr-bind SV6）
- GameModule 在 GameLogic 程序集（非 TEngine.Runtime），反射收集时须指定 `GetAssemblies().FirstOrDefault(a => a.GetName().Name == "GameLogic")` 再遍历，不在 TEngine.Runtime/TEngine.Editor 中（2026-06，ledger-client Play 验证）
- `_disposed + _fetchToken` 双保险在 Play 模式中可用 burst tab click + 立刻 closeBtn.onClick.Invoke() 方式触发验证：连点 tab × N → 立刻关窗,无 NRE 即证 dispose 保护生效，不需复杂 async 注入（2026-06，ledger-client PV11）
- 全代码生成窗口（prefab 仅最小壳,全部节点运行时 BuildLayout 生成）的 Play 手验路径：ShowUIAsync → 等 1 帧 → `GetComponentsInChildren<Text>(true)` 读 m_text_Status 内容即可断言状态栏文案（正在加载.../暂无流水/网络异常 等），不需找 prefab 节点（2026-06，ledger-client V1）
- Fantasy 服务端未起时 PlayMode 下日志会出现「重连次数已达上限」error：属 Fantasy 内部网络降级日志,非业务崩溃,不升 FAIL；RemoteAttrLedgerSource Session=null → NetworkDown 兜底文案「网络异常,请检查连接」属正常降级路径（2026-06，ledger-client V1）
- TEngine Log.Info/Warning/Error 在 PlayMode execute_code 中调用后不产生 Unity console 对应级别条目（路由到 TEngine 内部日志门面）；验证日志调用本身成功须用 `Debug.Log("[TAG] ... called successfully")` 包一层作为执行凭证，而非依赖 read_console filter 捞 Log.Warning 输出（2026-06，activity-client V7）
- fire-and-forget hook（`?.IncrementAndLogAsync(...).Forget()`）的 PlayMode 手验路径：①反射从 _uiStack 取窗口托管实例；②反射调私有 TriggerGameOver/TriggerWin；③read_console filter_text="[Activity]" 捞日志；④检查对应窗口（GameOverWindow/MergeOrderWinWindow）是否弹出；五步合一即证 hook 调用 + 日志 + 不阻塞三要素（2026-06，activity-client）
- 防重验证（_gameOverTriggered/_finished）可在 Play 手验时直接读反射字段：反射读 `_gameOverTriggered` before=False/after=True 配合「连续调两次 TriggerGameOver，[Activity] 日志只出现一次」，比纯桩注入更直接且不需注入 FakeSource（2026-06，activity-client V3）
