using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TsukiVox.AudioPrototype
{
    /// <summary>Converts vertical pointer drags over the queue into row-sized scroll steps.</summary>
    public sealed class QuestQueueSwipeHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float ClickCancelPixels = 12f;
        private const float PixelsPerRow = 36f;

        private Action<int> onRowsDragged;
        private float accumulatedPixels;
        private bool hasCancelledClick;

        public void Configure(Action<int> callback)
        {
            onRowsDragged = callback;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            accumulatedPixels = 0f;
            hasCancelledClick = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData == null || onRowsDragged == null)
            {
                return;
            }

            accumulatedPixels += eventData.delta.y;
            if (!hasCancelledClick && Mathf.Abs(accumulatedPixels) >= ClickCancelPixels)
            {
                hasCancelledClick = true;
            }

            if (hasCancelledClick)
            {
                eventData.eligibleForClick = false;
            }

            var rowCount = Mathf.FloorToInt(Mathf.Abs(accumulatedPixels) / PixelsPerRow);
            if (rowCount <= 0)
            {
                return;
            }

            var direction = accumulatedPixels > 0f ? 1 : -1;
            accumulatedPixels -= direction * rowCount * PixelsPerRow;
            onRowsDragged(direction * rowCount);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            accumulatedPixels = 0f;
            hasCancelledClick = false;
        }

        private void OnDisable()
        {
            accumulatedPixels = 0f;
            hasCancelledClick = false;
        }
    }
}
