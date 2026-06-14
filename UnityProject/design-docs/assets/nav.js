/* ============================================================
   Block Blast 策划文档 - 导航单一信息源
   职责:渲染①各页左侧文档树(自动高亮当前页)②首页卡片区
        ③本页目录(扫描当前页带 id 的 h2/h3 自动生成 + 滚动高亮)。
   新增/归档文档:只改下方 GROUPS 一处,全库各页自动同步。
   仅注入标准双栏布局的活跃文档与 index.html;单栏页(11、archive/)不引用本文件。
   ============================================================ */
(function () {
  // group.side = 侧边栏分组名;group.card = 首页区块标题(null 表示不在首页卡片区出现)
  // doc.href / doc.side(侧边栏标签)/ doc.tag·title·desc(首页卡片,desc 可含 HTML)
  // 归档组:archived:true;doc.related = 首页归档区链接文案
  const GROUPS = [
    { side: '总览', card: null, docs: [
      { href: 'index.html', side: '文档库首页' },
    ]},
    { side: '现状分析', card: '现状分析', docs: [
      { href: '01-gameplay-overview.html', side: '01 · 玩法总览', tag: '现状 · 核心', title: '01 · 玩法总览',
        desc: '核心循环、得分规则、方块库、动态难度系统——当前游戏到底是怎么玩的。' },
      { href: '02-dynamic-difficulty.html', side: '02 · 动态难度拆解', tag: '现状 · 数值', title: '02 · 动态难度拆解',
        desc: '会读心的发牌系统:dynamicWeight 橡皮筋、8 种算法、136 行权重表真实数据。' },
    ]},
    { side: '切片设计', card: '切片设计(活跃)', docs: [
      { href: '09-merge-order-energy.html', side: '09 · 合成订单切片', tag: '切片 · 核心循环', title: '09 · 元素合成 + 订单 + 体力',
        desc: '三系统叠成自持循环:体力→落子→消除→元素→合成→订单交付→奖励。含数值风险方案与挂接点。<b>注:元素注入规则已被 10 改为得分驱动。</b>' },
      { href: '10-score-element-rm-collect.html', side: '10 · 得分驱动元素', tag: '切片 · 玩法调整', title: '10 · 得分驱动元素生成 + 移除收集',
        desc: '消除得分驱动元素数量(映射公式 + 审定点);彻底移除收集玩法并甄别保留共享设施。含 dev 改动清单与验收点。' },
      { href: '11-core-loop-completion.html', side: '11 · 核心玩法补全', tag: '设计 · 核心补全', title: '11 · 核心玩法补全',
        desc: '把四份原始稿(方块/订单/宝箱/女神)的漏洞补成完整自洽设计:并发模型、连消/多消/全清结算、体力、术语统一、图案经济、智能生成仲裁、宝箱、女神。含验收点与已拍板决策记录(2026-06-12 用户确认)。' },
      { href: '12-tarot-blind-box.html', side: '12 · 神秘塔罗盲盒', tag: '设计 · 新玩法', title: '12 · 神秘塔罗盲盒',
        desc: '持有式即时开盒系统:连消/全消挑战与特殊订单交付攒盲盒,自选时机开出 Lv1–Lv3 图案 / 体力 / 订单缺口高阶物。含奖池权重、保底、解锁阈值逐档代入、dev 改动清单与 10 条验收点。接入现有 MergeOrderState / ClearSettlement。' },
      { href: '13-piety-temple-repair.html', side: '13 · 虔诚币 + 神庙修复', tag: '设计 · 长期主线', title: '13 · 虔诚币 + 神庙修复',
        desc: '长期主线成长链:订单交付产虔诚币 → 攒够修复 12 神庙大厅 → 修复产经验抬升守护者等级 → 升级解锁剧情章节(只做解锁标记)。加法式第二货币,不动现有灵力经济。含造价/等级曲线逐档代入、dev 改动清单与 12 条验收点。' },
      { href: '14-save-system.html', side: '14 · 跨会话存档', tag: '系统 · 持久化', title: '14 · 跨会话磁盘存档',
        desc: '把 MergeOrderState 元层进度(虔诚币/神庙/经验·守护者等级/灵力/盲盒/女神/订单完成数/今日祈愿)从单局尺度升为跨会话:启动加载、元变更后落盘、退出兜底。复用现有 Persistence 接缝;序列化层同步可单测、磁盘 IO 走 UniTask 异步外壳。含 version 迁移、缺字段保底、跨天重置、与悔棋快照分层、dev 改动清单与 14 条验收点。兑现设计 13 §七 O3。' },
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
      { href: '20-redeem-code-system.html', side: '20 · 通用兑换码系统', tag: '系统 · 通用兑换码', title: '20 · 通用兑换码系统(数据逻辑层 + 服务器接缝)',
        desc: '把玩家输入的兑换码字符串解析成奖励发放的数据逻辑层:校验(码是否有效)+ 本地一次性去重(防重复兑换)+ 发奖(复用 16 道具系统 ItemGrant/GrantPayload 落点,不另造)+ 结果编排(成功/无效/已兑换/过期/校验源不可用的结果码与文案 textId)。关键是服务器接缝——校验经可注入 IRedeemValidator 接口隔离:离线默认 LocalConfigRedeemValidator 查本地 Luban redeemcode 配置表(码→道具id×数量),RemoteRedeemValidator 仅留 stub(本工程无网络模块、方向去变现,不实接服务器,未来上后端时实现一次服务层零改动切换)。去重经既有 Persistence.Provider,新建 Luban 兑换码表(码主表+奖励子表)。加法式不改框架、不另造发奖/存储栈。含 schema 字段表、分层结构图、兑换时序图、dev 改动清单与 23 条验收点。兑现设计 19 §3.6 留的兑换码 TODO 钩子。输入/结果弹窗 UI→表现层延后(需美术)。xlsx 系统底层批次第六刀。' },
      { href: '21-mail-system.html', side: '21 · 通用邮件系统', tag: '系统 · 通用邮件', title: '21 · 通用邮件系统(数据逻辑层 + 服务器/运营接缝)',
        desc: '命名空间 GameLogic.Mail 的收件箱数据逻辑层:邮件模型(发件人/时间/标题/内容/奖励附件/已读/已领取)+ MailboxService(收件 API IMailService.Send / 列表已读>未读+时间排序 / 标记已读 / 领取单封+一键 / 删除已读 / 自动清理超N+过期 / 红点)。两道接缝:① 对外收件 API IMailService.Send 供排行榜结算·活动回收·系统补偿调用(即 spec 的「留邮件调用接口」,本轮真做本地实现);② 服务器/运营接缝 IMailSource(后台发删·定时·区服多选)= stub+TODO,离线不实现,区服离线视单一本地区服。奖励附件 = 道具系统 16 礼包随机库 id,领取时 GiftOpener.OpenRandom 抽奖 + ItemGrant 落点,领后用 17 RewardView 展示。持久化复用 Persistence.Provider 专用键 Mail.Inbox(JsonUtility 序列化收件箱,脏数据产合法空集合不抛),时钟注入 NowProvider 使有效期/保留/清理可单测。新建 Luban 邮件模板表 + 全局配置(maxCount 100/retainDays 30)。加法式不改框架、不另造发奖/存储栈。含 schema 字段表、状态机图、收件+领取时序图、dev 改动清单与 25 条验收点。邮件界面/详情/红点显示/icon→表现层延后(需美术)。xlsx 系统底层批次第七刀。' },
      { href: '22-rank-system.html', side: '22 · 排行榜底层系统', tag: '系统 · 排行榜底层', title: '22 · 排行榜底层系统(数据逻辑层 + 服务器接缝)',
        desc: '命名空间 GameLogic.Rank 的排名数据逻辑层:一张配置表控制所有榜(spec「统一用一个表格控制所有排行榜」),同 id 多行聚合成 RankDef(榜级字段)+ 名次奖励档 RankRewardTier。RankService 提供查榜(取前 N、查自己名次)、排序与并列(分数降序 + 同分按入榜时间升序,顺序名次)、结算编排(valid_type 四档:0无结算/1开服X天/2指定时间/3周循环星期X,IsSettleDue 注入时钟判到点 + 幂等防重复结)、每日/点赞奖跨天领取、红点 getter。两道接缝:① 排名数据源 IRankSource——离线 LocalRankSource(本机成绩 + 配置陪榜,可跑可测排序出名次)/ 远程 RemoteRankSource(stub 返空不连网,未来上后端实现一次服务层零改动);② 结算发奖不另造,直接调邮件系统 21 IMailService.Send(spec mail 字段=邮件id,结算奖励写进邮件待领),奖励内容复用 16 礼包随机库 id。排名层不碰 MergeOrderState/ItemGrant。持久化复用 Persistence.Provider 键 Rank.Progress(只存本机最佳分/上次结算/已结标记/每日点赞领取日期,他人成绩不进盘)。新建 Luban 排行榜表。加法式不改框架、不另造发奖/存储栈,邮件 21 零改动。含 schema 字段表、分层结构图、结算时序图、结算时机四档逐档代入、dev 改动清单与 26 条验收点。排行榜界面/列表/点赞按钮/头像/icon 与真实全服榜→表现层+远程实现延后。xlsx 系统底层批次第八刀。' },
    ]},
    { side: '表现层 / 换皮', card: '表现层(美术换皮)', docs: [
      { href: '23-settings-window-art.html', side: '23 · 设置窗美术换皮', tag: '表现层 · 首个美术 UI', title: '23 · 设置窗美术换皮',
        desc: '本工程第一个美术驱动的 UI 窗口:把效果图 setting.png 换皮成可运行的 SettingsWindow,兑现设计 19 设置系统的表现层(遗留 #24)。核心是打通「切图 → 每屏一个 SpriteAtlas v2 → prefab 摆节点(m_ 前缀)→ FindChildComponent 绑定 → [Window] 加载 → SetSubSprite 取子图 → 热更」整条链路,作为后续所有界面换皮的模板。含切图导入落点 / 图集建法 / YooAsset 收集器寻址(SetSubSprite 跑通的前提) / 1080×1920 节点树逐节点命名 + 子图映射 / 窗口脚本生命周期 / 每个按钮的实做vs占位分流表。基础设施决定:新建 GameContext 运行期上下文单例统一持有 SettingsService 等无主数据(影响后续 player-info/item/mail/rank)。数据层只调用不重写。验收拆「逻辑可单测」与「需 Play/人眼对位」两档。' },
    ]},
    { side: '代码 / 工具', card: '代码 / 工程', docs: [
      { href: '07-blockblast-code-architecture.html', side: '07 · 代码架构剖析', tag: '工程 · 代码剖析', title: '07 · BlockBlast 代码架构剖析',
        desc: '离线还原版动态难度系统的 C# 实现:三层分层、调度数据流、位棋盘 + 蒙特卡洛评分器、CPU 热点与可调点。' },
      { href: '06-psd2ugui-componentize.html', side: '06 · PSD2UGUI 工具', tag: '工具 · 已落地', title: '06 · PSD2UGUI 组件化工具',
        desc: '扁平节点 → 组件调色板一键挂 UGUI 组件(控件自动补 targetGraphic),引用在 Inspector 连。已实现并编译验证。' },
    ]},
    { side: '已归档', card: '已归档(archive/)', archived: true, docs: [
      { href: 'archive/03-reference-gap-roadmap.html', side: '03 · 移植路线图', related: '03 · 移植路线图(路线已被自研切片取代)' },
      { href: 'archive/04-proposal-combo-juice.html', side: '04 · Combo 提案', related: '04 · Combo 提案(弹字已落地,余项搁置)' },
      { href: 'archive/05-proposal-adventure.html', side: '05 · Adventure 提案', related: '05 · Adventure 提案(未实施)' },
      { href: 'archive/08-collect-demo-slice.html', side: '08 · 收集切片', related: '08 · 收集切片(玩法已移除)' },
    ]},
  ];

  // 当前页文件名(用于侧边栏 active 判定)
  const path = location.pathname.replace(/\\/g, '/');
  const file = path.substring(path.lastIndexOf('/') + 1) || 'index.html';

  function esc(s) { return s; } // 数据为本库自有可信内容,直接作 HTML 注入

  // ---------- ① 侧边栏文档树 ----------
  function buildSidebar() {
    const aside = document.getElementById('sidebar');
    if (!aside) return;
    let h = '<a class="side-brand" href="index.html"><span class="dot"></span>Block Blast 策划文档</a><nav>';
    for (const g of GROUPS) {
      if (g.archived) {
        h += '<details class="tree-fold"><summary>' + g.side + '</summary>';
        for (const d of g.docs) h += treeLink(d);
        h += '</details>';
      } else {
        h += '<div class="tree-group">' + g.side + '</div>';
        for (const d of g.docs) h += treeLink(d);
      }
    }
    h += '</nav>';
    aside.insertAdjacentHTML('afterbegin', h);
  }
  function treeLink(d) {
    const active = d.href === file ? ' active' : '';
    return '<a class="tree-link' + active + '" href="' + d.href + '">' + esc(d.side) + '</a>';
  }

  // ---------- ② 首页卡片区 ----------
  function buildCards() {
    const root = document.getElementById('cards-root');
    if (!root) return;
    let h = '';
    for (const g of GROUPS) {
      if (!g.card) continue;
      if (g.archived) {
        h += '<div class="related"><h2>' + g.card + '</h2>'
           + '<p style="color:var(--text-dim);font-size:14px;margin:0 0 12px">不再推进或已下线的历史文档,各篇首部标注归档原因。</p>'
           + '<div class="related-links">';
        for (const d of g.docs) h += '<a href="' + d.href + '">' + (d.related || d.side) + '</a>';
        h += '</div></div>';
      } else {
        h += '<h2>' + g.card + '</h2><div class="cards">';
        for (const d of g.docs) {
          h += '<a class="card" href="' + d.href + '">'
             + '<span class="tag">' + d.tag + '</span>'
             + '<h3>' + d.title + '</h3>'
             + '<p>' + d.desc + '</p></a>';
        }
        h += '</div>';
      }
    }
    root.innerHTML = h;
  }

  // ---------- ③ 本页目录(扫描带 id 的 h2/h3;立项信息框 #intro 置顶) ----------
  function buildToc() {
    const aside = document.getElementById('sidebar');
    const main = document.querySelector('main.content');
    if (!aside || !main) return;
    // 目录项 = 可选的立项信息框(#intro,非标题但设计稿首项)+ 各级标题
    const items = [];
    const intro = main.querySelector('#intro');
    if (intro) {
      const b = intro.querySelector('b');
      items.push({ el: intro, label: b ? b.textContent : '立项信息', sub: false });
    }
    main.querySelectorAll('h2[id], h3[id]').forEach(function (el) {
      items.push({ el: el, label: el.textContent, sub: el.tagName === 'H3' });
    });
    if (!items.length) return;
    let h = '<nav class="page-toc"><div class="toc-title">本页目录</div>';
    items.forEach(function (it) {
      h += '<a class="toc-link' + (it.sub ? ' sub' : '') + '" href="#' + it.el.id + '">' + it.label + '</a>';
    });
    h += '</nav>';
    aside.insertAdjacentHTML('beforeend', h);
    spyToc(items.map(function (it) { return it.el; }));
  }

  // 滚动高亮:当前章节对应的目录项加 .active(样式由本文件注入,不动 style.css)
  function spyToc(heads) {
    const style = document.createElement('style');
    style.textContent = '.toc-link.active{color:var(--accent);border-left-color:var(--accent);}';
    document.head.appendChild(style);
    const links = {};
    document.querySelectorAll('.toc-link').forEach(function (a) {
      links[a.getAttribute('href').slice(1)] = a;
    });
    let current = null;
    const obs = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (e.isIntersecting) {
          if (current) current.classList.remove('active');
          current = links[e.target.id];
          if (current) current.classList.add('active');
        }
      });
    }, { rootMargin: '0px 0px -75% 0px', threshold: 0 });
    heads.forEach(function (el) { obs.observe(el); });
  }

  buildSidebar();
  buildCards();
  buildToc();
})();
