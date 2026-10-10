using System;
using System.IO;
using Memori.Localization;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ
{
    // One row in the mod list panel - displays a single mod's manifest info and lets the player
    // toggle it on/off, move it up/down in load order, and publish/update it to the Steam
    // Workshop. Needs a prefab built in the editor: a horizontal row with
    // nameText/versionAuthorText (TMP_Text), enabledToggle (Toggle), moveUpButton/moveDownButton
    // (Button), publishButton (MemoriButtonV2) with a publishButtonText (TMP_Text) child,
    // previewImage (Image) for the mod's optional preview.png thumbnail, and loadedThisSessionIcon
    // (a GameObject, e.g. a small indicator dot) shown only when the mod was actually loaded at
    // boot, all assigned in the Inspector.
    public class ModListRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text versionAuthorText;
        [SerializeField] private Toggle enabledToggle;
        [SerializeField] private TMP_Text enabledToggleStatusText;
        [SerializeField] private Button moveUpButton;
        [SerializeField] private Button moveDownButton;
        [SerializeField] private MemoriButtonV2 publishButton;
        [SerializeField] private TMP_Text publishButtonText;
        [SerializeField] private Image previewImage;
        [SerializeField] private GameObject loadedThisSessionIcon;

        private const float ConfirmWindowSeconds = 3f;
        private const string PreviewFileName = "preview.png";

        private string _folder;
        private string _publishLabel;
        private bool _pendingConfirm;
        private Action<string> _onPublish;
        private Texture2D _previewTexture;
        private Sprite _previewSprite;

        public void Setup(ModListItem item, bool canMoveUp, bool canMoveDown,
            Action<string, bool> onToggled, Action<string> onMoveUp, Action<string> onMoveDown, Action<string> onPublish)
        {
            _folder = item.folder;
            _onPublish = onPublish;

            nameText.text = string.IsNullOrEmpty(item.manifest?.displayName) ? item.folder : item.manifest.displayName;
            string author = item.manifest?.author;
            string version = item.manifest?.version;
            versionAuthorText.text = string.IsNullOrEmpty(author) ? version : $"{version} - {author}";

            enabledToggle.SetIsOnWithoutNotify(item.enabled);
            UpdateEnabledToggleStatusText(item.enabled);
            enabledToggle.onValueChanged.RemoveAllListeners();
            enabledToggle.onValueChanged.AddListener(isOn =>
            {
                UpdateEnabledToggleStatusText(isOn);
                onToggled(_folder, isOn);
            });

            moveUpButton.interactable = canMoveUp;
            moveUpButton.ClearClickListeners();
            moveUpButton.onClick.AddListener(() => onMoveUp(_folder));

            moveDownButton.interactable = canMoveDown;
            moveDownButton.ClearClickListeners();
            moveDownButton.onClick.AddListener(() => onMoveDown(_folder));

            loadedThisSessionIcon.SetActive(ModLoadOrder.LoadedFolderNamesThisSession.Contains(item.folder));
            Debug.Log($"ModListRow.Setup: loadedThisSessionIcon.SetActive({loadedThisSessionIcon.activeSelf}) for folder '{item.folder}'");

            SetUpPublishButton(item);
            SetUpPreviewImage(item.folder);
        }

        private void UpdateEnabledToggleStatusText(bool isOn)
        {
            enabledToggleStatusText.text = LocalizationManager.Instance.GetText(isOn ? "settingsOn" : "settingsOff");
        }

        private void SetUpPreviewImage(string folder)
        {
            ClearPreviewImage();

            string previewPath = Path.Combine(ModLoadOrder.ModsRootPath, folder, PreviewFileName);
            if (!File.Exists(previewPath))
            {
                previewImage.gameObject.SetActive(false);
                return;
            }

            byte[] bytes = File.ReadAllBytes(previewPath);
            _previewTexture = new Texture2D(2, 2);
            if (!_previewTexture.LoadImage(bytes))
            {
                Destroy(_previewTexture);
                _previewTexture = null;
                previewImage.gameObject.SetActive(false);
                return;
            }

            _previewSprite = Sprite.Create(_previewTexture, new Rect(0, 0, _previewTexture.width, _previewTexture.height), new Vector2(0.5f, 0.5f));
            previewImage.sprite = _previewSprite;
            previewImage.gameObject.SetActive(true);
        }

        private void ClearPreviewImage()
        {
            if (_previewSprite != null) Destroy(_previewSprite);
            if (_previewTexture != null) Destroy(_previewTexture);
            _previewSprite = null;
            _previewTexture = null;
        }

        private void OnDestroy() => ClearPreviewImage();

        private void SetUpPublishButton(ModListItem item)
        {
            _pendingConfirm = false;
            CancelInvoke(nameof(RevertConfirm));

            if (SteamWorkshopModSync.IsWorkshopSyncedFolder(item.folder))
            {
                publishButton.gameObject.SetActive(false);
                return;
            }

            publishButton.gameObject.SetActive(true);
            publishButton.Button.interactable = true;

            bool alreadyPublished = !string.IsNullOrEmpty(item.manifest?.workshopFileId);
            _publishLabel = LocalizationManager.Instance.GetText(alreadyPublished ? "modsUpdateButton" : "modsPublishButton");
            publishButtonText.text = _publishLabel;

            publishButton.Button.ClearClickListeners();
            publishButton.Button.onClick.AddListener(OnPublishButtonClicked);
        }

        private void OnPublishButtonClicked()
        {
            if (_pendingConfirm)
            {
                CancelInvoke(nameof(RevertConfirm));
                _pendingConfirm = false;
                _onPublish(_folder);
                return;
            }

            _pendingConfirm = true;
            publishButtonText.text = LocalizationManager.Instance.GetText("modsPublishConfirm");
            Invoke(nameof(RevertConfirm), ConfirmWindowSeconds);
        }

        private void RevertConfirm()
        {
            _pendingConfirm = false;
            publishButtonText.text = _publishLabel;
        }

        public void SetPublishInteractable(bool interactable) => publishButton.Button.interactable = interactable;

        public void SetPublishBusyText(string text) => publishButtonText.text = text;
    }
}
