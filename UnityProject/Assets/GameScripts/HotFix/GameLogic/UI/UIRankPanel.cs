using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.Rank;

namespace GameLogic.UI
{
    /// <summary>
    /// 排行榜窗（美术换皮，设计 28）。模态弹窗：标题 + 榜单列表 + 我的名次条 + 底部按钮。
    /// 数据走 <see cref="GameContext.Instance"/>.Rank（设计 22 数据层），切图复用 Sheet_settings 精灵表（设计 23）。
    /// 榜行底 / 名次徽章 / 头像无切图 → 占位（§3.2，本屏 art 受限）；名次 / 名 / 分始终是真实数据。
    /// </summary>
    /// <remarks>
    /// 切图寻址：<c>Image.SetSubSprite("Sheet_settings", 子图名)</c>（同设计 23 / 25，精灵表 Multiple 模式，
    /// location=Sheet_settings，子图名=源切图文件名；引用计数由框架 SubSpriteReference 自动管，无需手动释放）。
    /// 接钮一律 <c>onClick.AddListener</c>（本工程 UIModule 无 RegisterButtonClick，设计 23 已验证）。
    ///
    /// 行实现 = 代码生成行（设计 28 §5.1 方案 B / D7 退路）：prefab 只摆静态壳 + 空 Content 容器，
    /// <see cref="OnRefresh"/> 时按 <see cref="BuildRowModels"/> 产出的行 VM 列表代码生成行，1080×1920 坐标系
    /// （不套 BlockLayout 750 私有坐标系、不依赖尚未铺通的 UIWidget+prefab 收集器寻址，故 art 受限下最稳）。
    /// 「board → 行 VM 列表」抽成纯静态方法 <see cref="BuildRowModels"/> 便于 EditMode 单测（设计 28 §九 W3）。
    ///
    /// 点赞按钮默认省略（效果图无该钮，设计 28 §七 D2）；接线点见 <see cref="OnPraisePlaceholder"/> 注释。
    /// </remarks>
    [Window(UILayer.Top, location: "UIRankPanel", fullScreen: false)]
    public sealed class UIRankPanel : UIPanelMono
    {
        private const string Atlas = "Sheet_settings";   // 复用设置窗精灵表（设计 23 §三）
        private const int RankId = 1;                     // 本屏默认展示榜 id（单榜，§七 D1；多榜则换页签选中 id）

        // 引用走 Inspector 拖拽（[SerializeField]）；prefab 节点路径见 ScriptGenerator 旁注。
        // ── 关闭 / 遮罩 / 底部按钮 ──
        [SerializeField] private Button _btnMask;
        [SerializeField] private Button _btnClose;
        [SerializeField] private Button _btnBottom;
        [SerializeField] private Image _imgClose;
        [SerializeField] private Image _imgBottomBtnBg;
        [SerializeField] private Image _imgPanelBg;
        [SerializeField] private Image _imgTitleBg;

        // ── 列表 + 我的名次条容器 ──
        [SerializeField] private Transform _listRoot;     // 榜单列表容器（ScrollRect content 或固定槽父节点，§五）
        [SerializeField] private Text _textMyRank;
        [SerializeField] private Text _textMyName;
        [SerializeField] private Text _textMyScore;
        [SerializeField] private Image _imgMyAvatar;
        [SerializeField] private GameObject _myRankNode;

        // 代码生成的行实例（OnRefresh 重建，跨刷复用以免泄漏）。
        private readonly List<GameObject> _rowInstances = new List<GameObject>();

        private RankService Svc => GameContext.Instance.Rank;

        protected override void ScriptGenerator()
        {
            // 引用由 [SerializeField] 在 Inspector 拖入就位（原 FindChild 路径对照，便于校核拖线）：
            //   _btnMask        m_btn_Mask
            //   _btnClose       Root/m_btn_Close (Button)        _imgClose       Root/m_btn_Close (Image)
            //   _btnBottom      Root/m_btn_Bottom (Button)       _imgBottomBtnBg Root/m_btn_Bottom (Image)
            //   _imgPanelBg     Root/m_img_PanelBg               _imgTitleBg     Root/m_img_TitleBg
            //   _listRoot       Root/m_scroll_List/Viewport/Content
            //   _myRankNode     Root/m_node_MyRank               _imgMyAvatar    Root/m_node_MyRank/m_img_MyAvatar
            //   _textMyRank     Root/m_node_MyRank/m_text_MyRank
            //   _textMyName     Root/m_node_MyRank/m_text_MyName _textMyScore    Root/m_node_MyRank/m_text_MyScore

            // 接钮（onClick；监听随 GameObject 销毁自动清，无需手动 Remove——同设计 23 / 25）。
            // 三种关闭：遮罩（点任意处）/ X / 底部「再来一次」，均 CloseUI（底部语义 D3 默认关窗）。
            if (_btnMask != null)   _btnMask.onClick.AddListener(Close);
            if (_btnClose != null)  _btnClose.onClick.AddListener(Close);
            if (_btnBottom != null) _btnBottom.onClick.AddListener(OnBottomButton);
        }

        protected override void OnCreate()
        {
            // 一次性贴静态图（SetSubSprite 自动管引用计数，无需 OnDestroy 释放）。
            _imgTitleBg?.SetSubSprite(Atlas, "box2");
            _imgPanelBg?.SetSubSprite(Atlas, "box1");
            _imgClose?.SetSubSprite(Atlas, "icon_x");
            _imgBottomBtnBg?.SetSubSprite(Atlas, "button");
            // 榜行底 / 名次徽章 / 头像无图 → 占位（§3.2，行渲染时按名次 / IsSelf 取占位色）。
        }

        protected override void OnRefresh()
        {
            // 查榜走异步入口（设计 31 CV1）：在线发查榜 RPC（服务端权威排序），断服 / 超时回退本地源
            // （本机 + 陪榜，不阻断、不伪造全服名次）。即发即忘：渲染在回包 / 回退后做（UI 不阻塞）。
            RefreshAsync().Forget();
        }

        private async UniTask RefreshAsync()
        {
            var svc = Svc;
            // svc 为 null（未初始化）→ 渲染空；否则走异步查榜（远程优先、断服回退本地，设计 31 §四）。
            var board = svc == null ? null : await svc.GetBoardAsync(RankId);
            RenderList(board);     // 逐条渲染榜行（占位行底 / 徽章 / 头像 + 真实名次 / 名 / 分）。空榜 / null 不抛（W6）。
            RenderMyRank(board);   // 我的名次条（读 Self / SelfRank / SelfScore）。
        }

        // ════════════ 纯逻辑：board → 行 VM 列表（设计 28 §九 W3 单测锚点）════════════

        /// <summary>
        /// 榜行视图模型（渲染所需的全部派生字段，与 UGUI 解耦便于单测，设计 28 §九 W3）。
        /// </summary>
        public readonly struct RankRowVM
        {
            /// <summary>名次（真实 <see cref="RankEntry.Rank"/>，1 起）。</summary>
            public readonly int Rank;
            /// <summary>玩家展示名（占位查表前显「玩家+textId」，设计 28 §六 O6）。</summary>
            public readonly string Name;
            /// <summary>成绩文本。</summary>
            public readonly string Score;
            /// <summary>是否本机玩家（本人行高亮）。</summary>
            public readonly bool IsSelf;

            public RankRowVM(int rank, string name, string score, bool isSelf)
            {
                Rank = rank;
                Name = name;
                Score = score;
                IsSelf = isSelf;
            }
        }

        /// <summary>
        /// 把 <see cref="RankBoard"/> 映射成行 VM 列表（纯逻辑、无 UGUI 依赖，设计 28 §九 W3 单测直调）。
        /// board==null（榜不存在）/ Entries==null / 空榜 → 返回空列表（不抛，设计 28 §九 W6）。
        /// </summary>
        public static List<RankRowVM> BuildRowModels(RankBoard board)
        {
            var rows = new List<RankRowVM>();
            if (board?.Entries == null) return rows;
            foreach (var e in board.Entries)
            {
                if (e == null) continue;
                rows.Add(new RankRowVM(e.Rank, NameFor(e), e.Score.ToString(), e.IsSelf));
            }
            return rows;
        }

        /// <summary>
        /// 玩家名显示（设计 28 §六 O6 / 设计 31 §3.5）：远程源回的展示名（账号占位 <see cref="RankEntry.RemoteName"/>）优先；
        /// 否则本地源走「玩家+textId」占位。真实多语言查表 / 本地昵称替换延后。
        /// </summary>
        private static string NameFor(RankEntry e)
            => !string.IsNullOrEmpty(e?.RemoteName) ? e.RemoteName : $"玩家{e?.PlayerNameTextId ?? 0}";

        // ════════════ 渲染：列表（代码生成行，§5.1 方案 B）════════════

        /// <summary>
        /// 渲染榜单列表（设计 28 §五）：清旧行 → 按 <see cref="BuildRowModels"/> 逐行代码生成。
        /// 行底 / 名次徽章 / 头像 = 占位纯色（§3.2）；名次 / 名 / 分真实。空榜 = 无行，不抛（W6）。
        /// </summary>
        private void RenderList(RankBoard board)
        {
            ClearRows();
            if (_listRoot == null) return;

            var rows = BuildRowModels(board);
            for (int i = 0; i < rows.Count; i++)
                _rowInstances.Add(BuildRowGo(_listRoot, rows[i], i));
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rowInstances.Count; i++)
                if (_rowInstances[i] != null) Object.Destroy(_rowInstances[i]);
            _rowInstances.Clear();
        }

        /// <summary>
        /// 代码生成一行（1080 宽坐标系，行高 120）。左名次徽章占位圆 + 头像占位块 + 名 + 右分数。
        /// 行底色携带名次语义（前三金 / 银 / 铜，4 起浅米，本人行高亮暖金，§3.2）。
        /// </summary>
        private static GameObject BuildRowGo(Transform parent, RankRowVM vm, int index)
        {
            const float rowW = 760f, rowH = 120f, gap = 8f;
            var row = NewRect("Row_" + index, parent, rowW, rowH);
            row.anchoredPosition = new Vector2(0f, -(rowH + gap) * index - rowH * 0.5f);

            // 行底（占位纯色条）。
            var bg = AddImage(row, "RowBg", rowW, rowH, RowBgColor(vm));

            // 名次徽章占位圆 + 真实名次数字。
            var badge = NewRect("Badge", bg.transform, 88f, 88f);
            badge.anchoredPosition = new Vector2(-rowW * 0.5f + 60f, 0f);
            AddImage2(badge, BadgeColor(vm.Rank));
            AddText(badge, "RankNum", 88f, 88f, Vector2.zero, vm.Rank.ToString(), 40,
                Color.white, TextAnchor.MiddleCenter);

            // 头像占位块。
            var avatar = NewRect("Avatar", bg.transform, 80f, 80f);
            avatar.anchoredPosition = new Vector2(-rowW * 0.5f + 160f, 0f);
            AddImage2(avatar, StableColorFor(vm.Name.GetHashCode() ^ (vm.IsSelf ? 1 : 0)));

            // 名（左对齐，跟头像后）。
            AddText(bg.transform, "Name", 320f, rowH, new Vector2(-rowW * 0.5f + 380f, 0f),
                vm.Name, 36, vm.IsSelf ? new Color(0.20f, 0.12f, 0.04f) : new Color(0.30f, 0.22f, 0.12f),
                TextAnchor.MiddleLeft);

            // 分（右对齐，★ 前缀对位效果图分数胶囊）。
            AddText(bg.transform, "Score", 240f, rowH, new Vector2(rowW * 0.5f - 140f, 0f),
                "★" + vm.Score, 38, new Color(0.55f, 0.30f, 0.05f), TextAnchor.MiddleRight);

            return row.gameObject;
        }

        // ════════════ 渲染：我的名次条（设计 28 §6 RenderMyRank）════════════

        /// <summary>
        /// 渲染我的名次条（设计 28 §五 / §6）：board.SelfRank&gt;0 → 显名次 + 头像占位 + 名 + 分；
        /// SelfRank==0（未入榜）→ 显「--」/「未上榜」+ 当前最佳分（board.SelfScore）。board==null 同未入榜。空不抛（W6）。
        /// </summary>
        private void RenderMyRank(RankBoard board)
        {
            if (_textMyRank != null)  _textMyRank.text = MyRankText(board);
            if (_textMyName != null)  _textMyName.text = MyNameText(board);
            if (_textMyScore != null) _textMyScore.text = MyScoreText(board);
            if (_imgMyAvatar != null)
                _imgMyAvatar.color = StableColorFor((board?.Self?.PlayerNameTextId ?? 0) ^ 1);
        }

        // ── 我的名次条文本映射（纯逻辑，设计 28 §九 W3 单测直调；board==null 同未入榜，不抛 W6）──

        /// <summary>我的名次文本：入榜（SelfRank&gt;0）显名次数字；未入榜显「--」。</summary>
        public static string MyRankText(RankBoard board)
            => (board?.SelfRank ?? 0) > 0 ? board.SelfRank.ToString() : "--";

        /// <summary>我的名字文本：入榜显占位名（Self 为 null 兜底「我」）；未入榜显「未上榜」。</summary>
        public static string MyNameText(RankBoard board)
        {
            if ((board?.SelfRank ?? 0) <= 0) return "未上榜";
            return board.Self != null ? NameFor(board.Self) : "我";
        }

        /// <summary>我的成绩文本：始终显当前最佳分（board.SelfScore；未入榜也显已达成的最佳分，可能 &lt; condition）。</summary>
        public static string MyScoreText(RankBoard board) => "★" + (board?.SelfScore ?? 0L);

        // ════════════ 占位色（§3.2，补图后替 SetSubSprite 即生效）════════════

        /// <summary>行底占位色：本人行高亮暖金；前三名暖金 / 银灰 / 铜棕；4 起浅米（名次携带语义，§3.2）。</summary>
        private static Color RowBgColor(RankRowVM vm)
        {
            if (vm.IsSelf) return new Color(0.98f, 0.86f, 0.45f);   // 高亮暖金
            switch (vm.Rank)
            {
                case 1: return new Color(0.96f, 0.80f, 0.36f);      // 金
                case 2: return new Color(0.82f, 0.84f, 0.88f);      // 银
                case 3: return new Color(0.80f, 0.58f, 0.38f);      // 铜
                default: return new Color(0.95f, 0.90f, 0.80f);     // 浅米
            }
        }

        /// <summary>名次徽章占位色（前三金 / 银 / 铜，4 起暗灰，§3.2）。</summary>
        private static Color BadgeColor(int rank)
        {
            switch (rank)
            {
                case 1: return new Color(0.93f, 0.72f, 0.18f);
                case 2: return new Color(0.70f, 0.72f, 0.76f);
                case 3: return new Color(0.72f, 0.48f, 0.28f);
                default: return new Color(0.55f, 0.50f, 0.42f);
            }
        }

        /// <summary>由整数取一个稳定的占位色（同输入同色，便于人眼区分头像，§3.2，同设计 25 头像占位口径）。</summary>
        private static Color StableColorFor(int seed)
        {
            float hue = ((seed * 0.61803398875f) % 1f + 1f) % 1f;   // 黄金比散列，分布均匀
            return Color.HSVToRGB(hue, 0.55f, 0.85f);
        }

        // ════════════ UGUI 代码生成小工具（1080×1920 坐标系，center 锚点）════════════

        private static RectTransform NewRect(string name, Transform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            return rt;
        }

        /// <summary>在 parent 下新建一个铺满 parent 的纯色 Image 子节点（行底用，可作徽章 / 头像内容父）。</summary>
        private static Image AddImage(RectTransform parent, string name, float w, float h, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>给一个已存在的 RectTransform 挂纯色 Image（占位圆 / 块用，铺满自身）。</summary>
        private static void AddImage2(RectTransform target, Color color)
        {
            var img = target.gameObject.AddComponent<Image>();
            img.color = color;
        }

        private static Text AddText(Transform parent, string name, float w, float h, Vector2 pos,
            string content, int fontSize, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            var txt = go.GetComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = anchor;
            txt.fontStyle = FontStyle.Bold;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return txt;
        }

        // ════════════ 关闭 / 底部按钮 ════════════

        /// <summary>底部「再来一次」（效果图）：默认关窗回上一界面（设计 28 §七 D3 安全默认）。
        /// 备选「再开一局」= CloseUI + ShowUIAsync&lt;GameWindow&gt;（需定从哪玩法再来，本轮不接）。</summary>
        private void OnBottomButton() => Close();

        // 点赞接线点（设计 28 §6.1 / §七 D2，效果图无该钮 → 本轮默认省略）：
        // 若产品 / 后续要点赞，prefab 加 m_btn_Praise 节点，ScriptGenerator 绑定 + onClick → OnPraisePlaceholder，
        // 实做体委托 Svc.ClaimPraise(RankId) → RankClaimResult 分支提示（Success / AlreadyClaimedToday / NoReward），
        // 奖经邮件发进收件箱（不在本窗弹奖，设计 22 §3.5），末尾 OnRefresh() 刷态。窗口不自写领取逻辑（W4）。
        // private void OnPraisePlaceholder() { var r = Svc.ClaimPraise(RankId); /* switch(r.Status) ... */ OnRefresh(); }

        // 关闭走基类 UIPanelMono.Close()（= UIModule.CloseUI(GetType())，等价 CloseUI<UIRankPanel>()）。
    }
}
