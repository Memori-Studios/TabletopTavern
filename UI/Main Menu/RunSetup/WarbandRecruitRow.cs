using System;
using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// One unit in the warband recruit list: portrait, name, type, squad size, price, add and remove buttons, and a chip
    /// beside the row counting how many are in the army.
    /// Hovering reports the unit so the Squad Battle Info panel can show its stats.
    /// </summary>
    public class WarbandRecruitRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private GameObject hoverFrame;
        [SerializeField] private CanvasGroup body;

        [Header("Unit")]
        [SerializeField] private Image portrait;
        [SerializeField] private Image portraitFrame;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private GameObject typeRow;
        [SerializeField] private Image typeIcon;
        [SerializeField] private TMP_Text typeText;

        [Header("Numbers")]
        [SerializeField] private CollectionChip armyChip;
        [SerializeField] private TMP_Text squadText;
        [SerializeField] private TMP_Text priceText;

        [Header("Add and remove")]
        [SerializeField] private Button addButton;
        [SerializeField] private Image addIcon;
        [SerializeField] private Button removeButton;
        // Hidden rather than disabled while none are in the army, so the columns never shift.
        [SerializeField] private CanvasGroup removeGroup;

        [SerializeField] private Color restBackground = new(1f, 1f, 1f, 0.03f);
        [SerializeField] private Color hoverBackground = new(0.79f, 0.84f, 0.88f, 0.1f);
        [SerializeField] private Color lockedPortrait = new(0f, 0f, 0f, 0.55f);
        // The plus sprite is already gold, so it is only faded, never recoloured.
        [SerializeField] private Color addReady = Color.white;
        [SerializeField] private Color addIdle = new(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color armyChipColour = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField, Range(0f, 1f)] private float dimAlpha = 0.5f;

        public Button AddButton => addButton;
        public SquadToLoad Squad { get; private set; }
        public bool Found { get; private set; }
        public int Cost { get; private set; }

        private Action<SquadToLoad> onAdd;
        private Action<SquadToLoad> onRemove;
        private Action<SquadToLoad> onHover;
        private Action onExit;

        public void Set(SquadToLoad squad, bool found, int cost, Action<SquadToLoad> _onAdd, Action<SquadToLoad> _onRemove,
                        Action<SquadToLoad> _onHover, Action _onExit)
        {
            Squad = squad;
            Found = found;
            Cost = cost;
            onAdd = _onAdd;
            onRemove = _onRemove;
            onHover = _onHover;
            onExit = _onExit;

            UnitName unit = squad.UnitName;
            SquadStats stats = TabletopTavernData.Instance.GetSquadStats(unit);
            Color rarity = (Color)ColorData.GetRarityTierColor(stats.RarityTier);

            portrait.sprite = TabletopTavernData.Instance.GetUnitIcon(unit);
            portrait.color = found ? Color.white : lockedPortrait;
            portraitFrame.color = found ? rarity : new Color(rarity.r, rarity.g, rarity.b, 0.35f);
            nameText.text = found ? T(unit.ToString()) : T("CollectionNotFound");

            typeRow.SetActive(found);
            typeIcon.sprite = TabletopTavernData.Instance.GetSquadTypeIcon(unit);
            string size = stats.unitSize != UnitSize.Artillery && stats.unitType != UnitType.Structure ? " " + T(stats.unitSize.ToString()) : string.Empty;
            typeText.text = T(stats.unitType.ToString()) + size;

            squadText.text = found ? squad.maxUnitCount.ToString() : string.Empty;

            addButton.onClick.RemoveListener(OnAddClicked);
            addButton.onClick.AddListener(OnAddClicked);
            removeButton.onClick.RemoveListener(OnRemoveClicked);
            removeButton.onClick.AddListener(OnRemoveClicked);
            // The row owns the hover sound; its buttons stay quiet so one row never plays two sounds.
            foreach (UIHoverSFX hoverSound in GetComponentsInChildren<UIHoverSFX>(true)) hoverSound.enabled = false;
            // An undiscovered unit's + is switched off; a click on it still shakes the row and says no.
            UIDenyFeedback.Attach(addButton, (RectTransform)background.transform);
            SetHovered(false);
        }

        /// <param name="inArmy">How many of this unit the starting army already holds.</param>
        /// <param name="affordable">The price fits the gold left.</param>
        /// <param name="armyFull">No squad slot is free.</param>
        public void SetState(int inArmy, bool affordable, bool armyFull)
        {
            body.alpha = Found && affordable ? 1f : dimAlpha;
            string colour = affordable ? ColorData.Gold : ColorData.Error;
            priceText.text = $"<color={colour}>{Cost}</color> <sprite name=GoldSprite>";

            // Overspending stays allowed, as with gear: the treasury turns red and the validation strip blocks Start.
            addButton.interactable = Found;
            addIcon.color = Found && affordable && !armyFull ? addReady : addIdle;
            this.armyFull = armyFull;

            bool inArmyNow = Found && inArmy > 0;
            armyChip.gameObject.SetActive(inArmyNow);
            if (inArmyNow) armyChip.Set(string.Format(T("WarbandRecruitInArmy"), inArmy), armyChipColour);
            removeGroup.alpha = inArmyNow ? 1f : 0f;
            removeGroup.interactable = inArmyNow;
            removeGroup.blocksRaycasts = inArmyNow;
        }

        private void OnAddClicked()
        {
            if (!Found) return;
            // A full army is refused by the manager's toast, which plays the fail sound; the row shakes with it.
            background.transform.localScale = Vector3.one;
            if (armyFull) Play(UIJuice.Shake((RectTransform)background.transform));
            else Play(UIJuice.Punch(background.transform, 1.04f));
            onAdd?.Invoke(Squad);
        }

        private void OnRemoveClicked() => onRemove?.Invoke(Squad);

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
            IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
            onHover?.Invoke(Squad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
            onExit?.Invoke();
        }

        private void OnDisable()
        {
            motion = null;
            tint = null;
            background.color = restBackground;
            hoverFrame.SetActive(false);
            background.transform.localScale = Vector3.one;
            addIcon.transform.localScale = Vector3.one;
        }

        private bool armyFull;
        private Coroutine motion, tint;

        private void Play(System.Collections.IEnumerator routine)
        {
            if (!isActiveAndEnabled) return;
            if (motion != null) StopCoroutine(motion);
            motion = StartCoroutine(routine);
        }

        // The row brightens over a moment rather than snapping, and its + grows a little while hovered.
        private void SetHovered(bool hovered)
        {
            hoverFrame.SetActive(hovered);
            if (!isActiveAndEnabled)
            {
                background.color = hovered ? hoverBackground : restBackground;
                return;
            }
            if (tint != null) StopCoroutine(tint);
            tint = StartCoroutine(Tint(hovered ? hoverBackground : restBackground, hovered && addButton.interactable ? 1.1f : 1f));
        }

        private System.Collections.IEnumerator Tint(Color target, float plusScale)
        {
            Color from = background.color;
            float fromScale = addIcon.transform.localScale.x;
            for (float t = 0f; t < 1f; t += Mathf.Min(Time.unscaledDeltaTime, UIJuice.MaxStep) / 0.08f)
            {
                background.color = Color.Lerp(from, target, t);
                float s = Mathf.Lerp(fromScale, plusScale, UIJuice.EaseOutCubic(t));
                addIcon.transform.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            background.color = target;
            addIcon.transform.localScale = new Vector3(plusScale, plusScale, 1f);
            tint = null;
        }

        private static string T(string key) => LocalizationManager.Instance.GetText(key);
    }
}
