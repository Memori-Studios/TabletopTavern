using UnityEngine;
using Memori.SaveData;
using TJ.Map;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TJ
{
    public class BattlefieldTutorial : MonoBehaviour
    {
        /// <summary>
        /// When a section is allowed to pop as a first-time tip. A section whose condition is not met is
        /// skipped without being marked as seen, so it still pops on a later battle where it applies.
        /// The Settings > Info copy of the same canvas ignores this - every section is always browsable there.
        /// </summary>
        public enum BattlefieldInfoCondition
        {
            None,
            PlayerArmyContainsMage,
            SpellsEnabled,
        }

        [System.Serializable] public struct BattlefieldInfoOverrideData
        {
            public GameObject Header;
            public GameObject Content;
            public string ContentDescription;
            public BattlefieldInfoCondition Condition;
        }

        [SerializeField] private GameObject _battlefieldTutorialCanvas;
        [SerializeField] private Button _showAnotherTipButton, _returnToBattleButton;
        public bool TutorialIsOpen => _battlefieldTutorialCanvas.activeSelf;
        [SerializeField] private BattlefieldInfoOverrideData[] battlefieldInfoOverrideDataArray;

        // Resolved once per battle load in HandleTutorialStuff; the army cannot change while the popup is up.
        private bool _playerArmyContainsMage;

        public void HandleTutorialStuff()
        {
            _showAnotherTipButton.onClick.RemoveAllListeners();
            _showAnotherTipButton.onClick.AddListener(ShowAnotherTip);
            _returnToBattleButton.onClick.RemoveAllListeners();
            _returnToBattleButton.onClick.AddListener(ReturnToBattle);

            _playerArmyContainsMage = PlayerArmyContainsMage();

            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[5] {
                TutorialData.SelectUnit,
                TutorialData.RepositionUnit,
                // TutorialData.GiveAttackOrders,
                TutorialData.SelectMultipleUnits,
                TutorialData.ChangeBattleSpeed,
                TutorialData.StartBattle});

            // TutorialManager.Instance.LoadTooltip(TutorialData.GuardMode, BattleManager.Instance.UIManager.GuardModeButtonTransform);
            if(CheckForUnseenTips())
            {
                _battlefieldTutorialCanvas.SetActive(true);
                OpenTip();
            }
        }

        #region Section conditions
        private bool IsSectionAvailable(BattlefieldInfoOverrideData overrideData)
        {
            switch (overrideData.Condition)
            {
                case BattlefieldInfoCondition.PlayerArmyContainsMage:
                    return _playerArmyContainsMage;
                case BattlefieldInfoCondition.SpellsEnabled:
#if SPELLS
                    return true;
#else
                    return false;
#endif
                default:
                    return true;
            }
        }

        private static bool PlayerArmyContainsMage()
        {
            // Same source ArmySpawnManager loads from, so custom and campaign battles agree. The call
            // re-sets BattleSaveManager.PlayerSquadsToSpawn to the value it already holds, which is harmless.
            var (army, _) = BattleManager.Instance.BattleSaveManager.GetArmyFromSaveData(true);
            foreach (SquadToLoad squad in army)
            {
                if (TabletopTavernConstants.Casts(TabletopTavernData.Instance.GetSquadStats(squad.UnitName).unitType))
                    return true;
            }
            return false;
        }
        #endregion

        public bool CheckForUnseenTips()
        {
            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            // for each override data, check to see if it is saved in the playerSaveData, if not, set the header and content to active and save it in the playerSaveData as seen
            foreach (BattlefieldInfoOverrideData overrideData in battlefieldInfoOverrideDataArray)
            {
                if (!IsSectionAvailable(overrideData)) continue;
                if (!playerSaveData.BattlefieldInfoSectionsViewed.Contains(overrideData.ContentDescription))
                {
                    return true;
                }
            }
           return false;
        }
        public void OpenTip()
        {
            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            // for each override data, check to see if it is saved in the playerSaveData, if not, set the header and content to active and save it in the playerSaveData as seen
            foreach (BattlefieldInfoOverrideData overrideData in battlefieldInfoOverrideDataArray)
            {
                if (!IsSectionAvailable(overrideData)) continue;
                if (!playerSaveData.BattlefieldInfoSectionsViewed.Contains(overrideData.ContentDescription))
                {
                    overrideData.Content.SetActive(true);
                    overrideData.Header.SetActive(true);
                    playerSaveData.BattlefieldInfoSectionsViewed.Add(overrideData.ContentDescription);
                    SaveDataHandler.SavePlayerSaveData(playerSaveData);
                    break;
                }
            }
            if(!CheckForUnseenTips())
            {
                _showAnotherTipButton.gameObject.SetActive(false);
            }
            EventSystem.current.SetSelectedGameObject(_returnToBattleButton.gameObject);
            _returnToBattleButton.GetComponent<Animator>().SetTrigger("Selected");
        }
        public void ShowAnotherTip()
        {
            foreach (BattlefieldInfoOverrideData overrideData in battlefieldInfoOverrideDataArray)
            {
                overrideData.Header.SetActive(false);
                overrideData.Content.SetActive(false);
            }
            OpenTip();
        }
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
