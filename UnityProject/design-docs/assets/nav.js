/* ============================================================
   Block Blast 策划文档 - 导航 + 运行时 Markdown 渲染(单一信息源)
   职责:① 左侧文档树(按 hash 高亮)② 首页卡片区
        ③ hash 路由:fetch <slug>.md → marked 渲染 → mermaid 围栏 → 本页目录(扫标题自动生成 + 滚动高亮)。
   hash 约定:#<slug> 跳文档;#<slug>::<章节id> 跳文档内章节(:: 避开与路由 hash 冲突)。
   新增文档:只改下方 GROUPS 一处(href 仍写 NN-xxx.html,slug 由代码去 .html 派生)+ 建同名 NN-xxx.md。
   须经本地服务器打开(双击 serve.bat);file:// 下 fetch 被浏览器 CORS 拦截。
   ============================================================ */
(function () {
  // group.side = 侧边栏分组名;group.card = 首页区块标题(null 表示不在首页卡片区出现)
  // doc.href / doc.side(侧边栏标签)/ doc.tag·title·desc(首页卡片,desc 可含 HTML)
  const GROUPS = [
    { side: '总览', card: null, docs: [
      { href: 'index.html', side: '文档库首页' },
    ]},
    { side: '现状分析', card: '现状分析', docs: [
      { href: '01-gameplay-overview.html', side: '01 · 玩法总览', tag: '现状 · 核心', title: '01 · 玩法总览',
        desc: '经典核心机制现状:核心循环、得分规则、方块库、动态难度系统。<b>注:经典已被融合吸收为底层引擎(见 29),不再是独立入口。</b>' },
      { href: '02-dynamic-difficulty.html', side: '02 · 动态难度拆解', tag: '现状 · 数值', title: '02 · 动态难度拆解',
        desc: '会读心的发牌系统:dynamicWeight 橡皮筋、8 种算法、136 行权重表真实数据。' },
    ]},
    { side: '切片设计', card: '切片设计(活跃)', docs: [
      { href: '09-merge-order-energy.html', side: '09 · 合成订单切片', tag: '切片 · 核心循环', title: '09 · 元素合成 + 订单 + 体力',
        desc: '三系统叠成自持循环:体力→落子→消除→元素→合成→订单交付→奖励。含数值风险方案与挂接点。元素产出由得分驱动(详见 10)。' },
      { href: '10-score-element-rm-collect.html', side: '10 · 得分驱动元素', tag: '切片 · 数值模型', title: '10 · 得分驱动元素生成',
        desc: '合成订单切片的元素产出由消除得分驱动数量:消得越狠携带越多。含映射公式逐档代入、类型选择、配置旋钮、挂接点与验收点。' },
      { href: '11-core-loop-completion.html', side: '11 · 核心玩法补全', tag: '设计 · 核心补全', title: '11 · 核心玩法补全',
        desc: '把四份原始稿(方块/订单/宝箱/女神)的漏洞补成完整自洽设计:并发模型、连消/多消/全清结算、体力、术语统一、图案经济、智能生成仲裁、宝箱、女神。含验收点与已拍板决策记录(2026-06-12 用户确认)。<b>合成订单完整设计,经典融合吸收进此玩法(顶层裁决见 29)。</b>' },
      { href: '12-tarot-blind-box.html', side: '12 · 神秘塔罗盲盒', tag: '设计 · 新玩法', title: '12 · 神秘塔罗盲盒',
        desc: '持有式即时开盒系统:连消/全消挑战与特殊订单交付攒盲盒,自选时机开出 Lv1–Lv3 图案 / 体力 / 订单缺口高阶物。含奖池权重、保底、解锁阈值逐档代入、dev 改动清单与 10 条验收点。接入现有 MergeOrderState / ClearSettlement。' },
      { href: '13-piety-temple-repair.html', side: '13 · 虔诚币 + 神庙修复', tag: '设计 · 长期主线', title: '13 · 虔诚币 + 神庙修复',
        desc: '长期主线成长链:订单交付产虔诚币 → 攒够修复 12 神庙大厅 → 修复产经验抬升守护者等级 → 升级解锁剧情章节(只做解锁标记)。加法式第二货币,不动现有灵力经济。含造价/等级曲线逐档代入、dev 改动清单与 12 条验收点。' },
      { href: '14-save-system.html', side: '14 · 跨会话存档', tag: '系统 · 持久化', title: '14 · 跨会话磁盘存档',
        desc: '把 MergeOrderState 元层进度(虔诚币/神庙/经验·守护者等级/灵力/盲盒/女神/订单完成数/今日祈愿)从单局尺度升为跨会话:启动加载、元变更后落盘、退出兜底。复用现有 Persistence 接缝;序列化层同步可单测、磁盘 IO 走 UniTask 异步外壳。含 version 迁移、缺字段保底、跨天重置、与悔棋快照分层、dev 改动清单与 14 条验收点。兑现设计 13 §七 O3。' },
      { href: '29-gameplay-fusion.html', side: '29 · 玩法融合', tag: '设计 · 顶层裁决', title: '29 · 玩法融合 · 经典吸收进合成订单',
        desc: '把两个并存入口(经典无尽 01 / 合成订单 11)融成一套:经典核心完全吸收为底层引擎(DDA/计分/方块库/连消钩子下沉),合成订单的体力/合成/订单/宝箱/女神/神庙是完整经济,移除纯无尽独立入口、保留体力预算。核心是 8 冲突点逐条裁决(入口/结束条件/计分语义/发牌/连消钩子点亮/方块库/存档/DDA 适配),每条给经典现状×合成订单现状×融合后取定+落点。基于代码现状(已远超旧设计稿快照:ClearSettlement/HandGenerationArbiter 已落地)逐条核实,标注现状/本篇目标。含经典保留(6项)/被覆盖移除(3项)清单、代码层融合落点(主菜单单入口/两窗合一/三隐患:Combo·Score 角色归一·dynamicWeight 跨模式残留·DDA 无分压适配/存档合并)、四项范围开关默认+备选、拍板记录(2026-06-16)与验收点。仅策划阶段不改码。' },
    ]},
    { side: '系统底层', card: '系统底层(xlsx 批次)', docs: [
      { href: '15-numeric-system.html', side: '15 · 数值底层系统', tag: '系统 · 配置化数值', title: '15 · 数值底层系统',
        desc: '把散在 MergeOrderState 的 ad-hoc 货币(灵力/虔诚币/经验/体力)统一收进 Luban 配置化货币注册表:按 num_id 查名称文本/图标/类型/品质;再提供全局显示格式化(0–999 / 999.9K / 999.9M,一位小数截断)与可复用数值显示 helper。加法式只建框架+配置+格式化+查询,不重构现有货币字段。含 schema 字段表、num_type 枚举与样例、边界逐档代入、dev 改动清单与 18 条验收点。xlsx 系统底层批次第一刀。' },
      { href: '16-item-system.html', side: '16 · 道具底层系统', tag: '系统 · 配置化道具', title: '16 · 道具底层系统',
        desc: '用 Luban 道具表登记每件道具的名/描述/图标/品质(1–6)/类型/使用效果/叠放;运行期按 Id 查。实现礼包开启(随机按 rate 权重抽、自选返回候选列表),产出经 UseEffect 落既有系统(货币→数值/MergeOrderState、图案→MergeElement);提供基础背包容器(叠加上限 999→999+、不可叠占格、容量 100)。加法式不重构既有系统。新建独立表(不扩展模板 TbItem)。含 schema 字段表、品质/类型枚举、礼包权重逐档、礼包开启时序图、dev 改动清单与 24 条验收点。限时/邮件补偿/UI 跳转字段进表逻辑 stub。xlsx 系统底层批次第二刀。' },
      { href: '17-reward-display.html', side: '17 · 通用奖励展示', tag: '系统 · 奖励展示归一', title: '17 · 通用奖励展示',
        desc: '把三种异构奖励产出(道具 GrantPayload / 盲盒 ChestReward / 数值裸 num+数量)归一成统一展示结构 RewardView(图标资源名 + 名称文本 id + 数量文本 + 品质色 + 类型角标),让任何 UI(开箱三选一、礼包开启、订单交付)用同一套渲染。加法式纯逻辑 helper + POCO,只读不写既有产出/发奖路径。定一份权威 6 档品质色收编旧 4 档。含各源转换、归一时序图、dev 改动清单与 18 条纯逻辑验收点。真实 Sprite 加载/UI 投放本轮不做(只给图标资源名 + Widget 骨架)。xlsx 系统底层批次第三刀。' },
      { href: '18-player-info.html', side: '18 · 玩家信息系统', tag: '系统 · 玩家信息', title: '18 · 玩家信息系统(数据逻辑层)',
        desc: '玩家个人信息系统的数据逻辑层:玩家信息数据模型(id/名字/等级/经验/当前头像·框/已解锁集合)跨会话持久化,加名字生成器(Player+6随机字符)、改名逻辑(首免/配置价/钻石扣费尝试/屏蔽字匹配)、头像框解锁判定(等级条件→三态:佩戴/已解锁/未解锁)、id 复制剪贴板工具。新建 Luban 头像&框表(id/类型/图片/解锁文字/解锁条件)。玩家等级是独立第三进度线(不复用守护者经验);持久化并入既有 MergeMetaSave。加法式不重构既有系统。含 schema 字段表、等级曲线逐档代入、改名时序图、dev 改动清单与 30 条验收点。UI 窗口/三态网格/经验槽→表现层延后(需美术)。xlsx 系统底层批次第四刀。' },
      { href: '19-settings-system.html', side: '19 · 通用设置系统', tag: '系统 · 通用设置', title: '19 · 通用设置系统(数据逻辑层)',
        desc: '常规游戏设置界面的数据逻辑层:音频设置(音乐开/关 + 音效开/关,默认全开)数据模型 + 本地持久化,加两个信息 getter(版本号 Application.version / 用户 ID 复用玩家信息 PlayerInfo.Id)。关键是复用框架既有设置约定——直写 TEngine.Constant.Setting.MusicMuted/SoundMuted 键(经 Utility.PlayerPrefs),muted↔on 取反映射,故启动流程 ProcedureLaunch.InitSoundSettings 零改动即「下次登录用本地配置」;切换经可注入 sink 推给 GameModule.Audio.MusicEnable/SoundEnable,经 ISettingsStore 接缝隔离 PlayerPrefs 使往返可单测。加法式不另造存储栈、不改框架代码。含键映射逐档代入表、切换时序图、dev 改动清单与 17 条验收点。设置界面 UI/各跳转按钮(客服/协议网址/兑换码/新手关/快捷登录)→表现层延后(需美术 icon),离线去变现:快捷登录不做、协议隐私存占位 URL、客服 stub。xlsx 系统底层批次第五刀。' },
      { href: '20-redeem-code-system.html', side: '20 · 兑换码 · 客户端侧', tag: '系统 · 兑换码客户端侧', title: '20 · 兑换码系统(客户端侧职责)',
        desc: '兑换码系统的客户端侧职责:输入采集数据层、发起校验请求、接收服务端裁决、按裁决经 16 道具系统既有落点本地发奖、经 17 归一展示结果。<b>裁决权威已上移服务端(见 30)</b>——码是否有效/此账号是否兑过/全局限量/能换什么全由服务端裁定,客户端不再持本地码表、不再做本地校验与去重权威(原本地配置校验器/本地去重集合已退出权威路径)。结果码六类(成功/码无效/已兑过/已过期/全局限量已满/服务不可用)语义保留,服务不可用时不本地放行(否则断网绕过服务端保证)。发奖复用道具系统「道具 id × 数量」落点、不另造;结果文案 id 占位(同 num/item/reward/settings)。输入/结果弹窗 UI→表现层延后(需美术)。本篇与 30 配套:30 为服务端权威 + 协议契约,本篇为客户端侧落地与展示。' },
      { href: '21-mail-system.html', side: '21 · 通用邮件系统', tag: '系统 · 通用邮件', title: '21 · 通用邮件系统(数据逻辑层 + 服务器/运营接缝)',
        desc: '命名空间 GameLogic.Mail 的收件箱数据逻辑层:邮件模型(发件人/时间/标题/内容/奖励附件/已读/已领取)+ MailboxService(收件 API IMailService.Send / 列表已读>未读+时间排序 / 标记已读 / 领取单封+一键 / 删除已读 / 自动清理超N+过期 / 红点)。两道接缝:① 对外收件 API IMailService.Send 供排行榜结算·活动回收·系统补偿调用(即 spec 的「留邮件调用接口」,本轮真做本地实现);② 服务器/运营接缝 IMailSource(运营推送来源)已接真实 RPC(见设计 32):运营邮件权威存服务端,客户端经联网会话拉应收+未过期列表,断服降级空载;区服离线视单一本地区服。领奖裁决权威已上移服务端(设计 32 客户端段:发领取请求→服务端原子防重+按库 id 抽奖+服务端时钟判过期→仅成功时客户端按服务端裁定的奖励本地落地复用 16,断服不本地放行),设计 21 本地抽奖/本地防重退出权威路径;领后用 17 RewardView 展示。持久化复用 Persistence.Provider 专用键 Mail.Inbox(JsonUtility 序列化收件箱,脏数据产合法空集合不抛),时钟注入 NowProvider 使有效期/保留/清理可单测。新建 Luban 邮件模板表 + 全局配置(maxCount 100/retainDays 30)。加法式不改框架、不另造发奖/存储栈。含 schema 字段表、状态机图、收件+领取时序图、dev 改动清单与 25 条验收点。邮件界面/详情/红点显示/icon→表现层延后(需美术)。xlsx 系统底层批次第七刀。' },
      { href: '22-rank-system.html', side: '22 · 排行榜底层系统', tag: '系统 · 排行榜底层', title: '22 · 排行榜底层系统(数据逻辑层 + 服务器接缝)',
        desc: '命名空间 GameLogic.Rank 的排名数据逻辑层:一张配置表控制所有榜(spec「统一用一个表格控制所有排行榜」),同 id 多行聚合成 RankDef(榜级字段)+ 名次奖励档 RankRewardTier。RankService 提供查榜(取前 N、查自己名次)、排序与并列(分数降序 + 同分按入榜时间升序,顺序名次)、结算编排(valid_type 四档:0无结算/1开服X天/2指定时间/3周循环星期X,IsSettleDue 注入时钟判到点 + 幂等防重复结)、每日/点赞奖跨天领取、红点 getter。两道接缝:① 排名数据源 IRankSource——离线 LocalRankSource(本机成绩 + 配置陪榜,可跑可测排序出名次)/ 远程 RemoteRankSource(已接真实 RPC,服务端权威排序、客户端短路本地排序,断服回退本地源,见设计 31);② 结算发奖不另造,直接调邮件系统 21 IMailService.Send(spec mail 字段=邮件id,结算奖励写进邮件待领),奖励内容复用 16 礼包随机库 id。排名层不碰 MergeOrderState/ItemGrant。持久化复用 Persistence.Provider 键 Rank.Progress(只存本机最佳分/上次结算/已结标记/每日点赞领取日期,他人成绩不进盘)。新建 Luban 排行榜表。加法式不改框架、不另造发奖/存储栈,邮件 21 零改动。含 schema 字段表、分层结构图、结算时序图、结算时机四档逐档代入、dev 改动清单与 26 条验收点。排行榜界面/列表/点赞按钮/头像/icon→表现层延后(需美术);真实全服榜远程源已接真实 RPC(见设计 31)。xlsx 系统底层批次第八刀。' },
    ]},
    { side: '全栈 / 服务端', card: '全栈 / 服务端', docs: [
      { href: '30-redeem-code-server.html', side: '30 · 兑换码服务端化', tag: '全栈 · 服务端权威', title: '30 · 兑换码服务端化(服务端权威 + 前后端协议契约)',
        desc: '本项目第一个全栈特性:把兑换码的裁决权威从客户端搬到服务端(Fantasy.Net)。服务端成唯一权威——查码有效性、按设备账号防跨会话/跨设备重兑、守全局限量(设计 20 离线做不到的两件事)、裁定奖励额度;客户端只采集输入、发请求、按裁决本地发奖(复用 16)+ 展示(17)。关键决策:① 不做离线兜底校验——服务不可用时兑换不发生(返「服务暂不可用」、码保持可兑),否则玩家断网即可绕过全部服务端保证;② 码表服务端权威、客户端不持第二份(从根消除双份漂移);③ 奖励回「道具 id × 数量」客户端解析展示(同邮件 21/排行榜 22 范式);④ 身份从会话已认证设备账号取、客户端不自报账号(防冒充)。含前后端职责切分表、行为级协议契约(请求/响应 code-free 语义,交服务端段定 proto)、整局走查每机制崩法+对策(并发双发/超发原子写、中途丢奖待发放重放、断服不本地放行)、服务端/客户端/联调三段验收(SV12+CV9+E4)、待拍板8档与风险表。设计 20 同步改写为客户端侧职责(本地校验/去重退出权威路径)。仅策划阶段,不写代码符号。' },
      { href: '31-rank-server.html', side: '31 · 排行榜服务端化', tag: '全栈 · 全服榜数据源', title: '31 · 排行榜上后端(服务端权威全服榜数据源 + 前后端协议契约)',
        desc: '第二个全栈特性:把排行榜的全服榜数据搬到服务端(Fantasy.Net),兑现设计 22 留的远程数据源接缝(原占位返空)。服务端成全服榜唯一权威——权威存储全服分数、维护排序(分数降序+同分入榜时间升序)、计算名次,对客户端提供两件事:上报一次成绩、查某榜前 N 名+自己名次。本增量只做数据源(上报+查询),结算发奖(设计 22 §3.4-3.5,依赖邮件 21)留后续。关键决策:① 同账号重复上报取最优(保留较高分,与设计 22「最佳成绩」语义一致),「比更优+写入」须原子条件写防并发低分覆盖高分;② 身份从会话已认证设备账号取、客户端不自报(防顶替/冒充刷分,沿用设计 30);③ 排序+名次在服务端(客户端无全服数据),客户端只显示;④ 服务不可用回退设计 22 本地源(本机+陪榜)、不阻断玩法、不伪造全服名次——与兑换码「不本地放行」区别在无超发风险;⑤ 榜定义配置(入榜要求/上限/排序规则)服务端权威、与客户端 rank.xlsx 同源导出。承认固有限制:客户端算分可被改、服务端守存储/排序/名次/防顶替权威而非「分真不真」(反作弊另开)。含前后端职责切分表、上报/查榜行为级协议契约(code-free,交服务端段定 proto)、整局走查每机制崩法+对策(并发取最优原子写/大榜性能取前N/身份防冒充)、与设计22远程源接缝关系(客户端段远程源短路本地排序)、服务端13条+客户端契约5条+联调4条验收、待拍板8档与风险表。客户端段(远程源切真实RPC)后续另增量。真往返写库依赖 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '32-mail-server.html', side: '32 · 邮件服务端化', tag: '全栈 · 运营来源 + 领奖权威', title: '32 · 邮件上后端(服务端权威运营来源 + 领奖裁决 + 前后端协议契约)',
        desc: '第三个全栈特性:把邮件的运营推送来源 + 领奖裁决权威搬到服务端(Fantasy.Net),兑现设计 21 留的运营来源接缝(原占位返空)+ 把客户端本地领奖(设计 21 §3.4.2 抽奖/落点/标已领)上移服务端。服务端成唯一权威——权威存运营邮件(模板/标题/正文 textId/附件奖励库 id/有效期),拉列表下发该账号应收的未过期邮件 + 每封领取态;领奖原子防重领(同邮件同账号一次)+ 服务端按库 id 抽礼包随机库裁定附件(客户端不申报)+ 服务端时钟判过期。本增量只做来源 + 领奖 + 过期 + 服务端进程内发奖入口(供未来排行榜结算发结算邮件复用),收件箱表现(排序/已读/删除/红点/超量清理)留客户端(设计 21)。关键决策:① 奖励附件 = 礼包随机库 id(spec),抽奖在服务端(giftrandom 同源导出 c,s 可加载)、返「道具 id × 数量」、客户端本地落地(同设计 30 范式);② 领奖防重 = 检查+抽奖+记录原子(抽奖在记录原子区内,非先抽后记),防并发双领;③ 身份从会话取、客户端不自报(防领他人邮件/拉他人收件箱);④ 领奖服务不可用不本地放行(发奖+防重,同兑换码;区别于排行榜查榜回退本地源无安全后果);⑤ 运营默认全服广播,系统定向走服务端进程内发奖入口(两路插入、一套领取);全局限量/定向投放/已读/删除服务端同步/批量领协议作开关默认不做、不强行塞满。承认固有限制:客户端本地进度可被改、服务端守领取裁决+运营来源权威;已读/删除留客户端跨设备不同步(开关)。含前后端职责切分表、拉列表/领取行为级协议契约(code-free,交服务端段定 proto)、整局走查每机制崩法+对策(并发双领原子/抽奖时机/过期服务端时钟/发奖入口非结算幂等)、与设计21关系前瞻(本增量 server 段先行不改设计21,客户端段再适配)、服务端14条+客户端契约7条+联调5条验收、待拍板11档与风险表。客户端段(运营来源切真实RPC+领奖走服务端校验+收件箱适配)后续另增量。真往返写库依赖 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '33-rank-settle-server.html', side: '33 · 排行榜结算服务端化', tag: '全栈 · 结算发奖 + 幂等', title: '33 · 排行榜结算发奖上后端(服务端结算编排 + 幂等)',
        desc: '第四个全栈特性,排行榜全栈三刀收口:把排行榜结算发奖搬服务端(Fantasy.Net),接上设计 31 留的「结算下一刀」(§七 O2)+ 设计 32 发奖入口声明的「调用方负责结算幂等」。结算编排行为基线 = 设计 22 §3.4-3.5,从「客户端单机算本人名次」升为「服务端遍历全服上榜账号逐名次档发奖」。本刀只落 server 段(结算编排 + 幂等存储),不动客户端。服务端新增:① 结算时机判定(设计 22 四档:0无结算/1开服X天/2指定时间/3周循环星期X,改用服务端时钟客户端改不了);② 结算编排(到点→读设计31全服分数按设计22 §3.3.2排序算各账号名次→按名次落档查奖励库id→经设计32服务端发奖入口给该账号投结算邮件,标题正文取邮件模板、附件库id用名次档reward覆盖);③ 结算幂等(每榜上次结算标记/时间MongoDB,判未结+写已结原子,并发/重启/同周期重复触发不重复发奖,幂等键=本周期结算时刻);④ 触发节律(进程内调度,定时tick/登录钩子由server-dev按Fantasy.Net定,幂等保证节律快慢不影响正确性)。无新客户端协议(结算服务端内部触发,非客户端RPC)。关键决策:① 结算遍历全服上榜账号逐账号发奖(非设计22客户端版只算本机一人);② 发奖必经设计32发奖入口不另造发奖路径;③ 结算时机用服务端时钟无客户端触发协议;④ 幂等粒度=榜级标记(半程崩溃/单封失败有窄重复/漏窗,严格每账号一次需账号级标记列O4后续);⑤ 每日/点赞奖(点赞依赖点赞数据无服务端来源)留后续O1。诚实边界:守名次计算/发奖/周期幂等权威,不守「分真不真」(反作弊另开)、榜级幂等有窄崩溃窗。结算服务端内部触发故server-test可直接真往返(起服+预置全服分数+触发结算→查已结标记+查投出的结算邮件落上榜账号收件箱),力争PASS非BLOCKED。含前后端职责切分表(结算几乎全服务端)、系统模型+结算编排顺序、四档时机服务端时钟、逐账号名次档发奖+边界、周期幂等原子、整局走查每机制崩法(同周期双发原子/半程崩溃/客户端催结算/只给一人/挂错奖)、与设计22关系前瞻(本刀不改设计22,客户端段本地结算退役)、服务端13条+客户端契约5条+联调4条验收、待拍板7档与风险表。客户端段(本地结算退役)后续另增量。真往返写库依赖 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
    ]},
    { side: '表现层 / 换皮', card: '表现层(美术换皮)', docs: [
      { href: '23-settings-window-art.html', side: '23 · 设置窗美术换皮', tag: '表现层 · 首个美术 UI', title: '23 · 设置窗美术换皮',
        desc: '本工程第一个美术驱动的 UI 窗口:把效果图 setting.png 换皮成可运行的 SettingsWindow,兑现设计 19 设置系统的表现层(遗留 #24)。核心是打通「切图 → 每屏一个 SpriteAtlas v2 → prefab 摆节点(m_ 前缀)→ FindChildComponent 绑定 → [Window] 加载 → SetSubSprite 取子图 → 热更」整条链路,作为后续所有界面换皮的模板。含切图导入落点 / 图集建法 / YooAsset 收集器寻址(SetSubSprite 跑通的前提) / 1080×1920 节点树逐节点命名 + 子图映射 / 窗口脚本生命周期 / 每个按钮的实做vs占位分流表。基础设施决定:新建 GameContext 运行期上下文单例统一持有 SettingsService 等无主数据(影响后续 player-info/item/mail/rank)。数据层只调用不重写。验收拆「逻辑可单测」与「需 Play/人眼对位」两档。' },
      { href: '25-player-info-window-art.html', side: '25 · 个人信息窗美术换皮', tag: '表现层 · 玩家信息 UI', title: '25 · 个人信息窗美术换皮',
        desc: '塔罗 UI 换皮自治线第二屏:把效果图 个人信息.png 换皮成可运行的 PlayerInfoWindow,兑现设计 18 玩家信息系统的表现层(遗留 #22)。纯 UI 补完——数据逻辑层(设计 18)已交付,本屏只调用 + 接线,全套基础设施复用设计 23 设置窗范式([Window(Top,false)] 弹窗 + 半透明遮罩 + GameContext 持有数据服务 + FindChildComponent + m_ 前缀 + SetSubSprite,不重造)。本屏无专属切图,复用 Sheet_settings 精灵表拼面板/标题板/确定钮/关闭钮;缺的圆头像框/编辑铅笔/下拉箭头占位 + TODO。把 GameContext 从只持有 Settings 扩成也持有 PlayerInfo(兑现设计 23 §五 player-info 挂入预告)。改名贯通数据层 PlayerRenameService.TryRename(RenameResult 四拒因分支提示)。关键决策:效果图的「生日 + 3 下拉」数据层无字段→UI 占位不入存档(决策 D2);头像三态网格/等级槽/id 复制属设计 18 完整界面元素、效果图本屏未画→后续屏。含效果图拆解 + 节点树逐节点命名 + GameContext 扩持有方案 + 控件分流表 + 验收拆「EditMode 可单测」与「需 Play/人眼对位」两档。' },
      { href: '26-settlement-window-art.html', side: '26 · 结算窗美术换皮', tag: '表现层 · 结算窗 UI', title: '26 · 结算窗美术换皮',
        desc: '塔罗 UI 换皮自治线第三屏:把效果图 游戏结束.png / 恭喜通关.png 换皮成两个已在运行的 code-built 结算窗 GameOverWindow / MergeOrderWinWindow。与前两屏本质差异——这两个窗 UI 早已存在且在跑(被 Classic / 合成订单 / 通关三条路径调用),不是从零造而是给在跑窗口换贴图。故取路 A 轻量换皮(保留 UGuiFactory 结构 + 结算逻辑,对返回的 Image/out bgImage 链 SetSubSprite 换木板/按钮子图)而非路 B prefab 重构(重写在跑窗口风险高)。零回归抬为硬约束:UserData 解析/结算计算/三处调用点传参/按钮回调目标一律保留不动。复用 Sheet_settings 精灵表,无新资源/无新文件。关键决策:效果图的三图标(星/心/草各60)+太阳奖励格既无切图又无数据源(BlockGameState 只有 Score/HighScore/Combo)→装饰占位不捏造统计字段(D1);分数沿用 Text(digits 未打表,位图数字为可选增强 B2);通关窗按钮回调是重开同局,「下一关」文案语义误导→默认「再来一局」(B3)。结算窗无 X/无遮罩关窗(强制二选一离开)换皮保持。含双图拆解 + 路A/B取舍 + 逐节点保留/换皮标注 + 触发回调结构图 + 验收拆「逻辑回归/编译 EditMode 硬验收」与「视觉对位/真机 Play」两档。' },
      { href: '27-tarot-mode-hud-art.html', side: '27 · 主玩法 HUD 美术换皮', tag: '表现层 · 主玩法 HUD（centerpiece）', title: '27 · 主玩法 HUD 美术换皮',
        desc: '塔罗 UI 换皮自治线第四屏(centerpiece,风险最高):把效果图 tarot_mode.png 换皮成已在运行的 Classic 主玩法窗 GameWindow 的静态 HUD 外壳。调查结论(读图+读代码核实):tarot_mode = Classic GameWindow 的再主题,非合成订单 MergeOrderWindow——效果图有大居中分数、无订单卡/合成区/体力条,与 GameWindow 逐项匹配。纯 UI 补完(只换静态视觉壳):棋盘渲染/落子拖拽/消除/补充/ghost/GameOver/分数滚动/BlockLayout 坐标映射全部保留不动,只改 BuildStaticUI 的背景/棋盘外框/格底/分数面板贴 Sheet_tarot_mode 木质子图(路 A 轻量换皮,对返回 Image 链 SetSubSprite),新增静态顶栏(头像+3资源条+齿轮)+ 3 动作按钮(更换/删除/提示)。首个用设计24打表工具产新精灵表的真实换皮屏(前三屏复用 Sheet_settings):16 张切图导入 ASCII 目录 tarot_mode/ → UIAtlasPacker.Pack 产 Sheet_tarot_mode.png → SetSubSprite 取图。关键决策:3 资源条多数无数据源(BlockGameState 只有 Score/HighScore/Combo)→占位,第1条接 HighScore、加号去变现不接购买(D1);更换/删除/提示是新玩法机制、数据层无→视觉占位+stub Log 待建,真机制本范围外(D5);头像无数据源→占位(D2);广告切图不投放(D3)。零回归是硬验收:玩法逻辑行/BlockLayout 坐标/数据层/退出回调全不动,Classic 整局可进可玩,合成订单窗不受影响。含 tarot_mode=哪个窗的逐项证据 + 资源条对照数据层 + 效果图拆解 + 换皮区/不碰区结构图 + 验收拆「回归/编译 EditMode 硬验收」与「视觉对位/真机 Play」两档。' },
      { href: '28-rank-window-art.html', side: '28 · 排行榜窗美术换皮', tag: '表现层 · 排行榜 UI（末屏 · art 受限）', title: '28 · 排行榜窗美术换皮',
        desc: '塔罗 UI 换皮自治线末屏(第五屏):把效果图 排行榜.png 换皮成可运行的 RankWindow,兑现设计 22 排行榜底层系统的表现层(遗留 #27)。纯 UI 补完——数据逻辑层(设计 22)已交付,本屏只调用 + 接线,全套基础设施复用设计 23/25 范式([Window(Top,false)] 弹窗 + 遮罩 + GameContext 持有数据服务 + FindChildComponent + m_ 前缀 + SetSubSprite,不重造)。本屏 art 受限:塔罗素材无排行榜专属切图、无榜行底/名次徽章(金银铜)/头像切图→复用 Sheet_settings 拼面板/标题/关闭/底部按钮,榜行底/徽章/头像占位(纯色条/块+字符)+ TODO,视觉是结构占位非高保真(已知限制,不判 FAIL)。把 GameContext 从持有 Settings+Player 扩成也持有 RankService(兑现设计 23 §五 rank 挂入末项预告;构造注入 LocalRankSource selfProvider 闭包+RankPersistence+IMailService,邮件服务来源 dev 按工程现状取)。榜单列表渲染读 RankService.GetBoard(rankId)→Entries 逐行(名次/名/分真实)+ Self/SelfRank/SelfScore 我的名次条;行实现 Widget+池/代码生成/固定槽三选一(dev grep 工程真实 UIWidget/池 API,别臆造)。点赞接 ClaimPraise(效果图无钮→默认省略留接线点,奖进邮箱)。关键决策:多榜页签(效果图无→默认单榜)/点赞按钮(效果图无→省略)/奖励预览(效果图无→不做)/结算触发(默认不在本屏起)均安全默认按效果图、留接线点;BLK 真阻塞:真实全服榜(无网络模块→远程返空、本机+陪榜)+ 点赞奖进邮箱可见(mail 表现层 #26 未做)。含效果图拆解 + 节点树逐节点命名 + GameContext 扩持有方案 + 查榜→渲染数据流结构图 + 控件分流表 + 验收拆「EditMode 可单测(持有/查榜贯通/点赞)」与「需 Play/人眼对位/列表渲染」两档。' },
    ]},
    { side: '代码 / 工具', card: '代码 / 工程', docs: [
      { href: '24-ui-atlas-packer.html', side: '24 · 散切图打表工具', tag: '工具 · Editor 打表', title: '24 · 散切图打表工具(Editor)',
        desc: '把一个散切图目录(AssetRaw/UIRaw/Atlas/<screen>/)一键合成一张 Multiple 模式精灵表 PNG,替代手工合表。读源 PNG → Texture2D.PackTextures 自动排布 → EncodeToPNG 写出 Sheet_<目录名>.png → TextureImporter 设 Sprite/Multiple/FullRect + 逐子图 SpriteMetaData(name=源文件名 / pivot 居中 / border 从源 TextureImporter.spriteBorder 继承 + 工具内可选覆盖 / rect 由排布定)→ SimulateBuild 重建模拟清单使 SetSubSprite 寻址生效。服务后续约 20 屏 UI 换皮打表(设计 23 范式的生产工具)。子图名=Sprite Editor 子精灵名=源文件名,故 LoadSubAssetsAsync<Sprite> 按文件名可取。不覆盖已存在文件、产出干净 N 子图(无早期自动切残留名表)。验收拆「工具行为 EditMode 可验(读回产出表断言 21 子图 + rect/pivot/border 对照现有 Sheet_settings.png)」与「运行期寻址 Play 验(count=21 + 按名取得到)」两档。' },
      { href: '07-blockblast-code-architecture.html', side: '07 · 代码架构剖析', tag: '工程 · 代码剖析', title: '07 · BlockBlast 代码架构剖析',
        desc: '离线还原版动态难度系统的 C# 实现:三层分层、调度数据流、位棋盘 + 蒙特卡洛评分器、CPU 热点与可调点。' },
      { href: '06-psd2ugui-componentize.html', side: '06 · PSD2UGUI 工具', tag: '工具 · 已落地', title: '06 · PSD2UGUI 组件化工具',
        desc: '扁平节点 → 组件调色板一键挂 UGUI 组件(控件自动补 targetGraphic),引用在 Inspector 连。已实现并编译验证。' },
    ]},
  ];

  // ---------- 路由工具 ----------
  function slugOf(href) { return href === 'index.html' ? '' : href.replace(/\.html$/, ''); }
  function esc(s) { return s; } // 数据为本库自有可信内容,直接作 HTML 注入
  // hash 约定:#<slug> 跳文档;#<slug>::<id> 跳文档内章节(:: 分隔,避开与路由 hash 冲突)
  function parseHash() {
    const raw = decodeURIComponent(location.hash.replace(/^#/, ''));
    const i = raw.indexOf('::');
    return i < 0
      ? { slug: raw.trim(), section: '' }
      : { slug: raw.slice(0, i).trim(), section: raw.slice(i + 2).trim() };
  }

  // ---------- ① 侧边栏文档树(建一次) ----------
  function buildSidebar() {
    const aside = document.getElementById('sidebar');
    if (!aside) return;
    let h = '<a class="side-brand" href="#"><span class="dot"></span>Block Blast 策划文档</a><nav>';
    for (const g of GROUPS) {
      h += '<div class="tree-group">' + g.side + '</div>';
      for (const d of g.docs) {
        const slug = slugOf(d.href);
        h += '<a class="tree-link" data-slug="' + slug + '" href="#' + slug + '">' + esc(d.side) + '</a>';
      }
    }
    h += '</nav>';
    aside.insertAdjacentHTML('afterbegin', h);
  }
  function setActiveSidebar(slug) {
    document.querySelectorAll('.tree-link').forEach(function (a) {
      a.classList.toggle('active', a.dataset.slug === slug);
    });
  }

  // ---------- ② 首页卡片区 ----------
  function buildCards() {
    const root = document.getElementById('cards-root');
    if (!root) return;
    let h = '';
    for (const g of GROUPS) {
      if (!g.card) continue;
      h += '<h2>' + g.card + '</h2><div class="cards">';
      for (const d of g.docs) {
        h += '<a class="card" href="#' + slugOf(d.href) + '">'
           + '<span class="tag">' + d.tag + '</span>'
           + '<h3>' + d.title + '</h3>'
           + '<p>' + d.desc + '</p></a>';
      }
      h += '</div>';
    }
    root.innerHTML = h;
  }

  // ---------- ③ 路由:fetch md → marked 渲染 → mermaid → 本页目录 ----------
  function elHome() { return document.getElementById('home'); }
  function elDoc() { return document.getElementById('doc-body'); }
  let loadedSlug = null;

  async function route() {
    const parsed = parseHash();
    setActiveSidebar(parsed.slug);
    if (!parsed.slug) { clearToc(); loadedSlug = null; showHome(); return; }
    if (parsed.slug !== loadedSlug) {
      clearToc();
      const ok = await showDoc(parsed.slug);
      loadedSlug = ok ? parsed.slug : null;
    }
    scrollToSection(parsed.section);
  }
  function showHome() {
    if (elHome()) elHome().style.display = '';
    if (elDoc()) elDoc().style.display = 'none';
    window.scrollTo(0, 0);
  }
  function scrollToSection(section) {
    if (!section) { window.scrollTo(0, 0); return; }
    const t = document.getElementById(section);
    if (t) t.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
  async function showDoc(slug) {
    const body = elDoc();
    if (!body) return false;
    if (elHome()) elHome().style.display = 'none';
    body.style.display = '';
    let md;
    try {
      const res = await fetch(slug + '.md', { cache: 'no-cache' });
      if (!res.ok) throw new Error('HTTP ' + res.status);
      md = await res.text();
    } catch (e) {
      body.innerHTML = '<h1>文档加载失败</h1><p>读取 <code>' + slug + '.md</code> 失败(' + e.message
        + ')。文档须经本地服务器打开(双击 <code>serve.bat</code>),不能 file:// 直接打开。</p>';
      return false;
    }
    body.innerHTML = window.marked
      ? marked.parse(md)
      : '<pre>' + md.replace(/[&<]/g, function (c) { return c === '&' ? '&amp;' : '&lt;'; }) + '</pre>';
    // 立项信息块由 GFM `> [!NOTE]` 渲染成无 id 的 .callout;按内容(首项=「立项信息」)补回 #intro,
    // 供本页目录顶项 + 跨文档 #slug::intro 锚点。按内容定位而非「首个 callout」:多数文档首个 callout 是「读前必看」warn,立项信息排第二
    if (!body.querySelector('#intro')) {
      body.querySelectorAll('.callout').forEach(function (c) {
        if (!document.getElementById('intro') && c.textContent.trim().indexOf('立项信息') === 0) c.id = 'intro';
      });
    }
    await renderMermaid(body);
    buildToc(body, slug);
    return true;
  }

  // ```mermaid 围栏 → 图(旧内联 <svg> 是裸 HTML,marked 已透传,不经此)
  let mermaidReady = false;
  async function renderMermaid(scope) {
    if (!window.mermaid) return;
    const blocks = scope.querySelectorAll('code.language-mermaid');
    if (!blocks.length) return;
    if (!mermaidReady) {
      // theme:'dark' 配 --bg:#15171e;fontFamily 对齐正文(渲染 CJK);themeVariables 让图贴文档调色板(共享层调一次,非每图加色)
      mermaid.initialize({
        startOnLoad: false, theme: 'dark', securityLevel: 'loose',
        fontFamily: '-apple-system,"Segoe UI","PingFang SC","Microsoft YaHei",sans-serif',
        themeVariables: {
          primaryColor: '#1e2230', primaryBorderColor: '#6c8cff', primaryTextColor: '#dde2f0',
          lineColor: '#8d96b5', secondaryColor: '#283256', tertiaryColor: '#1a1d27'
        }
      });
      mermaidReady = true;
    }
    blocks.forEach(function (c) {
      const div = document.createElement('div');
      div.className = 'mermaid';
      div.textContent = c.textContent;
      (c.closest('pre') || c).replaceWith(div);
    });
    try { await mermaid.run({ nodes: scope.querySelectorAll('.mermaid') }); } catch (e) { /* 单图失败不挡全文 */ }
  }

  // ---------- 本页目录(扫 #doc-body 的 h2/h3,无 id 补稳定 slug;#intro 置顶) ----------
  let tocObserver = null;
  function clearToc() {
    if (tocObserver) { tocObserver.disconnect(); tocObserver = null; }
    const old = document.querySelector('.page-toc');
    if (old) old.remove();
  }
  function slugify(text) {
    return text.trim().toLowerCase().replace(/[^\w一-龥]+/g, '-').replace(/^-+|-+$/g, '') || 'sec';
  }
  function buildToc(scope, slug) {
    const aside = document.getElementById('sidebar');
    if (!aside || !scope) return;
    const items = [];
    const intro = scope.querySelector('#intro');
    if (intro) {
      const b = intro.querySelector('b');
      items.push({ id: 'intro', label: b ? b.textContent : '立项信息', sub: false });
    }
    const used = {};
    scope.querySelectorAll('h2, h3').forEach(function (el) {
      if (!el.id) {
        const base = slugify(el.textContent);
        let id = base, n = 1;
        while (used[id] || document.getElementById(id)) { id = base + '-' + (n++); }
        el.id = id;
      }
      used[el.id] = 1;
      items.push({ id: el.id, label: el.textContent, sub: el.tagName === 'H3' });
    });
    if (!items.length) return;
    let h = '<nav class="page-toc"><div class="toc-title">本页目录</div>';
    items.forEach(function (it) {
      h += '<a class="toc-link' + (it.sub ? ' sub' : '') + '" href="#' + slug + '::' + it.id + '">' + it.label + '</a>';
    });
    h += '</nav>';
    aside.insertAdjacentHTML('beforeend', h);
    spyToc(items.map(function (it) { return it.id; }));
  }

  // 滚动高亮(.active 样式注入一次)
  function ensureTocStyle() {
    if (document.getElementById('toc-active-style')) return;
    const style = document.createElement('style');
    style.id = 'toc-active-style';
    style.textContent = '.toc-link.active{color:var(--accent);border-left-color:var(--accent);}';
    document.head.appendChild(style);
  }
  function spyToc(ids) {
    ensureTocStyle();
    const links = {};
    document.querySelectorAll('.page-toc .toc-link').forEach(function (a) {
      const href = a.getAttribute('href');
      const j = href.indexOf('::');
      links[j >= 0 ? href.slice(j + 2) : href.slice(1)] = a;
    });
    let current = null;
    tocObserver = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) {
          if (current) current.classList.remove('active');
          current = links[e.target.id];
          if (current) current.classList.add('active');
        }
      });
    }, { rootMargin: '0px 0px -75% 0px', threshold: 0 });
    ids.forEach(function (id) { const el = document.getElementById(id); if (el) tocObserver.observe(el); });
  }

  // marked 扩展:① GFM 提示块 `> [!NOTE]/[!WARNING]/[!TIP]` → callout div(marked 不原生支持)
  //            ② 标题尾随 `{#id}` → 显式 h2/h3 id(承重锚点:跨文档 #slug::id 链接 + 本页目录)
  // 向后兼容:裸 <div class="callout"> 原样透传按 CSS 渲染;无 {#id} 的标题仍走 buildToc 的 slugify 兜底
  function setupMarkedAlerts() {
    if (!window.marked || !marked.use) return;
    const map = { NOTE: 'note', WARNING: 'warn', WARN: 'warn', CAUTION: 'warn', TIP: 'good', GOOD: 'good', IMPORTANT: 'note' };
    marked.use({
      renderer: {
        blockquote(token) {
          const inner = this.parser.parse(token.tokens);
          const m = inner.match(/^\s*<p>\[!(\w+)\]/i);
          if (!m) return '<blockquote>' + inner + '</blockquote>\n';
          const cls = map[m[1].toUpperCase()] || 'note';
          const body = inner.replace(/^\s*<p>\[!\w+\][ \t]*(<br\s*\/?>)?\s*/i, '<p>').replace(/<p>\s*<\/p>/g, '');
          return '<div class="callout ' + cls + '">' + body + '</div>\n';
        },
        heading(token) {
          let text = this.parser.parseInline(token.tokens);
          const depth = token.depth;
          const m = text.match(/\s*\{#([\w-]+)\}\s*$/);
          let id = '';
          if (m) { id = m[1]; text = text.slice(0, m.index); }
          return '<h' + depth + (id ? ' id="' + id + '"' : '') + '>' + text + '</h' + depth + '>\n';
        }
      }
    });
  }

  // ---------- 启动 ----------
  setupMarkedAlerts();
  buildSidebar();
  buildCards();
  window.addEventListener('hashchange', route);
  route();
})();
