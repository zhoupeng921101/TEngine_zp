using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GameLogic.BlockBlastUI
{
    /// <summary>
    /// 候选方块拖拽器（运行时 AddComponent 到槽容器上）。
    /// 自身只负责"跟手 + 放大 + 回调"，落点判定/落子逻辑全部回调给 GameWindow。
    /// HybridCLR 下热更 MonoBehaviour 通过 AddComponent 动态挂载，不进 prefab 序列化。
    /// </summary>
    public sealed class BlockPieceDragger : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public int SlotIndex;

        /// <summary>拖拽回调（由 GameWindow 注入）。坐标为 UIRoot 本地坐标。</summary>
        public Action<int> OnBegin;
        public Action<int, Vector2> OnDragMove; // (slotIndex, 当前指针对应的方块中心本地坐标)
        public Action<int> OnEnd;

        private RectTransform _rt;
        private RectTransform _parentRt;
        private Canvas _canvas;
        private Vector2 _originAnchored;
        private Vector3 _originScale;

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

        public void OnBeginDrag(PointerEventData e)
        {
            RecordOrigin();
            _rt.localScale = _originScale * BlockLayout.DragScale;
            _rt.SetAsLastSibling();
            OnBegin?.Invoke(SlotIndex);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_parentRt == null) return;
            var cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera : null;
            // 把屏幕点转到父容器本地坐标，方块中心 = 指针上方 offset
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_parentRt, e.position, cam, out var local))
            {
                local.y += BlockLayout.DragFingerOffsetY;
                _rt.anchoredPosition = local;
                OnDragMove?.Invoke(SlotIndex, _rt.anchoredPosition);
            }
        }

        public void OnEndDrag(PointerEventData e)
        {
            OnEnd?.Invoke(SlotIndex);
        }

        /// <summary>非法落子时回弹到原位（恢复缩放）。</summary>
        public void ResetToOrigin()
        {
            _rt.anchoredPosition = _originAnchored;
            _rt.localScale = _originScale;
        }

        public Vector2 OriginAnchored => _originAnchored;
    }
}
