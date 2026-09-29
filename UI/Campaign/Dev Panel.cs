using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TJ
{
    public class DevPanel : MonoBehaviour
    {
        [SerializeField] private Button _giveGoldButton;
        [SerializeField] private Button _giveUnitButton;
        [SerializeField] private TMP_Dropdown _giveUnitDropdown;
        [SerializeField] private Button _healAllUnitsButton;
        [SerializeField] private Button _completeChapterButton;
        [SerializeField] private Button _giveConsumableButton;
        [SerializeField] private TMP_Dropdown _giveConsumableDropdown;
        [SerializeField] private Button _giveGearButton;
        [SerializeField] private TMP_Dropdown _giveGearDropdown;
        [SerializeField] private Button _forceLODButton;
        [SerializeField] private Button _grantOrdealButton;
        [SerializeField] private TMP_Dropdown _grantOrdealDropdown;
        private void Start()
        {
            if (_grantOrdealDropdown != null && _grantOrdealButton != null)
            {
                _grantOrdealDropdown.ClearOptions();
                foreach (OrdealDefinition ordeal in OrdealRegistry.All)
                    _grantOrdealDropdown.options.Add(new TMP_Dropdown.OptionData(ordeal.Id.ToString()));
                _grantOrdealDropdown.value = 0;
                _grantOrdealDropdown.RefreshShownValue();
                _grantOrdealButton.onClick.AddListener(GrantOrdeal);
            }

            _giveUnitDropdown.ClearOptions();
            foreach (var unit in System.Enum.GetValues(typeof(UnitName)))
            {
                _giveUnitDropdown.options.Add(new TMP_Dropdown.OptionData(
                    unit.ToString()
                ));
            }
            _giveUnitDropdown.value = 0;
            _giveUnitDropdown.RefreshShownValue();
            _giveUnitButton.onClick.AddListener(GiveUnit);
            

            _giveConsumableDropdown.ClearOptions();
            foreach (var consumable in System.Enum.GetValues(typeof(ConsumableEnum)))
            {
                _giveConsumableDropdown.options.Add(new TMP_Dropdown.OptionData(
                    consumable.ToString()
                ));
            }
            _giveConsumableDropdown.value = 0;
            _giveConsumableButton.onClick.AddListener(GiveConsumable);
            _giveConsumableDropdown.RefreshShownValue();


            _giveGearDropdown.ClearOptions();
            foreach (var gear in System.Enum.GetValues(typeof(GearID)))
            {
                _giveGearDropdown.options.Add(new TMP_Dropdown.OptionData(
                    gear.ToString()
                ));
            }
            _giveGearDropdown.value = 0;
            _giveGearDropdown.RefreshShownValue();
            _giveGearButton.onClick.AddListener(GiveGear);

            _giveGoldButton.onClick.AddListener(GiveGold);
            _healAllUnitsButton.onClick.AddListener(HealAllUnits);
            _completeChapterButton.onClick.AddListener(CompleteChapter);
            _forceLODButton.onClick.AddListener(ForceLODHighDetail);
        }
        private void SaveAndSnapshot()
        {
            CampaignManager.Instance.CampaignSaveManager.SaveCampaign();
            CampaignManager.Instance.CampaignSaveManager.SaveCampaignSnapshot();
        }
        public void GiveUnit()
        {
            if (!CampaignManager.HasInstance) return;
            string unitName = _giveUnitDropdown.options[_giveUnitDropdown.value].text;
            Debug.Log($"[DevPanel] GiveUnit: {unitName}");
            SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats((UnitName)System.Enum.Parse(typeof(UnitName), unitName));
            if(CampaignManager.Instance.CampaignSaveManager.CheckForRoomToRecruit())
                CampaignManager.Instance.CampaignSaveManager.RecruitSquad(squadStats);
            SaveAndSnapshot();
        }
        public void GiveConsumable()
        {
            if (!CampaignManager.HasInstance) return;
            string consumableName = _giveConsumableDropdown.options[_giveConsumableDropdown.value].text;
            Debug.Log($"[DevPanel] GiveConsumable: {consumableName}");
            ConsumableEnum consumableEnum = (ConsumableEnum)System.Enum.Parse(typeof(ConsumableEnum), consumableName);
            if(CampaignManager.Instance.CampaignSaveManager.HasRoomForConsumable())
                CampaignManager.Instance.CampaignSaveManager.AquireConsumable(consumableEnum);
            SaveAndSnapshot();
        }
        public void GiveGear()
        {
            if (!CampaignManager.HasInstance) return;
            string gearName = _giveGearDropdown.options[_giveGearDropdown.value].text;
            Debug.Log($"[DevPanel] GiveGear: {gearName}");
            GearID gearID = (GearID)System.Enum.Parse(typeof(GearID), gearName);
            if(CampaignManager.Instance.CampaignSaveManager.CanAquireGear())
                CampaignManager.Instance.CampaignSaveManager.AquireGear(gearID);
            SaveAndSnapshot();
        }
        public void GrantOrdeal()
        {
            if (!CampaignManager.HasInstance) return;
            string ordealName = _grantOrdealDropdown.options[_grantOrdealDropdown.value].text;
            Debug.Log($"[DevPanel] GrantOrdeal: {ordealName}");
            CampaignManager.Instance.CampaignSaveManager.TakeOrdeal((OrdealId)System.Enum.Parse(typeof(OrdealId), ordealName));
        }
        public void GiveGold()
        {
            if (!CampaignManager.HasInstance) return;
            Debug.Log("[DevPanel] GiveGold: +500");
            CampaignManager.Instance.GoldManager.ModifyGold(500, "Dev Panel");
            SaveAndSnapshot();
        }
        public void HealAllUnits()
        {
            if (!CampaignManager.HasInstance) return;
            Debug.Log("[DevPanel] HealAllUnits");
            CampaignManager.Instance.CampaignSaveManager.ModifyTroopHealth(1);
            SaveAndSnapshot();
        }
        public void CompleteChapter()
        {
            if (!CampaignManager.HasInstance) return;
            Debug.Log("[DevPanel] CompleteChapter");
            CampaignManager.Instance.MapSceneUIManager.CompleteLayerAction();
        }
        public void ForceLODHighDetail()
        {
            Debug.Log("[DevPanel] ForceLODHighDetail");
            foreach (var lod in FindObjectsByType<LODGroup>(FindObjectsSortMode.None))
            {
                lod.ForceLOD(1);
            }
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset)
                urpAsset.shadowDistance = 1000f;
        }

        [SerializeField] private string playerPrefsKey;

        [ContextMenu("Reset PlayerPrefs")]
        public void ResetPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(playerPrefsKey);
        }
    }
}
