# 角色记忆:策划(跨任务经验)

> 开工先读本文件;收尾把新的可复用经验沉淀进来(一条一行,过时即删)。
> 准入:只记跨任务可复用、且 agent 定义/设计文档/CLAUDE.md 未覆盖的经验。

- 玩法依赖未建框架(如关卡系统)时,切「独立 demo 切片」规避:移除星级/存档/Map 等框架件,目标硬编码单关(2026-06,collect 方案 A 先例)
- 在已通过的切片上叠新系统:新逻辑挂新「模式门控开关」+ 独立窗口 + 独立状态类(组合,不塞进共享 GameState),旧模式 off 时零影响——回归红线最干净(2026-06,merge-order 在 08 之上先例)
- 复用基线已实现链路时,先查找真实符号(grep 实现文件)再写挂接点,验收表「涉及模块」才能精确到方法名供 dev 定位(2026-06,查 CollectClearedElements/PlaceAndResolve 先例)
- 「以未提交工程现状为基线」的补完任务:先 `git diff` 逐文件核实前序(main/他人)的真实落点,别照 boss/简报的过时快照设计——简报可能停在动手前(2026-06,简报称"CollectDemo.cs 核心逻辑还在",实际 main 已把它收敛成纯表现工具类)。设计 = 在 diff 核实过的真实现状上补完+验证
- 移除某模块但其设施被别处共用且名字带原模块名(misnomer):优先「保留名字 + 文档标注为已转用」而非改名——改名要连带动 UI location 字符串/prefab/.meta/[Window]/调用点,对已交付切片是真风险;正名列独立低优先级任务。「移除干净」指玩法/入口下线,不等于消灭历史命名(2026-06,CollectElement/CollectWinWindow/CollectClearedElements 转用于 merge-order 先例)
- 用户嫌「固定比例/固定值」不灵活、要求「X 与 Y 关联」:输出映射函数而非新固定值——给公式(线性系数或分档)+ 默认常量 + 保底 + 封顶 + 边界(0/超界)逐档代入表 + 一个调松紧的旋钮命名,理由写够供审(2026-06,消除得分→元素数 ScorePerElement 旋钮先例)
- 补全「原始稿=完整愿景 vs 已落地切片」类设计:已落地代码的实现约束(配置注释、已编码不变量)是地基,优先于原始稿——常能反推翻原稿的设计前提,须显式指出并据此收敛。先查 Config/注释再设计经济(2026-06,MergeOrderConfig.OrderPool 注释「自动配对使非封顶级库存恒≤1」直接判死原稿「Lv1×3」订单,据此把订单形态收敛为「Lv1/Lv2 恒1、仅 Lv3 可堆积」先例)
- 完整设计含大量未落地系统时,每节标注「现状/本篇新增」+ 篇首立「单一事实源=代码」红框 + 每个新系统给「可降级到 demo 范围」说明——防 dev 误把设计目标当现状改坏切片,且便于 boss 按需切片分批落地(2026-06,11 核心补全先例)
- 新增设计稿时先 grep 全库 sidebar 文档树核对各篇是否真同步,别假设上一篇加文档时同步过——文档树是各篇各存一份的副本,极易漏(2026-06 实测:加 11 时各兄弟篇 sidebar 漏挂、停在 10;且采用旧 topbar 布局的篇无 sidebar 树、无处同步,属遗留结构差异,正名/改布局列独立任务勿在加文档时顺手扩范围)
- 批量给全库各篇 sidebar 插同一行(十几篇)时用脚本插入(Edit 工具要求逐篇先 Read,十几次往返不划算),但 design-docs 库各篇是**无 BOM** UTF-8;PowerShell 5.1 `Set-Content -Encoding UTF8` 会注入 BOM 造成不一致——改用 `[System.IO.File]::WriteAllText($p,$text,(New-Object Text.UTF8Encoding($false)))` 写回(ReadAllText 已自动剥 BOM)。收尾跑断链检查(每个 href 落实际文件)+ 抽一篇核 BOM 字节(2026-06,player-info 加 18 实测 Set-Content 注 BOM)
- 接「已建成但未接 UI」的数据层系统时(grep 其 public 入口只在 tests 被调用即可判定):钩子逻辑可单测可落地,但 UI 投放是独立未接项——把验收锚在「直接调该数据层方法」的单测上,不依赖 UI 跑通,并在设计稿/交接区显式标注 UI 投放不在本轮(2026-06,盲盒挂 DeliverSpecial 而 SpecialTrack.Request 仅 tests 调用先例)
- 设计「异步 IO 红线 vs 工程现有同步 Persistence 接缝」类持久化时,按介质拆两层:序列化层(对象↔字符串,纯逻辑同步、单测往返到 string/InMemory Provider,不碰真实磁盘)+ 磁盘 IO 层(异步 UniTask 外壳)。纯逻辑状态类不 using UniTask、不含 IO 调用,保持可在纯 C# 单测直接 new 出来跑;验收点全锚在同步纯方法,异步外壳只要求编译通过+人工冒烟,不强求 EditMode 覆盖。CLAUDE.md「禁同步 IO」红线针对阻塞磁盘/资源 IO,PlayerPrefs 非阻塞 KV 不触线(2026-06,save-system 设计 14 复用 Persistence.Provider 接缝先例)
- 跨会话存档类设计「哪些字段进盘」判据:元层进度(跨局累积/长期语义)进盘,局内瞬态(每局重开)不进盘;边界须配每日字段跨天重置(存上次重置日期 + 加载传入 today 参数使可单测)、version + 缺字段逐字段保底夹值(本地单机文件可被篡改/截断,ImportMeta 对任意输入须产出合法不变量)。与局内 undo 快照分两条独立轨,字段虽重叠但时机/介质/生命周期不同,互不调用(2026-06,save-system 先例)
- Luban 配置表类设计:运行期 `ConfigSystem.Instance.Tables` 走 YooAsset + ModuleSystem,纯 C#/EditMode 跑不通;配置表验收点要锚在「`AssetDatabase.LoadAssetAtPath<TextAsset>(.../xxx.bytes)` → `new TbXxx(ByteBuf)`」直读二进制的 EditMode 测试上(绕 YooAsset,WeightCfgLubanTests 先例),纯逻辑(格式化/桥接)再单独给 InitForTest 注入路径。Luban 行务必桥接成 POCO(业务侧只认 POCO,隔离生成类型,WeightCfgConfigMgr 先例)。导表工具链不可达列 BLOCKED 不判 FAIL。源 xlsx 在仓库根 `Configs/GameConfig/Datas`(与 UnityProject 同级),schema 写数据 xlsx 表头四行(##var/##type/##group/##),planner 备注类字段设 group=e 不导出运行期(2026-06,numeric-system 先例)
- 简报称「工程可能已有 X 表/枚举」需 grep 核实「扩展 vs 新建」时:先看该表是否 TEngine 框架自带模板示例(判据:数据是 demo 内容如服装/test.*,且 grep 表类型名只命中 GameProto 生成代码、无 GameLogic 业务消费者)。是模板示例 + 字段与 spec 冲突(如带 price/exchange_stream)→ 新建独立表(命名加语义后缀如 TbItemDef 区别 TbItem),不扩展——扩展等于把模板改生产表 + 删 demo 行 + 改被模板引用的枚举,回归面更大。枚举档数/语义不符(如现 EQuality 1-4 vs spec 1-6)同理新建,别改被模板表引用的旧枚举(2026-06,item-system:item.TbItem 模板示例新建 TbItemDef 先例)
- 设计持久化/设置类系统前,先 grep 框架(TEngine)本身是否已拥有该关注点的约定,而非只看项目 GameLogic 侧:框架常把「设置」「本地配置」这类引擎级关注点连键名+读写工具+启动加载一并备好。命中即复用其既有键 + 工具(本层只补缺的那一环,如「运行期可切换并落盘」),启动加载零改动即生效、与框架口径一致;别在项目侧另造平行存储。判据「该关注点归谁」:玩法元层进度归项目存档(如 MergeMetaSave),引擎级设置归框架约定——两者不强行合并(2026-06,settings 复用 TEngine.Constant.Setting.MusicMuted/SoundMuted + Utility.PlayerPrefs + ProcedureLaunch.InitSoundSettings,对比 18 player-info 并入 MergeMetaSave 先例)。注意框架键可能是反向语义(muted vs on),映射时取反并在键映射表逐档代入验证,与启动读取侧(!GetBool)对齐
