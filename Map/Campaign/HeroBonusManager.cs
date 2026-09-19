using UnityEngine;
using Memori.SaveData;
using Memori.Utilities;
using Memori.Scenes;
using Memori.Localization;
using System.Collections.Generic;

namespace TJ
{
    public class HeroBonusManager : Singleton<HeroBonusManager>
    {
        [SerializeField] private int activeHeroID = -1;
        [SerializeField] private bool isCustomBattle = false;
        [SerializeField] private static Race enemyRace;
        private static Race _activePlayerRace = Race.Special;
        public int ActiveHeroID => activeHeroID;
        SceneHandler sceneHandler;

        private static List<HeroStatBonusRule> _statRules;
        private static List<HeroAttributeBonusRule> _attributeRules;
        private static List<FactionBonusRule> _factionRules;

        private static void EnsureRulesLoaded()
        {
            if (_statRules != null) return;
            LoadRulesFromResourcesAndOverrides(ModLoadOrder.GetEnabledModFolderPathsInOrder());
        }

        public static void LoadRulesFromResourcesAndOverrides(List<string> modFolders)
        {
            _statRules = new List<HeroStatBonusRule>(HeroBonusRuleData.BaseStatRules);
            _attributeRules = new List<HeroAttributeBonusRule>(HeroBonusRuleData.BaseAttributeRules);
            _factionRules = new List<FactionBonusRule>(HeroBonusRuleData.BaseFactionRules);

            foreach (string modFolder in modFolders)
            {
                HeroBonusRuleOverrideLoader.ApplyOverridesFromModFolder(modFolder, _statRules, _attributeRules, _factionRules);
            }

            // Share the same list instances with the Components-assembly evaluator so
            // Systems-assembly ECS code (which can't reference this main-assembly class) reads
            // identical, already-overridden data - see HeroBonusRuleEvaluator.
            HeroBonusRuleEvaluator.SetRules(_statRules, _factionRules, _attributeRules);
        }

        private void Start()
        {
            sceneHandler = SceneHandler.Instance;
            sceneHandler.OnGameStateChanged += OnGameStateChanged;
        }
        public List<UnitStatBonus> GetHeroStatBonus(UnitStat _unitStat, UnitName _requestingUnit, float currentStatValue)
        {
            return GetHeroStatBonus(_unitStat, _requestingUnit, activeHeroID, currentStatValue);
        }
        public List<UnitAttributeBonus> GetHeroAttributeBonus(UnitName _requestingUnit)
        {
            return GetHeroAttributeBonus(_requestingUnit, activeHeroID);
        }
        public void OnGameStateChanged(GameStateEnum gameStateEnum)
        {
            isCustomBattle = SaveDataHandler.IsCustomBattle();
            activeHeroID = SaveDataHandler.GetActiveHeroID();
            enemyRace = SaveDataHandler.GetEnemyRace();
            // #if !UNITY_EDITOR
            //     if(activeHeroID != 1 || activeHeroID != 2 ||activeHeroID != 5 || activeHeroID != 6 ) {
            //         activeHeroID = 1;
            //     }
            // #endif
            
            if(isCustomBattle || gameStateEnum == GameStateEnum.MainMenu) {
                activeHeroID = -1;
            }
            _activePlayerRace = activeHeroID == -1 ? Race.Special : HeroData.GetRaceFromHero(activeHeroID);
            // Debug.Log($"Active Hero ID: {activeHeroID}, custom battle: {isCustomBattle}");
        }
        public static List<UnitStatBonus> GetFactionBonus(UnitStat _unitStat) => GetFactionBonusForRace(_unitStat, _activePlayerRace);

        // Stateless variant for callers (ECS systems applying real mechanics, not just display) that
        // must not depend on _activePlayerRace's managed-side event-driven caching - takes the ECS
        // singleton's ActiveHeroID directly, same pattern as GetHeroStatBonus/GetHeroAttributeBonus.
        public static List<UnitStatBonus> GetFactionBonusForHero(UnitStat _unitStat, int activeHeroID)
        {
            if (activeHeroID == -1) return new List<UnitStatBonus>();
            return GetFactionBonusForRace(_unitStat, HeroData.GetRaceFromHero(activeHeroID));
        }

        private static List<UnitStatBonus> GetFactionBonusForRace(UnitStat _unitStat, Race race)
        {
            List<UnitStatBonus> unitStatBonuses = new();
            EnsureRulesLoaded();

            foreach (var rule in _factionRules)
            {
                if (rule.Race != race || rule.Stat != _unitStat) continue;

                // LocalizationKey holds a "Bonus Name: effect description" entry - take the name half,
                // same as the original hardcoded Sakura-only implementation.
                string localisedBonusName = LocalizationManager.Instance.GetText(rule.LocalizationKey).Split(':')[0];

                float value = rule.Value;
                if (rule.MagnitudeKind == BonusMagnitudeKind.PercentOfCurrentValue)
                {
                    Debug.LogWarning($"[HeroBonusManager] FactionBonusRule for {rule.Race}/{rule.Stat} uses PercentOfCurrentValue, which GetFactionBonus has no current-value input to apply against - using raw Value instead.");
                }

                unitStatBonuses.Add(new UnitStatBonus(_unitStat, localisedBonusName, value));
            }

            return unitStatBonuses;
        }
                    
        public static List<UnitStatBonus> GetHeroStatBonus(UnitStat _unitStat, UnitName _requestingUnit, int activeHeroID, float currentStatValue)
        {
            List<UnitStatBonus> unitStatBonuses = new();

            if (activeHeroID == -1) return unitStatBonuses;

            EnsureRulesLoaded();
            SquadStats stats = TabletopTavernData.Instance.GetSquadStats(_requestingUnit);

            foreach (var rule in _statRules)
            {
                if (rule.HeroID != activeHeroID || rule.Stat != _unitStat) continue;
                if (!rule.Condition.Matches(_requestingUnit, stats, enemyRace)) continue;

                float value = rule.MagnitudeKind == BonusMagnitudeKind.PercentOfCurrentValue ? currentStatValue * rule.Value : rule.Value;
                unitStatBonuses.Add(new UnitStatBonus(_unitStat, LocalizationManager.Instance.GetText(rule.LocalizationKey), value));
            }

            return unitStatBonuses;
        }
        public static List<UnitAttributeBonus> GetHeroAttributeBonus(UnitName _requestingUnit, int activeHeroID)
        {
            List<UnitAttributeBonus> unitAttributeBonuses = new();

            if(activeHeroID == -1) return unitAttributeBonuses;

            EnsureRulesLoaded();
            SquadStats stats = TabletopTavernData.Instance.GetSquadStats(_requestingUnit);
            SquadAttributes squadAttributes = stats.SquadAttributes;

            foreach (var rule in _attributeRules)
            {
                if (rule.HeroID != activeHeroID) continue;
                if (TabletopTavernConstants.GetAttribute(squadAttributes, rule.GrantedAttribute)) continue; // already has it
                if (!rule.Condition.Matches(_requestingUnit, stats, enemyRace)) continue;

                unitAttributeBonuses.Add(new UnitAttributeBonus(rule.GrantedAttribute, LocalizationManager.Instance.GetText(rule.LocalizationKey), 0));
            }

            return unitAttributeBonuses;
        }
        // Allocation-free for per-frame callers; base stats count too, so modded attributes are honored.
        public static bool UnitHasAttribute(UnitName _requestingUnit, int activeHeroID, UnitAttribute attribute)
        {
            SquadStats stats = TabletopTavernData.Instance.GetSquadStats(_requestingUnit);
            if (TabletopTavernConstants.GetAttribute(stats.SquadAttributes, attribute)) return true;
            if (activeHeroID == -1) return false;

            EnsureRulesLoaded();
            foreach (var rule in _attributeRules)
            {
                if (rule.HeroID != activeHeroID || rule.GrantedAttribute != attribute) continue;
                if (rule.Condition.Matches(_requestingUnit, stats, enemyRace)) return true;
            }
            return false;
        }
        public static string GetLocalizedHeroUnlockDescription(Hero _hero, UnlockCondition _unlockCondition)
        {
            if(_unlockCondition != UnlockCondition.HeroCompletion) 
                return LocalizationManager.Instance.GetText(_unlockCondition.ToString());

            Hero prerequisiteHero = HeroData.GetHeroByID(_hero.HeroID - 1);
            string prerequisiteHeroName = LocalizationManager.Instance.GetText(prerequisiteHero.HeroName);
            string heroCompletionText = LocalizationManager.Instance.GetText("heroCompletion");
            return $"{heroCompletionText} {prerequisiteHeroName}";
        }
        public void OnDestroy()
        {
            sceneHandler.OnGameStateChanged -= OnGameStateChanged;
        }
    }
}