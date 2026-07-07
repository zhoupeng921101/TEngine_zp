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
    /// 静态壳（背板 / 头部 / 标题 / 收起按钮 / 角星 / 空态提示）由 prefab 承载、按节点名绑定。
    /// 格子经虚拟网格（<see cref="LoopGridView"/>，挂在 prefab 节点 <c>m_rect_Grid</c> 上）回收复用：
    /// 两轨投影铺平成统一格序列驱动列表，仅可视格实例化，滚动时按 <see cref="OnGetItemByRowColumn"/> 取用 + 绑定
    /// <see cref="BagGridCell"/>。数据权威在服务端（data-authority）：格子内容由登录快照 / 背包推送整份覆盖后经
    /// <see cref="InventoryService.OnInventoryChanged"/> 触发重建；批次倒计时由各可见格自刷（服务端时间基准，不信本地墙钟）。
    /// 使用结果只做提示（成功产出的货币由属性推送刷新 HUD，本窗不展示货币栏），投影刷新一律等服务端推送，不乐观改本地。
    /// </remarks>
    [Window(UILayer.UI, location: "UIBagPanel", fullScreen: false)]
    public sealed partial class UIBagPanel : UIPanelMono
    {
        // ── 浮层面板几何（设计坐标：左上原点、Y 下正，原生 1080×1920）──
        private const float PanelCx = BlockLayout.DesignWidth / 2f;   // 540
        private const float PanelCy = 960f;

        // ── 虚拟网格：4 列固定，格间留缝，外围内边距 ──
        private const int Cols = 4;
        private static readonly Vector2 ItemGap = new Vector2(20f, 46f);
        private const int PrewarmCount = 12;                          // 预热格数（约 4 列 × 3 可视行）

        // 两轨铺平后的统一格数据（堆叠轨在前、批次轨在后）。
        private readonly List<CellData> _cells = new();
        private LoopGridView _grid;
        private GameObject _cellTemplate;
        private bool _gridInited;

        // 使用事务在途闸：一次点击未回来前忽略后续点击（UI 侧，服务端另有 reqSeq 串行契约）。
        private bool _useInFlight;

        private InventoryService Inv => GameContext.Instance?.Inventory;
        private ItemBag Bag => GameContext.Instance?.Items;

        /// <summary>单格数据（两轨统一投影）：堆叠轨 <see cref="IsLot"/>=false 用 <see cref="Count"/>；批次轨用 <see cref="Lot"/>（含倒计时）。</summary>
        private readonly struct CellData
        {
            public readonly int ItemId;
            public readonly int Count;
            public readonly bool IsLot;
            public readonly InventoryLot Lot;

            public CellData(int itemId, int count)
            {
                ItemId = itemId;
                Count = count;
                IsLot = false;
                Lot = default;
            }

            public CellData(InventoryLot lot)
            {
                ItemId = lot.ItemId;
                Count = 0;
                IsLot = true;
                Lot = lot;
            }
        }

        protected override void OnCreate()
        {
            SetupGrid();

            // 背包投影变更（登录快照 / 背包推送整份覆盖后触发）→ 重建网格。堆叠轨与批次轨在同一推送内先后应用，
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

        /// <summary>
        /// 装配虚拟网格：把 prefab 上的 <c>m_rect_Grid</c>（已挂 <see cref="ScrollRect"/> + <see cref="LoopGridView"/>）接成竖向回收网格 —
        /// 建裁剪视口 + 内容容器、建单格模板并登记入池。容器/模板运行时建、随窗销毁（不入 prefab）。
        /// </summary>
        private void SetupGrid()
        {
            if (m_rect_Grid == null) return;
            _grid = m_rect_Grid.GetComponent<LoopGridView>();
            var scroll = m_rect_Grid.GetComponent<ScrollRect>();
            if (_grid == null || scroll == null)
            {
                Log.Error("[UIBagPanel] m_rect_Grid 缺少 ScrollRect / LoopGridView 组件（应在 prefab 上挂载）。");
                return;
            }

            // 裁剪视口（填满 m_rect_Grid，RectMask2D 裁剪越界格；透明底板接住空隙拖拽）。
            var viewport = UGuiFactory.CreateNode(m_rect_Grid, "Viewport");
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = Vector2.zero;
            viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<RectMask2D>();
            var drag = viewport.gameObject.AddComponent<Image>();
            drag.color = Color.clear;
            drag.raycastTarget = true;

            // 内容容器（LoopGridView 的 container；锚点/轴心由 InitGridView 归正）。
            var content = UGuiFactory.CreateNode(viewport, "Content");

            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.inertia = true;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = viewport;
            scroll.content = content;

            // 单格模板（不激活）+ 登记入池：走 ItemPrefabDataList 让 InitGridView 归正根节点锚点/轴心并预热。
            _cellTemplate = BagGridCell.BuildTemplate(content).gameObject;
            _grid.ItemPrefabDataList.Clear();
            _grid.ItemPrefabDataList.Add(new GridViewItemPrefabConfData
            {
                itemPrefab = _cellTemplate,
                initCreateCount = PrewarmCount,
            });
        }

        private void OnInventoryChanged() => RefreshGrid();

        /// <summary>
        /// 重建两轨数据投影 + 驱动虚拟网格：首次 <see cref="LoopGridView.InitGridView"/>，其后仅更新格数 + 刷新可见格。
        /// 两轨都空时按 <see cref="InventoryService.IsReady"/> 显「加载中 / 空背包」。
        /// </summary>
        private void RefreshGrid()
        {
            if (_grid == null) return;
            RebuildCellData();

            if (!_gridInited)
            {
                _grid.InitGridView(_cells.Count, OnGetItemByRowColumn, BuildSettingParam());
                _gridInited = true;
            }
            else
            {
                // 格数变了 SetListItemCount 会重排；不变时它 no-op，靠 RefreshAllShownItem 按新数据重绑可见格。
                _grid.SetListItemCount(_cells.Count, resetPos: false);
                _grid.RefreshAllShownItem();
            }

            // 空态提示。
            if (_cells.Count == 0)
            {
                m_text_Hint.gameObject.SetActive(true);
                var inv = Inv;
                bool ready = inv == null || inv.IsReady;
                m_text_Hint.text = ready ? "背包空空如也" : "加载中...";
            }
            else
            {
                m_text_Hint.gameObject.SetActive(false);
            }
        }

        /// <summary>铺平两轨为统一格序列：堆叠轨（Items）在前、批次轨（Inventory.Lots）在后。</summary>
        private void RebuildCellData()
        {
            _cells.Clear();

            var bag = Bag;
            if (bag != null)
            {
                foreach (var (id, count) in bag.Entries())
                {
                    _cells.Add(new CellData(id, count));
                }
            }

            var inv = Inv;
            if (inv != null)
            {
                var lots = inv.Lots;
                for (int i = 0; i < lots.Count; i++)
                {
                    _cells.Add(new CellData(lots[i]));
                }
            }
        }

        /// <summary>网格布局参数（代码下发，覆盖 Inspector）：4 列固定、格尺寸/缝/内边距。</summary>
        private static LoopGridViewSettingParam BuildSettingParam()
        {
            return new LoopGridViewSettingParam
            {
                ItemSize = new Vector2(BagGridCell.CellSize, BagGridCell.CellSize),
                ItemPadding = ItemGap,
                Padding = new RectOffset(40, 40, 20, 20),
                GridFixedType = GridFixedType.ColumnCountFixed,
                FixedRowOrColumnCount = Cols,
            };
        }

        /// <summary>取用回调：第 itemIndex 格滚入视口时从池取格并按对应轨数据绑定。</summary>
        private LoopGridViewItem OnGetItemByRowColumn(LoopGridView grid, int itemIndex, int row, int column)
        {
            if (itemIndex < 0 || itemIndex >= _cells.Count) return null;
            var item = grid.NewListViewItem(BagGridCell.PrefabName);
            if (item is not BagGridCell cell) return item;

            var data = _cells[itemIndex];
            Color iconColor = QualityColorOf(data.ItemId);
            if (data.IsLot) cell.BindLot(data.Lot, Inv, iconColor, OnCellClicked);
            else cell.BindHolding(data.ItemId, data.Count, iconColor, OnCellClicked);
            return item;
        }

        /// <summary>图标品质色（查道具表 <see cref="RewardDisplay.QualityColor"/>，查无退化白）。</summary>
        private static Color QualityColorOf(int itemId)
        {
            var def = ItemConfigMgr.GetItem(itemId);
            return RewardDisplay.QualityColor(def != null ? def.Quality : 1);
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
                Log.Warning($"[UIBagPanel] 使用道具异常：{e.Message}");
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

        /// <summary>轻量弹字提示（复用 <see cref="BurstText"/>，居中于面板；建于窗根固定设计坐标系，不随网格重绘销毁）。</summary>
        private void Toast(string msg)
        {
            BurstText.Spawn(rectTransform, PanelCx, PanelCy, msg, 48, new Color32(0x55, 0x44, 0x88, 0xFF));
        }

        private partial void OnClick_CloseBtn() => GameModule.UI.CloseUI<UIBagPanel>();
    }
}
