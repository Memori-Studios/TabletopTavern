using Memori.Tooltip;
using UnityEngine;
using TMPro;
using Memori.SaveData;
using Memori.Steamworks;
using Memori.Metaprogression;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Memori.Notifications;
using Memori.Audio;
using Memori.Localization;
using Memori.Scenes;
using System.Threading.Tasks;
using UnityEngine.Serialization;


namespace TJ.MainMenu
{
    public class UpgradesPanel : MainMenuPanel
    {
        [Header("Main Menu")]
        [SerializeField] private GameObject upgradesAvailableIndicator;

        [Header("Metaprogression Scene")]
        [SerializeField] private Camera _metaprogressionCamera;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private Transform cameraSceneParent;

        [Header("UI")]
        [FormerlySerializedAs("_depositedGoldText")]
        [SerializeField] private TMP_Text _renownText;
        [SerializeField] private MetaprogressionManager _metaprogressionManager;
        [SerializeField] private Button _resetButton, _depositButton;
        [SerializeField] private TooltipDropdown _tavernThemeDropdown;
        [SerializeField] private List<TavernThemeData> _tavernThemes;
        [SerializeField] private MemoriTooltipTrigger _tavernTooltipTrigger;

        List<MetaprogressionModel> _unlockedNodes = new();
        MetaprogressionPresenter _selectedNode;
        // Index 0 is always "None"; subsequent entries map 1:1 to _tavernThemes
        int _lastValidThemeIndex = 0;
        int _renownAvailable = 0;
        bool _isOpen = false;

        private void Start()
        {
            _metaprogressionCamera.enabled = false;
            cameraSceneParent.gameObject.SetActive(false);
            _resetButton.onClick.AddListener(ResetMetaprogression);
            _depositButton.onClick.AddListener(OverrideAddRenown);
            _tavernThemeDropdown.onValueChanged.AddListener(OnTavernThemeChanged);

            List<int> unlockedNodeIds = SaveDataHandler.GetUnlockedMetaprogressionNodes();
            _unlockedNodes = GetUnlockedNodesFromIds(unlockedNodeIds);
            CheckForAvailableUpgrades();

            _tavernTooltipTrigger.SetUpToolTip(
                LocalizationManager.Instance.GetText("TavernThemeTooltipTitle"),
                LocalizationManager.Instance.GetText("TavernThemeTooltipDescription")
            );
        }
        void Update()
        {
            if(!_isOpen) return;

            if (EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = _metaprogressionCamera.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                GameObject objectHit = hit.transform.gameObject;
                MetaprogressionPresenter presenter = objectHit.GetComponentInParent<MetaprogressionPresenter>();

                if (presenter != null)
                {
                    if(_selectedNode != presenter)
                    {
                        _selectedNode = presenter;
                        IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);

                        string costText = BuildNodeCostText(presenter);
                        int nodeValue = presenter.MetaprogressionModel.NodeValue;
                        string nodeName = LocalizationManager.Instance.GetText($"metaprogressionModel{presenter.MetaprogressionModel.NodeId}") + (nodeValue != 0 ? $" {nodeValue}" : "") + (presenter.MetaprogressionModel.AddGoldSprite ? " <sprite name=GoldSprite>" : "");
                        
                        _selectedNode.MouseOverHighlight(true);
                        TooltipManager.Instance.LoadToolTip(
                            nodeName,
                            costText,
                            ""
                        );
                    }
                }
                else
                {
                    if(_selectedNode != null)
                        _selectedNode.MouseOverHighlight(false);
                        
                    _selectedNode = null;
                    TooltipManager.Instance.HideTooltip();
                }
            }
            else
            {
                if(_selectedNode != null)
                    _selectedNode.MouseOverHighlight(false);

                _selectedNode = null;
                TooltipManager.Instance.HideTooltip();
            }

            // On left mouse click, attempt to purchase unlock
            if (Input.GetMouseButtonDown(0))
            {
                PurchaseUnlockNode();
            }
        }
        /// <summary>
        /// The body of a node's hover tooltip: its price, or why it cannot be bought.
        ///
        /// A gated node shows only "Not yet available." - quoting a price for something no amount of
        /// Renown can buy reads as a bug. The key is shared with the spell grimoire's unobtainable
        /// row rather than duplicated: the sentence is generic and already translated everywhere, and
        /// the two surfaces are saying the same thing.
        /// </summary>
        private string BuildNodeCostText(MetaprogressionPresenter _presenter)
        {
            if(_presenter.MetaprogressionModel.ComingSoon)
                return $"<color={ColorData.Error}>{LocalizationManager.Instance.GetText("SpellLockedUnknownDesc")}</color>";

            bool parentIsUnlocked = true;
            if(_presenter.ParentPresenter != null) parentIsUnlocked = _presenter.ParentPresenter.IsUnlocked;

            string costText = parentIsUnlocked ? "" : $"<color={ColorData.Error}>{LocalizationManager.Instance.GetText("upgradesLockedRequiresPreviousNode")}</color>\n";
            return costText + LocalizationManager.Instance.GetText("Cost") + ": " + _presenter.MetaprogressionModel.NodeCost.ToString();
        }
        public void PurchaseUnlockNode()
        {
            if(_selectedNode == null) return;

            if(_selectedNode.MetaprogressionModel.ComingSoon)
            {
                NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("SpellLockedUnknownDesc"));
                return;
            }

            if(_unlockedNodes.Contains(_selectedNode.MetaprogressionModel))
            {
                NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("upgradesNodeAlreadyUnlocked"));
                return;
            }

            if(_selectedNode.ParentPresenter != null && !_unlockedNodes.Contains(_selectedNode.ParentPresenter.MetaprogressionModel))
            {
                NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("upgradesRequiresPreviousNodeUnlock"));
                return;
            }

            if(_renownAvailable < _selectedNode.MetaprogressionModel.NodeCost)
            {
                NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("upgradesInsufficientRenown"));
                return;
            }

            SaveDataHandler.UnlockMetaprogressionNode(_selectedNode.MetaprogressionModel);
            List<int> unlockedNodeIds = SaveDataHandler.GetUnlockedMetaprogressionNodes();
            _unlockedNodes = GetUnlockedNodesFromIds(unlockedNodeIds);
            _selectedNode.Unlock(false);
            IAudioRequester.Instance.PlaySFX(SFXData.SelectHero);
            CalculateRenownSpent();
            _metaprogressionManager.HighlightAvailableUpgrades(_unlockedNodes, _renownAvailable);
            CheckMetaprogressionComplete();
        }
        /// <summary>
        /// "Legend of the Tavern" - every node in the Renown tree unlocked. Walks the tree rather than
        /// comparing counts, so a node appearing twice in the topology cannot unlock this early.
        ///
        /// Gated nodes are excluded, because requiring one no player can buy would make the
        /// achievement unobtainable for the whole patch. The tradeoff is deliberate: someone who
        /// clears the tree now keeps the achievement after the gated branch ships, since Steam
        /// achievements are never revoked.
        /// </summary>
        private void CheckMetaprogressionComplete()
        {
            MetaprogressionTreeModel treeModel = _metaprogressionManager.MetaprogressionTreeModel;

            // Counted rather than taken from the array length: an empty tree, or one that is entirely
            // gated, would otherwise satisfy the loop below and unlock immediately.
            int nodesRequired = 0;
            foreach(ChildParentPair pair in treeModel.MetaProgressionTree)
            {
                if(pair.Child == null) continue;
                if(pair.Child.ComingSoon) continue;

                nodesRequired++;
                if(!_unlockedNodes.Contains(pair.Child)) return;
            }
            if(nodesRequired == 0) return;

            SteamAchievements.Unlock(AchievementId.LegendOfTheTavern);
        }
        private void CalculateRenownSpent()
        {
            List<int> unlockedNodeIds = SaveDataHandler.GetUnlockedMetaprogressionNodes();
            _unlockedNodes = GetUnlockedNodesFromIds(unlockedNodeIds);
            int totalSpent = 0;
            foreach(MetaprogressionModel node in _unlockedNodes)
            {
                if(node == null) continue;
                totalSpent += node.NodeCost;
            }
            int renown = SaveDataHandler.GetRenown();
            _renownAvailable = renown - totalSpent;
            _renownText.text =  $"{_renownAvailable}/{renown}";
        }
        public override async void OpenPanel()
        {
            SceneHandler.Instance.TranstionCameras(_mainCamera, _metaprogressionCamera);
            await Task.Delay(500);
            cameraSceneParent.gameObject.SetActive(true);
            this.gameObject.SetActive(true);
            _isOpen = true;
            SetUpTavernThemeDropdown();
            DisplayNodes();
            base.OpenPanel();
        }
        [ContextMenu("Display Nodes")]
        private void DisplayNodes() 
        {
            List<int> unlockedNodeIds = SaveDataHandler.GetUnlockedMetaprogressionNodes();
            _unlockedNodes = GetUnlockedNodesFromIds(unlockedNodeIds);
            _metaprogressionManager.DisplayNodes(_unlockedNodes);
            _metaprogressionManager.HighlightAvailableUpgrades(_unlockedNodes, SaveDataHandler.GetRenown());
            CalculateRenownSpent();
            // Backstop, in the same spirit as the collection achievements re-evaluating on panel open.
            // Purchasing used to be the only trigger, which gating breaks: a player holding every
            // buyable node has nothing left to click, so the completion check would never run again.
            CheckMetaprogressionComplete();
        }
        public override async void ClosePanel()
        {
            TooltipManager.Instance.HideTooltip();
            SceneHandler.Instance.TranstionCameras(_metaprogressionCamera, _mainCamera);
            await Task.Delay(500);
            cameraSceneParent.gameObject.SetActive(false);
            _isOpen = false;
            CheckForAvailableUpgrades();
            base.ClosePanel();
            this.gameObject.SetActive(false);
        }
        private void ResetMetaprogression()
        {
            SaveDataHandler.ResetMetaprogression();
            DisplayNodes();
        }
        private void OverrideAddRenown()
        {
            PlayerSaveData playerSaveData = SaveDataHandler.LoadPlayerSaveData();
            playerSaveData.renown += 100;
            SaveDataHandler.SavePlayerSaveData(playerSaveData);
            CalculateRenownSpent();
        }
        private void CheckForAvailableUpgrades()
        {
            List<int> unlockedNodeIds = SaveDataHandler.GetUnlockedMetaprogressionNodes();
            List<MetaprogressionModel> unlockedNodes = GetUnlockedNodesFromIds(unlockedNodeIds);
            MetaprogressionTreeModel treeModel = _metaprogressionManager.MetaprogressionTreeModel;

            int renown = SaveDataHandler.GetRenown();
            int totalSpent = 0;
            foreach(MetaprogressionModel node in _unlockedNodes)
            {
                if(node == null) continue;
                totalSpent += node.NodeCost;
            }

            foreach(ChildParentPair pair in treeModel.MetaProgressionTree)
            {
                MetaprogressionModel node = pair.Child;
                if(unlockedNodes.Contains(node)) continue;

                //a gated node can never be bought, so it must not light the main-menu indicator
                if(node.ComingSoon) continue;

                //check if parent is unlocked
                if(pair.Parent != null && !unlockedNodes.Contains(pair.Parent)) continue;

                //check if enough renown to unlock
                if(renown - totalSpent >= node.NodeCost)
                {
                    // Debug.Log($"Upgrade available: {node.NodeId} with cost {node.NodeCost}");
                    upgradesAvailableIndicator.SetActive(true);
                    return;
                }
            }
            upgradesAvailableIndicator.SetActive(false);
            // Debug.Log("No upgrades available");
        }
        private List<MetaprogressionModel> GetUnlockedNodesFromIds(List<int> unlockedNodeIds)
        {
            List<MetaprogressionModel> unlockedNodes = new List<MetaprogressionModel>();
            MetaprogressionTreeModel treeModel = _metaprogressionManager.MetaprogressionTreeModel;

            foreach(ChildParentPair pair in treeModel.MetaProgressionTree)
            {
                if(unlockedNodeIds.Contains(pair.Child.NodeId))
                {
                    unlockedNodes.Add(pair.Child);
                }
            }

            return unlockedNodes;
        }
        private void SetUpTavernThemeDropdown()
        {
            // Re-evaluated on every open so a Godking run finished this session is reflected here
            // without needing a game restart, and so already-affected saves repair themselves.
            SaveDataHandler.RefreshTavernThemeUnlocks();

            _tavernThemeDropdown.options.Clear();

            foreach (TavernThemeData theme in _tavernThemes)
            {
                // if(theme.Race == Race.Special) 
                //     continue;

                bool unlocked = SaveDataHandler.IsTavernThemeUnlocked(theme.Race);
                string localizedRace = theme.Race == Race.Special
                    ? LocalizationManager.Instance.GetText("None")
                    : LocalizationManager.Instance.GetText(theme.Race.ToString());
                string label = unlocked ? localizedRace : $"<color=red>{localizedRace}</color>";
                _tavernThemeDropdown.options.Add(new TMP_Dropdown.OptionData(label));
            }

            int startIndex = _tavernThemes.FindIndex(t => t.Race == Race.Special);
            if (startIndex < 0) startIndex = 0;
            if (SaveDataHandler.TryGetActiveTavernTheme(out Race savedRace))
            {
                int themeIndex = _tavernThemes.FindIndex(t => t.Race == savedRace);
                if (themeIndex >= 0)
                    startIndex = themeIndex;
            }

            _lastValidThemeIndex = startIndex;
            _tavernThemeDropdown.SetValueWithoutNotify(startIndex);
            _tavernThemeDropdown.RefreshShownValue();
        }
        private void OnTavernThemeChanged(int _index)
        {
            TavernThemeData selected = _tavernThemes[_index];
            if (!SaveDataHandler.IsTavernThemeUnlocked(selected.Race))
            {
                NotificationManager.Instance.DisplayNotification(
                    LocalizationManager.Instance.GetText("TavernThemeTooltipDescription")
                );
                _tavernThemeDropdown.SetValueWithoutNotify(_lastValidThemeIndex);
                _tavernThemeDropdown.RefreshShownValue();
                return;
            }

            _lastValidThemeIndex = _index;
            if (selected.Race == Race.Special)
                SaveDataHandler.ClearActiveTavernTheme();
            else
                SaveDataHandler.SetActiveTavernTheme(selected.Race);

            TavernThemeManager.Instance.ApplyTheme(selected);
        }
    }
}
