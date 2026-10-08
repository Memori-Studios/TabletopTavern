using System;
using Memori.Localization;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>The Starting Gear block on the warband screen: the equipped item, an empty slot, or the Renown lock.</summary>
    public class WarbandGearSlot : MonoBehaviour
    {
        [SerializeField] private GameObject equippedRoot;
        [SerializeField] private GameObject emptyRoot;
        [SerializeField] private GameObject lockedRoot;

        [Header("Equipped")]
        [SerializeField] private WarbandGearTile tile;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Button removeButton;

        [Header("Empty and locked")]
        [SerializeField] private TMP_Text emptyTitleText;
        [SerializeField] private TMP_Text emptyHintText;
        [SerializeField] private TMP_Text lockedTitleText;
        [SerializeField] private TMP_Text lockedHintText;

        [Header("Feel")]
        // Optional. The shared Land Flare over the slot, played in the item's rarity colour on equip.
        [SerializeField] private UIFlare equipFlare;

        private Action onRemove;
        private Coroutine leaving;

        private void Awake()
        {
            removeButton.onClick.AddListener(() => onRemove?.Invoke());
            UIHoverBloom.Attach(removeButton.gameObject, null, 1.12f, false);
        }

        /// <param name="feedback">The player changed the gear just now, so the change lands or leaves visibly.</param>
        public void Show(GearID gearID, int cost, bool locked, Action _onRemove, bool feedback = false)
        {
            if (leaving != null) { StopCoroutine(leaving); leaving = null; ResetEquipped(); }
            bool wasEquipped = equippedRoot.activeSelf;
            bool willEquip = !locked && gearID != GearID.None;
            // Taking gear off lets the item shrink away before the empty slot shows.
            if (feedback && wasEquipped && !willEquip && isActiveAndEnabled)
            {
                leaving = StartCoroutine(Leave(gearID, cost, locked, _onRemove));
                return;
            }
            Apply(gearID, cost, locked, _onRemove);
            if (feedback && willEquip) PlayEquipped(gearID);
        }

        private System.Collections.IEnumerator Leave(GearID gearID, int cost, bool locked, Action _onRemove)
        {
            Transform item = equippedRoot.transform;
            CanvasGroup group = equippedRoot.GetComponent<CanvasGroup>();
            if (group == null) group = equippedRoot.AddComponent<CanvasGroup>();
            for (float t = 0f; t < 1f; t += Mathf.Min(Time.unscaledDeltaTime, UIJuice.MaxStep) / 0.12f)
            {
                float s = Mathf.Lerp(1f, 0.85f, t);
                item.localScale = new Vector3(s, s, 1f);
                group.alpha = 1f - t;
                yield return null;
            }
            ResetEquipped();
            leaving = null;
            Apply(gearID, cost, locked, _onRemove);
        }

        private void ResetEquipped()
        {
            equippedRoot.transform.localScale = Vector3.one;
            CanvasGroup group = equippedRoot.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
        }

        // The item lands in the slot: the tile punches and a flare in its rarity colour.
        private void PlayEquipped(GearID gearID)
        {
            if (!isActiveAndEnabled) return;
            tile.transform.localScale = Vector3.one;
            StartCoroutine(UIJuice.Punch(tile.transform, 1.12f, 0.06f, 0.18f));
            if (equipFlare != null) equipFlare.Play((Color)ColorData.GetGearRarityColor(GearData.GetGear(gearID).GearRarity));
        }

        private void Apply(GearID gearID, int cost, bool locked, Action _onRemove)
        {
            onRemove = _onRemove;
            bool equipped = !locked && gearID != GearID.None;
            equippedRoot.SetActive(equipped);
            emptyRoot.SetActive(!locked && !equipped);
            lockedRoot.SetActive(locked);

            emptyTitleText.text = T("WarbandGearEmpty");
            emptyHintText.text = T("WarbandGearEmptyHint");
            lockedTitleText.text = T("Locked");
            lockedHintText.text = T("WarbandGearLockedHint");
            if (!equipped) return;

            Gear gear = GearData.GetGear(gearID);
            string description = string.Format(T(gearID + "Desc"), gear.GearModifierValue);
            nameText.text = T(gearID + "Name");
            KeywordText.Apply(descriptionText, description);
            priceText.text = $"{cost} <sprite name=GoldSprite>";
            tile.Set(gearID, true, false, () => WarbandGearTile.BuildTooltip(gearID, true, cost, string.Empty), null);
            tile.SetState(false, true);
        }

        private static string T(string key) => LocalizationManager.Instance.GetText(key);
    }
}
