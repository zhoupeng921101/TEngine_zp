# PSD2UGUI 组件化工具

工具 / 管线 · 给「PSD 直解导入」生成的扁平节点,在 Unity 端<b>一键挂上对应 UGUI 组件</b>;spriteState / fill·handle / content 等引用在 Inspector 里连。

<div class="callout note">
      <b>它解决什么:</b>
      <ul>
        <li><b>新工作流定调:</b>PSD 侧只做<b>位置 / 层级 / 切图 / 文本 / 阴影 / 描边 / 九宫格 / @PNG 合图</b>这些「机械活 + 只有导入能做」的部分;<mark>组件类型移到 Unity 端</mark>——在真相所在地点几下,比 PSD 标注更方便、好改。</li>
        <li><b>导入产物 = 扁平脚手架:</b>无组件标注的层,导入器本就输出 <code>Image</code> / <code>Text</code> / 空容器(位置·切图准确)。本工具给这些节点<b>一键挂上对应组件</b>,控件自动补 <code>Image</code> 作 targetGraphic。</li>
        <li><b>薄工具:</b>只挂组件、不猜角色、不自动连引用——<code>spriteState</code> / fill·handle / content 等<b>在 Inspector 里直接连</b>。不走重导合并(<mark class="g">Unity 为唯一真相</mark>),组件化一次性完成。</li>
      </ul>
    </div>

<h2 id="principles">一、设计原则</h2>

| 原则 | 含义 |
| --- | --- |
| <b>薄工具</b> | 只做「挂组件 + 控件补 targetGraphic」;不猜角色、不自动连引用,具体在 Inspector 收尾。 |
| <b>选区驱动</b> | 作用于 Hierarchy 当前选中节点,支持多选批量挂。 |
| <b>幂等</b> | 节点已有该组件则跳过,不重复挂。 |
| <b>不破坏</b> | 只加组件,走 `Undo`,可整体撤销。 |
| <b>透明</b> | 控制台打印给谁挂了什么、targetGraphic 连了哪个。 |

<h2 id="palette">二、调色板组件</h2>

每个组件就是一颗按钮,点一下挂到选中节点。<b>控件类自动补 <code>Image</code> 作 targetGraphic</b>。

| 分组 | 组件 |
| --- | --- |
| 控件(补 targetGraphic) | Button · Toggle · Slider · Scrollbar · InputField |
| 布局 / 滚动 | Grid(GridLayoutGroup) · 水平布局 · 垂直布局 · ScrollRect · RectMask2D · LayoutElement · ContentSizeFitter |
| 图形 | Image · RawImage · Text |

TabGroup(项目自定义 CustomTab)后续补一颗按钮即可,同样是「挂组件 + Inspector 连」。

<h2 id="model">三、工作模型:挂组件 + Inspector 连引用</h2>

放弃了「缩略图角色指派 / 自动连 spriteState / 删状态节点 / 几何推断」那一整套——<mark class="y">美术命名不可靠</mark>、自动猜角色脆且收益有限。改为<b>极简流</b>,全链路如下:

<div class="diagram">
    <svg viewBox="0 0 900 300" width="100%" xmlns="http://www.w3.org/2000/svg" font-family="-apple-system,'Segoe UI','PingFang SC','Microsoft YaHei',sans-serif" role="img" aria-label="PSD2UGUI 组件化管线流程图">
      <defs>
        <marker id="ar" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#8d96b5"></path></marker>
        <marker id="ar-b" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse"><path d="M0 0 L10 5 L0 10 z" fill="#6c8cff"></path></marker>
      </defs>
      <!-- 主链 -->
      <g text-anchor="middle">
        <rect x="20" y="50" width="190" height="78" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="115" y="84" font-size="16" font-weight="700" fill="#f2f4fc">PSD 直解导入</text>
        <text x="115" y="108" font-size="12.5" fill="#8d96b5">位置·层级·切图·文本等机械活</text>
        <rect x="252" y="50" width="190" height="78" rx="12" fill="#1e2230" stroke="#8d96b5"></rect>
        <text x="347" y="84" font-size="16" font-weight="700" fill="#f2f4fc">扁平脚手架</text>
        <text x="347" y="108" font-size="12.5" fill="#8d96b5">Image / Text / 空容器</text>
        <rect x="484" y="50" width="190" height="78" rx="12" fill="#283256" stroke="#6c8cff"></rect>
        <text x="579" y="84" font-size="16" font-weight="700" fill="#f2f4fc">一键挂组件</text>
        <text x="579" y="108" font-size="12.5" fill="#aebcf5">选中节点 → 点调色板按钮</text>
        <rect x="716" y="50" width="165" height="78" rx="12" fill="#16382c" stroke="#5bd6a0"></rect>
        <text x="798" y="84" font-size="16" font-weight="700" fill="#f2f4fc">Inspector 收尾</text>
        <text x="798" y="108" font-size="12.5" fill="#8fd0b4">手动连具体引用</text>
      </g>
      <g stroke="#8d96b5" stroke-width="1.6">
        <line x1="210" y1="89" x2="244" y2="89" marker-end="url(#ar)"></line>
        <line x1="442" y1="89" x2="476" y2="89" marker-end="url(#ar)"></line>
        <line x1="674" y1="89" x2="708" y2="89" marker-end="url(#ar)"></line>
      </g>
      <!-- 挂组件的自动附带动作 -->
      <path d="M579 128 V 168" fill="none" stroke="#6c8cff" stroke-width="1.6" stroke-dasharray="5 4" marker-end="url(#ar-b)"></path>
      <g text-anchor="middle">
        <rect x="394" y="172" width="370" height="74" rx="12" fill="#283256" stroke="#6c8cff" stroke-dasharray="5 4"></rect>
        <text x="579" y="200" font-size="13.5" font-weight="700" fill="#aebcf5">工具自动附带(详见 §四)</text>
        <text x="579" y="224" font-size="12.5" fill="#aebcf5">控件补 Image 作 targetGraphic · 按生成器前缀重命名 · 去 @ 标签</text>
      </g>
      <!-- 图例 -->
      <line x1="220" y1="280" x2="256" y2="280" stroke="#8d96b5" stroke-width="2"></line>
      <text x="264" y="285" font-size="13" fill="#8d96b5">管线推进</text>
      <line x1="380" y1="280" x2="416" y2="280" stroke="#6c8cff" stroke-width="2" stroke-dasharray="5 4"></line>
      <text x="424" y="285" font-size="13" fill="#8d96b5">挂载时自动执行</text>
      <rect x="570" y="271" width="26" height="16" rx="4" fill="#16382c" stroke="#5bd6a0"></rect>
      <text x="604" y="285" font-size="13" fill="#8d96b5">人工收尾环节</text>
    </svg>
    </div>

对应的操作只有三步:

1. Hierarchy <b>选中节点</b>(可多选)
2. 窗口里<b>点对应组件按钮</b> → 组件挂上(控件自动补 Image 作 targetGraphic)
3. 在 <b>Inspector</b> 里连具体引用(spriteState、fill/handle、content…)

<div class="callout note"><b>为什么不自动连:</b>角色识别要么靠命名(不可靠),要么靠缩略图手点(每控件多步)。直接在 Inspector 连这些引用,是程序<mark class="g">最熟、最快、最可控</mark>的路径——工具只省「挂组件 + 补 targetGraphic」这点重复劳动即可。</div>

<h2 id="rules">四、挂载规则</h2>

| 类别 | 挂载行为 |
| --- | --- |
| <b>控件</b>(Button/Toggle/Slider/Scrollbar/InputField,均 `Selectable`) | 节点若没有任何 `Graphic` → 先补一个 `Image`;再挂控件;`targetGraphic` 为空则自动指向该 Graphic。 |
| <b>布局 / 滚动 / 图形</b> | 直接挂;节点已有同类型则跳过(幂等)。 |
| <b>重命名(所有类别)</b> | 挂组件时按所选组件给节点加 <b>生成器命名前缀</b> 并<b>去掉 <code>@\*</code> 标签</b>,使节点能被项目 UI 脚本生成器绑定。 |

<h3 id="rules-naming">命名规则(已实现,对齐项目生成器)</h3>

前缀<b>实时读自 <code>ScriptGeneratorSetting</code></b>(UI 代码生成器的 `uiElementRegex` = <mark>单一来源</mark>):Button→`m_btn`、Image→`m_img`、Text→`m_text`、ScrollRect→`m_scroll`、Toggle→`m_toggle`、Slider→`m_slider`、Grid→`m_grid` 等。

挂组件时 = <b>前缀 + "\_" + 基名</b>,去 `@*` + 替换旧前缀(取<b>最长</b>匹配,`m_scrollBar` 不会被 `m_scroll` 误吞;剥离后残留分隔下划线也清掉,不会出双下划线)。例:`buy@PNG` + Button → `m_btn_buy`;`m_btn_buy` + Image → `m_img_buy`。

生成器 `ScriptGenerator.cs` 用 `name.StartsWith(uiElementRegex)` 绑定 → <mark class="g">节点名直接可用</mark>。不在规则里的组件(RectMask2D/LayoutElement/ContentSizeFitter)不改名。

<h3 id="rules-translate">中文基名 → 英文(MyMemory,免 key + 缓存)</h3>

基名含中文时,先查本地术语表缓存 `TranslationCache.json`;缺失则调 <b>MyMemory</b>(免 key/免费)翻译并写回缓存;失败/离线保留中文。窗口有开关可关。例:`购买@PNG` + Button → `m_btn_Buy`(缓存命中后即时)。

<mark class="y">翻译质量不完美</mark>(如 设置→SettingsIni、BUY 大写),缓存是<b>可手编 JSON</b>,改一次永久生效。

<h2 id="inspector">五、挂完在 Inspector 连什么(速查)</h2>

| 组件 | Inspector 里要连 / 调 |
| --- | --- |
| Button | SpriteSwap → `spriteState` 的 pressed/disabled/highlighted(targetGraphic 已自动连) |
| Toggle | `graphic` = 勾选态 Image;多选互斥再加 ToggleGroup |
| Slider | `fillRect` / `handleRect` / 方向 / 取值范围 |
| ScrollRect | `content` / `viewport` / 横竖滚动;视口配 RectMask2D |
| Grid | `cellSize` / spacing / 约束行列 |
| InputField | `textComponent` / placeholder |

<h2 id="ux">六、调用方式 / UX(仅窗口)</h2>

入口 = 停靠式 `EditorWindow「PSD2UGUI 组件化」`(菜单 <mark><code>QuickTool / PSD2UGUI 组件化</code></mark>)。

- <b>顶部</b>:选中节点的对象引用字段(跟随 Hierarchy;多选显示数量)。
- <b>分组按钮</b>:控件 / 布局·滚动 / 图形,点哪个挂哪个。
- <b>多选批量</b>:对选中的所有节点各挂一份。
- <b>Undo</b>:每次挂载进一个 Undo group,可撤销。
- <b>报告</b>:控制台打印挂载结果。

<div class="related">
      <h2>相关文档</h2>
      <div class="related-links">
        <a href="#">← 返回总览</a>
      </div>
    </div>
