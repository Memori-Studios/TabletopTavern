using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using TJ;
using Memori.Core;
// using Memori.Localization;

namespace Memori.Input
{
    [System.Serializable] public class MonitoredDataSlider : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Slider slider;
        [SerializeField] private string playerPref;
        [SerializeField] private TMP_Text sliderNameText, sliderValueText;
        private string prefName;
        // private float baseValue;

        [HideInInspector] public Animator sliderAnimator;

        // public bool setUp;
        MonitoredData<float> monitoredData;

        void OnEnable()
        {
            if (sliderAnimator == null)
                sliderAnimator = gameObject.GetComponent<Animator>();
        }
        void Start()
        {
            LoadSettingsSlider(playerPref);
        }
        public void LoadSettingsSlider(string _prefName) 
        {
            prefName = _prefName;
            float savedValue = PlayerPrefs.GetFloat(prefName, 0.5f);
            slider.value = savedValue;
            sliderValueText.text = Percent(savedValue);
            slider.onValueChanged.AddListener(SetSliderValue);

            // sliderNameText.text = LocalizationManager.Instance.GetText(prefName);
        }
        public void AssignMonitoredData(MonitoredData<float> data)
        {
            monitoredData = data;
            slider.onValueChanged.AddListener((value) => monitoredData.Value = value);
        }
        public void SetSliderValue(float value)
        {
            sliderValueText.text = Percent(value);
            PlayerPrefs.SetFloat(prefName, value);
            monitoredData.Value = value;
        }
        static string Percent(float value) => Mathf.RoundToInt(value * 100f) + "%";
        public void SetSliderValueWithoutNotify(float value)
        {
            slider.SetValueWithoutNotify(value);
            sliderValueText.text = Percent(value);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            // IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
            // The Settings row slider has no Animator; its row draws the hover.
            if (sliderAnimator == null) return;
            if (!sliderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hover"))
                sliderAnimator.Play("Hover");
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (sliderAnimator == null) return;
            if (!sliderAnimator.GetCurrentAnimatorStateInfo(0).IsName("Normal"))
                sliderAnimator.Play("Normal");
        }

    }
}
