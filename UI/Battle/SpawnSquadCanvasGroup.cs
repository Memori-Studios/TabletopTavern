using Memori.Utilities;
using TMPro;
using UnityEngine;
using Memori.Localization;

namespace TJ
{
[RequireComponent(typeof(MemoriCanvasGroup))]
public class SpawnSquadCanvasGroup : MonoBehaviour
{
    [SerializeField] private bool showOnStart;
    [SerializeField] private TMP_Dropdown prestigeDropdown;
    [SerializeField] private TMP_Dropdown weatherDropdown;

    private void Start() 
    {
        prestigeDropdown.options.Clear();
        weatherDropdown.options.Clear();
        string prestigeLocalized = LocalizationManager.Instance.GetText("Prestige");
        string clearSkiesLocalized = LocalizationManager.Instance.GetText("ClearSkies");
        string rainLocalized = LocalizationManager.Instance.GetText("Rain");
        prestigeDropdown.options.Add(new TMP_Dropdown.OptionData(prestigeLocalized + " I"));
        prestigeDropdown.options.Add(new TMP_Dropdown.OptionData(prestigeLocalized + " II"));
        prestigeDropdown.options.Add(new TMP_Dropdown.OptionData(prestigeLocalized + " III"));
        // The caption keeps the scene's saved "Tier I" text unless it is redrawn after the options change.
        prestigeDropdown.RefreshShownValue();
        weatherDropdown.options.Add(new TMP_Dropdown.OptionData(clearSkiesLocalized));
        weatherDropdown.options.Add(new TMP_Dropdown.OptionData(rainLocalized));

        #if !UNITY_EDITOR
            showOnStart = BattleManager.Instance.BattleSaveManager.IsCustomBattle;
        #endif

        if(showOnStart){
            GetComponent<MemoriCanvasGroup>().CGEnable();
        } else {
            GetComponent<MemoriCanvasGroup>().CGDisable();
        }
    }
}
}

