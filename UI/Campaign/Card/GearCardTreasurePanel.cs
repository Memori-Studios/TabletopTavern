using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TJ.Shop;
using Memori.Notifications;
using Memori.Audio;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;
using Memori.Localization;
using TJ.Recruit;
using Memori.Core;
using TJ.Engagement;
using Memori.Tooltip;
using TJ.Map;
using System;
using Memori.SaveData;

namespace TJ
{
public class GearCardTreasurePanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
        [SerializeField] private Image gearImage;
        [SerializeField] private Image mouseOverHighlight1;
        [SerializeField] private MMF_Player selectMMF;
        [SerializeField] private MemoriButtonV2 memoriButton;
        [SerializeField] private TMP_Text _gearNameText, _gearDescriptionText;
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject newNotificationActive;

        public Action<GearID> OnGearCardSelected;
        public Action<ConsumableEnum> OnConsumableCardSelected;

        GearID _gearID;
        Gear _gear;
        ConsumableEnum _consumableEnum;
        bool _isConsumable;
        Coroutine _highlightCoroutine;

        public void LoadGearCardReward(GearID gearID)
        {
            _isConsumable = false;
            _gearID = gearID;
            _gear = GearData.GetGear(_gearID);

            string gearNameLocalized = LocalizationManager.Instance.GetText(_gearID+"Name");
            string gearDescLocalized = LocalizationManager.Instance.GetText(_gearID+"Desc");
            gearDescLocalized = string.Format(gearDescLocalized, _gear.GearModifierValue);
            gearImage.sprite = SpriteData.GetSprite(_gear.GearName);
            // The row has one description line, so the rarity word sits beside the name.
            _gearNameText.text = $"{gearNameLocalized} <size=70%>{ColorData.GearRarityLabel(_gear.GearRarity)}</size>";
            KeywordText.Apply(_gearDescriptionText, gearDescLocalized);
            mouseOverHighlight1.color = new Color(mouseOverHighlight1.color.r, mouseOverHighlight1.color.g, mouseOverHighlight1.color.b, 0f);
            mouseOverHighlight1.enabled = false;
            animator.SetBool("Normal", true);

            memoriButton.Button.onClick.RemoveAllListeners();
            memoriButton.Button.onClick.AddListener(SelectGearCard);

            bool isNew = !SaveDataHandler.GetGearIDsCollected().Contains((int)gearID);
            if (newNotificationActive != null) newNotificationActive.SetActive(isNew);
        }
        public void LoadConsumableCardReward(ConsumableEnum consumableEnum)
        {
            _isConsumable = true;
            _consumableEnum = consumableEnum;

            string nameLocalized = LocalizationManager.Instance.GetText(consumableEnum + "Name");
            string descLocalized = CampaignManager.Instance.ConsumableManager.GetConsumableDescription(consumableEnum);
            gearImage.sprite = SpriteData.GetSprite(consumableEnum.ToString());
            _gearNameText.text = nameLocalized;
            KeywordText.Apply(_gearDescriptionText, descLocalized);
            mouseOverHighlight1.color = new Color(mouseOverHighlight1.color.r, mouseOverHighlight1.color.g, mouseOverHighlight1.color.b, 0f);
            mouseOverHighlight1.enabled = false;
            animator.SetBool("Normal", true);

            memoriButton.Button.onClick.RemoveAllListeners();
            memoriButton.Button.onClick.AddListener(SelectGearCard);

            if (newNotificationActive != null) newNotificationActive.SetActive(false);
        }
        public void NotifyOfSelection(GearID gearID)
        {
            if (!_isConsumable && _gearID == gearID)
                selectMMF.PlayFeedbacks();
            else
                DarkenCard();
        }
        public void SelectGearCard()
        {
            if (_isConsumable)
                OnConsumableCardSelected?.Invoke(_consumableEnum);
            else
                OnGearCardSelected?.Invoke(_gearID);
        }
        public void PlayPurchaseFeedbacks()
        {
            selectMMF.PlayFeedbacks();
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_highlightCoroutine != null) StopCoroutine(_highlightCoroutine);
            _highlightCoroutine = StartCoroutine(FadeHighlight(1f));
            animator.SetBool("Highlighted", true);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            if (_highlightCoroutine != null) StopCoroutine(_highlightCoroutine);
            _highlightCoroutine = StartCoroutine(FadeHighlight(0f));
            animator.SetBool("Normal", true);
        }
        private System.Collections.IEnumerator FadeHighlight(float target)
        {
            mouseOverHighlight1.enabled = true;
            float start = mouseOverHighlight1.color.a;
            float elapsed = 0f;
            while (elapsed < 0.5f)
            {
                elapsed += Time.deltaTime;
                float a = Mathf.Lerp(start, target, elapsed / 0.5f);
                mouseOverHighlight1.color = new Color(mouseOverHighlight1.color.r, mouseOverHighlight1.color.g, mouseOverHighlight1.color.b, a);
                yield return null;
            }
            mouseOverHighlight1.color = new Color(mouseOverHighlight1.color.r, mouseOverHighlight1.color.g, mouseOverHighlight1.color.b, target);
            if (target == 0f) mouseOverHighlight1.enabled = false;
            _highlightCoroutine = null;
        }
        public void DarkenCard()
        {
            //this is triggered on all cards that are not selected, should get every text and image and set it to it's current color but slightly darker
            Color darkenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Image[] images = GetComponentsInChildren<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                //if color is black, skip it
                if (images[i].color == Color.black) continue;
                images[i].color = images[i].color * darkenColor;
            }
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].color = texts[i].color * darkenColor;
            }
        }
    }
}