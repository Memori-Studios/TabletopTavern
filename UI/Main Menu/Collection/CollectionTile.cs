using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// One square in a Collection grid: a gear item, a potion or a unit portrait. It only shows state;
    /// CollectionPanel decides what hovering and clicking mean.
    /// </summary>
    public class CollectionTile : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
    {
        [SerializeField] private Button button;
        [SerializeField] private Image frame;
        [SerializeField] private Image wash;
        [SerializeField] private Image icon;
        [Header("Unit tiles only")]
        [SerializeField] private Image typeIcon;
        [Header("State")]
        [SerializeField] private GameObject lockIcon;
        [SerializeField] private GameObject newDot;
        [SerializeField] private GameObject selectedGlow;

        [SerializeField] private Color lockedFrame = new(0.2f, 0.25f, 0.25f, 1f);
        [SerializeField] private Color hoverFrame = new(0.69f, 0.54f, 0.24f, 1f);
        [SerializeField] private Color selectedFrame = new(0.91f, 0.75f, 0.42f, 1f);
        [SerializeField] private Color lockedIcon = new(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color lockedPortrait = new(0.07f, 0.08f, 0.08f, 1f);
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField, Range(0f, 1f)] private float restFrameAlpha = 0.55f;
        [SerializeField, Range(0f, 1f)] private float washAlpha = 0.07f;
        [SerializeField] private Color dimmedIcon = new(0.6f, 0.6f, 0.6f, 0.7f);
        [SerializeField, Range(0f, 1f)] private float dimmedFrameAlpha = 0.25f;

        public event Action<CollectionTile> Hovered, Unhovered, Clicked;

        /// <summary>Index into the list the panel built this tile from.</summary>
        public int Index { get; set; }
        public bool Found { get; private set; }

        private Color _restFrame;
        private bool _hovered, _selected, _dimmed;

        private void Awake()
        {
            button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void SetItem(Sprite sprite, Color rarity, bool found, bool isNew)
        {
            icon.sprite = sprite;
            icon.color = found ? (_dimmed ? dimmedIcon : Color.white) : lockedIcon;
            Apply(rarity, found, isNew);
        }

        public void SetUnit(Sprite portrait, Sprite type, Color tier, bool found, bool isNew)
        {
            icon.sprite = portrait;
            icon.color = found ? Color.white : lockedPortrait;
            typeIcon.sprite = type;
            typeIcon.enabled = found && type != null;
            Apply(tier, found, isNew);
        }

        private void Apply(Color rarity, bool found, bool isNew)
        {
            Found = found;
            _restFrame = found ? new Color(rarity.r, rarity.g, rarity.b, restFrameAlpha) : lockedFrame;
            if (wash != null)
            {
                wash.enabled = found;
                wash.color = new Color(rarity.r, rarity.g, rarity.b, washAlpha);
            }
            lockIcon.SetActive(!found);
            SetNew(isNew);
            Refresh();
        }

        public void SetNew(bool isNew) => newDot.SetActive(isNew);

        public void SetSelected(bool selected)
        {
            _selected = selected;
            Refresh();
        }

        /// <summary>Greys a found item that cannot be chosen right now, such as gear the warband cannot afford.</summary>
        public void SetDimmed(bool dimmed)
        {
            _dimmed = dimmed;
            if (Found) icon.color = dimmed ? dimmedIcon : Color.white;
            Refresh();
        }

        private void Refresh()
        {
            Color rest = _dimmed && Found ? new Color(_restFrame.r, _restFrame.g, _restFrame.b, dimmedFrameAlpha) : _restFrame;
            frame.color = _selected ? selectedFrame : _hovered ? hoverFrame : rest;
            selectedGlow.SetActive(_selected);
            transform.localScale = _hovered && !_selected ? Vector3.one * hoverScale : Vector3.one;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            Refresh();
            Hovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            Refresh();
            Unhovered?.Invoke(this);
        }

        // Keyboard and controller focus has no hover, so moving onto a tile picks it.
        public void OnSelect(BaseEventData eventData)
        {
            if (eventData is PointerEventData) return;
            Clicked?.Invoke(this);
        }

        public void Focus()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(button.gameObject);
        }
    }
}
