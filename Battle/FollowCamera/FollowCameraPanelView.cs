using TMPro;
using UnityEngine;

namespace TJ
{
    /// <summary>The follow camera's key panel in the bottom right. It only shows text; FollowCamera owns every rule.</summary>
    public class FollowCameraPanelView : MonoBehaviour
    {
        [SerializeField] private Canvas canvas;
        // The root stays active and hides by alpha, so its children are set up before the first show.
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private GameObject pausedTag;
        [SerializeField] private TMP_Text exitKey;
        [SerializeField] private TMP_Text manualKey;
        [SerializeField] private TMP_Text cycleKey;

        public Canvas Canvas => canvas;

        private void Awake() => Hide();

        public void Show(string exit, string manual, string cycle)
        {
            exitKey.text = exit;
            manualKey.text = manual;
            cycleKey.text = cycle;
            rootGroup.alpha = 1f;
        }

        public void Hide()
        {
            rootGroup.alpha = 0f;
            pausedTag.SetActive(false);
        }

        public void SetTitle(string title) => titleLabel.text = title;

        public void SetPaused(bool paused)
        {
            if (pausedTag.activeSelf != paused) pausedTag.SetActive(paused);
        }
    }
}
