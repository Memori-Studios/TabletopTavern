using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Memori.Localization;

namespace TJ
{
    public static class ColorData
    {
        public static string Primary = "#ECF0F1";
        public static string Secondary = "#BDC3C7";

        public static string Player = "#D49B39";
        public static string Enemy = "#D44339";

        public static string Green = "#43F86C";
        public static string Error = "#CC2626";

        public static string Tier1 = "#BDC3C7";
        public static string Tier2 = "#8AFA88";
        public static string Tier3 = "#BA88FA";
        public static string Tier4 = "#F1C40F";

        // For events
        public static string Positive = "#47D439";
        public static string Negative = "#D44339";

        public static string Gold = "#E3BB71";
        public static string TroopHealth = "#E37188";
        public static string GearDrop = "#95A5A6";
        public static string UnitStat = "#E3BB71";

        // Battlefield
        public static string MinimapPlayer = "#15ff00ff";
        public static string MinimapEnemy = "#ff0000ff";
        public static string PlayerTeamOutline = "#FFE300";
        public static string EnemyTeamOutline = "#FF0000";

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
        public static string XMLTagColorApplicator(ref string _text)
        {
            if (_text.Length <= 0) return _text;

            string commonLocalized = "[" + LocalizationManager.Instance.GetText("Common") + "]";
            string uncommonLocalized = "[" + LocalizationManager.Instance.GetText("Uncommon") + "]";
            string rareLocalized = "[" + LocalizationManager.Instance.GetText("Rare") + "]";
            string legendaryLocalized = "[" + LocalizationManager.Instance.GetText("Legendary") + "]";

            //if text contains commonlocalized, replace common localized with the colordata xml tag
            if (_text.Contains(commonLocalized))
            {
                _text = _text.Replace(commonLocalized, $"<color={Tier1}>{commonLocalized}</color>");
            }
            if (_text.Contains(uncommonLocalized))
            {
                _text = _text.Replace(uncommonLocalized, $"<color={Tier2}>{uncommonLocalized}</color>");
            }
            if (_text.Contains(rareLocalized))
            {
                _text = _text.Replace(rareLocalized, $"<color={Tier3}>{rareLocalized}</color>");
            }
            if (_text.Contains(legendaryLocalized))
            {
                _text = _text.Replace(legendaryLocalized, $"<color={Tier4}>{legendaryLocalized}</color>");
            }

            string tier1Localized = "[" + LocalizationManager.Instance.GetText("Tier I") + "]";
            string tier2Localized = "[" + LocalizationManager.Instance.GetText("Tier II") + "]";
            string tier3Localized = "[" + LocalizationManager.Instance.GetText("Tier III") + "]";
            string tier4Localized = "[" + LocalizationManager.Instance.GetText("Tier IV") + "]";

            //if text contains commonlocalized, replace common localized with the colordata xml tag
            if (_text.Contains(tier1Localized))
            {
                _text = _text.Replace(tier1Localized, $"<color={Tier1}>{tier1Localized}</color>");
            }
            if (_text.Contains(tier2Localized))
            {
                _text = _text.Replace(tier2Localized, $"<color={Tier2}>{tier2Localized}</color>");
            }
            if (_text.Contains(tier3Localized))
            {
                _text = _text.Replace(tier3Localized, $"<color={Tier3}>{tier3Localized}</color>");
            }
            if (_text.Contains(tier4Localized))
            {
                _text = _text.Replace(tier4Localized, $"<color={Tier4}>{tier4Localized}</color>");
            }

            if (_text.Contains("+"))
            {
                int currentIndex = 0;
                while (currentIndex < _text.Length && currentIndex != -1)
                {
                    currentIndex = _text.IndexOf("+", currentIndex);
                    if (currentIndex == -1) break; // No more + found

                    int endIndex = _text.IndexOf(" ", currentIndex);
                    if (endIndex == -1) endIndex = _text.Length; // Use end of string if no space follows

                    if (endIndex > currentIndex)
                    {
                        string substring = _text.Substring(currentIndex, endIndex - currentIndex);
                        _text = _text.Replace(substring, $"<color={Green}>{substring}</color>");
                        currentIndex += substring.Length + Green.Length + 15; // Move past the replaced text (+15 for <color=...></color>)
                    }
                    else
                    {
                        currentIndex++; // Move past this + if no valid substring found
                    }
                }
            }
            // Terms to colour, not a strict UnitStat enum mirror - "Ranged" and "Morale" have no
            // matching enum value. Morale is here so effects that move CurrentMorale can name it
            // instead of misattributing themselves to [Leadership], which is the MaxMorale cap.
            string[] unitStats = new string[] { "MeleeAttack", "MeleeDefense", "WeaponStrength", "Accuracy", "Range", "MissileStrength", "HitPoints", "None", "Speed", "Armor", "ChargeBonus", "Leadership", "Ammunition", "ChargeImpactDamage", "Ranged", "Morale" };
            string[] damageAttributes = new string[] { "None", "ArmorPiercing", "AntiInfantry", "AntiLarge", "ArmorPiercingAntiInfantry", "ArmorPiercingAntiLarge", "Terror", "Outrider", "Rage", "StandardShields", "Terrifying", "Stalwart", "Ethereal", "SwampCreature", "ForestDweller", "ChickenFlight", "BloodFrenzy", "Emblazing", "Unstoppable", "HeavyShields", "ThrowingAxes", "ArmorSundering", "ForgefuryTempering", "FlamingAmmo", "MonsterSlayer", "DragonsHoard", "BackStabbers" };//"TowerShields",

            string[] unitStatLocalized = new string[unitStats.Length];
            for (int i = 0; i < unitStats.Length; i++)
            {
                unitStatLocalized[i] = "[" + LocalizationManager.Instance.GetText(unitStats[i]) + "]";
            }
            string[] damageAttributesLocalized = new string[damageAttributes.Length];
            for (int i = 0; i < damageAttributes.Length; i++)
            {
                damageAttributesLocalized[i] = "[" + LocalizationManager.Instance.GetText(damageAttributes[i]) + "]";
            }

            for (int i = 0; i < unitStatLocalized.Length; i++)
            {
                if (_text.Contains(unitStatLocalized[i]))
                {
                    _text = _text.Replace(unitStatLocalized[i], $"<color={UnitStat}>{unitStatLocalized[i]}</color>");
                }
            }
            for (int i = 0; i < damageAttributesLocalized.Length; i++)
            {
                if (_text.Contains(damageAttributesLocalized[i]))
                {
                    _text = _text.Replace(damageAttributesLocalized[i], $"<color={UnitStat}>{damageAttributesLocalized[i]}</color>");
                }
            }

            return _text;
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
