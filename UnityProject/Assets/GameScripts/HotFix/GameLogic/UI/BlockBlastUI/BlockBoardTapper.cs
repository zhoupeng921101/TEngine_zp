using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic
{
    /// <summary>
    /// 棋盘格点击捕获器（运行时 AddComponent 到铺满棋盘区域的透明 overlay 上）。
    /// 消除道具「指定格」用（设计 49 §3.1）：玩家点棋盘任一格 → 把点击屏幕坐标转成本地设计坐标 →
    /// 映射出 (col,row) → 回调 <see cref="OnTapCell"/>。只在 overlay 启用时接收点击（窗口按 arming 态开关 overlay）。
    /// 坐标转换与 <see cref="BlockPieceDragger"/> 同源（ScreenPointToLocalPointInRectangle + WorldSpace Canvas 相机）。
    /// </summary>
    public sealed class BlockBoardTapper : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>点中棋盘格回调：参数 (col,row)，越界由窗口侧判（理论上 overlay 限定在棋盘区不会越界）。</summary>
        public Action<int, int> OnTapCell;

        private RectTransform _rt;
        private Canvas _canvas;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
        }

        private Camera EventCam =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        public void OnPointerClick(PointerEventData e)
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, e.position, EventCam, out var local))
                return;

            // overlay 锚点居中、尺寸 = 设计全屏，故 local 原点在屏幕中心；转回设计坐标（左上原点，y 向下）。
            var design = BlockLayout.AnchoredToDesign(local);
            int col = Mathf.FloorToInt((design.x - BlockLayout.BoardOriginX) / BlockLayout.CellSize);
            int row = Mathf.FloorToInt((design.y - BlockLayout.BoardOriginY) / BlockLayout.CellSize);
            OnTapCell?.Invoke(col, row);
        }
    }
}
