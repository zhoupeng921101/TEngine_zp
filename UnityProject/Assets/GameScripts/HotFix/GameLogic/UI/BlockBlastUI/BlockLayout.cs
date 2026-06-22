using UnityEngine;
using GameLogic.BlockBlast;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// Block Blast UI 布局常量。坐标系基于 750×1334 设计分辨率（竖屏，match width）。
    /// 源项目是 450×800；这里按 ≈1.667 倍放大到 750 宽。
    /// 所有坐标以"左上角为原点、Y 向下为正"的设计坐标描述，
    /// 由 UGuiFactory 在锚点 (0,1) 下转成 anchoredPosition（X 右正、Y 下负）。
    /// </summary>
    public static class BlockLayout
    {
        // 设计分辨率（内部坐标系，保持 750×1334 不变，便于所有布局常量稳定）
        public const float DesignWidth = 750f;
        public const float DesignHeight = 1334f;

        // UIRoot CanvasScaler 实际参考分辨率（用户设为 1080×1920）。
        // Content 面板按 ContentScale 整体放大，使 750 设计宽填满 1080 参考宽（同为 9:16）。
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;
        public const float ContentScale = ReferenceWidth / DesignWidth;  // 1.44

        // 棋盘
        public const int BoardSize = 8;                 // 8×8
        public const float CellSize = 84f;              // 每格像素（450→750 缩放后 50*1.667≈84）
        public const float BoardPixels = CellSize * BoardSize;  // 672
        public const float BoardOriginX = (DesignWidth - BoardPixels) / 2f;  // 居中 ≈39
        public const float BoardOriginY = 300f;         // 棋盘左上 Y（设计坐标，向下）

        // 候选槽
        public const float SlotCell = 47f;              // 槽内格尺寸（28*1.667）
        public const float SlotY = 1100f;               // 3 个槽中心 Y
        public const float SlotSpacing = 217f;          // 槽水平间距（130*1.667）
        public const float SlotCenterX = DesignWidth / 2f;

        // 拖拽
        public static float DragScale => CellSize / SlotCell;   // 拖起放大倍数 ≈1.79
        public const float DragFingerOffsetY = 184f;            // 方块在手指上方（110*1.667），避免被指尖遮挡
        public const float DragGain = 1.0f;                     // 方块位移 = 触控位移 × 1.0（1:1 跟手，所见即所落）

        // 候选槽命中区（整块区域可点选，不必点到方块本身）。3 个槽水平平铺。
        public const float SlotZoneWidth = SlotSpacing - 10f;   // ≈207
        public const float SlotZoneHeight = 250f;

        // 颜色
        public static readonly Color BoardOuterColor = new Color32(0x2a, 0x2a, 0x55, 0xFF);
        public static readonly Color BoardCellBgColor = new Color32(0x1a, 0x1a, 0x40, 0xFF);
        public static readonly Color BgColor = new Color32(0x1a, 0x1a, 0x2e, 0xFF);
        public static readonly Color GhostOkColor = new Color32(0x66, 0xff, 0x77, 0x73);   // 绿 ~0.45a
        public static readonly Color GhostBadColor = new Color32(0xff, 0x55, 0x66, 0x73);  // 红

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
