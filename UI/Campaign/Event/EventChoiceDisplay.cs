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
    EventPanel eventPanel;
    [SerializeField] private MemoriTooltipTrigger memoriTooltipTrigger;
    [SerializeField] private Animator animator;
    [SerializeField] private Button button;
    [SerializeField] private GameObject repRequirementGO, goldRequirementGO;

    bool selected, disabled;
    CanvasGroup canvasGroup;
    int actionCost, index;

    static string ColorString (EventOutcomeModifier _eventOutcome) {
            return _eventOutcome.Value > 0 ? ColorData.Positive : _eventOutcome.Value < 0 ? ColorData.Negative : ColorData.Secondary;
        }
    // A gain or loss reads from the sign, not only the colour.
    static string Sign (EventOutcomeModifier _eventOutcome) {
            return _eventOutcome.Value > 0 ? "+" : _eventOutcome.Value < 0 ? "-" : "";
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
        eventChoiceRoll.text = $"{eventChoice.minimumRollNeeded}+</color> ";

        eventChoiceTitleText.text += eventChoiceTitleLocalized;

        string description = $"{eventChoiceDescLocalized}\n\n{successLocalized}: ";
        string successDescription = "";
        int i = 0;
        foreach (EventOutcomeModifier eventOutcomeModifier in eventChoice.successOutcome.EventOutcomeModifiers)
        {
            string localizedEnum = LocalizationManager.Instance.GetText(eventOutcomeModifier.EventOutcomeModifierEnum.ToString());
            successDescription += $"<color={ColorString(eventOutcomeModifier)}>{Sign(eventOutcomeModifier)}{localizedEnum}</color>";
            if(i < eventChoice.successOutcome.EventOutcomeModifiers.Count - 1) {
                successDescription += ", ";
            }
            i++;
        }
        description += $"{successDescription}\n{failureLocalized}: ";
        eventChoiceOutcome.text += successDescription;

        string failureDescription = "";
        i = 0;
        foreach (EventOutcomeModifier eventOutcomeModifier in eventChoice.failureOutcome.EventOutcomeModifiers)
        {
            string localizedEnum = LocalizationManager.Instance.GetText(eventOutcomeModifier.EventOutcomeModifierEnum.ToString());
            failureDescription += $"<color={ColorString(eventOutcomeModifier)}>{Sign(eventOutcomeModifier)}{localizedEnum}</color>";
            if(i < eventChoice.failureOutcome.EventOutcomeModifiers.Count - 1) {
                failureDescription += ", ";
            }
            i++;
        }
        description += $"{failureDescription}";
        eventChoiceOutcome.text += $" / {failureDescription}";

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

        if(actionCost > 0) {
            if(CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount < actionCost) {
                string errorLocalized = LocalizationManager.Instance.GetText("You do not have enough gold to make this choice.");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            } else {
                string localizedString = LocalizationManager.Instance.GetText("Event");
                CampaignManager.Instance.GoldManager.ModifyGold(-actionCost, localizedString);
            }
        }

        selected = true;
        eventPanel.ChoiceSelected(eventChoice, index);
        TooltipManager.Instance.HideTooltip();
        animator.SetBool("Selected", true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
}