using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Audio;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Memori.Notifications;
using Memori.Localization;
using System;

namespace TJ.Event
{
    public class EventRewardsPopUpDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Transform rewardParent;
        [SerializeField] private EventRewardDisplay eventRewardPrefab;
        List<EventRewardDisplay> eventRewards = new ();
        [SerializeField] private EventRewardDisplay noRewardsText;
        [SerializeField] private EventAquireRewardButton aquireRewardButtonPrefab;
        [SerializeField] private List<EventAquireRewardButton> eventAquireRewardButtons = new ();
        [SerializeField] private Sprite gearSprite;
        // EventAquireRewardButton acquireGearButton = null;
        public void LoadTitle(string _title)
        {
            titleText.text = MemoriUI.AddSpacesToSentence(_title);
            eventRewards.ForEach(x => Destroy(x.gameObject));
            eventRewards.Clear();
        }
        public async void LoadEventRewards(EventReward _eventReward)
        {
            foreach (EventRewardDisplay eventReward in eventRewards)
            {
                Destroy(eventReward.gameObject);
            }
            
            eventRewards.Clear();
            eventAquireRewardButtons.Clear();

            // Campaign-seeded, so re-entering the event (exiting to the main menu and back) offers the same
            // rewards instead of rerolling them. One stream for the whole outcome, so an outcome carrying
            // two consumable or unit drops still gets independent draws for each.
            System.Random rewardRandom = CampaignManager.Instance.CampaignSaveManager.GetCampaignRandom();

            foreach (EventOutcomeModifier eventOutcomeModifier in _eventReward.EventOutcome.EventOutcomeModifiers)
            {
                IAudioRequester.Instance.PlaySFX(SFXData.CollectItem);

                switch (eventOutcomeModifier.EventOutcomeModifierEnum)
                {
                    case EventOutcomeModifierEnum.None:
                        {
                            EventRewardDisplay eventRewardDisplay = Instantiate(noRewardsText, rewardParent);
                            eventRewards.Add(eventRewardDisplay);
                            break;
                        }
                    case EventOutcomeModifierEnum.PrestigeUnit:
                        {
                            string unitName = CampaignManager.Instance.CampaignSaveManager.PrestigeRandomUnit();
                            EventRewardDisplay eventRewardDisplay = Instantiate(eventRewardPrefab, rewardParent);
                            eventRewardDisplay.LoadEventReward(eventOutcomeModifier, LocalizationManager.Instance.GetText(unitName));
                            eventRewards.Add(eventRewardDisplay);
                            break;
                        }
                    case EventOutcomeModifierEnum.GearDrop:
                        {
                            EventAquireRewardButton acquireGearButton = Instantiate(aquireRewardButtonPrefab, rewardParent);
                            string AcquireGearLocalized = LocalizationManager.Instance.GetText("Acquire Gear");
                            List<GearID> gearList = CampaignManager.Instance.CampaignSaveManager.DrawRandomGear(1);
                            void acquireGearAction()
                            {
                                CampaignManager.Instance.MapSceneUIManager.TreasurePanel.LoadTreasurePanelFromShop(gearList[0]);
                                Destroy(acquireGearButton.gameObject);
                            }
                            acquireGearButton.SetUp(gearSprite, AcquireGearLocalized, acquireGearAction);
                            eventAquireRewardButtons.Add(acquireGearButton);
                            break;
                        }
                    case EventOutcomeModifierEnum.NewUnit:
                        {
                            //gets a new, likely low tier unit
                            UnitName[] unitNames = TabletopTavernData.Instance.GetSquadsToRecruitBasedOnReputation(0, 1, rewardRandom.Next(), CampaignManager.Instance.CampaignSaveManager.GetHeroID());
                            EventAquireRewardButton aquireUnitButton = Instantiate(aquireRewardButtonPrefab, rewardParent);
                            string RecruitUnitLocalized = LocalizationManager.Instance.GetText("Recruit Unit");
                            void recruitUnitAction()
                            {
                                if (aquireUnitButton.CanCombine)
                                {
                                    var army = CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy;
                                    string uid1 = string.Empty, uid2 = string.Empty;
                                    for (int i = 0; i < army.Length; i++)
                                    {
                                        if (army[i].UnitIndex == -1 || army[i].UnitName != unitNames[0] || army[i].UnitPrestige != 0) continue;
                                        if (uid1 == string.Empty) uid1 = army[i].UniqueID;
                                        else { uid2 = army[i].UniqueID; break; }
                                    }
                                    if (uid1 != string.Empty && uid2 != string.Empty)
                                    {
                                        IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
                                        CampaignManager.Instance.CampaignSaveManager.PrestigeAndCombineWithRecruit(uid1, uid2);
                                        Destroy(aquireUnitButton.gameObject);
                                    }
                                    return;
                                }
                                RecruitUnit(unitNames[0], aquireUnitButton.gameObject);
                            }

                            aquireUnitButton.SetUp(TabletopTavernData.Instance.GetUnitIcon(unitNames[0]),
                                $"{RecruitUnitLocalized} ({LocalizationManager.Instance.GetText(unitNames[0].ToString())})",
                                recruitUnitAction
                            );
                            aquireUnitButton.SetUpCombineCheck(unitNames[0]);
                            eventAquireRewardButtons.Add(aquireUnitButton);
                            
                            break;
                        }
                    case EventOutcomeModifierEnum.ConsumableDrop:
                        {
                            EventAquireRewardButton acquireConsumableButton = Instantiate(aquireRewardButtonPrefab, rewardParent);

                            ConsumableEnum consumable = ConsumableData.GetRandomConsumable(rewardRandom);
                            Consumable consumableData = ConsumableData.GetConsumable(consumable);

                            string consumableNameLocalized = LocalizationManager.Instance.GetText(consumableData.ConsumableEnum.ToString() + "Name");
                            void acquireConsumableAction()
                            {
                                AquireConsumable(consumable, acquireConsumableButton.gameObject);
                            }
                            acquireConsumableButton.SetUp(
                                SpriteData.GetSprite(consumable.ToString()),
                                consumableNameLocalized,
                                acquireConsumableAction
                            );
                            eventAquireRewardButtons.Add(acquireConsumableButton);
                            break;
                        }
                    default:
                        {
                            EventRewardDisplay eventRewardDisplay = Instantiate(eventRewardPrefab, rewardParent);
                            eventRewardDisplay.LoadEventReward(eventOutcomeModifier);
                            eventRewards.Add(eventRewardDisplay);
                            break;
                        }
                }

                await Task.Delay(300);
            }
        }
        public void RecruitUnit(UnitName _unitName, GameObject _button)
        {
            if(!CampaignManager.Instance.CampaignSaveManager.CheckForRoomToRecruit()) {
                string errorLocalized = LocalizationManager.Instance.GetText("Max Units Recruited");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }
            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
            CampaignManager.Instance.CampaignSaveManager.RecruitSquad(TabletopTavernData.Instance.GetSquadStats(_unitName));
            Destroy(_button);
        }
        public void AquireConsumable(ConsumableEnum _consumable, GameObject _button)
        {
            if (!CampaignManager.Instance.CampaignSaveManager.HasRoomForConsumable())
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("noRoomForConsumable"));
                return;
            }
            IAudioRequester.Instance.PlaySFX(SFXData.CollectItem);
            CampaignManager.Instance.CampaignSaveManager.AquireConsumable(_consumable);
            Destroy(_button);
        }
        public void CollectRemainingEventRewards()
        {
            foreach (EventAquireRewardButton eventReward in eventAquireRewardButtons)
            {
                if(eventReward != null)
                {
                    eventReward.Button.onClick.Invoke();
                }
            }
        }
    }
}
