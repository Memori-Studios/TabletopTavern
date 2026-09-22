using UnityEngine;
using TJ.Event;
using Memori.Scenes;
using TJ.Engagement;
using TJ.Recruit;
using TJ.Town;
using TJ.Shop;
using TJ.Treasure;
using TJ.Games;
using TJ.Campfire;
using TJ.Prestige;
using Memori.Audio;
using System;
using System.Collections.Generic;

namespace TJ.Map
{
    public class MapSceneUIManager : MonoBehaviour
    {
        [Header("Node Panels")]
        [SerializeField] private EventPanel eventPanel;
        [SerializeField] private EngagementPanel engagementPanel;
        [SerializeField] private TownPanel townPanel;
        [SerializeField] private ShopPanel shopPanel;
        [SerializeField] private RecruitPanel recruitPanel;
        [SerializeField] private TreasurePanel treasurePanel;
        [SerializeField] private GamesPanel tavernPanel;
        [SerializeField] private CampfirePanel campfirePanel;
        [SerializeField] private PrestigeTraitPanel prestigeTraitPanel;

        [Header("Other Panels")]
        [SerializeField] private HUDPanel hudPanel;
        [SerializeField] private GameOverPanel gameOverPanel;
        [SerializeField] private MapEscapePanel mapEscapePanel;

        public EventPanel EventPanel => eventPanel;
        public GamesPanel GamesPanel => tavernPanel;
        public EngagementPanel EngagementPanel => engagementPanel;
        public TownPanel TownPanel => townPanel;
        public ShopPanel ShopPanel => shopPanel;
        public RecruitPanel RecruitPanel => recruitPanel;
        public HUDPanel HUDPanel => hudPanel;
        public GameOverPanel GameOverPanel => gameOverPanel;
        public TreasurePanel TreasurePanel => treasurePanel;

        [Header("Other")]
        [SerializeField] private ArmyJuiceManager armyJuiceManager;
        [SerializeField] private AutoSavingIndicator autoSavingIndicator;
        [SerializeField] private MemoriCanvasGroup legendCanvasGroup;
        public MapSceneManager MapSceneManager => mapSceneManager;
        [SerializeField] private MapIntroDisplay mapIntroDisplay;
        public MapIntroDisplay MapIntroDisplay1 => mapIntroDisplay;
        [SerializeField] private MapIntroDisplay mapIntroDisplay2;
        public MapIntroDisplay MapIntroDisplay2 => mapIntroDisplay2;
        [SerializeField] private MapIntroDisplay mapIntroDisplay3;
        public MapIntroDisplay MapIntroDisplay3 => mapIntroDisplay3;

        private MapPanel activeNodePanel;
        private MapSceneManager mapSceneManager;
        private CampaignSaveManager campaignSaveManager;
        private int layerNodeSelected = -1;
        public int LayerNodeSelected => layerNodeSelected;
        private int activeLayer = 0;
        private bool isDrainingPrestigeChoices = false;

        public void SetUp(MapSceneManager _mapSceneManager)
        {
            mapSceneManager = _mapSceneManager;
            campaignSaveManager = CampaignManager.Instance.CampaignSaveManager;
            campaignSaveManager.OnGameSaved += autoSavingIndicator.OnGameSaved;

            mapEscapePanel.SetUp(campaignSaveManager);
            hudPanel.SetUp(campaignSaveManager, this);
            eventPanel.SetUp(campaignSaveManager, this);
            engagementPanel.SetUp(campaignSaveManager, this);
            townPanel.SetUp(campaignSaveManager, this);
            shopPanel.SetUp(campaignSaveManager, this);
            recruitPanel.SetUp(campaignSaveManager, this);
            treasurePanel.SetUp(campaignSaveManager, this);
            tavernPanel.SetUp(campaignSaveManager, this);
            campfirePanel.SetUp(campaignSaveManager, this);
            prestigeTraitPanel.SetUp(campaignSaveManager, this);
            armyJuiceManager.SetUp(campaignSaveManager, this);

            legendCanvasGroup.FadeInAsync();

            // Safety net: resolve any prestige trait choice left pending from a previous session.
            TryDrainPendingPrestigeChoices();
        }

        // Checks for a unit sitting at max prestige with no trait chosen yet, and shows the picker
        // for it (recursively, one at a time) before invoking onDrained. No-ops immediately if
        // nothing is pending. See the prestige trait picker call sites for how this is used.
        public void TryDrainPendingPrestigeChoices(Action onDrained = null)
        {
            if (!campaignSaveManager.TryGetNextPendingPrestigeTraitChoice(out Memori.SaveData.SquadToLoad pending))
            {
                if (isDrainingPrestigeChoices)
                {
                    isDrainingPrestigeChoices = false;
                    mapSceneManager.SetMapInput(true);
                }
                onDrained?.Invoke();
                return;
            }

            if (!isDrainingPrestigeChoices)
            {
                isDrainingPrestigeChoices = true;
                mapSceneManager.SetMapInput(false);
            }

            List<UnitAttribute> options = campaignSaveManager.GetPrestigeTraitOptions(pending.UnitName);
            prestigeTraitPanel.LoadPrestigeTraitPanel(pending, options, () => TryDrainPendingPrestigeChoices(onDrained));
        }

        public void LoadPanelFromNode(MapNode _node)
        {
            TryDrainPendingPrestigeChoices(() => LoadPanelFromNodeInternal(_node));
        }
        private void LoadPanelFromNodeInternal(MapNode _node)
        {
            Debug.Log($"[Map] LoadPanelFromNode — index {_node.Value.index} type {_node.Value.type} layer {_node.Value.layer}");
            legendCanvasGroup.FadeOutAsync();
            layerNodeSelected = _node.Value.index;
            activeLayer = _node.Value.layer;
            mapSceneManager.FocusSelectedNode();
            IAudioRequester.Instance.PlaySFX(SFXData.FocusNode);
            IAudioRequester.Instance.SetAmbienceDuck(AmbienceDuckSource.NodePanel, true);
            IAudioRequester.Instance.SetAmbienceBed(_node.Value.type == NodeType.Town ? AmbienceBed.Village : AmbienceBed.Hall);

            switch (_node.Value.type)
            {
                case NodeType.Shop:
                    activeNodePanel = shopPanel;
                    shopPanel.LoadShopPanel();
                    break;
                case NodeType.Event:
                    activeNodePanel = eventPanel;
                    eventPanel.LoadEventPanel(_node.Value.layer);
                    break;
                case NodeType.Town:
                    activeNodePanel = townPanel;
                    townPanel.LoadTownPanel(_node.Value.index, activeLayer);
                    break;
                case NodeType.Treasure:
                    activeNodePanel = treasurePanel;
                    treasurePanel.LoadTreasurePanelFromMapNode(3, true);
                    break;
                case NodeType.Skirmish:
                    activeNodePanel = engagementPanel;
                    LoadEngagementPanel(NodeType.Skirmish);
                    break;
                case NodeType.Horde:
                    activeNodePanel = engagementPanel;
                    LoadEngagementPanel(NodeType.Horde);
                    break;
                case NodeType.Games:
                    activeNodePanel = tavernPanel;
                    tavernPanel.LoadGamesPanel();
                    break;
                case NodeType.Campfire:
                    activeNodePanel = campfirePanel;
                    campfirePanel.LoadCampfirePanel();
                    break;
                default:
                    Debug.LogError($"No node type found for {_node.Value.type}");
                    break;
            }
        }
        public void CompleteLayer()
        {
            Debug.Log($"CompleteLayer called on {activeNodePanel}");
            legendCanvasGroup.FadeInAsync();
            IAudioRequester.Instance.SetAmbienceDuck(AmbienceDuckSource.NodePanel, false);
            IAudioRequester.Instance.SetAmbienceBed(AmbienceBed.Hall);
            mapSceneManager.CompleteLayer();
            layerNodeSelected = -1;
        }
        public void CompleteHordeBattle()
        {
            activeNodePanel.ClosePanel();
            gameOverPanel.DisplayActComplete();
        }
        public void SetActivePanel(MapPanel panel) => activeNodePanel = panel;

        public void CompleteLayerAction()
        {
            Debug.Log($"[Map] CompleteLayerAction — closing {activeNodePanel}");
            activeNodePanel.ClosePanel();
            CompleteLayer();
        }
        public void StartBattleButtonClicked()
        {
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Battle);
        }
        public void LoadEngagementPanel(NodeType _nodeType)
        {
            engagementPanel.LoadEngagementPanel(_nodeType);

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.SelectToBattle);
            IAudioRequester.Instance.PlaySFX(SFXData.SelectToBattle);
        } 
        public void LoseRunFromTown()
        {
            townPanel.DisableTownCanvasesOnLoss();
            gameOverPanel.DisplayGameOver();
        }
        private void OnDestroy() 
        {
            if(campaignSaveManager != null)
                campaignSaveManager.OnGameSaved -= autoSavingIndicator.OnGameSaved;
        }
    }
}