using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TJ
{
    /// <summary>Shows a keyword's definition while the pointer rests on its link in this label. Added by KeywordText.Show.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class KeywordLinkHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        TMP_Text _text;
        Canvas _rootCanvas;
        bool _inside;
        string _shownId;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        public void OnPointerEnter(PointerEventData eventData) => _inside = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _inside = false;
            Close();
        }

        void OnDisable()
        {
            _inside = false;
            Close();
        }

        void Update()
        {
            if (!_inside) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            string id = LinkUnder(mouse.position.ReadValue());
            if (id == _shownId) return;
            if (id == null || !KeywordRegistry.TryGet(id, out Keyword keyword) || !keyword.HasDescription)
            {
                Close();
                return;
            }
            _shownId = id;
            TooltipManager.Instance.LoadToolTip(keyword.Name, KeywordText.ForTooltip(keyword.Description, keyword.Id), string.Empty);
        }

        string LinkUnder(Vector2 screenPoint)
        {
            if (_rootCanvas == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas == null) return null;
                _rootCanvas = canvas.rootCanvas;
            }
            Camera eventCamera = _rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _rootCanvas.worldCamera;
            int index = TMP_TextUtilities.FindIntersectingLink(_text, screenPoint, eventCamera);
            if (index < 0) return null;
            string linkId = _text.textInfo.linkInfo[index].GetLinkID();
            return linkId.StartsWith(KeywordText.LinkPrefix, System.StringComparison.Ordinal) ? linkId.Substring(KeywordText.LinkPrefix.Length) : null;
        }

        // Closes only a tooltip this label opened; a card's own tooltip is left alone.
        void Close()
        {
            if (_shownId == null) return;
            _shownId = null;
            if (TooltipManager.HasInstance) TooltipManager.Instance.HideTooltip();
        }
    }
}
