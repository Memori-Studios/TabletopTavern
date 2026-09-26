using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Memori.Localization;
using Memori.Utilities;

namespace TJ
{
    public static class ColorData
    {
        public static string Primary = "#ECF0F1";
        public static string Secondary = "#BDC3C7";

        public static string Player => Pick(PlayerColors);
        public static string Enemy => Pick(EnemyColors);

        public static string Green => Pick(GreenColors);
        public static string Error => Pick(ErrorColors);

        public static string Tier1 = "#BDC3C7";
        public static string Tier2 = "#8AFA88";
        public static string Tier3 = "#BA88FA";
        public static string Tier4 = "#F1C40F";

        // For events
        public static string Positive => Pick(PositiveColors);
        public static string Negative => Pick(NegativeColors);

        public static string Gold = "#E3BB71";
        public static string TroopHealth = "#E37188";
        public static string GearDrop = "#95A5A6";
        public static string UnitStat = "#E3BB71";

        // Battlefield
        public static string MinimapPlayer => Pick(MinimapPlayerColors);
        public static string MinimapEnemy => Pick(MinimapEnemyColors);
        public static string PlayerTeamOutline => Pick(PlayerTeamOutlineColors);
        public static string EnemyTeamOutline => Pick(EnemyTeamOutlineColors);

        #region Colorblind Mode

        // Today's colour first, its Colorblind Mode version second.
        static readonly string[] PlayerColors = Good("#D49B39");
        static readonly string[] EnemyColors = Bad("#D44339");
        static readonly string[] GreenColors = Good("#43F86C");
        static readonly string[] ErrorColors = Bad("#CC2626");
        static readonly string[] PositiveColors = Good("#47D439");
        static readonly string[] NegativeColors = Bad("#D44339");
        static readonly string[] MinimapPlayerColors = Good("#15ff00ff");
        static readonly string[] MinimapEnemyColors = Bad("#ff0000ff");
        static readonly string[] PlayerTeamOutlineColors = Good("#FFE300");
        static readonly string[] EnemyTeamOutlineColors = Bad("#FF0000");

        static string[] Good(string hex) => new[] { hex, ColorVision.Blue(hex) };
        static string[] Bad(string hex) => new[] { hex, ColorVision.Orange(hex) };
        static string Pick(string[] colors) => ColorVision.IsOn ? colors[1] : colors[0];

        /// <summary>Localized text that embeds today's green and red hex codes gets their Colorblind Mode versions.</summary>
        public static string ApplyColorVision(string text)
        {
            if (!ColorVision.IsOn || string.IsNullOrEmpty(text)) return text;
            return text.Replace(GreenColors[0], GreenColors[1], System.StringComparison.OrdinalIgnoreCase)
                       .Replace(ErrorColors[0], ErrorColors[1], System.StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        public static Vector4 HexToRgba(string hexColor)
        {
            hexColor = hexColor.TrimStart('#'); // Remove '#' if present

            // Parse hexadecimal values for red, green, blue, and alpha components
            int r = int.Parse(hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            int g = int.Parse(hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            int b = int.Parse(hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            int a = hexColor.Length == 8 ? int.Parse(hexColor.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) : 255;

            // Normalize the color values from 0-255 to 0-1 range
            float rf = r / 255f;
            float gf = g / 255f;
            float bf = b / 255f;
            float af = a / 255f;

            return new Vector4(rf, gf, bf, af);
        }
        public static Vector4 GetEventOutcomeColor(string _eventOutcome)
        {
            return _eventOutcome switch
            {
                "PositiveReputation" => HexToRgba(Positive),
                "NegativeReputation" => HexToRgba(Negative),
                "Gold" => HexToRgba(Gold),
                "TroopHealth" => HexToRgba(TroopHealth),
                "GearDrop" => HexToRgba(GearDrop),
                "PresitgeUnit" => HexToRgba(Gold),
                _ => HexToRgba(Primary)
            };
        }
        public static Vector4 GetGearRarityColor(GearRarity _gearRarity)
        {
            return _gearRarity switch
            {
                GearRarity.Common => HexToRgba(Tier1),
                GearRarity.Uncommon => HexToRgba(Tier2),
                GearRarity.Rare => HexToRgba(Tier3),
                _ => HexToRgba(Primary)
            };
        }
        public static string GetGearRarityColorString(GearRarity _gearRarity)
        {
            return _gearRarity switch
            {
                GearRarity.Common => Tier1,
                GearRarity.Uncommon => Tier2,
                GearRarity.Rare => Tier3,
                _ => Primary
            };
        }
        /// <summary>The gear's rarity word in its rarity colour, so rarity reads without relying on the card tint.</summary>
        public static string GearRarityLabel(GearRarity _gearRarity)
        {
            return $"<color={GetGearRarityColorString(_gearRarity)}>[{LocalizationManager.Instance.GetText(_gearRarity.ToString())}]</color>";
        }
        public static Vector4 GetUnitStatColor(UnitStat _unitStat)
        {
            return HexToRgba(UnitStat);
        }
        public static Vector4 GetRarityTierColor(UnitRarity _tier)
        {
            return _tier switch
            {
                UnitRarity.Common => HexToRgba(Tier1),
                UnitRarity.Uncommon => HexToRgba(Tier2),
                UnitRarity.Rare => HexToRgba(Tier3),
                UnitRarity.Legendary => HexToRgba(Tier4),
                _ => HexToRgba(Primary)
            };
        }
        public static string GetRarityTierColorString(UnitRarity _tier)
        {
            return _tier switch
            {
                UnitRarity.Common => Tier1,
                UnitRarity.Uncommon => Tier2,
                UnitRarity.Rare => Tier3,
                UnitRarity.Legendary => Tier4,
                _ => Primary
            };
        }
        public static Vector4 GetTeamMinimapColor(bool _isPlayerTeam)
        {
            return _isPlayerTeam ? HexToRgba(MinimapPlayer) : HexToRgba(MinimapEnemy);
        }
        public static Color GetColorBasedOnAffordability(bool _canAfford)
        {
            return _canAfford ? (Color)HexToRgba(Primary) : (Color)HexToRgba(Error);
        }

        /// <summary> Returns the color with its alpha replaced by <paramref name="alpha255"/> on a 0-255 scale.</summary>
        public static Color WithAlpha255(Color color, float alpha255)
        {
            color.a = alpha255 / 255f;
            return color;
        }

        /// <summary>
        /// The opaque race-passive tint color: each race's PrimaryColor, except Sanguine Court and
        /// Sakura Dynasty which read better with their SecondaryColor. White if raceData is missing.
        /// Pair with <see cref="GetRacePassiveAlpha"/> and <see cref="WithAlpha255"/> for the faded fill.
        /// </summary>
        public static Color GetRacePassiveColor(Race race, RaceData raceData)
        {
            if (raceData == null) return Color.white;
            return race switch
            {
                Race.SanguineCourt => raceData.SecondaryColor,
                Race.SakuraDynasty => raceData.SecondaryColor,
                _                  => raceData.PrimaryColor,
            };
        }

        /// <summary>The race-passive tint with its per-race fill alpha already applied (GetRacePassiveColor + GetRacePassiveAlpha).</summary>
        public static Color GetRacePassiveTint(Race race, RaceData raceData)
        {
            return WithAlpha255(GetRacePassiveColor(race, raceData), GetRacePassiveAlpha(race));
        }
        
        #region Race display ramp

        // Foreground-safe faction colours, for icons and rails that sit ON a dark tile.
        //
        // RaceData's authored colours are banner fills - large areas of heraldry, where dark and muddy is
        // correct. Four of the nine are unusable as a foreground: measured against the spell tile ground
        // #2B3648, Gruntkin's #2B432F is 1.13:1 (the glyph disappears), and Raven Host, Deepstone Hold and
        // Iron Legion are barely better. GetRacePassiveColor already patches this by hand for Sanguine
        // Court and Sakura Dynasty, which read from SecondaryColor because their primaries are near black.
        // This generalises that patch to all nine.
        //
        // Derivation: take the authored colour to HSL, KEEP THE HUE (that is the faction's identity and it
        // must not drift), then floor saturation at 55% and normalise lightness to 62% so every faction
        // lands at the same perceived weight. Two hand corrections after that, because the source palette
        // has neighbours: Deepstone Hold is held down in saturation so its bronze does not fight
        // Taelindor's gold, and Iron Legion is nudged toward orange to separate its rust from Drakosaur's
        // coral. Iron Legion / Drakosaur and Taelindor / Deepstone remain close pairs - they are close in
        // the source art too, and the grouped layout in SpellBrowseMenu is what disambiguates them.
        //
        // GetRacePassiveColor is the large-fill pair and is deliberately left alone - SquadBattleInfo and
        // the run-setup grimoire rows tint backgrounds with it, where the authored colour is correct.
        public static string DisplayIronLegion = "#E27B58";
        public static string DisplayGruntkin = "#6DC47C";
        public static string DisplayRavenHost = "#8A85F2";
        public static string DisplayTaelindorForest = "#EFC169";
        public static string DisplaySanguineCourt = "#FF5C7E";
        public static string DisplaySakuraDynasty = "#F5A2E8";
        public static string DisplayDeepstoneHold = "#C6A47D";
        public static string DisplayDrakosaurBrood = "#F76F79";
        public static string DisplaySpecial = "#A8B8CB";

        /// <summary>
        /// Alpha (0-255) for the faction wash behind a spell icon. One value for every race, unlike
        /// <see cref="GetRacePassiveAlpha"/> - the display ramp is already lightness-normalised, so it
        /// does not need a per-race correction.
        ///
        /// Tuned down from 77 to 10 against the real battle scene. At this strength the wash is barely
        /// a tint and the icon and rail carry the faction almost entirely, which is the Variant A
        /// intent - the rail exists precisely so the wash does not have to be loud. Consequence worth
        /// knowing: the equipped-row wash dimming is now imperceptible, so that state reads on the
        /// icon alpha, the rail and the frame instead.
        /// </summary>
        public const float RACE_DISPLAY_WASH_ALPHA = 10f;

        /// <summary>
        /// The opaque foreground faction colour. Unlike <see cref="GetRacePassiveColor"/> this takes no
        /// RaceData: the ramp is authored here so the contrast guarantee holds no matter what a mod's
        /// race_overrides.json does to the banner colours.
        /// </summary>
        public static Color GetRaceDisplayColor(Race race)
        {
            return race switch
            {
                Race.IronLegion      => (Color)HexToRgba(DisplayIronLegion),
                Race.Gruntkin        => (Color)HexToRgba(DisplayGruntkin),
                Race.RavenHost       => (Color)HexToRgba(DisplayRavenHost),
                Race.TaelindorForest => (Color)HexToRgba(DisplayTaelindorForest),
                Race.SanguineCourt   => (Color)HexToRgba(DisplaySanguineCourt),
                Race.SakuraDynasty   => (Color)HexToRgba(DisplaySakuraDynasty),
                Race.DeepstoneHold   => (Color)HexToRgba(DisplayDeepstoneHold),
                Race.DrakosaurBrood  => (Color)HexToRgba(DisplayDrakosaurBrood),
                _                    => (Color)HexToRgba(DisplaySpecial),
            };
        }

        /// <summary>The foreground faction colour at wash alpha, for the gradient behind a spell icon.</summary>
        public static Color GetRaceDisplayTint(Race race)
        {
            return WithAlpha255(GetRaceDisplayColor(race), RACE_DISPLAY_WASH_ALPHA);
        }

        #endregion

        #region Spell tile frame states

        // Frame colours for spell tiles and hotbar slots. Deliberately achromatic, and this is load
        // bearing rather than a taste call: once the nine faction hues are spent on identity, no hue is
        // left for state. Green is Gruntkin, gold is Taelindor, crimson is Sanguine Court, indigo is
        // Raven Host. State reads on brightness and geometry instead.
        /// <summary>Fully transparent, for a frame drawn OVER a tile that already has its own base edge.</summary>
        public static Color SpellFrameIdle => new Color(1f, 1f, 1f, 0f);
        /// <summary>A dim visible edge, for a frame Image that IS the control's only border.</summary>
        public static Color SpellFrameRest => WithAlpha255((Color)HexToRgba(Secondary), 60f);
        public static Color SpellFrameHover => (Color)HexToRgba(Primary);
        public static Color SpellFrameEquipped => WithAlpha255((Color)HexToRgba(Primary), 128f);
        public static Color SpellFrameActive => Color.white;

        #endregion

        #region Mana

        // Tints for the white mana sprites (orb, gem strip, cost gems). Preview is the "this is what the
        // hovered spell spends" pulse target; it must stay distinct from Full at a glance.
        public static Color ManaFull => new Color32(90, 182, 255, 255);
        public static Color ManaPreview => Color.white;
        public static Color ManaPreviewBand => new Color32(168, 220, 255, 130);
        public static Color ManaEmpty => new Color32(34, 48, 58, 255);
        // Cost gems carry white text, so they sit darker than the strip gems.
        public static Color ManaCostGem => new Color32(42, 120, 200, 255);
        public static Color ManaUnaffordable => new Color32(178, 58, 51, 255);

        #endregion

        /// <summary>Per-race fill alpha (0-255) for the race-passive tint, hand-tuned per palette.</summary>
        public static float GetRacePassiveAlpha(Race race)
        {
            return race switch
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
        }
    }
}
