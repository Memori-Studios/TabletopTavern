using System;
using System.Collections.Generic;
using System.IO;
using Memori.Localization;
using Memori.Notifications;
using Memori.UI;
using TMPro;
using UnityEngine;
using TJ;

namespace TJ.MainMenu
{
    // Enable/reorder panel for installed mods, writing to Mods/modlist.json, plus publishing a
    // mod folder to the Steam Workshop. Enable/reorder changes apply on next restart (ModLoadOrder
    // is only read once, during TabletopTavernData.Awake()) - this panel does not attempt to
    // hot-reload mods mid-session. Publishing is a live network call and takes effect immediately,
    // independent of that restart requirement.
    //
    // Needs editor-side wiring: place this on a panel GameObject (with a MemoriCanvasGroup,
    // required by the MainMenuPanel base class) with modListContainer pointing at a ScrollRect's
    // Content transform, rowPrefab pointing at a ModListRow prefab (see that script), and
    // emptyStateText/restartNoticeText assigned to TMP_Text elements in the panel. moddingGuideButton
    // and steamWorkshopButton each need a Link asset assigned too (see Assets/Data/SOs/Links/ for
    // existing examples) - "Modding Guide Link"/"Steam Workshop Link" were created for this. Then
    // wire it into MainMenu the same way upgradesPanel/questsPanel are wired.
    public class ModListPanel : MainMenuPanel
    {
        [SerializeField] private Transform modListContainer;
        [SerializeField] private ModListRow rowPrefab;
        [SerializeField] private TMP_Text emptyStateText;
        [SerializeField] private TMP_Text restartNoticeText;

        [Header("External Links")]
        [SerializeField] private MemoriButtonV2 moddingGuideButton;
        [SerializeField] private Link moddingGuideLink;
        [SerializeField] private MemoriButtonV2 steamWorkshopButton;
        [SerializeField] private Link steamWorkshopLink;

        private List<ModListItem> _mods = new();
        private readonly List<ModListRow> _spawnedRows = new();
        private bool _isPublishing;

        public override void SetUp(MainMenu mainMenu)
        {
            base.SetUp(mainMenu);

            moddingGuideButton.Button.ClearClickListeners();
            moddingGuideButton.Button.onClick.AddListener(() => Application.OpenURL(moddingGuideLink.Url));

            steamWorkshopButton.Button.ClearClickListeners();
            steamWorkshopButton.Button.onClick.AddListener(() => Application.OpenURL(steamWorkshopLink.Url));
        }

        public override void OpenPanel()
        {
            base.OpenPanel();
            Refresh();
        }

        private void Refresh()
        {
            _mods = ModListManager.GetAllMods();

            foreach (var row in _spawnedRows)
            {
                if (row != null) Destroy(row.gameObject);
            }
            _spawnedRows.Clear();

            emptyStateText.text = LocalizationManager.Instance.GetText("modsEmptyState");
            emptyStateText.gameObject.SetActive(_mods.Count == 0);
            restartNoticeText.text = LocalizationManager.Instance.GetText("modsRestartNotice");
            restartNoticeText.gameObject.SetActive(_mods.Count > 0);

            for (int i = 0; i < _mods.Count; i++)
            {
                var row = Instantiate(rowPrefab, modListContainer);
                row.Setup(_mods[i], canMoveUp: i > 0, canMoveDown: i < _mods.Count - 1, OnToggled, OnMoveUp, OnMoveDown, OnPublishClicked);
                _spawnedRows.Add(row);
            }
        }

        private void OnToggled(string folder, bool isEnabled)
        {
            int index = _mods.FindIndex(m => m.folder == folder);
            if (index < 0) return;

            var mod = _mods[index];
            mod.enabled = isEnabled;
            _mods[index] = mod;

            ModListManager.SaveModList(_mods);
        }

        private void OnMoveUp(string folder)
        {
            int index = _mods.FindIndex(m => m.folder == folder);
            if (index <= 0) return;

            (_mods[index - 1], _mods[index]) = (_mods[index], _mods[index - 1]);
            ModListManager.SaveModList(_mods);
            Refresh();
        }

        private void OnMoveDown(string folder)
        {
            int index = _mods.FindIndex(m => m.folder == folder);
            if (index < 0 || index >= _mods.Count - 1) return;

            (_mods[index + 1], _mods[index]) = (_mods[index], _mods[index + 1]);
            ModListManager.SaveModList(_mods);
            Refresh();
        }

        private async void OnPublishClicked(string folder)
        {
            if (_isPublishing) return;

            int index = _mods.FindIndex(m => m.folder == folder);
            if (index < 0 || index >= _spawnedRows.Count) return;

            _isPublishing = true;
            ModListRow row = _spawnedRows[index];
            string displayName = string.IsNullOrEmpty(_mods[index].manifest?.displayName) ? folder : _mods[index].manifest.displayName;

            string busyLabel = LocalizationManager.Instance.GetText("modsPublishing");
            if (row != null)
            {
                row.SetPublishInteractable(false);
                row.SetPublishBusyText(busyLabel);
            }

            var progress = new Progress<float>(p =>
            {
                if (row != null) row.SetPublishBusyText($"{busyLabel} {p:P0}");
            });

            string modFolderPath = Path.Combine(ModLoadOrder.ModsRootPath, folder);
            bool success = await SteamWorkshopModSync.PublishOrUpdateModAsync(modFolderPath, progress);

            if (success)
            {
                NotificationManager.Instance.DisplayNotification(string.Format(LocalizationManager.Instance.GetText("modsPublishSuccess"), displayName));
            }
            else
            {
                NotificationManager.Instance.ErrorNotification(string.Format(LocalizationManager.Instance.GetText("modsPublishFailed"), displayName));
            }

            _isPublishing = false;
            Refresh();
        }
    }
}
