using UnityEngine;
using UnityEngine.EventSystems;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    public sealed class QuestTabletTiltModelTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private QuestTabletTiltController tiltController;

        public void Configure(QuestTabletTiltController controller)
        {
            tiltController = controller != null
                ? controller
                : FindAnyObjectByType<QuestTabletTiltController>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (tiltController == null)
            {
                tiltController = FindAnyObjectByType<QuestTabletTiltController>();
            }

            tiltController?.ToggleTiltFromModel();
        }
    }
}
