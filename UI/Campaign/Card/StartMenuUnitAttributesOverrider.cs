using UnityEngine;

namespace TJ
{
    [RequireComponent(typeof(UnitAttributesUIContainer))]
    public class StartMenuUnitAttributesOverrider : MonoBehaviour
    {
        // Awake, not Start: the container decides at its first Load whether to build the stack.
        void Awake()
        {
            GetComponent<UnitAttributesUIContainer>().OverrideStatsDisplayOnStart();
        }
    }
}
