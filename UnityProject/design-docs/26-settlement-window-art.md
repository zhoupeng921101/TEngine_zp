<style>
  /* 本篇专用：节点树 / 字段表 / 分流 / 对照小样式（沿用 23 / 25 口径） */
  pre.code { margin:8px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; font-size:13px; line-height:1.55; }
  table.tight td, table.tight th { padding:6px 10px; }
  .pill-new { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(91,214,160,.16); color:#5bd6a0; margin-left:6px; }
  .pill-cur { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
  .pill-no  { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(255,122,138,.16); color:#ff7a8a; margin-left:6px; }
  .yes { color:#5bd6a0; font-weight:bold; }
  .no  { color:#ff7a8a; font-weight:bold; }
  td.mono, code.mono { font-family:ui-monospace,Consolas,monospace; }
  .tree { font-family:ui-monospace,Consolas,monospace; font-size:12.5px; line-height:1.6; white-space:pre; background:#0d1622; border:1px solid var(--border); border-radius:8px; padding:12px; overflow:auto; }
  .diagram { margin:12px 0; padding:10px; background:#0d1622; border:1px solid var(--border); border-radius:8px; }
</style>

# 结算窗美术换皮 · 表现层

塔罗 UI 换皮自治线**第三屏**:把效果图 `游戏结束.png` / `恭喜通关.png` 换皮成两个<mark>已在运行</mark>的 code-built 结算窗 —— `GameOverWindow`(游戏结束)与 `MergeOrderWinWindow`(恭喜通关)。<mark>本轮 = 纯 UI 补完(视觉换皮)</mark>:这两个窗的结算逻辑、`UserData` 取参、重开 / 回主菜单回调全部**保留不动**,只把 `UGuiFactory` 的纯色 Image / 文本换成贴 `Sheet_settings` 木板 + 按钮子图。基础设施**全部复用**[设计 23](#23-settings-window-art) 的 `Image.SetSubSprite(精灵表, 子图名)` 取图链路。<b>不重新发明任何基础设施、不动任何结算逻辑。</b>

<div class="callout warn">
    <b>读前必看 · 五条边界（本轮与前两屏的关键差异：这两个窗在跑，零回归是硬约束）</b>
    <ul style="margin:8px 0 0">
      <li><b>被换皮的两个窗是 code-built、且正在被多条玩法路径调用 —— 不能改触发 / 传参 / 重开。</b><code>GameOverWindow</code> 被 Classic(<code>GameWindow.cs:336</code> 传 <code>previousHigh</code>)与 合成订单结束(<code>MergeOrderWindow.cs:731</code> 传 <code>0</code>)<b>两条路径</b>调用;<code>MergeOrderWinWindow</code> 被通关(<code>MergeOrderWindow.cs:718</code> 传结算行 <code>List&lt;string&gt;</code>)调用。换皮<mark>不得改动这三处调用点的传参语义、不得改 <code>UserData</code> 解析、不得改 PLAY AGAIN / 重试 / 返回 的回调目标窗</mark>(<a href="#26-settlement-window-art::regress">§九 R 组</a>零回归硬验收)。</li>
      <li><b>取路 A（轻量换皮），不取路 B（prefab 重构）。</b>这两个窗已用 <code>UGuiFactory</code> 跑通,<code>CreateImage</code> 返回的就是 <code>Image</code>,<mark>可直接对返回值调 <code>.SetSubSprite</code> 换贴图</mark> —— 保留窗口结构 + 结算逻辑,只把纯色块换成木板 / 按钮子图,改动最小、风险最低。<b>不</b>改成 prefab + <code>FindChildComponent</code>(那会重写两个在跑窗口,风险高、收益低,与「纯 UI 补完」相悖)。路 A/B 取舍论证见 <a href="#26-settlement-window-art::way">§三</a>。</li>
      <li><b>无结算专属切图，全部复用 <code>Sheet_settings</code>；分数文本沿用既有 <code>Text</code>，digits 位图为可选增强。</b>塔罗素材<mark>没有「游戏结束 / 恭喜通关」专属切图目录</mark>(只有 设置/占卜/… 12 个),木板 / 按钮全用设置窗已导入的 <code>Sheet_settings</code> 子图拼。效果图三图标(星 / 心 / 草)、太阳奖励格无对应子图、也<b>无数据层来源</b> → 装饰占位(<a href="#26-settlement-window-art::decor">§4.3</a>)。数字默认沿用既有 <code>UGuiFactory.CreateText</code>(零依赖、已在跑);位图数字 digits 因<mark>未打表</mark>属可选增强(<a href="#26-settlement-window-art::digits">§4.4</a>)。</li>
      <li><b>效果图的三图标 / 太阳格无数据源 → 不为对位而捏造统计字段。</b>效果图「星 60 / 心 60 / 草 60」「两个太阳格」在数据层<mark>无对应字段</mark>(<code>BlockGameState</code> 只有 <code>Score</code> / <code>HighScore</code> / <code>Combo</code>,无星 / 心 / 草 / 太阳统计)。本轮安全默认 = <b>把这些当装饰层</b>:要么作纯装饰图标(数字位接已得的 <code>Score</code> 或省略),要么整组省略只保「分数 + 按钮」核心。<mark>不擅自往结算逻辑加统计字段</mark>(决策 D1,<a href="#26-settlement-window-art::decor">§4.3</a>)。</li>
      <li><b>UI 代码在热更区，加法 / 改既有都最小，不破坏 Classic / Merge。</b>两个窗口脚本就在 <code>GameScripts/HotFix/GameLogic/UI/BlockBlastUI/</code>(热更),本轮只改这两个 <code>.cs</code> 文件的 <code>OnCreate</code> 视觉部分。<mark>不新建 prefab、不动 GameContext、不动数据层、不动三处调用点</mark>(<a href="#26-settlement-window-art::regress">§九 R</a>)。</li>
    </ul>
  </div>

<div class="callout note" id="intro">
    <b>立项信息</b>
    <table>
      <tbody><tr><th>类型</th><td><span class="chip">表现层换皮 · 塔罗 UI 自治线第 3 屏 · 纯 UI 补完（视觉换皮）</span> 复用设计 23 取图链路,把两个在跑 code-built 结算窗换皮。出设计稿 + 验收标准,交开发落地。</td></tr>
      <tr><th>设计基线（经 grep / 读图核实的真实符号 / 现状）</th><td>
        <b>被换皮窗口（code-built，本轮只改视觉，逻辑不动）</b>:<br>
        ① <code>GameLogic.BlockBlastUI.GameOverWindow</code>(<code>[Window(Top, "GameOverWindow", fullScreen:true)]</code>,72 行)。<code>OnCreate</code> 读 <code>UserData is int</code> = <code>previousHigh</code>;<code>BlockGameState.Instance.Score</code> / <code>.HighScore</code> 算 <code>finalScore</code> / <code>high</code> / <code>isNewBest</code>;<code>UGuiFactory.CreateContentPanel/CreateImage/CreateText/CreateButton</code> 摆:遮罩 + 卡片 + "GAME OVER" + (新纪录)徽章 + SCORE 标签 + 分数 + BEST 标签 + 最高分 + "PLAY AGAIN" 钮 + "Back to Menu" 钮。回调:PLAY AGAIN → <code>CloseUI&lt;GameOverWindow&gt;</code> + <code>ShowUIAsync&lt;GameWindow&gt;</code>;Back → <code>CloseUI</code> + <code>ShowUIAsync&lt;MainMenuWindow&gt;</code>。<br>
        ② <code>GameLogic.BlockBlastUI.MergeOrderWinWindow</code>(<code>[Window(Top, "MergeOrderWinWindow", fullScreen:true)]</code>,61 行)。<code>OnCreate</code> 读 <code>UserData as List&lt;string&gt;</code> = 结算行;摆:遮罩 + 卡片 + "通关！" + 逐行结算文本 + "再来一局" 钮 + "返回主菜单" 钮。回调:再来一局 → <code>CloseUI</code> + <code>ShowUIAsync&lt;MergeOrderWindow&gt;</code>(重入即 ResetForMergeOrder);返回 → <code>CloseUI</code> + <code>ShowUIAsync&lt;MainMenuWindow&gt;</code>。<br>
        <b>调用点（三处，本轮不改）</b>:<code>GameWindow.cs:336</code> <code>ShowUIAsync&lt;GameOverWindow&gt;(previousHigh)</code>(Classic);<code>MergeOrderWindow.cs:731</code> <code>ShowUIAsync&lt;GameOverWindow&gt;(0)</code>(订单结束);<code>MergeOrderWindow.cs:718</code> <code>ShowUIAsync&lt;MergeOrderWinWindow&gt;(lines)</code>(通关)。<br>
        <b>构建工厂 + 坐标系（本轮沿用）</b>:<code>UGuiFactory.CreateImage(parent,name,designCx,designCy,w,h,color)</code> → 返回 <code>Image</code>(可链 <code>.SetSubSprite</code>);<code>CreateButton(...,out Image bgImage,out Text label)</code> → <code>out bgImage</code> 即按钮底图,可 <code>SetSubSprite</code>;<code>CreateContentPanel</code> 建 750×1334 固定面板、localScale 放大到 1080 参考宽(<code>BlockLayout.ContentScale=1.44</code>)。坐标 = <b>750×1334 设计系</b>(左上原点 Y 下正,经 <code>DesignToAnchored</code> 转锚点)。<br>
        <b>取图 API（设计 23 实测打通）</b>:<code>Image.SetSubSprite(string location, string spriteName)</code>(<code>SetSpriteExtensions.cs:43</code>);<code>location="Sheet_settings"</code>(单张 Multiple 模式精灵表,<mark>非 SpriteAtlas v2</mark> —— v2 不向 YooAsset 暴露子精灵,设计 23 §3.3 实测),子图名 = 源切图文件名。<br>
        <b>可用子图（22 张，源 <code>AssetRaw/UIRaw/Atlas/setting/</code>）</b>:木板 <code>base_plate</code>/<code>base_plate2</code>/<code>base_plate3</code>/<code>box1</code>/<code>box2</code>;按钮底 <code>button</code>;关闭 <code>icon_x</code>/<code>x</code>;图标 <code>chat</code>/<code>clear</code>/<code>exit</code>/<code>game</code>/<code>help</code>/<code>language</code>/<code>printer</code>/<code>setting</code>/<code>Player_music</code>/<code>Volume_up</code>;社交 <code>facebook</code>/<code>twitter</code>/<code>youtube</code>/<code>instagram</code>。<mark>星 / 心 / 草 / 太阳 / 奖杯均不在其中</mark> → 装饰占位。<br>
        <b>数字图集</b>:<code>AssetRaw/UIRaw/Atlas/numbers/digits_white/</code> · <code>digits_yellow/</code> 各 0..9 散 PNG,<mark>未打成精灵表</mark>(无 <code>Sheet_digits*</code>)→ 本轮默认不用、分数沿用 <code>Text</code>(<a href="#26-settlement-window-art::digits">§4.4</a>)。
      </td></tr>
      <tr><th>方向约束</th><td>离线还原 · <b>去变现</b>:结算窗无内购 / 复活付费 / 看广告续命(效果图也无)。加法 / 改既有都最小:只改两个窗口脚本的视觉构建,不改框架、不改数据层、不改结算逻辑 / 触发 / 重开路径。</td></tr>
      <tr><th>影响范围</th><td>
        <b>新增资源</b>:<mark>无</mark>(复用 <code>Sheet_settings</code>,已导入并被收集器收录)。<br>
        <b>新增代码</b>:<mark>无新文件</mark>(不建 prefab、不建新窗口类)。<br>
        <b>改既有（最小，仅视觉）</b>:<code>GameOverWindow.cs</code> · <code>MergeOrderWinWindow.cs</code> 的 <code>OnCreate</code> 视觉部分(把 <code>CreateImage</code> 纯色卡片 / 遮罩 / 按钮底 改为贴 <code>Sheet_settings</code> 子图;标题文本 / 分数文本沿用或微调字号色)。<br>
        <b>不改</b>:两窗的 <code>UserData</code> 解析、结算逻辑、按钮回调目标;三处 <code>ShowUIAsync</code> 调用点;<code>UGuiFactory</code> / <code>BlockLayout</code>;<code>BlockGameState</code> / <code>MergeOrderState</code> 数据层;<code>GameContext</code>;Classic / Merge 玩法窗。
      </td></tr>
      <tr><th>关键约束（零回归是硬验收）</th><td>这两个窗<b>正在被玩法路径调用</b>。换皮后:① 编译 0 error + 现有 EditMode 全绿;② 三条触发路径(Classic 结束 / 订单结束 / 通关)弹窗正常、传参不丢、重试 / 下一关 / 返回正确跳转 —— 任一回归即不通过。视觉对位 / 真机点击须 Play / 人眼手验(<a href="#26-settlement-window-art::accept">§九</a>两档拆开)。</td></tr>
    </tbody></table>
  </div>

<h2 id="what">一、做什么与为什么</h2>

现状:这两个结算窗<mark>已实装且在跑</mark> —— Classic 玩死会弹 `GameOverWindow`(SCORE / BEST / NEW BEST 纯色卡片),合成订单 DEMO 通关弹 `MergeOrderWinWindow`(「通关！」+ 结算行纯色卡片)。它们功能完整,缺的只是**美术**:全是 `UGuiFactory` 的纯色方块 + 内置字体,与塔罗木质风格([设计 23 设置窗](#23-settings-window-art) / [设计 25 个人信息窗](#25-player-info-window-art))不一致。

本轮把它们换皮成木质风格,复用[设计 23](#23-settings-window-art) 已打通的 `SetSubSprite` 取图链路。**与前两屏的本质差异**:设置窗 / 个人信息窗是「数据层已建、UI 从零造」(新建 prefab + 新窗口类);本轮两个窗<mark>UI 早已存在且在跑</mark> —— 不是从零造,而是给在跑窗口**换贴图**。这决定了取路 A(轻量换皮)而非路 B(prefab 重构),并把「零回归」抬为硬约束([§九 R](#26-settlement-window-art::regress))。

| # | 本轮交付的 | 落法 | 性质 |
| --- | --- | --- | --- |
| 1 | `GameOverWindow` 换皮(对位 游戏结束.png) | 遮罩 / 卡片 / 按钮底改贴 `Sheet_settings` 木板 + button 子图;标题 / 分数文本沿用或微调([§五](#26-settlement-window-art::over)) | <span class="pill-cur">改既有视觉</span> |
| 2 | `MergeOrderWinWindow` 换皮(对位 恭喜通关.png) | 同上,标题改「恭喜通关」风格,结算行 / 按钮换皮([§六](#26-settlement-window-art::win)) | <span class="pill-cur">改既有视觉</span> |
| 3 | 效果图装饰元素的处置(星 / 心 / 草 / 太阳格) | 无数据源 / 无切图 → 装饰占位 或 省略,不捏造统计字段([§4.3](#26-settlement-window-art::decor),决策 D1) | <span class="pill-no">装饰占位</span> |

<b>不做（本轮明确排除）:</b><span class="pill-no">改结算逻辑</span>(分数 / 新纪录判定 / 结算行不动);<span class="pill-no">改触发 / 传参 / 重开路径</span>(三处调用点不动,回调目标窗不动);<span class="pill-no">新建 prefab / 新窗口类</span>(路 A 不重构);<span class="pill-no">为对位效果图加星 / 心 / 草 / 太阳统计字段</span>(无数据源,装饰占位);<span class="pill-no">复活 / 看广告续命 / 内购</span>(去变现);<span class="pill-no">导入新切图 / 新图集</span>(复用 Sheet\_settings)。

<h2 id="effigy">二、效果图拆解（对位基准）</h2>

<h3 id="effigy-over">2.1 游戏结束.png</h3>

1080×1920 竖屏。盖在玩法 HUD 上的**模态弹窗**:半透明深色遮罩 + 居中木质大面板。自上而下:

| 区块 | 效果图内容 | 取图 / 处置 | 对应既有节点 |
| --- | --- | --- | --- |
| ① 遮罩 | 半透明深色背景(透出底层 HUD) | 纯色 `Image`(沿用既有 `Mask`,alpha≈0.55) | ✓ `Mask` |
| ② 标题木牌 | 顶部小木牌「游戏结束」(凸出面板上沿) | `box2`(木牌底)+ 文本「游戏结束」(中文,替既有 "GAME OVER") | ✓ `Card` 上沿 + `Title` |
| ③ 主面板 | 居中大木板(浅木色,深木边框) | `box1` 或 `base_plate`(择最贴效果图者) | ✓ `Card` |
| ④ 三图标行 | 一行 3 图标:⭐星 / ❤红心 / 🍀四叶草,各下方数字「60」 | <mark>无切图 + 无数据源</mark> → 装饰占位([§4.3](#26-settlement-window-art::decor)):省略 或 摆装饰图(纯色 / 通用子图)+ 数字接 `finalScore` 或占位 | 替换既有 SCORE/BEST 区 |
| ⑤ 重试按钮 | 黄色长条「重试」 | `button` 子图 + 文本「重试」 | ✓ `BtnAgain`(回调不动) |
| ⑥ 返回按钮 | 木色长条「返回」 | `button` 子图 + 文本「返回」 | ✓ `BtnMenu`(回调不动) |

<div class="callout note" style="margin-top:8px">
    <b>注意：效果图无右上角 X 关闭按钮</b>
    <p style="margin:6px 0 0">与设置窗 / 个人信息窗不同,结算窗<mark>没有 X 关闭、也没有「点遮罩关闭」</mark> —— 玩家必须从「重试 / 返回」二选一离开(结算是强制选择,不能随手关掉回到死局)。既有窗口本就<b>无遮罩点击关窗、无 X</b>(读 <code>GameOverWindow.cs</code> 确认),换皮<mark>保持这一交互</mark>:遮罩 <code>Mask</code> 是纯 Image(不挂 Button),不加 X。</p>
  </div>

<h3 id="effigy-win">2.2 恭喜通关.png</h3>

1080×1920 竖屏,与「游戏结束」同骨架,多一行奖励格:

| 区块 | 效果图内容 | 取图 / 处置 | 对应既有节点 |
| --- | --- | --- | --- |
| ① 遮罩 | 半透明深色背景 | 纯色 `Image`(沿用既有 `Mask`) | ✓ `Mask` |
| ② 标题木牌 | 顶部木牌「恭喜通关」 | `box2` + 文本「恭喜通关」(替既有「通关！」) | ✓ `Card` 上沿 + `Title` |
| ③ 主面板 | 居中大木板 | `box1` / `base_plate` | ✓ `Card` |
| ④ 三图标行 | 星 / 心 / 草 各「60」(同游戏结束) | 装饰占位([§4.3](#26-settlement-window-art::decor)) | 替换 / 叠加既有结算行 |
| ⑤ 奖励格行 | 一行 2 个木框格,内含☀太阳(通关奖励) | <mark>无切图 + 无数据源</mark> → 装饰占位:框用 `box2` / 省略,太阳无图省略或通用子图 | 新增装饰(无既有对应) |
| ⑥ 下一关按钮 | 黄色长条「下一关」 | `button` + 文本「下一关」 | ✓ `BtnAgain`(回调不动,见下注) |
| ⑦ 返回按钮 | 木色长条「返回」 | `button` + 文本「返回」 | ✓ `BtnMenu`(回调不动) |

<div class="callout note" style="margin-top:8px">
    <b>「下一关」按钮的文案 vs 回调语义（不改回调）</b>
    <p style="margin:6px 0 0">效果图按钮写「下一关」,但既有 <code>BtnAgain</code> 回调是 <code>ShowUIAsync&lt;MergeOrderWindow&gt;</code>(<mark>重新进入合成订单 DEMO，重入即 ResetForMergeOrder</mark>,即「再来一局」)。合成订单 DEMO <b>无关卡序列概念</b>,「下一关」= 重新开一局。本轮<mark>只换文案显示、不改回调目标</mark>:文案取「再玩一局」/「下一关」(<a href="#26-settlement-window-art::open">§十一 B3</a> 待 boss 定文案,默认沿用「再来一局」语义安全),回调仍 <code>ShowUIAsync&lt;MergeOrderWindow&gt;</code> 不动。把按钮文案改成「下一关」却让它重开同一局,是<b>语义误导</b> —— 故默认偏向保「再来一局」文案,文案最终由 boss 拍。</p>
  </div>

<h2 id="way">三、reskin 方式：路 A（轻量换皮）选定</h2>

| 路 | 做法 | 利 | 弊 | 取舍 |
| --- | --- | --- | --- | --- |
| <b>A. 轻量换皮（选定）</b> | 保留两窗 code-built 结构 + 结算逻辑,把 `UGuiFactory.CreateImage` 的纯色卡片 / 遮罩 / 按钮底 改为贴 `Sheet_settings` 子图(对返回的 `Image` / `out bgImage` 调 `.SetSubSprite`),文本字号 / 色微调 | <mark>改动最小、风险最低</mark>;不动结算逻辑 / 传参 / 回调;无新 prefab / 新类;零回归面最小 | 仍是 750 设计坐标系(非 prefab 锚点),与设置窗 prefab 范式不统一(但本轮不追求统一,追求零回归) | 取此 |
| B. prefab 重构 | 改成 prefab + `FindChildComponent` 绑定(对齐设置窗范式) | 范式统一 | <mark>要重写两个在跑窗口</mark>:重建节点树 + 重接结算逻辑 + 重接三处调用 + 重接回调 —— 回归面大、收益低,与「纯 UI 补完」相悖 | 不取 |

**取路 A** 的关键依据:`UGuiFactory.CreateImage` 返回 `Image` 引用、`CreateButton` 经 `out Image bgImage` 给出按钮底图引用 —— 两者都能<mark>直接链 <code>.SetSubSprite("Sheet\_settings", 子图名)</code></mark> 换皮,无需重建节点。窗口的结算计算 / `UserData` 解析 / 按钮 `onClick` 回调**一行不改**,只换视觉表现。这正是简报「低风险优先」与「逻辑保留不动」的最直接落法。

<pre class="code">// 路 A 换皮示意（GameOverWindow.OnCreate，逻辑部分不动，只改视觉构建）
private const string Atlas = "Sheet_settings";
// 既有：纯色卡片 → 改为贴木板子图
var card = UGuiFactory.CreateImage(content, "Card", cx, cardCy, 560, 600, Color.white);
card.SetSubSprite(Atlas, "box1");        // 白底 + 贴图 = 木板原色（不再用纯色 0x222244）
// 既有：纯色按钮 → 贴 button 子图（bgImage 即按钮底，可链 SetSubSprite）
var btn = UGuiFactory.CreateButton(content, "BtnAgain", cx, cardCy + 230, 370, 90,
    "重试", 38, Color.white, Color.white, out var btnBg, out _);
btnBg.SetSubSprite(Atlas, "button");
btn.onClick.AddListener(() =&gt; {           // ← 回调一字不改（零回归）
    GameModule.UI.CloseUI&lt;GameOverWindow&gt;();
    GameModule.UI.ShowUIAsync&lt;GameWindow&gt;();
});</pre>

<div class="callout note" style="margin-top:8px">
    <b>贴子图后底色用白（让木纹原色透出）</b>
    <p style="margin:6px 0 0">既有 <code>CreateImage</code> 给的是纯色(如 <code>0x222244</code> 深蓝)。<mark>换皮后该色须改 <code>Color.white</code></mark>(或保留 alpha 的白),否则木纹子图会被深色 <code>Image.color</code> 染暗。同理按钮 <code>bgColor</code> 改白。文本节点(标题 / 分数 / 按钮 label)的色按效果图调(标题深棕 <code>#b86a45</code> 系 / 分数白或深色),字体仍用 <code>UGuiFactory</code> 内置字体(本轮不引美术字体)。</p>
  </div>

<h2 id="atlas">四、美术资产接入（复用 Sheet_settings，无新资源）</h2>

<h3 id="atlas-reuse">4.1 复用设置窗精灵表</h3>

本轮<mark>不导入任何新切图、不建新图集、不动打表工具</mark>。`Sheet_settings.png`(单张 Multiple 精灵表,22 命名子精灵)已被设置窗导入、收集器收录、运行期 `SetSubSprite` 寻址打通([设计 23 §三](#23-settings-window-art::atlas)实测)。两窗的木板 / 按钮底直接复用其子图。`location="Sheet_settings"`,子图名 = 切图文件名(不含扩展名)。

<h3 id="atlas-map">4.2 子图映射（dev 读图核实）</h3>

下表是<mark>按设置窗 / 个人信息窗已用子图推断</mark>的最省方案。dev 落地时对照 `游戏结束.png` / `恭喜通关.png` 与各子图缩略图,从 5 种木板(`base_plate`/`base_plate2`/`base_plate3`/`box1`/`box2`)选最贴的,把最终映射写进窗口脚本注释。

| 节点 | 推荐子图 | 说明 |
| --- | --- | --- |
| 主面板卡片(`Card`) | `box1` | 大木板;个人信息窗主面板同用。若 `base_plate` 更贴大面板则换 |
| 标题木牌 | `box2` | 小木牌底,凸在面板上沿;个人信息窗标题板同用 |
| 按钮底(`BtnAgain` / `BtnMenu`) | `button` | 长条按钮底;设置窗长条同用。黄色「重试 / 下一关」与木色「返回」同一张 `button`,色差经文本 / 暂以同图(切图若分黄 / 木两态则分用,无则同图) |
| 奖励格框(通关窗 ②) | `box2` 缩小 / 省略 | 装饰占位([§4.3](#26-settlement-window-art::decor)) |

<h3 id="decor">4.3 效果图装饰元素的处置（星 / 心 / 草 / 太阳格 → 装饰占位，决策 D1）</h3>

效果图的「三图标行(星 / 心 / 草各 60)」与「奖励格行(2 太阳)」是本轮最需要拿捏的取舍:它们既<mark>无对应切图</mark>(Sheet\_settings 无星 / 心 / 草 / 太阳),又<mark>无数据层来源</mark>(`BlockGameState` 经 grep 只有 `Score` / `HighScore` / `Combo`,无这些统计)。三个处置:

| 方案 | 做法 | 代价 | 本轮取舍 |
| --- | --- | --- | --- |
| <b>A. 装饰占位（默认）</b> | 三图标 / 太阳格作纯装饰:用通用子图(或纯色块)摆出位置对位效果图,数字位接已得的 `finalScore`(或固定占位),<mark>不绑统计、不入逻辑</mark>;太阳格摆框占位 | 零数据层 / 零逻辑改动,可逆,对位效果图骨架 | 取此 |
| B. 整组省略 | 不摆三图标 / 太阳格,只保「标题 + 分数文本 + 按钮」核心(更接近既有窗口) | 更省、零回归;但与效果图骨架差距大 | 备选(切图 / 美术补不上时退此) |
| C. 真做统计 | 给结算加星 / 心 / 草 / 太阳的统计字段 + 数据源 | <mark>动结算逻辑 + 数据层 + 三处调用传参</mark> —— 超出「纯 UI 补完」,且无 spec 定义这些统计是什么 | 不取 |

<b>取 A（装饰占位，决策 D1，安全默认）</b>。理由:① 本轮是「纯 UI 补完(视觉换皮)」,加统计会动结算逻辑 + 传参,溢出范围、且违「零回归」;② 这些图标语义无 spec 定义(星 / 心 / 草到底统计什么?太阳是什么奖励?未知)—— 擅自赋义是替产品定需求;③ 占位可逆 —— 日后产品定义了这些统计,在数据层加字段 + 接数字位即可,装饰节点已摆好。<mark>这三图标 / 太阳格代表什么、是否要真做统计,列待裁决交 boss / 产品</mark>([§十一 D1](#26-settlement-window-art::open)),本轮按装饰占位推进不阻塞。**dev 落地时 A 与 B 二选一以视觉效果为准**(占位图勉强 / 不如不摆则退 B),不阻塞验收。

<h3 id="digits">4.4 分数数字：默认沿用 Text，digits 位图为可选增强</h3>

简报提到位图数字图集 `digits_white` / `digits_yellow` 可拼分数。但勘察发现:这两个目录是<mark>散 PNG，未打成精灵表</mark>(无 `Sheet_digits*`)。而 `SetSubSprite` 的前提是「单张 Multiple 精灵表」(SpriteAtlas v2 不暴露子精灵,设计 23 §3.3 实测)。故:

- <b>默认（安全）</b>:分数 / 数字沿用既有 `UGuiFactory.CreateText`(内置字体,零依赖、已在跑)。换皮只改字号 / 色对位效果图,不引位图数字。
- <b>可选增强（B2）</b>:若要位图数字,须先把 `digits_white/` / `digits_yellow/` 用[设计 24 打表工具](#24-ui-atlas-packer)打成 `Sheet_digits_white` / `Sheet_digits_yellow` Multiple 精灵表(子图名 = `digits_white_0`…),再逐位 `SetSubSprite` 拼。<mark>这是额外打表工作量,非本轮必需</mark>,列待拍板([§十一 B2](#26-settlement-window-art::open))。

**取默认 Text**(B2 决策,安全默认):分数文本视觉够用、零额外打表,先把换皮主体落地;位图数字作后续美术增强。

<h2 id="over">五、GameOverWindow 换皮（对位 游戏结束.png）</h2>

逐节点对照既有 `GameOverWindow.OnCreate`(72 行)给「保留 / 换皮 / 新增装饰 / 删」标注。<mark>逻辑行(读 UserData / 算 finalScore / 接 onClick)一律保留不动</mark>,只动视觉构建行。

| 既有节点 | 本轮处置 | 具体改动 |
| --- | --- | --- |
| `Mask`(遮罩纯色) | 保留 | 沿用纯色 `Image`(alpha≈0.55);<mark>不挂 Button</mark>(结算无遮罩关窗,§2.1 注)。色可微调更深 |
| `Card`(纯色卡片) | 换皮 | 色改 `Color.white` + `card.SetSubSprite(Atlas,"box1")`;尺寸 / 位置按效果图微调(对位木板) |
| `Title`(文本 "GAME OVER") | 换皮 | 文本改「游戏结束」(中文);色改深棕系(`#b86a45` 或效果图取色);可加 `box2` 木牌底(新增一个 `CreateImage` + `SetSubSprite(Atlas,"box2")` 置标题后) |
| `Badge`(新纪录徽章,条件显示) | 保留逻辑 / 视觉微调 | `isNewBest` 判定逻辑不动;徽章文本 / 色保留或对位效果图(效果图无明显徽章位 → 可保留为面板内小字,或并入三图标装饰区) |
| `ScoreLabel` + `Score` | 保留 / 微调 | 分数文本沿用(读 `finalScore`);字号 / 色对位效果图(分数是核心信息,务必显);可挪到三图标装饰区某一图标下方 |
| `BestLabel` + `Best` | 保留 / 微调 | 最高分文本沿用(读 `high`);`isNewBest` 变色逻辑不动 |
| (新增)三图标装饰行 | 装饰占位 | [§4.3](#26-settlement-window-art::decor):星 / 心 / 草占位(通用子图 / 纯色)+ 数字位接 `finalScore` 或省略;或退 B 不摆。**不绑统计** |
| `BtnAgain`(PLAY AGAIN) | 换皮 / 改文案 | 底 `SetSubSprite(Atlas,"button")` + 底色白;label 改「重试」;<mark>onClick 回调一字不改</mark>(`CloseUI<GameOverWindow>` + `ShowUIAsync<GameWindow>`) |
| `BtnMenu`(Back to Menu) | 换皮 / 改文案 | 底贴 `button`(或保透明,效果图「返回」是实心长条 → 贴 button);label 改「返回」;<mark>onClick 回调不改</mark>(`ShowUIAsync<MainMenuWindow>`) |

<div class="callout warn" style="margin-top:8px">
    <b>零回归红线（GameOverWindow 被两条路径复用）</b>
    <p style="margin:6px 0 0"><code>GameOverWindow</code> 被 <mark>Classic(传 <code>previousHigh</code>)与 订单结束(传 <code>0</code>)</mark>两条路径调用。换皮<b>必须保留 <code>UserData is int p ? p : 0</code> 的解析</b> —— 订单结束传 <code>0</code> 时 <code>previousHigh=0</code>,<code>isNewBest = finalScore&gt;0 &amp;&amp; finalScore&gt;0</code> 仍成立。<mark>不得改 finalScore / high / isNewBest 的计算</mark>,否则两条路径的「新纪录」表现都会回归。验收 <a href="#26-settlement-window-art::regress">R1 / R2</a> 专核两条路径。</p>
  </div>

<h2 id="win">六、MergeOrderWinWindow 换皮（对位 恭喜通关.png）</h2>

同 §五口径。既有 `MergeOrderWinWindow.OnCreate`(61 行):遮罩 + 卡片 + "通关！" + <mark>逐行结算文本列表</mark>(`UserData as List<string>`,2 行:完成订单数 / 累计得分)+ 再来一局 + 返回主菜单。

| 既有节点 | 本轮处置 | 具体改动 |
| --- | --- | --- |
| `Mask` | 保留 | 纯色 `Image`,不挂 Button(同 §五) |
| `Card` | 换皮 | 色改白 + `SetSubSprite(Atlas,"box1")`;尺寸对位效果图(通关窗略高,容奖励格行) |
| `Title`(文本「通关！」) | 换皮 | 文本改「恭喜通关」;深棕系色;可加 `box2` 木牌底 |
| `Line_{i}`(结算行列表) | 保留逻辑 / 视觉微调 | <mark>逐行渲染 <code>lines\[i\]</code> 的循环不动</mark>(`UserData` 解析保留);字号 / 色对位效果图;可挪到三图标装饰区下方或保列表形态。**结算行是真数据(完成订单数 / 累计得分),务必显** |
| (新增)三图标装饰行 | 装饰占位 | [§4.3](#26-settlement-window-art::decor):同 §五,装饰或省略,不绑统计 |
| (新增)奖励格行(2 太阳) | 装饰占位 | [§4.3](#26-settlement-window-art::decor):框用 `box2` 缩 / 省略,太阳无图省略 / 通用子图。**不绑奖励数据**(无来源) |
| `BtnAgain`(再来一局) | 换皮 / 文案待定 | 底贴 `button` + 白底;label 默认「再来一局」(效果图「下一关」语义误导,§2.2 注,文案待 boss [B3](#26-settlement-window-art::open));<mark>onClick 回调不改</mark>(`ShowUIAsync<MergeOrderWindow>`) |
| `BtnMenu`(返回主菜单) | 换皮 / 改文案 | 底贴 `button`;label 改「返回」;<mark>onClick 回调不改</mark>(`ShowUIAsync<MainMenuWindow>`) |

<h2 id="flow">七、结算窗触发与回调（本轮全部保留，零回归参照）</h2>

下图标出三条触发路径与回调跳转 —— <mark>本轮换皮不改这张图的任何一条线</mark>,仅给两个结算窗节点换贴图。给 dev / test 作零回归核对基准。

<div class="diagram">
  <svg viewBox="0 0 760 430" width="100%" xmlns="http://www.w3.org/2000/svg" font-family="ui-monospace,Consolas,monospace" font-size="12">
    <defs>
      <marker id="arr" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto">
        <path d="M0,0 L7,3 L0,6 Z" fill="#8fa3c8"></path>
      </marker>
      <marker id="arrG" markerWidth="9" markerHeight="9" refX="7" refY="3" orient="auto">
        <path d="M0,0 L7,3 L0,6 Z" fill="#5bd6a0"></path>
      </marker>
    </defs>
    <!-- 来源窗 -->
    <rect x="20" y="40" width="200" height="58" rx="8" fill="#1c2740" stroke="#6c8cff"></rect>
    <text x="120" y="64" fill="#cdd8f0" text-anchor="middle">GameWindow (Classic)</text>
    <text x="120" y="82" fill="#8fa3c8" text-anchor="middle" font-size="11">死局 → :336 传 previousHigh</text>
    <rect x="20" y="170" width="200" height="58" rx="8" fill="#1c2740" stroke="#6c8cff"></rect>
    <text x="120" y="194" fill="#cdd8f0" text-anchor="middle">MergeOrderWindow</text>
    <text x="120" y="212" fill="#8fa3c8" text-anchor="middle" font-size="11">订单结束 → :731 传 0</text>
    <rect x="20" y="300" width="200" height="58" rx="8" fill="#1c2740" stroke="#5bd6a0"></rect>
    <text x="120" y="324" fill="#cdd8f0" text-anchor="middle">MergeOrderWindow</text>
    <text x="120" y="342" fill="#8fa3c8" text-anchor="middle" font-size="11">通关 → :718 传 lines</text>
    <!-- 结算窗（换皮目标） -->
    <rect x="320" y="100" width="200" height="68" rx="8" fill="#3a2a1c" stroke="#ffcf5c"></rect>
    <text x="420" y="126" fill="#ffe7a8" text-anchor="middle">GameOverWindow</text>
    <text x="420" y="144" fill="#d8c08f" text-anchor="middle" font-size="11">UserData is int → previousHigh</text>
    <text x="420" y="160" fill="#d8c08f" text-anchor="middle" font-size="11">★ 本轮换皮（逻辑不动）</text>
    <rect x="320" y="290" width="200" height="68" rx="8" fill="#2a3a1c" stroke="#5bd6a0"></rect>
    <text x="420" y="316" fill="#c8e7a8" text-anchor="middle">MergeOrderWinWindow</text>
    <text x="420" y="334" fill="#a8d88f" text-anchor="middle" font-size="11">UserData as List&lt;string&gt; → 结算行</text>
    <text x="420" y="350" fill="#a8d88f" text-anchor="middle" font-size="11">★ 本轮换皮（逻辑不动）</text>
    <!-- 回调目标 -->
    <rect x="600" y="40" width="140" height="48" rx="8" fill="#1c2740" stroke="#6c8cff"></rect>
    <text x="670" y="68" fill="#cdd8f0" text-anchor="middle">GameWindow</text>
    <rect x="600" y="200" width="140" height="48" rx="8" fill="#1c2740" stroke="#6c8cff"></rect>
    <text x="670" y="228" fill="#cdd8f0" text-anchor="middle">MainMenuWindow</text>
    <rect x="600" y="330" width="140" height="48" rx="8" fill="#1c2740" stroke="#5bd6a0"></rect>
    <text x="670" y="358" fill="#cdd8f0" text-anchor="middle">MergeOrderWindow</text>
    <!-- 触发线（实线=调用） -->
    <path d="M220,69 L316,124" stroke="#8fa3c8" fill="none" marker-end="url(#arr)"></path>
    <path d="M220,199 L316,148" stroke="#8fa3c8" fill="none" marker-end="url(#arr)"></path>
    <path d="M220,329 L316,324" stroke="#5bd6a0" fill="none" marker-end="url(#arrG)"></path>
    <!-- 回调线（虚线=按钮回调） -->
    <path d="M520,118 L596,70" stroke="#ffcf5c" fill="none" stroke-dasharray="5,4" marker-end="url(#arr)"></path>
    <text x="558" y="92" fill="#ffcf5c" font-size="10">重试→GameWindow</text>
    <path d="M520,150 L596,210" stroke="#ffcf5c" fill="none" stroke-dasharray="5,4" marker-end="url(#arr)"></path>
    <text x="556" y="186" fill="#ffcf5c" font-size="10">返回→MainMenu</text>
    <path d="M520,330 L596,350" stroke="#5bd6a0" fill="none" stroke-dasharray="5,4" marker-end="url(#arrG)"></path>
    <text x="528" y="318" fill="#5bd6a0" font-size="10">再来一局→MergeOrder</text>
    <path d="M520,310 L596,224" stroke="#5bd6a0" fill="none" stroke-dasharray="5,4" marker-end="url(#arrG)"></path>
    <text x="528" y="270" fill="#5bd6a0" font-size="10">返回→MainMenu</text>
    <!-- 图例 -->
    <text x="20" y="410" fill="#8fa3c8" font-size="11">蓝=Classic / 通用窗　绿=合成订单 DEMO 路径　黄=GameOver 回调</text>
    <text x="20" y="426" fill="#8fa3c8" font-size="11">实线=触发调用(ShowUIAsync)　虚线=按钮 onClick 回调　★=本轮换皮节点(逻辑保留不动)</text>
  </svg>
  </div>

<h2 id="accept">八、验收点</h2>

拆两档:<b>逻辑回归可单测 / 编译(EditMode,test 直接跑)</b>与 <b>视觉对位 / 真机点击(需 Play / 人眼)</b>。<mark>零回归是本轮硬验收</mark>(两窗在跑)。

<h3 id="regress">8.1 逻辑回归 / 编译（EditMode，硬验收）</h3>

<table class="tight">
    <tbody><tr><th>组</th><th>#</th><th>验收点（完成定义）</th></tr>
    <tr><td rowspan="2">编译 C</td><td>C1</td><td><code>GameOverWindow.cs</code> + <code>MergeOrderWinWindow.cs</code> 编译 0 error;现有 EditMode 全绿(零回归)</td></tr>
    <tr><td>C2</td><td>Code Review 5 红线:资源释放(<code>SetSubSprite</code> 自管引用计数,无裸 <code>LoadAssetAsync&lt;Sprite&gt;</code>)/ 热更边界(两窗在 HotFix)/ 事件解耦(按钮仍 onClick)/ 模块访问(<code>GameModule.UI</code>)/ 异步优先(取图走 SetSubSprite 异步外壳)</td></tr>
    <tr><td rowspan="4">零回归 R<br>（硬）</td><td>R1</td><td><b>GameOverWindow · Classic 路径</b>:<code>UserData = previousHigh</code> 解析不变;<code>finalScore</code> / <code>high</code> / <code>isNewBest</code> 计算与换皮前一致(读 <code>BlockGameState.Instance.Score</code> / <code>.HighScore</code>);代码静态核对调用点 <code>GameWindow.cs:336</code> 传参未改</td></tr>
    <tr><td>R2</td><td><b>GameOverWindow · 订单结束路径</b>:<code>MergeOrderWindow.cs:731</code> 传 <code>0</code> 不变;<code>previousHigh=0</code> 时 <code>isNewBest = finalScore&gt;0</code> 成立(分支逻辑保留)</td></tr>
    <tr><td>R3</td><td><b>MergeOrderWinWindow · 通关路径</b>:<code>UserData as List&lt;string&gt;</code> 解析 + 逐行渲染循环保留;调用点 <code>MergeOrderWindow.cs:718</code> 传 <code>lines</code> 未改</td></tr>
    <tr><td>R4</td><td><b>回调目标零回归</b>:三个按钮 onClick 目标窗未改 —— 重试→<code>GameWindow</code>;GameOver 返回→<code>MainMenuWindow</code>;再来一局→<code>MergeOrderWindow</code>;通关返回→<code>MainMenuWindow</code>(代码静态核对 §七图各条线)</td></tr>
  </tbody></table>

<div class="callout warn" style="margin-top:8px">
    <b>R 组怎么 test：以静态核对 + 编译为主</b>
    <p style="margin:6px 0 0">两窗的结算计算依赖 <code>BlockGameState.Instance</code> 运行期单例 + <code>UIWindow</code> 生命周期,EditMode 反射驱动 <code>OnCreate</code> 成本高。<mark>R 组主验收 = 编译 0 error(换皮未破坏类型 / 调用)+ test 逐条静态核对</mark>:① 两窗 <code>UserData</code> 解析行未改;② 结算计算行未改;③ 三处调用点传参未改;④ 三个按钮回调目标未改。这是「视觉换皮不碰逻辑」的可核对证据。R1–R4 是硬验收 —— 任一处逻辑被改即不通过(换皮越界)。</p>
  </div>

<h3 id="accept-play">8.2 视觉对位 / 真机（需 Play / 人眼，手验遗留）</h3>

| # | 验收点 | 能否 MCP 截图 |
| --- | --- | --- |
| V1 | <mark>切图经 SetSubSprite 正常显示</mark>:两窗的卡片 / 木牌 / 按钮贴上 `Sheet_settings` 子图,木质风格,版面接近效果图(证明换皮链路在结算窗也通) | 可 ShowUIAsync + 截图比对(核心) |
| V2 | **GameOverWindow 对位** 游戏结束.png:木牌「游戏结束」+ 大木板 + (装饰)三图标行 + 分数显示 + 「重试」黄条 + 「返回」条;无 X、无遮罩关窗 | 可 ShowUIAsync(previousHigh) + 截图 |
| V3 | **MergeOrderWinWindow 对位** 恭喜通关.png:木牌「恭喜通关」+ 木板 + (装饰)三图标 / 太阳格 + 结算行(完成订单 / 累计得分)+ 「再来一局 / 下一关」+ 「返回」 | 可 ShowUIAsync(lines) + 截图 |
| V4 | **三条触发路径真机弹窗正常**:Classic 玩死弹 GameOver(分数 / 最高分正确);合成订单失败弹 GameOver;通关弹 WinWindow(结算行正确) | 玩到结算须真机 / 手验(MCP 不能模拟拖拽落子玩到死局) |
| V5 | **按钮真机点击跳转正确**:重试→重开玩法;返回→主菜单;再来一局→重开订单;通关返回→主菜单 | 点击须真机 / 手验(MCP 不模拟点击) |
| V6 | 装饰占位(三图标 / 太阳格)不报错、不挡按钮、不穿帮(占位图勉强则按 §4.3 退 B 省略) | 可截图核版面 |

<h2 id="hook">九、dev 改动清单</h2>

符号名经 grep / 读图核实(真实存在标「✓」)。<mark>本轮无新文件、无新资源</mark>,只改两个在跑窗口的视觉构建。

| # | 文件 | 动作 | 说明 |
| --- | --- | --- | --- |
| 1 | ✓ `HotFix/GameLogic/UI/BlockBlastUI/GameOverWindow.cs` | 改 `OnCreate` 视觉 | §五;卡片 / 按钮底 `SetSubSprite("Sheet_settings", 子图)` + 底色白;标题改「游戏结束」+ box2 木牌;按钮文案「重试 / 返回」;(装饰)三图标占位 §4.3。<mark>UserData 解析 / finalScore 计算 / onClick 回调不动</mark> |
| 2 | ✓ `HotFix/GameLogic/UI/BlockBlastUI/MergeOrderWinWindow.cs` | 改 `OnCreate` 视觉 | §六;同上;标题「恭喜通关」;结算行循环保留;按钮文案「再来一局 / 下一关(B3)」「返回」;(装饰)三图标 / 太阳格占位 §4.3。<mark>UserData 解析 / 结算行渲染 / onClick 回调不动</mark> |
| 3 | 测试:`Assets/Editor/Tests/...` 结算窗回归 | 新建 / 补 | §8.1 R 组:静态核对两窗 UserData 解析 / 结算计算 / 调用点传参 / 回调目标未改 + 编译 0 error |
| — | ✓ `GameWindow.cs` / `MergeOrderWindow.cs`(三处调用点) | **不改** | 三处 `ShowUIAsync` 传参不动(零回归) |
| — | ✓ `UGuiFactory` / `BlockLayout` / `BlockGameState` / `MergeOrderState` / `GameContext` | **不改** | 沿用工厂 / 坐标系;不动数据层 / 上下文;不为对位加统计字段 |
| — | ✓ `Sheet_settings.png` + 收集器 | **不改** | 复用设置窗已导入 / 已收录的精灵表,无新资源 |

<h2 id="open">十、待拍板清单（范围开关，交 boss / 用户）</h2>

自治模式。有安全默认的按默认推进(记 decisions 供 boss 关单复核);<mark>无安全默认 / 抵触 spec / 不可逆</mark>的才入 blockers。下表均有安全默认 → **不入 blockers**。

| # | 开关 | 本轮默认（安全默认） | 备选 / 改动触发 |
| --- | --- | --- | --- |
| **D1** | 效果图三图标(星 / 心 / 草)+ 太阳奖励格:无数据源 / 无切图,怎么处置 | **装饰占位**([§4.3](#26-settlement-window-art::decor) 方案 A)—— 摆位对位骨架、不绑统计、不动逻辑;占位图勉强则退方案 B 省略 | 若产品定义了星 / 心 / 草 / 太阳各代表什么统计 + 提供数据源 → 后续轮真做(方案 C)。<mark>这些图标语义无 spec 定义,提请 boss / 产品复核</mark> |
| B2 | 分数是否用位图数字 digits | **沿用 Text**([§4.4](#26-settlement-window-art::digits))—— digits 未打表,Text 零依赖够用 | 要位图数字则先用设计 24 工具把 digits\_white/yellow 打成 Sheet,额外工作量,后续增强 |
| B3 | 通关窗 BtnAgain 文案:「再来一局」vs「下一关」 | <b>「再来一局」</b>(回调是重开同一局,「下一关」语义误导,§2.2 注);<mark>回调目标不改</mark> | boss 若要对位效果图文字「下一关」可改文案(仅显示文字,回调仍 `MergeOrderWindow`) |
| B4 | 木板子图选哪张(box1 / base\_plate / …) | dev 读图选最贴效果图者([§4.2](#26-settlement-window-art::atlas-map) 推断 box1 / box2 / button) | 视觉为准,dev 落地核实 |
| B5 | 「重试 / 下一关」黄条 vs「返回」木条是否用不同子图 | <b>同一张 <code>button</code></b>(切图无黄 / 木分态)+ 文本区分 | 切图若有黄 / 木两态按钮则分用;无则同图(默认) |

<div class="callout note" style="margin-top:8px">
    <b>自治分流说明</b>
    <p style="margin:6px 0 0">D1–B5 均有安全默认、可逆、不抵触 spec / GDD 主线(去变现 · 离线还原方向)→ 按 plan 红线<b>取默认推进、记 decisions、不入 blockers</b>(不停机)。其中 <b>D1</b>(装饰图标语义)是最值得 boss 关单复核的一项 —— 安全默认是装饰占位,真做与否需产品定义这些统计含义,但<mark>本轮不因它停机</mark>(占位即可交付完整换皮)。<b>本轮无「无安全默认 / 抵触 GDD / 不可逆」的方向问题 → blockers 为空。</b></p>
  </div>

<h2 id="risk">十一、风险表</h2>

| 风险 | 应对 |
| --- | --- |
| **换皮越界改了结算逻辑**:dev 在改视觉时顺手动了 UserData 解析 / finalScore 计算 / 回调目标,造成两窗在跑路径回归 | §五 / §六逐节点标「逻辑行保留不动」;§8.1 R 组硬验收逐条静态核对;§九清单明列「不改」项。这是本轮第一风险(窗在跑) |
| **贴图后被纯色 Image.color 染暗**:沿用既有 `CreateImage` 的深色却又贴子图,木纹被染黑 | §三注:换皮节点的 `color` 改 `Color.white`,让木纹原色透出;按钮 `bgColor` 同改白 |
| **为对位效果图捏造统计**:dev 为摆「星 60 / 心 60 / 草 60」去加数据层统计字段,溢出范围 + 动逻辑 | §4.3 决策 D1:三图标 / 太阳格是装饰占位,不绑统计;数字位接已得 `finalScore` 或省略;真做须 boss / 产品定义后另开轮 |
| **误把结算窗加上 X / 遮罩关窗**:照搬设置窗范式给结算窗加 X / 点遮罩关,破坏「结算强制选择」交互 | §2.1 注:结算窗本无 X、无遮罩关窗(读既有代码确认),换皮保持 —— 遮罩 `Mask` 是纯 Image 不挂 Button |
| **digits 寻址不通**:dev 误以为 digits 已打表,直接 `SetSubSprite("Sheet_digits_white",...)` 取不到 | §4.4:digits 未打表,本轮默认**不用**位图数字、沿用 Text;要用须先打表(B2 后续增强) |
| **误引 prefab 范式重构**:dev 想统一范式把两窗改成 prefab + FindChildComponent,扩大回归面 | §三:本轮取路 A 轻量换皮,不重构;两窗保持 code-built + UGuiFactory,只对返回的 Image 链 SetSubSprite |
