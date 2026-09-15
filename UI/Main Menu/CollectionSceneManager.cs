using UnityEngine;
using Memori.Input;
using Memori.Scenes;

namespace TJ.MainMenu
{
    /// <summary>
    /// Owns the Collection overlay scene. The scene is loaded additively on top of whatever game
    /// state is running, so nothing here may touch currentGameState, Time.timeScale, or the
    /// transition door. When opened from the Settings panel mid-battle, SettingsManager still owns
    /// the pause and the Settings panel deliberately stays open underneath.
    /// </summary>
    public class CollectionSceneManager : MonoBehaviour
    {
        [SerializeField] private CollectionPanel collectionPanel;

        private bool _closing;

        private void Start()
        {
            // Start runs after every Awake in this scene, so the panel's MemoriCanvasGroup has
            // already cached its CanvasGroup. That relies on the Collection Panel GameObject
            // staying active in the scene - MemoriCanvasGroup.Awake does not run on an inactive
            // object, and CGEnable has no null guard.
            // try/finally so a throw inside SetUp can never strand the loading readout, which
            // also holds blockRaycastsDuringTransition and would otherwise lock all input.
            try
            {
                collectionPanel.SetUp(Close);
                collectionPanel.OpenPanel();

                // Esc and right-click both close the overlay; MainMenu's right-click handler
                // stands down while OverlaySceneOpen, so this is the only responder up here.
                InputHandler.Instance.SettingsButtonPressed += Close;
                InputHandler.Instance.SecondaryActionPressed += Close;
            }
            finally
            {
                SceneHandler.Instance.OverlaySceneReady();
            }
        }

        public async void Close()
        {
            if (_closing) return;
            _closing = true;

            collectionPanel.ClosePanel();
            await SceneHandler.Instance.CloseOverlayScene(SceneHandler.CollectionScenePath);
        }

        private void OnDestroy()
        {
            if (InputHandler.HasInstance)
            {
                InputHandler.Instance.SettingsButtonPressed -= Close;
                InputHandler.Instance.SecondaryActionPressed -= Close;
            }
        }
    }
}
