using UnityEngine;
using UnityEngine.UI;
using TEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 神庙面板（长期主线，设计 13 §五）：顶部主线信息行（虔诚币 / 守护者等级 / 本级经验进度 / 已解锁章节）
    /// + 12 厅三态卡片（已修 / 可修 / 币不足 / 未解锁）+ 修复按钮（币不足或未解锁置灰）。
    /// glyph + 纯色、零美术，复用 <see cref="UGuiFactory"/> / <see cref="BurstText"/>，与 demo 一致。
    /// 读同一份 <see cref="BlockGameState.Instance"/>.MergeState（与 <see cref="MergeOrderWindow"/> 同源引用），
    /// 叠层打开不丢当前局；修复后刷新本窗，返回 MergeOrderWindow 时虔诚币自然反映扣减。
    /// </summary>
    [Window(UILayer.UI, location: "TempleWindow", fullScreen: true)]
    public sealed class TempleWindow : UIWindow
    {
        private const int Cols = 3;   // 3 列 × 4 行 = 12 厅

        private BlockGameState _state;
        private MergeOrderState _merge;

        private RectTransform _content;
        private RectTransform _cardLayer;

        private Text _pietyText;
        private Text _levelText;
        private Text _expText;
        private Text _chapterText;

        // 关窗回调：返回 MergeOrderWindow 时让其刷新虔诚币显示（叠层不丢局，UserData[0] 传入）。
        private System.Action _onClosed;

        protected override void OnCreate()
        {
            _state = BlockGameState.Instance;
            _merge = _state.MergeState;
            _onClosed = UserData as System.Action;

            BuildStaticUI();
            RefreshHeader();
            RefreshTempleList();
        }

        protected override void OnDestroy()
        {
            _onClosed?.Invoke();
        }

        private void BuildStaticUI()
        {
            _content = UGuiFactory.CreateContentPanel(rectTransform);
            float cx = BlockLayout.DesignWidth / 2f;

            UGuiFactory.CreateImage(_content, "Bg", cx, BlockLayout.DesignHeight / 2f,
                BlockLayout.DesignWidth, BlockLayout.DesignHeight, BlockLayout.BgColor);

            // 标题
            UGuiFactory.CreateText(_content, "Title", cx, 60, 600, 60, "神庙修复", 40,
                new Color32(0xff, 0xcf, 0x5c, 0xFF));

            // 退出（返回 MergeOrderWindow，不丢局：只关本窗）
            var exit = UGuiFactory.CreateButton(_content, "Exit", BlockLayout.DesignWidth - 55, 60, 70, 60, "×", 44,
                new Color(0, 0, 0, 0), Color.white, out _, out _);
            exit.onClick.AddListener(() => GameModule.UI.CloseUI<TempleWindow>());

            // 顶部主线信息行（两行）
            UGuiFactory.CreateImage(_content, "HeaderBg", cx, 165, 700, 130, new Color(0, 0, 0, 0.25f));
            // 第一行：虔诚币 + 守护者等级
            _pietyText = UGuiFactory.CreateText(_content, "Piety", cx - 175, 135, 340, 56, "", 36,
                new Color32(0xff, 0xcf, 0x5c, 0xFF), TextAnchor.MiddleLeft);   // 金
            _levelText = UGuiFactory.CreateText(_content, "Level", cx + 175, 135, 340, 56, "", 36,
                new Color32(0xae, 0xf0, 0xcf, 0xFF), TextAnchor.MiddleRight);  // 绿
            // 第二行：本级经验进度 + 已解锁章节
            _expText = UGuiFactory.CreateText(_content, "Exp", cx - 175, 195, 340, 50, "", 28,
                new Color32(0x7f, 0xca, 0xa0, 0xFF), TextAnchor.MiddleLeft);
            _chapterText = UGuiFactory.CreateText(_content, "Chapter", cx + 175, 195, 340, 50, "", 28,
                new Color32(0x8a, 0xa0, 0xd0, 0xFF), TextAnchor.MiddleRight);

            _cardLayer = UGuiFactory.CreateNode(_content, "CardLayer");
            UGuiFactory.PlaceByDesignCenter(_cardLayer, cx, BlockLayout.DesignHeight / 2f, 0, 0);
        }

        // ── 顶部信息行 ──
        private void RefreshHeader()
        {
            _pietyText.text = $"✦ {_merge.Piety}";
            int level = _merge.GuardianLevel;
            _levelText.text = $"守护者 Lv.{level}";

            // 本级经验进度：cur / 本级所需。floor = 升到当前级累计消耗的经验。
            int floor = ExpFloorForLevel(level);
            int cur = _merge.Exp - floor;
            int need = TempleConfig.ExpToNext(level);
            _expText.text = $"经验 {cur}/{need}";

            _chapterText.text = $"已解锁 第{_merge.UnlockedChapter}章";
        }

        /// <summary>升到第 level 级累计消耗的经验门槛（= sum ExpToNext(1..level-1)）。</summary>
        private static int ExpFloorForLevel(int level)
        {
            int sum = 0;
            for (int l = 1; l < level; l++) sum += TempleConfig.ExpToNext(l);
            return sum;
        }

        // ── 12 厅三态卡（每次刷新重建） ──
        private void RefreshTempleList()
        {
            for (int i = _cardLayer.childCount - 1; i >= 0; i--)
                Object.Destroy(_cardLayer.GetChild(i).gameObject);

            const float cardW = 220f;
            const float cardH = 200f;
            const float gapX = 18f;
            const float gapY = 18f;
            const float gridTop = 290f;
            float gridW = Cols * cardW + (Cols - 1) * gapX;
            float startX = (BlockLayout.DesignWidth - gridW) / 2f + cardW / 2f;

            for (int i = 0; i < TempleConfig.HallCount; i++)
            {
                int row = i / Cols;
                int col = i % Cols;
                float cx = startX + col * (cardW + gapX);
                float cy = gridTop + cardH / 2f + row * (cardH + gapY);
                BuildCard(i, cx, cy, cardW, cardH);
            }
        }

        // 四态：已修 / 可修 / 币不足 / 未解锁。
        private enum HallState { Repaired, Repairable, Insufficient, Locked }

        private HallState StateOf(int index)
        {
            if (_merge.TempleRepaired != null && _merge.TempleRepaired[index]) return HallState.Repaired;
            if (index != _merge.NextRepairIndex) return HallState.Locked;           // 前序未修
            return _merge.Piety >= TempleConfig.Cost(index) ? HallState.Repairable : HallState.Insufficient;
        }

        private void BuildCard(int index, float cx, float cy, float w, float h)
        {
            var st = StateOf(index);
            int cost = TempleConfig.Cost(index);

            Color cardColor = st switch
            {
                HallState.Repaired     => new Color32(0x1f, 0x44, 0x2e, 0xFF), // 绿（高亮已修）
                HallState.Repairable   => new Color32(0x2e, 0x33, 0x44, 0xFF), // 正常
                HallState.Insufficient => new Color32(0x2e, 0x33, 0x44, 0xFF),
                _                      => new Color32(0x1c, 0x1f, 0x2a, 0xFF), // 未解锁暗淡
            };
            UGuiFactory.CreateImage(_cardLayer, $"card_{index}", cx, cy, w, h, cardColor);

            // 厅名
            UGuiFactory.CreateText(_cardLayer, $"name_{index}", cx, cy - h / 2f + 32, w - 16, 44,
                TempleConfig.HallName(index), 26,
                st == HallState.Locked ? new Color32(0x66, 0x6c, 0x7a, 0xFF) : Color.white);

            // 装饰 glyph（已修才显示）
            if (st == HallState.Repaired)
                UGuiFactory.CreateText(_cardLayer, $"deco_{index}", cx, cy - 6, w, 64, "◈", 48,
                    new Color32(0x5b, 0xd6, 0xa0, 0xFF));

            // 状态行 / 造价
            switch (st)
            {
                case HallState.Repaired:
                    UGuiFactory.CreateText(_cardLayer, $"stat_{index}", cx, cy + h / 2f - 60, w, 40,
                        "✓ 已修复", 26, new Color32(0x5b, 0xd6, 0xa0, 0xFF));
                    break;
                case HallState.Repairable:
                    UGuiFactory.CreateText(_cardLayer, $"cost_{index}", cx, cy + h / 2f - 60, w, 40,
                        $"✦ {cost}", 28, new Color32(0xff, 0xcf, 0x5c, 0xFF));
                    break;
                case HallState.Insufficient:
                    UGuiFactory.CreateText(_cardLayer, $"cost_{index}", cx, cy + h / 2f - 78, w, 36,
                        $"✦ {cost}", 26, new Color32(0xff, 0x77, 0x66, 0xFF));
                    UGuiFactory.CreateText(_cardLayer, $"need_{index}", cx, cy + h / 2f - 48, w, 32,
                        $"还差 {cost - _merge.Piety}", 22, new Color32(0xcc, 0x88, 0x66, 0xFF));
                    break;
                default: // Locked
                    UGuiFactory.CreateText(_cardLayer, $"lock_{index}", cx, cy, w, 60,
                        "🔒", 40, new Color32(0x55, 0x5a, 0x68, 0xFF));
                    UGuiFactory.CreateText(_cardLayer, $"locktip_{index}", cx, cy + h / 2f - 40, w, 32,
                        "需先修前序", 22, new Color32(0x55, 0x5a, 0x68, 0xFF));
                    break;
            }

            // 修复按钮：仅「可修」「币不足」两态显示（可修可点，币不足置灰）；已修/未解锁不显示按钮。
            if (st == HallState.Repairable || st == HallState.Insufficient)
            {
                bool can = st == HallState.Repairable;
                int captured = index;
                var btn = UGuiFactory.CreateButton(_cardLayer, $"repair_{index}", cx, cy + h / 2f - 22, w - 24, 40,
                    "修复", 26,
                    can ? new Color32(0x33, 0xaa, 0x55, 0xFF) : new Color32(0x44, 0x44, 0x4c, 0xFF),
                    can ? Color.white : new Color32(0x88, 0x88, 0x88, 0xFF), out _, out _);
                btn.interactable = can;
                btn.onClick.AddListener(() => OnRepairClicked(captured));
            }
        }

        // ── 修复点击：扣币标记发奖 → 内联弹字 → 刷新本窗 ──
        private void OnRepairClicked(int index)
        {
            if (!_merge.RepairTemple(index, out var r)) return;

            BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 540,
                $"修复{TempleConfig.HallName(index)}！+经验{r.ExpGained} +体力{r.EnergyGained}", 40,
                new Color32(0xff, 0xcf, 0x5c, 0xFF));

            if (r.LevelsGained > 0)
                BurstText.Spawn(_content, BlockLayout.DesignWidth / 2f, 620,
                    $"守护者 Lv↑ 解锁第{_merge.UnlockedChapter}章", 38,
                    new Color32(0xae, 0xf0, 0xcf, 0xFF));

            RefreshHeader();
            RefreshTempleList();
        }
    }
}
