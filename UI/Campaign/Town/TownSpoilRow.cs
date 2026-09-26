using System.Collections;
using Memori.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Town
{
    /// <summary>One reward on the sacked town panel: loot gold, loot gear or a free recruit.</summary>
    public class TownSpoilRow : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private TMP_Text value;
        [SerializeField] private GameObject taken;
        [SerializeField] private TMP_Text takenTitle;
        // The button sits inside this wrapper, so the pop never fights MemoriButtonV2's hover scale on the button itself.
        [SerializeField] private RectTransform pop;
        [SerializeField] private CanvasGroup popGroup;
        [SerializeField] private string takeSfx = "upgrade-unlock";
        [SerializeField] private float punchScale = 1.06f;
        [SerializeField] private float punchSeconds = 0.07f;
        [SerializeField] private float vanishScale = 0.9f;
        [SerializeField] private float vanishSeconds = 0.16f;

        private Coroutine vanish;

        public Button Button => button;

        public void Set(string _title, string _detail, string _value)
        {
            title.text = _title;
            detail.text = _detail;
            value.text = _value;
            takenTitle.text = _title;
        }

        // A taken reward pops and disappears, leaving the Taken line in its slot.
        public void SetTaken(bool isTaken, bool animate = false)
        {
            if (vanish != null) StopCoroutine(vanish);
            vanish = null;
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
