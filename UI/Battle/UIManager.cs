using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using TMPro;
using System.Collections.Generic;
using Memori.Utilities;
using Unity.Mathematics;
using Memori.Scenes;
using Memori.Audio;
using Memori.Tooltip;
using Memori.Input;
using Memori.Localization;
using System;
using UnityEngine.InputSystem;
using System.Linq;
using System.Threading.Tasks;
using TJ.Battle;
using Unity.Entities;
using Memori.Notifications;
using Memori.SaveData;

namespace TJ
{
    public class UIManager : MonoBehaviour
    {
        [Header("Canvas Groups")]
        [SerializeField] private MemoriCanvasGroup mainCanvasGroup;
        [SerializeField] private MemoriCanvasGroup deploymentCanvasGroup;

        [Header("Squad Display")]
        [SerializeField] private Transform squadDisplayParent;
        [SerializeField] private SquadDisplayCardBattle squadDisplayPrefab;
        private List<SquadDisplayCardBattle> squadDisplays = new();
        public Action<List<SquadDisplayCardBattle>> OnSquadDisplaysChanged;

        [Header("Cursor Popups")]
        [SerializeField] private Transform cursorPopupParent;
        [SerializeField] private MemoriCanvasGroup spawnErrorMessage;
        [SerializeField] private BattlefieldBonusInfo battlefieldBonusInfo;
        [SerializeField] private TMP_Text spawnErrorText;
        [SerializeField] private GameObject addingOrQueuingIconParent;
        [SerializeField] private Image addingOrQueuingIcon;
        [SerializeField] private GameObject _spellQuickCast;
        private SpellQuickCastMenu spellQuickCastMenu;
        private bool isOverUI;
        private CursorMode cursorModeBeforeQuickCastMenu;

        [Header("Battle")]
        [SerializeField] private Button startBattleButton;
        public Transform GuardModeButtonTransform => guardModeButton.transform;
        [SerializeField] private CanvasGroup selectedSquadButtonsCanvasGroup;

        [Header("Battle Command Buttons")]
        [SerializeField] private BattleButton guardModeButton;
        [SerializeField] private BattleButton haltButton, withdrawButton, autoRetargetButton, meleeModeButton, 
            fireAtWillButton, volleyFireButton, balancedStanceButton, defensiveStanceButton,
            ceaseFireButton,
            saveFormationButton, loadFormationButton;
      

        [Header("Attack Arrows Drawer")]
        [SerializeField] private AttackArrowDrawer attackArrowPrefab;
        [SerializeField] private Transform drawingParent;
        private Dictionary<AttackArrowDrawer, int> attackArrowsDict = new();

        [Header("Hovered Squad")]
        [SerializeField] private SquadHoveredTooltip squadHoveredTooltip;
        [SerializeField] private SquadBattleInfo squadBattleInfo;
        public SquadBattleInfo SquadBattleInfo => squadBattleInfo;

        [Header("Healthbars")]
        [SerializeField] private Transform healthBarParent;
        [SerializeField] private HealthBar healthBarPrefab;
        private Dictionary<int, HealthBar> healthBars = new();

        [Header("Settings")]
        [SerializeField] private Button settingsToggleButton;
        // [SerializeField] private Button toggleDeploymentCanvasButton;

        [Header("End Battle")]
        [SerializeField] private GameObject endBattlePanel;
        [SerializeField] private Button continueAfterBattleButton, restartBattleButton;
        [SerializeField] private TMP_Text battleOutcomeText, battleVictoryOrDefeatText, continueAfterBattleButtonText;

        [Header("Garrison Tutorial")]
        [SerializeField] private GameObject garrisonTutorial;
        [SerializeField] private Button garrisonTutorialCloseButton;

        [Header("Weather Effects")]
        [SerializeField] private TMP_Text weatherTitleText;
        [SerializeField] private TMP_Text weatherDescriptionText;

        [Header("Balance of Power Display")]
        [SerializeField] private BalanceOfPowerDisplay balanceOfPowerDisplay;
        public BalanceOfPowerDisplay BalanceOfPowerDisplay => balanceOfPowerDisplay;

        [Header("Battle Layout Dice Roll")]
        [SerializeField] private BattleDiceRollPanel battleDiceRollPanel;
        public BattleDiceRollPanel BattleDiceRollPanel => battleDiceRollPanel;

        int cachedSquadHovered = 0;
        public int HoveredSquadId => cachedSquadHovered;

        private bool _isLoaded;

        bool allSelectedUnitsInGuardMode, selectedSquadsContainRangedUnits, selectedSquadsContainArtilleryUnits, selectedSquadsContainMageUnits, selectedSquadsContainShieldedUnits,
        allSelectedSquadsAutoRetarget, allSelectedSquadsMeleeMode, allSelectedSquadsVolleyFire, allSelectedSquadsFireAtWill, allSelectedSquadBalancedStance, allSelectedSquadsDefensiveStance,
        allSelectedSquadsCeaseFire;
        string recentPositionErrorMessage = "";

        public void Load()
        {
            if (_isLoaded) return;
            _isLoaded = true;

            startBattleButton.onClick.RemoveAllListeners();

            UnitName[] ironLegion =TabletopTavernData.Instance.GetUnitsOfRace(Race.IronLegion);
            UnitName[] Gruntkin =TabletopTavernData.Instance.GetUnitsOfRace(Race.Gruntkin);
            UnitName[] ravenHost =TabletopTavernData.Instance.GetUnitsOfRace(Race.RavenHost);
            UnitName[] taelindor =TabletopTavernData.Instance.GetUnitsOfRace(Race.TaelindorForest);
            UnitName[] sanguineCourt =TabletopTavernData.Instance.GetUnitsOfRace(Race.SanguineCourt);

            //combine the arrays
            UnitName[] allUnits = new UnitName[ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length + sanguineCourt.Length];
            ironLegion.CopyTo(allUnits, 0);
            Gruntkin.CopyTo(allUnits, ironLegion.Length);
            ravenHost.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length);
            taelindor.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length);
            sanguineCourt.CopyTo(allUnits, ironLegion.Length + Gruntkin.Length + ravenHost.Length + taelindor.Length);

            startBattleButton.onClick.AddListener(() => StartBattle());
            startBattleButton.gameObject.SetActive(BattleManager.Instance.BattleSaveManager.IsCustomBattle);

            SetUpBattleButtons();
            
            settingsToggleButton.onClick.RemoveAllListeners();
            settingsToggleButton.onClick.AddListener(SettingsManager.Instance.OpenSettingsPanel);

            BattleManager.Instance.OnGamePhaseChanged += OnGamePhaseChanged;
            BattleManager.Instance.UnitSelectionManager.OnSelectedSquadsChanged += OnSelectedSquadsChanged;
            BattleManager.Instance.SquadManager.OnSquadUpdated += OnSquadUpdated;
            SettingsManager.Instance.OnSettingsPanelToggled += OnSettingsPanelToggled;
            InputHandler.Instance.onHideUI += HideUI;
            deploymentCanvasGroup.CGEnable();

            squadBattleInfo.Unhover();
            mainCanvasGroup.CGEnable();
            // GroupManager must subscribe to OnDestroyedSquad before UIManager so that
            // RemoveSquadFromGroups updates squadIds before the OnSquadDisplaysChanged
            // chain triggers RefreshGroupUIs.
            BattleManager.Instance.GroupManager.Load();
            BattleManager.Instance.OnSquadBrokenEvent += OnSquadBrokenEvent;
            BattleManager.Instance.SquadManager.OnDestroyedSquad += OnSquadDestroyedEvent;
            InputHandler.Instance.OnToggleAutoRetarget += ToggleAutoRetarget;
            InputHandler.Instance.OnToggleGuardMode += ToggleGuardMode;
            InputHandler.Instance.OnToggleMeleeMode += ToggleMeleeMode;
            InputHandler.Instance.OnHaltCommand += IssueHaltCommand;
            InputHandler.Instance.OnWithdrawCommand += OnWithdrawSquadButtonClicked;
            BattleManager.Instance.SquadOrderManager.OnSquadOrderChanged += OnSquadOrderReceived;
            BattleManager.Instance.BattlefieldEnvManager.OnWeatherChanged += OnWeatherChanged;
            if (addingOrQueuingIcon != null) addingOrQueuingIcon.enabled = false;
            InputHandler.Instance.OnQueueOrder += EnableAddingOrQueuingIcon;
            InputHandler.Instance.OnQueueOrderCanceled += CancelAddingOrQueuingIcon;
            InputHandler.Instance.OnSpellQuickCast += ShowSpellQuickCastMenu;
            InputHandler.Instance.OnSpellQuickCastCanceled += HideSpellQuickCastMenu;
            InputHandler.Instance.OnFireAtWillModeToggle += SetFireAtWillMode;
            InputHandler.Instance.OnVolleyFireModeToggle += SetVolleyFireMode;
            InputHandler.Instance.OnBalancedStanceToggle += SetBalancedStance;
            InputHandler.Instance.OnDefensiveStanceToggle += SetDefensiveStance;
            InputHandler.Instance.OnCeaseFireToggled += SetCeaseFireMode;
            balanceOfPowerDisplay.ArmyLossesTriggered += ArmyLossesTriggered;
            SaveDataHandler.ArmyLossesSufferedThisBattle = false; // fresh per battle; consumed at battle end

            endBattlePanel.SetActive(false);
            UpdateBattleButtons(false);
            spellQuickCastMenu = _spellQuickCast.GetComponent<SpellQuickCastMenu>();
            _spellQuickCast.SetActive(false);

            if (BattleManager.Instance.BattleSaveManager.IsGarrisonBattle)
            {
                bool seenTutorial = PlayerPrefs.GetInt("GarrisonTutorialSeen", 0) == 1;
                if (!seenTutorial && garrisonTutorial != null)
                {
                    garrisonTutorial.SetActive(true);
                    PlayerPrefs.SetInt("GarrisonTutorialSeen", 1);
                    PlayerPrefs.Save();

                    if (garrisonTutorialCloseButton != null)
                        garrisonTutorialCloseButton.onClick.AddListener(() => garrisonTutorial.SetActive(false));
                }
            }

            if(BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                OnWeatherChanged(Weather.ClearSkies);
            }
        }

        private void LateUpdate()
        {
            cursorPopupParent.transform.position = Input.mousePosition;
        }

        private void Update()
        {

            bool overUI = UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
            if (overUI != isOverUI)
            {
                isOverUI = overUI;
                addingOrQueuingIconParent.SetActive(!isOverUI);
            }

            if (BattleManager.Instance.CursorMode == CursorMode.QuickCastMenu && Input.GetMouseButtonDown(1))
            {
                HideSpellQuickCastMenu();
            }

            foreach (HealthBar healthBar in healthBars.Values)
            {
                Vector3 goalPosition = Camera.main.WorldToScreenPoint(healthBar.SquadTransform.position);

                if (goalPosition.z >= 0)
                {
                    healthBar.transform.position = goalPosition;
                }
                else
                {
                    healthBar.transform.position = Vector3.down * 1000;
                }
            }
        }
        private void HideUI()
        {
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.Battle) return;

            //check if bug report open
            ReportABugScreen bugScreen = FindFirstObjectByType<ReportABugScreen>();
            if (bugScreen != null && bugScreen.GetComponent<CanvasGroup>().alpha > 0) return;
            

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);

            foreach (Canvas canvas in canvases)
            {
                canvas.enabled = !canvas.enabled;
            }

            int layerToToggle = LayerMask.NameToLayer("Shapes");
            Camera targetCamera = BattleManager.Instance.BattleCamera;

            bool IsLayerEnabled()
            {
                return (targetCamera.cullingMask & (1 << layerToToggle)) != 0; // Check if the layer's bit is set in the culling mask
            }
            if (IsLayerEnabled())
            {
                targetCamera.cullingMask &= ~(1 << layerToToggle);// Enable the layer by setting the corresponding bit to 1
            }
            else
            {
                targetCamera.cullingMask |= (1 << layerToToggle); // Disable the layer by setting the corresponding bit to 0
            }
            SquadFlagGameObjectTag[] flagGameObjectTags = FindObjectsByType<SquadFlagGameObjectTag>(FindObjectsSortMode.None);

            foreach (SquadFlagGameObjectTag flag in flagGameObjectTags)
            {
                flag.FlagMeshRenderer.enabled = !flag.FlagMeshRenderer.enabled;
            }

            Cursor.visible = !Cursor.visible;
        }
        private void OnSettingsPanelToggled(bool _open)
        {
            if (_open)
            {
                mainCanvasGroup.CGDisable();
            }
            else
            {
                mainCanvasGroup.CGEnable();
            }
        }
        public HealthBar LoadHealthbar(Transform _squadPosition, Team _faction, int id, int ammunition, bool isGate)
        {
            HealthBar healthBar = Instantiate(healthBarPrefab, healthBarParent);
            healthBar.SetUp(_faction, _squadPosition, ammunition, isGate);

            if (healthBars.ContainsKey(id))
            {
                Debug.LogError("yo it already exists");
                healthBars[id] = healthBar;
            }
            else
            {
                healthBars.Add(id, healthBar);
            }

            return healthBar;
        }
        public void OnSquadBrokenEvent(int squadID)
        {
            RemoveHealthbar(squadID);
        }
        public void OnSquadDestroyedEvent(int squadID)
        {
            RemoveHealthbar(squadID);
        }
        public void RemoveHealthbar(int id)
        {
            if (!healthBars.ContainsKey(id)) return;

            Destroy(healthBars[id].gameObject);
            healthBars.Remove(id);
        }

        public void AddSquad(SquadEntity _squad, int _unitCount)
        {
            SquadDisplayCardBattle squadDisplay = Instantiate(squadDisplayPrefab, squadDisplayParent);
            // Debug.Log($"Adding Squad Display for Squad ID: {_squad.SquadId}");

            int prestige = GetUnitPrestige(_squad.SquadId);
            squadDisplay.SetUnitPrestige(prestige);
            squadDisplay.SetUp(_squad, _unitCount);
            squadDisplays.Add(squadDisplay);
            OnSquadDisplaysChanged?.Invoke(squadDisplays);

            // In custom battles squads are spawned one at a time with no fixed final count,
            // so update the order on every add. In campaign battles, wait until all squads
            // are loaded before initializing to avoid redundant layout rebuilds during load.
            bool isCustomBattle = BattleManager.Instance.BattleSaveManager.IsCustomBattle;
            // A squad arriving after the order already exists (e.g. summoned by a spell) is appended
            // rather than triggering a wholesale re-Initialize, which would discard any manual card
            // reordering the player has done.
            if(BattleManager.Instance.SquadOrderManager.IsInitialized)
            {
                BattleManager.Instance.SquadOrderManager.AddSquad(_squad.SquadId);
            }
            else if(isCustomBattle || BattleManager.Instance.BattleSaveManager.PlayerSquadsToSpawn == squadDisplays.Count)
            {
                var orderedIds = squadDisplays.ConvertAll(c => c.SquadId);
                orderedIds.Sort();
                BattleManager.Instance.SquadOrderManager.Initialize(orderedIds);
            }
        }
        private int GetUnitPrestige(int _squadId)
        {
            if (BattleManager.Instance.SquadManager.UnitPrestigeDict.ContainsKey(_squadId))
            {
                return BattleManager.Instance.SquadManager.UnitPrestigeDict[_squadId];
            }
            else
            {
                Debug.Log($"Unit Prestige not found for squad ID: {_squadId}");
                return 0;
            }
        }
        public void RefreshSquadDisplay(int _squadId, int _unitCount)
        {
            foreach (SquadDisplayCardBattle squadDisplay in squadDisplays)
            {
                if (squadDisplay.SquadId == _squadId)
                {
                    squadDisplay.RefreshUnitCountInBattle(_unitCount);
                    if (_unitCount == 0)
                    {
                        RemoveSquad(_squadId);
                    }
                    return;
                }
            }
        }
        public void RemoveSquad(int _squadId)
        {
            for (int i = 0; i < squadDisplays.Count; i++)
            {
                if (squadDisplays[i].SquadId == _squadId)
                {
                    // Unparent before firing SquadOrderManager.RemoveSquad so that the
                    // ForceRebuildLayoutImmediate inside OnSquadOrderReceived does not include
                    // this card in the layout — preventing GroupUI from being offset by one
                    // card width. Also remove from list first so OnDestroy re-entry is a no-op.
                    SquadDisplayCardBattle card = squadDisplays[i] as SquadDisplayCardBattle;
                    bool alreadyDestroying = card != null && card.RemovedByUIManager;
                    if (card != null) card.RemovedByUIManager = true;
                    GameObject go = squadDisplays[i].gameObject;
                    squadDisplays.RemoveAt(i);
                    if (!alreadyDestroying) go.transform.SetParent(null);
                    // Remove from group data before the refresh chain fires. In the ECS
                    // destruction path, EntityWatcher.Update fires OnDestroyedSquad a frame
                    // after FixedUpdate detects missing components, so we can't rely on
                    // GroupManager's subscription to have already cleaned the squad out.
                    // RemoveSquadFromGroups is idempotent — a second call from EntityWatcher is a no-op.
                    BattleManager.Instance.GroupManager.RemoveSquadFromGroups(_squadId);
                    BattleManager.Instance.SquadOrderManager.RemoveSquad(_squadId);
                    Destroy(go);
                    return;
                }
            }
        }
        public void ShowPositionError(bool _error, string _message)
        {
            if (_error)
            {
                recentPositionErrorMessage = _message;
                spawnErrorText.text = _message;
                spawnErrorMessage.FadeInAsync(0.25f, false, false);
            }
            else
            {
                spawnErrorMessage.CGDisable();
            }
        }
        private async void StartBattle()
        {
            IAudioRequester.Instance.PlaySFX("start-battle");
            startBattleButton.interactable = false;
            startBattleButton.gameObject.SetActive(false);
            saveFormationButton.gameObject.SetActive(false);
            loadFormationButton.gameObject.SetActive(false);

            BattleManager.Instance.StartBattle();

            // if (BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            // {
            //     await BattleManager.Instance.StartBattle();
            // }
            // else
            // {
            //     if(!BattleManager.Instance.BattleSaveManager.IsCustomBattle) 
            //     {
            //         await BattleManager.Instance.ArmySpawnManager.LoadEnemyArmyFromSaveFiles();
            //     }
            // }
        }
        public void DisplayHoveredSquadUI(int _squadId, bool _hovered)
        {
            foreach (SquadDisplayCardBattle squadDisplay in squadDisplays)
            {
                if (squadDisplay.SquadId == _squadId)
                {
                    squadDisplay.HoverSquad(_hovered);
                }
            }
        }
        public void DeselectSquadEntitiesUI()
        {
            foreach (SquadDisplayCardBattle squadDisplay in squadDisplays)
            {
                squadDisplay.SelectSquad(false);
            }
        }
        public void HideSquadHoveredTooltip()
        {
            squadHoveredTooltip.Unhover();
        }
        public void SetNoSquadHovered()
        {
            cachedSquadHovered = 0;
            squadHoveredTooltip.Unhover();

            //check if any squad is selected
            if (BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Count == 0)
            {
                squadBattleInfo.Unhover();
            }
            else
            {
                SquadEntity hoveredSquad = BattleManager.Instance.SquadManager.GetSquad(BattleManager.Instance.UnitSelectionManager.SelectedSquadIds[0]);
                LoadSquadBattleInfo(hoveredSquad);
            }
        }
        public void SetSquadHovered(SquadEntity _squad, bool _isPlayer)
        {
            cachedSquadHovered = _squad.SquadId;

            squadHoveredTooltip.Load(_squad);
            LoadSquadBattleInfo(_squad);

            // await Task.Delay(500);
            if (cachedSquadHovered == 0) return;

            squadHoveredTooltip.Hover();
        }
        private void LoadSquadBattleInfo(SquadEntity _squad)
        {
            int unitCount = BattleManager.Instance.SquadManager.GetSquadUnitCount(_squad.SquadId);
            int prestige = 0;
            if (_squad.SquadId != 0) prestige = GetUnitPrestige(_squad.SquadId);

            squadBattleInfo.SetUpBattle(_squad, unitCount, prestige);
        }
        public void CreateAttackArrow(SquadEntity _SquadEntity)
        {
            // Debug.Log($"Creating Arrow for Squad: {_SquadEntity.SquadId}");
            AttackArrowDrawer attackArrow = Instantiate(attackArrowPrefab);
            attackArrow.SetUp(_SquadEntity);
            attackArrowsDict.Add(attackArrow, _SquadEntity.SquadId);
        }
        public void UpdateAttackArrowToMelee(int squadID, bool _toMelee)
        {
            foreach (var arrow in attackArrowsDict)
            {
                if (arrow.Value == squadID)
                {
                    arrow.Key.SwitchToMelee(_toMelee);
                    return;
                }
            }
        }
        public void ShowStartBattleButton() => startBattleButton.gameObject.SetActive(true);

        private void OnGamePhaseChanged(GamePhase _gamePhase)
        {
            switch (_gamePhase)
            {
                case GamePhase.Deployment:
                    deploymentCanvasGroup.CGEnable();
                    if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
                        startBattleButton.gameObject.SetActive(true);
                    break;
                case GamePhase.Battle:
                    deploymentCanvasGroup.FadeOutAsync(0.25f, false, false);
                    break;
                case GamePhase.PostGame:
                    HandleEndBattle();
                    break;
            }
        }
        public void RefreshSelectedSquadButtonStates()
        {
            OnSelectedSquadsChanged(BattleManager.Instance.UnitSelectionManager.SelectedSquadIds);
        }
        private void OnSelectedSquadsChanged(List<int> _selectedSquadIds)
        {
            selectedSquadsContainRangedUnits = false;
            selectedSquadsContainShieldedUnits = false;
            selectedSquadsContainArtilleryUnits = false;
            selectedSquadsContainMageUnits = false;

            allSelectedUnitsInGuardMode = true;
            allSelectedSquadsAutoRetarget = true;
            allSelectedSquadsMeleeMode = true;
            allSelectedSquadsVolleyFire = true;
            allSelectedSquadsFireAtWill = true;
            allSelectedSquadBalancedStance = true;
            allSelectedSquadsDefensiveStance = true;
            allSelectedSquadsCeaseFire = true;

            NativeArray<SquadOverridesComponent> playerSquads = BattleManager.Instance.SquadManager.RetrievePlayerSquadOverrideComponents();
            bool anyPlayerSquadSelected = false;

            foreach (SquadOverridesComponent squad in playerSquads)
            {
                if (!_selectedSquadIds.Contains(squad.SquadId)) continue;
                anyPlayerSquadSelected = true;

                if (!squad.GuardMode)
                {
                    allSelectedUnitsInGuardMode = false;
                }

                if(TabletopTavernConstants.FightsAtRange(squad.UnitType))
                {
                    selectedSquadsContainRangedUnits = true;

                    if (!squad.AutoTarget)
                    {
                        allSelectedSquadsAutoRetarget = false;
                    }
                    if (!squad.MeleeMode)
                    {
                        allSelectedSquadsMeleeMode = false;
                    }
                    if (squad.FireMode != RangedFireMode.Volley)
                    {
                        allSelectedSquadsVolleyFire = false;
                    }
                    if (squad.FireMode != RangedFireMode.FireAtWill)
                    {
                        allSelectedSquadsFireAtWill = false;
                    }
                }

                if(squad.UnitType == UnitType.Artillery)
                {
                    selectedSquadsContainArtilleryUnits = true;
                }

                // Mages get Cease Fire and nothing else from this block. They have no fire mode, no
                // melee toggle and no auto-retarget, so they must not set selectedSquadsContainRangedUnits.
                if(TabletopTavernConstants.Casts(squad.UnitType))
                {
                    selectedSquadsContainMageUnits = true;
                }

                if(TabletopTavernConstants.FightsAtRange(squad.UnitType) || squad.UnitType == UnitType.Artillery || TabletopTavernConstants.Casts(squad.UnitType))
                {
                    if (!squad.CeaseFire)
                    {
                        allSelectedSquadsCeaseFire = false;
                    }
                }

                if(squad.ShieldedStance != ShieldedStance.None)
                {
                    selectedSquadsContainShieldedUnits = true;

                    if (squad.ShieldedStance != ShieldedStance.Balanced)
                    {
                        allSelectedSquadBalancedStance = false;
                    }
                    if (squad.ShieldedStance != ShieldedStance.Defensive)
                    {
                        allSelectedSquadsDefensiveStance = false;
                    }
                }
            }

            UpdateBattleButtons(anyPlayerSquadSelected);

            if (_selectedSquadIds.Count != 0)
            {
                SquadEntity hoveredSquad = BattleManager.Instance.SquadManager.GetSquad(BattleManager.Instance.UnitSelectionManager.SelectedSquadIds[0]);
                LoadSquadBattleInfo(hoveredSquad);
            }
            else
            {
                squadBattleInfo.Unhover();
            }

            foreach (SquadDisplayCardBattle squadDisplay in squadDisplays)
            {
                if (_selectedSquadIds.Contains(squadDisplay.SquadId))
                {
                    squadDisplay.SelectSquad(true);
                }
                else
                {
                    squadDisplay.SelectSquad(false);
                }
            }

            playerSquads.Dispose();
        }

        #region Battle Buttons Presses
        private void SetGuardMode(bool _guardMode)
        {
            if(BattleManager.Instance.GamePhase != GamePhase.Battle &&
                BattleManager.Instance.GamePhase != GamePhase.Deployment) return;

            BattleManager.Instance.SquadManager.SetGuardMode(_guardMode);
        }
        private void SetAutoRetarget(bool _autoRetarget)
        {
            if(BattleManager.Instance.GamePhase != GamePhase.Battle &&
                BattleManager.Instance.GamePhase != GamePhase.Deployment) return;

            BattleManager.Instance.SquadManager.SetAutoTarget(_autoRetarget);
        }
        private void SetMeleeMode(bool _meleeMode)
        {
            if(BattleManager.Instance.GamePhase != GamePhase.Battle &&
                BattleManager.Instance.GamePhase != GamePhase.Deployment) return;

            BattleManager.Instance.SquadManager.SetMeleeMode(_meleeMode);
        }
        #endregion

        #region Battle Buttons Hotkeys
        private void ToggleGuardMode()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            guardModeButton.HotkeyInteract();
        }
        private void ToggleAutoRetarget()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            autoRetargetButton.HotkeyInteract();
        }
        private void ToggleMeleeMode()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            meleeModeButton.HotkeyInteract();
        }
        private void SetFireAtWillMode()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            BattleManager.Instance.SquadManager.SetFireAtWill();
            volleyFireButton.SetOnOrOff(false);
            fireAtWillButton.SetOnOrOff(true);
        }
        private void SetVolleyFireMode()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            BattleManager.Instance.SquadManager.SetVolleyFire();
            fireAtWillButton.SetOnOrOff(false);
            volleyFireButton.SetOnOrOff(true);
        }
        private void SetBalancedStance()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            BattleManager.Instance.SquadManager.SetBalancedStance();
            defensiveStanceButton.SetOnOrOff(false);
            balancedStanceButton.SetOnOrOff(true);
        }
        private void SetDefensiveStance()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            BattleManager.Instance.SquadManager.SetDefensiveStance();
            balancedStanceButton.SetOnOrOff(false);
            defensiveStanceButton.SetOnOrOff(true);
        }
        private void SetCeaseFireMode()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            IssueCeaseFireCommand();
        }
        #endregion

        private void SetUpBattleButtons()
        {
            string guardModeTitleLocalized = LocalizationManager.Instance.GetText("GuardModeTitle");
            string guardModeDescLocalized = LocalizationManager.Instance.GetText("GuardModeDesc");
            string haltTitleLocalized = LocalizationManager.Instance.GetText("HaltSquad");
            string haltDescLocalized = LocalizationManager.Instance.GetText("HaltSquadDesc");
            string destroyTitleLocalized = LocalizationManager.Instance.GetText("DestroySquad");
            string destroyDescLocalized = LocalizationManager.Instance.GetText("DestroySquadDesc");
            string withdrawTitleLocalized = LocalizationManager.Instance.GetText("WithdrawSquad");
            string withdrawDescLocalized = LocalizationManager.Instance.GetText("WithdrawSquadDesc");
            string ceaseFireTitleLocalized = LocalizationManager.Instance.GetText("CeaseFireTitle");
            string ceaseFireDescLocalized = LocalizationManager.Instance.GetText("CeaseFireDesc");

            string autoRetargetTitleLocalized = LocalizationManager.Instance.GetText("AutoRetargetTitle");
            string autoRetargetDescLocalized = LocalizationManager.Instance.GetText("AutoRetargetDesc");

            string meleeModeTitleLocalized = LocalizationManager.Instance.GetText("MeleeModeTitle");
            string meleeModeDescLocalized = LocalizationManager.Instance.GetText("MeleeModeDesc");
            string saveFormationTitleLocalized = LocalizationManager.Instance.GetText("SaveFormationTitle");
            string saveFormationDescLocalized = LocalizationManager.Instance.GetText("SaveFormationDesc");
            string loadFormationTitleLocalized = LocalizationManager.Instance.GetText("LoadFormationTitle");
            string loadFormationDescLocalized = LocalizationManager.Instance.GetText("LoadFormationDesc");
            string volleyFireTitleLocalized = LocalizationManager.Instance.GetText("VolleyFireTitle");
            string volleyFireDescLocalized = LocalizationManager.Instance.GetText("VolleyFireDesc");
            string fireAtWillTitleLocalized = LocalizationManager.Instance.GetText("FireAtWillTitle");
            string fireAtWillDescLocalized = LocalizationManager.Instance.GetText("FireAtWillDesc");
            string balancedStanceTitleLocalized = LocalizationManager.Instance.GetText("BalancedStanceTitle");
            string balancedStanceDescLocalized = LocalizationManager.Instance.GetText("BalancedStanceDesc");
            string defensiveStanceTitleLocalized = LocalizationManager.Instance.GetText("DefensiveStanceTitle");
            string defensiveStanceDescLocalized = LocalizationManager.Instance.GetText("DefensiveStanceDesc");

            string toggleGuardModeKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.ToggleGuardMode.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleFireAtWillKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.ToggleFireAtWillMode.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleVolleyFireKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.ToggleVolleyFireMode.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleMeleeModeKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.ToggleMeleeMode.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleWithdrawKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.Withdraw.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );  
            string toggleHaltKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.Halt.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleAutoRetargetKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.ToggleAutoRetarget.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleBalancedStanceKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.SetBalancedStance.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string toggleDefensiveStanceKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.SetDefensiveStance.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            string ceaseFireKey = InputControlPath.ToHumanReadableString(
                InputHandler.Instance.GameControls.Battle.CeaseFireCommand.bindings[0].effectivePath, 
                InputControlPath.HumanReadableStringOptions.OmitDevice
            );
            
            guardModeButton.SetUp($"{ guardModeTitleLocalized} ({toggleGuardModeKey})", guardModeDescLocalized, SetGuardMode);
            haltButton.SetUp($"{haltTitleLocalized} ({toggleHaltKey})", haltDescLocalized, onClickAction: () => IssueHaltCommand());
            withdrawButton.SetUp($"{withdrawTitleLocalized} ({toggleWithdrawKey})", withdrawDescLocalized, onClickAction: () => OnWithdrawSquadButtonClicked());
            autoRetargetButton.SetUp($"{autoRetargetTitleLocalized} ({toggleAutoRetargetKey})", autoRetargetDescLocalized, SetAutoRetarget);
            meleeModeButton.SetUp($"{meleeModeTitleLocalized} ({toggleMeleeModeKey})", meleeModeDescLocalized, SetMeleeMode);
            volleyFireButton.SetUp($"{volleyFireTitleLocalized} ({toggleVolleyFireKey})", volleyFireDescLocalized, onClickAction: () => SetVolleyFireMode());
            fireAtWillButton.SetUp($"{fireAtWillTitleLocalized} ({toggleFireAtWillKey})", fireAtWillDescLocalized, onClickAction: () => SetFireAtWillMode());
            balancedStanceButton.SetUp($"{balancedStanceTitleLocalized} ({toggleBalancedStanceKey})", balancedStanceDescLocalized, onClickAction: () => SetBalancedStance());
            defensiveStanceButton.SetUp($"{defensiveStanceTitleLocalized} ({toggleDefensiveStanceKey})", defensiveStanceDescLocalized, onClickAction: () => SetDefensiveStance());
            ceaseFireButton.SetUp($"{ceaseFireTitleLocalized} ({ceaseFireKey})", ceaseFireDescLocalized, onClickAction: () => IssueCeaseFireCommand());

            saveFormationButton.SetUp(
                saveFormationTitleLocalized, 
                saveFormationDescLocalized, 
                onClickAction: () => BattleManager.Instance.SquadManager.SaveFormation()
            );
            loadFormationButton.SetUp(
                loadFormationTitleLocalized, 
                loadFormationDescLocalized, 
                onClickAction: () => HandleArmyLoad()
            );

            saveFormationButton.gameObject.SetActive(true);
            loadFormationButton.gameObject.SetActive(true);

            if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
            {
                loadFormationButton.gameObject.SetActive(false);
            }
        }
        private async void HandleArmyLoad()
        {
            loadFormationButton.gameObject.SetActive(false);
            await BattleManager.Instance.ArmySpawnManager.ClearBothArmies();
            await BattleManager.Instance.ArmySpawnManager.LoadBothArmies();
            loadFormationButton.gameObject.SetActive(true);
        }
        private void UpdateBattleButtons(bool atLeastOneSquadSelected)
        {
            selectedSquadButtonsCanvasGroup.interactable = atLeastOneSquadSelected;
            selectedSquadButtonsCanvasGroup.alpha = atLeastOneSquadSelected ? 1f : 0.15f;

            guardModeButton.SetOnOrOff(allSelectedUnitsInGuardMode);

            if (selectedSquadsContainRangedUnits)
            {
                autoRetargetButton.gameObject.SetActive(true);
                meleeModeButton.gameObject.SetActive(true);
                fireAtWillButton.gameObject.SetActive(true);
                volleyFireButton.gameObject.SetActive(true);
                autoRetargetButton.SetOnOrOff(allSelectedSquadsAutoRetarget);
                meleeModeButton.SetOnOrOff(allSelectedSquadsMeleeMode);
                fireAtWillButton.SetOnOrOff(allSelectedSquadsFireAtWill);
                volleyFireButton.SetOnOrOff(allSelectedSquadsVolleyFire);
            }
            else
            {
                autoRetargetButton.gameObject.SetActive(false);
                meleeModeButton.gameObject.SetActive(false);
                fireAtWillButton.gameObject.SetActive(false);
                volleyFireButton.gameObject.SetActive(false);
            }

            // Mages are included: MageCastSystem and MageSquadFindTargetSystem both already respect
            // CeaseFireTag, and RegisterSquad adds the tag to every squad regardless of type, so this
            // is the only thing that was gating the command off for them. The button still reads
            // "Cease Fire" on a mage.
            bool anySelectedSquadCanHoldFire = selectedSquadsContainArtilleryUnits || selectedSquadsContainRangedUnits || selectedSquadsContainMageUnits;
            ceaseFireButton.gameObject.SetActive(anySelectedSquadCanHoldFire);
            if (anySelectedSquadCanHoldFire)
            {
                ceaseFireButton.SetOnOrOff(allSelectedSquadsCeaseFire);
            }

            balancedStanceButton.gameObject.SetActive(selectedSquadsContainShieldedUnits);
            defensiveStanceButton.gameObject.SetActive(selectedSquadsContainShieldedUnits);

            if(selectedSquadsContainShieldedUnits)
            {
                balancedStanceButton.SetOnOrOff(allSelectedSquadBalancedStance);
                defensiveStanceButton.SetOnOrOff(allSelectedSquadsDefensiveStance);
            }

            if(!atLeastOneSquadSelected)
            {
                guardModeButton.SetOnOrOff(false);
                autoRetargetButton.SetOnOrOff(false);
                meleeModeButton.SetOnOrOff(false);
                fireAtWillButton.SetOnOrOff(false);
                volleyFireButton.SetOnOrOff(false);
                balancedStanceButton.SetOnOrOff(false);
                defensiveStanceButton.SetOnOrOff(false);
                ceaseFireButton.SetOnOrOff(false);
            }

        }
        private void IssueHaltCommand()
        {
            if (BattleManager.InstanceIfExists == null) return;
            BattleManager.Instance.UnitPositioningManager.QueueSquadCommand(SquadCommand.HaltAndFreeze, false);
        }
        private void IssueCeaseFireCommand()
        {
            BattleManager.Instance.UnitPositioningManager.QueueSquadCommand(SquadCommand.HaltAndFreeze, false);
            BattleManager.Instance.SquadManager.CeaseFire();
            ceaseFireButton.SetOnOrOff(true);
        }
        private void OnSquadUpdated(int _squadId, float2 _unitCount)
        {
            // Debug.Log($"Squad Updated: {_squadId}");
            RefreshSquadDisplay(_squadId, (int)_unitCount.x);
        }

        private void OnDestroy()
        {
            _isLoaded = false;
            if (BattleManager.HasInstance)
            {
                BattleManager.Instance.OnGamePhaseChanged -= OnGamePhaseChanged;
                BattleManager.Instance.UnitSelectionManager.OnSelectedSquadsChanged -= OnSelectedSquadsChanged;
                BattleManager.Instance.SquadManager.OnSquadUpdated -= OnSquadUpdated;
                BattleManager.Instance.SquadManager.OnDestroyedSquad -= OnSquadDestroyedEvent;
                BattleManager.Instance.OnSquadBrokenEvent -= OnSquadBrokenEvent;
            }
            if (SettingsManager.HasInstance)
            {
                SettingsManager.Instance.OnSettingsPanelToggled -= OnSettingsPanelToggled;
            }
            if (InputHandler.HasInstance)
            {
                InputHandler.Instance.onHideUI -= HideUI;
                InputHandler.Instance.OnToggleAutoRetarget -= ToggleAutoRetarget;
                InputHandler.Instance.OnToggleGuardMode -= ToggleGuardMode;
                InputHandler.Instance.OnToggleMeleeMode -= ToggleMeleeMode;
                InputHandler.Instance.OnHaltCommand -= IssueHaltCommand;
                InputHandler.Instance.OnWithdrawCommand -= OnWithdrawSquadButtonClicked;
                InputHandler.Instance.OnQueueOrder -= EnableAddingOrQueuingIcon;
                InputHandler.Instance.OnQueueOrderCanceled -= CancelAddingOrQueuingIcon;
                InputHandler.Instance.OnSpellQuickCast -= ShowSpellQuickCastMenu;
                InputHandler.Instance.OnSpellQuickCastCanceled -= HideSpellQuickCastMenu;
                InputHandler.Instance.OnFireAtWillModeToggle -= SetFireAtWillMode;
                InputHandler.Instance.OnVolleyFireModeToggle -= SetVolleyFireMode;
                InputHandler.Instance.OnBalancedStanceToggle -= SetBalancedStance;
                InputHandler.Instance.OnDefensiveStanceToggle -= SetDefensiveStance;
                InputHandler.Instance.OnCeaseFireToggled -= SetCeaseFireMode;
            }
            if(BattleManager.HasInstance && BattleManager.Instance.SquadOrderManager != null)
            {
                BattleManager.Instance.SquadOrderManager.OnSquadOrderChanged -= OnSquadOrderReceived;
            }
            if(BattleManager.HasInstance && BattleManager.Instance.BattlefieldEnvManager != null)
            {
                BattleManager.Instance.BattlefieldEnvManager.OnWeatherChanged -= OnWeatherChanged;
            }
            if(balanceOfPowerDisplay != null)
            {
                balanceOfPowerDisplay.ArmyLossesTriggered -= ArmyLossesTriggered;
            }
        }
        private void OnWithdrawSquadButtonClicked()
        {
            if (BattleManager.InstanceIfExists == null) return;
            if (SettingsManager.Instance.SettingsPanelOpen) return;

            switch (BattleManager.Instance.GamePhase)
            {
                case GamePhase.Deployment:
                    if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
                    {
                        NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("CannotWithdrawDuringCampaignDeployment"));
                        break;
                    } 
                    for (int i = 0; i < BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Count; i++)
                    {
                        BattleManager.Instance.SquadManager.WithdrawSquad(BattleManager.Instance.UnitSelectionManager.SelectedSquadIds[i]);
                    }
                    break;
                case GamePhase.Battle:
                    if (BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Count > 1) 
                    {
                        NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("CannotWithdrawMultipleSquadsAtOnce"));
                        break;
                    }
                    for (int i = 0; i < BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Count; i++)
                    {
                        BattleManager.Instance.SquadManager.BreakSelectedSquads();
                    }
                    break;
            }
        }
        private void HandleEndBattle()
        {
            Debug.Log($"HandleEndBattle()");
            continueAfterBattleButton.onClick.RemoveAllListeners();
            restartBattleButton.onClick.RemoveAllListeners();

            string companyShatteredLocalized = LocalizationManager.Instance.GetText("CompanyShattered");
            string enemyHostLocalized = LocalizationManager.Instance.GetText("EnemyHost");
            string victoryLocalized = LocalizationManager.Instance.GetText("Victory");
            string defeatLocalized = LocalizationManager.Instance.GetText("Defeat");
            string defeatedLocalized = LocalizationManager.Instance.GetText("Defeated");

            bool playerWon = BattleManager.Instance.PlayerWon;
            battleOutcomeText.text = playerWon ? enemyHostLocalized + " " + defeatedLocalized : companyShatteredLocalized;
            battleVictoryOrDefeatText.text = playerWon ? victoryLocalized : defeatLocalized;
            endBattlePanel.SetActive(true);

            bool isCustomBattle = BattleManager.Instance.BattleSaveManager.IsCustomBattle;

            continueAfterBattleButton.onClick.AddListener(isCustomBattle ?
                () => BattleManager.Instance.BattleCleanUpManager.LeaveBattleLoadMainMenu() :
                () => BattleManager.Instance.BattleCleanUpManager.LeaveBattleLoadMap()
            );

            continueAfterBattleButtonText.text = isCustomBattle ?
                LocalizationManager.Instance.GetText("exitToMenuButton") :
                LocalizationManager.Instance.GetText("continueButton");

            restartBattleButton.gameObject.SetActive(isCustomBattle);
            if(isCustomBattle)
            {
                restartBattleButton.onClick.AddListener(() =>
                    HandleRestartCustomBattle()
                );
            }
        }
        private async void HandleRestartCustomBattle()
        {
            SceneHandler.Instance.RequestCustomBattleRestart();
            SceneHandler.Instance.RequestSceneCleanUpFunction(GameStateEnum.MainMenu);
        }

        public void HideSpawnErrorMessage()
        {
            spawnErrorMessage.CGDisable();
        }
        public void BroadcastSpawnError()
        {
            NotificationManager.Instance.DisplayNotification(recentPositionErrorMessage);
        }
        public void DisplayBonus(BattlefieldBonus _bonus)
        {
            battlefieldBonusInfo.Load(_bonus);
            battlefieldBonusInfo.Hover();
        }
        public void HideBonus()
        {
            battlefieldBonusInfo.Unhover();
        }

        public void CleanUp()
        {
            void ClearHealthbars()
            {
                foreach (HealthBar healthBar in healthBars.Values)
                {
                    if (healthBar == null) continue;

                    Destroy(healthBar.gameObject);
                }

                healthBars = new();
            }
            void ClearAttackArrows()
            {
                foreach (AttackArrowDrawer attackArrow in attackArrowsDict.Keys)
                {
                    if (attackArrow == null) continue;
                    Destroy(attackArrow.gameObject);
                }
                attackArrowsDict.Clear();
            }

            ClearHealthbars();
            ClearAttackArrows();
        }
        public void MarkSquadAsBroken(int _squadId, bool _broken)
        {
            foreach (SquadDisplayCardBattle squadDisplay in squadDisplays)
            {
                if (squadDisplay.SquadId == _squadId)
                {
                    squadDisplay.SetBroken(_broken);
                }
            }
        }
        /// <summary>
        /// Reacts to SquadOrderManager's authoritative order — sets sibling indices on all
        /// cards to match, then fires OnSquadDisplaysChanged for downstream listeners.
        /// </summary>
        private void OnSquadOrderReceived(IReadOnlyList<int> newOrder)
        {
            for (int i = 0; i < newOrder.Count; i++)
            {
                SquadDisplayCardBattle card = squadDisplays.Find(s => s.SquadId == newOrder[i]);
                if (card != null)
                    card.transform.SetSiblingIndex(i);
            }
            // Keep the list in sibling-index order so that GroupManager.GetGroupNumberForSquadAtIndex
            // can use list indices interchangeably with sibling indices during drag.
            squadDisplays.Sort((a, b) => a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            // Force layout so GroupManager can read settled card positions synchronously.
            LayoutRebuilder.ForceRebuildLayoutImmediate(squadDisplayParent as RectTransform);
            OnSquadDisplaysChanged?.Invoke(squadDisplays);
        }
        /// <summary>
        /// Returns the RectTransform for a squad's display card, or null if not found.
        /// Used by GroupManager for direct GroupUI positioning.
        /// </summary>
        public RectTransform GetCardForSquad(int squadId)
        {
            SquadDisplayCardBattle card = squadDisplays.Find(s => s.SquadId == squadId);
            return card != null ? card.transform as RectTransform : null;
        }
        public void OnWeatherChanged(Weather weather)
        {
            weatherTitleText.text = LocalizationManager.Instance.GetText("Weather") +" " + 
                LocalizationManager.Instance.GetText(weather.ToString());
            weatherDescriptionText.text = LocalizationManager.Instance.GetText(weather.ToString() + "Desc");
        }
        private void EnableAddingOrQueuingIcon()
        {
            if (addingOrQueuingIcon == null) { Debug.LogWarning("addingOrQueuingIcon not assigned in Inspector"); return; }
            addingOrQueuingIcon.enabled = true;
        }
        private void CancelAddingOrQueuingIcon()
        {
            if (addingOrQueuingIcon == null) return;
            addingOrQueuingIcon.enabled = false;
        }
        private void ShowSpellQuickCastMenu()
        {
#if !SPELLS
            return;
#endif
            if (_spellQuickCast == null || isOverUI) return;
            if (BattleManager.Instance.CursorMode == CursorMode.Reposition) return;

            cursorModeBeforeQuickCastMenu = BattleManager.Instance.CursorMode;
            BattleManager.Instance.SetCursorMode(CursorMode.QuickCastMenu);
            _spellQuickCast.SetActive(true);
        }
        private void HideSpellQuickCastMenu()
        {
            if (_spellQuickCast == null) return;
            _spellQuickCast.SetActive(false);

            int queuedSlotIndex = spellQuickCastMenu.ConsumeQueuedSlotIndex();
            if (queuedSlotIndex >= 0)
                BattleManager.Instance.SpellManager.SelectSpell(queuedSlotIndex);

            if (BattleManager.Instance.CursorMode == CursorMode.QuickCastMenu)
                BattleManager.Instance.SetCursorMode(cursorModeBeforeQuickCastMenu);
        }
        public void UpdateBalanceOfPower(BalanceOfPower balanceOfPower)
        {
            balanceOfPowerDisplay.UpdateBalanceOfPowerDisplay(balanceOfPower);
        }
        public void ArmyLossesTriggered(Team teamThatSufferedLosses)
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if(teamThatSufferedLosses == Team.Player)
            {
                entityManager.CreateEntity(typeof(ArmyLossesTriggeredPlayer));
                // Recorded into the run save at battle end ("Against All Odds"); the battle scene has
                // no CampaignSaveManager, so this rides the same static bridge as PauseUsedThisBattle.
                SaveDataHandler.ArmyLossesSufferedThisBattle = true;
            }
            else
            {
                entityManager.CreateEntity(typeof(ArmyLossesTriggeredEnemy));
            }
        }
    }
}
