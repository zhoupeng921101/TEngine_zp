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
      { href: '35-account-server.html', side: '35 · 账号服务端(Tier 0 第 1 子单)', tag: '全栈 · 账号注册 + 登录会话', title: '35 · 账号服务端(注册 + 登录会话 · Tier 0 第 1 子单)',
        desc: 'Tier 0 真实账号体系的第 1 子单(地基):把客户端 UUID 自登录的「即用即建」自动注册流 + 登录会话挂账号身份地基化到服务端(Fantasy.Net):服务端新建 MongoDB accounts 集合作账号权威存储(主键 = 客户端 UUID,字段 = 首次注册时间 / 末次登录时间 / 状态),首连 → 服务端按 UUID 自动 upsert(无密码、无用户输入、玩家无感)→ 会话挂账号身份(沿用既有 GateAccountFlagComponent 单链:登录成功后会话上挂 account 标记,30/31/32/33 已有的「身份从会话取 Account.Name」全链路语义不变 → 现有四个全栈特性 account 字段无迁移、无数据兼容性问题)。关键不变量:account = UUID 字符串(沿用既有兑换码 / 邮件 / 排行榜 / 结算所用 key 语义),既有业务集合零迁移、零 schema 改;客户端工程零 diff(LoginUI 空壳保留、UUID 生成器 FantasyNetworkConfig.DefaultAccountName 保留、设置窗登出占位保留)。本子单只做注册 + 登录会话两件事,显式不做:OpenID / 邮箱 / 密码 / 第三方登录(留 Tier 3 上层接入)、跨设备恢复(Tier 3)、踢号/封号(运营后台后续刀)、登出协议(Tier 1+ 客户端段)、玩家信息查询协议(设计 18 是离线数据层,本子单不联动)。范围严守:既有 demo 的内存态 Account 实体复用、扩持久化层;无密码注册下未来升「绑定 OpenID/邮箱」是加字段非改主键(本稿§六 留接口余量声明)。承认固有限制:无密码无身份证 → 服务端无法区分「这 UUID 真是同一玩家」与「另一玩家拿了别人的 UUID 来仿冒」;Tier 0 在去变现 + 单设备一对一前提下可接受(诚实边界 §五 末)。含前后端职责切分表(几乎全服务端 + 客户端零改)、注册/登录行为级协议契约(基于既有 C2G_LoginGameRequest,语义升级为「认证 = 注册 + 登录二合一」)、首连/重连/重启幂等性走查+崩法(首连/UUID 复用/Mongo 不可达/并发同 UUID 双登)、服务端段验收 SV1-SV12(纯服务端 + 真往返写库)、与设计 18/19/30/31/32/33 关系前瞻(本子单零改既有稿:18 客户端数据层不动、19 登出占位不动、30/31/32/33 account 语义无变化)、待拍板 7 档与风险表。真往返写库依赖 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '37-player-attr-server.html', side: '37 · 玩家属性服务端(Tier 2 第 1 子单)', tag: '全栈 · 金币/钻石/体力服务端权威', title: '37 · 玩家属性服务端(金币 / 钻石 / 体力服务端权威 · Tier 2 第 1 子单)',
        desc: 'Tier 2 玩家属性权威体系第 1 子单(地基):把三类玩家属性(金币 / 钻石 / 体力)的权威值与变更裁决从客户端 PlayerPrefs + Player 模块运行期态搬服务端(Fantasy.Net),铺反作弊地基,合并去除「钻石消费校验恒返 true」的反作弊核心缺口。服务端新建 MongoDB players 集合作三属性权威存储(主键 = 账号 UUID,与 35 accounts 集合 _id 1:1 关联;字段 = 金币 / 钻石 / 体力余额三项 + 末次变更时间 + schema 版本),并提供两条对客户端可见的能力:① 登录后属性快照拉取(随登录响应 / 单独 RPC 由服务端段定),客户端登录即拥有当前属性视图;② 单一通用变更入口 PropertyChangeRequest——客户端只声明「相对变更」(类型 + 增量 delta + 原因 reason 字符串,例如「+100 金币 / -50 钻石 / -1 体力」),服务端原子校验余额下界(不许负) + 类型上界(运营配置,钻石 999999 / 体力 5 等) + 写库 + 推送 G2C_PropertyDeltaPush 回客户端;客户端只显示,绝不持权威值。反作弊红线:服务端永不接受客户端传入的绝对数值,只接受相对增量声明(消费类含 reason 便于后续 ledger 审计);三属性的读写都过服务端;PlayerNameGenerator.cs:152 钻石消费恒返 true 缺口由服务端校验天然合上(本刀不动 PlayerNameGenerator,客户端段下一刀改持有方式 + 走 PropertyChangeRequest 校验流)。首登在 35 RegisterOrLogin upsert 钩子内顺带 setOnInsert 写一份 players 初始记录(三属性初始值由运营配置,金币 0 / 钻石 0 / 体力 5 等,与 35 同事务模式)。客户端工程零 diff(PlayerPrefs 迁移 + Player 模块改持有方式 + 既有变更入口接线 = Tier 2 第 2 子单客户端段)。本刀显式不做:ledger 历史(每笔变更存表查询是 Tier 2+ 后续刀)、商店购买路径接通、活动 / 任务 / 排行榜结算的奖励发放路径接通(各业务系统的发奖刀按其各自范围接 PropertyChangeRequest 服务端进程内 API)、退款 / 回滚(Tier 2+)、其它属性(经验 / 等级 / 体力上限 / 体力恢复速率等非本三属性)。承认固有限制:本刀的「金币 / 钻石 / 体力余额三字段」是属性反作弊的最小可信地基,但未含「分数 / 进度」(反作弊另开,属服务端跑玩法仿真范畴,见 31 排行榜诚实边界)。含前后端职责切分表(几乎全服务端 + 客户端零改)、行为级协议契约(快照拉取 + 通用变更入口 + 主动推送三条 RPC,code-free)、整局走查崩法 5 类(首登初始化 / 余额不足 / 类型上界溢出 / 并发同账号双扣 / 服务不可用) + 诚实边界、与 35 accounts 关系(1:1 关联 + 首登 setOnInsert)、与 18 玩家信息关系(正交:18 = 离线本地玩家信息 id/名字/等级、本刀 = 服务端三属性权威)、与 30/31/32/33 关系(本刀正交,均不依赖三属性)、服务端段 SV1-SV14 + 待拍板 8 档 + 风险表。真往返写库依赖 MongoDB 不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '42-tarot-hud-player-attr-bind.html', side: '42 · tarot HUD 三属性绑定(Tier 2 第 3 子单 · client HUD)', tag: '全栈 · HUD 接续刀', title: '42 · tarot HUD 三属性绑定(Tier 2 第 3 子单 · client HUD)',
        desc: 'Tier 2 真实玩家属性权威体系客户端表现层收口刀:把 Classic 主玩法窗 GameWindow 顶栏 3 资源条从「占位 + 第 1 条接 HighScore + 加号 stub」改为绑 PlayerAttrService 的金币/钻石/体力实时余额,订阅服务端 G2C_PropertyDeltaPush 自动刷新(沿 PlayerInfoWindow 钻石面板范式)。零新增协议/零新增数据层/零改服务端——38 客户端数据层已建,本子单只动 GameWindow.BuildTopBar 三数字初值 + 1 处订阅 + 1 处销毁解绑 + 1 个分发方法。三属性 → 三槽映射:Coin/Diamond/Stamina 自左到右,沿用现有切图 gemstone/gemstone2/potion 占位图标(O1 待裁产品定专属图);IsReady=false 时显「—」非 0 不误导玩家(沿 PlayerInfoWindow 加载中态);加号点击行为不变(仍 Log 待建,去变现红线)。零回归是硬约束:主玩法窗在跑,玩法逻辑(Render/落子/消除/补/ghost/GameOver/OnUpdate)/BlockLayout 坐标/数据层/退出回调/HighScore 显示位/MergeOrderWindow 全不动,git diff 仅 GameWindow.cs。同步落点(同任务内):覆盖式重写 27 §三 D1/§5.3/§6/§10 D1 为「资源条绑 PlayerAttrService」+ 38 §一切分表 HUD 行 + §7.7 诚实边界为「HUD 全屏三属性已接续 = 设计 42」。验收三档:R 组(零回归 7 条,硬验收 + 静态核对)/W 组(EditMode 订阅刷新分发)/V+E 组(Play 三属性初值显示 + 推送实时刷新 + 真往返,Fantasy + MongoDB 不可达判 BLOCKED)。' },
      { href: '44-player-attr-ledger.html', side: '44 · 玩家属性 ledger(Tier 2 第 2 子单 · server)', tag: '全栈 · 三属性变更审计 ledger', title: '44 · 玩家属性 ledger 审计日志(Tier 2 第 2 子单 · server)',
        desc: 'Tier 2 玩家属性权威体系 server 段补完刀:在 37 已落的「players 集合权威 + 通用变更入口」之上新建 MongoDB player_attr_ledger 集合,把每一次三属性(金币/钻石/体力)的变动以追加式流水写一行(timestamp / account / kind / balanceBefore / balanceAfter / delta / source 枚举 / reasonRaw / schemaVersion),供客服查账 / 玩家自查 / 反作弊审计。本子单只写流水不读流水(查询 API + 客户端「我的流水」UI 留 Tier 2+ 客户端段后续刀)。核心架构决策:① 独立集合(否决内嵌进 players._id 数组,理由:MongoDB 16MB 单文档上限会被活跃玩家月级变更触上限 + 读 players 余额会附带读出整段流水放大十倍 + 按账号按时间范围扫的查询模式与内嵌数组的 $elemMatch 遍历不匹配);② ledger 写入是旁路追加,不进 37 FindOneAndUpdate 原子边界(沿 37 §3.4「单条原子命令」基线;ledger 写失败仅告警不回滚余额,沿「服务可用性 > 审计完整性」取舍,Tier 2+ 若需最终一致再加重试队列);③ 写入时机 = 37 「FindOneAndUpdate 成功」分支后、G2C_PropertyDeltaPush 推送之前(失败裁决分支全部不写 ledger,审计不污染);④ ledger 永不 update / delete(追加式 invariant,Code Review 必拦);⑤ source 枚举单一映射函数(reason 字符串 → source),业务系统不绕过自报 source(防新发奖路径漏登记),映射在服务端进程内完成、协议字段仍是字符串 reason(沿 boss 硬约束「不改 PropertyChangeRequest 协议签名」);⑥ source 枚举覆盖 5 个核心(ChangeNameSpend 改名扣钻 = 38 已落、MailClaim 邮件领奖 = 32 + Tier 2+ 接线刀、RedeemCode 兑换码 = 30 + Tier 2+ 接线刀、RankSettleReward 排行榜结算 = 33 + Tier 2+ 接线刀、ActivityReward 活动发奖 = 39 + 40 + 43 + Tier 2+ 接线刀)+ 4 个增强(GameplayConsume / ShopPurchase / AdminGrant / Refund 留 Tier 2+ 接入)+ Unknown 兜底;⑦ 索引 (account ASC, timestamp DESC) 复合 + (timestamp DESC) 单字段(后者便于未来挂 TTL),不加 source 索引(低基数低频运营查询走全表扫即可);⑧ timestamp 字段取应用端写库时刻(= 37 写 players 成功的时刻),非 Mongo insert 时刻(避免与 Mongo 写延迟混淆,ObjectId 内嵌 timestamp 留作 Mongo 端旁证);⑨ 字段不变量 balanceAfter = balanceBefore + delta + 非负 + kind/source 在已登记枚举内,Code Review 必核。客户端工程零 diff(无新协议 / 无新 RPC / 38 行为零变化,只是其改名扣钻通路多写一行 ledger)。承认固有限制:守每笔成功变更追加一行 + 字段不变量 + source 集中映射 + ledger 永不改写 + 索引让按账号查秒级响应 + 余额失败时不污染审计 + 协议签名零改;不守 ledger 与余额的严格一致性(Mongo 写 ledger 失败时余额仍变 + 告警)、不守客户端可查 ledger(留 Tier 2+)、不守客服后台 GM(运营经 mongo shell / Compass 直读)、不守跨进程 ledger 全局顺序、不守退款 / 回滚 / BI 聚合 / TTL 自动归档(Tier 2+)、不守业务侧 reason 字符串子分类规范(reasonRaw 保留原文)、不守分数 / 进度的 ledger(反作弊另开)。同任务内同步重写(sweep 闭合):37 §读前必看第 5 条「不做的事 - ledger 历史」改写指向 44、§3.1 schema 表「末次变更时间」字段加旁注、§3.5 末段加一句「44 ledger 旁路写也挂在此通路」、§六 6.1 表「ledger 历史」从「新建 player_ledger 集合...」覆盖式重写为「44 已交付,字段 / 索引 / source 枚举详该篇」+ 删旧 player_ledger 命名(改 44 落地的 player_attr_ledger 单源,避免双源命名漂移)、§六 6.2 接口余量表「ledger 历史」更新状态、§一切分表「ledger 历史流水查询」一行从「不做 Tier 2+」改写为「已交付写入侧」+ 新增「客户端拉 + 我的流水 UI 留 Tier 2+」一行、§5.8 不守列表 ledger 行重写、§7.2 BLOCKED 列表 ledger 行重写。验收 SV1-SV18(编译 / 索引 / source 枚举登记 / 改名扣钻 ledger 写入 / 余额不足不写 / 上界溢出不写 / 服务端进程内 API 触发各 source / Unknown 兜底 / 大小写敏感 / 字段不变量 / 写入时机 = 写库后推送前 + Mongo 抖动告警 / 并发同账号双扣 ledger 顺序按 ObjectId / 索引可用按账号查 / 永不 update delete / Mongo 不可达 / 客户端工程零 diff / 既有全栈零回归 / Code Review 重点核 10 项)。真往返写库依赖本机 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '45-player-attr-ledger-query.html', side: '45 · 玩家属性 ledger 查询协议(Tier 2 第 3 子单 · server 段 + 客户端协议生成物)', tag: '全栈 · ledger 查询通路 server 段', title: '45 · 玩家属性 ledger 查询协议(Tier 2 第 3 子单 · server 段 + 客户端协议生成物)',
        desc: 'Tier 2 玩家属性权威体系 server 段查询通路刀:兑现 44 §6.2 「客户端我的流水 UI + 拉流水 RPC」接口余量声明的协议契约 + 服务端 handler + 客户端协议生成物三件交付。新协议 C2G_QueryAttrLedger(kind 可选 / sinceTs 可选默认 0 / limit 必填,服务端钳制上限 100) + G2C_QueryAttrLedgerResponse(resultCode + entries[] + hasMore);handler 按 (account, timestamp DESC) 主索引取前 limit 条(命中 44 §3.2 ix_account_ts_desc),支持 kind 过滤 + sinceTs 滑动窗口(ts > sinceTs 增量同步语义);entry 行白名单 7 字段(timestamp / kind / balanceBefore / balanceAfter / delta / source 整数枚举 / reasonRaw),不暴露内部 ObjectId / SchemaVersion / account(沿 32 / 33 不暴露内部 id 范式 + conventions 不投机暴露未来用不上的接口)。错误码集 3 个(Success / InvalidRequest 含 kind 未知 / sinceTs 负数 / ServiceUnavailable 含 MongoDB 不可达)。客户端业务接入(RemoteAttrLedgerService 沿 22 RankService 远程源同范式) + 我的流水 UI 投放(沿 28 排行榜窗 art 受限范式)留 Tier 2+ 客户端段后续刀(本子单零业务接入、零 UI、仅协议生成物 cs 类)。核心架构决策:① 协议字段语义 sinceTs 是「ts > sinceTs」上界过滤增量同步,不是翻旧页 cursor(翻旧页留 Tier 2+ 扩 untilTs / offset / cursor 任一);② 身份从会话取(handler 忽略请求中任何 account 字段),协议字段层面不预留 account(防拉他人 ledger,沿 37 / 38 / 44 同范式);③ limit 钳制 [0, 100] 不报错(降级语义,防 DOS:limit=999999999 实际返 100 + hasMore=true);④ kind / sinceTs 非法值校验(kind 整数 0/1/2/3 之外返 InvalidRequest,sinceTs 负数返 InvalidRequest);⑤ handler 只读 ledger 集合(永不 InsertOne / UpdateOne / DeleteOne / FindOneAndUpdate / FindOneAndDelete,沿 44 §5.4 追加式 invariant,Code Review 必拦);⑥ MongoDB 异常 catch 返 ServiceUnavailable 不抛(沿 32 / 44 不可达不抛口径);⑦ 响应字段裁剪 7 字段白名单(Code Review 拦其他字段);⑧ source 整数 → 文本映射留客户端段下一刀做(协议层只传整数,文本由 i18n 或硬编码 switch);⑨ 服务端段 + 客户端协议生成物分两件交付,客户端业务接入与 UI 投放留下一刀(沿 32 邮件 server 段先行 + 客户端段后续同范式,5d0d28e4 邮件协议生成物 + d52dbdd6 排行榜协议生成物先例)。同任务内同步重写(sweep 闭合):① 44 §6.2 接口余量表「客户端我的流水 UI + 拉流水 RPC」演进列覆盖式重写为「45 已交付服务端段 + 协议生成物;客户端业务接入 + UI 投放留 Tier 2+ 客户端段后续刀」;② 44 §一 切分表「拉流水 / 查历史 / 我的流水 UI」归属列从「不做 · Tier 2+」覆盖式重写为「协议契约 + 服务端 handler + 客户端协议生成物 = 服务端 · 45 已交付」;③ 44 §5.4 不守列表「客户端可查 ledger」行覆盖式重写指向 45;④ 44 §7.2 BLOCKED 列表「客户端我的流水 UI / 拉流水 RPC」行覆盖式重写指向 45。验收 SV1-SV18(服务端编译 + 源生成器产物 / handler 注册 / Success 返空 / Success 返非空 / limit 钳制上限 100 / limit=0 返空 / kind 过滤生效 / kind 未知 InvalidRequest / sinceTs 滑动窗口 / sinceTs 负数 InvalidRequest / 身份从会话取 / MongoDB 不可达 ServiceUnavailable / 索引命中 IXSCAN ix_account_ts_desc / handler 仅只读 / 响应字段裁剪 7 字段 / 既有服务端段零回归 / hasMore 语义边界 / Code Review 重点核 10 项)+ CV1-CV3(客户端协议生成物存在 + 编译过 / git diff 仅 Assets/GameProto/ / 既有客户端段零回归)+ E1(真往返双端共识)。真往返查 ledger 类 SV 依赖本机 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '38-player-attr-client.html', side: '38 · 玩家属性客户端(Tier 2 第 2 子单)', tag: '全栈 · 钻石数据层首次实装 + 接服务端', title: '38 · 玩家属性客户端(钻石首次实装 + 接服务端 · Tier 2 第 2 子单)',
        desc: '承接 37 服务端段已 PASS 的 Tier 2 玩家属性账本,收口 Tier 2 真实玩家属性权威体系客户端段。审计结论(读源码核实):简报描述与现状有出入——客户端工程从未在 PlayerPrefs/Player 模块持有金币/钻石/体力作为元层属性账本(PlayerInfo 持 Id/Name/Avatar/Level/Exp/RenameCount 而无三属性;MergeMetaSave DTO 无三属性字段;PlayerPrefs 中无三属性 key);ItemGrant.cs:85 + PlayerRenameService.cs:48 注释明示「钻石 num_id=3 数值系统尚未实装专门字段,本轮不落,占位 cost => true 待钻石实装接真实扣减不返工」;真实占位行是 PlayerInfoWindow.cs:152 而非简报说的 PlayerNameGenerator.cs:152(后者整文件 31 行只是名字生成器);故本子单实质 = 「钻石(及金币/体力)作为玩家可见元层属性,首次为客户端建数据层 + 接服务端账本」而非「数据源迁移」,且 PlayerInfoWindow.cs:152 是「未实装占位」非「反作弊缺口」(简报口径错指,二者本质区别在于缺口可被绕过的前提是已有校验设计,占位则无校验机制存在)。本子单范围:① 新建命名空间 GameLogic.BlockBlast.Player 下三属性数据层(PlayerAttrService:本地视图 + 订阅 G2C_PropertyDeltaPush + 缓存当前态,登录时收 G2C_PropertyInitSnapshot 初始化);② GameContext 加 PlayerAttr 持有(同 36 设置 Player/Rank/Mail 范式);③ 单一真接线点 = PlayerInfoWindow.cs:152 改名扣钻 trySpendDiamond 改为「调 C2G_PropertyChangeRequest 等服务端响应」(同步等待返码,等价拦截器,失败 RenameReject.NotEnoughDiamond 提示)。客户端工程改动面 = HotFix 三处:GameContext.cs + PlayerInfoWindow.cs + 新建 GameLogic/BlockBlast/Player/PlayerAttrService.cs;Fantasy.Scripts 接 RPC stub(零业务逻辑)。关键决策:① 金币/体力玩法路径接入(本子单不接,本设计自治拍板「核心档/可砍档」二档分流,核心 = 钻石消费一个真接线点,可砍 = 金币/体力玩法接入;后者无既有玩法消费/产出路径——体力是 merge-order Demo 局内态非元层余额,金币无任何消费/产出路径,故砍掉「可砍档」核心循环仍成立,留 Tier 2+ 业务玩法刀按需接);② HUD 显示三属性余额(本子单 UI 表现层不动,Tier 2+ 表现层换皮按需投放,本子单只在 PlayerInfoWindow 钻石余额面板上显示当前钻石数支撑「钻石不足」分支用户验证可用);③ PlayerPrefs 三属性 key 退役/迁移(无可迁数据,自然解决);④ 占位 cost => true 改为同步等待 RPC 返码(避免 fire-and-forget 致改名先成功后扣钻);⑤ 与 14 跨会话存档正交(三属性账本在服务端 players 集合,14 仍持其玩法元层 9 字段);⑥ 与 18 玩家信息正交(18 = 名字/头像/等级在本地,本刀 = 三属性在服务端);⑦ 服务不可用(网络断/快照丢)= 钻石消费拒绝(同 30 兑换码不本地放行,关闭超发面)。承认固有限制:守钻石/金币/体力的服务端账本接入 + 改名扣钻通路打通;不守 HUD 全屏显示余额(表现层后续刀)、不守金币/体力玩法路径接入(待相关玩法刀)、不守快照/推送的客户端去抖与离线追平(沿 37 §5.7 推送丢失下次登录对齐口径)、不守 PlayerNameGenerator 本身(文件仅生成名,无关属性)。含五条边界 + 现状审计逐处证据(三属性零客户端持有 / 占位非缺口 / PlayerInfoWindow 真接线点 / 简报基线错指 4 项) + 决策表 D1-D8 + 改名扣钻同步等待时序图 + 整局走查崩法 6 类(改名前快照未到 / 改名时网络断 / 改名同时余额被推送变更 / 钻石未刷新 UI / 服务端拒后客户端冒进 / RPC 异常断连)+ 37 同步前瞻清单 2 条 + 18/25 设计稿同步条 2 项 + CV1-CV9 + E1-E3 真往返 + 待拍板 7 档 + 风险表。真往返写库依赖 MongoDB 不可达列 BLOCKED 非 FAIL,Tier 2 全栈联调本子单收口。仅策划阶段,不写代码符号。' },
      { href: '36-account-client.html', side: '36 · 账号客户端(Tier 0 第 2 子单)', tag: '全栈 · 客户端登出接线 + 链路审计', title: '36 · 账号客户端(登出接线 + 自动登录链路审计 · Tier 0 第 2 子单)',
        desc: '承接 35 服务端段已 PASS 的 Tier 0 账号地基,收口 Tier 0 真实账号体系客户端段。审计结论(读源码核实):自动登录链路已通(GameApp.StartGameLogic 第一行 FantasyNetwork.Boot → 连服 → 自动登录 account=SystemInfo.deviceUniqueIdentifier 派生 → 自动 C2M_InitComplete → 主菜单直接打开),LoginUI 真空壳全工程零 ShowUIAsync 调用是「玩家无感」的设计意图而非漏接,简报「PlayerPrefs UUID」表述与现状不符(UUID 来自 SystemInfo 不入 PlayerPrefs)。本子单唯一新增可观测 UX = 设置窗登出按钮接线(占位文案「离线版无账号系统」在 35 上线后过时):OnLogout 由 1 行 ShowPlaceholder 改为 3 行(反馈 + Shutdown + Boot),= 断当前会话 + 立即重新走自动登录(等价「重新登录到同一账号」,UUID 不变服务端走 update 末次登录时间)。客户端工程改动面 git diff 仅 SettingsWindow.cs 一文件,LoginUI / FantasyNetwork / FantasyNetworkConfig / GameApp 全部零改动。关键决策:① LoginUI 不上线(GameApp 启动到主菜单是自动链路,插入 LoginUI 阻断点要么动玩法主入口路径要么一闪而过,Tier 1+ 范围);② 登出 = Shutdown→Boot 顺次调(工程无「回登录前态」UX 设施新建超范围,自然意图 = 重新登录);③ 不引入 PlayerPrefs UUID(改派生路径 = 既有四特性 account 字段值漂移,违 35 守不变量);④ 登出反馈走 ShowPlaceholder 兜底文案改现状语义,不做二次确认弹窗(工程无组件,Tier 1+ 加);⑤ 19 设计稿 §一 #10 / §3.6 末段 / §七 O8 / §八风险表「快捷登录 = 不做(离线无账号系统)」全部过时(account 已上服务端),conventions §6 触发本任务内改写:「快捷登录」收窄为 Tier 3 OpenID/邮箱绑定才属此范畴(仍 = 不做)、「设置窗登出 = 实做」标记。诚实边界:守自动登录链路完整 / account 主键值不漂移 / 既有四特性 E1 真往返不破 / server 段 35 协同;不守回登录前态 UI(Tier 1+)/ 账号切换 / 二次确认 / 登出后清玩法存档(登出 ≠ 切号)。含五条边界 + 现状审计逐行(自动登录链路 6 步 / LoginUI 真空壳 11 行 / UUID 来源 / OnLogout 接线点 / 门面 Shutdown+Boot API)+ 决策表 D1-D6 + 登出时序图 + 整局走查崩法 5 类(普通登出 / 服务端不可达 / 连点 / 零回归 / UUID 漂移)+ 19 同步改写清单 5 条 + CV1-CV8 + E1-E3 真往返 + 待拍板 7 档 + 风险表。真往返写库依赖 MongoDB 不可达列 BLOCKED 非 FAIL,Tier 0 全栈联调本子单收口。' },
      { href: '43-activity-login-batch.html', side: '43 · Login 类活动批量扩档(Tier 4 第 3 子单)', tag: '全栈 · Login 节律多活动并存', title: '43 · Login 类活动批量扩档 · 共享登录节律(Tier 4 第 3 子单)',
        desc: 'Tier 4 活动系统第 3 子单 server 段:在已建的「登录后钩子 → 遍历 type=Login 活动 → 各自 counter+1 + 判达标 + 发奖」流程上批量加 2 套新 Login 类活动配置(累计 7 天大奖 OneShot + 周累计 5 天奖 Weekly),兑现设计 39 §3.5 旁注「未来若加累计登录 N 天等共享此节律」并验证「同一节律支撑多活动并存」的架构扩展性。审计后调查结论:简报「扩 ActivityProgressService」实质 = 兑现 + 验证(规范层 39 §3.5 已声明遍历语义),若 server-dev 第 1 子单实现已按 type=Login 全表遍历 → 零代码改、纯配置加行验证扩展;若实现是硬编码 if activityId==1 → 同任务内改为按 type 遍历(非新增,是兑现规范)。范围严守:本子单纯 server only(客户端工程零 diff)、不动 39 ActivityDef schema / activity_progress schema / 32 SendMailTo 签名 / §3.4 发奖编排 / §3.5 Login 触发节律;不引入 Cumulative/Schedule/Action 新节律实现(留 39 O3);不引入「连续登录 N 天」(需扩 schema 加「上次登录日期 + 中断重置」字段违守不变量,列 O3 后续刀)。服务端段新增:① activity.xlsx 加 2 行(activity_id=3 累计7天大奖 OneShot reward=5003、activity_id=4 周累计5天奖 Weekly target=5 reward=5004 mail_def=7004);② giftrandom.xlsx 加 2-3 行(5003 钻石100单项必中 + 5004 金币500/体力5 多项加权或砍单项必中);③ mail.xlsx 加 2 行(7003/7004 占位 textId,可砍为 mail_def=0 兜底);④ 服务端 AuthoritativeDefs 注册;⑤ 登录钩子按 type=Login 遍历语义验证(若现状已正确则零代码改)。整局走查 8 类崩法(登录钩子硬编码单 id 致新活动被忽略 / Weekly 周期键 ISO 周 vs 自然周边界 / Weekly counter 跨周不清零 / 多活动遍历内单活动 SendMailTo 抛异常中断后续 / OneShot 永发幂等 / 同账号并发两连接同时第 7 次登录 / 客户端本地时钟催 Weekly 重置 / 邮件模板 textId 未配)+ 各类对策。同任务内同步重写:① 39 §3.5 节律表 Login 行状态从「实做(每日登录奖示例)」更新为「已兑现:多活动并存(Tier 4 累计 4 套)」+ 旁注末尾加「Daily/OneShot/Weekly 三种 cycle 在同一钩子内各自独立处理」;② 39 §七 O1 状态从「不定 9 套清单」更新为「Tier 4 累计已交付 4 套」+ O2 状态从「实做 Login」更新为「已兑现:多活动并存」。验收主验:SV1-SV2 4 张表加行 Luban 双端同源导出;SV3 登录钩子遍历 4 套(activity_progress 集合该账号下 4 个文档各自正确写入);SV4 Daily 周期键(活动 1 回归);SV5 OneShot 周期键(活动 2/3 回归 + 新增,第 7 次同次登录玩家邮箱新增 2 封);SV6 Weekly 周期键 + 跨周 counter 清零(活动 4 新增,跨周一 0:00 + 同账号第 1 次登录 counter=1<5 不发奖);SV7 多活动并发原子幂等(同账号并发两连接同时第 7 次登录每活动最多 1 次抢占,无双发);SV8 mail_def 加行 / 兜底两种走法等价;SV9 真往返(4 张表加行 + AuthoritativeDefs 注册 + 累计登录 1-8 次实际累计 4 封活动邮件);SV10 跨日 / 跨周时钟独立;SV11 第 1 / 第 2 子单 PASS 行为零回归(活动 1 / 2 既有行为完全不变 + activity_progress schema 零字段加 + activity.xlsx schema 零字段加 + 32 SendMailTo 签名零改 + 既有六全栈 + 35/36/37/38 + 39/40 PASS 不破);SV12 Code Review(登录钩子按 type 全表遍历 + 多活动遍历内单活动 SendMailTo 失败不中断后续 + Weekly 周期键算 + Weekly 跨周 counter 清零 + 配置引用一致性 + 无新协议 / 集合 / schema 改);CV1 客户端工程零 diff;E1 全栈真往返(累计登录 7 天 → 服务端发 4 封活动邮件 → 客户端拉邮件 + 领取 → 16 货币 useEffect=1 → 37 PropertyChangeRequest → 38 G2C_PropertyDeltaPush → 42 tarot HUD 见钻石 / 金币 / 体力变化);E2 Weekly 跨会话 + 跨周一 0:00 重置;E3 全栈零回归。真往返写库依赖本机 MongoDB,不可达列 BLOCKED 非 FAIL;Tier 4 第 3 子单交付后累计 4 套 Login 活动并存验证架构扩展能力,后续运营加新 Login 活动只需配 1 行(零代码改)。仅策划阶段,不写代码符号。' },
      { href: '41-event-unlock-client.html', side: '41 · EVENT 头像解锁客户端段(Tier 4 第 2 子单 client)', tag: '全栈 · EVENT 解锁收口', title: '41 · EVENT 头像解锁客户端段(Tier 4 第 2 子单 client)',
        desc: '承接 40 server 段已 PASS(Fantasy bafed768 + 设计稿 d3e3b4fd)的 EVENT 头像解锁通路,收口 Tier 4 第 2 子单。审计结论(读源码 + 读表核实):① Fantasy 服务端注释明示「服务端工程无 Luban,只守(道具 id=30101, 数量=1)抵达邮件附件;客户端 luban itemdef.xlsx 须含 id=30101 行 — 本子单 server 段不交付,客户端段下一刀处理」(MailServiceComponentSystem.cs:90-92);② 客户端 ItemGrant.cs:70 的 switch(def.UseEffect) 五档(1/2/3/4/default)是天然扩展点,加 case 5 EVENT 即正交扩展;③ AvatarUnlockService.GrantUnlock(PlayerInfo, AvatarEntry) 已就位(File:64,头像/框由 AvatarEntry.Type 自动分流到 UnlockedAvatarIds/UnlockedFrameIds);④ AvatarConfigMgr.GetAvatar(int id) 已就位、按 id 取 AvatarEntry;⑤ avatar.xlsx id=3 avt_star (EVENT, unlock_param=9001 占位) 实存;⑥ 客户端三处领奖路径(MailboxService / RemoteMailService / RedeemService)都已对每个 reward (itemId, count) 调 ItemGrant.GrantOnAcquire,EVENT 道具一加入 itemdef.xlsx 即被三处自动通过同一解析路径吃下;⑦ 客户端 itemdef.xlsx 现有 30001-30008 八行无 30101 行、giftrandom.xlsx 现有 6001 礼包无 6101 行 — 这两行是本子单 client 段必加项(与 server 段 GiftPoolSeeds 6101→30101 共识对齐)。简报建议「在 16 UseEffect 加 EffectType=5 EVENT handler」措辞与现状有出入:UseEffect 字段是 int(非枚举,ItemDef.cs L34),无需在 __enums__.xlsx 加 EUseEffect.EVENT 枚举档(现 __enums__ 的 EVENT 行是 avatar.EUnlockCond 段的、与道具 use_effect 无关),配置只需 itemdef.xlsx 加行(use_effect=5)+ giftrandom.xlsx 加行(index=6101 → item_id=30101)。本子单范围(纯客户端,Fantasy 仓零 diff):① 客户端 itemdef.xlsx 加 1 行 EVENT 解锁道具(id=30101, name=110101, desc=210101, icon=icon_avatar_event 占位, quality=4 EPIC, automatic=1 立即结算, type=MATERIAL 沿 16 §3.7 EVENT 类语义、不进背包, param=0, use_effect=5 EVENT, use_value=3 指 avatar id=3 avt_star, use_num=1, use_level=0, stacking=0, 限时整套填 0);② 客户端 giftrandom.xlsx 加 1 行 EVENT 礼包(auto_id=11, index=6101, item_id=30101, num=1, rate=100 单项必中,与 server GiftPoolSeeds Index=6101 → ItemId=30101 × 1 共识对齐);③ 扩 ItemGrant.cs Resolve switch 加 case 5 EVENT 分支(产出 GrantKind.EventUnlock + TargetId=use_value 头像/框 id + Amount=1);④ 加 GrantKind.EventUnlock 枚举档 + 适配器方法(ApplyEventUnlock?或在 ResolveAndApply 内分支)调 AvatarConfigMgr.GetAvatar(targetId) → 若 entry≠null && PlayerInfo 不空 → AvatarUnlockService.GrantUnlock(playerInfo, entry);⑤ 适配器获取 PlayerInfo = GameContext.Instance.Player(同 18/25 范式);⑥ EditMode 单测覆盖三类(Resolve 五档→六档不破/EVENT 分支产出对头像 id 命中 GrantOnAcquire/适配器查无 avatar 静默不抛)。守不变量:① Fantasy 仓零 diff(本子单 client only);② 32/33 SendMailTo 签名零改(沿 40 §读前必看第 2 条 / 第 3 条 D1);③ 39 ActivityDef schema 零字段加;④ AvatarUnlockService.GrantUnlock(PlayerInfo, AvatarEntry) API 签名零改;⑤ ItemGrant.Resolve / GrantOnAcquire / ResolveAndApply 外部签名零改(只在 switch 增 case 5 分支);⑥ 既有 16 道具表 21 字段结构零改(仅 itemdef.xlsx 加新行 30101);⑦ 既有 avatar 头像表零改(仅 use_value=3 引用 avt_star);⑧ 既有六全栈 + 35/36/37/38 + 39 + 40 PASS 不破。承认固有限制:① EVENT 道具进背包(automatic=0)路径:本子单 automatic=1 立即结算,自动使用=0 未定义(沿 40 §五 EVENT 类只能立即结算);② AvatarConfigMgr 运行期依赖 ConfigSystem(YooAsset),纯 EditMode 单测需 InitForTest 注入 avatar entries 列表(沿 PlayerInfoTests AvatarConfigMgr.InitForTest 范式);③ 跨设备已解锁集合同步是 Tier 3 范围(沿 40 §五 诚实边界);④ EVENT 道具效果目标错指(use_value=999 不在 avatar 表)→ 适配器静默不抛 + 记日志(沿 40 §五 崩法表)。验收主验:CV1 配置层 itemdef.xlsx 30101 + giftrandom.xlsx 6101 行存在 + Luban 导出物字段对;CV2 ItemGrant 单测 Resolve EVENT 分支产出对;CV3 EVENT 适配器单测 GrantUnlock 成功落 UnlockedAvatarIds;CV4 EVENT 适配器单测 avatar id 不存在静默不抛;CV5 EVENT 适配器单测 GameContext.Player 为空静默不抛;CV6 既有 ItemSystemTests / PlayerInfoTests / MailSystemTests / RedeemCodeSystemTests 全绿;E1 全栈真往返:起服 + 模拟登录 7 次 → 服务端投 EVENT 邮件 → 客户端拉邮件 + 领取 → GrantOnAcquire 调 EVENT 分支 → AvatarUnlockService.GrantUnlock(p, e=AvatarConfigMgr.GetAvatar(3)) → PlayerInfo.UnlockedAvatarIds 含 3 + 落盘 MergeMetaSave 持久(下次启动头像三态从 Locked → Unlocked)。MongoDB / Fantasy 不可达列 BLOCKED 非 FAIL;Tier 4 第 2 子单全栈联调本子单收口。仅策划阶段,不写代码符号。' },
      { href: '40-event-unlock-relay.html', side: '40 · EVENT 头像解锁通路(Tier 4 第 2 子单 · server 段)', tag: '全栈 · EVENT 解锁中继', title: '40 · EVENT 头像解锁通路 server 段(Tier 4 第 2 子单)',
        desc: 'Tier 4 活动系统第 2 子单 server 段:把「服务端活动达标 → 邮件礼包 → 客户端 ItemUse 解锁头像」整条 EVENT 通路在配置层接通。承接 Tier 4 第 1 子单 39 §D7「GrantUnlock 是已设计好的活动发放接缝、本子单不接、留第 2 子单」(已 PASS Fantasy de5d4da7);本子单兑现「设计 18 §3.6 旁注 = 活动发放 = 把 id 加进玩家已解锁集合」的待对接钩子。审计后调查结论(关键设计判断,与简报建议有出入):简报 ②a 提议「ActivityDef 增 EventUnlockId 字段;reward 列表附 EVENT 类型 item」需要扩 32 SendMailTo 签名让 EVENT 标记跟着 reward 列表流到客户端,违守不变量 ④「32/33 SendMailTo 签名不动」;沿设计 16 现有道具系统范式,EVENT 解锁就是「一个特殊使用效果(UseEffect=5)的道具」、经礼包随机库携带,在 39 ActivityDef 的 reward 字段填 EVENT 礼包 id 即可,不需要在 39 ActivityDef 加新字段、不需要扩邮件领取响应、不需要新 G2C_AvatarUnlockPush 协议——这条通路与现有所有奖励(货币/图案/嵌套礼包)走同一条「道具→使用效果→适配器」路径,正交扩展。本子单范围(纯服务端,客户端工程零 diff):① 在 __enums__.xlsx 使用效果枚举加 EVENT=5 档(与设计 16 §3.7 「0 无 / 1 货币 / 2 图案 / 3 自选 / 4 随机」并列);② 在 item.xlsx 加 1 行 EVENT 解锁头像道具(示例 id=30101, 使用效果=5, 效果目标=头像 id 3 avt_star, 自动使用=1);③ 在 giftrandom.xlsx 加 1 行 EVENT 礼包(所属礼包=6101, 奖品=30101, 数量=1, 权重=100);④ 在 activity.xlsx 加 1 个 EVENT 解锁活动实例(示例 activity_id=2, type=Login, cycle=OneShot, target=7 即累计登录 7 次永发, reward=6101 EVENT 礼包, mail_def=活动结算邮件模板);⑤ 服务端 AuthoritativeDefs 注册新道具/礼包/活动行。守不变量:① 32/33 SendMailTo 签名零改;② 39 ActivityDef schema 零字段加;③ 16 UseEffect 解析层与适配器层(GrantUnlock 接线)本子单 server 段不动(纯服务端配置,客户端段下一刀实做);④ 18 AvatarUnlockService.GrantUnlock API 不动;⑤ 既有四全栈+ 35/36/37/38 + 39 PASS 不破。承认固有限制:本子单 server 段只让通路在「配置/数据」层接通,真正的端到端「玩家累计登录 7 次 → 邮箱收 EVENT 礼包 → 领取 → 头像解锁」运行需客户端段下一刀(扩 16 §3.7 UseEffect 解析 + 加 EVENT 适配器调 GrantUnlock)实做。验收主验:SV1 配置层四张表新增行 Luban 导出产物字段集核 + SV2 AuthoritativeDefs 加载 EVENT 活动行 + SV3 真往返(账号累计登录 7 次后 activity_progress.lastClaimedCycleKey 写已发周期键 + mails 集合收件箱多一封活动邮件,reward 库 id=6101 抽奖在领取时,客户端段下一刀验领取到 EVENT 道具的解析路径)。真往返写库依赖 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
      { href: '39-activity-server.html', side: '39 · 活动系统服务端地基(Tier 4 第 1 子单)', tag: '全栈 · 活动基础架构 + 每日登录', title: '39 · 活动系统服务端地基(基础架构 + 每日登录跑通 · Tier 4 第 1 子单)',
        desc: 'Tier 4 活动系统的第 1 子单(地基):把活动「配置 + 达标节律 + 发奖入口」的服务端基础架构铺起来,并用 1 个最简活动实例(每日登录奖)端到端跑通验证。9 套活动的其它 8 套(签到/分享/邀请/累计登录/累计游戏/累计消费/累计获得/累计交付/限时回归)留后续逐套刀,每套只是在本架构上加 1 个活动定义 + 1 个达标计数器,无需再动地基。审计结论(读源码核实):简报描述与现状有出入——① 「AvatarUnlockService.cs 钩子恒返未解锁」描述基本正确但定性错(全文件 94 行,GrantUnlock(p,e) 是已设计好的活动发放接缝把 id 写进 PlayerInfo.UnlockedAvatarIds 集合,「EVENT 未发放则未解锁」是设计意图非缺口,本子单实质 = 对接此接口的服务端反面);② 「RankPersistence 日期本地判可刷」描述正确(`dailyClaimDateBin` 本地存档可改),但这是排行榜每日/点赞奖问题,设计 33 §七 O1 已留后续刀,Tier 4 活动从一开始用服务端权威避免重蹈;③ 工程零既有「活动/quest/每日任务」底盘,grep activity/quest/dailytask 0 命中。范围严守:本子单 = 「活动基础架构(配置 / 达标 / 发奖三层各 1 套接缝)+ 1 个每日登录奖活动跑通」,9 套活动其它 8 套留后续逐套刀。服务端新增:① MongoDB activity_progress 集合(每账号每活动一条进度记录:account / activityId / 计数器 / 上次发放周期键 / version);② Luban activity.xlsx 配置表(activity_id / type 触发类型 / cycle 节律 / target 达标阈值 / reward 库 id / mail_def 结算邮件模板 id;ssgroup=c,s 双端同源导出);③ 三条达标节律接缝(登录触发 / 服务端进程内事件触发 / 周期 tick 触发)各自记本子单只接「登录触发」一套;④ 服务端进程内发奖入口复用 32 §3.5 IMailSourceService.SendMailTo 投活动结算邮件(挂活动 reward 库 id),不另造发奖路径;⑤ 每日登录奖活动实例(activity_id=1, cycle=daily, target=1 即每天登录 1 次,reward=邮件挂礼包 id,server 时钟跨天判)端到端跑通。守不变量:客户端工程零 diff(本子单 server only;若 9 套活动需客户端 UI 展示交 client 段后续刀)、Fantasy 仓加新代码、既有 30/31/32/33/35/37 业务集合 schema 零 diff、既有六全栈 PASS 不破。关键决策:① 配置存 Luban(沿 22/32/33 同源导出范式,客户端将来接 UI 时可直接读 c 组);② 达标节律 = 沿 33 rank 结算「服务端时钟 + MongoDB 已发放周期键防重」(每日登录奖周期键 = 本地日期 ticks of 服务端时区,与 33 §3.3 幂等键 = 本周期结算时刻同口径);③ 发奖入口 = 复用 32 §3.5 SendMailTo,活动 reward 走邮件领奖链不直发(诚实边界:玩家进度是客户端本地存档,服务端无玩家库存可直写,沿 30/32 范式);④ 头像 EVENT 解锁本子单不接(AvatarUnlockService.GrantUnlock 在客户端进程内,服务端发放需走「邮件挂 reward → 客户端领奖时 reward.ItemUse 走头像解锁路径」,礼包→头像解锁通路属设计 16 的扩展不在本子单,留待 Tier 4 第 2 子单接);⑤ Tier 3 跨设备恢复独立(activity_progress 已按 account UUID 与 35 关联,跨设备登同账号读到同一进度记录,Tier 3 缺口仅在「换设备如何认领此 UUID」,本子单兼容);⑥ 9 套活动定义模板:每套活动 = activity.xlsx 加 1 行 + 服务端按 type 字段选预设达标计数器(activity_progress.counter 字段自适应)+ 配置 reward + mail_def,无新代码——架构跑通后逐套刀只动配置不动代码。诚实边界:守活动配置 / 达标判定 / 周期幂等 / 发奖入口非超发权威;不守玩家「本地达标计数」真假(玩家本机刷登录计数无意义,因登录触发由服务端记录,签到/累计类活动达标计数由服务端事件触发记录,客户端不上报);不守 9 套活动具体玩法接入(留后续刀);不守活动结算自动触发(本子单只在登录时机触发「每日登录奖」达标判定,周期 tick 自动结算留 O3 后续)。含五条边界 + 现状审计逐处证据(AvatarUnlockService 全文+定性 / RankPersistence 字段证据 / 工程零活动底盘 / GDD 无活动章节)+ 立项框 a/b/c + 决策表 D1-D8 + activity_progress schema + activity.xlsx schema + 每日登录奖跑通时序图 + 整局走查崩法 6 类(同周期重复发 / 客户端伪造登录 / 服务端时钟 vs 客户端时钟 / 同账号并发登录双触发 / 跨设备同 UUID 双登 / activity_id 配置错 mail_def)+ 7 套活动接入模板预告 + 与 32/33/37 关系对照 + SV1-SV12 服务端验收 + 待拍板 O1-O7 + 风险表。真往返写库依赖 MongoDB,不可达列 BLOCKED 非 FAIL。仅策划阶段,不写代码符号。' },
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
