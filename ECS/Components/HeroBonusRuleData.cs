using System.Collections.Generic;

// 1:1 transcription of HeroBonusManager's former hardcoded switch statements into data.
// Rules aren't naturally one-per-file the way units/heroes/races are (a hero may have zero to
// several rules), so the base set stays plain C# data here rather than a ScriptableObject
// asset; mod overrides (hero_bonus_rules.json, see HeroBonusRuleOverrideLoader) replace a
// hero/race's entire rule set for a given stat/attribute key at load time.
//
// Lives in the Components assembly (not alongside HeroBonusManager in the main assembly) so that
// Systems-assembly ECS code can reference it directly via HeroBonusRuleEvaluator - see that file
// for why the mechanical (Burst-adjacent) and display (HeroBonusManager) layers had to split this
// way rather than sharing a single main-assembly source.
public static class HeroBonusRuleData
{
    private static BonusCondition Unconditional() => new() { FilterKind = BonusFilterKind.Unconditional };
    private static BonusCondition Tier(UnitRarity tier) => new() { FilterKind = BonusFilterKind.RarityTier, RequiredRarityTier = tier };
    private static BonusCondition Units(params UnitName[] units) => new() { FilterKind = BonusFilterKind.UnitName, UnitNames = units };
    private static BonusCondition Tag(string tag) => new() { FilterKind = BonusFilterKind.UnitTag, Tag = tag };
    private static BonusCondition Types(params UnitType[] types) => new() { FilterKind = BonusFilterKind.UnitType, UnitTypes = types };
    private static BonusCondition Sizes(params UnitSize[] sizes) => new() { FilterKind = BonusFilterKind.UnitSize, UnitSizes = sizes };
    private static BonusCondition EnemyRaceIs(Race race) => new() { FilterKind = BonusFilterKind.EnemyRace, RequiredEnemyRace = race };

    public static readonly List<HeroStatBonusRule> BaseStatRules = new()
    {
        // Hero 1 - Edric Valeward
        new() { HeroID = 1, LocalizationKey = "heroBonusTitle2", Stat = UnitStat.ChargeBonus, Condition = Unconditional(), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 2 },
        new() { HeroID = 1, LocalizationKey = "heroBonusTitle1", Stat = UnitStat.MeleeAttack, Condition = Tier(UnitRarity.Rare), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { HeroID = 1, LocalizationKey = "heroBonusTitle1", Stat = UnitStat.Leadership, Condition = Tier(UnitRarity.Rare), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },

        // Hero 2 - Rhydan Greythorne
        new() { HeroID = 2, LocalizationKey = "heroBonusTitle3", Stat = UnitStat.Accuracy, Condition = Units(UnitName.DeepwoodRangers), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 2, LocalizationKey = "heroBonusTitle3", Stat = UnitStat.MissileStrength, Condition = Units(UnitName.DeepwoodRangers), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { HeroID = 2, LocalizationKey = "heroBonusTitle4", Stat = UnitStat.MeleeDefense, Condition = Tier(UnitRarity.Common), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { HeroID = 2, LocalizationKey = "heroBonusTitle4", Stat = UnitStat.Leadership, Condition = Tier(UnitRarity.Common), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },

        // Hero 3 - Boblin The Goblin King
        new() { HeroID = 3, LocalizationKey = "heroBonusTitle6", Stat = UnitStat.MeleeDefense, Condition = Tag("Goblin"), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 6 },
        new() { HeroID = 3, LocalizationKey = "heroBonusTitle6", Stat = UnitStat.MeleeAttack, Condition = Tag("Goblin"), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 6 },

        // Hero 4 - Kragmuk Gorethirster
        new() { HeroID = 4, LocalizationKey = "heroBonusTitle7", Stat = UnitStat.MeleeAttack, Condition = Units(UnitName.OrcRavagers), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 4, LocalizationKey = "heroBonusTitle7", Stat = UnitStat.WeaponStrength, Condition = Units(UnitName.OrcRavagers), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 5 - Bjorn Ironskull
        new() { HeroID = 5, LocalizationKey = "heroBonusTitle9", Stat = UnitStat.MeleeDefense, Condition = Tag("MeleeInfantry"), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { HeroID = 5, LocalizationKey = "heroBonusTitle9", Stat = UnitStat.Armor, Condition = Tag("MeleeInfantry"), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },

        // Hero 6 - Freyja Stormweaver
        new() { HeroID = 6, LocalizationKey = "heroBonusTitle11", Stat = UnitStat.MeleeDefense, Condition = Units(UnitName.Shieldmaidens), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { HeroID = 6, LocalizationKey = "heroBonusTitle11", Stat = UnitStat.Leadership, Condition = Units(UnitName.Shieldmaidens), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },

        // Hero 7 - Iltharion Starpire
        new() { HeroID = 7, LocalizationKey = "heroBonusTitle13", Stat = UnitStat.ChargeBonus, Condition = Unconditional(), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 5 },
        new() { HeroID = 7, LocalizationKey = "heroBonusTitle13", Stat = UnitStat.MeleeAttack, Condition = Unconditional(), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 8 - Serendael of Nytherial
        new() { HeroID = 8, LocalizationKey = "heroBonusTitle16", Stat = UnitStat.MeleeDefense, Condition = Units(UnitName.ForestSpirits, UnitName.Treants), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 5 },
        new() { HeroID = 8, LocalizationKey = "heroBonusTitle16", Stat = UnitStat.WeaponStrength, Condition = Units(UnitName.ForestSpirits, UnitName.Treants), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 9 - Sister Morvayne has no stat bonus rules (attribute-only, see BaseAttributeRules)

        // Hero 10 - Lord Draven Bloodreaver. NOTE: the MeleeAttack magnitude was already +8 in the
        // original hardcoded switch even though its own descriptive comment said "+4" - preserved
        // as-is per the migration plan rather than silently changed.
        new() { HeroID = 10, LocalizationKey = "heroBonusTitle19", Stat = UnitStat.Leadership, Condition = Units(UnitName.Bloodsworn, UnitName.BloodswornKnights), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 15 },
        new() { HeroID = 10, LocalizationKey = "heroBonusTitle19", Stat = UnitStat.MeleeAttack, Condition = Units(UnitName.Bloodsworn, UnitName.BloodswornKnights), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 8 },

        // Hero 11 - Oda Nobukage
        new() { HeroID = 11, LocalizationKey = "heroBonusTitle21", Stat = UnitStat.MeleeAttack, Condition = Unconditional(), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 12 - Tokugawa Harunobu
        new() { HeroID = 12, LocalizationKey = "heroBonusTitle24", Stat = UnitStat.Accuracy, Condition = Units(UnitName.EmperorsArquebusiers), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 12, LocalizationKey = "heroBonusTitle24", Stat = UnitStat.MissileStrength, Condition = Units(UnitName.EmperorsArquebusiers), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 13 - Hrothgar Goblinslayer
        new() { HeroID = 13, LocalizationKey = "heroBonusTitle25", Stat = UnitStat.MeleeAttack, Condition = EnemyRaceIs(Race.Gruntkin), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 13, LocalizationKey = "heroBonusTitle25", Stat = UnitStat.WeaponStrength, Condition = EnemyRaceIs(Race.Gruntkin), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 14 - Bertha Barrelstorm
        new() { HeroID = 14, LocalizationKey = "heroBonusTitle27", Stat = UnitStat.Accuracy, Condition = Types(UnitType.Artillery), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 14, LocalizationKey = "heroBonusTitle27", Stat = UnitStat.Range, Condition = Types(UnitType.Artillery), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        // Hybrids are deliberately excluded, consistent with every other ammunition bonus they are
        // denied (prestige ammo, Deep Quivers): their pools are token throwing weapons, not a quiver.
        new() { HeroID = 14, LocalizationKey = "heroBonusTitle28", Stat = UnitStat.Ammunition, Condition = Types(UnitType.Ranged, UnitType.Artillery), MagnitudeKind = BonusMagnitudeKind.PercentOfCurrentValue, Value = 0.5f },

        // Hero 15 - Skrix the Swarmcaller
        new() { HeroID = 15, LocalizationKey = "heroBonusTitle29", Stat = UnitStat.Leadership, Condition = Units(UnitName.KoboldBrawlers, UnitName.ScalebowKobolds), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 15, LocalizationKey = "heroBonusTitle29", Stat = UnitStat.MeleeAttack, Condition = Units(UnitName.KoboldBrawlers, UnitName.ScalebowKobolds), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },

        // Hero 16 - Valthrex Primeclaw
        new() { HeroID = 16, LocalizationKey = "heroBonusTitle31", Stat = UnitStat.Leadership, Condition = Sizes(UnitSize.Cavalry, UnitSize.Monstrous, UnitSize.SingleUnit, UnitSize.Artillery), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 10 },
        new() { HeroID = 16, LocalizationKey = "heroBonusTitle31", Stat = UnitStat.MeleeDefense, Condition = Sizes(UnitSize.Cavalry, UnitSize.Monstrous, UnitSize.SingleUnit, UnitSize.Artillery), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { HeroID = 16, LocalizationKey = "heroBonusTitle32", Stat = UnitStat.Armor, Condition = Units(UnitName.StegoplateGuard), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 15 },
        new() { HeroID = 16, LocalizationKey = "heroBonusTitle32", Stat = UnitStat.WeaponStrength, Condition = Units(UnitName.StegoplateGuard), MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
    };

    public static readonly List<HeroAttributeBonusRule> BaseAttributeRules = new()
    {
        new() { HeroID = 4, LocalizationKey = "heroBonusTitle7", Condition = Units(UnitName.OrcRavagers), GrantedAttribute = UnitAttribute.Terrifying },
        new() { HeroID = 6, LocalizationKey = "heroBonusTitle12", Condition = Unconditional(), GrantedAttribute = UnitAttribute.Stalwart },
        new() { HeroID = 9, LocalizationKey = "heroBonusTitle18", Condition = Unconditional(), GrantedAttribute = UnitAttribute.Outrider },
        new() { HeroID = 13, LocalizationKey = "heroBonusTitle26", Condition = Units(UnitName.Cragflayers), GrantedAttribute = UnitAttribute.Rage },
    };

    // "Bushido Discipline" - only Sakura Dynasty is wired up today (see DataTypes.cs's flavor-text
    // comments for the other 8 unimplemented race passives). LocalizationKey follows Unity's
    // "{Race}BonusDescription" convention, where the text is "Bonus Name: effect description" -
    // GetFactionBonus splits on the first ':' to get the display name, same as the original code.
    public static readonly List<FactionBonusRule> BaseFactionRules = new()
    {
        new() { Race = Race.SakuraDynasty, LocalizationKey = "SakuraDynastyBonusDescription", Stat = UnitStat.MeleeAttack, MagnitudeKind = BonusMagnitudeKind.Flat, Value = 4 },
        new() { Race = Race.SakuraDynasty, LocalizationKey = "SakuraDynastyBonusDescription", Stat = UnitStat.Leadership, MagnitudeKind = BonusMagnitudeKind.Flat, Value = 20 },
    };
}
