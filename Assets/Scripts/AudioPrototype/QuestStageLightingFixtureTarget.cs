using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

namespace TsukiVox.AudioPrototype
{
    [DisallowMultipleComponent]
    public sealed class QuestStageLightingFixtureTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private QuestStageLightingPrototype stageLightingPrototype;

        public void Configure(QuestStageLightingPrototype stageLighting)
        {
            stageLightingPrototype = stageLighting != null
                ? stageLighting
                : GetComponentInParent<QuestStageLightingPrototype>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (stageLightingPrototype == null)
            {
                stageLightingPrototype = GetComponentInParent<QuestStageLightingPrototype>();
            }

            if (stageLightingPrototype == null)
            {
                return;
            }

            stageLightingPrototype.SetLightingEnabled(!stageLightingPrototype.LightingEnabled);
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (device.isValid)
            {
                device.SendHapticImpulse(0u, 0.2f, 0.05f);
            }
        }
    }
}
