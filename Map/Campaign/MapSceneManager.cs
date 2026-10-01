using UnityEngine;
using Memori.Scenes;
using Memori.Tooltip;
using System.Collections.Generic;
using Memori.Audio;
using Memori.Input;
using System.Collections;
using Memori.Localization;
using Memori.SaveData;
using TabletopTavern.Analytics;
namespace TJ.Map
{
    public class MapSceneManager : MonoBehaviour
    {
        [SerializeField] private bool allowMapInput = false;
        public bool AllowMapInput => allowMapInput;
        // Holds map input off from the act's first load until its Ordeal is taken.
        private bool ordealPickPending;
        [SerializeField] MapSceneUIManager mapSceneUIManager;
        [SerializeField] private MapGenerator mapGenerator;
        
        [Header("Map Scene Camera")]
        [SerializeField] private Camera mapScecneCamera;
        [SerializeField] private MapCamera mapCamera;
        public Camera MapScecneCamera => mapScecneCamera;
        AudioListener mapSceneAudioListener;

        [Header("Map Objects")]
        [SerializeField] private MapNode hoveredNode;
        [SerializeField] private MapNode selectedNode;
        [SerializeField] private int activeChapterIndex;
        [SerializeField] private List<MapLayer> mapLayers = new();
        public List<MapLayer> MapLayers => mapLayers;

        [Header("Player Token")]
        [SerializeField] private PlayerToken playerToken;

        [Header("Map Race")]
        public Race MapRace => mapGenerator.MapRace;
        public MapNodeData SelectedNodeData => selectedNode.Value;

        // CampaignSaveManager campaignSaveManager;

        private void Start()
        {
            mapSceneAudioListener = mapScecneCamera.GetComponent<AudioListener>();
            SceneHandler.Instance.OnGameStateChanged += OnGameStateChanged;
            InputHandler.Instance.PrimaryActionPerformed += LeftClick;
            InputHandler.Instance.SecondaryActionPressed += RightClick;
            Memori.Utilities.ColorVision.Changed += ApplyRoutePlan;
            RouteMarkColors.Changed += ApplyRoutePlan;
        }
        private void OnGameStateChanged(GameStateEnum gameStateEnum)
        {
            Debug.Log($"Map scene game state changed to {gameStateEnum}");
            // Debug.Log($"Previous game state was {SceneHandler.Instance.PreviousGameState}");
            if(CampaignManager.InstanceIfExists == null)
            {
                Debug.LogError("CampaignManager.Instance is null");
                return;
            }
            CampaignManager.Instance.CampaignSaveManager.Init(SceneHandler.Instance.PreviousGameState);//set up savedata

            mapSceneUIManager.SetUp(this);
            CampaignManager.Instance.CampaignSaveManager.Load();//invoke to update all UI elements
            mapCamera.SetUp(this);

            mapScecneCamera.enabled = gameStateEnum.Equals(GameStateEnum.Map);
            mapSceneAudioListener.enabled = gameStateEnum.Equals(GameStateEnum.Map);
            mapGenerator.LoadMap(CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber);
        }
        public void CompleteLoad()
        {
            activeChapterIndex = CampaignManager.Instance.CampaignSaveManager.SaveData.activeMapLayer;
            Debug.Log($"Loading map scene with active chapter index: {activeChapterIndex}");
            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            ordealPickPending = activeChapterIndex == -1 && !save.battleCompleted && save.OrdealPickDue;
            ResetAllNodes();
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.nodesRevealed)
                RevealNodesVisually(-1, mapLayers.Count);
            playerToken.LoadHero();
            CampaignManager.Instance.GearManager.LoadAllGear();

            int selectedNodeID = CampaignManager.Instance.CampaignSaveManager.SaveData.GetSelectedNodeIndex();
        
            void LoadPostBattle()
            {
                // Debug.Log($"post battle load of map scene");
                // Debug.Log($"Loading node {selectedNodeID} on layer {activeChapterIndex} and battle completed {campaignSaveManager.SaveData.battleCompleted}");
                selectedNode = mapLayers[activeChapterIndex+1].LayerNodes.Find(x => x.index == selectedNodeID).mapNodeGameObject;
                if(selectedNode == null) 
                {
                    Debug.LogError($"Selected node {selectedNodeID} not found on layer {activeChapterIndex+1}");
                    selectedNode = mapLayers[activeChapterIndex+1].LayerNodes[0].mapNodeGameObject;
                }
                UpdateNodePathFromSave();
                UpdateNodePathPostBattle();
                mapSceneUIManager.LoadPanelFromNode(selectedNode);
                SetMapInput(true);
                playerToken.transform.position = selectedNode.transform.position;
                FocusSelectedNode();
                mapSceneUIManager.HUDPanel.HudAnimator.Play("HUD Open");
            }

            void InitialLoad()
            {
                // Debug.Log($"Initial load of map scene");
                HandleIntro();
                SelectNextLayer();
            }

            void SnapshotLoad()
            {
                Debug.Log($"Snapshot load of map scene activeChapterIndex: {activeChapterIndex}");
                List<int> nodePath = CampaignManager.Instance.CampaignSaveManager.SaveData.nodePath;

                // Find the pivot: the last nodePath entry that lives on mapLayers[activeChapterIndex].
                // This is the last *completed* node on the current layer — SelectNextLayer uses it
                // to mark the correct layer-(N+1) nodes as selectable.
                selectedNode = null;
                for (int ni = nodePath.Count - 1; ni >= 0 && selectedNode == null; ni--)
                {
                    selectedNode = mapLayers[activeChapterIndex].LayerNodes
                        .Find(x => x.index == nodePath[ni]).mapNodeGameObject;
                }

                // Place the token at the pre-selected next node if one exists, else at the pivot.
                int tokenNodeId = selectedNodeID != -1 ? selectedNodeID : (selectedNode != null ? selectedNode.Value.index : -1);
                if (tokenNodeId != -1)
                {
                    bool placed = false;
                    for (int i = 0; i < mapLayers.Count && !placed; i++) {
                        for (int j = 0; j < mapLayers[i].LayerNodes.Count && !placed; j++) {
                            if (mapLayers[i].LayerNodes[j].index == tokenNodeId) {
                                playerToken.transform.position = mapLayers[i].LayerNodes[j].mapNodeGameObject.transform.position;
                                placed = true;
                            }
                        }
                    }
                }

                SelectNextLayer();
                UpdateNodePathFromSave();
                mapSceneUIManager.HUDPanel.HudAnimator.Play("HUD Open");
            }

            // A node with a locked roll reopens on it, so the player cannot walk away from a result they have seen.
            void LoadNodeResume()
            {
                SnapshotLoad();
                int resumeIndex = save.nodeResume.nodeIndex;
                MapNode resumeNode = mapLayers[activeChapterIndex + 1].LayerNodes.Find(x => x.index == resumeIndex).mapNodeGameObject;
                if (resumeNode == null)
                {
                    Debug.LogError($"[Map] Locked node {resumeIndex} not found on layer {activeChapterIndex + 1}; dropping the lock");
                    save.nodeResume = default;
                    return;
                }
                selectedNode = resumeNode;
                hoppingArrived = true;
                playerToken.transform.position = resumeNode.transform.position;
                resumeNode.NodeClicked();
                DeselectOtherNodesOnLayer();
                UpdateNodePath();
                FocusSelectedNode();
                mapSceneUIManager.LoadPanelFromNode(resumeNode);
            }

            if (save.battleCompleted) LoadPostBattle();
            else if (save.nodeResume.active && activeChapterIndex >= 0) LoadNodeResume();
            else if (activeChapterIndex == -1) InitialLoad();
            else SnapshotLoad();

            //check if map scene is the override
            if(SceneHandler.Instance.EditorOverride == SceneHandler.EditorOverrides.Map && activeChapterIndex >= 0 && !CampaignManager.Instance.CampaignSaveManager.SaveData.battleCompleted && !save.nodeResume.active)
            {
                // Debug.Log($"Map scene loaded from editor override");
                SnapshotLoad();
            }

            MapRoutePlan.Prune(RouteMarks(), LayerOfNode, ReachedLayer());
            ApplyRoutePlan();
            SetMapInput(true);
        }
        public void Update()
        {
            if(!CanHoverNodes) return;

            if(SettingsManager.Instance.SettingsPanelOpen) return;

            //if mouse over ui, return
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) {
                return;
            }

            //if mouse hits node, hover it
            Ray ray = mapScecneCamera.ScreenPointToRay(InputHandler.Instance.MousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                if (hit.transform.GetComponent<MapNode>())
                {
                    MapNode mapNode = hit.transform.GetComponent<MapNode>();
                    if (hoveredNode != null && hoveredNode != mapNode){
                        hoveredNode.HoverNode(false);
                    }
                    if (hoveredNode != mapNode) {
                        hoveredNode = mapNode;
                        hoveredNode.HoverNode(true);
                    }
                } else {
                    if (hoveredNode != null){
                        hoveredNode.HoverNode(false);
                        hoveredNode = null;
                    }
                }
            }
        }
        public void LeftClick()
        {
            if (UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) {
                return;
            }
            if (!AllowMapInput) return;

            if (hoveredNode != null)
            {
                if (!hoveredNode.Selectable)
                {
                    // Debug.Log($"[Map] Click blocked — node {hoveredNode.Value.index} ({hoveredNode.Value.type}) is not selectable");
                    return;
                }

                if (mapSceneUIManager.LayerNodeSelected != -1)
                {
                    // Debug.Log($"[Map] Click blocked — layer already has selected node {mapSceneUIManager.LayerNodeSelected}");
                    return;
                }

                // Debug.Log($"[Map] Click accepted — node {hoveredNode.Value.index} ({hoveredNode.Value.type}) layer {hoveredNode.Value.layer}");
                SelectNode(hoveredNode);
            }
        }
        #region Route Marks
        // The free camera turns map input off, but it is where the player looks over the act, so marking stays on there.
        private bool CanHoverNodes => AllowMapInput || mapCamera.IsFreeCameraMode;
        /// <summary>Whether a right-click now would mark or clear the hovered node.</summary>
        public bool CanMarkHoveredNode =>
            CanHoverNodes && hoveredNode != null && mapLayers.Count > 0 && hoveredNode.Value.layer > ReachedLayer()
            && !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();

        public void RightClick()
        {
            if (!CanMarkHoveredNode) return;

            List<int> marks = RouteMarks();
            bool marked = MapRoutePlan.Toggle(marks, hoveredNode.Value.index, LayerOfNode);
            ApplyRoutePlan();
            IAudioRequester.Instance.PlaySFX(marked ? SFXData.SelectCard : SFXData.TinyClick);
            CampaignManager.Instance.CampaignSaveManager.SaveCampaign();
            CampaignManager.Instance.CampaignSaveManager.SaveCampaignSnapshot();
        }
        private List<int> RouteMarks()
        {
            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            return save.plannedNodes ??= new List<int>();
        }
        // The layer the player stands on or is entering; only nodes past it can be marked.
        private int ReachedLayer() => selectedNode != null ? selectedNode.Value.layer : activeChapterIndex;
        private int CurrentNodeIndex()
        {
            if (selectedNode != null) return selectedNode.Value.index;
            List<int> nodePath = CampaignManager.Instance.CampaignSaveManager.SaveData.nodePath;
            return nodePath.Count > 0 ? nodePath[^1] : -1;
        }
        private int LayerOfNode(int nodeIndex)
        {
            foreach (MapLayer mapLayer in mapLayers)
                foreach (MapNodeData mapNode in mapLayer.LayerNodes)
                    if (mapNode.index == nodeIndex) return mapNode.layer;
            return -1;
        }
        // Runs after every repaint of the paths, which would otherwise leave the plan's colour off or stale.
        private void ApplyRoutePlan()
        {
            if (mapLayers.Count == 0 || CampaignManager.InstanceIfExists == null) return;

            List<int> marks = RouteMarks();
            int currentNodeIndex = CurrentNodeIndex();
            foreach (MapLayer mapLayer in mapLayers) {
                foreach (MapNodeData mapNode in mapLayer.LayerNodes) {
                    if (mapNode.mapNodeGameObject == null) continue;
                    mapNode.mapNodeGameObject.SetRouteMark(marks.Contains(mapNode.index));
                    for (int i = 0; i < mapNode.connectedNodeIndexes.Count; i++) {
                        bool planned = MapRoutePlan.IsPlannedLine(marks, mapNode.index, mapNode.index == currentNodeIndex, mapNode.connectedNodeIndexes[i]);
                        mapNode.mapNodeGameObject.SetPlannedLine(i, planned);
                    }
                }
            }
        }
        #endregion
        public void SetMapLayers(List<MapLayer> _mapLayers)
        {
            mapLayers = _mapLayers;
        }
        public int GetActiveChapterIndex() => activeChapterIndex;
        public void RevealNodesInNextLayers(int fromLayer, int layerCount)
        {
            RevealNodesVisually(fromLayer, layerCount);
            CampaignManager.Instance.CampaignSaveManager.SaveData.nodesRevealed = true;
            CampaignManager.Instance.CampaignSaveManager.SaveCampaign();
            CampaignManager.Instance.CampaignSaveManager.SaveCampaignSnapshot();
        }
        private void RevealNodesVisually(int fromLayer, int layerCount)
        {
            int end = Mathf.Min(fromLayer + 1 + layerCount, mapLayers.Count);
            for (int i = fromLayer + 1; i < end; i++)
            {
                foreach (MapNodeData node in mapLayers[i].LayerNodes)
                {
                    node.mapNodeGameObject?.Reveal();
                }
            }
        }
        public void UpdateNodePath()
        {
            // Debug.Log($"Updating node path on layer {activeChapterIndex+1}");
            List<int> nodePath = CampaignManager.Instance.CampaignSaveManager.SaveData.nodePath;
            // Debug.Log($"Node path: {string.Join(", ", nodePath)} with active layer {activeChapterIndex}");

            //show which nodes we have completed
            foreach(MapLayer mapLayer in mapLayers) {
                foreach(MapNodeData mapNode in mapLayer.LayerNodes) {
                    if (nodePath.Contains(mapNode.index)) {
                        mapNode.mapNodeGameObject.ShowCompleted(nodePath, mapNode.layer == activeChapterIndex + 1);
                    }
                }
            }
            for(int i = 0; i < mapLayers.Count; i++) {
                if (i == activeChapterIndex + 1) {
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        mapLayers[i].LayerNodes[j].mapNodeGameObject.DeselectNodeLayer();
                    }
                }
            }
        }
        public void UpdateNodePathFromSave()
        {
            // Debug.Log($"Updating node path from save on layer {activeChapterIndex}");
            List<int> nodePath = CampaignManager.Instance.CampaignSaveManager.SaveData.nodePath;
            // Debug.Log($"Node path: {string.Join(", ", nodePath)} with active layer {activeChapterIndex}");

            for(int i = 0; i < mapLayers.Count; i++) {
                if(i <= activeChapterIndex){
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        if(mapLayers[i].LayerNodes[j].mapNodeGameObject != selectedNode) {
                            // Debug.Log($"Deselecting other nodes on layer {activeChapterIndex}");
                            mapLayers[i].LayerNodes[j].mapNodeGameObject.ShowPassed();
                        }
                    }
                }
            }

            //show which nodes we have completed
            foreach(MapLayer mapLayer in mapLayers) {
                foreach(MapNodeData mapNode in mapLayer.LayerNodes) {
                    if (nodePath.Contains(mapNode.index)) {
                        mapNode.mapNodeGameObject.ShowCompleted(nodePath, mapNode.layer == activeChapterIndex);
                    }
                }
            }
            for(int i = 0; i < mapLayers.Count; i++) {
                if (i == activeChapterIndex) {
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        mapLayers[i].LayerNodes[j].mapNodeGameObject.DeselectNodeLayer();
                    }
                }
            }
        }
        public void UpdateNodePathPostBattle()
        {
            Debug.Log($"Updating node path post battle on layer {activeChapterIndex}");
            List<int> nodePath = CampaignManager.Instance.CampaignSaveManager.SaveData.nodePath;

            for(int i = 0; i < mapLayers.Count; i++) {
                if(i <= activeChapterIndex + 1){
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        if(mapLayers[i].LayerNodes[j].mapNodeGameObject != selectedNode) {
                            // Debug.Log($"Deselecting other nodes on layer {activeChapterIndex}");
                            mapLayers[i].LayerNodes[j].mapNodeGameObject.ShowPassed();
                        }
                    }
                }
            }

            //show which nodes we have completed
            foreach(MapLayer mapLayer in mapLayers) {
                foreach(MapNodeData mapNode in mapLayer.LayerNodes) {
                    if (nodePath.Contains(mapNode.index)) {
                        mapNode.mapNodeGameObject.ShowCompleted(nodePath, mapNode.layer == activeChapterIndex + 1);
                    }
                }
            }
            for(int i = 0; i < mapLayers.Count; i++) {
                if (i == activeChapterIndex + 1) {
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        mapLayers[i].LayerNodes[j].mapNodeGameObject.DeselectNodeLayer();
                    }
                }
            }
        }
        public void SelectNextLayer()
        {
            // Debug.Log($"Selecting next layer from activeChapterIndex: {activeChapterIndex}");
            if(selectedNode == null) {
                // CameraUnFocusFromNode();
                // Debug.Log($"No node selected, selecting first node on layer {activeChapterIndex}");
            } else {
                CameraFocusOnNode(selectedNode);
            }

            //for setting the first layer
            if(selectedNode == null) {
                for(int j = 0; j < mapLayers[0].LayerNodes.Count; j++) {
                    mapLayers[0].LayerNodes[j].mapNodeGameObject.SelectNodeLayer();
                }
                return;
            }

            for(int i = 0; i < mapLayers.Count; i++) {
                if (i == activeChapterIndex + 1) {
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        if(selectedNode.Value.connectedNodeIndexes.Contains(mapLayers[i].LayerNodes[j].index)) {
                            mapLayers[i].LayerNodes[j].mapNodeGameObject.SelectNodeLayer();
                        }
                    }
                }
            }

            selectedNode = null;
        }
        private void DeselectOtherNodesOnLayer()
        {
            // Debug.Log($"Deselecting other nodes on layer {activeChapterIndex + 1}");
            for(int i = 0; i < mapLayers.Count; i++) {
                if(i==activeChapterIndex +1){
                    for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                        if(mapLayers[i].LayerNodes[j].mapNodeGameObject != selectedNode) {
                            // Debug.Log($"Deselecting other nodes on layer {activeChapterIndex}");
                            mapLayers[i].LayerNodes[j].mapNodeGameObject.ShowPassed();
                        }
                    }
                }
            }
        }
        private void ResetAllNodes()
        {
            for(int i = 0; i < mapLayers.Count; i++) {
                for(int j = 0; j < mapLayers[i].LayerNodes.Count; j++) {
                    mapLayers[i].LayerNodes[j].mapNodeGameObject.ResetNode();
                }
            }
        }
#region Complete Layer
        public void CompleteLayer(bool claimVictory = false)
        {
            Debug.Log($"[Map] Completing layer {activeChapterIndex}");
            int squadsLost = CountZeroHealthSquads();
            CampaignManager.Instance.CampaignSaveManager.RemoveZeroHealthSquads();
            CampaignManager.Instance.CampaignSaveManager.HandleSpecialSquadsOnChapterEnd();

            // OverrideSelectedNodeBeforeBattle already recorded the node (and added it to nodePath)
            // before the battle started. Only call RecordSelectedNode if it wasn't pre-recorded,
            // otherwise nodePath ends up with the same index twice.
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.GetSelectedNodeIndex() != selectedNode.Value.index)
                CampaignManager.Instance.CampaignSaveManager.RecordSelectedNode(selectedNode.Value.index, selectedNode.Value.type);
            UpdateNodePath();
            CampaignManager.Instance.CampaignSaveManager.CompleteChapter();
            CampaignManager.Instance.CampaignSaveManager.CheckForFourFactions();
            activeChapterIndex = CampaignManager.Instance.CampaignSaveManager.SaveData.activeMapLayer;
            int bookNumber = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
            // Taken before the build, so a failed build cannot leave this node's detail for the next one.
            Dictionary<string, object> nodeDetail = NodeLog.Take();
            AnalyticsNodeReport nodeReport = GameEventTracker.TryBuild("nodeCompleted", () => BuildNodeReport(squadsLost, nodeDetail));

            //interest tutorial step trigger on layer 3 book 1
            if(activeChapterIndex == 3 && bookNumber == 1)
            {
                TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1]{ TutorialData.GoldInterest });
            }

            PayOrdealGoldLoss();

            // Debug.Log($"Layer completed. Moving to layer {activeChapterIndex}");
            // if (activeChapterIndex == 0) // for quick completion check
            if (activeChapterIndex == mapLayers.Count - 1)
            {
                ReportNodeCompleted(nodeReport, 0);

                // The last story act banks the win either way; only Claim Victory ends the run, and
                // below Overlord there is no March On, so the win ends it as before.
                if (bookNumber >= TabletopTavernConstants.FINAL_STORY_ACT)
                {
                    BankVictory();
                    bool endlessAllowed = DifficultyRules.EndlessAllowed(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel);
                    if (claimVictory || !endlessAllowed)
                    {
                        DisplayGameOver();
                        return;
                    }
                }

                CampaignManager.Instance.CampaignSaveManager.CompleteBook();
                ResetAllNodes();
                selectedNode = null;
                SceneHandler.Instance.SwitchGameState(GameStateEnum.Map, true);
                return;
            }
#if DEMO
            if (activeChapterIndex == 1 && bookNumber == 2)
            {
                DisplayGameOver();
                return;
            }
// #else
//             if (activeChapterIndex == 1 && bookNumber == 1)
//             {
//                 DisplayGameOver();
//                 return;
//             }
#endif

            IAudioRequester.Instance.PlaySFX(SFXData.CompleteLayer);
            int goldBeforeInterest = CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount;
            CampaignManager.Instance.GoldManager.CollectInterest();
            ReportNodeCompleted(nodeReport, CampaignManager.Instance.CampaignSaveManager.SaveData.goldAmount - goldBeforeInterest);

            SelectNextLayer();
            ApplyRoutePlan();
            SetMapInput(true);
            mapSceneUIManager.HUDPanel.ShowFreeCameraTip();
            CampaignManager.Instance.CampaignSaveManager.SaveCampaign();
            CampaignManager.Instance.CampaignSaveManager.SaveCampaignSnapshot();
        }
        // Locks the win in when the last story act falls: achievements, difficulty unlock, hero
        // completion, Godking time and the analytics win. Marching on afterwards cannot undo any of it.
        private void BankVictory()
        {
            CampaignSaveManager campaignSaveManager = CampaignManager.Instance.CampaignSaveManager;
            if (campaignSaveManager.SaveData.victoryBanked) return;

            campaignSaveManager.CheckPostRunAchievements();
            SaveDataHandler.RecordVictoryUnlocks();
            GameEventTracker.RunEnded(campaignSaveManager.SaveData, RunResult.Win, "win");
            campaignSaveManager.SaveCampaign();
            campaignSaveManager.SaveCampaignSnapshot();
        }
        private void DisplayGameOver()
        {
            BankVictory();

            mapSceneUIManager.GameOverPanel.RecordGameOver(true);
            mapSceneUIManager.GameOverPanel.DisplayGameOver(true);
            mapSceneUIManager.HUDPanel.LegendGO.SetActive(false);
        }
        // Squads RemoveZeroHealthSquads is about to erase.
        private int CountZeroHealthSquads()
        {
            SquadToLoad[] army = CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy;
            if (army == null) return 0;
            int lost = 0;
            foreach (SquadToLoad squad in army)
                if (squad.UnitIndex != -1 && !squad.isEmptySquad && squad.SquadCurrentHealth == 0) lost++;
            return lost;
        }
        // Read after CompleteChapter, so activeMapLayer is the layer this node sat on.
        private AnalyticsNodeReport BuildNodeReport(int squadsLost, Dictionary<string, object> detail)
        {
            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            var report = new AnalyticsNodeReport
            {
                Layer = save.activeMapLayer,
                NodeIndex = selectedNode.Value.index,
                NodeType = selectedNode.Value.type.ToString(),
                GoldAfter = save.goldAmount,
                SquadsLost = squadsLost,
                Detail = detail,
            };
            // spoilsTaken outlives its battle, so it only belongs to a node whose rewards were offered here.
            if (detail != null && detail.ContainsKey("rw") && save.spoilsTaken != null)
                detail["spoils"] = new List<string>(save.spoilsTaken);
            foreach (SquadToLoad squad in save.playerArmy)
            {
                if (squad.UnitIndex == -1 || squad.isEmptySquad) continue;
                report.ArmySize++;
                if (squad.UnitIndex < 10) report.DeployedSquads++;
                report.ArmyValue += TabletopTavernData.Instance.GetUnitCost(squad.UnitName);
                if (squad.HitPointsPerUnit > 0) report.UnitsAlive += squad.SquadCurrentHealth / squad.HitPointsPerUnit;
                report.UnitsMax += squad.maxUnitCount;
                report.HealthNow += squad.SquadCurrentHealth;
                report.HealthMax += squad.SquadMaxHealth;
            }
            if (selectedNode.Value.type == NodeType.Campfire)
            {
                var campfire = mapSceneUIManager.CampfirePanel;
                report.CampfireChoice = campfire.Chosen.ToString();
                report.TrainedUnit = campfire.TrainedUnit;
                report.TrainedPrestige = campfire.TrainedPrestige;
                // The trait picker runs before the layer completes, so a max-prestige squad already holds its trait here.
                foreach (SquadToLoad squad in save.playerArmy)
                    if (campfire.TrainedSquadId != null && squad.UniqueID == campfire.TrainedSquadId && squad.PrestigeTrait != UnitAttribute.None)
                        report.TrainedTrait = squad.PrestigeTrait.ToString();
            }
            return report;
        }
        // Sends the node once and forgets the pick, so a later node cannot inherit it.
        private void ReportNodeCompleted(AnalyticsNodeReport report, int interest)
        {
            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            if (report != null) report.Interest = interest;
            GameEventTracker.NodeCompleted(save, report);
            save.nodeVisit = default;
        }
        // What was on offer when the player picked, kept on the save until nodeCompleted sends it.
        private void RecordNodeVisit(MapNode picked)
        {
            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            List<OfferedNode> offered = new();
            int layer = activeChapterIndex + 1;
            if (layer >= 0 && layer < mapLayers.Count)
            {
                foreach (MapNodeData node in mapLayers[layer].LayerNodes)
                {
                    if (node.mapNodeGameObject == null || !node.mapNodeGameObject.Selectable) continue;
                    offered.Add(new OfferedNode { index = node.index, type = node.type, hidden = node.mapNodeGameObject.Surprise });
                }
            }
            save.nodeVisit = new NodeVisit
            {
                recorded = true,
                nodeIndex = picked.Value.index,
                hidden = picked.Surprise,
                goldOnEntry = save.goldAmount,
                offered = offered,
            };
        }
#endregion
        // The act 3 final pays rewards like every other final now that the run may march on; the run
        // only ends from the Act Complete screen. The demo still stops cold at its last battle.
        public bool WillCompleteLayerEndInGameOver()
        {
#if DEMO
            int bookNumber = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
            int nextLayerIndex = activeChapterIndex + 1;
            if (nextLayerIndex == 1 && bookNumber == 2)
                return true;
#endif
            return false;
        }
        public void OverrideSelectedNodeBeforeBattle()
        {
            Debug.Log($"Overriding selected node {selectedNode.Value.index}");
            CampaignManager.Instance.CampaignSaveManager.RecordSelectedNode(selectedNode.Value.index, selectedNode.Value.type);
        }
        private bool hoppingArrived = false;
        private const float ArrivalWatchdogBuffer = 2f;
        public void SelectNode(MapNode _selectedNode)
        {
            Debug.Log($"Selecting node {_selectedNode.Value.index}");
            GameEventTracker.TryRun("node visit", () => RecordNodeVisit(_selectedNode));
            selectedNode = _selectedNode;
            MapRoutePlan.Prune(RouteMarks(), LayerOfNode, ReachedLayer());
            hoppingArrived = false;
            SetMapInput(false);

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.SelectNode);

            StartCoroutine(MoveTokenToNode(selectedNode));
        }
        public IEnumerator MoveTokenToNode(MapNode _node)
        {
            //move player token to node position at a constant speed
            Vector3 startPos = playerToken.transform.position;
            Vector3 endPos = _node.transform.position;

            //rotate to face position
            playerToken.transform.LookAt(endPos);
            float distance = Vector3.Distance(startPos, endPos);

            playerToken.StartHopping();

            float speed = 0.3f;
#if UNITY_EDITOR
            if(Input.GetKey(KeyCode.LeftShift)) {
                speed = 3f;
            }
#endif
            float travelTime = distance / speed;

            // Safety net: if the landing feedback chain never calls back (e.g. the player
            // alt-tabs mid-hop and the hop/land MMF sequence desyncs), force the arrival
            // logic so the player never gets permanently stuck with map input locked.
            StartCoroutine(ArrivalWatchdog(_node, travelTime + ArrivalWatchdogBuffer));

            float time = 0f;
            while (time < travelTime) {
                time += Time.deltaTime;
                playerToken.transform.position = Vector3.Lerp(startPos, endPos, time / travelTime);
                yield return null;
            }
            playerToken.transform.position = endPos;
            playerToken.ReachedDestination(this);
        }
        private IEnumerator ArrivalWatchdog(MapNode _node, float timeout)
        {
            yield return new WaitForSeconds(timeout);
            if (!hoppingArrived && selectedNode == _node)
            {
                Debug.LogWarning($"[Map] Arrival watchdog forcing FinishHopping for node {_node.Value.index} - landing sequence never completed");
                FinishHopping();
            }
        }
        public void FinishHopping()
        {
            if (hoppingArrived) return;
            hoppingArrived = true;

            Debug.Log($"[Map] Arrived at node {selectedNode.Value.index} ({selectedNode.Value.type}) layer {selectedNode.Value.layer}");
            selectedNode.NodeClicked();
            DeselectOtherNodesOnLayer();
            UpdateNodePath();
            ApplyRoutePlan();
            mapSceneUIManager.LoadPanelFromNode(selectedNode);
        }
        public void CameraFocusOnNode(MapNode _node)
        {
            mapCamera.LerpCameraPullBackFocusPosition(_node.transform.position);
        }
        public void FocusSelectedNode()
        {
            if(selectedNode == null) {
                Debug.Log("No node selected");
                return;
            }
            mapCamera.LerpToFocusedPosition(selectedNode.transform.position);
        }
        public void OnDestroy()
        {
            if(SceneHandler.HasInstance)
                SceneHandler.Instance.OnGameStateChanged -= OnGameStateChanged;
            if(InputHandler.HasInstance)
                InputHandler.Instance.PrimaryActionPerformed -= LeftClick;
            if(InputHandler.HasInstance)
                InputHandler.Instance.SecondaryActionPressed -= RightClick;
            Memori.Utilities.ColorVision.Changed -= ApplyRoutePlan;
            RouteMarkColors.Changed -= ApplyRoutePlan;
        }
        private void HandleIntro()
        {
            StartCoroutine(DelayedTitleStart());

            mapCamera.HandleMapCameraIntro();
        }
        private IEnumerator DelayedTitleStart()
        {
            yield return new WaitForSeconds(0.25f);
            int bookNumber = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
            
            RaceData raceData = TabletopTavernData.Instance.GetRaceData(MapRace);
            switch(bookNumber)
            {
                case 1:
                    mapSceneUIManager.MapIntroDisplay1.DisplayTitle(raceData, bookNumber);
                    break;
                case 2:
                    mapSceneUIManager.MapIntroDisplay2.DisplayTitle(raceData, bookNumber);
                    break;
                case 3:
                default:
                    mapSceneUIManager.MapIntroDisplay3.DisplayTitle(raceData, bookNumber);
                    break;
            }
            

            IAudioRequester.Instance.PlaySFX(SFXData.Title);

            float duration = 3.5f;
            float elapsedTime = 0f;
            while (elapsedTime < duration && !mapCamera.SkipIntro)
            {
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            //complete intro after 3 seconds
            switch(bookNumber)
            {
                case 1:
                    mapSceneUIManager.MapIntroDisplay1.HideTitle();
                    break;
                case 2:
                    mapSceneUIManager.MapIntroDisplay2.HideTitle();
                    break;
                case 3:
                default:
                    mapSceneUIManager.MapIntroDisplay3.HideTitle();
                    break;
            }
            
            mapSceneUIManager.HUDPanel.HudAnimator.Play("HUD Open");
            if (ordealPickPending) StartCoroutine(OfferOrdeals());
            else SetMapInput(true);
        }
        public void SetMapInput(bool _allowMapInput)
        {
            // Debug.Log($"Setting map input to {_allowMapInput}");
            allowMapInput = _allowMapInput && !ordealPickPending;
        }
        #region Ordeals
        // The Tithe and Mercenary Contract, on every completed layer; gold floors at 0, so a broke run loses nothing.
        private void PayOrdealGoldLoss()
        {
            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            int loss = Mathf.Min(OrdealRegistry.GoldLostPerTurn(save), save.goldAmount);
            if (loss <= 0) return;
            string label = LocalizationManager.Instance.GetText(OrdealRegistry.Get(save.HasOrdeal(OrdealId.TheTithe) ? OrdealId.TheTithe : OrdealId.MercenaryContract).NameKey);
            CampaignManager.Instance.GoldManager.ModifyGold(-loss, label);
        }
        private IEnumerator OfferOrdeals()
        {
            while (mapSceneUIManager.IsDrainingPrestigeChoices) yield return null;

            CampaignSaveData save = CampaignManager.Instance.CampaignSaveManager.SaveData;
            List<OrdealId> offer = OrdealRegistry.DrawOffer(save);
            if (offer.Count == 0)
            {
                FinishOrdealPick(OrdealId.None, offer);
                yield break;
            }
            mapSceneUIManager.OrdealPanel.Open(offer, save.bookNumber, taken => FinishOrdealPick(taken, offer));
        }
        private void FinishOrdealPick(OrdealId taken, List<OrdealId> offer)
        {
            List<string> notices = CampaignManager.Instance.CampaignSaveManager.BeginOrdealAct(taken, offer);
            if (notices.Count > 0) Memori.Notifications.NotificationManager.Instance.DisplayNotification(string.Join("\n", notices));

            bool redrawMap = taken != OrdealId.None && OrdealRegistry.Get(taken).RedrawsMap;
            ordealPickPending = false;
            if (redrawMap)
            {
                mapGenerator.RedrawNodes();
                hoveredNode = null;
                selectedNode = null;
                if (CampaignManager.Instance.CampaignSaveManager.SaveData.nodesRevealed)
                    RevealNodesVisually(-1, mapLayers.Count);
                SelectNextLayer();
                ApplyRoutePlan();
            }
            SetMapInput(true);
        }
        #endregion
    }
}
