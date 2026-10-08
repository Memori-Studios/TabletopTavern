using UnityEngine;
using UnityEngine.UI;
using TJ.Map;

namespace TJ
{
    /// <summary>Shows the icon on each custom battle Map Settings row; the choices stay in CustomBattleUIManager.</summary>
    public class MapSettingsView : MonoBehaviour
    {
        [Header("Row icons")]
        [SerializeField] private Image weatherIcon;
        [SerializeField] private Image biomeIcon;
        [SerializeField] private Image garrisonIcon;

        [Header("Sprites")]
        // Indexed by the Weather enum.
        [SerializeField] private Sprite[] weatherSprites;
        // Indexed by the Biome enum.
        [SerializeField] private Sprite[] biomeSprites;
        [SerializeField] private Sprite garrisonNoneSprite;
        [SerializeField] private Sprite garrisonWallsSprite;

        [Header("Tints")]
        [SerializeField] private Color iconColor = new(0.914f, 0.753f, 0.416f);
        [SerializeField] private Color dimColor = new(0.549f, 0.604f, 0.635f);

        public void ShowWeather(Weather weather) => Show(weatherIcon, weatherSprites, (int)weather, iconColor);

        public void ShowBiome(Biome biome, bool enabled) => Show(biomeIcon, biomeSprites, (int)biome, enabled ? iconColor : dimColor);

        // -1 is no walls, the dropdown's None.
        public void ShowGarrison(int townSize)
        {
            if (garrisonIcon == null) return;
            garrisonIcon.sprite = townSize < 0 ? garrisonNoneSprite : garrisonWallsSprite;
            garrisonIcon.color = townSize < 0 ? dimColor : iconColor;
        }

        private static void Show(Image icon, Sprite[] sprites, int index, Color color)
        {
            if (icon == null) return;
            if (sprites != null && index >= 0 && index < sprites.Length && sprites[index] != null) icon.sprite = sprites[index];
            icon.color = color;
        }
    }
}
