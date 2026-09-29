using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Memori.Audio;
using Memori.UI;
using Memori.Tooltip;
using Memori.Notifications;
using Memori.Utilities;
using Memori.Localization;

namespace TJ.Event
{
[RequireComponent(typeof(MemoriTooltipTrigger), typeof(CanvasGroup))]
public class EventChoiceDisplay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private TMP_Text eventChoiceTitleText, eventChoiceRoll, eventChoiceOutcome, requirementText;
    EventChoice eventChoice;
    public EventChoice EventChoice => eventChoice;
    public int Index => index;
    EventPanel eventPanel;
    [SerializeField] private MemoriTooltipTrigger memoriTooltipTrigger;
    [SerializeField] private Animator animator;
    [SerializeField] private Button button;
    [SerializeField] private GameObject repRequirementGO, goldRequirementGO;

    bool selected, disabled;
    CanvasGroup canvasGroup;
    int actionCost, index;

    // Losses and a stronger enemy hurt the player whatever the sign of Value.
    static int Effect (EventOutcomeModifier _eventOutcome) {
            switch (_eventOutcome.EventOutcomeModifierEnum) {
                case EventOutcomeModifierEnum.LoseGear:
                case EventOutcomeModifierEnum.LoseSquad:
                case EventOutcomeModifierEnum.LosePrestige:
                    return -1;
                case EventOutcomeModifierEnum.NextBattleEnemyLeadership:
                    return -System.Math.Sign(_eventOutcome.Value);
                case EventOutcomeModifierEnum.RevealMap:
                case EventOutcomeModifierEnum.NextBattleFixture:
                    return 1;
            }
            return System.Math.Sign(_eventOutcome.Value);
        }
    static string ColorString (EventOutcomeModifier _eventOutcome) {
            int effect = Effect(_eventOutcome);
            return effect > 0 ? ColorData.Positive : effect < 0 ? ColorData.Negative : ColorData.Secondary;
        }
    // A gain or loss reads from the sign, not only the colour.
    static string Sign (EventOutcomeModifier _eventOutcome) {
            if (_eventOutcome.EventOutcomeModifierEnum is EventOutcomeModifierEnum.LoseGear or EventOutcomeModifierEnum.LoseSquad or EventOutcomeModifierEnum.LosePrestige) return "";
            // The sign says which way the enemy moves; the colour says whether that helps you.
            if (_eventOutcome.EventOutcomeModifierEnum == EventOutcomeModifierEnum.NextBattleEnemyLeadership)
                return _eventOutcome.Value > 0 ? "+" : _eventOutcome.Value < 0 ? "-" : "";
            int effect = Effect(_eventOutcome);
            return effect > 0 ? "+" : effect < 0 ? "-" : "";
        }
    static string Describe (System.Collections.Generic.List<EventOutcomeModifier> _modifiers) {
            if (_modifiers == null) return "";
            System.Collections.Generic.List<string> parts = new();
            foreach (EventOutcomeModifier modifier in _modifiers)
            {
                string name = modifier.EventOutcomeModifierEnum == EventOutcomeModifierEnum.NextBattleFixture
                    ? $"{LocalizationManager.Instance.GetText(modifier.Fixture.ToString())} ({LocalizationManager.Instance.GetText("NextBattleFixture")})"
                    : LocalizationManager.Instance.GetText(modifier.EventOutcomeModifierEnum.ToString());
                parts.Add($"<color={ColorString(modifier)}>{Sign(modifier)}{name}</color>");
            }
            return string.Join(", ", parts);
        }
    static string Requirements (EventChoice _choice) {
            System.Collections.Generic.List<string> names = new();
            if (_choice.RequiredRaces != null) foreach (Race race in _choice.RequiredRaces) names.Add(LocalizationManager.Instance.GetText(race.ToString()));
            if (_choice.RequiredHeroes != null) foreach (int hero in _choice.RequiredHeroes) names.Add(LocalizationManager.Instance.GetText(HeroData.GetHeroByID(hero).HeroName));
            if (_choice.RequiredUnitTypes != null) foreach (UnitType type in _choice.RequiredUnitTypes) names.Add(LocalizationManager.Instance.GetText(type.ToString()));
            if (_choice.RequiredGear != null) foreach (GearID gear in _choice.RequiredGear) names.Add(LocalizationManager.Instance.GetText($"{gear}Name"));
            return names.Count == 0 ? "" : $"{LocalizationManager.Instance.GetText("EventRequires")}: {string.Join(", ", names)}";
        }

    public void LoadEventChoice(EventChoice _eventChoice, EventPanel _eventPanel, string _eventTableIndex)
    {
        canvasGroup = GetComponent<CanvasGroup>();
        eventChoice = _eventChoice;
        eventPanel = _eventPanel;

        string eventChoiceTitleLocalized = LocalizationManager.Instance.GetEventString(_eventTableIndex+"Title");
        string eventChoiceDescLocalized = LocalizationManager.Instance.GetEventString(_eventTableIndex+"Desc");
        string successLocalized = LocalizationManager.Instance.GetText("Success");
        string failureLocalized = LocalizationManager.Instance.GetText("Failure");
        string armySizeRequirementLocalized = LocalizationManager.Instance.GetText("Army Size required");
        string costLocalized = LocalizationManager.Instance.GetText("Cost");

        //get last 1 character of the eventTableIndex to get the index of the event choice
        index = int.Parse(_eventTableIndex[^1..]);

        eventChoiceTitleText.text = "";
        eventChoiceRoll.text = "";
        eventChoiceOutcome.text = "";

        actionCost = 0;
        bool rolls = eventChoice.Kind == EventChoiceKind.Roll;
        eventChoiceRoll.text = rolls
            ? $"{eventChoice.minimumRollNeeded}+</color> "
            : LocalizationManager.Instance.GetText("EventKind" + eventChoice.Kind);

        eventChoiceTitleText.text += eventChoiceTitleLocalized;

        string successDescription = Describe(eventChoice.successOutcome.EventOutcomeModifiers);
        string description;
        if (rolls)
        {
            string failureDescription = Describe(eventChoice.failureOutcome.EventOutcomeModifiers);
            description = $"{eventChoiceDescLocalized}\n\n{successLocalized}: {successDescription}\n{failureLocalized}: {failureDescription}";
            eventChoiceOutcome.text += $"{successDescription} / {failureDescription}";
        }
        else
        {
            description = $"{eventChoiceDescLocalized}\n\n{LocalizationManager.Instance.GetText("EventOutcome")}: {successDescription}";
            eventChoiceOutcome.text += successDescription;
        }
        if (eventChoice.Cost != null && eventChoice.Cost.Count > 0)
            description += $"\n{costLocalized}: {Describe(eventChoice.Cost)}";

        string requirementTextAmount = "";
        requirementText.text = "";

        if(eventChoice.ArmySizeRequired > 0) {
            repRequirementGO.SetActive(true);
            string colorString = CampaignManager.Instance.CampaignSaveManager.GetArmySize() >= eventChoice.ArmySizeRequired ? ColorData.Positive : ColorData.Negative;
            requirementText.text = $"{eventChoice.ArmySizeRequired}";
            requirementTextAmount = $"{armySizeRequirementLocalized}: {eventChoice.ArmySizeRequired}";
        } else {
            repRequirementGO.SetActive(false);
        }
        
        if(eventChoice.GoldRequired > 0) {
            actionCost = eventChoice.GoldRequired;
            // if(CampaignManager.Instance.GearManager.CheckForGear(GearID.QuantitativeEasingPolicy)) {
            //     actionCost = 0;
            // }
            goldRequirementGO.SetActive(true);
            string colorString = CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount >= actionCost ? ColorData.Positive : ColorData.Negative;
            requirementText.text = $"{actionCost}";
            requirementTextAmount = $"{costLocalized}: {actionCost}<sprite name=GoldSprite>";
        } else {
            goldRequirementGO.SetActive(false);
        }

        string requires = Requirements(eventChoice);
        if (requires != "")
            requirementTextAmount = requirementTextAmount == "" ? requires : $"{requires}\n{requirementTextAmount}";

        memoriTooltipTrigger.SetUpToolTip(
            _title: requirementTextAmount,
            _description:description, 
            _delay: 0.25f);

        animator.SetBool("Normal", true);
        selected = false;
        disabled = false;
        button.enabled = true;
        memoriTooltipTrigger.enabled = true;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        StartCoroutine(canvasGroup.CGFadeIn(0.2f));
        IAudioRequester.Instance.PlaySFX(SFXData.EventOptionLoad);
    }
    public void Disable()
    {
        disabled = true;
        button.enabled = false;
        if(selected) return;

        animator.SetBool("Disabled", true);
        memoriTooltipTrigger.enabled = false;
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        if(disabled) return;
        IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        MemoriUI.BloomItemScale(transform, 1.025f, 0.1f);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        MemoriUI.BloomItemScale(transform, 1f, 0.1f);
    }
    public void OnPointerClick(PointerEventData eventData)
    {
        if(disabled) return;

        if(eventChoice.ArmySizeRequired > 0) {
            if(CampaignManager.Instance.CampaignSaveManager.GetArmySize() < eventChoice.ArmySizeRequired) {
                string errorLocalized = LocalizationManager.Instance.GetText("You do not have a large enough force to make this choice.");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }
        } 

        if(actionCost > 0 && CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount < actionCost) {
            string errorLocalized = LocalizationManager.Instance.GetText("You do not have enough gold to make this choice.");
            NotificationManager.Instance.ErrorNotification(errorLocalized);
            return;
        }
        if(!CampaignManager.Instance.CampaignSaveManager.CanPayEventCost(eventChoice.Cost)) {
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("EventCannotPay"));
            return;
        }
        if(actionCost > 0) {
            string localizedString = LocalizationManager.Instance.GetText("Event");
            CampaignManager.Instance.GoldManager.ModifyGold(-actionCost, localizedString);
        }
        CampaignManager.Instance.CampaignSaveManager.PayEventCost(eventChoice.Cost);

        selected = true;
        eventPanel.ChoiceSelected(eventChoice, index);
        TooltipManager.Instance.HideTooltip();
        animator.SetBool("Selected", true);
    }
    // Reopens a choice whose roll was locked before a quit; its cost is already in the locked save, so nothing is paid again.
    public void ResumeSelection()
    {
        selected = true;
        eventPanel.ChoiceSelected(eventChoice, index);
        animator.SetBool("Selected", true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
}