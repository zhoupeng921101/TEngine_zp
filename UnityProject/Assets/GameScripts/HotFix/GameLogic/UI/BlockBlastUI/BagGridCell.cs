using UnityEngine;
using UnityEngine.UI;
using GameLogic.BlockBlast.Item;
using GameLogic.BlockBlast.Player;

namespace GameLogic
{
    /// <summary>
    /// 背包虚拟网格的单格视图（<see cref="LoopGridView"/> 的回收单元）：承载图标 / 数量角标 / 倒计时三个子节点，
    /// 由 <see cref="UIBagPanel"/> 在取用回调里按第 index 格数据绑定。两轨复用同一模板 —
    /// 堆叠轨（无有效期）走 <see cref="BindHolding"/>，批次轨（带倒计时）走 <see cref="BindLot"/>。
    /// 批次倒计时在本格 <c>Update</c> 内按服务端时间基准（<see cref="InventoryService.RemainingMs"/>）自刷新，
    /// 仅当前可见格参与、回收即停；不信本地墙钟。图标为品质色占位块（无美术），点击落到底板按钮发起使用。
    /// </summary>
    public sealed class BagGridCell : LoopGridViewItem
    {
        /// <summary>单格边长（设计像素）。子节点按格心局部坐标偏移布置。</summary>
        public const float CellSize = 190f;

        /// <summary>池按预制名建池，模板名即此常量（<see cref="UIBagPanel"/> 取用时按名 New）。</summary>
        public const string PrefabName = "BagCell";

        private static readonly Color CellColor = new Color32(0xF7, 0xF6, 0xFC, 0xFF);
        private static readonly Color CountColor = new Color32(0x33, 0x2A, 0x55, 0xFF);
        private static readonly Color CountdownColor = new Color32(0xC2, 0x5A, 0x5A, 0xFF);
        private static readonly Color ExpiredColor = new Color32(0x99, 0x99, 0x99, 0xFF);

        private Image _bg;
        private Image _icon;
        private Text _count;
        private Text _countdown;
        private Button _button;
        private bool _refsResolved;

        // 绑定态：批次轨才有效，用于本格 Update 自刷新倒计时。
        private InventoryService _inv;
        private bool _isLot;
        private InventoryLot _lot;
        private int _itemId;
        private System.Action<int> _onClick;

        /// <summary>惰性解析子节点引用（按名，一次）。模板经 Instantiate 克隆后各实例独立解析各自子树。</summary>
        private void EnsureRefs()
        {
            if (_refsResolved) return;
            _refsResolved = true;
            _bg = GetComponent<Image>();
            _button = GetComponent<Button>();
            _icon = transform.Find("Icon")?.GetComponent<Image>();
            _count = transform.Find("Count")?.GetComponent<Text>();
            _countdown = transform.Find("Countdown")?.GetComponent<Text>();
            if (_button != null)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClicked);
            }
        }

        /// <summary>绑定堆叠轨格（itemId→数量，无有效期，不显倒计时）。</summary>
        public void BindHolding(int itemId, int count, Color iconColor, System.Action<int> onClick)
        {
            EnsureRefs();
            _isLot = false;
            _inv = null;
            _itemId = itemId;
            _onClick = onClick;
            ApplyCommon(iconColor, "×" + ItemBag.FormatCount(count));
            if (_countdown != null) _countdown.text = "";
        }

        /// <summary>绑定批次轨格（带倒计时，服务端时间基准）。</summary>
        public void BindLot(InventoryLot lot, InventoryService inv, Color iconColor, System.Action<int> onClick)
        {
            EnsureRefs();
            _isLot = true;
            _lot = lot;
            _inv = inv;
            _itemId = lot.ItemId;
            _onClick = onClick;
            ApplyCommon(iconColor, "×" + lot.Count);
            RefreshCountdown();
        }

        private void ApplyCommon(Color iconColor, string countText)
        {
            if (_bg != null) _bg.color = CellColor;
            if (_icon != null) _icon.color = iconColor;
            if (_count != null) _count.text = countText;
        }

        private void OnClicked() => _onClick?.Invoke(_itemId);

        private void Update()
        {
            if (_isLot) RefreshCountdown();
        }

        /// <summary>按服务端时间基准刷新剩余；永不过期批次不显，已过期转灰。</summary>
        private void RefreshCountdown()
        {
            if (_countdown == null || _inv == null) return;
            if (_lot.ExpireMs <= 0) { _countdown.text = ""; return; }
            long remain = _inv.RemainingMs(_lot);
            if (remain <= 0)
            {
                _countdown.text = "已过期";
                _countdown.color = ExpiredColor;
            }
            else
            {
                _countdown.text = FormatRemaining(remain);
                _countdown.color = CountdownColor;
            }
        }

        /// <summary>剩余毫秒 → 紧凑倒计时文本（天 / 时 / 分 / 秒，取最大非零档）。</summary>
        private static string FormatRemaining(long ms)
        {
            long sec = ms / 1000;
            if (sec >= 86400) return (sec / 86400) + "天";
            if (sec >= 3600) return (sec / 3600) + "时";
            if (sec >= 60) return (sec / 60) + "分";
            return sec + "秒";
        }

        /// <summary>
        /// 构建单格模板（不激活；供 <see cref="LoopGridView"/> 池克隆）。底板 Button 承接点击，
        /// 图标 / 数量 / 倒计时子节点按格心局部坐标（Y 上正）偏移，均 center 锚点。
        /// 根节点锚点/轴心由 LoopGridView 在建池时按 ArrangeType 归正，此处不预设。
        /// </summary>
        public static BagGridCell BuildTemplate(Transform parent)
        {
            var go = new GameObject(PrefabName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(CellSize, CellSize);

            var bg = go.GetComponent<Image>();
            bg.color = CellColor;
            bg.raycastTarget = true;
            go.GetComponent<Button>().targetGraphic = bg;

            // 图标（品质色占位；不拦点击，让点击落到底板 Button）。
            var icon = PlaceLocal(UGuiFactory.CreateImage(rt, "Icon", 0, 0, CellSize * 0.62f, CellSize * 0.62f, Color.white).rectTransform,
                0f, 0f);
            icon.GetComponent<Image>().raycastTarget = false;

            // 数量角标（右下）。
            PlaceLocal(UGuiFactory.CreateText(rt, "Count", 0, 0, 120, 60, "", 40, CountColor, TextAnchor.MiddleRight).rectTransform,
                CellSize * 0.28f, -CellSize * 0.30f);
            // 倒计时（顶部）。
            PlaceLocal(UGuiFactory.CreateText(rt, "Countdown", 0, 0, CellSize, 50, "", 34, CountdownColor).rectTransform,
                0f, CellSize * 0.36f);

            var cell = go.AddComponent<BagGridCell>();
            go.SetActive(false);
            return cell;
        }

        /// <summary>把 <see cref="UGuiFactory"/> 建出的节点（原按设计坐标定位）改为格心局部偏移，center 锚点/轴心。</summary>
        private static RectTransform PlaceLocal(RectTransform rt, float x, float y)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            return rt;
        }
    }
}
