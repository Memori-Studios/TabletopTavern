using Memori.Tooltip;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Memori.Audio;
using Memori.UI;
using System.Collections;
using Memori.Notifications;
using Memori.Input;
using Memori.Localization;
using MoreMountains.Feedbacks;
using Memori.Steamworks;
using Memori.Metaprogression;
using Memori.SaveData;

namespace TJ.Map
{
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class ConsumableUI : MemoriButtonV2
    {
        [SerializeField] private Image consumableIcon;

        [Header("Options")]
        [SerializeField] private GameObject consumableOptions, drinkingNotesOptions;
        [SerializeField] private TMP_Text drinkText, sellText;
        [SerializeField] private Color availableColor, unavailableColor;
        [SerializeField] private Button drinkButton, sellButton;
        
        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _consumableSellValueMetaprogressionModel;

        MemoriTooltipTrigger memoriTooltipTrigger;
        Consumable consumable;
        private bool consumableLoaded = false;
        public bool ConsumableLoaded => consumableLoaded;
        int targetUnitIndex;
        HUDPanel hudPanel;
        int sellValue;
        [SerializeField] private int indexHovered;
        [SerializeField] private MMF_Player consumeableSpawnFeedback;
        private void Awake()
        {
            memoriTooltipTrigger = GetComponent<MemoriTooltipTrigger>();
        }
        public void LoadConsumableUI(ConsumableEnum _consumableEnum)
        {
            consumable = ConsumableData.GetConsumable(_consumableEnum);
            consumableIcon.sprite = SpriteData.GetSprite(_consumableEnum.ToString());
            hudPanel = CampaignManager.Instance.MapSceneUIManager.HUDPanel;
            string localizedConsumableName = LocalizationManager.Instance.GetText(consumable.ConsumableEnum.ToString() + "Name");
            string localizedConsumableDescription = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(consumable.ConsumableEnum);
            memoriTooltipTrigger.SetUpToolTip(localizedConsumableName, localizedConsumableDescription, _delay: 0f);
            memoriTooltipTrigger.enabled = true;
            consumableIcon.enabled = true;
            consumableLoaded = true;
            consumableOptions.SetActive(false);

            sellValue = ConsumableData.SellValue(consumable.ConsumableRarity);

            //DifficultyMod 13
            if(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Emperor)
            {
                sellValue = 0;
            }

            #region Metaprogression
            if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_consumableSellValueMetaprogressionModel)) {
                sellValue += _consumableSellValueMetaprogressionModel.NodeValue;
            }
            #endregion

            string sellLocalizedText = LocalizationManager.Instance.GetText("Sell");
            sellText.text = $"{sellLocalizedText}     {sellValue} <sprite name=GoldSprite>";

            Button.onClick.RemoveAllListeners();
            Button.onClick.AddListener(OnConsumableUISelected);
            sellButton.onClick.RemoveAllListeners();
            sellButton.onClick.AddListener(SellConsumable);
            drinkButton.onClick.RemoveAllListeners();
            drinkButton.onClick.AddListener(AttemptToDrink);
        }
        public void UnloadConsumableUI()
        {
            consumableIcon.sprite = null;
            consumableIcon.enabled = false;
            consumableLoaded = false;
            consumableOptions.SetActive(false);
            Button.onClick.RemoveAllListeners();
            string tooltipTitle = LocalizationManager.Instance.GetText("EmptyConsumableSlot");
            string tooltipDesc = LocalizationManager.Instance.GetText("EmptyConsumableSlotDesc");
            memoriTooltipTrigger.SetUpToolTip(tooltipTitle, tooltipDesc, _delay: 1f);
            memoriTooltipTrigger.enabled = true;
        }
        public void UpdateConsumableUI(bool _available)
        {
            drinkButton.interactable = _available;
            drinkText.color = _available ? availableColor : unavailableColor;
        }
        public override void OnPointerEnter(PointerEventData eventData)
        {
            if(consumableLoaded)
            {
                MemoriUI.BloomItemScale(transform, 1.025f, 0.1f);
                highlightImage.enabled = true;
                IAudioRequester.Instance.PlaySFX(SFXData.Drink);
            }
            else
            {
                MemoriUI.BloomItemScale(transform, 1.025f, 0.1f);
                IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
            }
        }
        public void AttemptToDrink()
        {
            switch (consumable.ConsumableEnum)
            {
                case ConsumableEnum.MinorHealth:
                    StartCoroutine(OnDrink());
                    break;
                case ConsumableEnum.MajorHealth:
                    StartCoroutine(OnDrink());
                    break;
                case ConsumableEnum.Prestige:
                    StartCoroutine(OnDrink());
                    break;
                case ConsumableEnum.Duplicate:
                    StartCoroutine(OnDrink());
                    break;
                case ConsumableEnum.TrialofGrasses:
                    StartCoroutine(OnDrink());
                    break;
                case ConsumableEnum.NewUnit:
                    DrinkConsumable();
                    break;
                case ConsumableEnum.Alchemist:
                    DrinkConsumable();
                    break;
                case ConsumableEnum.Rewind:
                    DrinkConsumable();
                    break;
                case ConsumableEnum.FateshineElixir:
                    DrinkConsumable();
                    break;
                case ConsumableEnum.RunewellNectar:
                    DrinkConsumable();
                    break;
                case ConsumableEnum.LambSauce:
                    DrinkConsumable();
                    break;
                default:
                    Debug.LogError($"Consumable {consumable.ConsumableEnum} not implemented for drinking.");
                    break;
            }
        }
        public IEnumerator OnDrink()
        {
            hudPanel.UILineDrawer.SetUp(this.GetComponent<RectTransform>());
            drinkingNotesOptions.SetActive(true);
            consumableOptions.SetActive(false);

            while (true)
            {
                hudPanel.UILineDrawer.pointB.transform.position = InputHandler.Instance.MousePosition;

                if (Input.GetMouseButtonDown(1))
                    break;

                indexHovered = CampaignManager.Instance.MapSceneUIManager.HUDPanel.HoveredSquadIndex;
                if (Input.GetMouseButtonDown(0))
                {
                    if(indexHovered != -1)
                    {
                        targetUnitIndex = indexHovered;
                        DrinkConsumable();
                        break;
                    }
                    else
                    {
                        string selectTargetText = LocalizationManager.Instance.GetText("consumable_must_select_target");
                        NotificationManager.Instance.ErrorNotification(selectTargetText);
                        break;
                    }
                }

                yield return null;
            }

            hudPanel.UILineDrawer.TurnOff();
            drinkingNotesOptions.SetActive(false);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
        }
        public void DrinkConsumable()
        {
            if (!CampaignManager.Instance.ConsumableManager.AttemptToUseConsumable(consumable.ConsumableEnum, targetUnitIndex))
            {
                CloseConsumableOptions();
                return;
            }

            if (!drinkButton.interactable)
            {
                NotificationManager.Instance.ErrorNotification("Consumable is not available at this time");
                return;
            }

            drinkButton.onClick.RemoveAllListeners();
            IAudioRequester.Instance.PlaySFX(SFXData.Drink);
            TooltipManager.Instance.HideTooltip();

            CampaignManager.Instance.ConsumableManager.UseConsumable(consumable.ConsumableEnum);
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.CloseAllPopUps();
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.MarkUnitAsJustUsedConsumable(targetUnitIndex);
            CampaignManager.Instance.CampaignSaveManager.SaveCampaign();
        }
        public void OnConsumableUISelected()
        {
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.CloseAllPopUps();
            TooltipManager.Instance.HideTooltip();
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            if (consumableOptions.activeSelf)
            {
                CloseConsumableOptions();
                return;
            }

            UpdateConsumableUI(CampaignManager.Instance.ConsumableManager.CanUseConsumable(consumable.ConsumableEnum));
            consumableOptions.SetActive(true);
            memoriTooltipTrigger.enabled = false;

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.ConsumableUsage);
        }
        public void CloseConsumableOptions()
        {
            consumableOptions.SetActive(false);
            memoriTooltipTrigger.enabled = true;
        }
        public void SellConsumable()
        {
            TooltipManager.Instance.HideTooltip();
            sellButton.onClick.RemoveAllListeners();

            if (consumable.ConsumableEnum == ConsumableEnum.Alchemist)
            {
                SteamAchievements.Unlock(AchievementId.SellAlchemy);
            }

            CampaignManager.Instance.CampaignSaveManager.SellConsumable(consumable, sellValue);
        }
        public void AcquireConsumableJuice()
        {
            consumeableSpawnFeedback.PlayFeedbacks();
        }
    }
}