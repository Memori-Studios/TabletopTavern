using Memori.SaveData;
using UnityEngine;
using System.Collections.Generic;

namespace TJ
{
    public static class ArmyCreator
    {
        // DifficultyMod 10 / DifficultyMod 14 tier-count and prestige-chance tables now live in
        // ArmyGenerationRuleData (mod-overridable) instead of hardcoded here - see
        // ArmyGenerationRuleData.DefaultEnemyPrestigeRules / DefaultEnemyArmyRules / DefaultTownGarrisonRules.
        private const int ENEMY_PRESTIGE_SEED_OFFSET = 104729; // decorrelate from the deck-shuffle RNG in CreateArmyFromUnitsByTier
        // Elite Guard draws its squad on its own stream, so the rest of the army is the same as without the card.
        private const int ELITE_GUARD_SEED_OFFSET = 7727;
        // The spell stopgap squad draws on its own stream too, so the rest of the army is unchanged.
        private const int SPELLS_EXTRA_SQUAD_SEED_OFFSET = 3571;
        // Mage-cap swaps draw on their own stream, so an army within the cap is the same as before.
        private const int MAGE_CAP_SEED_OFFSET = 9931;

        // DifficultyMod 10 / DifficultyMod 14: gives enemy squads a chance to spawn already prestiged, scaling with Act and difficulty
        private static SquadToLoad[] ApplyEnemyPrestige(SquadToLoad[] _squads, int _actNumber, int _seed, bool _enhanced, bool _doubleChance = false)
        {
            return ApplyEnemyPrestige(_squads, ArmyGenerationRuleData.ResolveEnemyPrestigeProfile(_actNumber, _enhanced), _seed, _doubleChance);
        }
        private static SquadToLoad[] ApplyEnemyPrestige(SquadToLoad[] _squads, EnemyPrestigeRule profile, int _seed, bool _doubleChance)
        {
            if (_squads.Length == 0) return _squads;

            // Veteran Hosts on a level that already runs the enhanced table.
            if (_doubleChance)
                profile.ChancePerSquad = Mathf.Min(TabletopTavernConstants.ENDLESS_PRESTIGE_CHANCE_CAP, profile.ChancePerSquad * 2f);

            System.Random random = new(_seed + ENEMY_PRESTIGE_SEED_OFFSET);

            // Shuffle selection order so the cap doesn't systematically favor whichever tier CreateArmyFromUnitsByTier appended first
            List<int> order = new();
            for (int i = 0; i < _squads.Length; i++) order.Add(i);
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(0, i + 1);
                (order[j], order[i]) = (order[i], order[j]);
            }

            int prestiged = 0;
            foreach (int index in order)
            {
                if (prestiged >= profile.MaxPrestigedSquads) break;
                if (_squads[index].isEmptySquad) continue;
                if (random.NextDouble() >= profile.ChancePerSquad) continue;

                int level = random.NextDouble() < profile.PrestigeTwoChance ? 2 : 1;
                _squads[index].UnitPrestige = level;

                if (level == 2)
                {
                    List<UnitAttribute> eligible = TabletopTavernData.Instance.GetUsablePrestigeTraits(_squads[index].UnitName);
                    if (eligible.Count > 0)
                        _squads[index].PrestigeTrait = eligible[random.Next(eligible.Count)];
                }
                prestiged++;
            }
            return _squads;
        }

        public static SquadToLoad[] GenerateTownGarrison(TownSize _townSize, int _seed, List<UnitTier> unitsPool, bool difficultyImperator, int _bookNumber, bool enemyPrestigeEligible, bool enemyPrestigeEnhanced, bool eliteGuard = false, bool doublePrestigeChance = false, bool smallerGarrison = false)
        {
            // Garrisons don't field cavalry or outriders — filter them out before picking units
            unitsPool = unitsPool.FindAll(u =>
                TabletopTavernData.Instance.GetUnitSizeFromUnitName(u.unitName) != UnitSize.Cavalry
            );

            TierCount[] tierCounts = ArmyGenerationRuleData.ResolveTownGarrisonTierCounts(_townSize, _bookNumber, difficultyImperator);

            SquadToLoad[] garrison = CreateArmyFromUnitsByTier(tierCounts, unitsPool, _seed);
            // Drops the last squad, the highest tier, so the rest match the full garrison draw for draw.
            if (smallerGarrison && garrison.Length > 1) System.Array.Resize(ref garrison, garrison.Length - 1);
            if (eliteGuard) garrison = AddEliteSquad(garrison, unitsPool, _seed);
            garrison = CapMages(garrison, unitsPool, _seed);
            if (enemyPrestigeEligible) garrison = ApplyEnemyPrestige(garrison, _bookNumber, _seed, enemyPrestigeEnhanced, doublePrestigeChance);
            return garrison;
        }
        /// <summary>Elite Guard: one more tier 4 squad (tier 3 when the pool has none), never past the deployment cap.</summary>
        public static SquadToLoad[] AddEliteSquad(SquadToLoad[] _army, List<UnitTier> _unitsPool, int _seed)
        {
            if (_army.Length >= TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP) return _army;
            List<UnitName> deck = new();
            foreach (int tier in new[] { 4, 3 })
            {
                foreach (UnitTier u in _unitsPool)
                    if (u.tier == tier) deck.Add(u.unitName);
                if (deck.Count > 0) break;
            }
            if (deck.Count == 0) return _army;

            System.Random random = new(_seed + ELITE_GUARD_SEED_OFFSET);
            List<SquadToLoad> squads = new(_army) { new SquadToLoad(deck[random.Next(deck.Count)]) };
            return squads.ToArray();
        }
        /// <summary>Spell stopgap: one more Common in act 1, Uncommon in act 2, Rare from act 3, never past the deployment cap.</summary>
        private static SquadToLoad[] AddSpellsExtraSquad(SquadToLoad[] _army, int _boardNumber, List<UnitTier> _unitsPool, int _seed)
        {
            if (_army.Length >= TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP) return _army;
            int tier = Mathf.Clamp(_boardNumber, 1, 3);
            List<UnitName> deck = new();
            foreach (UnitTier u in _unitsPool)
                if (u.tier == tier) deck.Add(u.unitName);
            if (deck.Count == 0) return _army;

            System.Random random = new(_seed + SPELLS_EXTRA_SQUAD_SEED_OFFSET);
            List<SquadToLoad> squads = new(_army);
            // After the last squad of its tier, so the Gruntkin act 1 trim of the last squad drops the same squad as before.
            int insertAt = 0;
            for (int i = 0; i < squads.Count; i++)
                if (TabletopTavernData.Instance.GetUnitTierFromUnitName(squads[i].UnitName) <= tier) insertAt = i + 1;
            squads.Insert(insertAt, new SquadToLoad(deck[random.Next(deck.Count)]));
            return squads.ToArray();
        }
        private static SquadToLoad[] CreateArmyFromUnitsByTier(TierCount[] _tierCounts, List<UnitTier> _unitsPool, int _seed)
        {
            System.Random random = new(_seed);
            List<SquadToLoad> squads = new();

            Dictionary<int, List<UnitName>> decksByTier = new();
            foreach (TierCount entry in _tierCounts)
            {
                if (decksByTier.ContainsKey(entry.Tier)) continue;
                List<UnitName> deck = new();
                foreach (UnitTier u in _unitsPool)
                    if (u.tier == entry.Tier) deck.Add(u.unitName);
                if (deck.Count == 0)
                {
                    Debug.LogError($"ArmyCreator: No unit found for tier {entry.Tier} in the provided pool.");
                    continue;
                }
                for (int i = deck.Count - 1; i > 0; i--)
                {
                    int j = random.Next(0, i + 1);
                    (deck[j], deck[i]) = (deck[i], deck[j]);
                }
                decksByTier[entry.Tier] = deck;
            }

            foreach (TierCount entry in _tierCounts)
            {
                if (!decksByTier.TryGetValue(entry.Tier, out List<UnitName> deck)) continue;
                for (int i = 0; i < entry.Count; i++)
                {
                    UnitName chosen = random.NextDouble() < 0.35
                        ? deck[random.Next(deck.Count)]
                        : deck[i % deck.Count];
                    squads.Add(new SquadToLoad(chosen));
                }
            }
            return squads.ToArray();
        }
        public static SquadToLoad[] GenerateEnemyArmy(int _boardNumber, int _battlesFought, int _seed, bool _finalBattle, List<UnitTier> unitsPool, bool knightDifficulty, bool enemyPrestigeEligible, bool enemyPrestigeEnhanced, bool eliteGuard = false, bool doublePrestigeChance = false, bool spellsExtraSquad = false)
        {
            TierCount[] tierCounts = ArmyGenerationRuleData.ResolveEnemyArmyTierCounts(_boardNumber, _finalBattle, knightDifficulty, _battlesFought);

            SquadToLoad[] army = CreateArmyFromUnitsByTier(tierCounts, unitsPool, _seed);
            if (spellsExtraSquad) army = AddSpellsExtraSquad(army, _boardNumber, unitsPool, _seed);
            if (eliteGuard) army = AddEliteSquad(army, unitsPool, _seed);
            army = CapMages(army, unitsPool, _seed);
            if (enemyPrestigeEligible) army = ApplyEnemyPrestige(army, _boardNumber, _seed, enemyPrestigeEnhanced, doublePrestigeChance);
            return army;
        }

        /// <summary>
        /// A rogue host on the March. Size and quality come from the battle number (MarchRules), not the act, and the
        /// same extras as any enemy army go on top. scheduleBattle is the battle number after difficulty and Ordeals.
        /// </summary>
        public static SquadToLoad[] GenerateMarchArmy(int scheduleBattle, int _seed, bool warlord, List<UnitTier> unitsPool, bool enemyPrestigeEligible, bool enemyPrestigeEnhanced, bool eliteGuard = false, bool doublePrestigeChance = false, bool spellsExtraSquad = false, int extraSquads = 0)
        {
            TierCount[] tierCounts = FoldMissingTiers(MarchRules.ArmyTierCounts(scheduleBattle, warlord, extraSquads), unitsPool);

            SquadToLoad[] army = CreateArmyFromUnitsByTier(tierCounts, unitsPool, _seed);
            if (spellsExtraSquad) army = AddSpellsExtraSquad(army, TabletopTavernConstants.FINAL_STORY_ACT, unitsPool, _seed);
            if (eliteGuard) army = AddEliteSquad(army, unitsPool, _seed);
            army = CapMages(army, unitsPool, _seed);
            if (enemyPrestigeEligible) army = ApplyEnemyPrestige(army, MarchRules.PrestigeProfile(scheduleBattle, enemyPrestigeEnhanced), _seed, doublePrestigeChance);
            return army;
        }
        // A faction with no unit of a tier fields the next tier down, so a host never comes up short.
        private static TierCount[] FoldMissingTiers(TierCount[] _tierCounts, List<UnitTier> _unitsPool)
        {
            Dictionary<int, int> counts = new();
            foreach (TierCount entry in _tierCounts)
            {
                int tier = entry.Tier;
                while (tier > 1 && !_unitsPool.Exists(u => u.tier == tier)) tier--;
                counts[tier] = counts.TryGetValue(tier, out int held) ? held + entry.Count : entry.Count;
            }
            List<TierCount> folded = new();
            foreach (KeyValuePair<int, int> entry in counts) folded.Add(new TierCount { Tier = entry.Key, Count = entry.Value });
            folded.Sort((a, b) => a.Tier.CompareTo(b.Tier));
            return folded.ToArray();
        }

        public static SquadToLoad[] ReplaceMonsterUnits(SquadToLoad[] _squadsToLoad, int _seed, List<UnitTier> unitsPool)
        {
            List<SquadToLoad> newSquads = new();
            System.Random random = new(_seed);
            foreach (SquadToLoad squad in _squadsToLoad)
            {
                if (TabletopTavernData.Instance.GetUnitSizeFromUnitName(squad.UnitName) != UnitSize.Infantry)
                {
                    // Race race = TabletopTavernData.Instance.GetRaceFromUnitName(squad.UnitName);
                    //filter out all units that are not the same tier
                    //filter out all large units
                    int unitTier = TabletopTavernData.Instance.GetUnitTierFromUnitName(squad.UnitName);
                    List<UnitTier> filteredUnitsPool = new();
                    int searchTier = unitTier;
                    while (filteredUnitsPool.Count == 0 && searchTier >= 1)
                    {
                        filteredUnitsPool = unitsPool.FindAll(unit => unit.tier == searchTier);
                        filteredUnitsPool = filteredUnitsPool.FindAll(unit => TabletopTavernData.Instance.GetUnitSizeFromUnitName(unit.unitName) != UnitSize.Monstrous && TabletopTavernData.Instance.GetUnitSizeFromUnitName(unit.unitName) != UnitSize.SingleUnit);
                        searchTier--;
                    }

                    if (filteredUnitsPool.Count == 0)
                    {
                        Debug.LogError($"ArmyCreator: No replacement infantry unit found for tier {unitTier} or below. Keeping original squad.");
                        newSquads.Add(squad);
                        continue;
                    }

                    int randomIndex = random.Next(0, filteredUnitsPool.Count);
                    newSquads.Add(new SquadToLoad(filteredUnitsPool[randomIndex].unitName));
                }
                else
                {
                    newSquads.Add(squad);
                }
            }
            return CapMages(newSquads.ToArray(), unitsPool, _seed);
        }

        /// <summary>Swaps every mage past ENEMY_MAGE_SQUAD_CAP for a non-mage of the same tier, or the nearest tier below.</summary>
        private static SquadToLoad[] CapMages(SquadToLoad[] _army, List<UnitTier> _unitsPool, int _seed)
        {
            TabletopTavernData data = TabletopTavernData.Instance;
            System.Random random = null;
            int mages = 0;
            for (int i = 0; i < _army.Length; i++)
            {
                if (_army[i].isEmptySquad || !TabletopTavernConstants.Casts(data.GetUnitTypeFromUnitName(_army[i].UnitName))) continue;
                if (++mages <= TabletopTavernConstants.ENEMY_MAGE_SQUAD_CAP) continue;

                List<UnitTier> candidates = new();
                for (int tier = data.GetUnitTierFromUnitName(_army[i].UnitName); tier >= 1 && candidates.Count == 0; tier--)
                    candidates = _unitsPool.FindAll(u => u.tier == tier && !TabletopTavernConstants.Casts(data.GetUnitTypeFromUnitName(u.unitName)));
                if (candidates.Count == 0) continue;

                random ??= new System.Random(_seed + MAGE_CAP_SEED_OFFSET);
                _army[i] = new SquadToLoad(candidates[random.Next(candidates.Count)].unitName);
            }
            return _army;
        }
    }
}