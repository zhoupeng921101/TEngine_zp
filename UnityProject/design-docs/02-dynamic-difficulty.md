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

<div class="diagram">
    <svg viewBox="0 0 880 660" width="100%" xmlns="http://www.w3.org/2000/svg" font-family="-apple-system,'Segoe UI','PingFang SC','Microsoft YaHei',sans-serif" role="img" aria-label="发牌调度决策流程图">
      <defs>
        <marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#8d96b5"></path></marker>
        <marker id="ar-g" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#5bd6a0"></path></marker>
        <marker id="ar-b" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#6c8cff"></path></marker>
      </defs>
      <!-- 左列:决策链 -->
      <g text-anchor="middle">
        <rect x="30" y="14" width="250" height="64" rx="12" fill="#283256" stroke="#6c8cff"></rect>
        <text x="155" y="42" font-size="15" font-weight="700" fill="#f2f4fc">手牌用完,补 3 块</text>
        <text x="155" y="62" font-size="12" fill="#aebcf5">OfferTrio() 入口</text>
        <rect x="30" y="118" width="250" height="64" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="155" y="146" font-size="15" font-weight="700" fill="#f2f4fc">① 调试强制档?</text>
        <text x="155" y="166" font-size="12" fill="#8d96b5">ForceAlgorithm 非空</text>
        <rect x="30" y="222" width="250" height="64" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="155" y="250" font-size="15" font-weight="700" fill="#f2f4fc">② 清屏窗口?</text>
        <text x="155" y="270" font-size="12" fill="#8d96b5">分数 &lt; 15000 且窗口开启</text>
        <rect x="30" y="326" width="250" height="64" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="155" y="354" font-size="15" font-weight="700" fill="#f2f4fc">③ Override 命中?</text>
        <text x="155" y="374" font-size="12" fill="#8d96b5">开局首发 / 空盘保护</text>
        <rect x="30" y="430" width="250" height="64" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="155" y="458" font-size="15" font-weight="700" fill="#f2f4fc">④ 未激活?</text>
        <text x="155" y="478" font-size="12" fill="#8d96b5">未初始化 或 分数 &lt; 1000</text>
      </g>
      <!-- 左列向下箭头(否) -->
      <g stroke="#8d96b5" stroke-width="1.6">
        <line x1="155" y1="78" x2="155" y2="110" marker-end="url(#ar)"></line>
        <line x1="155" y1="182" x2="155" y2="214" marker-end="url(#ar)"></line>
        <line x1="155" y1="286" x2="155" y2="318" marker-end="url(#ar)"></line>
        <line x1="155" y1="390" x2="155" y2="422" marker-end="url(#ar)"></line>
        <line x1="155" y1="494" x2="155" y2="540" marker-end="url(#ar)"></line>
      </g>
      <g font-size="12" fill="#8d96b5">
        <text x="168" y="204">否</text>
        <text x="168" y="308">否</text>
        <text x="168" y="412">否</text>
        <text x="168" y="522">否</text>
      </g>
      <!-- 右列:命中出口 -->
      <g text-anchor="middle">
        <rect x="520" y="118" width="330" height="64" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="685" y="146" font-size="15" font-weight="700" fill="#f2f4fc">指定算法</text>
        <text x="685" y="166" font-size="12" fill="#8d96b5">HUD 调试用,线上不触发</text>
        <rect x="520" y="222" width="330" height="64" rx="12" fill="#16382c" stroke="#5bd6a0"></rect>
        <text x="685" y="250" font-size="15" font-weight="700" fill="#f2f4fc">贪心钥匙算法</text>
        <text x="685" y="270" font-size="12" fill="#8fd0b4">强制喂「能清空棋盘」的块(§一·2)</text>
        <rect x="520" y="326" width="330" height="64" rx="12" fill="#16382c" stroke="#5bd6a0"></rect>
        <text x="685" y="354" font-size="15" font-weight="700" fill="#f2f4fc">FILL / 随机无死亡</text>
        <text x="685" y="374" font-size="12" fill="#8fd0b4">首发走 FILL,空盘走随机无死(§一·3)</text>
        <rect x="520" y="430" width="330" height="64" rx="12" fill="#16382c" stroke="#5bd6a0"></rect>
        <text x="685" y="458" font-size="15" font-weight="700" fill="#f2f4fc">随机无死亡</text>
        <text x="685" y="478" font-size="12" fill="#8fd0b4">新手期保底,绝不卡死</text>
      </g>
      <!-- 命中横向箭头(是) -->
      <g stroke="#5bd6a0" stroke-width="1.6">
        <line x1="280" y1="150" x2="512" y2="150" marker-end="url(#ar-g)"></line>
        <line x1="280" y1="254" x2="512" y2="254" marker-end="url(#ar-g)"></line>
        <line x1="280" y1="358" x2="512" y2="358" marker-end="url(#ar-g)"></line>
        <line x1="280" y1="462" x2="512" y2="462" marker-end="url(#ar-g)"></line>
      </g>
      <g font-size="12" fill="#5bd6a0" text-anchor="middle">
        <text x="396" y="142">是</text>
        <text x="396" y="246">是</text>
        <text x="396" y="350">是</text>
        <text x="396" y="454">是</text>
      </g>
      <!-- 底行:动态调度 → 后处理 → 出牌 -->
      <g text-anchor="middle">
        <rect x="30" y="548" width="260" height="64" rx="12" fill="#283256" stroke="#6c8cff"></rect>
        <text x="160" y="576" font-size="15" font-weight="700" fill="#f2f4fc">⑤ 正式动态调度</text>
        <text x="160" y="596" font-size="12" fill="#aebcf5">weight+分数选 tier → odds 抽算法(§二)</text>
        <rect x="340" y="548" width="250" height="64" rx="12" fill="#3a331c" stroke="#ffcf5c"></rect>
        <text x="465" y="576" font-size="15" font-weight="700" fill="#f2f4fc">⑥ 后处理</text>
        <text x="465" y="596" font-size="12" fill="#e8d49a">早期屏蔽 · 三块去重(§一·6)</text>
        <rect x="640" y="548" width="210" height="64" rx="12" fill="#283256" stroke="#6c8cff"></rect>
        <text x="745" y="576" font-size="15" font-weight="700" fill="#f2f4fc">返回 3 块</text>
        <text x="745" y="596" font-size="12" fill="#aebcf5">3 个 shapeId 进手牌</text>
      </g>
      <g stroke="#6c8cff" stroke-width="1.6">
        <line x1="290" y1="580" x2="332" y2="580" marker-end="url(#ar-b)"></line>
        <line x1="590" y1="580" x2="632" y2="580" marker-end="url(#ar-b)"></line>
      </g>
      <!-- 图例 -->
      <g font-size="13" fill="#8d96b5">
        <line x1="60" y1="644" x2="96" y2="644" stroke="#8d96b5" stroke-width="2"></line>
        <text x="104" y="649">未命中,继续向下</text>
        <line x1="250" y1="644" x2="286" y2="644" stroke="#5bd6a0" stroke-width="2"></line>
        <text x="294" y="649">命中即返回</text>
        <line x1="420" y1="644" x2="456" y2="644" stroke="#6c8cff" stroke-width="2"></line>
        <text x="464" y="649">兜底主流程</text>
      </g>
    </svg>
    </div>

各级的判定细节与数值:

1. <b>调试强制档</b>(`ForceAlgorithm`):HUD 调试用,非空时所有发牌都用指定算法。线上不触发。
2. <b>清屏窗口</b>(分数 &lt; <mark>15000</mark>):进入窗口后<b>每回合强制喂「能清空棋盘」的方块</b>(贪心钥匙算法),引导你清盘。清空后<b>冷却 1\~2 回合</b>走普通调度,再开窗口 —— 制造「填满→清空」的张弛节奏。
3. <b>优先级覆盖层(Override)</b>:特定时机插队接管。当前内置两条:
  - <span class="chip good">开局首发</span> 一局第一次补块走 <b>FILL</b>,确保你一上手就能消行,建立「这游戏会消除」的预期。
  - <span class="chip good">空盘保护</span> 棋盘刚清空时走 <b>随机无死亡</b>,避免清盘后立刻甩一个死亡难题。
4. <b>未激活</b>(未初始化 或 分数 &lt; 1000):直接 <b>随机无死亡</b>,保证新手期绝不卡死。
5. <b>正式动态调度</b>:按 `dynamicWeight` + 当前分数选中一个 <b>tier(难度档)</b>,再在档内按 8 个 odds 加权抽一个算法生成方块。
6. <b>后处理</b>:分数 &lt; 10000 时套<b>早期屏蔽</b>(剔除超难大块);非清屏窗口期做<b>三块去重</b>(避免一组里出现重复图形)。

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

关键在 <b>tier 表的设计</b>:`dynamicWeight` 越高,档内越偏向发<b>难块</b>。于是形成<mark class="g">自动负反馈</mark>,循环如下:

<div class="diagram">
    <svg viewBox="0 0 880 460" width="100%" xmlns="http://www.w3.org/2000/svg" font-family="-apple-system,'Segoe UI','PingFang SC','Microsoft YaHei',sans-serif" role="img" aria-label="dynamicWeight 负反馈回路图">
      <defs>
        <marker id="lr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#8d96b5"></path></marker>
        <marker id="lr-r" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#ff7a8a"></path></marker>
        <marker id="lr-g" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#5bd6a0"></path></marker>
      </defs>
      <!-- 顶:放水 -->
      <g text-anchor="middle">
        <rect x="320" y="16" width="240" height="68" rx="12" fill="#16382c" stroke="#5bd6a0"></rect>
        <text x="440" y="44" font-size="15" font-weight="700" fill="#f2f4fc">玩家顺:一直喂简单块</text>
        <text x="440" y="64" font-size="12" fill="#8fd0b4">Fill / ClearAll 等放水算法</text>
        <!-- 右:权重升 -->
        <rect x="620" y="190" width="240" height="68" rx="12" fill="#283256" stroke="#6c8cff"></rect>
        <text x="740" y="218" font-size="15" font-weight="700" fill="#f2f4fc">dynamicWeight 升高</text>
        <text x="740" y="238" font-size="12" fill="#aebcf5">简单块 = 正增量(§二 因子表)</text>
        <!-- 底:做局 -->
        <rect x="320" y="364" width="240" height="68" rx="12" fill="#3a1f26" stroke="#ff7a8a"></rect>
        <text x="440" y="392" font-size="15" font-weight="700" fill="#ffb3bd">切高权重档:狂发难块</text>
        <text x="440" y="412" font-size="12" fill="#d49aa4">Diff / 死亡难题等做局算法</text>
        <!-- 左:权重落 -->
        <rect x="20" y="190" width="240" height="68" rx="12" fill="#283256" stroke="#6c8cff"></rect>
        <text x="140" y="218" font-size="15" font-weight="700" fill="#f2f4fc">dynamicWeight 回落</text>
        <text x="140" y="238" font-size="12" fill="#aebcf5">难块 = 负增量,玩家被卡住</text>
        <!-- 中心 -->
        <text x="440" y="212" font-size="14" font-weight="700" fill="#f2f4fc">负反馈闭环</text>
        <text x="440" y="234" font-size="12.5" fill="#8d96b5">临界点 dynamicWeight ≈ 10</text>
        <text x="440" y="254" font-size="12.5" fill="#8d96b5">永远把玩家按在心流区</text>
      </g>
      <!-- 顺时针箭头 -->
      <path d="M562 50 Q 740 60 740 182" fill="none" stroke="#8d96b5" stroke-width="1.8" marker-end="url(#lr)"></path>
      <path d="M740 260 Q 740 388 568 396" fill="none" stroke="#ff7a8a" stroke-width="1.8" marker-end="url(#lr-r)"></path>
      <path d="M318 396 Q 140 388 140 266" fill="none" stroke="#8d96b5" stroke-width="1.8" marker-end="url(#lr)"></path>
      <path d="M140 182 Q 140 60 312 52" fill="none" stroke="#5bd6a0" stroke-width="1.8" marker-end="url(#lr-g)"></path>
      <g font-size="12.5" text-anchor="middle">
        <text x="722" y="120" fill="#8d96b5">每发一组都在累加</text>
        <text x="712" y="346" fill="#ff7a8a">权重 ≥ 10:翻脸进做局档</text>
        <text x="158" y="346" fill="#8d96b5">连吃难块,权重被拉低</text>
        <text x="166" y="120" fill="#5bd6a0">权重 &lt; 10:切回放水档</text>
      </g>
      <!-- 图例 -->
      <g font-size="13" fill="#8d96b5">
        <line x1="240" y1="452" x2="276" y2="452" stroke="#8d96b5" stroke-width="2"></line>
        <text x="284" y="457">权重累加流转</text>
        <line x1="410" y1="452" x2="446" y2="452" stroke="#ff7a8a" stroke-width="2"></line>
        <text x="454" y="457">过临界,转做局</text>
        <line x1="590" y1="452" x2="626" y2="452" stroke="#5bd6a0" stroke-width="2"></line>
        <text x="634" y="457">回临界下,转放水</text>
      </g>
    </svg>
    </div>

临界点大约在 <mark><code>dynamicWeight = 10</code></mark>:低于它是「放水档」,达到/超过它就翻脸进「做局档」。下表是<b>从真实配置表解出</b>的第 1 分数段(1000–3795 分)数据,看「做局占比」如何随权重陡然翻倍:

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
| 6 | ClearAll 清盘 Plus | <span class="chip good">放水</span> | 棋盘格≥10 时,找一组能<b>一次清空全盘</b>的方块(大爽点) | 120 |
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

有意思的是:<b>每个分数段的橡皮筋幅度几乎一致</b>——最低权重档约 27\~30% 做局,最高权重档约 92% 做局。

也就是说<mark>难度主要靠 dynamicWeight 横向调,而非靠分数纵向堆</mark>。分数段更多是微调与上限控制。

<h3 id="config-odds">第 1 段完整 odds 表(按权重档从低到高)</h3>

| 权重区间 | Fill | Random | Entropy | Easy | Hard | Intuition | ClearBoard | AllUnite |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ≤ −410 | 299 | 55 | 75 | 0 | 203 | 0 | 49 | 319 |
| −290 \~ −225 | 503 | 28 | 63 | 0 | 192 | 0 | 58 | 156 |
| −105 \~ −70 | 715 | 9 | 38 | 0 | 149 | 0 | 36 | 53 |
| −10 \~ 10 | 770 | 2 | 37 | 0 | 146 | 0 | 20 | 25 |
| 10 \~ 30 | 156 | 10 | 40 | 1 | <b>694</b> | 0 | 0 | 99 |
| 50 \~ 70 | 162 | 9 | 35 | 0 | <b>707</b> | 0 | 1 | 86 |
| 90 \~ 130 | 159 | 8 | 33 | 0 | <b>709</b> | 0 | 0 | 91 |
| ≥ 230 | 45 | 3 | 8 | 0 | <b>908</b> | 0 | 0 | 36 |

odds 是相对权重(不必归一)。注意 <b>Easy 和 Intuition 这两列几乎全 0</b>——线上实际只用了 6 种算法,EasyDiff / StraightDeathDiff 基本被雪藏,难度主要靠 Hard(困难难题)一列扛。权重低时 Fill 唱主角(放水),权重一过 10,Hard 直接飙到 700+ 接管(做局)。

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
