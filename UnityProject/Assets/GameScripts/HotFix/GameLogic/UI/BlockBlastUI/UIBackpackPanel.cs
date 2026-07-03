using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast.Item;
using GameLogic.BlockBlast.Player;
using GameLogic.BlockBlast.Reward;
using GameLogic.Config;

namespace GameLogic
{
    /// <summary>
    /// 背包悬浮窗（背包系统·客户端段）：浮层非弹框（无全屏遮罩，主菜单仍可见），展示两轨道具 —
    /// 堆叠轨（<see cref="GameContext.Items"/>，itemId→数量）与批次轨（<see cref="GameContext.Inventory"/> 的有有效期批次，带倒计时）。
    /// 点格子发起「使用」事务（<see cref="InventoryService.TryUseAsync"/>），服务端裁定并推送新投影；本窗只持投影、不本地改库存。
    /// </summary>
    /// <remarks>
    /// 全代码构建（沿 <c>UIRankPanel</c> 全代码生成范式，prefab 仅为空壳承载脚本，无 Inspector 拖线）。
    /// 数据权威在服务端（data-authority）：格子内容由登录快照 / 背包推送整份覆盖后经 <see cref="InventoryService.OnInventoryChanged"/>
    /// 触发重绘；批次倒计时基于服务端时间基准（<see cref="InventoryService.RemainingMs"/>），不信本地墙钟。
    /// 使用结果只做提示（成功产出的货币由属性推送刷新 HUD，本窗不展示货币栏），投影刷新一律等服务端推送，不乐观改本地。
    /// 本轮不做滚动：当前道具种类少，固定网格直排；种类增多时补 ScrollRect（本窗留待后续）。
    /// </remarks>
    [Window(UILayer.UI, location: "UIBackpackPanel", fullScreen: false)]
    public sealed class UIBackpackPanel : UIPanelMono
    {
        // ── 浮层面板几何（设计坐标：左上原点、Y 下正，原生 1080×1920）──
        private const float PanelCx = BlockLayout.DesignWidth / 2f;   // 540
        private const float PanelCy = 960f;
        private const float PanelW = 900f;
        private const float PanelH = 1180f;
        private const float HeaderCy = 460f;
        private const float HeaderH = 180f;

        // ── 网格：4 列，格心间距固定，首列居中反推 ──
        private const int Cols = 4;
        private const float CellSize = 190f;
        private const float ColSpacing = 210f;
        private const float RowSpacing = 236f;
        private const float GridTopCy = 700f;                         // 第一行格心 Y
        private static float FirstColCx => PanelCx - ColSpacing * (Cols - 1) / 2f;

        // ── 取自效果图的配色 ──
        private static readonly Color PanelColor = new Color32(0xEA, 0xE6, 0xF5, 0xFF);
        private static readonly Color HeaderColor = new Color32(0x7B, 0x86, 0xC2, 0xFF);
        private static readonly Color CellColor = new Color32(0xF7, 0xF6, 0xFC, 0xFF);
        private static readonly Color StarColor = new Color32(0xE5, 0xB9, 0x4E, 0xFF);
        private static readonly Color TitleColor = Color.white;
        private static readonly Color CountColor = new Color32(0x33, 0x2A, 0x55, 0xFF);
        private static readonly Color CountdownColor = new Color32(0xC2, 0x5A, 0x5A, 0xFF);
        private static readonly Color ExpiredColor = new Color32(0x99, 0x99, 0x99, 0xFF);
        private static readonly Color HintColor = new Color32(0x88, 0x82, 0xA6, 0xFF);

        private RectTransform _content;
        private RectTransform _gridRoot;     // 网格容器：刷新时整块清空重建
        private Text _hint;                  // 空 / 加载中提示

        // 批次格倒计时：OnUpdate 每帧按服务端时间基准刷新剩余文本。
        private readonly List<(Text label, InventoryLot lot)> _countdownCells = new();

        // 使用事务在途闸：一次点击未回来前忽略后续点击（UI 侧，服务端另有 reqSeq 串行契约）。
        private bool _useInFlight;

        private InventoryService Inv => GameContext.Instance?.Inventory;
        private ItemBag Bag => GameContext.Instance?.Items;

        protected override void OnCreate()
        {
            BuildStaticUI();

            // 背包投影变更（登录快照 / 背包推送整份覆盖后触发）→ 重绘网格。堆叠轨与批次轨在同一推送内先后应用，
            // 该事件在两轨都更新后触发，故订阅它即覆盖两轨刷新（见 GameApp.ApplyServerInventorySnapshot）。
            var inv = Inv;
            if (inv != null) inv.OnInventoryChanged += OnInventoryChanged;

            RefreshGrid();
        }

        protected override void OnDestroyWindow()
        {
            var inv = Inv;
            if (inv != null) inv.OnInventoryChanged -= OnInventoryChanged;
        }

        private void OnInventoryChanged() => RefreshGrid();

        /// <summary>面板 / 头部 / 关闭按钮 / 角星等静态壳，一次性构建。</summary>
        private void BuildStaticUI()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);

            // 悬浮面板底板（非全屏遮罩：面板外可点透，符合「悬浮不是弹框」）。
            UGuiFactory.CreateImage(_content, "PanelBg", PanelCx, PanelCy, PanelW, PanelH, PanelColor);

            // 头部条 + 标题。
            UGuiFactory.CreateImage(_content, "Header", PanelCx, HeaderCy, PanelW, HeaderH, HeaderColor);
            UGuiFactory.CreateText(_content, "Title", PanelCx, HeaderCy, 500, 100, "背包", 68, TitleColor);

            // 收起按钮（浮层无遮罩，须显式提供关闭入口）。
            var btnClose = UGuiFactory.CreateButton(_content, "BtnClose",
                PanelCx + PanelW / 2f - 90f, HeaderCy, 120, 90, "收起", 44,
                new Color32(0x5A, 0x64, 0xA0, 0xFF), Color.white, out _, out _);
            btnClose.onClick.AddListener(OnClose);

            // 四角装饰星（效果图元素，纯展示）。
            float halfW = PanelW / 2f, halfH = PanelH / 2f;
            UGuiFactory.CreateText(_content, "StarTL", PanelCx - halfW + 40f, PanelCy - halfH + 40f, 60, 60, "★", 44, StarColor);
            UGuiFactory.CreateText(_content, "StarTR", PanelCx + halfW - 40f, PanelCy - halfH + 40f, 60, 60, "★", 44, StarColor);
            UGuiFactory.CreateText(_content, "StarBL", PanelCx - halfW + 40f, PanelCy + halfH - 40f, 60, 60, "★", 44, StarColor);
            UGuiFactory.CreateText(_content, "StarBR", PanelCx + halfW - 40f, PanelCy + halfH - 40f, 60, 60, "★", 44, StarColor);

            // 网格容器（清空重建的锚点）。
            _gridRoot = UGuiFactory.CreateNode(_content, "Grid");
            _gridRoot.sizeDelta = new Vector2(PanelW, PanelH - HeaderH);

            // 空 / 加载中提示（居中于网格区）。
            _hint = UGuiFactory.CreateText(_content, "Hint", PanelCx, GridTopCy + RowSpacing, 700, 100, "", 44, HintColor);
        }

        /// <summary>
        /// 整块重绘网格：先清空旧格，再按「堆叠轨（Items）在前、批次轨（Inventory.Lots）在后」的顺序逐格铺设。
        /// 两轨都空时按 <see cref="InventoryService.IsReady"/> 显「加载中 / 空背包」。
        /// </summary>
        private void RefreshGrid()
        {
            if (_gridRoot == null) return;

            // 清旧格（含挂在其上的倒计时文本引用）。
            _countdownCells.Clear();
            for (int i = _gridRoot.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(_gridRoot.GetChild(i).gameObject);
            }

            var bag = Bag;
            var inv = Inv;
            int index = 0;

            // 堆叠轨：itemId → 数量（无有效期）。
            if (bag != null)
            {
                foreach (var (id, count) in bag.Entries())
                {
                    BuildHoldingCell(index++, id, count);
                }
            }

            // 批次轨：有有效期批次（带倒计时）。
            if (inv != null)
            {
                var lots = inv.Lots;
                for (int i = 0; i < lots.Count; i++)
                {
                    BuildLotCell(index++, lots[i]);
                }
            }

            // 空态提示：无任何格子时按就绪态区分「加载中 / 空」。
            if (index == 0)
            {
                _hint.gameObject.SetActive(true);
                bool ready = inv == null || inv.IsReady;
                _hint.text = ready ? "背包空空如也" : "加载中...";
            }
            else
            {
                _hint.gameObject.SetActive(false);
            }
        }

        /// <summary>格心设计坐标（第 index 个格子，按 4 列自左而右、自上而下排布）。</summary>
        private static Vector2 CellCenter(int index)
        {
            int col = index % Cols;
            int row = index / Cols;
            return new Vector2(FirstColCx + col * ColSpacing, GridTopCy + row * RowSpacing);
        }

        /// <summary>堆叠轨格子：品质色图标 + 数量角标；点击发起使用。</summary>
        private void BuildHoldingCell(int index, int itemId, int count)
        {
            var center = CellCenter(index);
            Color iconColor = BuildCellShell(index, itemId);
            BuildCellIcon(center, iconColor);
            // 数量角标（右下）。
            UGuiFactory.CreateText(_gridRoot, "Count",
                center.x + CellSize * 0.28f, center.y + CellSize * 0.30f, 120, 60,
                "×" + ItemBag.FormatCount(count), 40, CountColor, TextAnchor.MiddleRight);
        }

        /// <summary>批次轨格子：品质色图标 + 数量角标 + 倒计时（服务端时间基准）；点击发起使用。</summary>
        private void BuildLotCell(int index, InventoryLot lot)
        {
            var center = CellCenter(index);
            Color iconColor = BuildCellShell(index, lot.ItemId);
            BuildCellIcon(center, iconColor);
            UGuiFactory.CreateText(_gridRoot, "Count",
                center.x + CellSize * 0.28f, center.y + CellSize * 0.30f, 120, 60,
                "×" + lot.Count, 40, CountColor, TextAnchor.MiddleRight);

            // 倒计时文本（顶部），OnUpdate 每帧刷新剩余。
            var cd = UGuiFactory.CreateText(_gridRoot, "Countdown",
                center.x, center.y - CellSize * 0.36f, CellSize, 50, "", 34, CountdownColor);
            _countdownCells.Add((cd, lot));
        }

        /// <summary>图标占位（品质色块，无美术）。不拦点击（raycastTarget=false），让点击落到底板 Button。</summary>
        private void BuildCellIcon(Vector2 center, Color iconColor)
        {
            var icon = UGuiFactory.CreateImage(_gridRoot, "Icon", center.x, center.y,
                CellSize * 0.62f, CellSize * 0.62f, iconColor);
            icon.raycastTarget = false;
        }

        /// <summary>
        /// 格子底板 Button（点击→使用）建于 <see cref="_gridRoot"/>；图标 / 数量 / 倒计时同建于 _gridRoot 同坐标系、叠在其上。
        /// 返回图标品质色（查道具表 <see cref="RewardDisplay.QualityColor"/>，查无退化白）。
        /// </summary>
        private Color BuildCellShell(int index, int itemId)
        {
            var center = CellCenter(index);
            var btn = UGuiFactory.CreateButton(_gridRoot, "Cell_" + index, center.x, center.y, CellSize, CellSize,
                "", 1, CellColor, Color.clear, out _, out _);
            int capturedId = itemId;
            btn.onClick.AddListener(() => OnCellClicked(capturedId));

            var def = ItemConfigMgr.GetItem(itemId);
            return RewardDisplay.QualityColor(def != null ? def.Quality : 1);
        }

        protected override void OnUpdate()
        {
            var inv = Inv;
            if (inv == null || _countdownCells.Count == 0) return;
            for (int i = 0; i < _countdownCells.Count; i++)
            {
                var (label, lot) = _countdownCells[i];
                if (label == null) continue;
                long remain = inv.RemainingMs(lot);
                if (lot.ExpireMs <= 0)
                {
                    // 永不过期批次不显倒计时。
                    label.text = "";
                    continue;
                }
                if (remain <= 0)
                {
                    label.text = "已过期";
                    label.color = ExpiredColor;
                }
                else
                {
                    label.text = FormatRemaining(remain);
                    label.color = CountdownColor;
                }
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

        // ── 使用事务 ──

        private void OnCellClicked(int itemId) => UseItemAsync(itemId).Forget();

        /// <summary>
        /// 发起使用（服务端裁定）：经 <see cref="InventoryService.TryUseAsync"/> 发一次请求，等响应。
        /// 成功且有货币产出 → 弹字提示（货币 HUD 由属性推送刷新）；投影刷新一律等服务端背包推送经 OnInventoryChanged 覆盖，不本地乐观改。
        /// 由 Button.onClick 经 .Forget() 调（等价 async void），全程吞异常不外逃。
        /// </summary>
        private async UniTaskVoid UseItemAsync(int itemId)
        {
            if (_useInFlight) return;
            var inv = Inv;
            if (inv == null) { Toast("背包服务未就绪"); return; }

            _useInFlight = true;
            try
            {
                var r = await inv.TryUseAsync(itemId, 1);
                HandleUseResult(r);
            }
            catch (System.Exception e)
            {
                Log.Warning($"[UIBackpackPanel] 使用道具异常：{e.Message}");
            }
            finally
            {
                _useInFlight = false;
            }
        }

        /// <summary>使用结果码 → 用户提示。成功由服务端推送刷新投影，失败不脏投影。</summary>
        private void HandleUseResult(UseItemResult r)
        {
            switch (r.Code)
            {
                case UseItemCode.Success:
                    Toast(r.HasProduce
                        ? $"获得 {AttrName(r.ProducedType)} +{r.ProducedAmount}"
                        : "使用成功");
                    break;
                case UseItemCode.Duplicate:
                    // 幂等重复（同一 reqSeq 已处理）：静默，投影仍由推送对齐。
                    break;
                case UseItemCode.NotUsable: Toast("该道具不可直接使用"); break;
                case UseItemCode.Expired: Toast("道具已过期"); break;
                case UseItemCode.NotEnough: Toast("数量不足"); break;
                case UseItemCode.UnknownItem: Toast("未知道具"); break;
                case UseItemCode.NotLoggedIn: Toast("请先登录"); break;
                case UseItemCode.NetworkDown: Toast("网络异常，请稍后再试"); break;
                default: Toast("使用失败"); break;
            }
        }

        /// <summary>货币类型 → 中文名（提示用；仅本轮支持的货币产出）。</summary>
        private static string AttrName(AttrType type)
        {
            switch (type)
            {
                case AttrType.Coin: return "金币";
                case AttrType.Diamond: return "钻石";
                case AttrType.Stamina: return "体力";
                case AttrType.SoulPower: return "灵力";
                case AttrType.Piety: return "虔诚币";
                case AttrType.GuardianExp: return "经验";
                case AttrType.Energy: return "体力";
                default: return "奖励";
            }
        }

        /// <summary>轻量弹字提示（复用 <see cref="BurstText"/>，居中于面板；建于 <see cref="_content"/> 固定设计坐标系，不随网格重绘销毁）。</summary>
        private void Toast(string msg)
        {
            var parent = _content != null ? (Transform)_content : transform;
            BurstText.Spawn(parent, PanelCx, PanelCy, msg, 48, new Color32(0x55, 0x44, 0x88, 0xFF));
        }

        private void OnClose() => GameModule.UI.CloseUI<UIBackpackPanel>();
    }
}
