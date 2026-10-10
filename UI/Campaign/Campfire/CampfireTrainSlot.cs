using System;
using Memori.Audio;
using Memori.SaveData;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Campfire
{
    /// <summary>One squad in the campfire's Train strip: its army card, its Prestige marks and its price.</summary>
    public class CampfireTrainSlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private RectTransform cardHolder;
        [SerializeField] private Image hoverRing;
        [SerializeField] private Image[] marks;
        [SerializeField] private TMP_Text priceText;
        [SerializeField] private Color markEarned = new(0.914f, 0.753f, 0.416f, 1f);
        [SerializeField] private Color markNext = new(0.914f, 0.753f, 0.416f, 0.35f);
        [SerializeField] private Color markEmpty = new(0.337f, 0.376f, 0.373f, 1f);
        [SerializeField] private Color maxColour = new(0.549f, 0.604f, 0.635f, 1f);

        private SquadToLoad squad;
        private bool trainable;
        private Action<SquadToLoad> onPicked;
        private Action<SquadToLoad, bool> onHovered;

        public void SetUp(SquadToLoad _squad, SquadDisplayCardMenu cardPrefab, bool inReserve, string price, bool _trainable,
            Action<SquadToLoad> _onPicked, Action<SquadToLoad, bool> _onHovered)
        {
            squad = _squad;
            trainable = _trainable;
            onPicked = _onPicked;
            onHovered = _onHovered;

            SquadDisplayCardMenu card = Instantiate(cardPrefab, cardHolder);
            card.SetUp(squad, inReserve);
            // The card is only a picture here: no drag, no select, no squad options. This slot takes the clicks.
            card.LockCard(true);
            card.MakeInteractable(false);
            card.InheritCanvasSorting();
            RectTransform cardRect = (RectTransform)card.transform;
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = Vector2.zero;

            for (int i = 0; i < marks.Length; i++)
            {
                bool earned = i < squad.UnitPrestige;
                bool next = trainable && i == squad.UnitPrestige;
                marks[i].color = earned ? markEarned : next ? markNext : markEmpty;
            }

            priceText.text = price;
            if (!trainable) priceText.color = maxColour;
            hoverRing.enabled = false;

            button.ClearClickListeners();
            if (trainable) button.onClick.AddListener(() => onPicked?.Invoke(squad));
            if (trainable) UIHoverBloom.Attach(gameObject, null, 1.04f, false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hoverRing.enabled = trainable;
            if (trainable) IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
            onHovered?.Invoke(squad, true);
            if (!CampaignManager.HasInstance) return;
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.HoverSquad(squad, true, transform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hoverRing.enabled = false;
            onHovered?.Invoke(squad, false);
            if (!CampaignManager.HasInstance) return;
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.HoverSquad(squad, false, transform);
        }
    }
}
