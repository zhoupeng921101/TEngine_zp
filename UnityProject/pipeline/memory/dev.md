# 角色记忆:开发(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 agent 定义/设计文档/CLAUDE.md/references 未覆盖的经验。

- 图标/元素类表现的零美术方案:`Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 渲染 ◆★ 等 glyph;内置资源无需 YooAsset 释放(2026-06,collect)
- 给既有玩法加模式开关的安全做法:加法式扩展 + 单开关门控 + 所有离开路径调 Exit 清态;off 路径逐字节不变,靠原回归单测兜底(2026-06,CollectMode)
- 镜像既有窗口(如 GameWindow)做新切片窗口:逻辑层单测可全覆盖,风险集中在拖拽手势交互层,交接时显式标注(2026-06)
- 新增 UIWindow 必须配一份同名 prefab 到 `Assets/AssetRaw/UI/Prefabs/<location>.prefab`(`AssetRaw/UI` 按文件名寻址);即便 UI 全代码构建,prefab 也只是 Canvas+GraphicRaycaster 根,可照抄既有窗口 prefab 仅改 m_Name + .meta GUID。漏建则 LoadGameObjectAsync(location) 找不到资源(2026-06,merge-order)
- 2048 式自动合成(满 2 即升级)与"订单要求某等级 ×N":非封顶等级库存恒 ≤1,故 N≥2 的订单只在封顶等级(可堆积)能满足;低级订单数量只能为 1。设计若写「Lv1 ×3」类订单需先合规则(订单吃多个低级 / 允许低级堆积),否则永不可达(2026-06,merge-order)
- 改 UIWindow 类名(AssetRaw/UI 按文件名寻址)必须同步改四处,漏一处运行时找不到资源:① .cs 类名+[Window(location:"...")] 字符串 ② 同名 prefab 文件名 ③ prefab 内 m_Name ④ prefab .meta。prefab/.cs 改名用 `git mv` 连 .meta 一起搬保 GUID;.cs 内容用 Write 重建后 `git mv` 旧 .meta→新名 .meta(GUID 不变)、rm 旧 .cs。代码内 ShowUIAsync<T>/CloseUI<T> 泛型引用随类名走,不涉寻址(2026-06,collect-rename)
- 在 merge-order 状态机上叠新系统(结算/特殊轨/灵力/女神):新字段一律同步进 `MergeOrderState.Snapshot.Capture/Restore`,否则悔棋只回滚旧字段、新状态不回滚——值类型(SpecialOrder/Order)浅拷贝即可,List 用 ToArray()+RestoreXxx。off/Classic 路径不碰这些字段,回归靠原单测兜(2026-06,core-loop)
- 把「连消倍率只乘显示分、不污染元素产出/全清判定」这类隔离铁律落地:做成独立纯函数模块(ClearSettlement.Settle 进 MergeOrderState 改状态、回 result 结构),窗口只接线;倍率用整数千分比(×1.2=1200)避免浮点不可单测。隔离点本身写成专门单测(乘倍率后元素数仍 = 用基础分算的 k)(2026-06,core-loop)
- 智能生成上层仲裁套现状 dynamicWeight:仲裁器返回「接管/不接管」二态,不接管=回落现状发牌逐字节不变——把「现状零改变」做成可测断言(四规则都不触发→Override==false)。规则到 trio 复用既有 BlockAlgorithms,别重写启发式(2026-06,core-loop)
- unityMCP 桥可能整会话 no_session:Unity.exe 进程在但 `refresh_unity` 反复 60s 超时未 ready、所有读 no_session = 编辑器卡导入/桥插件未加载,非编译错误(编译错会进「带错就绪」态可被 read_console 读出)。纯逻辑 C# 无法本会话自跑时,逐文件静态复核(tuple/Array.Empty/既有 API 签名/test asmdef 引用可达)+ 把运行验证交接给自带 unityMCP 的 test 子会话,并在交接区显式标注阻塞与重试条件(2026-06,core-loop)
- 该 no_session 阻塞会跨轮持续(同一编辑器进程 PID/启动时间不变):若 dev 与 test 子会话都连不上同一实例,再 spawn 子会话重试也是徒劳,属环境问题须人工解(用户重连 MCP-for-Unity 桥 / 必要时重启编辑器使 `instances` 非空)。dev/test agent **不擅自强杀编辑器**(有未保存编辑器态风险)→ 此时正确动作:静态复核确认无代码缺陷后,把「需人工恢复桥会话」作为 blocker 上报 boss,而非继续在子会话里空转重试(2026-06,core-loop 返修轮)
- Unity no_session 的连接诊断(进程冻结 vs 桥会话未注册的判读、各自处置)以 `/unity-check` skill「边界」为单一信息源,不在本文件复述
- `run_tests` 在编辑器处于(或正进入)Play Mode 时直接返 `status:failed` + 「Cannot start a test run while the Editor is in or entering Play Mode」,非测试失败。先 `manage_editor action=stop` 退出 Play Mode、停几秒再复跑。判读编译是否干净不能只看 console:`CSxxxx` 才是编译诊断,`MCP-FOR-UNITY: disposed object`(域重载期桥重连瞬态)/无堆栈 `NullReferenceException`(Play Mode 运行期事件)都非编译错;EditMode 测试能跑起来 = 相关程序集已编译通过,本身就是最强的编译自检(2026-06,tarot-blind-box)
