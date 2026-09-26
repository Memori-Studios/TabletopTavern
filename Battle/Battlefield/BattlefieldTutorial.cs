using System.Collections.Generic;
using Memori.SaveData;
using TJ.Map;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Pops unread Battle Guide topics before deployment, one at a time, and tracks them as read.
    /// </summary>
    public class BattlefieldTutorial : MonoBehaviour
    {
        /// <summary>
        /// When a topic may pop as a pre-battle tip. A topic whose condition is not met is skipped without being
        /// marked as read, so it still pops on a later battle where it applies. Settings > Guide ignores this.
        /// Append only: the ordinals are serialized in BattleGuideContent.
        /// </summary>
        public enum BattlefieldInfoCondition
        {
            None,
            PlayerArmyContainsMage,
            SpellsEnabled,
            PlayerArmyContainsRanged,
            PlayerArmyContainsShields,
            PlayerArmyContainsLarge,
            PlayerArmyContainsAntiLarge,
            GarrisonBattle,
        }

        [SerializeField] private GameObject _battlefieldTutorialCanvas;
        [SerializeField] private BattleGuideView _guide;
        // How many unread topics one popup offers; the rest wait for later battles.
        [SerializeField] private int _maxTipsPerBattle = 3;
        public bool TutorialIsOpen => _battlefieldTutorialCanvas.activeSelf;

        // Resolved once per battle load; the army cannot change while the popup is up.
        private readonly HashSet<BattlefieldInfoCondition> _armyConditions = new();

        public void HandleTutorialStuff()
        {
            // Attack orders come after Start Battle: in a deferred layout the enemy is not on the field while deploying.
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[6] {
                TutorialData.SelectUnit,
                TutorialData.RepositionUnit,
                TutorialData.SelectMultipleUnits,
                TutorialData.StartBattle,
                TutorialData.GiveAttackOrders,
                TutorialData.ChangeBattleSpeed});

            if (_guide == null || _guide.Content == null)
            {
                Debug.LogError("BattlefieldTutorial: the Battle Guide view or its content is not assigned.");
                return;
            }

            ReadArmy();
            List<GuideTopic> tips = UnreadTips();
            if (tips.Count == 0) return;
            _battlefieldTutorialCanvas.SetActive(true);
            _guide.OpenTips(tips, ReturnToBattle);
        }

        #region Tip selection
        private List<GuideTopic> UnreadTips()
        {
            PlayerSaveData save = SaveDataHandler.LoadPlayerSaveData();
            if (BattleGuideProgress.Migrate(save)) SaveDataHandler.SavePlayerSaveData(save);

            var candidates = new List<(GuideTopic topic, int order)>();
            List<GuideTopic> topics = _guide.Content.topics;
            for (int i = 0; i < topics.Count; i++)
            {
                GuideTopic topic = topics[i];
                if (!topic.showAsTip || !BattleGuideProgress.IsAvailable(topic) || !IsConditionMet(topic.condition)) continue;
                if (save.BattlefieldInfoSectionsViewed.Contains(topic.id)) continue;
                candidates.Add((topic, i));
            }
            candidates.Sort((a, b) => a.topic.tipPriority != b.topic.tipPriority ? a.topic.tipPriority.CompareTo(b.topic.tipPriority) : a.order.CompareTo(b.order));

            var tips = new List<GuideTopic>();
            foreach (var candidate in candidates)
            {
                if (tips.Count >= _maxTipsPerBattle) break;
                tips.Add(candidate.topic);
            }
            return tips;
        }

        private bool IsConditionMet(BattlefieldInfoCondition condition)
        {
            if (condition == BattlefieldInfoCondition.None) return true;
            if (condition == BattlefieldInfoCondition.SpellsEnabled)
            {
#if SPELLS
                return true;
#else
                return false;
#endif
            }
            return _armyConditions.Contains(condition);
        }

        private void ReadArmy()
        {
            _armyConditions.Clear();
            if (BattleManager.Instance.BattleSaveManager.IsGarrisonBattle) _armyConditions.Add(BattlefieldInfoCondition.GarrisonBattle);
            // Same source ArmySpawnManager loads from, so custom and campaign battles agree.
            var (army, _) = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(true);
            foreach (SquadToLoad squad in army)
            {
                SquadStats stats = TabletopTavernData.Instance.GetSquadStats(squad.UnitName);
                if (TabletopTavernConstants.Casts(stats.unitType)) _armyConditions.Add(BattlefieldInfoCondition.PlayerArmyContainsMage);
                if (TabletopTavernConstants.Shoots(stats.unitType)) _armyConditions.Add(BattlefieldInfoCondition.PlayerArmyContainsRanged);
                if (stats.SquadAttributes.StandardShields || stats.SquadAttributes.HeavyShields) _armyConditions.Add(BattlefieldInfoCondition.PlayerArmyContainsShields);
                if (stats.unitSize == UnitSize.Cavalry || stats.unitSize == UnitSize.Monstrous) _armyConditions.Add(BattlefieldInfoCondition.PlayerArmyContainsLarge);
                if (stats.SquadAttributes.AntiLarge) _armyConditions.Add(BattlefieldInfoCondition.PlayerArmyContainsAntiLarge);
            }
        }
        #endregion

        public void ReturnToBattle()
        {
            _battlefieldTutorialCanvas.SetActive(false);
        }

        [ContextMenu("Reset Battlefield Tutorial")]
        public void ResetBattlefieldTutorial()
        {
            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            playerSaveData.BattlefieldInfoSectionsViewed.Clear();
            SaveDataHandler.SavePlayerSaveData(playerSaveData);
        }
    }
}
