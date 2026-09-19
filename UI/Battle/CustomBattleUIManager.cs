using UnityEngine;
using UnityEngine.UI;
using System;
using Unity.Collections;
using TMPro;
using System.Collections.Generic;
using Memori.Utilities;
using Memori.Scenes;
using Memori.Audio;
using Synty.Interface.FantasyMenus.Samples;
using Memori.UI;
using TJ.IrregularGrid;
using Memori.Steamworks;
using TJ.Map;
using System.Threading.Tasks;
using Memori.Localization;

namespace TJ
{
    public class CustomBattleUIManager : MonoBehaviour
    {
        [Header("Prefab I always need")]
        [SerializeField] private CustomBattleSquadSpawnCard unitUIPrefab;

        [Header("Deployment")]
        [SerializeField] private GameObject customBattlePanel;
        [SerializeField] private MemoriCanvasGroup spawnUnitsCanvasGroup, mapSettingsCanvasGroup;
        [SerializeField] private ToggleGroup deploymentToggleGroup;
        private Toggle lastActiveDeploymentToggle;

        [SerializeField] private Transform unitUIParent;
        [SerializeField] private TMP_InputField unitCountInputField;
        [SerializeField] private Button deleteAllSquadsButton;
        [SerializeField] private MemoriCanvasGroup spawnOptionsCanvasGroup, spawnControlsCanvasGroup;
        [SerializeField] private TMP_Dropdown prestigeDropdown, weatherDropdown, mapRegionDropdown, biomeDropdown;
        [SerializeField] private ToggleGroup factionToggleGroup;
        [SerializeField] private TMPSpawnScaler factionNameText;
        [SerializeField] private Race selectedRace;
        [SerializeField] private ToggleGroup raceToggleGroup;
        [SerializeField] private TMPSpawnScaler raceNameText;
        private Toggle lastActiveToggle, lastActiveFactionToggle;
        [SerializeField] private Image[] uiBackgrounds;
        [SerializeField] private Color playerColor, enemyColor;


        [Header("Battlefield Generation")]
        [SerializeField] private MemoriButtonV2 generateBattlefieldButton;
        [SerializeField] private Toggle useRandomSeedToggle;
        [SerializeField] private TMP_InputField seedInputField;
        [SerializeField] private Biome biome;
        [SerializeField] private Weather weather;
        [SerializeField] private MapRegion mapRegion;
        [SerializeField] private int seed;
        [SerializeField] private bool useRandomSeed;

        List<MapRegion> mapRegions;
        List<CustomBattleSquadSpawnCard> unitUIPrefabs = new();

        public void LoadUI(int _seed)
        {
            if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle && (SceneHandler.Instance.EditorOverride != SceneHandler.EditorOverrides.TavernBattle || SceneHandler.Instance.EditorLoadCampaignBattle))
            {
                customBattlePanel.SetActive(false);
                return;
            }
            customBattlePanel.SetActive(true);

            unitCountInputField.onValueChanged.RemoveAllListeners();

            UnitName[] ironLegion =TabletopTavernData.Instance.GetUnitsOfRace(Race.IronLegion);
            UnitName[] Gruntkin =TabletopTavernData.Instance.GetUnitsOfRace(Race.Gruntkin);
            UnitName[] ravenHost =TabletopTavernData.Instance.GetUnitsOfRace(Race.RavenHost);
            UnitName[] taelindor =TabletopTavernData.Instance.GetUnitsOfRace(Race.TaelindorForest);
            UnitName[] sanguineCourt =TabletopTavernData.Instance.GetUnitsOfRace(Race.SanguineCourt);
            UnitName[] sakuraDynasty = TabletopTavernData.Instance.GetUnitsOfRace(Race.SakuraDynasty);
            UnitName[] deepstoneHold = TabletopTavernData.Instance.GetUnitsOfRace(Race.DeepstoneHold);
            UnitName[] drakosaurBrood = TabletopTavernData.Instance.GetUnitsOfRace(Race.DrakosaurBrood);
            UnitName[] special = TabletopTavernData.Instance.GetUnitsOfRace(Race.Special);
            
            //remove gate from special
            List<UnitName> specialList = new(special);
            specialList.Remove(UnitName.Gate);
            special = specialList.ToArray();

            //combine the arrays
            UnitName[] allUnits = new UnitName[ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length + sanguineCourt.Length + sakuraDynasty.Length + deepstoneHold.Length + drakosaurBrood.Length + special.Length];
            ironLegion.CopyTo(allUnits, 0);
            Gruntkin.CopyTo(allUnits, ironLegion.Length);
            ravenHost.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length);
            taelindor.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length);
            sanguineCourt.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length);
            sakuraDynasty.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length + sanguineCourt.Length);
            deepstoneHold.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length + sanguineCourt.Length + sakuraDynasty.Length);
            drakosaurBrood.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length + sanguineCourt.Length + sakuraDynasty.Length + deepstoneHold.Length);
            special.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length + sanguineCourt.Length + sakuraDynasty.Length + deepstoneHold.Length + drakosaurBrood.Length);

            foreach (UnitName unitName in allUnits)
            {
                CustomBattleSquadSpawnCard unitUI = Instantiate(unitUIPrefab, unitUIParent);
                unitUI.SetUp(unitName, SwitchCursorModeToSpawn);
                unitUIPrefabs.Add(unitUI);
            }

            deleteAllSquadsButton.onClick.AddListener(() => DeleteAllSquadsOfFaction());

            Toggle[] toggles = raceToggleGroup.GetComponentsInChildren<Toggle>();
            Toggle firstToggle = toggles[0];
            firstToggle.isOn = true;
            firstToggle.Select();
            firstToggle.gameObject.GetComponent<Animator>().Play("Pressed");
            lastActiveToggle = firstToggle;

            toggles = factionToggleGroup.GetComponentsInChildren<Toggle>();
            Toggle firstFactionToggle = toggles[0];
            firstFactionToggle.isOn = true;
            firstFactionToggle.Select();
            firstFactionToggle.gameObject.GetComponent<Animator>().Play("Pressed");
            lastActiveFactionToggle = firstFactionToggle;

            unitCountInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            unitCountInputField.onValueChanged.AddListener(value =>
            {
                if (int.TryParse(value, out int count) && count > 0)
                    BattleManager.Instance.SpawnManager.SetUnitCountOverride(count);
                else
                    BattleManager.Instance.SpawnManager.SetUnitCountOverride(0);
            });
            BattleManager.Instance.OnCursorModeChanged += OnCursorModeChanged;

            spawnOptionsCanvasGroup.CGEnable();
            spawnControlsCanvasGroup.CGDisable();
            generateBattlefieldButton.Button.onClick.AddListener(RegenerateBattlefield);

            mapRegionDropdown.options.Clear();
            mapRegions = MapThemeManager.Instance.GetAllMapRegions();
            foreach (MapRegion region in mapRegions)
            {
                if(region.Race == Race.Special) continue;
                
                string localizedRegionName = LocalizationManager.Instance.GetText(region.Race.ToString()+"MapRegion");
                mapRegionDropdown.options.Add(new TMP_Dropdown.OptionData(localizedRegionName));
            }
            mapRegionDropdown.value = 0;
            mapRegionDropdown.RefreshShownValue();
            mapRegionDropdown.onValueChanged.AddListener(value =>
            {
                OnRegionChanged(value);
            });
            mapRegion = mapRegions[0];

            biomeDropdown.onValueChanged.AddListener(value =>
            {
                OnBiomeChanged(value);
            });
            ResetBiomeDropdownOptions();

            weatherDropdown.onValueChanged.AddListener(value =>
            {
                OnWeatherChanged(value);
            });
            ResetWeatherDropdownOptions();

            useRandomSeedToggle.onValueChanged.AddListener(ChangeRandomSeedToggle);
            seedInputField.text = _seed.ToString();
            seedInputField.onValueChanged.AddListener((string value) =>
            {
                if (int.TryParse(value, out int result))
                {
                    seed = result;
                }
                else
                {
                    seed = 0;
                }
            });

            SetDeploymentToggleOnStart();

            prestigeDropdown.onValueChanged.AddListener(value => SetPrestige());
            prestigeDropdown.value = 0;

            SetPrestige();
            SelectRace(Race.IronLegion);
            SelectTeam(Team.Player);
        }
        private void OnCursorModeChanged(CursorMode _cursorMode)
        {
            switch (_cursorMode)
            {
                case CursorMode.Free:
                    spawnOptionsCanvasGroup.CGEnable();
                    spawnControlsCanvasGroup.CGDisable();
                    break;
                case CursorMode.SpawnSquad:
                    spawnOptionsCanvasGroup.CGDisable();
                    spawnControlsCanvasGroup.CGEnable();
                    break;
            }
        }
        private void SwitchCursorModeToSpawn()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.MouseOverNode);
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
            BattleManager.Instance.SetCursorMode(CursorMode.SpawnSquad);
        }
        public void SelectRace(Race _race)
        {
            selectedRace = _race;
            string raceName = LocalizationManager.Instance.GetText(selectedRace.ToString());
            raceNameText.SetText(raceName);
            SetUnitPrefabs(selectedRace);
        }
        public void OnToggleChanged(Toggle toggle)
        {
            if (toggle.isOn && toggle != lastActiveToggle)
            {
                Race race = (Race)Enum.Parse(typeof(Race), toggle.name);
                SelectRace(race);
                lastActiveToggle = toggle; // Update the last active toggle
                IAudioRequester.Instance.PlaySFX("tiny-click");
            }
        }
        public void ChangeRandomSeedToggle(bool _on)
        {
            // Debug.Log($"Random Seed Toggle: {_on}");
            // useRandomSeedToggle.isOn = _on;
            useRandomSeed = _on;
        }
        public void ChangeToggle(bool _previous)
        {
            lastActiveToggle.gameObject.GetComponent<SampleToggleHelper>().SetToggle(false);
            if (!_previous) lastActiveToggle.gameObject.GetComponent<SampleNavInputHelper>().SelectNext();
            else
                lastActiveToggle.gameObject.GetComponent<SampleNavInputHelper>().SelectPrevious();
        }
        public void OnFactionToggleChanged(Toggle toggle)
        {
            if (toggle.isOn && toggle != lastActiveFactionToggle)
            {
                Team faction = (Team)Enum.Parse(typeof(Team), toggle.name);
                SelectTeam(faction);
                lastActiveFactionToggle = toggle; // Update the last active toggle
                IAudioRequester.Instance.PlaySFX("tiny-click");
            }
        }
        public void SelectTeam(Team _faction)
        {
            BattleManager.Instance.SetSelectedTeam(_faction);
            string factionName = _faction == Team.Player ? "Player" : "Enemy";
            factionName = LocalizationManager.Instance.GetText(factionName);
            factionNameText.SetText(factionName);

            BattleManager.Instance.BattleCameraScript.SetFaction(_faction);
            foreach (Image background in uiBackgrounds)
            {
                background.color = _faction == Team.Player ? playerColor : enemyColor;
            }
        }
        public void ChangeFactionToggle(bool _previous)
        {
            lastActiveFactionToggle.gameObject.GetComponent<SampleToggleHelper>().SetToggle(false);
            if (!_previous) lastActiveFactionToggle.gameObject.GetComponent<SampleNavInputHelper>().SelectNext();
            else
                lastActiveFactionToggle.gameObject.GetComponent<SampleNavInputHelper>().SelectPrevious();
        }
        public void ToggleWeather(int value)
        {
        }
        public void ToggleBattlefieldType()
        {
            biome = (Biome)biomeDropdown.value;
        }
        public void SetPrestige()
        {
            BattleManager.Instance.UnitsToSpawnPrestige = prestigeDropdown.value;
        }
        private void SetUnitPrefabs(Race race)
        {
            foreach (CustomBattleSquadSpawnCard unitUI in unitUIPrefabs)
            {
                bool shouldBeActive = TabletopTavernData.Instance.GetRaceFromUnitName(unitUI.UnitName) == race;
                unitUI.gameObject.SetActive(shouldBeActive);
            }
        }
        private void DeleteAllSquadsOfFaction()
        {
            //delete all squads of selected faction
            BattleManager.Instance.UnitSelectionManager.DeselectSquadsBeforeDeletionOrSpawning();
            NativeArray<SquadEntity> playerSquads = BattleManager.Instance.SquadManager.RetrieveAllSquads();
            foreach (SquadEntity squad in playerSquads)
            {
                if (squad.Team != BattleManager.Instance.SelectedTeam) continue;
                BattleManager.Instance.UnitSelectionManager.AttemptSquadSelect(squad.SquadId, true);
            }
            BattleManager.Instance.SquadManager.DeleteSelectedSquads();
            BattleManager.Instance.SquadManager.WipeRegisteredSquadData(BattleManager.Instance.SelectedTeam == Team.Player);
            playerSquads.Dispose();
        }
        private void OnDestroy()
        {
            deleteAllSquadsButton.onClick.RemoveAllListeners();
            generateBattlefieldButton.Button.onClick.RemoveAllListeners();
            useRandomSeedToggle.onValueChanged.RemoveAllListeners();
            seedInputField.onValueChanged.RemoveAllListeners();
            unitCountInputField.onValueChanged.RemoveAllListeners();
            prestigeDropdown.onValueChanged.RemoveAllListeners();
            biomeDropdown.onValueChanged.RemoveAllListeners();
            weatherDropdown.onValueChanged.RemoveAllListeners();
            mapRegionDropdown.onValueChanged.RemoveAllListeners();

            if (BattleManager.HasInstance)
                BattleManager.Instance.OnCursorModeChanged -= OnCursorModeChanged;
        }
        private async void RegenerateBattlefield()
        {
            BattleManager.Instance.BattleCleanUpManager.RegeneratingBattlefieldIndicator.SetActive(true);
            await Task.Delay(100); //small delay to ensure the indicator shows up

            int _seed = BattleManager.Instance.GreyCompanyBattlefield.GenerateBattlefieldFromCustomBattle(useRandomSeed, seed, mapRegion, biome, _regeneratingBattlefield: true);
            seed = _seed;
            seedInputField.text = seed.ToString();
            ResetWeatherDropdownOptions();
            ResetBiomeDropdownOptions();
        }
        private void OnRegionChanged(int value)
        {
            mapRegion = mapRegions[value];
            mapRegionDropdown.value = value;
            mapRegionDropdown.RefreshShownValue();
            // Each region has its own weather and biome lists; stale labels index the wrong entry.
            ResetWeatherDropdownOptions();
            ResetBiomeDropdownOptions();
        }
        private void ResetBiomeDropdownOptions()
        {
            biomeDropdown.options.Clear();
            foreach (var possibleBiomes in mapRegion.possibleBiomes)
            {
                string localizedBiomeName = LocalizationManager.Instance.GetText(possibleBiomes.biome.ToString());
                biomeDropdown.options.Add(new TMP_Dropdown.OptionData(localizedBiomeName));
            }
            biomeDropdown.value = 0;
            biome = (Biome)0;
            biomeDropdown.RefreshShownValue();
        }
        private void OnBiomeChanged(int value)
        {
            biome = mapRegion.possibleBiomes[value].biome;
            biomeDropdown.RefreshShownValue();
        }
        private void ResetWeatherDropdownOptions()
        {
            weatherDropdown.options.Clear();
            foreach (var possibleWeathers in mapRegion.possibleWeathers)
            {
                string localizedWeatherName = LocalizationManager.Instance.GetText(possibleWeathers.weather.ToString());
                weatherDropdown.options.Add(new TMP_Dropdown.OptionData(localizedWeatherName));
            }
            weatherDropdown.value = 0;
            weatherDropdown.RefreshShownValue();
        }
        private void OnWeatherChanged(int value)
        {
            weather = mapRegion.possibleWeathers[value].weather;
            weatherDropdown.RefreshShownValue();
            BattleManager.Instance.BattlefieldEnvManager.ToggleWeather(weather);

        }
        public void OnDeploymentToggleChanged(Toggle toggle)
        {
            if (toggle.isOn && toggle != lastActiveDeploymentToggle)
            {
                bool isSpawnUnits = toggle.name == "SpawnUnits";
                if (isSpawnUnits)
                {
                    spawnUnitsCanvasGroup.CGEnable();
                    mapSettingsCanvasGroup.CGDisable();
                }
                else
                {
                    spawnUnitsCanvasGroup.CGDisable();
                    mapSettingsCanvasGroup.CGEnable();
                }
                lastActiveDeploymentToggle = toggle; // Update the last active toggle
                IAudioRequester.Instance.PlaySFX("tiny-click");
            }
        }
        public void SetDeploymentToggleOnStart()
        {
            deploymentToggleGroup.GetComponentsInChildren<Toggle>()[0].isOn = true;
            lastActiveDeploymentToggle = deploymentToggleGroup.GetComponentsInChildren<Toggle>()[0];
            spawnUnitsCanvasGroup.CGEnable();
        }
    }
}