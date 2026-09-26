using System;
using Memori.Localization;
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

        private Action onRemove;

        private void Awake()
        {
            removeButton.onClick.AddListener(() => onRemove?.Invoke());
        }

        public void Show(GearID gearID, int cost, bool locked, Action _onRemove)
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
