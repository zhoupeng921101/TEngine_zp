<style>
  /* 本篇专用：节点树 / 字段表 / 分流 / 对照小样式（沿用 23 / 25 / 26 口径） */
  pre.code { margin:8px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; font-size:13px; line-height:1.55; }
  table.tight td, table.tight th { padding:6px 10px; }
  .pill-new { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(91,214,160,.16); color:#5bd6a0; margin-left:6px; }
  .pill-cur { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
  .pill-no  { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(255,122,138,.16); color:#ff7a8a; margin-left:6px; }
  .yes { color:#5bd6a0; font-weight:bold; }
  .no  { color:#ff7a8a; font-weight:bold; }
  td.mono, code.mono { font-family:ui-monospace,Consolas,monospace; }
  .tree { font-family:ui-monospace,Consolas,monospace; font-size:12.5px; line-height:1.6; white-space:pre; background:#0d1622; border:1px solid var(--border); border-radius:8px; padding:12px; overflow:auto; }
  .diagram { margin:12px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; }
</style>

# 主玩法 HUD 美术换皮 · 表现层

塔罗 UI 换皮自治线<b>第四屏（centerpiece，风险最高）</b>:把效果图 `tarot_mode.png` 换皮成<mark>已在运行的 Classic 主玩法窗 <code>GameWindow</code></mark> 的静态 HUD 外壳。<mark>本轮 = 纯 UI 补完（只换静态视觉壳）</mark>:棋盘渲染 / 落子拖拽 / 消除 / 补充 / ghost / GameOver 等动态玩法逻辑**一行不动**,只把 `UGuiFactory` 的纯色背景 / 棋盘外框 / 分数面板换成贴 `Sheet_tarot_mode` 木质子图,并对位效果图补出**静态顶栏（头像 + 3 资源条 + 齿轮）+ 3 动作按钮（更换 / 删除 / 提示）的视觉占位**。取图链路复用[设计 23](#23-settings-window-art)、换皮手法复用[设计 26 路 A](#26-settlement-window-art)(对在跑 code-built 窗的 `UGuiFactory` Image 链 `SetSubSprite`);<b>本屏是首个用<a href="#24-ui-atlas-packer">设计 24 打表工具</a>产新精灵表 <code>Sheet\_tarot\_mode</code> 的真实换皮屏</b>(前三屏复用 `Sheet_settings`,本屏有专属切图)。

<div class="callout warn">
    <b>读前必看 · 五条边界（本屏触及在跑玩法窗，守「不破坏 Classic / Merge」是硬约束）</b>
    <ul style="margin:8px 0 0">
      <li><b>tarot_mode = Classic <code>GameWindow</code> 的再主题，不是合成订单 <code>MergeOrderWindow</code>。</b>(经读图 + 读代码核实,<a href="#27-tarot-mode-hud-art::which">§二</a>)效果图结构 = 8×8 棋盘 + 3 候选块 + 大居中分数 + <mark>无订单卡 / 无合成区 / 无体力条</mark>,与 <code>GameWindow.cs</code>(Classic,435 行)逐项匹配;<code>MergeOrderWindow.cs</code>(799 行)有双订单卡 / 合成 token 行 / 体力条 / 虔诚币 / 神庙 / 盲盒,效果图全无。<mark>本轮只改 <code>GameWindow.cs</code> 的 <code>BuildStaticUI</code> 视觉部分</mark>,不碰 <code>MergeOrderWindow</code>。</li>
      <li><b>动态玩法逻辑绝不碰，只 reskin 静态 HUD 外壳。</b>棋盘渲染(<code>RenderBoard</code>)/ 候选槽(<code>RenderSlots</code>)/ 落子拖拽回调(<code>OnPieceBegin/Drag/End</code>)/ 消除补充(<code>PlaceAndResolve</code>)/ ghost 高亮(<code>UpdateGhost</code>)/ GameOver(<code>TriggerGameOver</code>)/ 分数滚动(<code>OnUpdate</code>)/ <code>BlockLayout</code> 棋盘坐标映射 —— 全部<mark>保留不动</mark>。本轮只动 <code>BuildStaticUI</code> 里「背景 / 棋盘外框 / 格子背景 / 分数面板 / 退出钮」这些<b>静态视觉构建行</b>,以及新增静态顶栏 / 动作按钮节点(<a href="#27-tarot-mode-hud-art::regress">§九 R 组</a>零回归硬验收)。</li>
      <li><b>顶栏 3 资源条 + 3 动作按钮（更换/删除/提示）= 视觉占位 + stub 钩子，不是真机制。</b>Classic <code>GameWindow</code> 的数据层 <code>BlockGameState</code> 经 grep 只有 <code>Score</code> / <code>HighScore</code> / <code>Combo</code>(<a href="#27-tarot-mode-hud-art::data">§三</a>),<mark>没有体力 / 货币 / 钻石等资源字段</mark>,也<mark>没有「更换候选块 / 删除棋子 / 提示落点」这三个新机制</mark>。本轮:3 资源条接得上的字段就接(分数 / 最高分),接不上就摆数字占位;3 动作按钮摆视觉 + 点击给 <code>Log</code>「待建」+ TODO,<b>真机制是新玩法功能、本范围外</b>(<a href="#27-tarot-mode-hud-art::stub">§六</a>)。</li>
      <li><b>取路 A（轻量换皮），不取路 B（prefab 重构）。</b>同<a href="#26-settlement-window-art::way">设计 26 §三</a>:<code>GameWindow</code> 已用 <code>UGuiFactory</code> 跑通,<code>CreateImage</code> 返回 <code>Image</code> 可直接链 <code>.SetSubSprite</code>、<code>CreateButton</code> 经 <code>out Image bgImage</code> 给按钮底图引用。保留窗口结构 + 全部玩法逻辑,只换静态视觉。<mark>不</mark>改成 prefab + <code>FindChildComponent</code>(重写在跑的核心玩法窗 = 回归面最大,与「纯 UI 补完」相悖)。</li>
      <li><b>本屏首次用打表工具产新精灵表，UI 代码在热更区。</b>16 张切图(<code>塔罗模式\</code>)→ 导入 ASCII 目录 <code>AssetRaw/UIRaw/Atlas/tarot_mode/</code> → 跑<a href="#24-ui-atlas-packer">设计 24 打表工具</a>产 <code>Sheet_tarot_mode.png</code> → 运行期 <code>SetSubSprite("Sheet_tarot_mode", 子图名)</code> 取图(<a href="#27-tarot-mode-hud-art::atlas">§四</a>)。<code>GameWindow.cs</code> 在 <code>GameScripts/HotFix/GameLogic/UI/BlockBlastUI/</code>(热更),本轮只改它一个 <code>.cs</code> 的视觉部分 + 新增切图 / 精灵表资源。<mark>不动数据层、不动 MergeOrderWindow、不动收集器</mark>。</li>
    </ul>
  </div>

<div class="callout note" id="intro">
    <b>立项信息</b>
    <table>
      <tbody><tr><th>类型</th><td><span class="chip">表现层换皮 · 塔罗 UI 自治线第 4 屏（centerpiece）· 纯 UI 补完（只换静态视觉壳）</span> 复用设计 23 取图链路 + 设计 26 路 A 手法 + 设计 24 打表工具,给在跑 Classic 主玩法窗换静态 HUD 皮。出设计稿 + 验收标准,交开发落地。</td></tr>
      <tr><th>设计基线（经 grep / 读图核实的真实符号 / 现状）</th><td>
        <b>被换皮窗口（code-built，本轮只改静态视觉，玩法逻辑不动）</b>:<code>GameLogic.BlockBlastUI.GameWindow</code>(<code>[Window(UILayer.UI, "GameWindow", fullScreen:true)]</code>,435 行)。<br>
        <b>静态视觉构建（本轮改这里）</b>:<code>BuildStaticUI()</code>(:86–132)摆 <code>Bg</code> 全屏背景(纯色 <code>BlockLayout.BgColor</code>)、<code>BoardOuter</code> 棋盘外框(<code>BoardOuterColor</code>)、64 个 <code>cellbg_r_c</code> 格子背景(<code>BoardCellBgColor</code>)、<code>BestLabel</code>+<code>Best</code> 最高分文本、<code>Score</code> 大分数文本、<code>Exit</code> 退出钮(透明底 "×")。<br>
        <b>动态玩法逻辑（本轮绝不动）</b>:<code>RenderBoard</code>(:147)逐格贴 <code>BlockLayout.ColorOf</code> 方块色;<code>RenderSlots</code>(:177)建 3 候选槽 + <code>BlockPieceDragger</code> 拖拽;<code>OnPieceEnd</code>(:248)+ <code>PlaceAndResolve</code>(:274)落子 / 消除 / 补充 / 弹字 / GameOver;<code>UpdateGhost</code>(:340)落点高亮;<code>InitGhostPool</code> / <code>ComputeGridPos</code> / <code>UpdateBest</code> / <code>OnUpdate</code> 分数滚动。<br>
        <b>数据层（grep 核实，本轮只读不写）</b>:<code>GameLogic.BlockBlast.BlockGameState</code>(<code>SimpleSingleton</code>)字段仅 <code>SaveArr</code>(棋盘)/ <code>OperaArr</code>(3 候选块)/ <code>Score</code>(当前分)/ <code>HighScore</code>(Classic 最高分)/ <code>Combo</code>(连击)+ merge-order 门控字段(<code>ElementArr</code>/<code>MergeOrderMode</code>/<code>MergeState</code>,Classic 下不用)。<mark>无体力 / 货币 / 钻石 / 心 / 提示次数等资源字段</mark>。<br>
        <b>构建工厂 + 坐标系（本轮沿用）</b>:<code>UGuiFactory.CreateImage(parent,name,designCx,designCy,w,h,color)</code> → 返回 <code>Image</code>(可链 <code>.SetSubSprite</code>);<code>CreateButton(...,out Image bgImage,out Text label)</code> → <code>out bgImage</code> 即按钮底图;<code>CreateContentPanel</code> 建 750×1334 固定面板 localScale 放大到 1080 参考宽(<code>BlockLayout.ContentScale</code>)。坐标 = <b>750×1334 设计系</b>(左上原点 Y 下正,经 <code>DesignToAnchored</code> 转锚点);棋盘原点 / 格尺寸 / 候选槽位由 <code>BlockLayout</code> 常量定(本轮不动)。<br>
        <b>取图 API（设计 23 实测打通）</b>:<code>Image.SetSubSprite(string location, string spriteName)</code>(<code>SetSpriteExtensions.cs:43</code>);<code>location="Sheet_tarot_mode"</code>(本屏打表产出的单张 Multiple 精灵表,<mark>非 SpriteAtlas v2</mark> —— v2 不向 YooAsset 暴露子精灵,设计 23 §3.3 实测),子图名 = 源切图文件名。<br>
        <b>打表工具（设计 24 已落地）</b>:<code>UIAtlasPackerTool.UIAtlasPacker.Pack(string folderPath, bool simulateBuild=true)</code>(<code>Assets/Editor/UIAtlasPacker/UIAtlasPacker.cs:92</code>);菜单 <code>Tools/UI/打表(散切图 -&gt; Multiple 精灵表)</code>(:31,选中目录后点);产出落 <code>Assets/AssetRaw/UIRaw/Atlas/</code>,<mark>不覆盖已存在文件</mark>。<br>
        <b>本屏切图源（16 张，<code>C:\Users\pc\Downloads\塔罗\塔罗\塔罗模式\</code>）</b>:<code>Rectangle</code> · <code>advertisement</code> · <code>blue</code> · <code>chess</code> · <code>chessboard</code> · <code>gemstone</code> · <code>gemstone2</code> · <code>hammer</code> · <code>icon_setting</code> · <code>image</code> · <code>magic_book</code> · <code>mask</code> · <code>potion</code> · <code>resourcebar2</code> · <code>tarot_mode</code> · <code>temple</code>。子图名 = 文件名去扩展名(<a href="#27-tarot-mode-hud-art::map">§4.3</a> 给映射推断)。
      </td></tr>
      <tr><th>方向约束</th><td>离线还原 · <b>去变现</b>:主玩法 HUD 无内购 / 资源购买 / 看广告(效果图顶栏资源条带「+」加号是<mark>视觉占位</mark>,本轮点击 → <code>Log</code>「待建」,<b>不接任何购买 / 充值</b>);切图里 <code>advertisement</code>(广告)子图不投放(<a href="#27-tarot-mode-hud-art::open">§十 D3</a>)。加法 / 改既有都最小:只改 <code>GameWindow.cs</code> 静态视觉构建 + 新增切图 / 精灵表,不改框架、不改数据层、不改任何玩法逻辑。</td></tr>
      <tr><th>影响范围</th><td>
        <b>新增资源</b>:切图 16 张(<code>AssetRaw/UIRaw/Atlas/tarot_mode/</code>,ASCII 目录)+ 打表产出 <code>Sheet_tarot_mode.png</code> + <code>.meta</code>(落 <code>AssetRaw/UIRaw/Atlas/</code>,被收集器 <code>UIRaw</code> 组自动收录)。<br>
        <b>新增代码</b>:<mark>无新文件 / 无新窗口类</mark>(路 A 不重构)。<br>
        <b>改既有（最小，仅静态视觉）</b>:<code>GameWindow.cs</code> 的 <code>BuildStaticUI</code> —— 背景 / 棋盘外框 / 格子背景 / 分数文本区改贴 <code>Sheet_tarot_mode</code> 子图 + 底色白;新增静态顶栏(头像 + 3 资源条 + 齿轮)+ 3 动作按钮(更换 / 删除 / 提示)节点 + stub 钩子。<br>
        <b>不改</b>:<code>GameWindow</code> 的所有 <code>Render*</code> / 拖拽 / 落子 / 消除 / ghost / GameOver / <code>OnUpdate</code> 逻辑;<code>BlockLayout</code> 棋盘坐标映射;<code>BlockGameState</code> / <code>MergeOrderState</code> 数据层;<code>MergeOrderWindow</code> / Classic 触发链;收集器配置;打表工具本身。
      </td></tr>
      <tr><th>关键约束（零回归是硬验收）</th><td>这是<b>正在被玩家用来玩 Classic 的核心玩法窗</b>。换皮后:① 编译 0 error + 现有 EditMode 全绿;② Classic 整局可进可玩 —— 摆块 / 落子 / 消除 / 连击弹字 / 补块 / GameOver / 分数滚动 / 最高分全部正常,棋盘坐标对位不偏;③ 合成订单 <code>MergeOrderWindow</code> 完全不受影响(本轮不碰它,门控独立)。任一玩法回归即不通过。视觉对位 / 真机拖拽落子须 Play / 人眼手验(<a href="#27-tarot-mode-hud-art::accept">§九</a>两档拆开)。</td></tr>
    </tbody></table>
  </div>

<h2 id="what">一、做什么与为什么</h2>

现状:Classic 主玩法窗 `GameWindow` <mark>已实装且在跑</mark> —— 玩家从主菜单进 Classic 就是它:8×8 棋盘 + 3 候选块 + 大分数 + BEST,全是 `UGuiFactory` 的纯色木纹色块 + 内置字体,零美术。前三屏换皮([设置窗](#23-settings-window-art) / [个人信息窗](#25-player-info-window-art) / [结算窗](#26-settlement-window-art))已把塔罗木质风格铺到弹窗与结算,但**玩家停留时间最长的主玩法界面仍是纯色占位** —— 这是整条换皮线的 centerpiece。

本轮给主玩法 HUD 换上塔罗木质皮。**与前三屏的两点差异**:① 它触及<mark>核心玩法窗</mark>(前三屏是弹窗 / 结算,玩死才弹;主玩法窗每局全程在跑),「不破坏 Classic」的零回归约束最重;② 它是<mark>首个有专属切图、要用打表工具产新精灵表的换皮屏</mark>(前三屏复用 `Sheet_settings`),把[设计 24 打表工具](#24-ui-atlas-packer)从「只验过 `setting/`」推到「真实生产一屏」。

| # | 本轮交付的 | 落法 | 性质 |
| --- | --- | --- | --- |
| 1 | 切图导入 + 打表产 `Sheet_tarot_mode` | 16 张切图导入 ASCII 目录 `tarot_mode/` → 跑设计 24 打表工具 → 产 `Sheet_tarot_mode.png`([§四](#27-tarot-mode-hud-art::atlas)) | <span class="pill-new">新精灵表（首用打表工具产）</span> |
| 2 | `GameWindow` 静态 HUD 换皮(对位 tarot\_mode.png) | 背景 / 棋盘外框 / 格子背景 / 分数面板改贴 `Sheet_tarot_mode` 木质子图 + 底色白([§五](#27-tarot-mode-hud-art::board)) | <span class="pill-cur">改既有静态视觉</span> |
| 3 | 静态顶栏(头像 + 3 资源条 + 齿轮)视觉占位 + 入口接线 | 新增静态节点对位顶栏;资源条接得上的接(分数 / 最高分)、接不上摆占位;齿轮 → 开设置窗([§5.3](#27-tarot-mode-hud-art::topbar)) | <span class="pill-cur">视觉占位 + 接线</span> |
| 4 | 3 动作按钮(更换 / 删除 / 提示)视觉占位 + stub 钩子 | 新增按钮节点 + 点击 → `Log`「待建」+ TODO,真机制本范围外([§六](#27-tarot-mode-hud-art::stub)) | <span class="pill-no">stub 占位</span> |

<b>不做（本轮明确排除）:</b><span class="pill-no">改任何玩法逻辑</span>(棋盘渲染 / 落子 / 消除 / 补充 / ghost / GameOver / 分数滚动不动);<span class="pill-no">改 <code>BlockLayout</code> 棋盘坐标映射</span>(格尺寸 / 原点 / 候选槽位不动,防对位偏移);<span class="pill-no">真做更换 / 删除 / 提示机制</span>(新玩法功能,本范围外,stub);<span class="pill-no">给资源条接真货币 / 资源系统</span>(数据层无字段,占位);<span class="pill-no">改成 prefab 重构</span>(路 A 不重构);<span class="pill-no">动 MergeOrderWindow / 合成订单玩法</span>(本轮不碰);<span class="pill-no">投放广告 / 资源购买</span>(去变现)。

<h2 id="which">二、调查结论：tarot_mode = 哪个窗？（给证据）</h2>

简报要求读图 + 读代码核实 tarot\_mode.png 对应 `GameWindow`(Classic)还是 `MergeOrderWindow`(合成订单)。<mark>结论:Classic <code>GameWindow</code> 的再主题</mark>。逐项比对:

| 效果图 tarot\_mode.png 元素 | GameWindow（Classic，435 行） | MergeOrderWindow（合成订单，799 行） | 判定 |
| --- | --- | --- | --- |
| 8×8 棋盘 + 格子背景 | ✓ 64 个 `cellbg` + `RenderBoard` 逐格 | ✓ 同构(也有棋盘) | 两者都有 |
| 3 候选块（底部一行） | ✓ `RenderSlots` 建 3 槽 | ✓ 同构 | 两者都有 |
| 大居中分数（"23333"） | ✓ `Score` 大文本 84px 居中 + `OnUpdate` 滚动 | ✗ 无大居中分数(顶部是体力 / 完成单数 / 标题「合成订单 DEMO」) | **指向 GameWindow** |
| 订单卡 / 合成 token 行 / 体力条 | ✗ 无(Classic 无这些系统) | ✓ 双订单卡 + 合成 token 行 + 体力条 + 虔诚币 + 神庙 + 盲盒 | **效果图全无 → 指向 GameWindow** |
| 顶栏 3 资源条 + 头像 + 齿轮 | ✗ 当前只有 BEST + 大分数 + 退出 × | ✗ 当前是体力 / 单数 / 盲盒 / 虔诚币 / 神庙 / 悔棋 / 退出 | 两窗当前都无此顶栏(本轮新增静态占位) |
| 3 动作按钮（更换 / 删除 / 提示） | ✗ 无 | ✗ 无(有的是悔棋 / 开盒 / 交付,语义不同) | 两窗都无 → 新机制,本轮 stub 占位 |

**判定依据**:效果图<mark>有「大居中分数」、无「订单卡 / 合成区 / 体力条」</mark> —— 这正是 Classic `GameWindow` 的特征(大分数是它的核心 HUD,而合成订单窗顶部是体力 / 单数 / 标题、无大居中分数)。故 <b>tarot\_mode = Classic <code>GameWindow</code> 的再主题</b>,本轮改 `GameWindow.cs`。这也决定**回归面 = Classic 整局玩法**(合成订单窗本轮不碰,其门控 `MergeOrderMode` 独立、Classic 下短路)。

<h2 id="data">三、顶栏 3 资源条 = 什么？（对照数据层）</h2>

效果图顶栏自左到右:① 圆头像;② 3 个资源条(各显「12345」+ 末尾「+」加号);③ 右上齿轮。要判断 3 资源条各显什么、能否接真数据,须对照 Classic `GameWindow` 的数据层 `BlockGameState`。grep 核实其全部数值字段:

| `BlockGameState` 字段 | 语义 | 能否给资源条接 |
| --- | --- | --- |
| `Score` (int) | 当前局得分 | 已在大分数显示;不重复进资源条(也可作其一) |
| `HighScore` (int) | Classic 历史最高分 | ✓ 可接一条资源条(语义 = 最高分 / 奖杯) |
| `Combo` (int) | 连击数(瞬态,消除清零) | 瞬态、非「资源」语义,不接资源条 |
| — | <mark>无体力 / 金币 / 钻石 / 心 / 宝石 / 提示次数字段</mark> | 资源条要的「货币 / 资源」语义无数据源 |

<div class="callout warn" style="margin-top:8px">
    <b>结论：3 资源条多数无数据源 → 占位（决策 D1）</b>
    <p style="margin:6px 0 0">Classic <code>BlockGameState</code> 只有得分语义字段(<code>Score</code> / <code>HighScore</code> / <code>Combo</code>),<mark>没有效果图资源条暗示的「3 种可累积资源(金币 / 宝石 / 钻石 之类)」</mark>。切图里有 <code>gemstone</code>(宝石)/ <code>gemstone2</code> / <code>potion</code>(药水)等资源图标,但<b>它们各代表什么资源、从哪累积、加号点了干什么,均无 spec 定义、无数据源</b>。本轮安全默认 = <b>资源条作视觉占位</b>:摆出 3 条对位效果图骨架,数字位 ——「接得上的接(如一条接 <code>HighScore</code>)、接不上的摆静态占位数字(如 "0" 或 "—")」,加号「+」点击 → <code>Log</code>「待建」+ TODO,<mark>不接任何货币系统、不擅自定义资源语义、不接购买</mark>(去变现)。这些资源条代表什么、是否真做,列待裁决交 boss / 产品(<a href="#27-tarot-mode-hud-art::open">§十 D1</a>),本轮按占位推进不阻塞。</p>
  </div>

<h2 id="effigy">三之补 · 效果图拆解（对位基准）</h2>

美术基准 `tarot_mode.png`(竖屏,扁平 PNG,木纹底)。自上而下:

| 区块 | 效果图内容 | 取图 / 处置 | 对应既有节点 |
| --- | --- | --- | --- |
| ① 窗口背景 | 木纹竖向背景(暖棕) | `chessboard` / `image` / `blue` 择木纹底者,或保既有纯色微调([§4.3](#27-tarot-mode-hud-art::map)) | ✓ `Bg`(换皮) |
| ② 顶栏头像 | 左上圆头像(双人占位图) | <mark>无头像数据源(Classic 无 PlayerInfo 接入)</mark> → 占位圆图(通用子图 / 纯色),或接 [设计 18](#18-player-info) 默认头像([§十 D2](#27-tarot-mode-hud-art::open)) | 新增静态(无既有对应) |
| ③ 顶栏 3 资源条 | 3 个木条:图标 + 「12345」+ 「+」加号 | `resourcebar2`(条底)+ `gemstone`/`gemstone2`/`potion`(图标)+ 文本 + 加号占位([§5.3](#27-tarot-mode-hud-art::topbar),<mark>无数据源 → 占位 D1</mark>) | 新增静态(部分接 HighScore) |
| ④ 顶栏齿轮 | 右上圆齿轮(设置入口) | `icon_setting` → 接 `ShowUIAsync<SettingsWindow>`([设计 23](#23-settings-window-art) 已建,**真接线**) | 替既有 `Exit` ×(见 [§5.4](#27-tarot-mode-hud-art::exit)) |
| ⑤ 大分数 | 顶栏下大白字「23333」 | 沿用既有 `Score` 大文本([§5.5](#27-tarot-mode-hud-art::digits):位图数字 `image` 为可选增强);字号 / 色对位效果图 | ✓ `Score`(保留 / 微调) |
| ⑥ 棋盘外框 + 格子背景 | 木质圆角外框 + 8×8 浅格 | `chess`(外框)+ `Rectangle` / `blue`(格底),或保既有纯色微调([§五](#27-tarot-mode-hud-art::board)) | ✓ `BoardOuter` + `cellbg_*`(换皮) |
| ⑦ 候选块（3 块） | 底部一行 3 候选方块(彩色) | <mark>动态生成,本轮不碰</mark>(`RenderSlots` + `BlockLayout.ColorOf` 纯色块保留) | ✓ 动态,保留不动 |
| ⑧ 3 动作按钮 | 底部 3 木条按钮「更换 / 删除 / 提示」 | `Rectangle`(条底)+ `magic_book`/`hammer`/图标 + 文本;<mark>新机制 → stub 占位</mark>([§六](#27-tarot-mode-hud-art::stub)) | 新增静态 + stub |

<div class="callout note" style="margin-top:8px">
    <b>切图与功能的对应须 dev 读图二次核实</b>
    <p style="margin:6px 0 0">16 张切图的命名(<code>chess</code> / <code>chessboard</code> / <code>Rectangle</code> / <code>resourcebar2</code> / <code>magic_book</code> / <code>hammer</code> 等)与效果图各区块的对应,上表是<mark>按图名 + 缩略推断</mark>。dev 落地时对照 <code>tarot_mode.png</code> 与各子图缩略图逐一核实,把最终「子图名 → 区块」映射写进 <code>GameWindow.cs</code> 注释。子图名取<b>切图文件名(不含扩展名)</b>。</p>
  </div>

<h2 id="atlas">四、美术资产接入（首个用打表工具产新精灵表）</h2>

<h3 id="atlas-import">4.1 切图导入落点（ASCII 目录，避免中文 location）</h3>

把 `C:\Users\pc\Downloads\塔罗\塔罗\塔罗模式\` 的 16 张 PNG 导入工程,落点**用 ASCII 目录名**(中文目录会让 location / 子图名带中文,寻址 / git 易出问题):

<pre class="code">Assets/AssetRaw/UIRaw/Atlas/tarot_mode/        ← 新建 ASCII 子目录，16 张切图放这里
    Rectangle.png  advertisement.png  blue.png  chess.png  chessboard.png
    gemstone.png  gemstone2.png  hammer.png  icon_setting.png  image.png
    magic_book.png  mask.png  potion.png  resourcebar2.png  tarot_mode.png  temple.png</pre>

**导入设置**(每张):`Texture Type = Sprite (2D and UI)`、`Sprite Mode = Single`、`Mesh Type = Full Rect`;九宫格拉伸图(`resourcebar2` 资源条底 / `Rectangle` 按钮条底 / `chess` 棋盘外框)在源 PNG importer 设 `Border`(打表工具从源 `spriteBorder` 继承,设计 24 §四)。<mark>目录须在 <code>AssetRaw/UIRaw/Atlas/</code> 下</mark>(打表工具校验落点;不在该树下产出表不被收集器收录、寻址不到,设计 24 §2.2)。

<h3 id="atlas-pack">4.2 跑打表工具产 Sheet_tarot_mode</h3>

切图导入后,用[设计 24 打表工具](#24-ui-atlas-packer)(已落地)把 `tarot_mode/` 目录打成一张 Multiple 精灵表。两种跑法等价:

- **菜单**:Project 选中 `Assets/AssetRaw/UIRaw/Atlas/tarot_mode` 目录 → 点菜单 `Tools/UI/打表(散切图 -> Multiple 精灵表)`。
- **代码**:`UIAtlasPackerTool.UIAtlasPacker.Pack("Assets/AssetRaw/UIRaw/Atlas/tarot_mode")`。

产出 `Assets/AssetRaw/UIRaw/Atlas/Sheet_tarot_mode.png`(+ `.meta`),16 个命名子图(子图名 = 源文件名去扩展名),`spriteMode=Multiple` / pivot 居中 / border 从源继承 / `SimulateBuild` 重建模拟清单使运行期可寻址。落点在收集器 `UIRaw` 组(`AddressByFileName` + `PackDirectory`)已收录树下,<mark>无需改收集器</mark>,`location = "Sheet_tarot_mode"`。打表工具**不覆盖已存在文件**(设计 24);若 `Sheet_tarot_mode.png` 已存在(重跑),先删旧表再跑。

<div class="callout note" style="margin-top:8px">
    <b>dev 落地第一步：先验寻址跑通，再铺满整窗</b>
    <p style="margin:6px 0 0">同<a href="#23-settings-window-art::atlas">设计 23</a> 经验:<b>打表后先在 Play 模式取任意一张子图显示出来</b>(如给棋盘外框贴 <code>chess</code>),确认 <code>SetSubSprite("Sheet_tarot_mode", "chess")</code> 取得到(<code>GetAssetInfo</code> 不返 invalid、子图非 null),再逐节点铺满。避免摆完整窗才发现寻址不通。设计 23 / 25 / 26 已实证 <code>Sheet_settings</code> 寻址链路通,本屏只是换一张新表(同范式),风险点收敛在「打表工具对 16 张新切图产出正确 + 新表能被 YooAsset 当 SubAssets 加载」—— 这正是本屏作为「首个用打表工具产新表的真实屏」要验的核心。</p>
  </div>

<h3 id="map">4.3 子图映射（dev 读图核实）</h3>

下表是<mark>按图名 + 缩略推断</mark>的映射。dev 落地对照 `tarot_mode.png` 与各子图缩略,把最终映射写进 `GameWindow.cs` 注释。

| 节点 / 区块 | 推断子图 | 说明 |
| --- | --- | --- |
| 窗口背景 `Bg` | `chessboard` / `image` / `blue` | 木纹底,择最贴效果图者;若无整屏背景图则保既有纯色微调 |
| 棋盘外框 `BoardOuter` | `chess` | 木质圆角外框(九宫格拉伸,border 从源继承) |
| 格子背景 `cellbg_*` | `Rectangle` / `blue` | 浅格底;64 个共用一张子图。<mark>若贴图导致逐格 64 张 Image 各加载子图开销 / 视觉杂,可只换外框 + 背景、格底保既有纯色</mark>([§5.2](#27-tarot-mode-hud-art::board) 取舍) |
| 资源条底 | `resourcebar2` | 3 条共用(九宫格拉伸) |
| 资源条图标 | `gemstone` / `gemstone2` / `potion` | 3 资源各一图标(占位,无数据源 D1) |
| 齿轮 | `icon_setting` | 设置入口(真接线 → SettingsWindow) |
| 动作按钮条底 | `Rectangle` | 3 按钮共用(九宫格) |
| 动作按钮图标 | `magic_book`(提示?) / `hammer`(删除?) / ?(更换) | dev 读图核实哪图标对哪按钮;无对位图标的用文本「更换 / 删除 / 提示」 |
| 头像 | `mask` / 占位圆 | 无头像数据源 → 占位(D2) |
| (不投放) | `advertisement` | 广告图,去变现方向不投放([§十 D3](#27-tarot-mode-hud-art::open)) |

<h2 id="board">五、GameWindow 静态 HUD 换皮（对位 tarot_mode.png）</h2>

逐节点对照既有 `GameWindow.BuildStaticUI`(:86–132)给「保留 / 换皮 / 新增 / 改」标注。<mark>玩法逻辑行(<code>Render\*</code> / 拖拽 / 落子 / ghost / GameOver / <code>OnUpdate</code>)一律保留不动</mark>,只动 `BuildStaticUI` 里的静态视觉构建。

<h3 id="board-existing">5.1 既有静态节点逐项处置</h3>

| 既有节点（BuildStaticUI） | 本轮处置 | 具体改动 |
| --- | --- | --- |
| `Bg`(全屏背景纯色 `BgColor`) | 换皮 | 色改 `Color.white` + `bg.SetSubSprite(Atlas,"chessboard")`(或木纹底子图);若无整屏背景图则保纯色微调 |
| `BoardOuter`(棋盘外框纯色 `BoardOuterColor`) | 换皮 | 色改白 + `SetSubSprite(Atlas,"chess")`(木质外框,九宫格);<mark>位置 / 尺寸不动</mark>(`BlockLayout.BoardOriginX/Y` + `BoardPixels`,改了会偏移落子对位) |
| 64 个 `cellbg_r_c`(格底纯色 `BoardCellBgColor`) | 换皮 / 可选保纯色 | 可改白 + `SetSubSprite(Atlas,"Rectangle")`;<mark>位置 / 尺寸不动</mark>(`CellCenterDesign` + `CellSize`)。[§5.2](#27-tarot-mode-hud-art::board-cellbg):64 张逐格贴图若视觉杂 / 开销大,保既有纯色格底也可(外框 + 背景已足够换出木质风) |
| `BestLabel` + `Best`(最高分文本) | 保留 / 改位 | 文本逻辑不动(读 `_initialHigh` / `UpdateBest`);可挪进顶栏资源条之一([§5.3](#27-tarot-mode-hud-art::topbar)),字号 / 色对位效果图 |
| `Score`(大分数文本) | 保留 / 微调 | 文本逻辑不动(`OnUpdate` 滚动);字号 / 色 / 位置对位效果图(大白字居中)。位图数字 [§5.5](#27-tarot-mode-hud-art::digits) 可选增强 |
| `Exit`(退出钮 "×" 透明底) | 改 / 见 §5.4 | 效果图右上是齿轮不是 ×。<mark>退出入口须保留</mark>(否则无法离开主玩法窗回主菜单)—— [§5.4](#27-tarot-mode-hud-art::exit):齿轮接设置窗,退出钮另置或并入 |

<h3 id="board-cellbg">5.2 棋盘换皮的取舍：外框必换，格底可选</h3>

棋盘是玩法核心区,换皮须<mark>绝对保位</mark>(落子靠 `BlockLayout` 坐标映射,外框 / 格底只是视觉底,坐标常量一动落子就偏)。两档:

| 方案 | 做法 | 取舍 |
| --- | --- | --- |
| <b>A. 外框 + 背景换皮，格底保纯色（默认推荐）</b> | `Bg` + `BoardOuter` 贴木质子图;64 个 `cellbg` 保既有 `BoardCellBgColor` 纯色(或微调更暖) | 改动最小、64 格无逐张贴图开销、木质风已出(外框 + 背景是视觉主体);取此为默认 |
| B. 连格底也换皮 | 64 个 `cellbg` 也 `SetSubSprite(Atlas,"Rectangle")` | 更贴效果图格纹,但 64 张 Image 各持子图引用、视觉可能杂;dev 视效果定,不强求 |

<b>取 A(默认)</b>:外框 + 背景换出木质风即达标,格底纯色不影响对位效果图整体观感,且零额外开销。<mark>无论 A / B,<code>BlockLayout</code> 的棋盘原点 / 格尺寸 / 候选槽位常量一律不动</mark>(动了落子对位偏移 = 玩法回归)。

<h3 id="topbar">5.3 新增静态顶栏（头像 + 3 资源条 + 齿轮）</h3>

效果图顶栏在既有 `GameWindow` 里**不存在**(它当前只有 BEST + 大分数 + ×)。本轮新增静态节点对位顶栏,全是 `UGuiFactory.CreateImage` / `CreateButton` 摆在 `_content` 上(750 设计系坐标,对着效果图微调):

<pre class="code">// BuildStaticUI 末尾新增（静态顶栏，全部 SetSubSprite 取 Sheet_tarot_mode 子图）
private const string Atlas = "Sheet_tarot_mode";
// ① 头像（占位，无数据源 D2）
var avatar = UGuiFactory.CreateImage(_content, "Avatar", 70, 70, 90, 90, Color.white);
avatar.SetSubSprite(Atlas, "mask");   // 或占位圆；接设计18默认头像见 §十 D2
// ② 3 资源条（占位，无数据源 D1）—— 条底 + 图标 + 数字 + 加号
string[] icons = { "gemstone", "gemstone2", "potion" };
for (int i = 0; i &lt; 3; i++) {
    float cx = 230 + i * 175;
    var bar = UGuiFactory.CreateImage(_content, $"ResBar_{i}", cx, 70, 160, 56, Color.white);
    bar.SetSubSprite(Atlas, "resourcebar2");
    var ic = UGuiFactory.CreateImage(_content, $"ResIcon_{i}", cx - 55, 70, 44, 44, Color.white);
    ic.SetSubSprite(Atlas, icons[i]);
    // 数字：接得上的接（如 i==0 接 HighScore），接不上摆占位 "0"/"—"
    UGuiFactory.CreateText(_content, $"ResNum_{i}", cx + 10, 70, 90, 40,
        i == 0 ? _state.HighScore.ToString() : "0", 28, Color.white);
    // 加号按钮 → 占位（去变现，不接购买）
    var plus = UGuiFactory.CreateButton(_content, $"ResPlus_{i}", cx + 70, 70, 36, 36,
        "+", 28, Color.white, Color.white, out var plusBg, out _);
    plus.onClick.AddListener(() =&gt; Log.Info("[GameWindow] 资源条加号：待建（无资源系统，设计27 §十 D1）"));
}
// ③ 齿轮 → 真接设置窗（设计23 已建）
var gear = UGuiFactory.CreateButton(_content, "Gear", BlockLayout.DesignWidth - 70, 70, 80, 80,
    "", 0, Color.white, Color.white, out var gearBg, out _);
gearBg.SetSubSprite(Atlas, "icon_setting");
gear.onClick.AddListener(() =&gt; GameModule.UI.ShowUIAsync&lt;SettingsWindow&gt;());</pre>

**资源条数据分流**(决策 D1):接得上的接(默认把第 1 条接 `HighScore` = 最高分语义,有真数据);其余 2 条数字摆占位("0" / "—"),图标用 `gemstone`/`potion` 占位。<mark>加号一律占位 → <code>Log</code>「待建」(去变现,不接购买)</mark>。资源条真语义 / 真数据源待 boss / 产品定义([§十 D1](#27-tarot-mode-hud-art::open))。

<h3 id="exit">5.4 退出入口须保留（效果图无 ×，但不能丢退出）</h3>

既有 `Exit` 钮("×")是<mark>玩家离开主玩法窗回主菜单的唯一入口</mark>(回调 `CloseUI<GameWindow>` + `ShowUIAsync<MainMenuWindow>`)。效果图右上是齿轮(设置)而非 ×。处置:

- **默认**:齿轮接设置窗(§5.3);<mark>退出钮保留</mark>但挪位(如齿轮左侧小返回钮,或顶栏头像旁),回调**一字不改**。不能因对位效果图把退出入口删掉(否则进了 Classic 出不来)。
- **备选**:设置窗里本就有「返回主菜单 / 退出」路径(设计 23),若产品确认「主玩法只留齿轮、退出走设置窗」,则退出钮可并入设置窗 —— 但本轮安全默认**保留独立退出钮**(改动小、不依赖设置窗有退出项),退出交互归属列 [§十 D4](#27-tarot-mode-hud-art::open) 交 boss。

<h3 id="digits">5.5 分数数字：默认沿用 Text，位图为可选增强</h3>

同[设计 26 §4.4](#26-settlement-window-art::digits):**默认**大分数 / 资源条数字沿用既有 `UGuiFactory.CreateText`(内置字体,零依赖、已在跑),换皮只改字号 / 色对位效果图。切图 `image` 若是位图数字条,要用须先确认它是「单张含 0–9 的精灵 / 或已是子图」并逐位拼,<mark>属额外工作量、非本轮必需</mark>,列可选增强([§十 B2](#27-tarot-mode-hud-art::open))。**取默认 Text**(安全默认):分数视觉够用,先把换皮主体落地。

<h2 id="stub">六、3 动作按钮（更换 / 删除 / 提示）= 视觉占位 + stub 钩子</h2>

效果图底部一行 3 木条按钮「更换 / 删除 / 提示」。这是<mark>三个新玩法机制</mark>,Classic `GameWindow` 与数据层 `BlockGameState` 均无:

| 按钮 | 推测机制 | 现状（grep 核实） | 本轮处置 |
| --- | --- | --- | --- |
| **更换** | 更换当前 3 候选块(花资源重摇) | 无(`RefillPieces` 是落完自动补,无手动重摇 + 无资源扣费) | 视觉占位 + stub |
| **删除** | 删除棋盘某棋子 / 某候选块(花资源) | 无(无单格删除机制) | 视觉占位 + stub |
| **提示** | 高亮一个可行落点(花资源 / 次数) | 无(有 `UpdateGhost` 拖拽时高亮,但无「主动求提示」) | 视觉占位 + stub |

**处置**:新增 3 按钮节点(`Rectangle` 条底 + 图标 + 文本),点击调统一 stub:

<pre class="code">// BuildStaticUI 末尾新增（3 动作按钮，stub 钩子）
string[] actions = { "更换", "删除", "提示" };
for (int i = 0; i &lt; 3; i++) {
    float cx = 130 + i * 245;
    var btn = UGuiFactory.CreateButton(_content, $"Action_{i}", cx, 1245, 220, 110,
        actions[i], 32, Color.white, Color.white, out var btnBg, out _);
    btnBg.SetSubSprite(Atlas, "Rectangle");
    int captured = i;
    btn.onClick.AddListener(() =&gt; Log.Info($"[GameWindow] 动作按钮「{actions[captured]}」：待建（新玩法机制，设计27 §六）"));
    // TODO(设计27 §六)：更换/删除/提示是新玩法功能，真机制本范围外，待产品定义后另开轮
}</pre>

<div class="callout warn" style="margin-top:8px">
    <b>stub 不等于无反馈，但绝不碰玩法逻辑</b>
    <p style="margin:6px 0 0">3 按钮点击要有「待建」<code>Log</code>(或 Toast),不能死按钮;但<mark>绝不在本轮顺手实现「更换 / 删除 / 提示」机制</mark> —— 那要动 <code>OperaArr</code> / <code>SaveArr</code> / 资源扣费 + 落子合法性,溢出「纯 UI 补完」、且会改玩法逻辑(违零回归)。真机制是独立新玩法功能,待产品定义(花什么资源 / 几次 / 规则)后另开轮(<a href="#27-tarot-mode-hud-art::open">§十 D5</a>)。本轮只摆视觉 + 留接线点。</p>
  </div>

<h2 id="flow">七、换皮范围与零回归边界（结构图）</h2>

下图标出 `GameWindow` 哪些部分本轮换皮(静态 HUD)、哪些绝不碰(动态玩法)。给 dev / test 作零回归核对基准。

<div class="diagram">
  <svg viewBox="0 0 760 440" width="100%" xmlns="http://www.w3.org/2000/svg" font-family="ui-monospace,Consolas,monospace" font-size="12">
    <defs>
      <marker id="arr" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto">
        <path d="M0,0 L7,3 L0,6 Z" fill="#8fa3c8"></path>
      </marker>
    </defs>
    <!-- GameWindow 容器 -->
    <rect x="20" y="20" width="720" height="400" rx="10" fill="#141d2e" stroke="#3a4760"></rect>
    <text x="40" y="44" fill="#cdd8f0" font-size="13">GameWindow（Classic 主玩法窗，在跑）</text>
    <!-- 换皮区（黄=本轮改） -->
    <rect x="40" y="60" width="330" height="150" rx="8" fill="#3a2a1c" stroke="#ffcf5c"></rect>
    <text x="55" y="82" fill="#ffe7a8" font-size="12">★ 本轮换皮（静态 HUD 外壳）</text>
    <text x="55" y="104" fill="#d8c08f" font-size="11">BuildStaticUI：</text>
    <text x="65" y="124" fill="#d8c08f" font-size="11">· Bg / BoardOuter / cellbg 贴木质子图</text>
    <text x="65" y="142" fill="#d8c08f" font-size="11">· Score / Best 文本微调</text>
    <text x="65" y="160" fill="#d8c08f" font-size="11">· 新增顶栏（头像/3资源条/齿轮）§5.3</text>
    <text x="65" y="178" fill="#d8c08f" font-size="11">· 新增 3 动作按钮（更换/删除/提示）stub §6</text>
    <text x="65" y="196" fill="#d8c08f" font-size="11">· 退出入口保留 §5.4</text>
    <!-- 占位区（紫=占位/stub） -->
    <rect x="40" y="228" width="330" height="78" rx="8" fill="#2a1c3a" stroke="#c89aff"></rect>
    <text x="55" y="250" fill="#dcc8ff" font-size="12">占位 / stub（无数据源 / 无机制）</text>
    <text x="65" y="270" fill="#c0a8d8" font-size="11">· 3 资源条数字 / 加号 → 占位 + 待建（D1）</text>
    <text x="65" y="288" fill="#c0a8d8" font-size="11">· 更换/删除/提示 → Log 待建（D5）</text>
    <!-- 不碰区（绿=保留不动） -->
    <rect x="390" y="60" width="330" height="246" rx="8" fill="#1c3a2a" stroke="#5bd6a0"></rect>
    <text x="405" y="82" fill="#a8e8c8" font-size="12">绝不碰（动态玩法逻辑，零回归）</text>
    <text x="415" y="104" fill="#9bd8b8" font-size="11">· RenderBoard 棋盘渲染</text>
    <text x="415" y="122" fill="#9bd8b8" font-size="11">· RenderSlots 候选槽 + Dragger 拖拽</text>
    <text x="415" y="140" fill="#9bd8b8" font-size="11">· OnPieceEnd / PlaceAndResolve 落子消除补充</text>
    <text x="415" y="158" fill="#9bd8b8" font-size="11">· UpdateGhost 落点高亮</text>
    <text x="415" y="176" fill="#9bd8b8" font-size="11">· TriggerGameOver / UpdateBest</text>
    <text x="415" y="194" fill="#9bd8b8" font-size="11">· OnUpdate 分数滚动</text>
    <text x="415" y="212" fill="#9bd8b8" font-size="11">· BlockLayout 棋盘坐标映射（格尺寸/原点/槽位）</text>
    <text x="415" y="230" fill="#9bd8b8" font-size="11">· BlockGameState 数据层（Score/HighScore/Combo）</text>
    <text x="415" y="256" fill="#9bd8b8" font-size="11">· 退出回调目标（→ MainMenuWindow）</text>
    <text x="415" y="282" fill="#9bd8b8" font-size="11">· MergeOrderWindow / 合成订单玩法（不碰）</text>
    <!-- 关系 -->
    <path d="M205,210 L205,226" stroke="#8fa3c8" fill="none" marker-end="url(#arr)"></path>
    <text x="210" y="222" fill="#8fa3c8" font-size="10">无数据/机制处降级为</text>
    <!-- 图例 -->
    <text x="40" y="358" fill="#8fa3c8" font-size="11">黄=本轮换皮（静态 HUD）　紫=占位/stub（无数据源·无机制）　绿=绝不碰（动态玩法·零回归硬约束）</text>
    <text x="40" y="378" fill="#8fa3c8" font-size="11">★ 换皮只动 BuildStaticUI 视觉构建行 + 新增静态节点；所有 Render*/拖拽/落子/坐标映射保留不动</text>
    <text x="40" y="398" fill="#8fa3c8" font-size="11">资源条加号/动作按钮一律占位 stub，绝不在本轮实现真机制或接购买（去变现 + 纯 UI 补完）</text>
  </svg>
  </div>

<h2 id="hook">八、dev 改动清单</h2>

符号名经 grep / 读图核实(真实存在标「✓」)。<mark>本轮无新代码文件、无新窗口类</mark>,只改一个在跑窗口的静态视觉构建 + 新增切图 / 精灵表资源。

| # | 文件 / 资源 | 动作 | 说明 |
| --- | --- | --- | --- |
| 1 | 切图 16 张 → `AssetRaw/UIRaw/Atlas/tarot_mode/` | 新增(ASCII 目录) | §4.1;源 `塔罗\塔罗模式\`;每张设 Sprite/Single/FullRect,九宫格图设 Border |
| 2 | `Sheet_tarot_mode.png` + `.meta` | 打表产出 | §4.2;跑 `UIAtlasPacker.Pack(".../tarot_mode")` 或菜单;落 `AssetRaw/UIRaw/Atlas/`,收集器自动收 |
| 3 | ✓ `HotFix/GameLogic/UI/BlockBlastUI/GameWindow.cs` | 改 `BuildStaticUI` 视觉 | §五 / §六;`Bg`/`BoardOuter`(/`cellbg`)贴子图 + 底色白;新增顶栏(头像 / 3 资源条 / 齿轮)+ 3 动作按钮 + stub;齿轮接 `ShowUIAsync<SettingsWindow>`;退出入口保留。<mark>所有 Render\*/拖拽/落子/ghost/GameOver/OnUpdate/BlockLayout 不动</mark> |
| 4 | 测试:`Assets/Editor/Tests/...` GameWindow 静态换皮回归 | 新建 / 补 | §9.1 R 组:静态核对玩法逻辑行未改 + 编译 0 error |
| — | ✓ `BlockLayout.cs` | **不改** | 棋盘原点 / 格尺寸 / 候选槽位 / 坐标映射常量不动(动了落子对位偏移 = 玩法回归) |
| — | ✓ `BlockGameState.cs` / `MergeOrderState.cs` | **不改** | 不动数据层;不为资源条 / 动作按钮加字段 |
| — | ✓ `MergeOrderWindow.cs` | **不改** | 合成订单玩法本轮不碰(门控独立,Classic 下短路) |
| — | ✓ 收集器 `AssetBundleCollectorSetting.asset` | **不改** | `Sheet_tarot_mode` 落已收录 `UIRaw/Atlas` 树,自动收 |

<h2 id="accept">九、验收点</h2>

拆两档:<b>回归（硬：不破坏对应玩法 + Classic + Merge，编译零回归）</b>与 <b>视觉（Play 对位 tarot\_mode.png + 切图贴对 + 玩法仍可进可玩）</b>。<mark>回归是本轮硬验收</mark>(主玩法窗在跑)。

<h3 id="regress">9.1 回归 / 编译（EditMode + 静态核对，硬验收）</h3>

<table class="tight">
    <tbody><tr><th>组</th><th>#</th><th>验收点（完成定义）</th></tr>
    <tr><td rowspan="2">编译 C</td><td>C1</td><td><code>GameWindow.cs</code> 编译 0 error;现有 EditMode 全绿(零回归)</td></tr>
    <tr><td>C2</td><td>Code Review 5 红线:资源释放(<code>SetSubSprite</code> 自管引用计数,无裸 <code>LoadAssetAsync&lt;Sprite&gt;</code>)/ 热更边界(<code>GameWindow</code> 在 HotFix)/ 事件解耦(按钮 onClick)/ 模块访问(<code>GameModule.UI</code>)/ 异步优先(取图走 SetSubSprite 异步外壳)</td></tr>
    <tr><td rowspan="5">零回归 R<br>（硬）</td><td>R1</td><td><b>玩法逻辑行未改</b>:静态核对 <code>RenderBoard</code> / <code>RenderSlots</code> / <code>OnPieceBegin/Drag/End</code> / <code>PlaceAndResolve</code> / <code>UpdateGhost</code> / <code>InitGhostPool</code> / <code>ComputeGridPos</code> / <code>TriggerGameOver</code> / <code>UpdateBest</code> / <code>OnUpdate</code> 与换皮前逐行一致(只 <code>BuildStaticUI</code> 视觉部分变)</td></tr>
    <tr><td>R2</td><td><b>棋盘坐标映射未改</b>:<code>BlockLayout</code> 的 <code>BoardOriginX/Y</code> / <code>BoardPixels</code> / <code>CellSize</code> / <code>CellCenterDesign</code> / <code>SlotCenterX</code> / <code>SlotY</code> / <code>SlotSpacing</code> 等常量未动(换皮节点的 position/size 沿用既有值);落子 grid 计算路径不变</td></tr>
    <tr><td>R3</td><td><b>数据层未改</b>:<code>BlockGameState</code> 无新增字段;<code>OperaArr</code> / <code>SaveArr</code> / <code>Score</code> / <code>HighScore</code> / <code>Combo</code> 读写路径不变;资源条 / 动作按钮未往数据层加任何状态</td></tr>
    <tr><td>R4</td><td><b>退出回调目标未改</b>:退出入口回调仍 <code>CloseUI&lt;GameWindow&gt;</code> + <code>ShowUIAsync&lt;MainMenuWindow&gt;</code>(代码静态核对);齿轮新增的 <code>ShowUIAsync&lt;SettingsWindow&gt;</code> 是叠层、不关本窗、不丢局</td></tr>
    <tr><td>R5</td><td><b>合成订单玩法不受影响</b>:<code>MergeOrderWindow.cs</code> 本轮未改(git diff 仅 <code>GameWindow.cs</code> + 资源);<code>MergeOrderMode</code> 门控独立,Classic 换皮不触碰合成订单分支</td></tr>
  </tbody></table>

<div class="callout warn" style="margin-top:8px">
    <b>R 组怎么 test：以静态核对 + 编译为主</b>
    <p style="margin:6px 0 0"><code>GameWindow</code> 的玩法依赖 <code>BlockGameState.Instance</code> 运行期单例 + <code>UIWindow</code> 生命周期 + 拖拽指针事件,EditMode 反射驱动 <code>OnCreate</code> / 模拟拖拽落子成本高。<mark>R 组主验收 = 编译 0 error(换皮未破坏类型 / 调用)+ test 逐条静态核对</mark>:① 所有 <code>Render*</code> / 拖拽 / 落子 / ghost / GameOver / <code>OnUpdate</code> 逻辑行未改;② <code>BlockLayout</code> 坐标常量未改;③ 数据层无新字段;④ 退出回调目标未改;⑤ git diff 仅 <code>GameWindow.cs</code> 视觉部分 + 资源。这是「视觉换皮不碰玩法」的可核对证据。R1–R5 是硬验收 —— 任一处玩法逻辑被改即不通过(换皮越界)。<b>整局可玩</b>(摆块→落子→消除→连击→补块→GameOver→分数滚动)须 Play / 人眼手验(MCP 不能模拟拖拽落子),归 §9.2 V4。</p>
  </div>

<h3 id="accept-play">9.2 视觉对位 / 真机（需 Play / 人眼，手验遗留）</h3>

| # | 验收点 | 能否 MCP 截图 |
| --- | --- | --- |
| V1 | <mark>打表 + 切图经 SetSubSprite 正常显示</mark>:`Sheet_tarot_mode` 打表产 16 子图、运行期取得到(背景 / 棋盘外框 / 资源条 / 齿轮 / 动作按钮贴上木质子图)。证明打表工具产新表 + 寻址链路在主玩法屏也通(本屏核心) | 可 ShowUIAsync + 截图(核心) |
| V2 | <b>对位 tarot\_mode.png</b>:顶栏(头像 + 3 资源条 + 齿轮)+ 大分数 + 木质棋盘外框 + 8×8 + 3 候选块 + 底部 3 动作按钮(更换 / 删除 / 提示),版面接近效果图 | 可 ShowUIAsync + 截图比对 |
| V3 | **棋盘对位不偏**:换皮后落子 ghost / 已落方块仍精确贴 8×8 格(坐标映射未受换皮影响);候选块在底部正确位 | 截图可看静态对位;拖拽落子手验 |
| V4 | <b>Classic 整局可进可玩（零回归实玩）</b>:从主菜单进 Classic → 摆块 / 落子 / 消除 / 连击弹字 / 补块 / GameOver / 分数滚动 / 最高分全部正常 | 玩到整局须真机 / 手验(MCP 不模拟拖拽落子) |
| V5 | **齿轮 / 退出 / 加号 / 动作按钮交互**:齿轮 → 开设置窗(不丢局);退出 → 回主菜单;加号 / 更换 / 删除 / 提示 → Log 待建(不报错、不动玩法) | 点击须真机 / 手验(MCP 不模拟点击) |
| V6 | 资源条占位 / 动作按钮 stub 不报错、不挡棋盘 / 候选块 / 落子区、不穿帮 | 可截图核版面 |
| V7 | **合成订单窗不受影响**:从主菜单进合成订单 → 正常(本轮未碰) | 可 ShowUIAsync 截图 + 手验 |

<h2 id="open">十、待拍板清单（范围开关，交 boss / 用户）</h2>

自治模式。有安全默认的按默认推进(记 decisions 供 boss 关单复核);<mark>无安全默认 / 抵触 spec / 不可逆</mark>的才入 blockers。下表均有安全默认 → **不入 blockers**。

| # | 开关 | 本轮默认（安全默认） | 备选 / 改动触发 |
| --- | --- | --- | --- |
| **D1** | 顶栏 3 资源条代表什么 / 数据源 / 加号干什么(数据层无资源字段) | **视觉占位**([§5.3](#27-tarot-mode-hud-art::topbar))—— 摆 3 条对位骨架,第 1 条接 `HighScore`、其余数字占位,图标用 gemstone/potion 占位,加号 → Log 待建(去变现不接购买);不擅自定义资源语义 | 若产品定义 3 资源各是什么(金币 / 宝石 / 提示券…)+ 数据源 + 加号行为 → 后续轮真做(加货币系统 + 接数字位)。<mark>资源语义无 spec 定义,提请 boss / 产品复核</mark> |
| **D5** | 3 动作按钮(更换 / 删除 / 提示)真机制 | **stub 占位**([§六](#27-tarot-mode-hud-art::stub))—— 摆视觉 + 点击 Log 待建 + TODO;真机制本范围外 | 更换 / 删除 / 提示是新玩法功能(花什么资源 / 几次 / 规则未定),产品定义后另开轮真做。<mark>本轮不实现(违纯 UI 补完 + 零回归)</mark> |
| D2 | 顶栏头像数据源(Classic 无 PlayerInfo 接入) | **占位圆图**(用 mask / 通用子图)—— 不为头像把 PlayerInfo 接进 Classic `GameWindow` | 若要真头像:接 [设计 18](#18-player-info) 默认头像(`GameContext.Instance.Player`),属接数据层、可后续做 |
| D3 | 切图 `advertisement`(广告)是否投放 | **不投放**([§4.3](#27-tarot-mode-hud-art::map))—— 去变现方向,不接广告 | 切图自带但本轮不用;若产品要广告位则违去变现方向,须 boss 拍(默认不投) |
| D4 | 退出入口归属(独立退出钮 vs 并入设置窗) | **保留独立退出钮**([§5.4](#27-tarot-mode-hud-art::exit))—— 挪位但回调不改,不依赖设置窗有退出项 | 产品若定「主玩法只留齿轮、退出走设置窗」则可并入(但设置窗须有退出主菜单项);默认保独立钮更稳 |
| B1 | 棋盘格底是否换皮(64 格) | **外框 + 背景换皮，格底保纯色**([§5.2](#27-tarot-mode-hud-art::board-cellbg) 方案 A) | dev 视效果可连格底也换(方案 B);视觉为准,坐标常量一律不动 |
| B2 | 大分数 / 资源条数字是否用位图数字 | **沿用 Text**([§5.5](#27-tarot-mode-hud-art::digits))—— 零依赖够用 | 要位图数字须先确认 `image` 切图是数字条并逐位拼,额外工作量,后续增强 |
| B3 | 背景 / 棋盘外框 / 各底用哪张子图 | dev 读图选最贴效果图者([§4.3](#27-tarot-mode-hud-art::map) 推断 chessboard/chess/Rectangle/resourcebar2) | 视觉为准,dev 落地核实 |

<div class="callout note" style="margin-top:8px">
    <b>自治分流说明</b>
    <p style="margin:6px 0 0">D1–B3 均有安全默认、可逆、不抵触 spec / GDD 主线(去变现 · 离线还原方向)→ 按 plan 红线<b>取默认推进、记 decisions、不入 blockers</b>(不停机)。其中 <b>D1</b>(资源条语义)与 <b>D5</b>(动作按钮机制)是最值得 boss / 产品复核的两项 —— 安全默认都是占位 / stub,真做需产品定义资源 / 机制含义,但<mark>本轮不因它们停机</mark>(占位 + stub 即可交付完整静态壳换皮)。<b>本轮无「无安全默认 / 抵触 GDD / 不可逆」的方向问题 → blockers 为空。</b></p>
  </div>

<h2 id="risk">十一、风险表</h2>

| 风险 | 应对 |
| --- | --- |
| **换皮越界改了玩法逻辑**:dev 在改视觉时顺手动了 `Render*` / 落子 / ghost / GameOver / `OnUpdate`,造成 Classic 整局回归(主玩法窗在跑,这是本轮第一风险) | §五逐节点标「玩法逻辑行保留不动」;§7 结构图明分换皮区 / 不碰区;§9.1 R 组硬验收逐条静态核对 + git diff 仅 `GameWindow.cs` 视觉;§八清单明列「不改」项 |
| **动了 BlockLayout 坐标常量致落子对位偏移**:为对位效果图改了棋盘原点 / 格尺寸 / 候选槽位,落子 / ghost 不再贴格 | §5.2 / R2:换皮节点 position/size 沿用既有 `BlockLayout` 值,坐标常量一律不动;V3 Play 验落子对位不偏 |
| **为对位捏造资源 / 机制**:dev 为摆 3 资源条 / 3 动作按钮去加货币字段 / 实现更换删除提示,溢出范围 + 动逻辑 | §三 D1 / §六 D5:资源条 / 动作按钮是占位 + stub,不绑数据 / 不实现机制;真做须产品定义后另开轮 |
| **贴图后被纯色 Image.color 染暗**:沿用既有 `CreateImage` 深色却又贴子图,木纹被染黑 | §5.1:换皮节点 `color` 改 `Color.white` 让木纹原色透出;按钮 `bgColor` 同改白(同设计 26 经验) |
| **打表工具对 16 张新切图产出异常**:首次用打表工具产新表,可能遇九宫格 border 未继承 / 子图名带中文 / 超 2048 排不下 | §4.1 ASCII 目录 + 源 PNG 设 Border;§4.2 dev 落地第一步先验取到一张子图再铺满;设计 24 工具有校验中止(超 2048 报错)。这是本屏「首用打表工具」的核心验证点(V1) |
| **丢失退出入口**:照搬效果图右上齿轮把退出 × 删掉,进 Classic 出不来 | §5.4:退出入口须保留(挪位但回调不改);齿轮是设置入口、非退出;V5 验退出回主菜单 |
| **误碰合成订单窗**:换皮时顺手改了 `MergeOrderWindow` 或共享的 `BlockLayout` | §八:`MergeOrderWindow` 不改;R5 / V7 验合成订单窗不受影响;git diff 仅 `GameWindow.cs` + 资源 |
| **误引 prefab 范式重构**:dev 想统一范式把 `GameWindow` 改成 prefab + FindChildComponent,扩大回归面 | §读前必看边界4 / 设计 26 §三:本屏取路 A 轻量换皮,不重构;保持 code-built + UGuiFactory,只对返回的 Image 链 SetSubSprite |
