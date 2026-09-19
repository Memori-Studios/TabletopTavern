using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MoreMountains.Feedbacks;
using Memori.Localization;
using System;

namespace TJ
{
    public class PrestigeTraitCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        // [SerializeField] private Image mouseOverHighlight1;
        // [SerializeField] private MMF_Player selectMMF;
        [SerializeField] private Button _button;
        [SerializeField] private TMP_Text _traitNameText, _traitDescriptionText;
        [SerializeField] private Animator animator;

        public Action<UnitAttribute> OnTraitCardSelected;

        UnitAttribute _trait;

        public void LoadTraitCard(UnitAttribute trait)
        {
            _trait = trait;

            string nameLocalized = LocalizationManager.Instance.GetText(trait.ToString());
            string descLocalized = LocalizationManager.Instance.GetText(trait.ToString() + "Desc");
            _traitNameText.text = nameLocalized;
            _traitDescriptionText.text = descLocalized;

            animator.SetBool("Normal", true);

            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(SelectTraitCard);
        }
        public void NotifyOfSelection(UnitAttribute trait)
        {
            if (_trait != trait)
                DarkenCard();
        }
        public void SelectTraitCard()
        {
            OnTraitCardSelected?.Invoke(_trait);
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            animator.SetBool("Highlighted", true);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            animator.SetBool("Normal", true);
        }

        public void DarkenCard()
        {
            Color darkenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Image[] images = GetComponentsInChildren<Image>();
            for (int i = 0; i < images.Length; i++)
            {
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
