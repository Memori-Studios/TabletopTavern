using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJ
{
    // Every field is optional and string-typed, same pattern as the other JSON override loaders -
    // a modder writes a unitName plus only the fields they want to change; everything else is
    // left at its current value.
    [Serializable]
    public struct SquadStatsOverrideEntry
    {
        public string unitName;
        public string unitType;
        public string unitSize;
        public string race;
        public string meleeAttack;
        public string meleeDefense;
        public string hitPointsPerUnit;
        public string weaponStrength;
        public string speed;
        public string leadership;
        public string armor;
        public string chargeBonus;
        public string chargeImpactDamage;
        public string chargeCount;
        public string baseRange;
        public string attackAccuracy;
        public string missileStrength;
        public string rarityTier;
        public string baseUnitCount;
        public string attackCooldown;
        public string ammunition;
        public string rateOfFire;
        public string explosionDamage;
        public string explosionRange;
        public string explosionForce;
        public string none;
        public string standardShields;
        public string armorPiercing;
        public string antiInfantry;
        public string antiLarge;
        public string terrifying;
        public string stalwart;
        public string outrider;
        public string swampCreature;
        public string forestDweller;
        public string chickenFlight;
        public string ethereal;
        public string bloodFrenzy;
        public string rage;
        public string emblazing;
        public string unstoppable;
        public string heavyShields;
        public string throwingAxes;
        public string armorSundering;
        public string monsterSlayer;
        public string forgefuryTempering;
        public string flamingAmmo;
        public string dragonsHoard;
        public string backStabbers;
        public string thickScales;
        public string shotDiscipline;
        public string overdraw;
        public string steadyAim;
        public string demolisher;
        public string powderReserves;
        public string deepQuivers;
    }

    [Serializable]
    public class SquadStatsOverrideFile
    {
        public List<SquadStatsOverrideEntry> overrides = new();
    }

    public static class SquadStatsOverrideLoader
    {
        public const string FileName = "unit_overrides.json";
        private const int MaxEntries = 1000;

        public static void ApplyOverridesFromModFolder(string modFolderPath, Dictionary<UnitName, SquadStats> squadStatsDictionary,
            Dictionary<UnitName, SquadAssets> squadAssetsDictionary, Dictionary<Race, List<UnitName>> unitsOfRaceDictionary)
        {
            string path = System.IO.Path.Combine(modFolderPath, FileName);
            string modLabel = ModOverrideValidation.GetModLabel(modFolderPath);

            ModOverrideValidation.TryLoadFile(path,
                () => ApplyJson(System.IO.File.ReadAllText(path), modLabel, squadStatsDictionary, squadAssetsDictionary, unitsOfRaceDictionary),
                $"SquadStats ({modLabel})");
        }

        private static void ApplyJson(string json, string modLabel, Dictionary<UnitName, SquadStats> squadStatsDictionary,
            Dictionary<UnitName, SquadAssets> squadAssetsDictionary, Dictionary<Race, List<UnitName>> unitsOfRaceDictionary)
        {
            var file = JsonUtility.FromJson<SquadStatsOverrideFile>(json);
            if (file?.overrides == null) return;

            if (file.overrides.Count > MaxEntries)
                Debug.LogWarning($"[ModOverride] SquadStats ({modLabel}): file has {file.overrides.Count} entries, only applying the first {MaxEntries}.");

            int applied = 0;
            int entryLimit = Math.Min(file.overrides.Count, MaxEntries);
            for (int i = 0; i < entryLimit; i++)
            {
                var entry = file.overrides[i];
                string context = $"SquadStats ({modLabel}) entry {i} [{entry.unitName}]";

                if (string.IsNullOrEmpty(entry.unitName) || !Enum.TryParse(entry.unitName, out UnitName unitName))
                {
                    Debug.LogWarning($"[ModOverride] {context}: missing or unknown unitName '{entry.unitName}', skipping.");
                    continue;
                }
                if (!squadStatsDictionary.TryGetValue(unitName, out SquadStats stats))
                {
                    Debug.LogWarning($"[ModOverride] {context}: UnitName '{unitName}' not found, skipping.");
                    continue;
                }

                if (ModOverrideValidation.TryParseEnumOrWarn(entry.unitType, "unitType", context, out UnitType ut)) stats.unitType = ut;
                if (ModOverrideValidation.TryParseEnumOrWarn(entry.unitSize, "unitSize", context, out UnitSize us)) stats.unitSize = us;
                if (ModOverrideValidation.TryParseEnumOrWarn(entry.rarityTier, "rarityTier", context, out UnitRarity rarity)) stats.RarityTier = rarity;

                if (ModOverrideValidation.TryParseIntOrWarn(entry.meleeAttack, "meleeAttack", context, out int meleeAttack)) stats.MeleeAttack = meleeAttack;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.meleeDefense, "meleeDefense", context, out int meleeDefense)) stats.MeleeDefense = meleeDefense;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.hitPointsPerUnit, "hitPointsPerUnit", context, out int hp)) stats.HitPointsPerUnit = ModOverrideValidation.ValidatePositive(hp, stats.HitPointsPerUnit, "hitPointsPerUnit", context);
                if (ModOverrideValidation.TryParseIntOrWarn(entry.weaponStrength, "weaponStrength", context, out int weaponStrength)) stats.WeaponStrength = weaponStrength;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.speed, "speed", context, out float speed)) stats.Speed = speed;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.leadership, "leadership", context, out float leadership)) stats.Leadership = leadership;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.armor, "armor", context, out int armor)) stats.Armor = armor;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.chargeBonus, "chargeBonus", context, out int chargeBonus)) stats.ChargeBonus = chargeBonus;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.chargeImpactDamage, "chargeImpactDamage", context, out int chargeImpact)) stats.ChargeImactDamage = chargeImpact;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.chargeCount, "chargeCount", context, out int chargeCount)) stats.ChargeCount = chargeCount;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.baseRange, "baseRange", context, out float baseRange)) stats.BaseRange = baseRange;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.attackAccuracy, "attackAccuracy", context, out float accuracy)) stats.attackAccuracy = accuracy;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.missileStrength, "missileStrength", context, out int missileStrength)) stats.MissileStrength = missileStrength;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.baseUnitCount, "baseUnitCount", context, out int baseUnitCount)) stats.baseUnitCount = ModOverrideValidation.ValidatePositive(baseUnitCount, stats.baseUnitCount, "baseUnitCount", context);
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.attackCooldown, "attackCooldown", context, out float cooldown)) stats.attackCooldown = cooldown;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.ammunition, "ammunition", context, out int ammunition)) stats.Ammunition = ammunition;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.rateOfFire, "rateOfFire", context, out float rateOfFire)) stats.rateOfFire = rateOfFire;
                if (ModOverrideValidation.TryParseIntOrWarn(entry.explosionDamage, "explosionDamage", context, out int explosionDamage)) stats.ExplosionDamage = explosionDamage;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.explosionRange, "explosionRange", context, out float explosionRange)) stats.ExplosionRange = explosionRange;
                if (ModOverrideValidation.TryParseFloatOrWarn(entry.explosionForce, "explosionForce", context, out float explosionForce)) stats.ExplosionForce = explosionForce;

                if (ModOverrideValidation.TryParseBoolOrWarn(entry.none, "none", context, out bool none)) stats.SquadAttributes.None = none;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.standardShields, "standardShields", context, out bool standardShields)) stats.SquadAttributes.StandardShields = standardShields;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.armorPiercing, "armorPiercing", context, out bool armorPiercing)) stats.SquadAttributes.ArmorPiercing = armorPiercing;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.antiInfantry, "antiInfantry", context, out bool antiInfantry)) stats.SquadAttributes.AntiInfantry = antiInfantry;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.antiLarge, "antiLarge", context, out bool antiLarge)) stats.SquadAttributes.AntiLarge = antiLarge;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.terrifying, "terrifying", context, out bool terrifying)) stats.SquadAttributes.Terrifying = terrifying;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.stalwart, "stalwart", context, out bool stalwart)) stats.SquadAttributes.Stalwart = stalwart;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.outrider, "outrider", context, out bool outrider)) stats.SquadAttributes.Outrider = outrider;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.swampCreature, "swampCreature", context, out bool swampCreature)) stats.SquadAttributes.SwampCreature = swampCreature;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.forestDweller, "forestDweller", context, out bool forestDweller)) stats.SquadAttributes.ForestDweller = forestDweller;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.chickenFlight, "chickenFlight", context, out bool chickenFlight)) stats.SquadAttributes.ChickenFlight = chickenFlight;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.ethereal, "ethereal", context, out bool ethereal)) stats.SquadAttributes.Ethereal = ethereal;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.bloodFrenzy, "bloodFrenzy", context, out bool bloodFrenzy)) stats.SquadAttributes.BloodFrenzy = bloodFrenzy;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.rage, "rage", context, out bool rage)) stats.SquadAttributes.Rage = rage;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.emblazing, "emblazing", context, out bool emblazing)) stats.SquadAttributes.Emblazing = emblazing;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.unstoppable, "unstoppable", context, out bool unstoppable)) stats.SquadAttributes.Unstoppable = unstoppable;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.heavyShields, "heavyShields", context, out bool heavyShields)) stats.SquadAttributes.HeavyShields = heavyShields;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.throwingAxes, "throwingAxes", context, out bool throwingAxes)) stats.SquadAttributes.ThrowingAxes = throwingAxes;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.armorSundering, "armorSundering", context, out bool armorSundering)) stats.SquadAttributes.ArmorSundering = armorSundering;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.monsterSlayer, "monsterSlayer", context, out bool monsterSlayer)) stats.SquadAttributes.MonsterSlayer = monsterSlayer;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.forgefuryTempering, "forgefuryTempering", context, out bool forgefuryTempering)) stats.SquadAttributes.ForgefuryTempering = forgefuryTempering;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.flamingAmmo, "flamingAmmo", context, out bool flamingAmmo)) stats.SquadAttributes.FlamingAmmo = flamingAmmo;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.dragonsHoard, "dragonsHoard", context, out bool dragonsHoard)) stats.SquadAttributes.DragonsHoard = dragonsHoard;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.backStabbers, "backStabbers", context, out bool backStabbers)) stats.SquadAttributes.BackStabbers = backStabbers;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.thickScales, "thickScales", context, out bool thickScales)) stats.SquadAttributes.ThickScales = thickScales;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.shotDiscipline, "shotDiscipline", context, out bool shotDiscipline)) stats.SquadAttributes.ShotDiscipline = shotDiscipline;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.overdraw, "overdraw", context, out bool overdraw)) stats.SquadAttributes.Overdraw = overdraw;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.steadyAim, "steadyAim", context, out bool steadyAim)) stats.SquadAttributes.SteadyAim = steadyAim;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.demolisher, "demolisher", context, out bool demolisher)) stats.SquadAttributes.Demolisher = demolisher;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.powderReserves, "powderReserves", context, out bool powderReserves)) stats.SquadAttributes.PowderReserves = powderReserves;
                if (ModOverrideValidation.TryParseBoolOrWarn(entry.deepQuivers, "deepQuivers", context, out bool deepQuivers)) stats.SquadAttributes.DeepQuivers = deepQuivers;

                squadStatsDictionary[unitName] = stats;

                if (ModOverrideValidation.TryParseEnumOrWarn(entry.race, "race", context, out Race newRace))
                    ApplyRaceMove(unitName, newRace, squadStatsDictionary, squadAssetsDictionary, unitsOfRaceDictionary);

                applied++;
            }

            Debug.Log($"[ModOverride] SquadStats ({modLabel}): applied overrides for {applied} unit(s).");
        }

        // Moves a unit between race rosters, keeping SquadAssets.race and UnitsOfRaceDictionary
        // (the roster list every Collection/army-builder UI reads) in sync - mirrors the sort
        // TabletopTavernData.LoadStatsFromSOs does at boot.
        private static void ApplyRaceMove(UnitName unitName, Race newRace, Dictionary<UnitName, SquadStats> squadStatsDictionary,
            Dictionary<UnitName, SquadAssets> squadAssetsDictionary, Dictionary<Race, List<UnitName>> unitsOfRaceDictionary)
        {
            if (!squadAssetsDictionary.TryGetValue(unitName, out SquadAssets assets)) return;
            if (assets.race == newRace) return;

            if (unitsOfRaceDictionary.TryGetValue(assets.race, out var oldList))
                oldList.Remove(unitName);

            if (!unitsOfRaceDictionary.TryGetValue(newRace, out var newList))
            {
                newList = new List<UnitName>();
                unitsOfRaceDictionary[newRace] = newList;
            }
            if (!newList.Contains(unitName))
            {
                newList.Add(unitName);
                newList.Sort((a, b) => squadStatsDictionary[a].RarityTier.CompareTo(squadStatsDictionary[b].RarityTier));
            }

            assets.race = newRace;
            squadAssetsDictionary[unitName] = assets;
        }

        // Exports every unit fully populated - a complete, correct starting point a modder trims
        // down to just the fields they actually want to change.
        public static string ExportTemplate(Dictionary<UnitName, SquadStats> squadStatsDictionary, Dictionary<UnitName, SquadAssets> squadAssetsDictionary)
        {
            var file = new SquadStatsOverrideFile();
            foreach (var kvp in new SortedDictionary<UnitName, SquadStats>(squadStatsDictionary))
            {
                var s = kvp.Value;
                var a = s.SquadAttributes;
                file.overrides.Add(new SquadStatsOverrideEntry
                {
                    unitName = s.unitName.ToString(),
                    unitType = s.unitType.ToString(),
                    unitSize = s.unitSize.ToString(),
                    race = squadAssetsDictionary.TryGetValue(kvp.Key, out SquadAssets assets) ? assets.race.ToString() : Race.Special.ToString(),
                    meleeAttack = s.MeleeAttack.ToString(),
                    meleeDefense = s.MeleeDefense.ToString(),
                    hitPointsPerUnit = s.HitPointsPerUnit.ToString(),
                    weaponStrength = s.WeaponStrength.ToString(),
                    speed = s.Speed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    leadership = s.Leadership.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    armor = s.Armor.ToString(),
                    chargeBonus = s.ChargeBonus.ToString(),
                    chargeImpactDamage = s.ChargeImactDamage.ToString(),
                    chargeCount = s.ChargeCount.ToString(),
                    baseRange = s.BaseRange.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    attackAccuracy = s.attackAccuracy.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    missileStrength = s.MissileStrength.ToString(),
                    rarityTier = s.RarityTier.ToString(),
                    baseUnitCount = s.baseUnitCount.ToString(),
                    attackCooldown = s.attackCooldown.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ammunition = s.Ammunition.ToString(),
                    rateOfFire = s.rateOfFire.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    explosionDamage = s.ExplosionDamage.ToString(),
                    explosionRange = s.ExplosionRange.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    explosionForce = s.ExplosionForce.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    none = a.None.ToString(),
                    standardShields = a.StandardShields.ToString(),
                    armorPiercing = a.ArmorPiercing.ToString(),
                    antiInfantry = a.AntiInfantry.ToString(),
                    antiLarge = a.AntiLarge.ToString(),
                    terrifying = a.Terrifying.ToString(),
                    stalwart = a.Stalwart.ToString(),
                    outrider = a.Outrider.ToString(),
                    swampCreature = a.SwampCreature.ToString(),
                    forestDweller = a.ForestDweller.ToString(),
                    chickenFlight = a.ChickenFlight.ToString(),
                    ethereal = a.Ethereal.ToString(),
                    bloodFrenzy = a.BloodFrenzy.ToString(),
                    rage = a.Rage.ToString(),
                    emblazing = a.Emblazing.ToString(),
                    unstoppable = a.Unstoppable.ToString(),
                    heavyShields = a.HeavyShields.ToString(),
                    throwingAxes = a.ThrowingAxes.ToString(),
                    armorSundering = a.ArmorSundering.ToString(),
                    monsterSlayer = a.MonsterSlayer.ToString(),
                    forgefuryTempering = a.ForgefuryTempering.ToString(),
                    flamingAmmo = a.FlamingAmmo.ToString(),
                    dragonsHoard = a.DragonsHoard.ToString(),
                    backStabbers = a.BackStabbers.ToString(),
                    thickScales = a.ThickScales.ToString(),
                    shotDiscipline = a.ShotDiscipline.ToString(),
                    overdraw = a.Overdraw.ToString(),
                    steadyAim = a.SteadyAim.ToString(),
                    demolisher = a.Demolisher.ToString(),
                    powderReserves = a.PowderReserves.ToString(),
                    deepQuivers = a.DeepQuivers.ToString(),
                });
            }

            return JsonUtility.ToJson(file, true);
        }
    }
}
