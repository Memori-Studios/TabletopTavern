using System;
using Memori.Localization;
using Memori.SaveData;
using Memori.Tooltip;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// A Collection item tile showing one gear item on the warband screen. The tile keeps the
    /// Collection look; this adds the gear data, the tooltip and the click.
    /// </summary>
    public class WarbandGearTile : MonoBehaviour
    {
        [SerializeField] private CollectionTile tile;
        [SerializeField] private Button button;
        [SerializeField] private MemoriTooltipTrigger tooltip;

        public GearID GearID { get; private set; }
        public bool Found { get; private set; }

        private Action<GearID> onClick;
        private bool isNew;

        /// <summary>A null <paramref name="_onClick"/> leaves the tile hover-only.</summary>
        public void Set(GearID _gearID, bool _found, bool _isNew, Func<TooltipContent> content, Action<GearID> _onClick)
        {
            GearID = _gearID;
            Found = _found;
            isNew = _found && _isNew;
            onClick = _onClick;

            Gear gear = GearData.GetGear(_gearID);
            tile.SetItem(SpriteData.GetSprite(gear.GearName), (Color)ColorData.GetGearRarityColor(gear.GearRarity), _found, isNew);
            tooltip.SetContentProvider(content);

            // Listens to the Button, not CollectionTile.Clicked: that also fires when keyboard focus
            // lands on the tile, which would equip every item the arrow keys pass over.
            button.onClick.RemoveListener(OnClicked);
            button.onClick.AddListener(OnClicked);
            tile.Hovered -= OnHovered;
            tile.Hovered += OnHovered;
        }

        public void SetState(bool equipped, bool affordable)
        {
            tile.SetSelected(equipped);
            tile.SetDimmed(Found && !equipped && !affordable);
        }

        public void RefreshTooltip() => tooltip.RefreshContent();

        private void OnClicked() => onClick?.Invoke(GearID);

        private void OnHovered(CollectionTile _)
        {
            if (!isNew) return;
            isNew = false;
            SaveDataHandler.AcknowledgedGear(GearID);
            tile.SetNew(false);
        }

        private void OnDestroy()
        {
            if (tile != null) tile.Hovered -= OnHovered;
        }

        /// <summary>The rich tooltip for one gear item. <paramref name="footer"/> may be empty.</summary>
        public static TooltipContent BuildTooltip(GearID gearID, bool found, int cost, string footer)
        {
            Gear gear = GearData.GetGear(gearID);
            var content = new TooltipContent
            {
                Icon = SpriteData.GetSprite(gear.GearName),
                Accent = (Color)ColorData.GetGearRarityColor(gear.GearRarity),
                Subtitle = $"{T(gear.GearRarity.ToString())} · {T("CollectionGear")}",
            };

            if (!found)
            {
                content.IconColor = new Color(0f, 0f, 0f, 0.55f);
                content.Title = T("CollectionNotFound");
                content.Body = T("Obtain in Campaign");
                return content;
            }

            string description = string.Format(T(gearID + "Desc"), gear.GearModifierValue);
            content.Title = T(gearID + "Name");
            content.Subtitle += $" · {cost} <sprite name=GoldSprite>";
            content.Body = KeywordText.ForTooltip(description);
            content.Detail = $"<i>{T(gearID + "Flavor")}</i>";
            content.Footer = footer;
            return content;
        }

        private static string T(string key) => LocalizationManager.Instance.GetText(key);
    }
}
