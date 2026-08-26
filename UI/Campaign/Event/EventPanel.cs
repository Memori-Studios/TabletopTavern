using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Utilities;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;
using Memori.UI;
using TJ.Map;
using Memori.Audio;
using Memori.Notifications;
using Memori.Scenes;
using Memori.SaveData;
using Unity.Mathematics;
using Memori.Localization;
using Memori.Steamworks;
using Memori.Tooltip;
using Memori.Metaprogression;

namespace TJ.Event
{
    public class EventPanel : MapPanel
    {
        [SerializeField] private TMP_Text chapterNumberText, eventNameText, eventDescriptionText;
        [SerializeField] private Transform eventChoicesParent;
        [SerializeField] private EventChoiceDisplay eventChoicePrefab;
        [SerializeField] private Animator chapterAnimator;
        [SerializeField] private EventRewardsPopUpDisplay eventRewardsDisplay;
        [SerializeField] private TMP_Text outcomeDescriptionText;
        [SerializeField] private MemoriCanvasGroup uiBackgroundCanvasGroup;
        // [SerializeField] private MemoriButtonV2 diceRollCodeButton;

        [Header("Roll Dice")]
        [SerializeField] private TMP_Text rollResultText;
        [SerializeField] private TMP_Text continueButtonText;//, rerollButtonText; //successOrFailText,
        [SerializeField] private GameObject rollTextObject;
        [SerializeField] private MemoriButtonV2 rerollButton, acceptRollButton;//rerollButton,
        [SerializeField] private Animator diceRollAnimator;
        [SerializeField] private Transform physicsDieTransform;
        [SerializeField] private DieSpinner physicsDie;

        [Header("Rewards")]
        [SerializeField] private EventRollOutcome eventRollOutcome;
        [SerializeField] private ReputationModificationSlider reputationSlider;
        [SerializeField] private GameObject rerollButtonGameObject;
        [SerializeField] private LockedButton rerollLockedButton;

        [Header("Camera")]
        [SerializeField] private Camera eventCamera;
        [SerializeField] private MenuCameraRotator menuCameraRotator;
        [SerializeField] private float lookAtPeriod = 2f;
        [SerializeField] private float focusFOV = 20f;
        [SerializeField] private Transform _overheadCameraTransform;
        Quaternion originalRotation;
        float originalFOV;
        Vector3 _cameraBaseLocalPosition;
        bool _isOpen;

        [Header("Startlit Guidance")]
        [SerializeField] private GameObject startLitGuidance;
        [SerializeField] private TMP_Text startLitGuidanceOutcomeText, starlitGuidanceCostText;
        [SerializeField] private MemoriTooltipTrigger startLitGuidanceTooltip;
        bool startLitGuidanceActive = false;
        Button startlitGuidanceButton;

        [Header("Claimed by Destiny")]
        [SerializeField] private GameObject claimedByDestiny;
        [SerializeField] private MemoriTooltipTrigger claimedByDestinyTooltip;
        bool claimedByDestinyActive = false;
        Button claimedByDestinyButton;

        
        [Header("Event Scene Prefab")]
        [SerializeField] private AssetReferenceGameObject _eventScenePrefab;
        private GameObject _eventSceneInstance;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _eventDoubleRewardsMetaprogressionModel;
        [SerializeField] private MetaprogressionModel _eventReducedRollRequirementsMetaprogressionModel;

        TT_Event[] gc_Events;
        TT_Event gc_Event;

        MemoriCanvasGroup eventPanelCanvasGroup;
        List<EventChoiceDisplay> eventChoices = new List<EventChoiceDisplay>();
        MemoriCanvasGroup descMemoriCanvasGroup, choiceParentMemoriCanvasGroup;
        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;
        EventChoice selectedChoice;
        [SerializeField] bool rollAccepted = false;
        int rollBonus = 0;
        int roll = 0;
        bool eventRolled = false;
        public bool CanReroll => !rollAccepted && eventRolled;
        string collapsedEventName;
        private void Awake()
        {
            originalRotation = eventCamera.transform.rotation;
            originalFOV = eventCamera.fieldOfView;
            physicsDie.gameObject.SetActive(false);
        }

        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;

            eventPanelCanvasGroup = GetComponent<MemoriCanvasGroup>();
            descMemoriCanvasGroup = eventDescriptionText.GetComponent<MemoriCanvasGroup>();
            choiceParentMemoriCanvasGroup = eventChoicesParent.GetComponent<MemoriCanvasGroup>();

            acceptRollButton.Button.onClick.RemoveAllListeners();
            acceptRollButton.Button.onClick.AddListener(AcceptRoll);

            gc_Events = EventData.GetAllEvents();
            descMemoriCanvasGroup.CGDisable();
            choiceParentMemoriCanvasGroup.CGDisable();

            // diceRollCodeButton.Button.onClick.RemoveAllListeners();
            // diceRollCodeButton.Button.onClick.AddListener(() => Application.OpenURL("https://github.com/Memori-Studios/TabletopTavernPublic/blob/main/RandomDiceRoll.cs"));

            //reroll buttons
            startLitGuidance.SetActive(false);
            claimedByDestiny.SetActive(false);
            startlitGuidanceButton = startLitGuidance.GetComponent<Button>();
            // startlitGuidanceButton.onClick.RemoveAllListeners();
            // startlitGuidanceButton.onClick.AddListener(() => ActivateStartLitGuidance());

            claimedByDestinyButton = claimedByDestiny.GetComponent<Button>();
            claimedByDestinyButton.onClick.RemoveAllListeners();
            claimedByDestinyButton.onClick.AddListener(() => ActivateClaimedByDestiny());
        }
        public async void LoadEventPanel(int _chapter)
        {
            if (_eventScenePrefab != null && _eventScenePrefab.RuntimeKeyIsValid())
            {
                GameObject prefab = await AddressablesManager.Instance.LoadAsync<GameObject>(_eventScenePrefab);
                if (prefab != null)
                    _eventSceneInstance = Instantiate(prefab);
            }

            menuCameraRotator.enabled = true;
            menuCameraRotator.transform.rotation = Quaternion.identity;
            _cameraBaseLocalPosition = eventCamera.transform.localPosition;
            _isOpen = true;
            physicsDie.gameObject.SetActive(true);
            diceRollAnimator.gameObject.SetActive(false);
            physicsDie.PreSpinDie();
            SceneHandler.Instance.TranstionCameras(
                CampaignManager.Instance.MapCamera.MapCameraInstance,
                CampaignManager.Instance.MapCamera.EventCamera
            );
            uiBackgroundCanvasGroup.FadeInAsync();
            await Task.Delay(500);
            CampaignManager.Instance.MapCamera.OverrideDepthOfField();

            if (HeroBonusManager.Instance.ActiveHeroID == 7 || HeroBonusManager.Instance.ActiveHeroID == 8)
            {
                starlitGuidanceCostText.text = "5";
                if (CampaignManager.Instance.GoldManager.CheckIfCanAfford(5))
                {
                    starlitGuidanceCostText.color = (Color)ColorData.HexToRgba(ColorData.Primary);
                }
                else
                {
                    starlitGuidanceCostText.color = (Color)ColorData.HexToRgba(ColorData.Error);
                }
                starlitGuidanceCostText.text += " <sprite name=GoldSprite>";
                startLitGuidanceActive = false;
                startLitGuidance.SetActive(true);
                startLitGuidanceOutcomeText.text = "?";
                startLitGuidanceTooltip.SetUpToolTip(
                    LocalizationManager.Instance.GetText("Campaign Bonus"),
                    LocalizationManager.Instance.GetText("TaelindorForestBonusDescription")
                );
            }
            else
            {
                startLitGuidance.SetActive(false);
            }

            gc_Event = GetRandomEvent();
            string chapterLocalized = LocalizationManager.Instance.GetText("Chapter");
            chapterNumberText.text = $"{chapterLocalized} {MemoriUI.ConvertNumberToRomanNumeral(_chapter + 1)}";

            //localization
            collapsedEventName = gc_Event.EventName.Replace(" ", "");
            string eventTitleLocalized = LocalizationManager.Instance.GetEventString(collapsedEventName + "Name");
            string eventDescriptionLocalized = LocalizationManager.Instance.GetEventString(collapsedEventName + "Desc");
            eventNameText.text = eventTitleLocalized;
            eventDescriptionText.text = eventDescriptionLocalized;

            eventPanelCanvasGroup.canvasGroup.interactable = true;
            eventPanelCanvasGroup.canvasGroup.blocksRaycasts = true;
            eventPanelCanvasGroup.FadeInAsync();
            descMemoriCanvasGroup.FadeInAsync();
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1]{ TutorialData.EventExplanation });
            chapterAnimator.SetBool("Active", true);
            eventRewardsDisplay.gameObject.SetActive(false);

            rollTextObject.SetActive(false);
            rerollButton.gameObject.SetActive(false);
            acceptRollButton.gameObject.SetActive(false);
            rollAccepted = false;
            acceptRollButton.Button.onClick.RemoveAllListeners();
            acceptRollButton.Button.onClick.AddListener(AcceptRoll);

            eventChoices.ForEach(x => Destroy(x.gameObject));
            eventChoices.Clear();
            outcomeDescriptionText.text = "";
            choiceParentMemoriCanvasGroup.CGEnable();
            claimedByDestinyActive = false;
            claimedByDestiny.SetActive(false);

            await Task.Delay(500);
            for (int i = 0; i < gc_Event.EventChoices.Length; i++)
            {
                EventChoiceDisplay eventChoise = Instantiate(eventChoicePrefab, eventChoicesParent);
                EventChoice choice = gc_Event.EventChoices[i];
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_eventReducedRollRequirementsMetaprogressionModel)) {
                    choice.minimumRollNeeded = math.max(1, choice.minimumRollNeeded - _eventReducedRollRequirementsMetaprogressionModel.NodeValue);
                }
                eventChoise.LoadEventChoice(choice, this, collapsedEventName + i);
                eventChoices.Add(eventChoise);
                await Task.Delay(100);
            }
        }
        public async void ChoiceSelected(EventChoice _eventChoice, int _index)
        {
            selectedChoice = _eventChoice;
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            // descriptionObject.SetActive(false);
            eventChoices.ForEach(x => x.Disable());
            startLitGuidance.SetActive(false);

            RollDice();

            uiBackgroundCanvasGroup.FadeOutAsync(0.25f);

            while (!rollAccepted)
            {
                await Task.Yield();
            }

            IAudioRequester.Instance.PlaySFX(SFXData.EventOptionLoad);
            EventReward eventReward = GenerateReward(selectedChoice, eventRollOutcome);

            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_eventDoubleRewardsMetaprogressionModel) &&
                eventRollOutcome is EventRollOutcome.CriticalSuccess or EventRollOutcome.Success) {
                var modifiers = eventReward.EventOutcome.EventOutcomeModifiers;
                for (int i = 0; i < modifiers.Count; i++) {
                    if (modifiers[i].EventOutcomeModifierEnum == EventOutcomeModifierEnum.Gold) {
                        var m = modifiers[i];
                        m.Value *= 2;
                        modifiers[i] = m;
                    }
                }
                Debug.Log($"Doubled Event Gold Rewards!");
            }

            string eventOutcomeDescription = LocalizationManager.Instance.GetEventString(collapsedEventName + _index + eventRollOutcome.ToString() + "OutcomeDesc");
            outcomeDescriptionText.text = eventOutcomeDescription;

            //Critical Success, Success, Failure, Critical Failure
            string eventRollOutcomeLocalized = LocalizationManager.Instance.GetText(eventRollOutcome.ToString());
            eventRewardsDisplay.LoadTitle(eventRollOutcomeLocalized);

            eventRewardsDisplay.gameObject.SetActive(true);

            await Task.Delay(500);
            campaignSaveManager.AddEventReward(eventReward);
            eventRewardsDisplay.LoadEventRewards(eventReward);
        }
        public async void RollDice()
        {
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.EventExplanation);
            menuCameraRotator.enabled = false;
            menuCameraRotator.transform.rotation = Quaternion.identity;
            if (eventCamera.TryGetComponent<ParallaxCamera>(out var parallax)) parallax.enabled = false;
            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            rollResultText.text = "";
            eventDescriptionText.text = "";

            rollBonus = 0;
            System.Random random = campaignSaveManager.GetCampaignRandom();
            roll = random.Next(1, 21);
            if (campaignSaveManager.FateshineElixirArmed)
            {
                roll = 20;
                campaignSaveManager.ConsumeFateshineElixir();
            }

            //old
            // if (CampaignManager.Instance.GearManager.CheckForGear(GearID.Shungite)) roll = math.clamp(roll, 2, 20);

            FocusOnDice();
            outcomeDescriptionText.enabled = false;

            physicsDie.gameObject.SetActive(true);
            diceRollAnimator.gameObject.SetActive(false);
            diceRollAnimator.transform.localPosition = Vector3.zero;
            physicsDie.ResetDie();

            await Task.Delay(1000);

            physicsDie.gameObject.SetActive(false);
            diceRollAnimator.gameObject.SetActive(true);
            diceRollAnimator.Play("Dice_" + roll);

            await Task.Delay(500);

            rollTextObject.SetActive(true);
            rollResultText.text = roll.ToString();

            await Task.Delay(500);

            if (roll == 20) SteamAchievements.Unlock(AchievementId.Roll20);
            if (roll == 1) SteamAchievements.Unlock(AchievementId.SnakeEyes);

            rerollButton.gameObject.SetActive(true);
            acceptRollButton.gameObject.SetActive(true);
            string acceptLocalized = LocalizationManager.Instance.GetText("Accept");
            continueButtonText.text = acceptLocalized;

            //DifficultyMod 8
            bool rerollLocked = CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Baron;
            string difficultyLocalized = LocalizationManager.Instance.GetText("Difficulty");
            string difficultyLevelLocalized = LocalizationManager.Instance.GetText("difficultyName" + (int)CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel);
            rerollLockedButton.SetLockedState(rerollLocked, $"{difficultyLocalized}: {difficultyLevelLocalized}");

            reputationSlider.LoadReputationSlider(roll, this);
            ShowRowResult();
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.ModifyRoll });

            switch (roll)
            {
                case 1:
                    IAudioRequester.Instance.PlaySFX(SFXData.CriticalFailure);
                    IAudioRequester.Instance.PlaySFX(SFXData.Boo);
                    break;
                case 20:
                    IAudioRequester.Instance.PlaySFX(SFXData.CriticalSuccess);
                    IAudioRequester.Instance.PlaySFX(SFXData.Cheer);
                    break;
                default:
                    bool success = roll >= selectedChoice.minimumRollNeeded;
                    IAudioRequester.Instance.PlaySFX(success ? SFXData.Success : SFXData.Failure);
                    IAudioRequester.Instance.PlaySFX(success ? SFXData.Cheer : SFXData.Boo);
                    break;
            }
            eventRolled = true;
        }
        private void ShowRowResult()
        {
            int modifiedRoll = roll + rollBonus;
            rollResultText.text = modifiedRoll.ToString();
            rollTextObject.SetActive(true);

            if (modifiedRoll == 20)
            {
                eventRollOutcome = EventRollOutcome.CriticalSuccess;
            }
            else if (modifiedRoll == 1)
            {
                eventRollOutcome = EventRollOutcome.CriticalFailure;
            }
            else
            {
                eventRollOutcome = modifiedRoll >= selectedChoice.minimumRollNeeded ? EventRollOutcome.Success : EventRollOutcome.Failure;
            }

            rollResultText.color = modifiedRoll >= selectedChoice.minimumRollNeeded ? Color.green : Color.red;
        }
        public void ModifyRoll(int _value)
        {
            rollBonus = _value;
            IAudioRequester.Instance.PlaySFX(SFXData.SliderClick);
            ShowRowResult();
        }
        public void AcceptRoll()
        {
            outcomeDescriptionText.enabled = true;
            uiBackgroundCanvasGroup.FadeInAsync(0.25f);
            rollAccepted = true;
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.TowerShields)) rollBonus /= 2;
            string localizedString = LocalizationManager.Instance.GetText("Cost");
            CampaignManager.Instance.GoldManager.ModifyGold(-rollBonus, localizedString);
            rerollButton.gameObject.SetActive(false);
            acceptRollButton.Button.onClick.RemoveAllListeners();
            acceptRollButton.Button.onClick.AddListener(() => CompleteEvent());

            string continueButtonTextLocalized = LocalizationManager.Instance.GetText("continueButton");
            continueButtonText.text = continueButtonTextLocalized;
            eventChoices.ForEach(x => x.Hide());
            eventRolled = false;
        }
        public override async void ClosePanel()
        {
            Debug.Log("[Map] Closing EventPanel");
            _isOpen = false;
            eventCamera.transform.localPosition = _cameraBaseLocalPosition;
            eventRewardsDisplay.CollectRemainingEventRewards();
            SceneHandler.Instance.TranstionCameras(
                CampaignManager.Instance.MapCamera.EventCamera,
                CampaignManager.Instance.MapCamera.MapCameraInstance
            );
            await Task.Delay(500);

            campaignSaveManager.RemoveZeroHealthSquads();
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.ArmyStructureChanged();

            chapterAnimator.SetBool("Active", false);
            descMemoriCanvasGroup.CGDisable();
            choiceParentMemoriCanvasGroup.CGDisable();
            eventPanelCanvasGroup.FadeOutAsync();
            eventCamera.transform.rotation = originalRotation;
            eventCamera.fieldOfView = originalFOV;
            eventRewardsDisplay.gameObject.SetActive(false);

            if (_eventSceneInstance != null)
            {
                Destroy(_eventSceneInstance);
                _eventSceneInstance = null;
                AddressablesManager.Instance.Release(_eventScenePrefab.AssetGUID);
            }
        }
        public void CompleteEvent()
        {
            mapSceneUIManager.TryDrainPendingPrestigeChoices(() => mapSceneUIManager.CompleteLayerAction());
        }
        public TT_Event GetRandomEvent()
        {
            TT_Event ttEvent = gc_Events[campaignSaveManager.SaveData.eventOrdering[0]];
            campaignSaveManager.SaveData.eventOrdering.RemoveAt(0);
            return ttEvent;
        }
        public void HideActionButton()
        {
            acceptRollButton.gameObject.SetActive(false);
        }
        public void RevealActionButton()
        {
            acceptRollButton.gameObject.SetActive(true);
        }
        public EventReward GenerateReward(EventChoice _eventChoice, EventRollOutcome _eventRollOutcome)
        {
            EventOutcome source = _eventRollOutcome switch
            {
                EventRollOutcome.CriticalSuccess  => _eventChoice.criticalSuccessOutcome,
                EventRollOutcome.Success          => _eventChoice.successOutcome,
                EventRollOutcome.Failure          => _eventChoice.failureOutcome,
                EventRollOutcome.CriticalFailure  => _eventChoice.criticalFailureOutcome,
                _                                 => new EventOutcome(),
            };

            // Copy the modifiers list so AddRange (double-rewards) never mutates the original EventChoice data.
            return new EventReward
            {
                EventRewardTitle = _eventChoice.eventChoiceTitle,
                EventOutcome = new EventOutcome
                {
                    OutcomeDescription       = source.OutcomeDescription,
                    EventOutcomeModifiers    = new List<EventOutcomeModifier>(source.EventOutcomeModifiers ?? new()),
                },
            };
        }
        float cachedDiePosition;
        float bounceDelay = 0.1f;
        public async void FocusOnDice()
        {
            cachedDiePosition = physicsDieTransform.position.y;
            bounceDelay = 0.1f;
            float lookAtTime = lookAtPeriod;
            Vector3 startPos = eventCamera.transform.position;
            while (lookAtTime > 0)
            {
                lookAtTime -= Time.deltaTime;

                if (_overheadCameraTransform != null)
                {
                    float t = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(lookAtTime / lookAtPeriod));
                    eventCamera.transform.position = Vector3.Lerp(startPos, _overheadCameraTransform.position, t);
                }

                Quaternion lookAtRotation = Quaternion.LookRotation(physicsDieTransform.position - eventCamera.transform.position);
                eventCamera.transform.rotation = Quaternion.Slerp(eventCamera.transform.rotation, lookAtRotation, 0.2f);
                eventCamera.fieldOfView = Mathf.Lerp(eventCamera.fieldOfView, focusFOV, 0.1f);

                // Debug.Log($"physicsDieTransform.position.y: {physicsDieTransform.position.y}");
                if (cachedDiePosition < physicsDieTransform.position.y && bounceDelay <= 0)
                {
                    IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);
                    bounceDelay = 2f;
                    // Debug.Log($"Bounce");
                }
                bounceDelay -= Time.deltaTime;
                cachedDiePosition = physicsDieTransform.position.y;

                await Task.Yield();
            }
        }
        // public void ActivateStartLitGuidance()
        // {
        //     if (startLitGuidanceActive) return;

        //     if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(5))
        //     {
        //         string errorLocalized = LocalizationManager.Instance.GetText("You do not have enough gold to make this choice.");
        //         NotificationManager.Instance.ErrorNotification(errorLocalized);
        //         return;
        //     }

        //     startLitGuidanceActive = true;
        //     System.Random random = campaignSaveManager.GetCampaignRandom();
        //     int roll = random.Next(1, 21);
        //     startLitGuidanceOutcomeText.text = roll.ToString();
        //     CampaignManager.Instance.GoldManager.ModifyGold(-5);
        //     IAudioRequester.Instance.PlaySFX(SFXData.Purchase);
        // }
        public void ActivateClaimedByDestiny()
        {
            if (claimedByDestinyActive) return;

            claimedByDestinyActive = true;
            CampaignManager.Instance.CampaignSaveManager.IncrementRerollCount();
            RollDice();
            claimedByDestiny.SetActive(false);
        }
}
}