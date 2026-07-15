using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TsukiVox.AudioPrototype
{
    public sealed class QuestUiButtonFeedback : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] private Text hoverLabel;
        [SerializeField] private TMP_Text tmpHoverLabel;
        [SerializeField] private string hoverText;

        private bool isHovered;
        private float targetScale = 1f;

        public void Configure(Text label, string labelText)
        {
            hoverLabel = label;
            tmpHoverLabel = null;
            hoverText = labelText;
        }

        public void Configure(TMP_Text label, string labelText)
        {
            hoverLabel = null;
            tmpHoverLabel = label;
            hoverText = labelText;
        }

        private void Update()
        {
            var current = transform.localScale.x;
            var next = Mathf.Lerp(current, targetScale, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            transform.localScale = Vector3.one * next;
        }

        private void OnDisable()
        {
            isHovered = false;
            targetScale = 1f;
            transform.localScale = Vector3.one;
            ClearHoverLabel();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            targetScale = 1.025f;
            if (hoverLabel != null)
            {
                hoverLabel.text = hoverText;
            }

            if (tmpHoverLabel != null)
            {
                tmpHoverLabel.text = hoverText;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            targetScale = 1f;
            ClearHoverLabel();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            targetScale = 0.955f;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            targetScale = isHovered ? 1.025f : 1f;
        }

        private void ClearHoverLabel()
        {
            if (hoverLabel != null && hoverLabel.text == hoverText)
            {
                hoverLabel.text = string.Empty;
            }

            if (tmpHoverLabel != null && tmpHoverLabel.text == hoverText)
            {
                tmpHoverLabel.text = string.Empty;
            }
        }
    }
}
