using System.Collections;
using Memori.Audio;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Engagement
{
    /// <summary>A spoil the player takes after a win (bounty, consumable, recruit). Every one can be taken.</summary>
    public class EngagementSpoilRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private GameObject valueGroup;
        [SerializeField] private Image valueIcon;
        [SerializeField] private TMP_Text value;
        [SerializeField] private GameObject tag;
        [SerializeField] private TMP_Text tagText;
        [SerializeField] private MemoriTooltipTrigger tooltip;
        [SerializeField] private GameObject taken;
        [SerializeField] private TMP_Text takenTitle;
        // The button sits inside this wrapper, so the pop never fights a hover scale on the button itself.
        [SerializeField] private RectTransform pop;
        [SerializeField] private CanvasGroup popGroup;
        [SerializeField] private string takeSfx = "upgrade-unlock";
        [SerializeField] private float punchScale = 1.06f;
        [SerializeField] private float punchSeconds = 0.07f;
        [SerializeField] private float vanishScale = 0.9f;
        [SerializeField] private float vanishSeconds = 0.16f;

        private Coroutine vanish;

        public Button Button => button;
        public bool IsTaken { get; private set; }

        public void Set(Sprite _icon, Color _iconColour, string _title, string _detail)
        {
            icon.sprite = _icon;
            icon.color = _iconColour;
            title.text = _title;
            detail.text = _detail;
            takenTitle.text = _title;
            valueGroup.SetActive(false);
            tag.SetActive(false);
            tooltip.enabled = false;
        }

        // Gold values carry the coin icon; other values show without it.
        public void SetValue(string _value, bool _gold)
        {
            valueGroup.SetActive(true);
            valueIcon.gameObject.SetActive(_gold);
            value.text = _value;
        }

        public void SetTag(string _text, Color _colour)
        {
            tag.SetActive(true);
            tagText.text = _text;
            tagText.color = _colour;
        }

        public void SetTooltip(string _title, string _body)
        {
            tooltip.enabled = true;
            tooltip.SetUpToolTip(_title, _body);
        }

        // A taken spoil pops and disappears, leaving the Taken line in its slot.
        public void SetTaken(bool isTaken, bool animate = false)
        {
            if (vanish != null) StopCoroutine(vanish);
            vanish = null;
            IsTaken = isTaken;
            button.interactable = !isTaken;
            taken.SetActive(isTaken);
            pop.localScale = Vector3.one;
            popGroup.alpha = 1f;
            if (isTaken && animate && isActiveAndEnabled)
            {
                pop.gameObject.SetActive(true);
                vanish = StartCoroutine(Vanish());
                return;
            }
            pop.gameObject.SetActive(!isTaken);
        }

        private IEnumerator Vanish()
        {
            IAudioRequester.Instance.PlaySFX(takeSfx);
            for (float t = 0f; t < punchSeconds; t += Time.unscaledDeltaTime)
            {
                pop.localScale = Vector3.one * Mathf.Lerp(1f, punchScale, t / punchSeconds);
                yield return null;
            }
            for (float t = 0f; t < vanishSeconds; t += Time.unscaledDeltaTime)
            {
                float k = t / vanishSeconds;
                pop.localScale = Vector3.one * Mathf.Lerp(punchScale, vanishScale, k * k);
                popGroup.alpha = 1f - k;
                yield return null;
            }
            pop.gameObject.SetActive(false);
            pop.localScale = Vector3.one;
            popGroup.alpha = 1f;
            vanish = null;
        }
    }
}
