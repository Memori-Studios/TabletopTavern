using Memori.Tooltip;
using TJ;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UI.Extensions;
using UnityEngine.EventSystems;
using TMPro;
using Memori.Audio;
using Memori.UI;
using Memori.Utilities;
using TJ.Map;
using System.Collections.Generic;
using Memori.Localization;
using MoreMountains.Feedbacks;
using Memori.Metaprogression;
using Memori.SaveData;

namespace TJ
{
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class GearDisplay : MemoriButtonV2
    {
        [SerializeField] private Image gearIcon;

        [Header("Sell Tag")]
        [SerializeField] private GameObject gearSellTag;
        [SerializeField] private TMP_Text sellValueText;
        [SerializeField] private Button sellButton;
        [SerializeField] private MMF_Player gearSpawnFeedback;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _gearSellValueMetaprogressionModel;

        MemoriTooltipTrigger memoriTooltipTrigger;
        int sellValue;
        int bonusValue;
        Gear gear;
        GearID gearID;
        public GearID GearID => gearID;
        // bool dynamicSellValue = false;

        private void Awake()
        {
            memoriTooltipTrigger = GetComponent<MemoriTooltipTrigger>();
        }
        public void LoadGearDisplay(GearID _gearID)
        {
            gearID = _gearID;
            gear = GearData.GetGear(gearID);
            gearIcon.sprite = SpriteData.GetSprite(gear.GearName);

            string gearNameLocalized = LocalizationManager.Instance.GetText(gearID + "Name");
            string gearDescLocalized = LocalizationManager.Instance.GetText(gearID + "Desc");
            gearDescLocalized = string.Format(gearDescLocalized, gear.GearModifierValue);
            string gearFlavorLocalized = LocalizationManager.Instance.GetText(gearID + "Flavor");

            memoriTooltipTrigger.SetUpToolTip(gearNameLocalized, KeywordText.ForTooltip(gearDescLocalized), gearFlavorLocalized, _delay: 0f);
            memoriTooltipTrigger.enabled = true;
            gearIcon.enabled = true;
            gearSellTag.SetActive(false);
            sellValue = GetGearSellValue(gearID);
            bonusValue = 0;

            #region Metaprogression
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_gearSellValueMetaprogressionModel)) {
                bonusValue += _gearSellValueMetaprogressionModel.NodeValue;
            }
            #endregion

            if (gear.GearName == GearData.OrnateRing.GearName)//doubles gold
            {
                // dynamicSellValue = true;
                sellValue = CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount;
                sellValue = sellValue.Clamp(0, 20);
            }
            else if (gear.GearName == GearData.ThePotato.GearName)//increases in sell each chapter
            {
                // dynamicSellValue = true;
                sellValue += CampaignManager.Instance.CampaignSaveManager.SaveData.turnsSincePotato;
            }
            else if (gear.GearName == GearData.Cauldron.GearName)//get combined value of all gear
            {
                // dynamicSellValue = true;
                List<GearID> gearNames = CampaignManager.Instance.CampaignSaveManager.SaveData.Gear;
                foreach (GearID gearName in gearNames)
                {
                    if (gearName == GearID.Cauldron) continue;

                    if (gearName == GearID.ThePotato)
                    {
                        sellValue += GetGearSellValue(gearName);
                        sellValue += CampaignManager.Instance.CampaignSaveManager.SaveData.turnsSincePotato;
                        continue;
                    }

                    if (gearName == GearID.OrnateRing)
                    {
                        int ringValue = CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount;
                        sellValue += ringValue.Clamp(0, 20);
                        continue;
                    }

                    sellValue += GetGearSellValue(gearName);
                }
            }
            string sellLocalizedText = LocalizationManager.Instance.GetText("Sell");
            string bonusValueString = bonusValue > 0 ? $"<color={ColorData.Green}>+{bonusValue}</color>" : "";
            sellValueText.text = $"{sellLocalizedText}    {sellValue}{bonusValueString} <sprite name=GoldSprite>";

            Button.onClick.RemoveAllListeners();
            Button.onClick.AddListener(OnGearDisplaySelected);
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(SellGear);
        }
        public void AquireGearJuice()
        {
            gearSpawnFeedback.PlayFeedbacks();
        }
        public void UnloadGearDisplay()
        {
            gearIcon.sprite = null;
            gearIcon.enabled = false;
            gearSellTag.SetActive(false);
            Button.onClick.RemoveAllListeners();
            string titleLocalized = LocalizationManager.Instance.GetText("emptyGearSlotTitle");
            string descriptionLocalized = LocalizationManager.Instance.GetText("emptyGearSlotDescription");
            memoriTooltipTrigger.SetUpToolTip(titleLocalized, descriptionLocalized, _delay: 1f);
            memoriTooltipTrigger.enabled = true;
            gearID = GearID.None;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            if (gearSellTag.activeSelf)
                CloseGearSellTag();
        }
        public void OnGearDisplaySelected()
        {
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.CloseAllPopUps();
            TooltipManager.Instance.HideTooltip();
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            if(gearSellTag.activeSelf) {
                CloseGearSellTag();
            } else {
                OpenGearSellTag();
            }

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.SellGear);
        }
        public void OpenGearSellTag()
        {
            gearSellTag.SetActive(true);
            memoriTooltipTrigger.enabled = false;
        }
        public void CloseGearSellTag()
        {
            gearSellTag.SetActive(false);
            if (GetComponentInChildren<MetaprogressionLockedButton>() == null)
                memoriTooltipTrigger.enabled = true;
        }
        public void SellGear()
        {
            TooltipManager.Instance.HideTooltip();
            sellButton.onClick.RemoveAllListeners();

            //prestige a random unit
            if(gear.GearName == GearData.Mitre.GearName)
            {
                CampaignManager.Instance.CampaignSaveManager.PrestigeRandomUnit();
                CampaignManager.Instance.MapSceneUIManager.TryDrainPendingPrestigeChoices();
            }
            CampaignManager.Instance.CampaignSaveManager.SellGear(gearID, sellValue + bonusValue);
        }
        public int GetGearSellValue(GearID gearName)
        {
            return GearData.GetSellValue(GearData.GetGear(gearName).GearRarity);
        }
    }
}
