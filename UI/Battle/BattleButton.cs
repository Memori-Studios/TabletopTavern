using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using TMPro;
using System.Collections.Generic;
using Memori.Utilities;
using Unity.Mathematics;
using Memori.Scenes;
using Memori.Audio;
using Memori.Tooltip;
using Memori.Input;
using Memori.Localization;
using System;
using Memori.UI;

namespace TJ
{
    public class BattleButton : MonoBehaviour
    {
        [SerializeField] private MemoriButtonV2 battleButton;
        [SerializeField] private GameObject onGameObject, offGameObject;
        [SerializeField] private MemoriTooltipTrigger tooltipTrigger;
        private bool isOn = false;
        public bool IsOn => isOn;
        public void SetUp(string tooltipTitle, string tooltipDescription, Action<bool> onClickBoolToggleAction = null, Action onClickAction = null)
        {
            battleButton.Button.onClick.RemoveAllListeners();
            if (onClickBoolToggleAction != null)
            {
                battleButton.Button.onClick.AddListener(() =>
                {
                    isOn = !isOn;
                    onClickBoolToggleAction.Invoke(isOn);
                    onGameObject.SetActive(isOn);
                    offGameObject.SetActive(!isOn);
                    IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
                });
            }
            else if (onClickAction != null)
            {
                battleButton.Button.onClick.AddListener(() =>
                {
                    NonToggleAction(onClickAction);
                });
            }

            onGameObject.SetActive(false);
            offGameObject.SetActive(true);
                tooltipTrigger.SetUpToolTip(tooltipTitle, tooltipDescription);
        }
        /// <summary>Rewrites the words only; the click wiring stays.</summary>
        public void SetTooltip(string tooltipTitle, string tooltipDescription)
        {
            if (tooltipTrigger != null) tooltipTrigger.SetUpToolTip(tooltipTitle, tooltipDescription);
        }
        public void SetOnOrOff(bool _isOn)
        {
            isOn = _isOn;
            onGameObject.SetActive(isOn);
            offGameObject.SetActive(!isOn);
        }
        public void HotkeyInteract()
        {
            battleButton.Button.onClick.Invoke();
        }
        private void NonToggleAction(Action onClickAction)
        {
            if(isOn) return;

            onClickAction.Invoke();
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
        }
    }
}