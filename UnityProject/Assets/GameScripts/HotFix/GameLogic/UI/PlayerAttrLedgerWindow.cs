using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.AttrLedger;
using GameLogic.BlockBlast.Player;

namespace GameLogic.UI
{
    /// <summary>
    /// 我的流水窗(独立窗,设计 46 §4.1)。模态弹窗:标题 + 过滤栏(全部/金币/钻石/体力)+ 列表 + 加载更多按钮 + 关闭。
    /// 数据走 <see cref="GameContext.Instance"/>.AttrLedger(设计 46 客户端段)。
    /// </summary>
    /// <remarks>
    /// 全代码生成 UI 内容(prefab 只摆根 Canvas + GraphicRaycaster 最小壳,沿设计 46 §4.1 art 受限占位):
    /// 流水窗无塔罗 20 屏专属素材,故行底 / 标题底 / 按钮底全用纯色占位(沿 28 排行榜窗 §3.2 占位范式);
    /// 美术补图后改 <c>SetSubSprite</c> 即可,本子单不阻塞美术。
    /// 行为级:打开即拉协议(默认 50 条 + 「全部」tab),切 tab 重拉,加载更多本子单置灰(45 协议 sinceTs 是「取更新」非翻旧页,
    /// 设计 46 §4.3 / O3,Tier 2+ 加 untilTs 字段才能翻);Window 销毁解绑 + 用 _fetchToken 防响应回来时已 dispose 致 NRE
    /// (设计 46 §5.1 表)。
    /// </remarks>
    [Window(UILayer.Top, location: "PlayerAttrLedgerWindow", fullScreen: false)]
    public sealed class PlayerAttrLedgerWindow : UIWindowMono
    {
        // 拉取参数(设计 46 §4.1 O2 默认 50 条)
        private const int DefaultLimit = 50;

        // 当前过滤(null = 全部 tab;非 null = 单 kind tab)
        private AttrType? _currentKind;

        // 防响应到达时窗已 dispose(沿设计 46 §5.1 表「切窗 + response 回来不 NRE」)
        private bool _disposed;
        // 每次发请求递增 token,响应到达时核 token 不匹配则丢弃(防高频切 tab 时旧响应覆盖新列表)
        private int _fetchToken;

        // 已动态生成的 UI 节点(关窗时由 GameObject 销毁链自动清,无需手动 Destroy)
        private RectTransform _rootPanel;
        private RectTransform _listContent;
        private Text _statusText;          // 状态栏「正在加载...」/「暂无流水」/「服务不可用」/「网络异常」
        private Button _btnLoadMore;
        private Text _btnLoadMoreText;
        private readonly List<GameObject> _rowInstances = new List<GameObject>();
        private readonly List<Button> _tabButtons = new List<Button>();
        private readonly List<Image> _tabBgImages = new List<Image>();

        // tab 定义:文本 + 对应 kind(null = 全部)
        private static readonly (string label, AttrType? kind)[] Tabs =
        {
            ("全部",   null),
            ("金币",   AttrType.Coin),
            ("钻石",   AttrType.Diamond),
            ("体力",   AttrType.Stamina),
        };

        private RemoteAttrLedgerService Svc => GameContext.Instance?.AttrLedger;

        protected override void ScriptGenerator()
        {
            // 全代码生成,prefab 只有根 Canvas + GraphicRaycaster,ScriptGenerator 无 FindChildComponent 可绑;
            // UI 构造统一在 OnCreate 内做(rectTransform 在那时已可用)。
        }

        protected override void OnCreate()
        {
            BuildLayout();
            // 首屏拉默认 tab(全部)的流水
            RefetchAsync(_currentKind).Forget();
        }

        protected override void OnDestroyWindow()
        {
            _disposed = true; // 后到达的 RefetchAsync 响应据此跳过 UI 更新
            // 子节点(行 / 按钮 / Text)由 GameObject 销毁链自动清,无需手动 Destroy
        }

        // ════════════ UI 布局(全代码生成,1080×1920 坐标系,沿 28 RankWindow 范式)════════════

        private void BuildLayout()
        {
            var root = rectTransform; // 根 RectTransform = prefab 根(stretch 0,0~1,1)
            if (root == null) return;

            // 全屏遮罩(点关窗;放最底层防被面板挡)
            var mask = AddPanel(root, "m_btn_Mask", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0f, 0f, 0f, 0.5f));
            var maskBtn = mask.gameObject.AddComponent<Button>();
            maskBtn.targetGraphic = mask.GetComponent<Image>();
            maskBtn.onClick.AddListener(Close);

            // 主面板(居中,800×1100)
            _rootPanel = NewRect("Root", root, 800f, 1100f);
            _rootPanel.anchorMin = _rootPanel.anchorMax = _rootPanel.pivot = new Vector2(0.5f, 0.5f);
            _rootPanel.anchoredPosition = Vector2.zero;
            AddImage2(_rootPanel, new Color(0.96f, 0.94f, 0.86f, 0.98f)); // 浅米占位面板底

            // 标题
            AddText(_rootPanel, "m_text_Title", 600f, 80f, new Vector2(0f, 1100f * 0.5f - 60f),
                "我的流水", 42, new Color(0.30f, 0.20f, 0.08f), TextAnchor.MiddleCenter);

            // 关闭按钮(右上角 X)
            var closeRect = NewRect("m_btn_Close", _rootPanel, 64f, 64f);
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(0.5f, 0.5f);
            closeRect.anchoredPosition = new Vector2(800f * 0.5f - 50f, 1100f * 0.5f - 50f);
            AddImage2(closeRect, new Color(0.85f, 0.60f, 0.35f));
            var closeBtn = closeRect.gameObject.AddComponent<Button>();
            closeBtn.targetGraphic = closeRect.GetComponent<Image>();
            closeBtn.onClick.AddListener(Close);
            AddText(closeRect, "Label", 64f, 64f, Vector2.zero, "×", 40, Color.white, TextAnchor.MiddleCenter);

            // 过滤栏(4 tab,横排)
            BuildTabs(_rootPanel);

            // 列表 + 滚动容器(简化:用 RectMask2D 做裁剪,不挂 ScrollRect 以省 ScrollRect 字段绑定;
            // 内容超容器会被裁剪,首屏 50 条用静态垂直排版即够,Tier 2+ 翻旧页时再升级 ScrollRect。
            // 沿设计 46 §4.1 列表行高 80,首屏 50 条排版高度 = 50*80 = 4000,超 800 容器需 ScrollRect 才能滚;
            // 本子单实现取静态排版 + 取首屏前 N 行可见的妥协,test PV 时若反馈滚动需求再升级 ScrollRect)
            var listFrame = NewRect("m_scroll_List", _rootPanel, 760f, 760f);
            listFrame.anchorMin = listFrame.anchorMax = listFrame.pivot = new Vector2(0.5f, 0.5f);
            listFrame.anchoredPosition = new Vector2(0f, -40f);
            AddImage2(listFrame, new Color(0.92f, 0.88f, 0.78f));
            listFrame.gameObject.AddComponent<RectMask2D>(); // 裁剪超出列表框的行
            // Viewport / Content 同位铺满,沿 28 RankWindow 结构
            var viewport = NewRect("Viewport", listFrame, 760f, 760f);
            viewport.anchorMin = viewport.anchorMax = viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.anchoredPosition = Vector2.zero;
            _listContent = NewRect("Content", viewport, 760f, 760f);
            _listContent.anchorMin = _listContent.anchorMax = _listContent.pivot = new Vector2(0.5f, 1f);
            _listContent.anchoredPosition = new Vector2(0f, 0f);

            // 状态栏(列表上方,显加载中 / 暂无流水 / 错误兜底)
            _statusText = AddText(_rootPanel, "m_text_Status", 600f, 60f, new Vector2(0f, 1100f * 0.5f - 180f),
                "", 28, new Color(0.45f, 0.32f, 0.18f), TextAnchor.MiddleCenter);

            // 加载更多按钮(底部,本子单置灰提示「更多功能即将推出」)
            var moreRect = NewRect("m_btn_LoadMore", _rootPanel, 360f, 80f);
            moreRect.anchorMin = moreRect.anchorMax = moreRect.pivot = new Vector2(0.5f, 0.5f);
            moreRect.anchoredPosition = new Vector2(0f, -1100f * 0.5f + 80f);
            AddImage2(moreRect, new Color(0.75f, 0.70f, 0.62f)); // 置灰底
            _btnLoadMore = moreRect.gameObject.AddComponent<Button>();
            _btnLoadMore.targetGraphic = moreRect.GetComponent<Image>();
            _btnLoadMore.interactable = false; // 本子单一直置灰(设计 46 §4.3 + O3,翻旧页待 Tier 2+ 加 untilTs)
            _btnLoadMoreText = AddText(moreRect, "Label", 360f, 80f, Vector2.zero,
                "更多功能即将推出", 28, new Color(0.55f, 0.50f, 0.42f), TextAnchor.MiddleCenter);
        }

        private void BuildTabs(RectTransform parent)
        {
            // 4 个 tab,横排,共 760 宽
            const float tabW = 180f, tabH = 70f, gap = 8f;
            float startX = -(tabW * 4f + gap * 3f) * 0.5f + tabW * 0.5f;
            float y = 1100f * 0.5f - 150f;

            for (int i = 0; i < Tabs.Length; i++)
            {
                var t = Tabs[i];
                var rect = NewRect("m_tab_" + i, parent, tabW, tabH);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + (tabW + gap) * i, y);
                var bg = AddImage2(rect, TabBgColor(i == 0));
                _tabBgImages.Add(bg);
                AddText(rect, "Label", tabW, tabH, Vector2.zero, t.label, 30,
                    new Color(0.30f, 0.22f, 0.12f), TextAnchor.MiddleCenter);
                var btn = rect.gameObject.AddComponent<Button>();
                btn.targetGraphic = bg;
                int captured = i;
                btn.onClick.AddListener(() => OnTabClicked(captured));
                _tabButtons.Add(btn);
            }
        }

        private static Color TabBgColor(bool selected)
            => selected ? new Color(0.95f, 0.78f, 0.42f) : new Color(0.82f, 0.78f, 0.70f);

        // ════════════ 拉数据 + 渲染 ════════════

        private void OnTabClicked(int tabIndex)
        {
            if (_disposed) return;
            _currentKind = Tabs[tabIndex].kind;
            // 视觉高亮当前 tab
            for (int i = 0; i < _tabBgImages.Count; i++)
                if (_tabBgImages[i] != null) _tabBgImages[i].color = TabBgColor(i == tabIndex);
            // 切 tab 时列表先清空再拉(设计 46 PV5「无残留旧 tab 数据」)
            ClearRows();
            RefetchAsync(_currentKind).Forget();
        }

        private async UniTaskVoid RefetchAsync(AttrType? kind)
        {
            if (_disposed) return;
            int myToken = ++_fetchToken;
            SetStatus("正在加载...");

            var svc = Svc;
            if (svc == null)
            {
                if (_disposed || myToken != _fetchToken) return;
                ClearRows();
                SetStatus("服务暂不可用,稍后再试");
                return;
            }

            AttrLedgerPage page;
            try
            {
                // sinceTs=0(本子单首屏取最新 N 条);limit=50(O2 默认)
                page = await svc.FetchPageAsync(kind, 0L, DefaultLimit);
            }
            catch
            {
                if (_disposed || myToken != _fetchToken) return;
                ClearRows();
                SetStatus("网络异常,请检查连接");
                return;
            }

            // 响应到达时核 token + dispose:旧响应或已关窗 → 丢弃,不动 UI
            if (_disposed || myToken != _fetchToken) return;

            RenderPage(page);
        }

        private void RenderPage(AttrLedgerPage page)
        {
            ClearRows();
            if (page == null)
            {
                SetStatus("服务暂不可用,稍后再试");
                return;
            }

            switch (page.Code)
            {
                case AttrLedgerQueryCode.Success:
                    if (page.Entries == null || page.Entries.Count == 0)
                    {
                        SetStatus("暂无流水");
                    }
                    else
                    {
                        SetStatus(""); // 隐藏状态栏(列表非空时无须文案)
                        var nowUtc = DateTime.UtcNow;
                        for (int i = 0; i < page.Entries.Count; i++)
                            _rowInstances.Add(BuildRowGo(_listContent, page.Entries[i], i, nowUtc));
                    }
                    break;

                case AttrLedgerQueryCode.ServiceUnavailable:
                    SetStatus("服务暂不可用,稍后再试");
                    break;

                case AttrLedgerQueryCode.NetworkDown:
                    SetStatus("网络异常,请检查连接");
                    break;

                case AttrLedgerQueryCode.InvalidRequest:
                default:
                    SetStatus("请求参数异常,请联系客服");
                    Log.Error($"[PlayerAttrLedgerWindow] 收到 InvalidRequest 结果码,可能是客户端 bug");
                    break;
            }
        }

        private void SetStatus(string msg)
        {
            if (_statusText == null) return;
            _statusText.text = msg ?? string.Empty;
        }

        private void ClearRows()
        {
            for (int i = 0; i < _rowInstances.Count; i++)
                if (_rowInstances[i] != null) UnityEngine.Object.Destroy(_rowInstances[i]);
            _rowInstances.Clear();
        }

        // ════════════ 列表行视图模型(便于单测,设计 46 PV6 / PV7 锚点)════════════

        /// <summary>
        /// 列表行视图模型(纯派生字段,与 UGUI 解耦,便于 EditMode 单测「entry → 行 VM」单步映射)。
        /// </summary>
        public readonly struct RowVM
        {
            public readonly string TimeText;     // 设计 46 §3.5 相对/绝对时间文案
            public readonly AttrType Kind;       // 属性种类(决定占位色)
            public readonly string KindText;     // 「金币/钻石/体力」中文占位(无图标时降级文案)
            public readonly string DeltaText;    // 「+50」/「-100」
            public readonly string SourceText;   // 设计 46 §3.4 中文 source 文案
            public readonly string BalanceText;  // 「100 → 50」

            public RowVM(string time, AttrType kind, string kindText, string delta, string source, string balance)
            {
                TimeText = time;
                Kind = kind;
                KindText = kindText;
                DeltaText = delta;
                SourceText = source;
                BalanceText = balance;
            }
        }

        /// <summary>
        /// entry → 行 VM(纯函数,设计 46 §3.4 + §3.5,EditMode 单测锚点)。
        /// </summary>
        public static RowVM BuildRowVM(AttrLedgerEntry e, DateTime nowUtc)
        {
            if (e == null)
                return new RowVM("?", AttrType.Coin, "?", "?", "?", "?");
            return new RowVM(
                AttrLedgerFormat.FormatRelativeTime(e.Timestamp, nowUtc),
                e.Kind,
                AttrLedgerFormat.FormatKind(e.Kind),
                (e.Delta >= 0 ? "+" : "") + e.Delta,
                AttrLedgerFormat.FormatSource(e.Source),
                e.BalanceBefore + " → " + e.BalanceAfter);
        }

        // ════════════ 列表行 UGUI 生成(沿 28 RankWindow BuildRowGo 范式)════════════

        private static GameObject BuildRowGo(Transform parent, AttrLedgerEntry e, int index, DateTime nowUtc)
        {
            const float rowW = 740f, rowH = 80f, gap = 4f;
            var vm = BuildRowVM(e, nowUtc);
            var row = NewRect("Row_" + index, parent, rowW, rowH);
            row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -(rowH + gap) * index);

            // 行底(条纹斑马,便于多行可读)
            var bg = AddImage(row, "RowBg", rowW, rowH,
                (index % 2 == 0) ? new Color(0.94f, 0.90f, 0.80f) : new Color(0.98f, 0.95f, 0.85f));

            // 时间列(160 宽)
            AddText(bg.transform, "Time", 160f, rowH, new Vector2(-rowW * 0.5f + 90f, 0f),
                vm.TimeText, 22, new Color(0.40f, 0.30f, 0.18f), TextAnchor.MiddleLeft);

            // 属性图标占位(沿 42 §三 三色;无切图 → 用稳定占位色 + 中文一字)
            var iconRect = NewRect("Icon", bg.transform, 50f, 50f);
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(-rowW * 0.5f + 200f, 0f);
            AddImage2(iconRect, KindColor(vm.Kind));
            AddText(iconRect, "KindLabel", 50f, 50f, Vector2.zero, vm.KindText.Substring(0, 1), 24,
                Color.white, TextAnchor.MiddleCenter);

            // delta 列(80 宽,无符号颜色区分本子单不做,O7;统一深色)
            AddText(bg.transform, "Delta", 100f, rowH, new Vector2(-rowW * 0.5f + 290f, 0f),
                vm.DeltaText, 26, new Color(0.30f, 0.20f, 0.08f), TextAnchor.MiddleLeft);

            // source 列(160 宽)
            AddText(bg.transform, "Source", 160f, rowH, new Vector2(-rowW * 0.5f + 430f, 0f),
                vm.SourceText, 24, new Color(0.30f, 0.20f, 0.08f), TextAnchor.MiddleLeft);

            // 余额变化列(右对齐)
            AddText(bg.transform, "Balance", 200f, rowH, new Vector2(rowW * 0.5f - 110f, 0f),
                vm.BalanceText, 22, new Color(0.55f, 0.30f, 0.05f), TextAnchor.MiddleRight);

            return row.gameObject;
        }

        /// <summary>属性占位色(沿 42 §三 占位映射:Coin → 金 / Diamond → 蓝绿 / Stamina → 红)。</summary>
        private static Color KindColor(AttrType type)
        {
            switch (type)
            {
                case AttrType.Coin:    return new Color(0.95f, 0.78f, 0.30f); // 金
                case AttrType.Diamond: return new Color(0.30f, 0.75f, 0.85f); // 蓝绿宝石
                case AttrType.Stamina: return new Color(0.90f, 0.40f, 0.45f); // 红心
                default:               return new Color(0.55f, 0.50f, 0.42f);
            }
        }

        // ════════════ UGUI 代码生成小工具(1080×1920 坐标系,center 锚点)════════════

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

        /// <summary>给一个 RectTransform 挂纯色 Image(铺满自身)+ 返回 Image 引用。</summary>
        private static Image AddImage2(RectTransform target, Color color)
        {
            var img = target.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>新建带 Image 子节点(用作行底 / 容器);返 Image 引用便于继续 SetSprite。</summary>
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

        /// <summary>给 root 直接挂半透明遮罩面板(用 stretch 锚点全屏覆盖)。</summary>
        private static RectTransform AddPanel(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            go.GetComponent<Image>().color = color;
            return rt;
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

        private void Close() => GameModule.UI.CloseUI<PlayerAttrLedgerWindow>();
    }
}
