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
        // Shown over gear an Ordeal switches off or Rusted Arms broke; null on a slot built before it existed.
        [SerializeField] private GameObject inactiveCross;

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
        bool broken;
        public GearID GearID => gearID;
        // bool dynamicSellValue = false;

        // The Collection's dim tint for an owned item that cannot be used right now.
        static readonly Color InactiveIconColor = new(0.6f, 0.6f, 0.6f, 0.7f);

        Color iconColor;

        private void Awake()
        {
            memoriTooltipTrigger = GetComponent<MemoriTooltipTrigger>();
            iconColor = gearIcon.color;
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

            CampaignSaveData run = CampaignManager.Instance.CampaignSaveManager.SaveData;
            broken = run.IsGearBroken(gearID);
            OrdealId countering = OrdealRegistry.CounteringOrdeal(run.ActiveOrdeals, gearID);
            string inactiveNote = broken ? OrdealRegistry.BrokenNote() : countering != OrdealId.None ? OrdealRegistry.InactiveNote(countering) : "";
            gearIcon.color = inactiveNote.Length > 0 ? iconColor * InactiveIconColor : iconColor;
            if (inactiveCross != null) inactiveCross.SetActive(inactiveNote.Length > 0);

            memoriTooltipTrigger.SetUpToolTip(new TooltipContent
            {
                Title = gearNameLocalized,
                Body = KeywordText.ForTooltip(gearDescLocalized),
                Detail = gearFlavorLocalized,
                Footer = inactiveNote,
            });
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

            // Broken gear sells for nothing and fires no sell effect; selling it only frees the slot.
            if (broken)
            {
                sellValue = 0;
                bonusValue = 0;
            }
            else if (gear.GearName == GearData.OrnateRing.GearName)//doubles gold
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
                    if (CampaignManager.Instance.CampaignSaveManager.SaveData.IsGearBroken(gearName)) continue;

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

            // Not ClearClickListeners: OnGearDisplaySelected plays the click itself.
            Button.onClick.RemoveAllListeners();
            Button.onClick.AddListener(OnGearDisplaySelected);
            sellButton.ClearClickListeners();
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
            gearIcon.color = iconColor;
            if (inactiveCross != null) inactiveCross.SetActive(false);
            broken = false;
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
            sellButton.ClearClickListeners();

            //prestige a random unit
            if(gear.GearName == GearData.Mitre.GearName && !broken)
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
