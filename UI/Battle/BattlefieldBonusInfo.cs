using TMPro;
using UnityEditor;
using UnityEngine;
using Memori.UI;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.Localization;
using System.Collections;

namespace TJ
{
    [RequireComponent(typeof(MemoriCanvasGroup))]
    public class BattlefieldBonusInfo : MonoBehaviour
    {
        [SerializeField] private TMP_Text bonusNameText, bonusDescriptionText;
        MemoriCanvasGroup squadHoverPopup;
        Coroutine displayCoroutine;
        bool shown;

        private void Awake()
        {
            squadHoverPopup = GetComponent<MemoriCanvasGroup>();
        }
        public void Unhover()
        {
            if(displayCoroutine != null) StopCoroutine(displayCoroutine);
            squadHoverPopup.CGDisable();
        }
        public void Hover()
        {
            if(displayCoroutine != null) StopCoroutine(displayCoroutine);

            displayCoroutine = StartCoroutine(OutlineGlow());
        }
        public IEnumerator OutlineGlow()
        {
            // Turn on in 0.25 seconds
            float elapsedTime = 0f;
            float turnOnDuration = 0.25f;

            while (elapsedTime < turnOnDuration)
            {
                float t = elapsedTime / turnOnDuration;

                //fade in alpha of canvas group
                squadHoverPopup.alpha = t;

                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure final values are set
            squadHoverPopup.alpha = 1;
            

            // Pause for 3 seconds
            yield return new WaitForSecondsRealtime(3f);

            // Turn off in 0.5 seconds
            elapsedTime = 0f;
            float turnOffDuration = 0.5f;

            while (elapsedTime < turnOffDuration)
            {
                float t = elapsedTime / turnOffDuration;

                squadHoverPopup.alpha = 1 - t;
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            // Ensure final values are set
            squadHoverPopup.alpha = 0;
        }
        public void Load(BattlefieldBonus _battlefieldBonus)
        {
            string localizedBonusName = LocalizationManager.Instance.GetText(_battlefieldBonus.BattlefieldBonusEnum.ToString());
            bonusNameText.text = localizedBonusName;

            //add special condition for forest bonuses
            if (_battlefieldBonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Forest)
            {
                bonusNameText.text = LocalizationManager.Instance.GetText("Forest");
                bonusDescriptionText.text = ColorData.ApplyColorVision(LocalizationManager.Instance.GetText("InForestDesc"));
            }
            else if (_battlefieldBonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Swamp)
            {
                bonusNameText.text = LocalizationManager.Instance.GetText("Swamp");
                bonusDescriptionText.text = ColorData.ApplyColorVision(LocalizationManager.Instance.GetText("InSwampDesc"));
            }
            else
            {
                string statLocalized = LocalizationManager.Instance.GetText(_battlefieldBonus.UnitStat.ToString());
                string teamLocalized = LocalizationManager.Instance.GetText(_battlefieldBonus.Team.ToString());
                string sign = _battlefieldBonus.Value > 0 ? "+" : "-";
                string coloredStat = $"<color {(_battlefieldBonus.Value > 0 ? ColorData.Green : ColorData.Error)}>{sign}{_battlefieldBonus.Value} [{statLocalized}]</color>";
                bonusDescriptionText.text = $"{coloredStat} {string.Format(LocalizationManager.Instance.GetText("battlefieldBonusFor"), teamLocalized)}";
            }
        }
    }
}