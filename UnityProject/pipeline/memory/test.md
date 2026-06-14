# 角色记忆:测试(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 agent 定义/设计文档/CLAUDE.md/references 未覆盖的经验。

- UI 经 UICamera(ScreenSpaceCamera)渲染,Play 手验/截图须经它取景(2026-06,collect 实测)
- MCP 无法可靠模拟指针拖拽(BlockPieceDragger),拖拽/手势类验证点一律标「需人工 Play 手验」;其逻辑层改用单测 + state 注入覆盖(2026-06)
- 单测先隔离跑 `run_tests(EditMode, assembly=BlockBlast.Tests)`;全量 EditMode 可能混入其他程序集的无关失败,先隔离再下判定(2026-06)
- 异步连开/连关窗口得到的瞬态读数不可直接判缺陷:改用正常 UI 路径复现,复现不出按测试驱动假象处理(2026-06,Diamond 9/6 案例)
- Unity MCP 不可达(Editor 未开/被占用)时:第 1-3 类运行验证跑不了,判 BLOCKED(非 PASS、非代码 FAIL)、把补跑命令清单写进报告,不伪造运行结果;第 4 类 code review + 静态 API 签名核验 + 单测逐例手推仍照做,能挡住编译/逻辑缺陷(2026-06)
- Unity 连接诊断(进程冻结 vs 桥会话未注册的判读、各自处置)以 `/unity-check` skill「边界」为单一信息源,不在本文件复述
- 同一未注册实例跨轮复跑徒劳:首轮已做全量静态核验且代码零改动时,后续轮只需确认阻塞仍在 + 刷新诊断,不重复逐例手推(无新信息)(2026-06)
- 静态核验单测:逐例手工走断言路径(尤其级联/快照回滚类),配合 grep 实际签名,可在无 Editor 时预判 90% 编译与逻辑错;但 HybridCLR 热更编译 + YooAsset 模拟寻址的真导入仍须 Editor 确认,不可代签 PASS(2026-06)
- UIWindow 寻址链验证(尤其 prefab/location 改名)绕开拖拽通关:Play 中用 execute_code 反射调 `GameModule.UI.ShowUIAsync<T>(new object[]{userData})`(T 从已加载 GameLogic 程序集按全名取),再 `GameObject.Find("<WinName>")` 确认实例化 + 子节点齐全,即证 location↔prefab 文件名↔m_Name 三处一致、LoadGameObjectAsync 未断;CloseUI<T> 泛型解析则触发对应按钮 onClick.Invoke 验(2026-06,collect-rename)
- 要驱动 UIWindow 私有字段/方法(注入 state + 调私有刷新/点击 handler)而非仅查 GameObject:UI 模块无同步 getter,从其私有字段 `_uiStack`(List)遍历取类型匹配的托管实例,再反射读写私有字段/Invoke 私有方法;ValueTuple 字典键的元素字段名运行时是 `Item1/Item2` 不是声明的具名(`type/level`),反射取值用 Item* 否则 NRE(2026-06,tarot-blindbox F12/F13 手验)
- code-built 窗口(UGuiFactory 运行时建节点、无 prefab codegen)的节点名是运行时查找名,naming-rules 的 `m_btn_`/`m_text_` 前缀规则不适用,与全窗既有裸名体例一致即合规——别误判前缀缺失为违规(2026-06,tarot-blindbox)
- execute_code 反射收集程序集类型别用裸 `asms.SelectMany(a=>a.GetTypes())`:某程序集中途抛 ReflectionTypeLoadException 会让整段枚举半途终止,导致同一会话两次调用一会找得到 GameLogic.GameModule、一会找不到(采样不稳)。改用 per-assembly try/catch、catch `ReflectionTypeLoadException` 取其 `.Types` 非空项、其他异常 `continue` 的稳健收集器(2026-06,temple 手验)
- 主菜单冷启动直接 ShowUIAsync 开游戏内窗口常 NRE(MergeState/UI 栈未就绪):正路是先点真实入口按钮(如 MainMenuWindow 的 BtnMerge)进对局,等 `GameObject.Find("MergeOrderWindow")` 出现后再注入 state + 点窗内入口按钮开子窗——全程走真实 UI 路径,比反射硬开模块内部稳且更接近用户操作(2026-06,temple 手验)
- 持久化/存档类功能:单测锚 InMemory Provider,Play 模式手验须补走**生产真实 Provider 路径**——execute_code 反射读 `Persistence.Provider` 确认类型是 PlayerPrefsProvider(非 InMemory),再 SaveAsync→读 PlayerPrefs raw→Reset→Load→ImportMeta 跑跨会话往返,验真实存储栈而非测试替身(2026-06,save-system)
- execute_code 不能写顶层 `using`(代码被包进方法体,using 触发 "Identifier expected"):全程用全限定名(`System.Reflection.BindingFlags`/`UnityEngine.PlayerPrefs`);反射取基类静态成员(如 SimpleSingleton<T>.Instance)须带 `FlattenHierarchy` flag,否则 GetProperty 返 null 致 NRE(2026-06,save-system)
- Luban 配置表桥接(Mgr/POCO)的 EditMode 单测走 AssetDatabase 直读 .bytes 绕 YooAsset,刻意不验真实加载链;Play 手验补走生产真实路径(ResetForTest 清缓存 → Get(id) 强制走 ConfigSystem.Instance.Tables.TbXxx 的 YooAsset 加载 → 反射读 POCO 字段比对源 xlsx),才覆盖 EnsureLoaded 真实 LoadAsset。源↔产物交叉核验:xlsx 表头 ##group 行各列分组 + 生成行类型字段读序(只含 client 组列、按 xlsx 列序)要对得上,否则字节布局错位(2026-06,numeric-system)
- 单测用手写 POCO 夹具(非真实 .bytes 行)的逻辑层,夹具值可能与源 xlsx 漂移而单测全绿——夹具是自洽闭环测不出数据偏差。Play 模式调真实 Get(id) 反射读字段比对源 xlsx 能逮到:实例 ItemSystem 中 itemdef.xlsx 30006 automatic=0 但夹具写 1,因无验收项校验 automatic、无用例调用读它的 GrantOnAcquire,夹具值从未被断言。判据:某字段只被夹具设置、无验收项校验、无用例读取 → 既是覆盖缺口也是数据漂移温床,报记录项不判 FAIL(2026-06,item-system)
