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

        [Header("Startlit Guidance")]
        [SerializeField] private GameObject startLitGuidance;
        [SerializeField] private TMP_Text startLitGuidanceOutcomeText, starlitGuidanceCostText;
        [SerializeField] private MemoriTooltipTrigger startLitGuidanceTooltip;
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

        EventDefinitionSO[] gc_Events;
        EventDefinitionSO gc_Event;

        MemoriCanvasGroup eventPanelCanvasGroup;
        List<EventChoiceDisplay> eventChoices = new List<EventChoiceDisplay>();
        MemoriCanvasGroup descMemoriCanvasGroup, choiceParentMemoriCanvasGroup;
        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;
        EventChoice selectedChoice;
        [SerializeField] bool rollAccepted = false;
        int rollBonus = 0;
        int roll = 0;
        int selectedChoiceIndex;
        // For the node log: dice thrown this event (a reroll adds one) and whether a Fateshine Elixir set the roll.
        int rollsThisEvent;
        bool fateshineUsed;
        // A d20 locked before a quit; RollDice shows it instead of drawing a new one.
        int resumeRoll = 0;
        bool eventRolled = false;
        public bool CanReroll => !rollAccepted && eventRolled;
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
        /// <summary>Dev Tools: the next Event node shows this event instead of drawing one. Read once, then cleared.</summary>
        public static string DevForcedEventKey;
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

            NodeResume resume = campaignSaveManager.SaveData.nodeResume;
            EventDefinitionSO resumeEvent = resume.active && resume.nodeType == NodeType.Event
                ? System.Array.Find(gc_Events, x => x.TableKey == resume.eventKey)
                : null;
            EventDefinitionSO forcedEvent = string.IsNullOrEmpty(DevForcedEventKey) ? null : System.Array.Find(gc_Events, x => x.TableKey == DevForcedEventKey);
            DevForcedEventKey = null;
            gc_Event = resumeEvent != null ? resumeEvent : forcedEvent != null ? forcedEvent : GetRandomEvent();
            rollsThisEvent = 0;
            fateshineUsed = false;
            string chapterLocalized = LocalizationManager.Instance.GetText("Chapter");
            chapterNumberText.text = $"{chapterLocalized} {MemoriUI.ConvertNumberToRomanNumeral(_chapter + 1)}";

            //localization
            string eventTitleLocalized = LocalizationManager.Instance.GetEventString(gc_Event.NameKey);
            string eventDescriptionLocalized = LocalizationManager.Instance.GetEventString(gc_Event.DescriptionKey);
            eventNameText.text = eventTitleLocalized;
            eventDescriptionText.text = eventDescriptionLocalized;

            eventPanelCanvasGroup.interactable = true;
            eventPanelCanvasGroup.blocksRaycasts = true;
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
            for (int i = 0; i < gc_Event.Choices.Length; i++)
            {
                EventChoice choice = gc_Event.Choices[i];
                if (!campaignSaveManager.MeetsEventRequirements(choice)) continue;
                EventChoiceDisplay eventChoise = Instantiate(eventChoicePrefab, eventChoicesParent);
                if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_eventReducedRollRequirementsMetaprogressionModel)) {
                    choice.minimumRollNeeded = math.max(1, choice.minimumRollNeeded - _eventReducedRollRequirementsMetaprogressionModel.NodeValue);
                }
                eventChoise.LoadEventChoice(choice, this, gc_Event.ChoiceKey(i));
                eventChoices.Add(eventChoise);
                await Task.Delay(100);
            }

            if (resumeEvent != null)
            {
                EventChoiceDisplay lockedChoice = eventChoices.Find(x => x.Index == resume.choiceIndex);
                if (lockedChoice != null)
                {
                    resumeRoll = resume.roll;
                    lockedChoice.ResumeSelection();
                }
                else Debug.LogError($"[Event] Locked choice {resume.choiceIndex} of {resume.eventKey} is not on the panel");
            }
        }
        public async void ChoiceSelected(EventChoice _eventChoice, int _index)
        {
            selectedChoice = _eventChoice;
            selectedChoiceIndex = _index;
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            // descriptionObject.SetActive(false);
            eventChoices.ForEach(x => x.Disable());
            startLitGuidance.SetActive(false);

            if (_eventChoice.Kind == EventChoiceKind.Roll)
            {
                RollDice();
                uiBackgroundCanvasGroup.FadeOutAsync(0.25f);
            }
            else ResolveWithoutRoll();

            while (!rollAccepted)
            {
                // A quit to the menu unloads the panel before the roll is accepted; stop instead of waiting forever.
                if (this == null) return;
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

            string eventOutcomeDescription = LocalizationManager.Instance.GetEventString(gc_Event.OutcomeKey(_index, eventRollOutcome));
            outcomeDescriptionText.text = eventOutcomeDescription;

            //Critical Success, Success, Failure, Critical Failure
            string eventRollOutcomeLocalized = LocalizationManager.Instance.GetText(eventRollOutcome.ToString());
            eventRewardsDisplay.LoadTitle(eventRollOutcomeLocalized);

            eventRewardsDisplay.gameObject.SetActive(true);

            await Task.Delay(500);
            campaignSaveManager.RecordEventOutcome(EventData.HistoryEntry(gc_Event, _index, eventRollOutcome));
            TabletopTavern.Analytics.NodeLog.Try("event", () => LogEventOutcome(_index, eventReward));
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
            RollOutcomeText.text = "";
            eventDescriptionText.text = "";

            rollBonus = 0;
            rollsThisEvent++;
            if (resumeRoll > 0)
            {
                roll = resumeRoll;
                resumeRoll = 0;
            }
            else
            {
                System.Random random = campaignSaveManager.GetCampaignRandom();
                roll = random.Next(1, 21);
                if (campaignSaveManager.FateshineElixirArmed)
                {
                    fateshineUsed = true;
                    roll = 20;
                    campaignSaveManager.ConsumeFateshineElixir();
                }
            }
            // Locked before the die is shown, so a quit to the menu cannot replay the event knowing this roll.
            campaignSaveManager.LockNodeResult(new NodeResume
            {
                nodeIndex   = mapSceneUIManager.LayerNodeSelected,
                nodeType    = NodeType.Event,
                eventKey    = gc_Event.TableKey,
                choiceIndex = selectedChoiceIndex,
                roll        = roll,
            });

            //old
            // if (CampaignManager.Instance.GearManager.CheckForGear(GearID.Shungite)) roll = math.clamp(roll, 2, 20);

            FocusOnDice();
            outcomeDescriptionText.enabled = false;

            physicsDie.gameObject.SetActive(true);
            diceRollAnimator.gameObject.SetActive(false);
            diceRollAnimator.transform.localPosition = Vector3.zero;
            physicsDie.ResetDie();

            await Task.Delay(1000);
            if (this == null) return;

            physicsDie.gameObject.SetActive(false);
            diceRollAnimator.gameObject.SetActive(true);
            diceRollAnimator.Play("Dice_" + roll);

            await Task.Delay(500);
            if (this == null) return;

            rollTextObject.SetActive(true);
            rollResultText.text = roll.ToString();

            await Task.Delay(500);
            if (this == null) return;

            if (roll == 20) SteamAchievements.Unlock(AchievementId.Roll20);
            if (roll == 1) SteamAchievements.Unlock(AchievementId.SnakeEyes);

            rerollButton.gameObject.SetActive(true);
            acceptRollButton.gameObject.SetActive(true);
            string acceptLocalized = LocalizationManager.Instance.GetText("Accept");
            continueButtonText.text = acceptLocalized;

            //DifficultyMod 8
            bool rerollLocked = DifficultyRules.EventRollsLocked(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel);
            string difficultyLocalized = LocalizationManager.Instance.GetText("Difficulty");
            string difficultyLevelLocalized = LocalizationManager.Instance.GetText(DifficultyData.GetDifficultyLevelData(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel).difficultyName);
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
        // Pay, Sacrifice and Walk away skip the die and take the success outcome.
        private void ResolveWithoutRoll()
        {
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.EventExplanation);
            rollBonus = 0;
            eventRollOutcome = EventRollOutcome.Success;
            eventDescriptionText.text = "";
            acceptRollButton.gameObject.SetActive(true);
            AcceptRoll();
        }
        private void ShowRowResult()
        {
            int modifiedRoll = roll + rollBonus;
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

            rollResultText.text = modifiedRoll.ToString();
            rollResultText.color = modifiedRoll >= selectedChoice.minimumRollNeeded ? ColorVision.Good(Color.green) : ColorVision.Bad(Color.red);
            RollOutcomeText.text = LocalizationManager.Instance.GetText(eventRollOutcome.ToString());
            RollOutcomeText.color = rollResultText.color;
        }

        private TMP_Text _rollOutcomeText;

        // The outcome word under the roll keeps pass and fail readable without colour; cloned so it shares the roll's font and outline.
        private TMP_Text RollOutcomeText
        {
            get
            {
                if (_rollOutcomeText != null) return _rollOutcomeText;
                _rollOutcomeText = Instantiate(rollResultText, rollResultText.transform.parent);
                _rollOutcomeText.name = "Roll Outcome Text";
                _rollOutcomeText.enableAutoSizing = false;
                _rollOutcomeText.fontSize = rollResultText.fontSize * 0.45f;
                _rollOutcomeText.alignment = TextAlignmentOptions.Center;
                _rollOutcomeText.textWrappingMode = TextWrappingModes.NoWrap;
                RectTransform source = rollResultText.rectTransform;
                RectTransform rt = _rollOutcomeText.rectTransform;
                rt.sizeDelta = new Vector2(source.rect.width * 3f, source.rect.height * 0.4f);
                rt.anchoredPosition = source.anchoredPosition + new Vector2(0f, -source.rect.height * 0.85f);
                _rollOutcomeText.text = "";
                return _rollOutcomeText;
            }
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
        // The choice, the roll against what it needed, and the reward, so each event's odds and payouts can be read.
        private void LogEventOutcome(int _index, EventReward _reward)
        {
            bool rolled = selectedChoice.Kind == EventChoiceKind.Roll;
            var rewards = new List<Dictionary<string, object>>();
            List<EventOutcomeModifier> modifiers = _reward.EventOutcome.EventOutcomeModifiers;
            if (modifiers != null)
                foreach (EventOutcomeModifier modifier in modifiers)
                    rewards.Add(new Dictionary<string, object> { { "k", modifier.EventOutcomeModifierEnum.ToString() }, { "v", (double)modifier.Value } });
            TabletopTavern.Analytics.NodeLog.Set("event", new Dictionary<string, object>
            {
                { "key", gc_Event.TableKey },
                { "choice", _index },
                { "kind", selectedChoice.Kind.ToString() },
                { "needed", rolled ? (object)selectedChoice.minimumRollNeeded : null },
                { "roll", rolled ? (object)roll : null },
                { "bonus", rolled ? (object)rollBonus : null },
                { "rolls", rollsThisEvent },
                { "fateshine", fateshineUsed },
                { "outcome", eventRollOutcome.ToString() },
                { "rewards", rewards },
            });
        }
        public void CompleteEvent()
        {
            mapSceneUIManager.TryDrainPendingPrestigeChoices(() => mapSceneUIManager.CompleteLayerAction());
        }
        public EventDefinitionSO GetRandomEvent()
        {
            CampaignSaveData save = campaignSaveManager.SaveData;
            MigrateLegacyEventOrdering(save);
            return EventData.PickEvent(gc_Events, campaignSaveManager.GetEventDrawContext(), save.seenEvents, campaignSaveManager.GetCampaignRandom());
        }
        // Saves from before the registry list the first ten registry positions still to come; the rest of those ten were seen.
        private void MigrateLegacyEventOrdering(CampaignSaveData _save)
        {
            const int LegacyEventCount = 10;
            if (_save.eventOrdering == null || _save.eventOrdering.Count == 0) return;
            for (int i = 0; i < LegacyEventCount && i < gc_Events.Length; i++)
                if (!_save.eventOrdering.Contains(i) && !_save.seenEvents.Contains(gc_Events[i].TableKey))
                    _save.seenEvents.Add(gc_Events[i].TableKey);
            _save.eventOrdering.Clear();
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
                EventOutcome = new EventOutcome
                {
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
                if (this == null) return;
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