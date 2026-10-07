using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.SaveData;
using Memori.Audio;
using TJ.Settings;
using System;
using Memori.Tooltip;
using System.Collections;
using Memori.UI;
using Unity.Mathematics;
using MoreMountains.Feedbacks;
using Memori.Input;
using Memori.Localization;
using Memori.Steamworks;

namespace TJ.Map
{
    public class MapIntroDisplay : MonoBehaviour
    {
        [SerializeField] private GameObject bookIntroTitle;
        [SerializeField] private TMP_Text[] titleLine1, titleLine2, titleLine3;
        [SerializeField] private MemoriCanvasGroup titleLine3CanvasGroup;
        Coroutine displayFactionCoroutine;

        public void DisplayTitle(RaceData raceData, int bookNumber)
        {
            titleLine3CanvasGroup.CGDisable();
            string actNumeral = MemoriUI.ConvertNumberToRomanNumeral(bookNumber);
            string actLocalized = LocalizationManager.Instance.GetText("Act");
            string raceLocalized = LocalizationManager.Instance.GetText(raceData.Race.ToString()+"MapRegion");

            for (int i = 0; i < titleLine1.Length; i++)
            {
                titleLine1[i].text = $"{actLocalized}";
            }
            for (int i = 0; i < titleLine2.Length; i++)
            {
                titleLine2[i].text = $"{actNumeral}";
            }
            for (int i = 0; i < titleLine3.Length; i++)
            {
                titleLine3[i].text = $"{raceLocalized}";
            }
            bookIntroTitle.SetActive(true);
            if (displayFactionCoroutine != null)
                StopCoroutine(displayFactionCoroutine);

            displayFactionCoroutine = StartCoroutine(DisplayFactionAfterDelay(1f));
        }
        /// <summary>The March has no single enemy to name, so the third line names the mode instead of a region.</summary>
        public void DisplayMarchTitle(int bookNumber)
        {
            titleLine3CanvasGroup.CGDisable();
            string actLocalized = LocalizationManager.Instance.GetText("Act");
            string actNumeral = MemoriUI.ConvertNumberToRomanNumeral(bookNumber);
            string line = LocalizationManager.Instance.GetText("marchIntroEndless");

            for (int i = 0; i < titleLine1.Length; i++) titleLine1[i].text = actLocalized;
            for (int i = 0; i < titleLine2.Length; i++) titleLine2[i].text = actNumeral;
            for (int i = 0; i < titleLine3.Length; i++) titleLine3[i].text = line;
            bookIntroTitle.SetActive(true);
            if (displayFactionCoroutine != null)
                StopCoroutine(displayFactionCoroutine);

            displayFactionCoroutine = StartCoroutine(DisplayFactionAfterDelay(1f));
        }
        public void HideTitle()
        {
            bookIntroTitle.SetActive(false);
            if (displayFactionCoroutine != null)
                StopCoroutine(displayFactionCoroutine);

            titleLine3CanvasGroup.CGDisable();
        }
        public IEnumerator DisplayFactionAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            titleLine3CanvasGroup.FadeInAsync(1f);
        }
    }
}