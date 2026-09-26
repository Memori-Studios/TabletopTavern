using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJ
{
    [Serializable]
    public struct TierCount
    {
        public int Tier;
        public int Count;
    }

    // Nullable fields mean "matches regardless of this" - e.g. a rule with KnightDifficulty == null
    // applies whether or not the player is on Knight difficulty. Only plain C# (not JsonUtility)
    // ever constructs these directly; mod-supplied JSON goes through ArmyGenerationRuleOverrideLoader,
    // which parses the string-typed wire format into this.
    public struct EnemyArmyRule
    {
        public int Board;
        public bool FinalBattle;
        public bool? KnightDifficulty;
        public int? BattlesFoughtMin;
        public int? BattlesFoughtMax;
        public TierCount[] TierCounts;

        public readonly bool Matches(int board, bool finalBattle, bool knightDifficulty, int battlesFought)
        {
            if (Board != board || FinalBattle != finalBattle) return false;
            if (KnightDifficulty.HasValue && KnightDifficulty.Value != knightDifficulty) return false;
            if (BattlesFoughtMin.HasValue && battlesFought < BattlesFoughtMin.Value) return false;
            if (BattlesFoughtMax.HasValue && battlesFought >= BattlesFoughtMax.Value) return false;
            return true;
        }
    }

    public struct TownGarrisonRule
    {
        public TownSize TownSize;
        public int BookNumber;
        public bool? DifficultyImperator;
        public TierCount[] TierCounts;

        public readonly bool Matches(TownSize townSize, int bookNumber, bool difficultyImperator)
        {
            if (TownSize != townSize || BookNumber != bookNumber) return false;
            if (DifficultyImperator.HasValue && DifficultyImperator.Value != difficultyImperator) return false;
            return true;
        }
    }

    public struct EnemyPrestigeRule
    {
        public int ActNumber;
        public bool Enhanced;
        public float ChancePerSquad;
        public int MaxPrestigedSquads;
        public float PrestigeTwoChance;

        public readonly bool Matches(int actNumber, bool enhanced) => ActNumber == actNumber && Enhanced == enhanced;
    }

    // Default tables below are a 1:1 transcription of the hardcoded switches ArmyCreator used
    // before this became data-driven (see git history for the original form), plus the two
    // ENEMY_PRESTIGE_* arrays. Mod rules (from army_generation_rules.json, loaded by
    // ArmyGenerationRuleOverrideLoader into the ModXxxRules lists below) are matched first, in
    // file order - first full match wins - falling through to these defaults when nothing in a
    // mod's list matches. CustomBattleGeneratorEditor reads the same default tables so its preview
    // can never drift from real gameplay again.
    public static class ArmyGenerationRuleData
    {
        private static readonly List<EnemyArmyRule> ModEnemyArmyRules = new();
        private static readonly List<TownGarrisonRule> ModTownGarrisonRules = new();
        private static readonly List<EnemyPrestigeRule> ModEnemyPrestigeRules = new();

        public static void ClearModRules()
        {
            ModEnemyArmyRules.Clear();
            ModTownGarrisonRules.Clear();
            ModEnemyPrestigeRules.Clear();
        }

        // Inserted at the front so the most-recently-processed mod's rules are checked first,
        // matching ModLoadOrder's "lower in the list wins" convention - TabletopTavernData
        // processes mod folders in that same order.
        public static void PrependModRules(List<EnemyArmyRule> rules) => ModEnemyArmyRules.InsertRange(0, rules);
        public static void PrependModRules(List<TownGarrisonRule> rules) => ModTownGarrisonRules.InsertRange(0, rules);
        public static void PrependModRules(List<EnemyPrestigeRule> rules) => ModEnemyPrestigeRules.InsertRange(0, rules);

        public static TierCount[] ResolveEnemyArmyTierCounts(int board, bool finalBattle, bool knightDifficulty, int battlesFought)
        {
            foreach (EnemyArmyRule rule in ModEnemyArmyRules)
                if (rule.Matches(board, finalBattle, knightDifficulty, battlesFought)) return rule.TierCounts;
            foreach (EnemyArmyRule rule in DefaultEnemyArmyRules)
                if (rule.Matches(board, finalBattle, knightDifficulty, battlesFought)) return rule.TierCounts;

            if (board > TabletopTavernConstants.FINAL_STORY_ACT)
            {
                TierCount[] lastStoryAct = ResolveEnemyArmyTierCounts(TabletopTavernConstants.FINAL_STORY_ACT, finalBattle, knightDifficulty, battlesFought);
                return ExtendForEndless(lastStoryAct, TabletopTavernConstants.EndlessActs(board), alternateTierFour: finalBattle);
            }

            Debug.LogWarning($"[ArmyGeneration] No enemy army rule matched board={board}, finalBattle={finalBattle}, knightDifficulty={knightDifficulty}, battlesFought={battlesFought} - returning an empty army.");
            return Array.Empty<TierCount>();
        }

        public static TierCount[] ResolveTownGarrisonTierCounts(TownSize townSize, int bookNumber, bool difficultyImperator)
        {
            foreach (TownGarrisonRule rule in ModTownGarrisonRules)
                if (rule.Matches(townSize, bookNumber, difficultyImperator)) return rule.TierCounts;
            foreach (TownGarrisonRule rule in DefaultTownGarrisonRules)
                if (rule.Matches(townSize, bookNumber, difficultyImperator)) return rule.TierCounts;

            if (bookNumber > TabletopTavernConstants.FINAL_STORY_ACT)
            {
                TierCount[] lastStoryAct = ResolveTownGarrisonTierCounts(townSize, TabletopTavernConstants.FINAL_STORY_ACT, difficultyImperator);
                return ExtendForEndless(lastStoryAct, TabletopTavernConstants.EndlessActs(bookNumber), alternateTierFour: false);
            }

            Debug.LogWarning($"[ArmyGeneration] No town garrison rule matched townSize={townSize}, bookNumber={bookNumber}, difficultyImperator={difficultyImperator} - returning an empty garrison.");
            return Array.Empty<TierCount>();
        }

        public static EnemyPrestigeRule ResolveEnemyPrestigeProfile(int actNumber, bool enhanced)
        {
            foreach (EnemyPrestigeRule rule in ModEnemyPrestigeRules)
                if (rule.Matches(actNumber, enhanced)) return rule;
            foreach (EnemyPrestigeRule rule in DefaultEnemyPrestigeRules)
                if (rule.Matches(actNumber, enhanced)) return rule;

            // No exact rule (mod or default) for this act - clamp into the base game's 3-act
            // range, same fallback ArmyCreator used before this became data-driven, so a mod that
            // adds a new act via enemyArmyRules without also adding enemyPrestigeRules still gets
            // sensible (if static) prestige odds instead of a silent zero.
            int clampedAct = Math.Clamp(actNumber, 1, TabletopTavernConstants.FINAL_STORY_ACT);
            foreach (EnemyPrestigeRule rule in DefaultEnemyPrestigeRules)
                if (rule.Matches(clampedAct, enhanced)) return ExtendPrestigeForEndless(rule, TabletopTavernConstants.EndlessActs(actNumber));

            return default;
        }

        // Endless acts build on the last story act's row: one more squad per extra act, tier 3 for
        // regular fights and alternating tier 3 / tier 4 for finals, never past the deployment cap.
        private static TierCount[] ExtendForEndless(TierCount[] baseCounts, int endlessActs, bool alternateTierFour)
        {
            List<TierCount> counts = new(baseCounts);
            int total = 0;
            foreach (TierCount entry in counts) total += entry.Count;

            for (int i = 0; i < endlessActs && total < TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP; i++, total++)
            {
                int tier = alternateTierFour && i % 2 == 1 ? 4 : 3;
                int index = counts.FindIndex(c => c.Tier == tier);
                if (index >= 0) counts[index] = new TierCount { Tier = tier, Count = counts[index].Count + 1 };
                else counts.Add(new TierCount { Tier = tier, Count = 1 });
            }
            return counts.ToArray();
        }

        private static EnemyPrestigeRule ExtendPrestigeForEndless(EnemyPrestigeRule rule, int endlessActs)
        {
            if (endlessActs <= 0) return rule;
            rule.ChancePerSquad = Math.Min(TabletopTavernConstants.ENDLESS_PRESTIGE_CHANCE_CAP, rule.ChancePerSquad + endlessActs * TabletopTavernConstants.ENDLESS_PRESTIGE_CHANCE_PER_ACT);
            rule.MaxPrestigedSquads = Math.Min(TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP, rule.MaxPrestigedSquads + endlessActs);
            rule.PrestigeTwoChance = Math.Min(TabletopTavernConstants.ENDLESS_PRESTIGE_TWO_CHANCE_CAP, rule.PrestigeTwoChance + endlessActs * TabletopTavernConstants.ENDLESS_PRESTIGE_CHANCE_PER_ACT);
            return rule;
        }

        private static TierCount[] T(params (int tier, int count)[] pairs)
        {
            var result = new TierCount[pairs.Length];
            for (int i = 0; i < pairs.Length; i++) result[i] = new TierCount { Tier = pairs[i].tier, Count = pairs[i].count };
            return result;
        }

        public static readonly List<EnemyArmyRule> DefaultEnemyArmyRules = new()
        {
            // Board 1
            new() { Board = 1, FinalBattle = true, KnightDifficulty = true, TierCounts = T((1, 3), (2, 3), (3, 1), (4, 1)) },
            new() { Board = 1, FinalBattle = true, KnightDifficulty = false, TierCounts = T((1, 3), (2, 2), (3, 1), (4, 1)) },
            new() { Board = 1, FinalBattle = false, BattlesFoughtMax = 3, TierCounts = T((1, 4)) },
            new() { Board = 1, FinalBattle = false, BattlesFoughtMin = 3, BattlesFoughtMax = 5, TierCounts = T((1, 3), (2, 1)) },
            new() { Board = 1, FinalBattle = false, BattlesFoughtMin = 5, BattlesFoughtMax = 7, TierCounts = T((1, 3), (2, 1), (3, 1)) },
            new() { Board = 1, FinalBattle = false, BattlesFoughtMin = 7, TierCounts = T((1, 3), (2, 2), (3, 2)) },

            // Board 2
            new() { Board = 2, FinalBattle = true, KnightDifficulty = true, TierCounts = T((1, 1), (2, 3), (3, 4), (4, 1)) },
            new() { Board = 2, FinalBattle = true, KnightDifficulty = false, TierCounts = T((1, 1), (2, 4), (3, 3), (4, 1)) },
            new() { Board = 2, FinalBattle = false, BattlesFoughtMax = 3, TierCounts = T((1, 1), (2, 2), (3, 1)) },
            new() { Board = 2, FinalBattle = false, BattlesFoughtMin = 3, BattlesFoughtMax = 5, TierCounts = T((1, 1), (2, 3), (3, 2)) },
            new() { Board = 2, FinalBattle = false, BattlesFoughtMin = 5, BattlesFoughtMax = 7, TierCounts = T((1, 1), (2, 3), (3, 3)) },
            new() { Board = 2, FinalBattle = false, BattlesFoughtMin = 7, TierCounts = T((1, 1), (2, 2), (3, 5)) },

            // Board 3
            new() { Board = 3, FinalBattle = true, KnightDifficulty = true, TierCounts = T((1, 2), (2, 4), (3, 4), (4, 1)) },
            new() { Board = 3, FinalBattle = true, KnightDifficulty = false, TierCounts = T((1, 3), (2, 3), (3, 4), (4, 1)) },
            new() { Board = 3, FinalBattle = false, BattlesFoughtMax = 3, TierCounts = T((1, 3), (2, 3), (3, 2)) },
            new() { Board = 3, FinalBattle = false, BattlesFoughtMin = 3, BattlesFoughtMax = 5, TierCounts = T((1, 2), (2, 2), (3, 4)) },
            new() { Board = 3, FinalBattle = false, BattlesFoughtMin = 5, BattlesFoughtMax = 7, TierCounts = T((1, 2), (2, 2), (3, 5)) },
            new() { Board = 3, FinalBattle = false, BattlesFoughtMin = 7, TierCounts = T((2, 2), (3, 7)) },
        };

        public static readonly List<TownGarrisonRule> DefaultTownGarrisonRules = new()
        {
            new() { BookNumber = 1, TownSize = TownSize.Village, DifficultyImperator = false, TierCounts = T((1, 3)) },
            new() { BookNumber = 1, TownSize = TownSize.Castle, DifficultyImperator = false, TierCounts = T((1, 4), (2, 2)) },
            new() { BookNumber = 1, TownSize = TownSize.City, DifficultyImperator = false, TierCounts = T((1, 2), (2, 2), (3, 2)) },
            new() { BookNumber = 1, TownSize = TownSize.Village, DifficultyImperator = true, TierCounts = T((1, 4)) },
            new() { BookNumber = 1, TownSize = TownSize.Castle, DifficultyImperator = true, TierCounts = T((1, 2), (2, 4)) },
            new() { BookNumber = 1, TownSize = TownSize.City, DifficultyImperator = true, TierCounts = T((1, 3), (2, 3), (3, 2)) },

            new() { BookNumber = 2, TownSize = TownSize.Village, DifficultyImperator = false, TierCounts = T((1, 5)) },
            new() { BookNumber = 2, TownSize = TownSize.Castle, DifficultyImperator = false, TierCounts = T((2, 6)) },
            new() { BookNumber = 2, TownSize = TownSize.City, DifficultyImperator = false, TierCounts = T((2, 4), (3, 2)) },
            new() { BookNumber = 2, TownSize = TownSize.Village, DifficultyImperator = true, TierCounts = T((1, 6)) },
            new() { BookNumber = 2, TownSize = TownSize.Castle, DifficultyImperator = true, TierCounts = T((2, 7)) },
            new() { BookNumber = 2, TownSize = TownSize.City, DifficultyImperator = true, TierCounts = T((2, 4), (3, 4)) },

            new() { BookNumber = 3, TownSize = TownSize.Village, DifficultyImperator = false, TierCounts = T((1, 8)) },
            new() { BookNumber = 3, TownSize = TownSize.Castle, DifficultyImperator = false, TierCounts = T((1, 2), (2, 5)) },
            new() { BookNumber = 3, TownSize = TownSize.City, DifficultyImperator = false, TierCounts = T((2, 3), (3, 6)) },
            new() { BookNumber = 3, TownSize = TownSize.Village, DifficultyImperator = true, TierCounts = T((1, 9)) },
            new() { BookNumber = 3, TownSize = TownSize.Castle, DifficultyImperator = true, TierCounts = T((1, 2), (2, 7)) },
            new() { BookNumber = 3, TownSize = TownSize.City, DifficultyImperator = true, TierCounts = T((2, 2), (3, 7)) },
        };

        public static readonly List<EnemyPrestigeRule> DefaultEnemyPrestigeRules = new()
        {
            // Duke+ (DifficultyMod 10) - base rates
            new() { ActNumber = 1, Enhanced = false, ChancePerSquad = 0.10f, MaxPrestigedSquads = 1, PrestigeTwoChance = 0.00f },
            new() { ActNumber = 2, Enhanced = false, ChancePerSquad = 0.20f, MaxPrestigedSquads = 2, PrestigeTwoChance = 0.15f },
            new() { ActNumber = 3, Enhanced = false, ChancePerSquad = 0.25f, MaxPrestigedSquads = 3, PrestigeTwoChance = 0.25f },

            // Emperor+ (DifficultyMod 14) - enhanced rates, replaces the base table when active
            new() { ActNumber = 1, Enhanced = true, ChancePerSquad = 0.20f, MaxPrestigedSquads = 2, PrestigeTwoChance = 0.00f },
            new() { ActNumber = 2, Enhanced = true, ChancePerSquad = 0.35f, MaxPrestigedSquads = 3, PrestigeTwoChance = 0.20f },
            new() { ActNumber = 3, Enhanced = true, ChancePerSquad = 0.45f, MaxPrestigedSquads = 4, PrestigeTwoChance = 0.30f },
        };
    }
}
