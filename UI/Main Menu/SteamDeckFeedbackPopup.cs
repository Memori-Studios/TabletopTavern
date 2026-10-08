using System.Collections;
using Memori.Audio;
using Memori.Scenes;
using Memori.Steamworks;
using Memori.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    // Asks Steam Deck players once for feedback on Discord. The root stays active and hides by alpha.
    public class SteamDeckFeedbackPopup : MonoBehaviour
    {
        public const string ShownKey = "steam_deck_feedback_shown";
        // Above the roadmap pop-up (6), below Settings (105).
        private const int SortingOrder = 7;
        // Lets the doors open and the title settle before the popup asks for attention.
        private const float ShowDelay = 1.5f;

        [SerializeField] private Canvas canvas;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button notNowButton;
        [SerializeField] private Link discordLink;

        private bool _isRunning;
        private Coroutine _motion;
        private GameObject _selectedBefore;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            // A builder-made nested canvas saves without its sorting override.
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            SetInteractive(false);
            group.alpha = 0f;
            joinButton.onClick.AddListener(OnJoin);
            notNowButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            joinButton.onClick.RemoveListener(OnJoin);
            notNowButton.onClick.RemoveListener(Close);
        }

        #region Show rule
        // Called when the main menu opens; does nothing off the Deck or once the popup has been seen.
        public void TryShow()
        {
            if (_isRunning || IsOpen || !UIScaler.IsSteamDeck || PlayerPrefs.GetInt(ShownKey, 0) == 1) return;
            StartCoroutine(ShowWhenMenuIsIdle());
        }

        private IEnumerator ShowWhenMenuIsIdle()
        {
            _isRunning = true;
            yield return new WaitForSecondsRealtime(ShowDelay);
            while (!MenuIsIdle())
            {
                // Live read: the player may have left for the map or a battle while waiting.
                if (SceneHandler.Instance.CurrentGameState != GameStateEnum.MainMenu)
                {
                    _isRunning = false;
                    yield break;
                }
                yield return null;
            }
            _isRunning = false;
            Show();
        }

        private bool MenuIsIdle()
        {
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.MainMenu) return false;
            if (SceneHandler.Instance.OverlaySceneOpen || SettingsManager.Instance.SettingsPanelOpen) return false;
            CanvasGroup menuPanel = transform.parent != null ? transform.parent.GetComponentInParent<CanvasGroup>() : null;
            return menuPanel == null || menuPanel.alpha > 0.99f;
        }
        #endregion

        #region Open and close
        [ContextMenu("Show Steam Deck popup")]
        private void Show()
        {
            // Saved on show, so a quit or crash with the popup up never brings it back.
            PlayerPrefs.SetInt(ShownKey, 1);
            PlayerPrefs.Save();

            IsOpen = true;
            group.blocksRaycasts = true;
            ContainedNavigation.Attach(panel.gameObject).enabled = true;
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
            Play(OpenRoutine());
        }

        private IEnumerator OpenRoutine()
        {
            EventSystem es = EventSystem.current;
            _selectedBefore = es != null ? es.currentSelectedGameObject : null;
            yield return UIJuice.Open(group, panel);
            group.interactable = true;
            if (es != null) es.SetSelectedGameObject(joinButton.gameObject);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            SetInteractive(false);
            ContainedNavigation nav = panel.GetComponent<ContainedNavigation>();
            if (nav != null) nav.enabled = false;

            EventSystem es = EventSystem.current;
            if (es != null && _selectedBefore != null && _selectedBefore.activeInHierarchy)
                es.SetSelectedGameObject(_selectedBefore);
            Play(UIJuice.Close(group));
        }

        private void OnJoin()
        {
            if (!IsOpen) return;
            group.interactable = false;
            SteamStatic.OpenWebPage(discordLink.Url);
            Play(JoinRoutine());
        }

        // The punch lands on the button's wrapper; MemoriButtonV2 owns the button's own scale.
        private IEnumerator JoinRoutine()
        {
            yield return UIJuice.Punch(joinButton.transform.parent);
            Close();
        }

        private void SetInteractive(bool on)
        {
            group.interactable = on;
            group.blocksRaycasts = on;
        }

        private void Play(IEnumerator routine)
        {
            if (_motion != null) StopCoroutine(_motion);
            _motion = StartCoroutine(routine);
        }
        #endregion

        // The game binds no gamepad back button, so B is read here; Esc and right-click arrive through MainMenu.
        private void Update()
        {
            if (!IsOpen || !group.interactable) return;
            Gamepad pad = Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) Close();
        }
    }
}
