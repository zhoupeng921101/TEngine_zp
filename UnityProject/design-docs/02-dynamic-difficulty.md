# 动态难度拆解

现状分析 · 游戏最核心、最值钱的系统。它让发牌「会读心」:你顺的时候偷偷加码,你快死的时候悄悄放水,永远把你按在「就差一点」的心流区。

<div class="callout note">
      <b>这套 DDA 是融合发牌的底层引擎(R4)</b>:8 算法与权重表即融合玩法的发牌底层。本篇通篇以 <code>Score</code> 为强度信号(决定 DDA 是否激活做局);融合后<mark>DDA 用哪个分量作强度信号</mark>是一个有安全默认的范围开关——默认以 <code>Score</code> 为信号(该量在融合里若恒低则 DDA 休眠在清屏窗口),可选改接经营进度量(让做局随经营推进激活)。信号源裁决与默认见 <a href="#29-gameplay-fusion::v8">29·§3.8</a> 与 <a href="#29-gameplay-fusion::decisions">29·§七#2</a>。
    </div>

<div class="callout note">
      <b>一句话原理:</b> 系统维护一个隐藏变量 <code>dynamicWeight</code>(可理解为「最近有多惯着你」的累加器)。喂你简单块 → 这个值升高 → 触发「做局」档发难块;喂你难块 → 值降低 → 触发「放水」档发简单块。<b>一根自动收紧又松开的橡皮筋。</b>
    </div>

<h2 id="dispatch">一、调度总链路</h2>

每次手牌用完补 3 块时,`DynamicWeightDiff.OfferTrio()` 按优先级从上到下决策,<mark>命中即返回</mark>。整条决策流如下图:

```mermaid
flowchart TD
    e1["手牌用完,补 3 块<br/>OfferTrio() 入口"]
    q1["① 调试强制档?<br/>ForceAlgorithm 非空"]
    q2["② 清屏窗口?<br/>分数 &lt; 15000 且窗口开启"]
    q3["③ Override 命中?<br/>开局首发 / 空盘保护"]
    q4["④ 未激活?<br/>未初始化 或 分数 &lt; 1000"]
    o1["指定算法<br/>HUD 调试用,线上不触发"]
    o2["贪心钥匙算法<br/>强制喂能清空棋盘的块(§一·2)"]
    o3["FILL / 随机无死亡<br/>首发走 FILL,空盘走随机无死(§一·3)"]
    o4["随机无死亡<br/>新手期保底,绝不卡死"]
    s5["⑤ 正式动态调度<br/>weight+分数选 tier → odds 抽算法(§二)"]
    s6["⑥ 后处理<br/>早期屏蔽 · 三块去重(§一·6)"]
    ret["返回 3 块<br/>3 个 shapeId 进手牌"]
    e1 --> q1
    q1 -->|是| o1
    q1 -->|否| q2
    q2 -->|是| o2
    q2 -->|否| q3
    q3 -->|是| o3
    q3 -->|否| q4
    q4 -->|是| o4
    q4 -->|否| s5
    s5 --> s6
    s6 --> ret
```

各级的判定细节与数值:

1. **调试强制档**(`ForceAlgorithm`):HUD 调试用,非空时所有发牌都用指定算法。线上不触发。
2. **清屏窗口**(分数 &lt; <mark>15000</mark>):进入窗口后**每回合强制喂「能清空棋盘」的方块**(贪心钥匙算法),引导你清盘。清空后**冷却 1\~2 回合**走普通调度,再开窗口 —— 制造「填满→清空」的张弛节奏。
3. <b>优先级覆盖层(Override)</b>:特定时机插队接管。当前内置两条:
  - <span class="chip good">开局首发</span> 一局第一次补块走 **FILL**,确保你一上手就能消行,建立「这游戏会消除」的预期。
  - <span class="chip good">空盘保护</span> 棋盘刚清空时走 **随机无死亡**,避免清盘后立刻甩一个死亡难题。
4. **未激活**(未初始化 或 分数 &lt; 1000):直接 **随机无死亡**,保证新手期绝不卡死。
5. **正式动态调度**:按 `dynamicWeight` + 当前分数选中一个 <b>tier(难度档)</b>,再在档内按 8 个 odds 加权抽一个算法生成方块。
6. **后处理**:分数 &lt; 10000 时套**早期屏蔽**(剔除超难大块);非清屏窗口期做**三块去重**(避免一组里出现重复图形)。

<h2 id="weight">二、核心机制:dynamicWeight 橡皮筋</h2>

每次发完一组牌、玩家落子后,按所用算法给 `dynamicWeight` 加一个增量(`AddWeight`)。<mark>增量正负就是这套 DDA 的方向盘</mark>。

<h3 id="weight-factors">调权因子表(每种算法把权重推向哪边)</h3>

| 算法 | 倾向 | 首次/换向 增量 | 同向连续 增量 |
| --- | --- | --- | --- |
| ClearAll 清盘 | <span class="chip good">放水</span> | +60 | +30 |
| Fill 填空消除 | <span class="chip good">放水</span> | +40 | +20 |
| AllCombination 全组合 | <span class="chip good">放水</span> | +35 | +15 |
| EasyDiff 简单难题 | <span class="chip good">偏易</span> | +30 | +12 |
| RandomNoDie 随机无死 | <span class="chip">中性</span> | +15 | +5 |
| Add3 熵增 | <span class="chip warn">偏难</span> | −25 | −10 |
| Diff 困难难题 | <span class="chip bad">做局</span> | −40 | −15 |
| StraightDeathDiff 死亡难题 | <span class="chip bad">做局</span> | −80 | −30 |

<div class="callout note">
      「同向连续」增量比首次小:连续放水/连续做局时<b>力度衰减</b>,防止权重一路冲到底,让橡皮筋更柔和。权重最终会被 clamp 到配置表覆盖的真实区间内。
    </div>

<h3 id="weight-loop">闭环是怎么转起来的</h3>

关键在 **tier 表的设计**:`dynamicWeight` 越高,档内越偏向发**难块**。于是形成<mark class="g">自动负反馈</mark>,循环如下:

```mermaid
flowchart LR
    n1["玩家顺:一直喂简单块<br/>Fill / ClearAll 等放水算法"]
    n2["dynamicWeight 升高<br/>简单块 = 正增量(§二 因子表)"]
    n3["切高权重档:狂发难块<br/>Diff / 死亡难题等做局算法"]
    n4["dynamicWeight 回落<br/>难块 = 负增量,玩家被卡住"]
    n1 -->|每发一组都在累加| n2
    n2 -->|权重 ≥ 10:翻脸进做局档| n3
    n3 -->|连吃难块,权重被拉低| n4
    n4 -->|权重 &lt; 10:切回放水档| n1
    note["负反馈闭环 · 临界点 dynamicWeight ≈ 10<br/>永远把玩家按在心流区"]
```

临界点大约在 <mark><code>dynamicWeight = 10</code></mark>:低于它是「放水档」,达到/超过它就翻脸进「做局档」。下表是**从真实配置表解出**的第 1 分数段(1000–3795 分)数据,看「做局占比」如何随权重陡然翻倍:

| dynamicWeight 区间 | 做局占比\* | 体感 |
| --- | --- | --- |
| ≤ −410 | 28% | <span class="chip good">放水</span> |
| −290 \~ −225 | 26% | <span class="chip good">放水</span> |
| −105 \~ −70 | 19% | <span class="chip good">大放水</span> |
| −10 \~ 10 | 18% | <span class="chip good">最舒服</span> |
| 10 \~ 30 | <b>73%</b> | <span class="chip bad">翻脸做局</span> |
| 50 \~ 70 | 74% | <span class="chip bad">做局</span> |
| 90 \~ 130 | 74% | <span class="chip bad">做局</span> |
| ≥ 230 | <b>92%</b> | <span class="chip bad">往死里整</span> |

\* 做局占比 = (熵增+困难+死亡难题) odds / 全部 odds。权重过 10 这条线,占比从 \~18% 直接跳到 \~73%,这就是橡皮筋「翻脸」的瞬间。

<h2 id="algos">三、8 种发牌算法逐一拆解</h2>

每种算法都返回 3 个 `shapeId`。难题类用「<mark>位棋盘启发式 + 蒙特卡洛采样</mark>」找最难的组合,放水类则找最好消的组合。

| # | 算法 | 目标 | 实现思路(简) | 采样次数 |
| --- | --- | --- | --- | --- |
| 0 | Fill 填空消除 | <span class="chip good">放水</span> | 先找「快填满的行/列」,针对缺口定制线条钥匙块,凑出能消最多格的组合 | 80 |
| 1 | RandomNoDie 随机无死 | <span class="chip">保底</span> | 纯随机抽,只要保证三块都放得下(不逼死) | 50 |
| 2 | Add3 熵增 | <span class="chip warn">偏难</span> | 挑让棋盘「最乱/碎片最多」的组合(最大化熵) | 60 |
| 3 | EasyDiff 简单难题 | <span class="chip warn">偏难</span> | 控制解法数量落在 5\~30 这个「要想但想得出」的甜区 | 80 |
| 4 | Diff 困难难题 | <span class="chip bad">做局</span> | 找最大空矩形塞大锚块,逼到解法数 ≤ 几种 | 160 |
| 5 | StraightDeathDiff 死亡难题 | <span class="chip bad">做局</span> | 双锚块猛攻,专门找「只有唯一解或直接逼死」的组合 | 320 |
| 6 | ClearAll 清盘 Plus | <span class="chip good">放水</span> | 棋盘格≥10 时,找一组能**一次清空全盘**的方块(大爽点) | 120 |
| 7 | AllCombination 全组合 | <span class="chip good">放水</span> | 穷举落点找能消最多行列的组合(比 Fill 更激进) | 150 |

<div class="callout note">
      采样次数 = 算法的「努力程度」。<b>死亡难题 320 次</b>采样最舍得算力,因为「逼死你」比「放水」更难找到合适组合。这些数字目前<mark class="y">硬编码</mark>在 <code>BlockAlgorithms.cs</code>,<b>是一个可外置到配置表的调参入口</b>。
    </div>

<h2 id="config">四、配置表结构(weightcfg)</h2>

真实权重表 `block_tbweightcfg.bytes`(Luban 生成),共 <mark>136 行 = 8 个分数段 × 17 个权重档</mark>。每行 13 个字段:tier id、8 种算法 odds、分数上下限、factor 区间上下限。

<h3 id="config-segments">8 个分数段</h3>

| 段 | 分数范围 | 含义 |
| --- | --- | --- |
| 1 | 1000 – 3795 | 动态难度激活起点 |
| 2 | 3795 – 4597 |  |
| 3 | 4597 – 6460 |  |
| 4 | 6460 – 9265 |  |
| 5 | 9265 – 11802 |  |
| 6 | 11802 – 14233 |  |
| 7 | 14233 – 21872 | 跨过 15000,清屏窗口退场 |
| 8 | 21872 – 426964 | 高分长尾 |

有意思的是:**每个分数段的橡皮筋幅度几乎一致**——最低权重档约 27\~30% 做局,最高权重档约 92% 做局。

也就是说<mark>难度主要靠 dynamicWeight 横向调,而非靠分数纵向堆</mark>。分数段更多是微调与上限控制。

<h3 id="config-odds">第 1 段完整 odds 表(按权重档从低到高)</h3>

| 权重区间 | Fill | Random | Entropy | Easy | Hard | Intuition | ClearBoard | AllUnite |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ≤ −410 | 299 | 55 | 75 | 0 | 203 | 0 | 49 | 319 |
| −290 \~ −225 | 503 | 28 | 63 | 0 | 192 | 0 | 58 | 156 |
| −105 \~ −70 | 715 | 9 | 38 | 0 | 149 | 0 | 36 | 53 |
| −10 \~ 10 | 770 | 2 | 37 | 0 | 146 | 0 | 20 | 25 |
| 10 \~ 30 | 156 | 10 | 40 | 1 | **694** | 0 | 0 | 99 |
| 50 \~ 70 | 162 | 9 | 35 | 0 | **707** | 0 | 1 | 86 |
| 90 \~ 130 | 159 | 8 | 33 | 0 | **709** | 0 | 0 | 91 |
| ≥ 230 | 45 | 3 | 8 | 0 | **908** | 0 | 0 | 36 |

odds 是相对权重(不必归一)。注意 **Easy 和 Intuition 这两列几乎全 0**——线上实际只用了 6 种算法,EasyDiff / StraightDeathDiff 基本被雪藏,难度主要靠 Hard(困难难题)一列扛。权重低时 Fill 唱主角(放水),权重一过 10,Hard 直接飙到 700+ 接管(做局)。

<h2 id="review">五、策划视角点评 &amp; 可调参数</h2>

<div class="callout good"><b>设计亮点:</b> 用一个隐藏标量 + 一张二维表(分数 × 权重)就实现了<mark class="g">平滑、自适应、玩家无感的动态难度</mark>,且把「难/易」拆成 8 种可独立调权的具体手法,调参空间极大。</div>

<div class="callout warn"><b>可优化 / 可调点:</b>
      <ul>
        <li><b>EasyDiff / StraightDeathDiff / Intuition 列几乎全 0</b>——做了算法却没在表里启用,难度梯度其实只有「Fill ↔ Hard」两极。可考虑用上 EasyDiff 做更细腻的过渡档。</li>
        <li><b>翻脸太陡</b>:权重过 10 这条线,做局占比<mark class="r">从 18% 直接跳到 73%</mark>,体感可能是「突然变态」。可在 10~30 档加一个中间梯度。</li>
        <li><b>采样次数硬编码</b>在代码里(死亡难题 320 次),建议外置到配置表,方便按机型性能/难度调。</li>
        <li>难度几乎只靠 dynamicWeight 调,<b>8 个分数段区分度很低</b>——高分玩家和中分玩家体验曲线几乎一样,可考虑让高分段整体更毒。</li>
      </ul>
    </div>

<div class="related">
      <h2>相关文档</h2>
      <div class="related-links">
        <a href="#">← 返回总览</a>
        <a href="#01-gameplay-overview">玩法总览</a>
      </div>
    </div>
