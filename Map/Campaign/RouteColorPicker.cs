using Memori.Audio;
using Memori.Localization;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Map
{
    /// <summary>The wheel on the free camera overlay where the player picks the colour of their route marks.</summary>
    public class RouteColorPicker : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text hintText;

        [Header("Wheel")]
        // One per RouteMarkColors.Palette entry, in the same order.
        [SerializeField] private Button[] swatches;
        [SerializeField] private Image[] swatchFills;
        [SerializeField] private RectTransform selectedRing;

        [Header("Open")]
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform body;

        private Coroutine _open, _punch;

        private void Awake()
        {
            titleText.text = LocalizationManager.Instance.GetText("RouteColor");
            hintText.text = InputText.Get("RouteMarkHint");
            int count = Mathf.Min(swatches.Length, RouteMarkColors.Palette.Length);
            if (count != RouteMarkColors.Palette.Length) Debug.LogError($"[RouteColorPicker] {swatches.Length} swatches for {RouteMarkColors.Palette.Length} colours");
            for (int i = 0; i < count; i++)
            {
                int index = i;
                swatchFills[i].color = RouteMarkColors.Palette[i];
                swatches[i].onClick.AddListener(() => Pick(index));
            }
        }

        private void OnEnable()
        {
            Show();
            if (_open != null) StopCoroutine(_open);
            _open = StartCoroutine(UIJuice.Open(group, body));
        }

        private void Pick(int index)
        {
            if (index == RouteMarkColors.Index) return;
            RouteMarkColors.Index = index;
            IAudioRequester.Instance.PlaySFX(SFXData.SelectCard);
            Show();
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(UIJuice.Punch(swatches[index].transform, 1.15f));
        }

        private void Show()
        {
            int index = RouteMarkColors.Index;
            if (index >= swatches.Length) return;
            selectedRing.anchoredPosition = ((RectTransform)swatches[index].transform).anchoredPosition;
        }
    }
}
