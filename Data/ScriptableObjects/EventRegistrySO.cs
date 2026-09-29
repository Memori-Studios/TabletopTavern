using UnityEngine;

namespace TJ
{
    /// <summary>Every map event; saved runs store positions in this list, so only ever append to it.</summary>
    [CreateAssetMenu(fileName = "EventRegistry", menuName = "GameData/EventRegistry", order = 2)]
    public class EventRegistrySO : ScriptableObject
    {
        public EventDefinitionSO[] AllEvents;
    }
}
