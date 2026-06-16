<style>
  /* 本篇专用：盲盒奖池小样式 */
  .lv { display:inline-block; min-width:1.6em; text-align:center; border:1px solid #444; border-radius:4px; padding:0 4px; }
  pre.code { margin:8px 0; padding:12px; background:#0d1622; border:1px solid var(--border); border-radius:8px; overflow:auto; font-size:13px; line-height:1.55; }
  .hole { display:inline-block; font-size:11px; letter-spacing:.5px; padding:1px 7px; border-radius:10px; background:var(--accent-soft); color:var(--accent); margin-left:6px; vertical-align:middle; }
  table.tight td, table.tight th { padding:6px 10px; }
  .pill-new { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(91,214,160,.16); color:#5bd6a0; margin-left:6px; }
  .pill-cur { display:inline-block; font-size:11px; padding:1px 7px; border-radius:10px; background:rgba(108,140,255,.16); color:#6c8cff; margin-left:6px; }
</style>

# 神秘塔罗盲盒

把 GDD 的「神秘塔罗盲盒」落成一个<b>持有式、即时开盒</b>的核心系统:消除中的连消/全消挑战与特殊订单交付积累盲盒,玩家自选时机一键开出 <span class="lv">Lv1</span>–<span class="lv">Lv3</span> 图案 / 体力 / 当前订单所需高阶物。接入现有 merge-order 切片([09](#09-merge-order-energy)/[10](#10-score-element-rm-collect)/[11](#11-core-loop-completion)),复用 `MergeOrderState` / `ClearSettlement` / `SpecialOrderTrack` / `ChestSystem` 的既有形态,不另起一套。

<div class="callout warn">
    <b>读前必看 · 与工程现状的关系(单一事实源 = 代码)</b>
    <p style="margin:8px 0 0">盲盒接的是已落地的 merge-order 切片现状,不是 GDD 的完整商业体量游戏。两条边界先钉死:</p>
    <ul style="margin:8px 0 0">
      <li><b>产物上限随现状封顶 <span class="lv">Lv3</span>,不引入 Lv4</b>。GDD 写盲盒产「Lv1–Lv4」,但工程 <code>MergeOrderConfig.MaxLevel=3</code>,且设计 11 §六已把「5 级 → 3 级」作为已拍板收敛(Lv4/Lv5 不落地)。盲盒奖池产 Lv4 会与封顶不变量冲突——本篇据此把图案产物收敛为 <span class="lv">Lv1</span>–<span class="lv">Lv3</span>。详见 <a href="#12-tarot-blind-box::what">§一</a>。</li>
      <li><b>「付费购买」渠道本轮不做</b>。项目红线去变现,GDD 盲盒的「付费购买」获取渠道明确不实装(见任务边界)。本轮只做<b>消除挑战解锁</b>与<b>特殊订单附赠</b>两条免费获取路径。</li>
    </ul>
  </div>

<div class="callout note" id="intro">
    <b>立项信息</b>
    <table>
      <tbody><tr><th>类型</th><td><span class="chip">新玩法 · 核心系统</span> 出设计稿 + 验收标准,交开发落地</td></tr>
      <tr><th>设计基线</th><td>GDD「三 · 3 神秘塔罗盲盒」逐字需求 + 已落地 merge-order 切片(<code>Module/BlockBlast/MergeOrderState.cs</code> / <code>ClearSettlement.cs</code> / <code>SpecialOrderTrack.cs</code> / <code>ChestSystem.cs</code> / <code>MergeOrderConfig.cs</code>)</td></tr>
      <tr><th>方向约束</th><td>离线还原 · <b>去变现</b>(无付费购买盲盒 / 无广告开盒加速——项目红线)</td></tr>
      <tr><th>影响范围</th><td>新增数据层模块 1 个(盲盒奖池)+ <code>MergeOrderState</code> 加 1 个持有计数字段并入快照 + <code>ClearSettlement</code> 加 1 个解锁钩子 + <code>DeliverSpecial</code> 加附赠 + <code>MergeOrderWindow</code> 加计数显示与开盒窗。<b>旧路径(Classic / 无盲盒触发时)零行为变化。</b></td></tr>
      <tr><th>关键约束(继承现状)</th><td><b>自动配对合成</b>使每个非封顶等级库存恒 ≤1(满 2 即升级)。盲盒产 Lv1/Lv2 图案进收集区会触发级联合并,这是预期行为而非缺陷;但意味着盲盒「产 1 个 Lv1」可能瞬间合成升级——产物价值要按级联后的实际收益评估。详见 <a href="#12-tarot-blind-box::grant">§3.3</a>。</td></tr>
    </tbody></table>
  </div>

<h2 id="what">一、改什么与为什么</h2>

当前 merge-order 切片的中时间跨度目标只有「完成 N 单通关」与「女神好评条 N/10」。GDD 给盲盒定的定位是<b>「惊喜感,玩家的中时间跨度消除目标」</b>——即一个攒着开、开出随机惊喜的蓄水池,填补「连消/全消打得好 → 攒到一个看得见的奖励」这条反馈链。现状的连消/全消只即时发图案(设计 11 §5.2/§5.4),缺一个「跨多手攒、自选时机开」的延迟满足层。盲盒补的就是这一层。

本篇相对现状的三处新增,逐条对应 GDD:

| # | GDD 原文 | 本篇落法 | 现状/新增 |
| --- | --- | --- | --- |
| 1 | 盲盒作为「惊喜感的中跨度目标」 | `MergeOrderState.BlindBoxCount` 持有计数,自选时机开 | <span class="pill-new">新增</span> |
| 2 | 获取:连消、全消等高挑战形成去开启 | 挂 `ClearSettlement.Settle` 解锁钩子:全清直发 1 个盲盒;连消链达阈值发 1 个 | <span class="pill-cur">现状有结算流水线</span> + <span class="pill-new">钩子</span> |
| 3 | 获取:订单奖励(特殊订单) | `DeliverSpecial` 交付时附赠盲盒 | <span class="pill-cur">现状有特殊轨</span> + <span class="pill-new">附赠</span> |
| 4 | 内容:随机 Lv1–Lv4 图案 / 体力 / 订单所需高阶物 | 奖池产 <span class="lv">Lv1</span>–<span class="lv">Lv3</span> 图案 / 体力 / <b>当前订单缺口的高阶图案</b>(收敛 Lv4 见上方红框) | <span class="pill-new">新增奖池</span> |

<b>不做(本轮明确排除,后续轮次):</b>命运之轮活动包装、付费购买入口、集卡碎片、占卜屋/扭蛋、神谕降临双倍。本篇只做「持有 + 三渠道获取 + 即时开盒」的最小自洽系统。

<h2 id="model">二、系统模型</h2>

<h3 id="hold">2.1 持有式道具(非倒计时)</h3>

盲盒与<b>宝箱</b>(`ChestSystem`,设计 11 §九)是<b>两套不同的获取节奏</b>,刻意不合并:

- <b>宝箱 = 占槽 + 倒计时</b>:获取后入 4 箱位,必须等倒计时(去变现下只能等),到点才可开。它考验的是「攒箱位、规划开箱节奏」。
- <b>盲盒 = 持有计数 + 即时开</b>:获取即 `BlindBoxCount += 1`,玩家任意时刻点「开盒」立即扣 1 开 1。它考验的是「连消/全消打得好就多攒,想用高阶物时就开」。无倒计时、无箱位上限概念,只有一个计数。

选持有计数模型而非复用宝箱占槽的理由:GDD 把盲盒定义为「中时间跨度目标」且「在消除过程中通过高挑战去开启」,语义是<b>攒够了就能开、想开就开</b>,与倒计时的「被动等待」相反。复用宝箱的倒计时会改变它的玩法语义。两套并存、各管一种节奏。

<h3 id="vs-chest">2.2 与宝箱的分工(为什么不复用 ChestSystem 的占槽)</h3>

<div class="diagram">
  <svg viewBox="0 0 900 360" width="100%" xmlns="http://www.w3.org/2000/svg" font-family="-apple-system,'Segoe UI','PingFang SC','Microsoft YaHei',sans-serif" role="img" aria-label="盲盒与宝箱获取节奏对比结构图">
    <defs>
      <marker id="m-blue" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#6c8cff"></path></marker>
      <marker id="m-green" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#5bd6a0"></path></marker>
      <marker id="m-brown" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#b86a45"></path></marker>
    </defs>
    <!-- 共同产出引擎 -->
    <rect x="350" y="20" width="200" height="56" rx="10" fill="#1b2740" stroke="#6c8cff" stroke-width="1.5"></rect>
    <text x="450" y="44" text-anchor="middle" fill="#cdd9ff" font-size="16" font-weight="bold">消除结算</text>
    <text x="450" y="64" text-anchor="middle" fill="#8aa0d0" font-size="12">ClearSettlement.Settle</text>
    <!-- 盲盒路径 绿 -->
    <rect x="120" y="140" width="240" height="64" rx="10" fill="#16301f" stroke="#5bd6a0" stroke-width="1.5"></rect>
    <text x="240" y="166" text-anchor="middle" fill="#aef0cf" font-size="15" font-weight="bold">盲盒(持有计数)</text>
    <text x="240" y="188" text-anchor="middle" fill="#7fcaa0" font-size="12">全清/连消阈值 → Count+1</text>
    <rect x="120" y="250" width="240" height="64" rx="10" fill="#16301f" stroke="#5bd6a0" stroke-width="1.5"></rect>
    <text x="240" y="276" text-anchor="middle" fill="#aef0cf" font-size="15" font-weight="bold">即时开盒</text>
    <text x="240" y="298" text-anchor="middle" fill="#7fcaa0" font-size="12">玩家点开 → 扣1 → 掷奖池</text>
    <!-- 宝箱路径 棕 -->
    <rect x="540" y="140" width="240" height="64" rx="10" fill="#2a1d14" stroke="#b86a45" stroke-width="1.5"></rect>
    <text x="660" y="166" text-anchor="middle" fill="#e3b694" font-size="15" font-weight="bold">宝箱(占槽 4 位)</text>
    <text x="660" y="188" text-anchor="middle" fill="#c89a78" font-size="12">掉落 → 入箱位 + 倒计时</text>
    <rect x="540" y="250" width="240" height="64" rx="10" fill="#2a1d14" stroke="#b86a45" stroke-width="1.5"></rect>
    <text x="660" y="276" text-anchor="middle" fill="#e3b694" font-size="15" font-weight="bold">倒计时到点才可开</text>
    <text x="660" y="298" text-anchor="middle" fill="#c89a78" font-size="12">被动等待 → 三选一</text>
    <line x1="400" y1="76" x2="270" y2="138" stroke="#5bd6a0" stroke-width="1.6" marker-end="url(#m-green)"></line>
    <line x1="500" y1="76" x2="630" y2="138" stroke="#b86a45" stroke-width="1.6" marker-end="url(#m-brown)"></line>
    <line x1="240" y1="204" x2="240" y2="248" stroke="#5bd6a0" stroke-width="1.6" marker-end="url(#m-green)"></line>
    <line x1="660" y1="204" x2="660" y2="248" stroke="#b86a45" stroke-width="1.6" marker-end="url(#m-brown)"></line>
    <!-- 图例 -->
    <rect x="120" y="334" width="14" height="10" fill="#16301f" stroke="#5bd6a0"></rect><text x="140" y="343" fill="#9fb0c8" font-size="12">盲盒 = 主动开,无等待(本篇)</text>
    <rect x="540" y="334" width="14" height="10" fill="#2a1d14" stroke="#b86a45"></rect><text x="560" y="343" fill="#9fb0c8" font-size="12">宝箱 = 被动等,占箱位(设计 11 §九,已落地未接 UI)</text>
  </svg>
  </div>

两者共用上游的消除结算引擎,下游分两套互不干扰。盲盒的产物落点(图案/体力)复用 `MergeOrderState.AddDirect` / `RefundEnergy`,与宝箱发奖同源,改产物只改奖池一处。

<h2 id="numbers">三、数值正文</h2>

全部硬编码进新模块 `TarotBlindBoxConfig`(静态类,仿 `MergeOrderConfig` / `ChestSystem` 风格,不接 Luban)。下文给公式 + 默认常量 + 旋钮命名 + 边界逐档代入。

<h3 id="pool">3.1 奖池与掉率</h3>

开一个盲盒掷出<b>恰好 1 项</b>奖励(不是三选一——盲盒是「开即得」的惊喜,不是宝箱的「选其一」)。奖项分四类,按权重抽样:

| 奖项(RewardKind) | 产物 | 默认权重 | 占比 | 价值定位 |
| --- | --- | --- | --- | --- |
| `PatternLow` | <span class="lv">Lv1</span> 图案 ×1(类型见 §3.3) | 40 | 40% | 常见、保底兜底项 |
| `PatternMid` | <span class="lv">Lv2</span> 图案 ×1 | 22 | 22% | 中等惊喜 |
| `Energy` | 体力 +`BoxEnergyGain`(默认 10) | 20 | 20% | 续命,等价约 10 次落子 |
| `PatternHigh` | <span class="lv">Lv3</span> 图案 ×1(=封顶高阶物) | 10 | 10% | 稀有大奖,GDD「高阶物品」 |
| `NeededHigh` | <b>当前订单缺口的最高等级图案 ×1</b>(无缺口时降级为 `PatternHigh`) | 8 | 8% | GDD「直接产出订单所需高阶物品」,针对性最强 |

权重总和 100,占比即权重(便于读)。<b>旋钮:</b> `BoxRewardWeights` 数组——调大某项权重即提高该项出率;调 `BoxEnergyGain` 改体力档。

权重设计意图:图案类(Low/Mid/High/NeededHigh)合计 80%,体力 20%。高阶物(Lv3 + NeededHigh)合计 18%,做到「常开常有小惊喜,偶尔大奖」。NeededHigh 是「针对性高阶物」,占比刻意低于通用 Lv3,避免盲盒变成订单缺口的稳定补给(那会架空合成玩法)。

<h3 id="roll">3.2 开盒算法(含保底)</h3>

用 `RandomSource`(数据层确定性随机,可单测,与 `ChestSystem.RollRewards` 同源)做加权抽样:

<pre class="code">// TarotBlindBoxConfig.RollReward(): 加权抽样掷 1 项
int total = Σ BoxRewardWeights;            // = 100
int r = RandomSource.Range(0, total);      // [0, total)
累加权重,r 落入哪个区间即抽中该 RewardKind;
返回 BlindBoxReward{ Kind, 产物等级/数量 }。</pre>

<b>保底</b>(双重):

- <b>下限保底:</b>任意一次开盒必产<b>有效奖励</b>(权重表无 0 价值项,最低也是 1 个 Lv1 图案或体力)。不存在「开了个空」。
- <b>NeededHigh 降级保底:</b>抽中 `NeededHigh` 但当前无订单缺口(`NeededTypes()` 空 / 所有订单已可交付)→ 自动降级为通用 `PatternHigh`(Lv3),不浪费这次大奖。

<h3 id="grant">3.3 产物落点与边界</h3>

| 奖项 | 落点方法(现状符号) | 类型选择 | 边界处理 |
| --- | --- | --- | --- |
| 图案 Lv1/Lv2/Lv3 | `MergeOrderState.AddDirect(type, level, 1)` | 当前订单所需类型之一(`NeededTypes()` 轮转取),空则回退 `Diamond` | AddDirect 走级联合并:产 Lv1 若该类已有 1 个 Lv1 → 自动升 Lv2(预期,非缺陷)。封顶 Lv3 不再升、堆积。 |
| NeededHigh | `AddDirect(type, level, 1)` | 缺口里<b>最高等级</b>的那一项的(类型,等级) | 无缺口 → 降级 PatternHigh(见 §3.2) |
| 体力 | `MergeOrderState.RefundEnergy(BoxEnergyGain)` | — | 受体力软上限 `EnergyCap` 约束,不溢出(与消除返还同规则) |

<div class="callout note">
    <b>为什么图案用 <code>AddDirect</code> 而非进待选区:</b>盲盒产的是「图案/收集物」,语义上属合成区(收集区)资源,直接进 <code>Inventory</code> 供合成/交付,与多消里程碑直发、全清奖、宝箱图案奖完全同源(都走 AddDirect)。不进待选区(待选区是「带元素的待落方块」,是另一条注入路径,设计 10 的 PendingElements 队列)。这条边界让盲盒产物与现有图案经济无缝合流。
  </div>

<b>开盒前置:</b> `BlindBoxCount > 0` 才可开;开盒后 `BlindBoxCount -= 1`。计数无硬上限(GDD 未限,持有式攒多少都行);若需防溢出可设软上限旋钮 `BoxHoldCap`(默认很大,如 99,达上限后获取不再增——见 §七待拍板)。

<h3 id="unlock">3.4 连消/全消解锁阈值</h3>

挂 `ClearSettlement.Settle` 结算流水线(单一信息源,所有落子后结算都经此)。在现有「连消链推进 / 全清武装位」逻辑之后追加盲盒解锁判定,<b>产出到 SettlementResult 供窗口表现,计数副作用在 Settle 内施加</b>(与全清奖、女神推进同体例):

| 触发条件 | 判据 | 盲盒产出 | 旁注 |
| --- | --- | --- | --- |
| <b>全清</b> | 发生全清且发奖(`boardEmptyAfter && AllClearArmed`,沿用现有全清武装位) | +1 盲盒 | 复用全清武装位:连续第 2 次全清不发盲盒(防刷,与全清图案奖同步) |
| <b>连消链达阈值</b> | `ComboChain == BoxComboThreshold`(默认 4,即第 4 连消那一手) | +1 盲盒 | 用 `==` 而非 `>=`:每条连消链<b>跨过阈值的那一手发一次</b>,链更长不重复发(否则 4/5/6 连消每手都发,通胀)。链断回 1 后重新计。 |

<b>逐档代入</b>(阈值默认 4):

| 连消链推进序列 | 各手 ComboChain | 发盲盒的手 |
| --- | --- | --- |
| 连续 6 手都触发消除 | 2,3,4,5,6,7(注:链长上限由倍率表封顶,链值本身继续累加) | 仅第 3 手(ComboChain 跨到 4 的那手),共 +1 |
| 3 连消后断链,再 4 连消 | 2,3,4 \| 断 \| 2,3,4,5 | 第一段第 3 手 +1、第二段第 3 手 +1,共 +2 |
| 全程无连消(每手孤立消除) | 每手都是 2,然后链断回 1 | 不发(没达到阈值 4) |

<div class="callout note">
    <b>ComboChain 的现状语义核对(已 grep 核实):</b> <code>ClearSettlement.Settle</code> 中有消除时 <code>m.ComboChain += 1</code>,无消除时归 1。开局 <code>Reset()</code> 置 1。倍率表 <code>ComboMultPermille</code> 用 5 档封顶 ×2.0,但 <code>ComboChain</code> 字段本身不封顶(<code>ComboMultPermilleFor</code> 内部 clamp 索引)。因此「<code>ComboChain == 4</code>」判定是稳的:第 4 连消那一手恰好 ComboChain 从 3 自增到 4。<b>旋钮:</b> <code>BoxComboThreshold</code>,调小更易出、调大更难。
  </div>

<h3 id="special">3.5 特殊订单附赠</h3>

GDD「紧急/特殊订单奖励含神秘塔罗盲盒」。挂 `MergeOrderState.DeliverSpecial()`:特殊订单交付成功时附赠盲盒。

<table class="tight">
    <tbody><tr><th>特殊订单类型</th><th>附赠盲盒数</th><th>旋钮</th></tr>
    <tr><td>加急(Express)</td><td><code>BoxPerExpress</code>(默认 1)</td><td rowspan="3">各 Kind 独立常量,可分别调</td></tr>
    <tr><td>剧情(Story)</td><td><code>BoxPerStory</code>(默认 2,主线奖励更重)</td></tr>
    <tr><td>黄金时段(GoldenHour)</td><td><code>BoxPerGolden</code>(默认 1)</td></tr>
  </tbody></table>

<b>实现位置抉择:</b> `DeliverSpecial` 内按 `SpecialTrack.Occupied.Kind` 查表 `BlindBoxCount += 表[kind]`。注意 `DeliverSpecial` 末尾有 `_undoStack.Clear()`(交付是已提交动作,悔棋不倒回)——附赠的盲盒计数随交付一起固化,不被悔棋倒回,符合「已交付」语义。

<div class="callout note">
    <b>现状边界(已核实):</b> <code>SpecialOrderTrack</code> 与 <code>DeliverSpecial</code> 已在数据层建成且有单测,但<b>尚未接入任何 UI 窗口</b>(<code>SpecialTrack.Request</code> 目前只在测试里被调用)。本篇的特殊订单附赠钩子是<b>数据层逻辑</b>,可单测、可落地;但「特殊订单在窗口里怎么投放/交付」是 <a href="#11-core-loop-completion::concurrency">11·§三(订单并发模型)</a> 的独立未接 UI 项,<b>不在本轮范围</b>。本轮只保证「一旦 DeliverSpecial 被调用,盲盒按表附赠」这条逻辑正确且被测覆盖。
  </div>

<h2 id="hook">四、挂接点 / dev 改动清单</h2>

符号名均经 grep 核实于当前工程。dev 照此定位,不需要重新摸索结构。

| # | 文件 / 符号 | 改动 |
| --- | --- | --- |
| 1 | `Module/BlockBlast/TarotBlindBoxConfig.cs` <span class="pill-new">新建</span> | 静态配置类:`BoxRewardWeights` / `BoxEnergyGain` / `BoxComboThreshold` / `BoxPerExpress\|Story\|Golden` / 枚举 `BlindBoxRewardKind` / 结构 `BlindBoxReward` / 方法 `RollReward(MergeOrderState)`(加权抽样 + NeededHigh 降级保底)。仿 `ChestSystem.RollRewards` 用 `RandomSource`。 |
| 2 | `MergeOrderState.cs` · 新字段 `public int BlindBoxCount;` | 在 `Reset()` 中置 0;加方法 `AddBlindBox(int)` / `bool CanOpenBlindBox` / `bool OpenBlindBox(out BlindBoxReward)`(扣 1 + 调 `TarotBlindBoxConfig.RollReward` + 按 Kind 调 `AddDirect`/`RefundEnergy` 发放,返回掷出的奖励供 UI 展示)。 |
| 3 | `MergeOrderState.cs` · `Snapshot.Capture` / `Restore` | 加 `_blindBoxCount` 字段,Capture 存、Restore 复原。<b>必须</b>——否则悔棋后计数错乱(与 `_soul`/`_goddessRating` 同体例,照抄那两行的写法)。 |
| 4 | `MergeOrderState.cs` · `DeliverSpecial()` | 在 `SpecialTrack.OnDelivered()` 之前(还能读到 `Occupied.Kind`),按 §3.5 表 `BlindBoxCount += 表[kind]`。 |
| 5 | `ClearSettlement.cs` · `Settle()` | 全清发奖分支内(`AddDirect` 全清奖之后)`m.AddBlindBox(1)`;连消分支判 `m.ComboChain == BoxComboThreshold` 则 `m.AddBlindBox(1)`。把「本手获得几个盲盒」加入 `SettlementResult`(新字段 `BlindBoxGained`)供窗口表现。 |
| 6 | `UI/BlockBlastUI/MergeOrderWindow.cs` | 顶部信息行加盲盒计数显示 + 开盒按钮(详见 §五);`PlaceAndResolve` 内读 `settle.BlindBoxGained > 0` 弹获得提示;开盒结果用内联结果条/小窗展示。 |
| 7 | `Editor/Tests/BlockBlast/TarotBlindBoxTests.cs` <span class="pill-new">新建</span> | 覆盖 §六验收点(奖池抽样确定性、保底、解锁阈值、附赠、快照回滚)。仿 `CoreLoopCompletionTests` 的 `SetUp`(InMemory Provider + `RandomSource.SetSeed`)。 |

注:`MergeOrderState` 整体目前<b>不做磁盘持久化</b>——只有 `BlockGameState.Save/Load` 持久化棋盘+待选块+分数,MergeOrderState 每局 `ResetForMergeOrder` 重建。盲盒计数跟随此现状:进悔棋快照(单局内回滚正确),不单独建磁盘序列化层。任务简报的「纳入持久化」在现状下等价于「纳入快照」——若要 MergeOrderState 全量跨会话存盘,那是独立的大改(全字段序列化),不在本轮、见 §七待拍板。

<h2 id="ui">五、UI 方案</h2>

glyph + 纯色,零美术,接现有 `MergeOrderWindow`。两块:

- <b>盲盒计数 + 开盒按钮(常驻顶部信息行):</b>在体力条/完成单数那一行旁加一个「🔮 ×N」计数 + 「开盒」按钮。`BlindBoxCount==0` 时按钮置灰(同悔棋按钮的置灰写法 `interactable = can`)。glyph 用 🔮 或 ◈,色用紫/金。
- <b>开盒结果(内联结果条):</b>点开盒 → 调 `OpenBlindBox` → 用 `BurstText.Spawn`(现状已有,全清/多消弹字同款)在棋盘上方弹一条「开出:◆ Lv3 ×1」之类,带 glyph + 色。不做独立全屏弹窗——盲盒是高频轻动作,内联弹字够且不打断节奏。获得盲盒时(连消/全清/交付)同样弹一条「+1 🔮」。

开盒结果展示用内联弹字而非模态窗口的理由:盲盒开得频繁,模态窗口每次都要点关闭会拖慢节奏;内联弹字与现有连消/多消反馈同一套表现语言,一致且零额外 prefab。若后续要做「开盒动画/仪式感」,那是表现层增强,本轮不做(见 §七)。

<div class="callout note">
    <b>刷新调用:</b>开盒/获得后须调现有 <code>RefreshSynthesis()</code>(图案进了合成区)+ <code>RefreshEnergy()</code>(可能加了体力)+ 新增 <code>RefreshBlindBox()</code>(计数与按钮态)。与现有 <code>OnDeliverClicked</code> 末尾的批量刷新同体例。
  </div>

<h2 id="accept">六、验收点(test 可逐条核对)</h2>

每条对应一个或一组单测;test 子会话编译 + 跑 EditMode(`BlockBlast.Tests`),全绿且 core-loop 基线 129 例不回归。

| # | 验收点 | 完成定义(可核对) |
| --- | --- | --- |
| A1 | 奖池加权抽样确定性 | 固定 `RandomSource.SetSeed` 后,`RollReward` 连掷 N 次的 Kind 序列可复现;各 Kind 出现频次与权重比大致吻合(大样本)。 |
| A2 | 下限保底 | 任意 seed 下 `RollReward` 返回的 Kind ∈ 有效奖项集,产物等级 ≥1 或体力 ≥1,不存在空奖。 |
| A3 | NeededHigh 降级保底 | 构造无订单缺口的 state,抽中 NeededHigh → 实际发放为 PatternHigh(Lv3);有缺口时发缺口最高等级项。 |
| A4 | 开盒扣计数 + 发放 | `BlindBoxCount=2`,`OpenBlindBox` 一次后计数=1;图案项使 `Inventory` 对应项 +1(或级联升级);体力项使 `Energy` 增(不超 `EnergyCap`)。计数=0 时 `CanOpenBlindBox` 为 false 且 `OpenBlindBox` 返回 false、计数不变。 |
| A5 | 全清解锁盲盒 | `Settle(boardEmptyAfter=true, Armed)` → `BlindBoxCount` +1 且 `SettlementResult.BlindBoxGained==1`;连续第 2 次全清(武装位已消)不再发(`BlindBoxGained==0`)。 |
| A6 | 连消阈值解锁 | 推进 ComboChain 到恰好 `BoxComboThreshold` 那一手 +1 盲盒;同一连消链继续更长不再发;链断回 1 后再达阈值重新发。逐档代入(§3.4 表)逐行核对。 |
| A7 | 无消除不发盲盒 | `Settle(lines=0)` 不增 `BlindBoxCount`。 |
| A8 | 特殊订单附赠 | 占槽各 Kind(Express/Story/Golden)交付 `DeliverSpecial` 成功 → `BlindBoxCount` 按 §3.5 表增对应数;不可交付时(库存不足)`DeliverSpecial` 返回 false 且计数不变。 |
| A9 | 悔棋快照回滚计数 | 记 `BlindBoxCount`=c → `CaptureSnapshot` → 落子触发全清使计数变 c+1 → `Undo` → 计数回 c。(与现有 Soul/Goddess 快照测试同构) |
| A10 | 旧路径零回归 | core-loop 基线 129 例全绿;无盲盒触发(无全清/未达连消阈值/不交付特殊单)时,所有现有结算结果与基线一致。 |

<h2 id="open">七、待拍板清单(交 boss / 用户)</h2>

以下为范围/取向开关,集中列出。其余数值我已按 §三默认拍板,dev 直接用。

| # | 待决项 | 我的默认取向(若无异议即按此) |
| --- | --- | --- |
| O1 | <b>盲盒产物是否含 Lv4。</b>GDD 写 Lv1–Lv4,工程封顶 Lv3 且设计 11 已收敛 5→3 级。 | 收敛到 Lv1–Lv3(本篇方案)。若 boss 要支持 Lv4,须先抬高 `MaxLevel` 并重审整个合成链(超本轮范围)。 |
| O2 | <b>持有计数是否设软上限 <code>BoxHoldCap</code>。</b> | 本轮不设硬上限(GDD 未限,持有式);留旋钮默认 99 备用。 |
| O3 | <b>MergeOrderState 是否做全量跨会话磁盘持久化。</b>现状只快照、不存盘。 | 本轮不做——跟随现状只入快照。全量存盘是独立大改,单列任务。 |
| O4 | <b>开盒表现是否要独立仪式感动画/弹窗。</b> | 本轮内联弹字(轻、不打断)。仪式感动画属表现增强,后续轮次。 |

<h2 id="risk">八、风险表</h2>

| 风险 | 后果 | 应对 |
| --- | --- | --- |
| 奖池产 Lv3/NeededHigh 占比偏高,盲盒成为订单缺口的稳定补给 | 架空合成玩法(玩家靠开盒而非合成凑单) | 高阶物合计 18%、NeededHigh 仅 8%(§3.1);全部走旋钮,实测手感后调权重。验收 A1 留频次核对口子。 |
| 连消阈值用 `>=` 误实现 → 每手都发 | 盲盒严重通胀 | 设计明写 `==` 单次跨阈发(§3.4);验收 A6 逐档代入逐行核对,专测「链更长不重复发」。 |
| 盲盒计数漏进快照 | 悔棋后计数错乱(与 Soul 早期同类隐患) | 改动清单 #3 明列必改;验收 A9 专测回滚。code review 比对 Capture/Restore 字段对称。 |
| 图案产物 AddDirect 触发的级联合并被误判为 bug | dev 误改 AddDirect 破坏现有合成 | §3.3 明示级联是预期行为;验收按级联后实际库存断言,不断言「Lv1 必为 1 个」。 |
| 特殊订单 UI 未接,附赠钩子无处触发 | 盲盒「特殊订单获取」渠道在 demo 里跑不出来,验收只能靠单测 | §3.5 明示这是数据层逻辑、UI 投放是设计 11 独立未接项;验收 A8 用单测直接调 DeliverSpecial 覆盖,不依赖 UI。 |
| MCP 桥不可达,test 无法编译/跑测 | 无法判 PASS | 按硬约束:test 判 BLOCKED 不判 FAIL,环境恢复后补测。 |
