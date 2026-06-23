using UnityEngine;
using GameLogic.BlockBlast;

namespace GameLogic
{
    /// <summary>
    /// Block Blast UI 布局常量。坐标系即原生 1080×1920 设计分辨率（竖屏，match width），无整体缩放。
    /// 所有坐标以"左上角为原点、Y 向下为正"的设计坐标描述，
    /// 由 UGuiFactory 在锚点 (0,1) 下转成 anchoredPosition（X 右正、Y 下负）。
    /// 像素常量是 1080 原生空间的绝对值（Content localScale=1）。
    /// </summary>
    public static class BlockLayout
    {
        // 设计分辨率（内部坐标系 = 原生 1080×1920，与 UIRoot CanvasScaler 参考分辨率一致，Content 不缩放）
        public const float DesignWidth = 1080f;
        public const float DesignHeight = 1920f;

        // 棋盘
        public const int BoardSize = 8;                 // 8×8
        public const float CellSize = 121f;             // 每格像素（1080 原生空间绝对值）

        // 棋盘格视觉内缩间隙（单格视觉边长 = 格距 - 此值）：单一事实源。
        // 自适应棋盘渲染（MergeOrderWindow）单格 = BoardCellSize() - BoardCellGap，格距 = BoardCellSize()；
        // 候选块单格 base 反推此值除以拖起放大倍数，使放大后单格视觉与棋盘格精确相等。
        public const float BoardCellGap = 6f;
        public const float BoardPixels = CellSize * BoardSize;  // 968
        public const float BoardOriginX = (DesignWidth - BoardPixels) / 2f;  // 居中 ≈56
        public const float BoardOriginY = 432f;         // 棋盘左上 Y（设计坐标，向下）

        // 候选槽
        public const float SlotCell = 68f;              // 槽内格尺寸
        public const float SlotSpacing = 312f;          // 槽水平间距
        public const float SlotCenterX = DesignWidth / 2f;

        // 拖拽
        public static float DragScale => CellSize / SlotCell;   // 拖起放大倍数 ≈1.78
        public const float DragFingerOffsetY = 265f;            // 方块在手指上方，避免被指尖遮挡
        public const float DragGain = 1.0f;                     // 方块位移 = 触控位移 × 1.0（1:1 跟手，所见即所落）

        // 候选槽命中区（整块区域可点选，不必点到方块本身）。3 个槽水平平铺。
        public const float SlotZoneWidth = SlotSpacing - 14f;   // ≈298
        public const float SlotZoneHeight = 360f;

        // 颜色
        public static readonly Color BoardOuterColor = new Color32(0x2a, 0x2a, 0x55, 0xFF);
        public static readonly Color BoardCellBgColor = new Color32(0x1a, 0x1a, 0x40, 0xFF);
        public static readonly Color BgColor = new Color32(0x1a, 0x1a, 0x2e, 0xFF);
        public static readonly Color GhostOkColor = new Color32(0x66, 0xff, 0x77, 0x73);   // 绿 ~0.45a
        public static readonly Color GhostBadColor = new Color32(0xff, 0x55, 0x66, 0x73);  // 红

        // 消除预览发光（落子后会满、将被消除的整行整列高亮）：亮绿描边辉光，贴参考观感。
        // 形状由 UI/GlowCell shader 在 UV 空间沿带边缘画绿色描边光（横条上下边、竖条左右边），此色作为顶点 tint 注入（不依赖 sprite）。
        public static readonly Color ClearPreviewGlowColor = new Color32(0x55, 0xff, 0x55, 0xE0); // 亮绿发光基色 ~0.88a

        // 消除预览「行内填充」：覆盖可消除整行整列方块的半透明绿，使各色方块统一偏绿但仍可见（与边缘辉光一起出现）。
        // 普通 alpha 混合的纯色覆盖层（非 shader），alpha≈0.4。
        public static readonly Color ClearPreviewFillColor = new Color32(0x55, 0xff, 0x55, 0x66); // 亮绿半透明 ~0.4a

        /// <summary>8 种方块颜色 → RGB（对应 BlockColor 0..7）。</summary>
        public static readonly Color[] BlockColors =
        {
            new Color32(0x44, 0x99, 0xff, 0xFF), // Blue
            new Color32(0x55, 0xcc, 0x66, 0xFF), // Green
            new Color32(0xff, 0xdd, 0x44, 0xFF), // Yellow
            new Color32(0xff, 0x99, 0x44, 0xFF), // Orange
            new Color32(0xff, 0x55, 0x66, 0xFF), // Red
            new Color32(0x99, 0xaa, 0xbb, 0xFF), // Steel
            new Color32(0x44, 0xcc, 0xcc, 0xFF), // Teal
            new Color32(0xcc, 0x88, 0xff, 0xFF), // Purple
        };

        public static Color ColorOf(BlockColor c)
        {
            int idx = (int)c;
            if (idx < 0 || idx >= BlockColors.Length) idx = 0;
            return BlockColors[idx];
        }

        /// <summary>棋盘格 (col,row) 的中心设计坐标（左上原点，Y 向下）。</summary>
        public static Vector2 CellCenterDesign(int col, int row)
        {
            return new Vector2(
                BoardOriginX + col * CellSize + CellSize / 2f,
                BoardOriginY + row * CellSize + CellSize / 2f);
        }

        /// <summary>
        /// 设计坐标（左上原点、Y 下正）→ 固定面板 center 锚点的 anchoredPosition（中心原点、Y 上正）。
        /// 面板尺寸固定 DesignWidth×DesignHeight、pivot 居中。
        /// </summary>
        public static Vector2 DesignToAnchored(float designX, float designY)
        {
            return new Vector2(designX - DesignWidth / 2f, DesignHeight / 2f - designY);
        }

        public static Vector2 DesignToAnchored(Vector2 design) => DesignToAnchored(design.x, design.y);

        /// <summary>anchoredPosition（中心原点、Y 上正）→ 设计坐标（左上原点、Y 下正）。</summary>
        public static Vector2 AnchoredToDesign(Vector2 anchored)
        {
            return new Vector2(anchored.x + DesignWidth / 2f, DesignHeight / 2f - anchored.y);
        }
    }
}
