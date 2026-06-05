using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 候选方块拖拽器（运行时 AddComponent 到槽容器上）。
    /// 交互：按下即放大并"拾起"（无需先拖动）；移动按 1.2× 增益跟手 + 拇指上方 offset；
    /// 抬手（含未拖动的点击）统一回调 OnEnd 由 GameWindow 做落子/回弹判定。
    /// 命中区为整个槽区域（GameWindow 把容器尺寸设为槽区域大小），点区域任意处即选中。
    /// </summary>
    public sealed class BlockPieceDragger : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public int SlotIndex;

        public Action<int> OnBegin;
        public Action<int, Vector2> OnDragMove;
        public Action<int> OnEnd;

        private RectTransform _rt;
        private RectTransform _parentRt;
        private Canvas _canvas;
        private Vector2 _originAnchored;
        private Vector3 _originScale;

        private Vector2 _pointerStart;  // 按下时指针的本地坐标
        private Vector2 _pieceBase;     // 拾起基准（指针 + 上方 offset）

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _parentRt = _rt.parent as RectTransform;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void RecordOrigin()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            _originAnchored = _rt.anchoredPosition;
            _originScale = _rt.localScale;
        }

        private Camera EventCam =>
            _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

        private bool ToLocal(Vector2 screen, out Vector2 local)
        {
            if (_parentRt == null) { local = default; return false; }
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRt, screen, EventCam, out local);
        }

        public void OnPointerDown(PointerEventData e)
        {
            RecordOrigin();
            // 按下立即放大 + 提层（不依赖拖动阈值）
            _rt.localScale = _originScale * BlockLayout.DragScale;
            _rt.SetAsLastSibling();
            OnBegin?.Invoke(SlotIndex);

            if (ToLocal(e.position, out var local))
            {
                _pointerStart = local;
                _pieceBase = local + Vector2.up * BlockLayout.DragFingerOffsetY;
                _rt.anchoredPosition = _pieceBase;
                OnDragMove?.Invoke(SlotIndex, _rt.anchoredPosition);
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (ToLocal(e.position, out var local))
            {
                // 位移按 1.2× 增益放大
                _rt.anchoredPosition = _pieceBase + (local - _pointerStart) * BlockLayout.DragGain;
                OnDragMove?.Invoke(SlotIndex, _rt.anchoredPosition);
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            OnEnd?.Invoke(SlotIndex);
        }

        /// <summary>非法落子 / 点击未拖动时回弹到原位（恢复缩放）。</summary>
        public void ResetToOrigin()
        {
            _rt.anchoredPosition = _originAnchored;
            _rt.localScale = _originScale;
        }

        public Vector2 OriginAnchored => _originAnchored;
    }
}
