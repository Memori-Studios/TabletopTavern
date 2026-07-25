using System;
using Memori.Tooltip;
using Memori.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
    /// <summary>
    /// One selectable spell in the pre-battle <see cref="SpellBrowseMenu"/>. Shows the spell icon,
    /// a hover tooltip (name + description) and, when clicked, requests a swap into the slot the
    /// browse menu was opened from. The row keeps a fixed position in the list; when its spell is
    /// already equipped it is dimmed and non-interactable rather than removed, so nothing reorders.
    /// </summary>
    public class SpellBrowseSlot : MonoBehaviour
    {
        [SerializeField] private Image spellIcon;
        [SerializeField] private Button selectButton;
        [SerializeField] private MemoriTooltipTrigger tooltipTrigger;
        [SerializeField] private Color equippedTint = new Color(1f, 1f, 1f, 0.35f);
        [Header("Race Theming")]
        [SerializeField] private Image backgroundImage;
        // 0-255, matching the SquadBattleInfo race-passive convention. Applied over RaceData.PrimaryColor.
        [SerializeField] private float backgroundAlpha = 25f;

        private Color defaultIconColor;
        private SpellData spellData;
        public SpellData SpellData => spellData;

        public void SetUp(SpellData _spellData, Action onClicked)
        {
            spellData = _spellData;
            spellIcon.sprite = _spellData.SpellSprite;
            defaultIconColor = spellIcon.color;

            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onClicked?.Invoke());

            string localizedSpellName = LocalizationManager.Instance.GetText(_spellData.Spell.ToString());
            string localizedSpellDescription = _spellData.GetLocalizedSpellDescription();
            tooltipTrigger.SetUpToolTip(localizedSpellName, localizedSpellDescription, _delay: 0.25f);

            if(backgroundImage != null && _spellData.RaceData != null)
            {
                Color raceColor = _spellData.RaceData != null ? _spellData.RaceData.Race switch
                {
                    Race.IronLegion      => _spellData.RaceData.PrimaryColor,
                    Race.Gruntkin        => _spellData.RaceData.PrimaryColor,
                    Race.RavenHost       => _spellData.RaceData.PrimaryColor,
                    Race.TaelindorForest => _spellData.RaceData.PrimaryColor,
                    Race.SanguineCourt   => _spellData.RaceData.SecondaryColor,
                    Race.SakuraDynasty   => _spellData.RaceData.SecondaryColor,
                    Race.DeepstoneHold   => _spellData.RaceData.PrimaryColor,
                    Race.DrakosaurBrood  => _spellData.RaceData.PrimaryColor,
                    _                    => _spellData.RaceData.PrimaryColor,
                } : Color.white;

                float alpha = _spellData.RaceData.Race switch
                {
                    Race.IronLegion      => 25f,
                    Race.Gruntkin        => 85f,
                    Race.RavenHost       => 65f,
                    Race.TaelindorForest => 15f,
                    Race.SanguineCourt   => 10f,
                    Race.SakuraDynasty   => 10f,
                    Race.DeepstoneHold   => 35f,
                    Race.DrakosaurBrood  => 25f,
                    _                    => 25f,
                };

                backgroundImage.color = new Color(raceColor.r, raceColor.g, raceColor.b, alpha / 255f);
            }
        }

        /// <summary>Dims and disables the row when its spell is already equipped, without moving it.</summary>
        public void SetEquipped(bool equipped)
        {
            selectButton.interactable = !equipped;
            spellIcon.color = equipped ? equippedTint : defaultIconColor;
        }
    }
}
