using System;
using System.Collections;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Engagement
{
    /// <summary>A spoil of war after a win. The player keeps only one, so the row looks like a choice, not a button.</summary>
    public class EngagementChoiceRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public enum State { Rest, Hover, Dim, Chosen }

        [SerializeField] private Button button;
        [SerializeField] private Image fill;
        [SerializeField] private Image edge;
        [SerializeField] private Image glow;
        [SerializeField] private Image diamondEdge;
        [SerializeField] private Image diamondFill;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private GameObject valueGroup;
        [SerializeField] private Image valueIcon;
        [SerializeField] private TMP_Text value;
        [SerializeField] private TMP_Text tag;
        [SerializeField] private TMP_Text takenText;
        [SerializeField] private Image[] unitIcons;
        [SerializeField] private GameObject unitIconGroup;
        [SerializeField] private MemoriTooltipTrigger tooltip;
        // Dims the whole row while another row is hovered; the pop runs on the same wrapper.
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform pop;

        [Header("Look")]
        [SerializeField] private Color restFill = new(0.086f, 0.125f, 0.137f, 1f);
        [SerializeField] private Color hoverFill = new(0.106f, 0.165f, 0.180f, 1f);
        [SerializeField] private Color restEdge = new(0.690f, 0.541f, 0.243f, 0.65f);
        [SerializeField] private Color hoverEdge = new(0.851f, 0.698f, 0.369f, 1f);
        [SerializeField] private Color restTitle = new(0.914f, 0.753f, 0.416f, 1f);
        [SerializeField] private Color hoverTitle = new(0.949f, 0.808f, 0.486f, 1f);
        [SerializeField] private Color diamondRest = new(0.059f, 0.086f, 0.094f, 1f);
        [SerializeField] private Color diamondChosen = new(0.914f, 0.753f, 0.416f, 1f);
        [SerializeField] private float restGlow = 0.14f;
        [SerializeField] private float hoverGlow = 0.3f;
        [SerializeField] private float dimAlpha = 0.38f;
        [SerializeField] private float vanishSeconds = 0.16f;

        private Action<EngagementChoiceRow, bool> onHover;
        private Coroutine vanish;
        private Color glowColour;

        public Button Button => button;
        public State Current { get; private set; }

        private void Awake()
        {
            glowColour = glow.color;
        }

        public void SetUp(Action<EngagementChoiceRow, bool> _onHover)
        {
            onHover = _onHover;
        }

        public void Set(Sprite _icon, Color _iconColour, string _title, string _detail)
        {
            icon.sprite = _icon;
            icon.color = _iconColour;
            icon.gameObject.SetActive(_icon != null);
            title.text = _title;
            detail.text = _detail;
            valueGroup.SetActive(false);
            tag.gameObject.SetActive(false);
            unitIconGroup.SetActive(false);
            takenText.gameObject.SetActive(false);
            tooltip.enabled = false;
            SetState(State.Rest);
        }

        public void SetValue(string _value, bool _gold)
        {
            valueGroup.SetActive(true);
            valueIcon.gameObject.SetActive(_gold);
            value.text = _value;
        }

        public void SetTag(string _text)
        {
            tag.gameObject.SetActive(true);
            tag.text = _text;
        }

        public void SetDetailColour(Color colour) => detail.color = colour;

        public void SetTooltip(string _title, string _body, string _flavour = "")
        {
            tooltip.enabled = true;
            tooltip.SetUpToolTip(_title, _body, _flavour);
        }

        // Raise Dead shows the units it would bring back beside its name.
        public void SetUnits(Sprite[] sprites)
        {
            unitIconGroup.SetActive(sprites.Length > 0);
            for (int i = 0; i < unitIcons.Length; i++)
            {
                bool used = i < sprites.Length;
                unitIcons[i].gameObject.SetActive(used);
                if (used) unitIcons[i].sprite = sprites[i];
            }
        }

        public void SetState(State state)
        {
            Current = state;
            bool lit = state == State.Hover || state == State.Chosen;
            fill.color = state == State.Hover ? hoverFill : restFill;
            edge.color = lit ? hoverEdge : restEdge;
            title.color = state == State.Hover ? hoverTitle : restTitle;
            diamondEdge.color = lit ? hoverEdge : restEdge;
            diamondFill.color = state == State.Chosen ? diamondChosen : diamondRest;
            Color colour = glowColour;
            colour.a = state == State.Dim ? 0f : lit ? hoverGlow : restGlow;
            glow.color = colour;
            group.alpha = state == State.Dim ? dimAlpha : 1f;
            if (state != State.Chosen) return;
            button.interactable = false;
            valueGroup.SetActive(false);
            tag.gameObject.SetActive(false);
            takenText.gameObject.SetActive(true);
        }

        // The other choices leave once one is kept.
        public void PopAway()
        {
            button.interactable = false;
            if (vanish != null) StopCoroutine(vanish);
            if (!isActiveAndEnabled)
            {
                pop.gameObject.SetActive(false);
                return;
            }
            vanish = StartCoroutine(Vanish());
        }

        private IEnumerator Vanish()
        {
            float start = group.alpha;
            for (float t = 0f; t < vanishSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / vanishSeconds;
                pop.localScale = Vector3.one * Mathf.Lerp(1f, 0.92f, k * k);
                group.alpha = start * (1f - k);
                yield return null;
            }
            pop.gameObject.SetActive(false);
            vanish = null;
        }

        public void OnPointerEnter(PointerEventData eventData) => Hover(true);
        public void OnPointerExit(PointerEventData eventData) => Hover(false);
        public void OnSelect(BaseEventData eventData) => Hover(true);
        public void OnDeselect(BaseEventData eventData) => Hover(false);

        private void Hover(bool hovered)
        {
            if (!button.interactable) return;
            onHover?.Invoke(this, hovered);
        }
    }
}
