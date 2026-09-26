using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using Memori.Audio;
using Memori.Notifications;
using TJ.Map;
using Memori.Localization;
using Memori.Utilities;

namespace TJ.Event
{
[System.Serializable] public class ReputationModificationSlider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text sliderValueText;
    [HideInInspector] public Animator sliderAnimator;
    EventPanel eventPanel;
    Color defaultColor;

    void OnEnable()
    {
        if (sliderAnimator == null)
            sliderAnimator = gameObject.GetComponent<Animator>();

        defaultColor = sliderValueText.color;
    }
    public void LoadReputationSlider(int _min, EventPanel _eventPanel) 
    {
        slider.onValueChanged.RemoveAllListeners();
        eventPanel = _eventPanel;
        slider.minValue = _min;
        slider.value = 0;
        sliderValueText.text = "+0";
        slider.onValueChanged.AddListener(SetSliderValue);
    }

    public void SetSliderValue(float value)
    {
        int modificationAmount = (int)value - (int)slider.minValue;
        sliderValueText.text = "+"+modificationAmount.ToString();
        if(CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount < modificationAmount){
            string localizedError = LocalizationManager.Instance.GetText("InsufficientGoldError");
            NotificationManager.Instance.ErrorNotification(localizedError);
            sliderValueText.color = ColorVision.Bad(Color.red);
            return;
        } else {
            sliderValueText.color = defaultColor;
        }
        if(CampaignManager.Instance.GearManager.CheckForGear(GearID.TowerShields)) modificationAmount *= 2;
        eventPanel.ModifyRoll(modificationAmount);
        TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ModifyRoll);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        if (!sliderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hover"))
            sliderAnimator.Play("Hover");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!sliderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal"))
            sliderAnimator.Play("Normal");
    }

}
}
