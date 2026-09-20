using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TJ
{
    // Sparse, string-typed like race_bonus_overrides.json: a modder sets only the values they want
    // to change. The rain/snow/fog blocks write into WeatherRuleData (Components assembly). Each
    // regionWeathers entry replaces that region's whole weather table, the same way a mod's
    // hero_bonus_rules list replaces a hero's rule set; the region is keyed by the race whose
    // campaign it hosts, and MapRegion.GetPossibleWeathers reads the replacement instead of the
    // asset, so the ScriptableObject is never written.
    [Serializable]
    public struct RainOverrideEntry
    {
        public string largeUnitSpeedModifier;
        public string removesChargeBonus;
        public string autoResolveAccuracyModifier;
    }

    [Serializable]
    public struct SnowOverrideEntry
    {
        public string moralePenalty;
    }

    [Serializable]
    public struct FogOverrideEntry
    {
        public string accuracyModifier;
        public string rangeModifier;
    }

    [Serializable]
    public struct WeatherLikelihoodEntry
    {
        public string weather;
        public string likelihood;
    }

    [Serializable]
    public struct RegionWeatherEntry
    {
        public string race;
        public List<WeatherLikelihoodEntry> weathers;
    }

    [Serializable]
    public class WeatherOverrideFile
    {
        public RainOverrideEntry rain;
        public SnowOverrideEntry snow;
        public FogOverrideEntry fog;
        public List<RegionWeatherEntry> regionWeathers = new();
    }

    public static class WeatherOverrideLoader
    {
        public const string FileName = "weather_overrides.json";

        private static readonly Dictionary<Race, List<MapRegion.WeatherLikelihood>> _regionWeathers = new();

        // Called once per TabletopTavernData.ApplyModOverrides() before the mod loop.
        public static void ClearOverrides()
        {
            WeatherRuleData.ClearOverrides();
            _regionWeathers.Clear();
        }

        public static bool TryGetRegionWeathers(Race race, out List<MapRegion.WeatherLikelihood> weathers)
        {
            return _regionWeathers.TryGetValue(race, out weathers);
        }

        public static void ApplyOverridesFromModFolder(string modFolderPath)
        {
            string path = Path.Combine(modFolderPath, FileName);
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);

            ModOverrideValidation.TryLoadFile(path,
                () => ApplyJson(File.ReadAllText(path), modLabel),
                $"Weather ({modLabel})");
        }

        private static void ApplyJson(string json, string modLabel)
        {
            var file = JsonUtility.FromJson<WeatherOverrideFile>(json);
            if (file == null) return;

            ApplyRain(file.rain, $"Weather ({modLabel}) rain");
            ApplySnow(file.snow, $"Weather ({modLabel}) snow");
            ApplyFog(file.fog, $"Weather ({modLabel}) fog");

            int regions = 0;
            if (file.regionWeathers != null)
            {
                foreach (RegionWeatherEntry entry in file.regionWeathers)
                {
                    if (ApplyRegion(entry, $"Weather ({modLabel}) regionWeathers '{entry.race}'")) regions++;
                }
            }

            Debug.Log($"[ModOverride] Weather ({modLabel}): applied weather overrides, {regions} region table(s) replaced.");
        }

        private static void ApplyRain(RainOverrideEntry e, string context)
        {
            var c = WeatherRuleData.Rain;
            // The removal branch divides speed back out, so zero would leave rained-on units frozen.
            if (TryPositiveFloat(e.largeUnitSpeedModifier, "largeUnitSpeedModifier", context, out float speed)) c.LargeUnitSpeedModifier = speed;
            if (ModOverrideValidation.TryParseBoolOrWarn(e.removesChargeBonus, "removesChargeBonus", context, out bool charge)) c.RemovesChargeBonus = charge;
            if (TryNonNegativeFloat(e.autoResolveAccuracyModifier, "autoResolveAccuracyModifier", context, out float acc)) c.AutoResolveAccuracyModifier = acc;
            WeatherRuleData.Rain = c;
        }

        private static void ApplySnow(SnowOverrideEntry e, string context)
        {
            var c = WeatherRuleData.Snow;
            if (ModOverrideValidation.TryParseFloatOrWarn(e.moralePenalty, "moralePenalty", context, out float penalty)) c.MoralePenalty = penalty;
            WeatherRuleData.Snow = c;
        }

        private static void ApplyFog(FogOverrideEntry e, string context)
        {
            var c = WeatherRuleData.Fog;
            if (TryNonNegativeFloat(e.accuracyModifier, "accuracyModifier", context, out float acc)) c.AccuracyModifier = acc;
            if (TryNonNegativeFloat(e.rangeModifier, "rangeModifier", context, out float range)) c.RangeModifier = range;
            WeatherRuleData.Fog = c;
        }

        // A region table is all-or-nothing: one bad row rejects the whole entry so the region never
        // ends up with a partial list that rolls differently from what the modder wrote.
        private static bool ApplyRegion(RegionWeatherEntry entry, string context)
        {
            if (!ModOverrideValidation.TryParseEnumOrWarn(entry.race, "race", context, out Race race))
            {
                Debug.LogWarning($"[ModOverride] {context}: missing or unknown race, skipping.");
                return false;
            }
            if (entry.weathers == null || entry.weathers.Count == 0)
            {
                Debug.LogWarning($"[ModOverride] {context}: weathers list is empty, skipping.");
                return false;
            }

            var table = new List<MapRegion.WeatherLikelihood>(entry.weathers.Count);
            float total = 0f;
            foreach (WeatherLikelihoodEntry row in entry.weathers)
            {
                if (!ModOverrideValidation.TryParseEnumOrWarn(row.weather, "weather", context, out Weather weather))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or unknown weather '{row.weather}', skipping region.");
                    return false;
                }
                if (!TryNonNegativeFloat(row.likelihood, "likelihood", context, out float likelihood))
                {
                    Debug.LogWarning($"[ModOverride] {context}: {weather} has no valid likelihood, skipping region.");
                    return false;
                }
                total += likelihood;
                table.Add(new MapRegion.WeatherLikelihood { weather = weather, likelihood = likelihood });
            }
            if (total <= 0f)
            {
                Debug.LogWarning($"[ModOverride] {context}: likelihoods sum to zero, skipping region.");
                return false;
            }

            _regionWeathers[race] = table;
            return true;
        }

        private static bool TryPositiveFloat(string raw, string field, string context, out float value)
        {
            if (!ModOverrideValidation.TryParseFloatOrWarn(raw, field, context, out value)) return false;
            if (value > 0f) return true;
            Debug.LogWarning($"[ModOverride] {context}: {field} must be positive, ignoring value {value}.");
            return false;
        }

        private static bool TryNonNegativeFloat(string raw, string field, string context, out float value)
        {
            if (!ModOverrideValidation.TryParseFloatOrWarn(raw, field, context, out value)) return false;
            if (value >= 0f) return true;
            Debug.LogWarning($"[ModOverride] {context}: {field} must not be negative, ignoring value {value}.");
            return false;
        }

        // Exports the current values as a complete starting point a modder trims down. Regions come
        // from the caller because the MapRegion assets are not in Resources.
        public static string ExportTemplate(IEnumerable<MapRegion> regions)
        {
            var file = new WeatherOverrideFile
            {
                rain = new RainOverrideEntry
                {
                    largeUnitSpeedModifier = F(WeatherRuleData.Rain.LargeUnitSpeedModifier),
                    removesChargeBonus = WeatherRuleData.Rain.RemovesChargeBonus.ToString(),
                    autoResolveAccuracyModifier = F(WeatherRuleData.Rain.AutoResolveAccuracyModifier),
                },
                snow = new SnowOverrideEntry
                {
                    moralePenalty = F(WeatherRuleData.Snow.MoralePenalty),
                },
                fog = new FogOverrideEntry
                {
                    accuracyModifier = F(WeatherRuleData.Fog.AccuracyModifier),
                    rangeModifier = F(WeatherRuleData.Fog.RangeModifier),
                },
            };

            foreach (MapRegion region in regions)
            {
                var entry = new RegionWeatherEntry { race = region.Race.ToString(), weathers = new List<WeatherLikelihoodEntry>() };
                foreach (MapRegion.WeatherLikelihood row in region.GetPossibleWeathers())
                {
                    entry.weathers.Add(new WeatherLikelihoodEntry { weather = row.weather.ToString(), likelihood = F(row.likelihood) });
                }
                file.regionWeathers.Add(entry);
            }

            return JsonUtility.ToJson(file, true);
        }

        private static string F(float value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
