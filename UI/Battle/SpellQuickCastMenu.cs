using TJ.Spells;
using UnityEngine;
using Memori.Audio;

namespace TJ
{
    public class SpellQuickCastMenu : MonoBehaviour
    {
        [SerializeField] private SpellQuickCastSlot quickCastSlot1, quickCastSlot2, quickCastSlot3, quickCastSlot4;
        private int queuedSlotIndex = -1;

        private void OnEnable()
        {
            transform.position = Input.mousePosition;
            queuedSlotIndex = -1;
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
        }

        public void Load(SpellData[] spellData)
        {
            quickCastSlot1.SetUp(GetSpell(spellData, 0), () => QueueSlot(0), () => UnqueueSlot(0));
            quickCastSlot2.SetUp(GetSpell(spellData, 1), () => QueueSlot(1), () => UnqueueSlot(1));
            quickCastSlot3.SetUp(GetSpell(spellData, 2), () => QueueSlot(2), () => UnqueueSlot(2));
            quickCastSlot4.SetUp(GetSpell(spellData, 3), () => QueueSlot(3), () => UnqueueSlot(3));
        }

        // The loadout can be shorter than these four fixed slots; the extras load empty.
        private static SpellData GetSpell(SpellData[] spellData, int index)
            => (spellData != null && index < spellData.Length) ? spellData[index] : null;

        private void QueueSlot(int slotIndex) => queuedSlotIndex = slotIndex;
        private void UnqueueSlot(int slotIndex)
        {
            if(queuedSlotIndex == slotIndex) queuedSlotIndex = -1;
        }

        public int ConsumeQueuedSlotIndex()
        {
            int result = queuedSlotIndex;
            queuedSlotIndex = -1;
            return result;
        }
    }
}
