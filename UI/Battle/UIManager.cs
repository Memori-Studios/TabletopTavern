using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using TMPro;
using System.Collections;
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
using TJ.Map;

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
        [UnityEngine.Serialization.FormerlySerializedAs("addingOrQueuingIcon")]
        [SerializeField] private Image queueingOrderIcon;
        [SerializeField] private Image addingUnitsIcon;

        [Header("Spell Target Hint")]
        [SerializeField] private MemoriCanvasGroup spellTargetHint;
        [SerializeField] private RawImage spellTargetHintIcon;
        [SerializeField] private TMP_Text spellTargetHintText;
        private bool isOverUI;
        // Which words the auto-retarget button currently carries, so the tooltip is rewritten on a change only.
        private bool autoRetargetReadsAsFreeCast;
        private bool ceaseFireReadsAsHoldSpells;

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
        public AttackArrowDrawer AttackArrowPrefab => attackArrowPrefab;

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
        [SerializeField] private EndBattlePanelView endBattleView;
        private TooltipContent endBattleStats;
        private bool endBattleStatsOpen;


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
            ApplyHealthBarScale(BattlefieldMarkerScale.Current);
            BattlefieldMarkerScale.Changed -= ApplyHealthBarScale;
            BattlefieldMarkerScale.Changed += ApplyHealthBarScale;
            SettingsManager.Instance.UIScale.OnValueChanged -= OnUIScaleChangedForHealthBars;
            SettingsManager.Instance.UIScale.OnValueChanged += OnUIScaleChangedForHealthBars;

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
            SceneHandler.Instance.OnSceneSetUpComplete -= OnSceneSetUpComplete;
            SceneHandler.Instance.OnSceneSetUpComplete += OnSceneSetUpComplete;
            InputDevices.Changed -= OnInputDeviceChanged;
            InputDevices.Changed += OnInputDeviceChanged;
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
            if (queueingOrderIcon != null) queueingOrderIcon.enabled = false;
            if (addingUnitsIcon != null) addingUnitsIcon.enabled = false;
            InputHandler.Instance.OnQueueOrder += ShowQueueingOrderIcon;
            InputHandler.Instance.OnQueueOrderCanceled += HideQueueingOrderIcon;
            InputHandler.Instance.OnAddUnitsToSelection += ShowAddingUnitsIcon;
            InputHandler.Instance.OnAddUnitsToSelectionCanceled += HideAddingUnitsIcon;
            InputHandler.Instance.OnFireAtWillModeToggle += SetFireAtWillMode;
            InputHandler.Instance.OnVolleyFireModeToggle += SetVolleyFireMode;
            InputHandler.Instance.OnBalancedStanceToggle += SetBalancedStance;
            InputHandler.Instance.OnDefensiveStanceToggle += SetDefensiveStance;
            InputHandler.Instance.OnCeaseFireToggled += SetCeaseFireMode;
            balanceOfPowerDisplay.ArmyLossesTriggered += ArmyLossesTriggered;
            SaveDataHandler.ArmyLossesSufferedThisBattle = false; // fresh per battle; consumed at battle end

            endBattleView.Hide();
            UpdateBattleButtons(false);

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
            RescanWhileHidden();
        }
        private void HideUI()
        {
            if (SceneHandler.Instance.CurrentGameState != GameStateEnum.Battle) return;

            //check if bug report open
            ReportABugScreen bugScreen = FindFirstObjectByType<ReportABugScreen>();
            if (bugScreen != null && bugScreen.GetComponent<CanvasGroup>().alpha > 0) return;

            SetBattleUIHidden(s_hiddenBy != this);
        }
        #region Hide UI
        // The UIManager whose Hide UI key last hid the battle UI; null while it is shown.
        private static UIManager s_hiddenBy;
        /// <summary>True while the player has hidden the battle UI. Esc shows it again instead of opening Settings.</summary>
        public static bool BattleUIHidden => s_hiddenBy != null;
        public static void RequestShowBattleUI()
        {
            if (s_hiddenBy != null) s_hiddenBy.SetBattleUIHidden(false);
        }
        private const float HiddenRescanSeconds = 0.25f;
        private readonly BattleViewHider hiddenViewHider = new();
        private float nextHiddenRescan;

        // Markers and every other canvas go at once; the HUD panels slide off their nearest edge, like Total War.
        private void SetBattleUIHidden(bool hide)
        {
            s_hiddenBy = hide ? this : null;
            if (hide)
            {
                hiddenViewHider.Hide(null, showFlags: false, slidingHud: HudRootCanvas);
                Cursor.visible = false;
                SetShapesLayerVisible(false);
                nextHiddenRescan = Time.unscaledTime + HiddenRescanSeconds;
            }
            else
            {
                hiddenViewHider.Restore();
                SetShapesLayerVisible(true);
            }
            SetHudHidden(this, hide);
        }
        private void SetShapesLayerVisible(bool visible)
        {
            int bit = 1 << LayerMask.NameToLayer("Shapes");
            Camera battleCamera = BattleManager.Instance.BattleCamera;
            if (visible) battleCamera.cullingMask |= bit;
            else battleCamera.cullingMask &= ~bit;
        }
        // The battle keeps running, so markers, flags and canvases that switch on while hidden are caught as they appear.
        private void RescanWhileHidden()
        {
            if (hudHiders.Count == 0 || Time.unscaledTime < nextHiddenRescan) return;
            nextHiddenRescan = Time.unscaledTime + HiddenRescanSeconds;
            if (s_hiddenBy == this)
            {
                // Leaving photo mode or the follow camera clears this flag even while the Hide UI key holds the view.
                BattleMarkers.Hidden = true;
                hiddenViewHider.RescanCanvases();
                hiddenViewHider.Rescan();
            }
            if (hudFullyOut) SwitchOffHudCanvases();
        }
        #endregion

        #region HUD slide
        // How far the door is open before the HUD starts in, so it lands as the door clears.
        private const float RevealDoorProgress = 0.4f;
        // A door that never reports progress must not keep the HUD away.
        private const float RevealWaitCap = 1.5f;
        // Everyone who wants the HUD away: the Hide UI key, photo mode, the follow camera.
        private readonly HashSet<object> hudHiders = new();
        // Battle HUD canvases this script switched off, so showing puts back exactly those.
        private readonly List<Canvas> hiddenHudCanvases = new();
        private BattleHudSlide hudSlide;
        private Coroutine hudSlideRoutine;
        private bool hudFullyOut;

        /// <summary>The battle HUD's root canvas. A view hider leaves it on, because this script slides it away.</summary>
        public Canvas HudRootCanvas => mainCanvasGroup.GetComponentInParent<Canvas>().rootCanvas;
        private BattleHudSlide HudSlide => hudSlide ??= new BattleHudSlide((RectTransform)mainCanvasGroup.transform, healthBarParent, cursorPopupParent);

        /// <summary>Slides the HUD off screen while any owner wants it hidden, and back once none does. Health bars go at once.</summary>
        public void SetHudHidden(object owner, bool hidden)
        {
            bool wasHidden = hudHiders.Count > 0;
            if (hidden) hudHiders.Add(owner);
            else hudHiders.Remove(owner);
            if ((hudHiders.Count > 0) == wasHidden) return;

            if (hudSlideRoutine != null) StopCoroutine(hudSlideRoutine);
            hudFullyOut = false;
            if (!wasHidden)
            {
                SwitchOffHudCanvas(healthBarParent.GetComponent<Canvas>());
                SwitchOffHudCanvas(cursorPopupParent.GetComponent<Canvas>());
                hudSlideRoutine = StartCoroutine(SlideHudOut());
            }
            else
            {
                foreach (Canvas canvas in hiddenHudCanvases)
                    if (canvas != null) canvas.enabled = true;
                hiddenHudCanvases.Clear();
                hudSlideRoutine = StartCoroutine(HudSlide.Slide(false));
            }
        }
        private IEnumerator SlideHudOut()
        {
            yield return HudSlide.Slide(true);
            hudFullyOut = true;
            SwitchOffHudCanvases();
        }
        // Off screen is not enough: a panel that switches on while hidden must not appear.
        private void SwitchOffHudCanvases()
        {
            foreach (Canvas canvas in HudRootCanvas.GetComponentsInChildren<Canvas>())
                SwitchOffHudCanvas(canvas);
        }
        private void SwitchOffHudCanvas(Canvas canvas)
        {
            if (canvas == null || !canvas.enabled) return;
            canvas.enabled = false;
            hiddenHudCanvases.Add(canvas);
        }
        // The HUD waits off screen behind the closed door and slides in as it opens.
        private void OnSceneSetUpComplete()
        {
            if (hudHiders.Count > 0) return;
            if (hudSlideRoutine != null) StopCoroutine(hudSlideRoutine);
            HudSlide.SnapOut();
            hudSlideRoutine = StartCoroutine(SlideHudInWithDoor());
        }
        private IEnumerator SlideHudInWithDoor()
        {
            // This event fires before the door is told to open, so its progress is read from the next frame.
            yield return null;
            float waited = 0f;
            while (SceneHandler.Instance.DoorOpenProgress < RevealDoorProgress && waited < RevealWaitCap)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return HudSlide.Slide(false);
        }
        #endregion

        #region Deployment slide
        private BattleHudSlide deploymentSlide;
        private Coroutine deploymentSlideRoutine;

        // Clicks and keys stop the moment the battle starts, so nothing on the leaving panels can be pressed late.
        private void SlideDeploymentOut()
        {
            deploymentCanvasGroup.interactable = false;
            deploymentCanvasGroup.blocksRaycasts = false;
            deploymentSlide ??= new BattleHudSlide((RectTransform)deploymentCanvasGroup.transform);
            if (deploymentSlideRoutine != null) StopCoroutine(deploymentSlideRoutine);
            deploymentSlideRoutine = StartCoroutine(SlideDeploymentOutThenDisable());
        }
        private IEnumerator SlideDeploymentOutThenDisable()
        {
            yield return deploymentSlide.Slide(true);
            deploymentCanvasGroup.CGDisable();
        }
        private void SlideDeploymentIn()
        {
            deploymentCanvasGroup.CGEnable();
            if (deploymentSlide == null) return;
            if (deploymentSlideRoutine != null) StopCoroutine(deploymentSlideRoutine);
            deploymentSlideRoutine = StartCoroutine(deploymentSlide.Slide(false));
        }
        #endregion
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
        public HealthBar LoadHealthbar(Transform _squadPosition, Team _faction, int id, int ammunition, bool isGate, bool hasCooldown)
        {
            HealthBar healthBar = Instantiate(healthBarPrefab, healthBarParent);
            healthBar.SetUp(_faction, _squadPosition, ammunition, isGate, hasCooldown);

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
        private void StartBattle()
        {
            IAudioRequester.Instance.PlaySFX("start-battle");
            // The button slides away with the deployment panels, switched off so it cannot be pressed twice.
            startBattleButton.interactable = false;
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
            if (!TryShowComparison(_squad))
                LoadSquadBattleInfo(_squad);

            // await Task.Delay(500);
            if (cachedSquadHovered == 0) return;

            squadHoveredTooltip.Hover();
        }
        private void LoadSquadBattleInfo(SquadEntity _squad) => LoadSquadBattleInfo(squadBattleInfo, _squad);
        private void LoadSquadBattleInfo(SquadBattleInfo _panel, SquadEntity _squad)
        {
            int unitCount = BattleManager.Instance.SquadManager.GetSquadUnitCount(_squad.SquadId);
            int prestige = 0;
            if (_squad.SquadId != 0) prestige = GetUnitPrestige(_squad.SquadId);

            _panel.SetUpBattle(_squad, unitCount, prestige);
        }
        // With a squad selected, the panel keeps showing it and the hovered squad hangs off its right.
        private bool TryShowComparison(SquadEntity _hovered)
        {
            List<int> selectedIds = BattleManager.Instance.UnitSelectionManager.SelectedSquadIds;
            if (_hovered.SquadId == 0 || selectedIds.Count == 0 || selectedIds[0] == _hovered.SquadId) return false;
            SquadEntity selected = BattleManager.Instance.SquadManager.GetSquad(selectedIds[0]);
            if (selected.SquadId == 0) return false;

            LoadSquadBattleInfo(selected);
            squadBattleInfo.ShowComparison(panel => LoadSquadBattleInfo(panel, _hovered));
            return true;
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
                    SlideDeploymentIn();
                    if (!BattleManager.Instance.BattleSaveManager.IsCustomBattle)
                        startBattleButton.gameObject.SetActive(true);
                    break;
                case GamePhase.Battle:
                    SlideDeploymentOut();
                    break;
                case GamePhase.PostGame:
                    HandleEndBattle();
                    break;
            }
        }
        /// <summary>1-based position of the squad's card in the bottom row, 0 if it has no card.</summary>
        public int GetSquadCardNumber(int squadId)
        {
            for (int i = 0; i < squadDisplays.Count; i++)
                if (squadDisplays[i] != null && squadDisplays[i].SquadId == squadId) return i + 1;
            return 0;
        }
        public SquadDisplayCardBattle GetSquadCard(int squadId)
        {
            for (int i = 0; i < squadDisplays.Count; i++)
                if (squadDisplays[i] != null && squadDisplays[i].SquadId == squadId) return squadDisplays[i];
            return null;
        }
        // The strip's authored height above the cards, and how far it moves up while mage tiles stand on them.
        private float spellTargetHintRestY;
        private bool spellTargetHintRestYCached;
        private const float SPELL_TARGET_HINT_LIFT = 74f;
        public void SetSpellTargetHintLifted(bool lifted)
        {
            if (spellTargetHint == null) return;
            RectTransform rect = (RectTransform)spellTargetHint.transform;
            if (!spellTargetHintRestYCached)
            {
                spellTargetHintRestY = rect.anchoredPosition.y;
                spellTargetHintRestYCached = true;
            }
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, spellTargetHintRestY + (lifted ? SPELL_TARGET_HINT_LIFT : 0f));
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
                    // AutoTarget doubles as Free Cast for a mage.
                    if (!squad.AutoTarget)
                    {
                        allSelectedSquadsAutoRetarget = false;
                    }
                }

                if(TabletopTavernConstants.HoldsFire(squad.UnitType))
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
                if (cachedSquadHovered == 0 || !TryShowComparison(BattleManager.Instance.SquadManager.GetSquad(cachedSquadHovered)))
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

        #region Action button keys
        // A pad with no button for an action leaves the bracket off, since the tooltip already sits on the button.
        private static string Titled(string title, UnityEngine.InputSystem.InputAction action)
        {
            string key = InputGlyphs.For(action);
            return key == null ? title : $"{title} ({key})";
        }

        private void RetitleAutoRetarget()
        {
            autoRetargetButton.SetTooltip(
                Titled(LocalizationManager.Instance.GetText(autoRetargetReadsAsFreeCast ? "MageFreeCastTitle" : "AutoRetargetTitle"), InputHandler.Instance.GameControls.Battle.ToggleAutoRetarget),
                LocalizationManager.Instance.GetText(autoRetargetReadsAsFreeCast ? "MageFreeCastDesc" : "AutoRetargetDesc"));
        }

        private void RetitleCeaseFire()
        {
            ceaseFireButton.SetTooltip(
                Titled(LocalizationManager.Instance.GetText(ceaseFireReadsAsHoldSpells ? "MageHoldSpellsTitle" : "CeaseFireTitle"), InputHandler.Instance.GameControls.Battle.CeaseFireCommand),
                LocalizationManager.Instance.GetText(ceaseFireReadsAsHoldSpells ? "MageHoldSpellsDesc" : "CeaseFireDesc"));
        }

        // Rewrites the titles only, so a device switch mid-battle keeps every button's on or off state.
        private void OnInputDeviceChanged()
        {
            var battle = InputHandler.Instance.GameControls.Battle;
            void Retitle(BattleButton button, string titleKey, string descKey, UnityEngine.InputSystem.InputAction action, bool keyword = false)
            {
                string desc = LocalizationManager.Instance.GetText(descKey);
                button.SetTooltip(Titled(LocalizationManager.Instance.GetText(titleKey), action), keyword ? KeywordText.ForTooltip(desc) : desc);
            }
            Retitle(guardModeButton, "GuardModeTitle", "GuardModeDesc", battle.ToggleGuardMode);
            Retitle(haltButton, "HaltSquad", "HaltSquadDesc", battle.Halt);
            Retitle(withdrawButton, "WithdrawSquad", "WithdrawSquadDesc", battle.Withdraw);
            Retitle(meleeModeButton, "MeleeModeTitle", "MeleeModeDesc", battle.ToggleMeleeMode);
            Retitle(volleyFireButton, "VolleyFireTitle", "VolleyFireDesc", battle.ToggleVolleyFireMode);
            Retitle(fireAtWillButton, "FireAtWillTitle", "FireAtWillDesc", battle.ToggleFireAtWillMode);
            Retitle(balancedStanceButton, "BalancedStanceTitle", "BalancedStanceDesc", battle.SetBalancedStance, true);
            Retitle(defensiveStanceButton, "DefensiveStanceTitle", "DefensiveStanceDesc", battle.SetDefensiveStance, true);
            RetitleAutoRetarget();
            RetitleCeaseFire();
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
            string balancedStanceDescLocalized = KeywordText.ForTooltip(LocalizationManager.Instance.GetText("BalancedStanceDesc"));
            string defensiveStanceTitleLocalized = LocalizationManager.Instance.GetText("DefensiveStanceTitle");
            string defensiveStanceDescLocalized = KeywordText.ForTooltip(LocalizationManager.Instance.GetText("DefensiveStanceDesc"));

            
            guardModeButton.SetUp(Titled(guardModeTitleLocalized, InputHandler.Instance.GameControls.Battle.ToggleGuardMode), guardModeDescLocalized, SetGuardMode);
            haltButton.SetUp(Titled(haltTitleLocalized, InputHandler.Instance.GameControls.Battle.Halt), haltDescLocalized, onClickAction: () => IssueHaltCommand());
            withdrawButton.SetUp(Titled(withdrawTitleLocalized, InputHandler.Instance.GameControls.Battle.Withdraw), withdrawDescLocalized, onClickAction: () => OnWithdrawSquadButtonClicked());
            autoRetargetButton.SetUp(Titled(autoRetargetTitleLocalized, InputHandler.Instance.GameControls.Battle.ToggleAutoRetarget), autoRetargetDescLocalized, SetAutoRetarget);
            meleeModeButton.SetUp(Titled(meleeModeTitleLocalized, InputHandler.Instance.GameControls.Battle.ToggleMeleeMode), meleeModeDescLocalized, SetMeleeMode);
            volleyFireButton.SetUp(Titled(volleyFireTitleLocalized, InputHandler.Instance.GameControls.Battle.ToggleVolleyFireMode), volleyFireDescLocalized, onClickAction: () => SetVolleyFireMode());
            fireAtWillButton.SetUp(Titled(fireAtWillTitleLocalized, InputHandler.Instance.GameControls.Battle.ToggleFireAtWillMode), fireAtWillDescLocalized, onClickAction: () => SetFireAtWillMode());
            balancedStanceButton.SetUp(Titled(balancedStanceTitleLocalized, InputHandler.Instance.GameControls.Battle.SetBalancedStance), balancedStanceDescLocalized, onClickAction: () => SetBalancedStance());
            defensiveStanceButton.SetUp(Titled(defensiveStanceTitleLocalized, InputHandler.Instance.GameControls.Battle.SetDefensiveStance), defensiveStanceDescLocalized, onClickAction: () => SetDefensiveStance());
            ceaseFireButton.SetUp(Titled(ceaseFireTitleLocalized, InputHandler.Instance.GameControls.Battle.CeaseFireCommand), ceaseFireDescLocalized, onClickAction: () => IssueCeaseFireCommand());

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
                // A mage-only selection still gets the auto-retarget button, read as Free Cast: the
                // same AutoTarget flag, the same B key, only the words change.
                autoRetargetButton.gameObject.SetActive(selectedSquadsContainMageUnits);
                if (selectedSquadsContainMageUnits) autoRetargetButton.SetOnOrOff(allSelectedSquadsAutoRetarget);
                meleeModeButton.gameObject.SetActive(false);
                fireAtWillButton.gameObject.SetActive(false);
                volleyFireButton.gameObject.SetActive(false);
            }

            // Mages are included: MageCastSystem and MageSquadFindTargetSystem both already respect
            // CeaseFireTag, and RegisterSquad adds the tag to every squad regardless of type, so this
            // is the only thing that was gating the command off for them.
            bool mageOnlyFreeCast = selectedSquadsContainMageUnits && !selectedSquadsContainRangedUnits;
            if (mageOnlyFreeCast != autoRetargetReadsAsFreeCast)
            {
                autoRetargetReadsAsFreeCast = mageOnlyFreeCast;
                RetitleAutoRetarget();
            }

            // A mage-only selection reads the same button as "Hold Spells"; anything with a shooter in it keeps
            // "Cease Fire", since both meanings apply.
            bool mageOnlyHoldSpells = selectedSquadsContainMageUnits && !selectedSquadsContainRangedUnits && !selectedSquadsContainArtilleryUnits;
            if (mageOnlyHoldSpells != ceaseFireReadsAsHoldSpells)
            {
                ceaseFireReadsAsHoldSpells = mageOnlyHoldSpells;
                RetitleCeaseFire();
            }

            bool anySelectedSquadCanHoldFire = selectedSquadsContainArtilleryUnits || selectedSquadsContainRangedUnits || selectedSquadsContainMageUnits;
            ceaseFireButton.gameObject.SetActive(anySelectedSquadCanHoldFire);
            if (anySelectedSquadCanHoldFire)
            {
                ceaseFireButton.SetOnOrOff(allSelectedSquadsCeaseFire);
            }
            // Shooters only: a mage-only selection reads this button as Hold Spells.
            if (selectedSquadsContainRangedUnits || selectedSquadsContainArtilleryUnits)
            {
                string ceaseFireKey = InputGlyphs.For(InputHandler.Instance.GameControls.Battle.CeaseFireCommand)
                    ?? LocalizationManager.Instance.GetText("InputOnScreen");
                // Open left: the unit card strip sits above the action row, right of this button.
                TutorialManager.Instance.LoadTooltip(TutorialData.CeaseFire, ceaseFireButton.transform, CalloutSide.Left, ceaseFireKey);
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
            TutorialManager.Instance.CloseTooltip();
        }
        private void OnSquadUpdated(int _squadId, float2 _unitCount)
        {
            // Debug.Log($"Squad Updated: {_squadId}");
            RefreshSquadDisplay(_squadId, (int)_unitCount.x);
        }

        // Health bars sit on the overlay canvas, which the UI Scale already grows; divide it back out so they follow the marker setting instead.
        private void ApplyHealthBarScale(float markerScale)
        {
            healthBarParent.localScale = Vector3.one * (markerScale / SettingsManager.Instance.UIScale.Value);
        }
        private void OnUIScaleChangedForHealthBars(float _) => ApplyHealthBarScale(BattlefieldMarkerScale.Current);
        private void OnDestroy()
        {
            _isLoaded = false;
            if (s_hiddenBy == this) s_hiddenBy = null;
            // Core canvases, the cursor and the marker flag outlive the battle scene.
            hiddenViewHider.Restore(sceneClosing: true);
            if (SceneHandler.HasInstance) SceneHandler.Instance.OnSceneSetUpComplete -= OnSceneSetUpComplete;
            InputDevices.Changed -= OnInputDeviceChanged;
            BattlefieldMarkerScale.Changed -= ApplyHealthBarScale;
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
                SettingsManager.Instance.UIScale.OnValueChanged -= OnUIScaleChangedForHealthBars;
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
                InputHandler.Instance.OnQueueOrder -= ShowQueueingOrderIcon;
                InputHandler.Instance.OnQueueOrderCanceled -= HideQueueingOrderIcon;
                InputHandler.Instance.OnAddUnitsToSelection -= ShowAddingUnitsIcon;
                InputHandler.Instance.OnAddUnitsToSelectionCanceled -= HideAddingUnitsIcon;
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
                    if (BattleManager.Instance.SquadManager.OrdealActive(OrdealId.NoRetreat))
                    {
                        NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("OrdealNoRetreatBlocked"));
                        break;
                    }
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
        #region End battle
        private void HandleEndBattle()
        {
            Debug.Log($"HandleEndBattle()");
            bool playerWon = BattleManager.Instance.PlayerWon;
            BattleSaveManager saves = BattleManager.Instance.BattleSaveManager;
            bool isCustomBattle = saves.IsCustomBattle;
            List<ArmySpawnManager.EndBattleSquad> results = BattleManager.Instance.ArmySpawnManager.BuildEndBattleResults();

            var yours = new List<DamageReportRow>();
            var enemy = new List<DamageReportRow>();
            int slain = 0, troopsLost = 0, squadsLost = 0;
            UnitName? yourFirst = null, enemyFirst = null;
            foreach (ArmySpawnManager.EndBattleSquad squad in results)
            {
                var row = new DamageReportRow { Unit = squad.Unit, Damage = squad.Damage, Value = squad.Value, Kills = squad.Kills, Lost = squad.Lost };
                if (squad.IsPlayer)
                {
                    row.Fallen = squad.UnitsLeft <= 0;
                    yours.Add(row);
                    slain += squad.Kills;
                    troopsLost += squad.Lost;
                    if (row.Fallen) squadsLost++;
                    yourFirst ??= squad.Unit;
                }
                else
                {
                    enemy.Add(row);
                    enemyFirst ??= squad.Unit;
                }
            }
            yours.Sort(DamageReportTooltip.ByValue);
            enemy.Sort(DamageReportTooltip.ByValue);
            TabletopTavernData data = TabletopTavernData.Instance;
            Race yourRace = yourFirst.HasValue ? data.GetRaceFromUnitName(yourFirst.Value) : Race.IronLegion;
            Race enemyRace = enemyFirst.HasValue ? data.GetRaceFromUnitName(enemyFirst.Value) : Race.IronLegion;

            string title = GetText(playerWon ? "Victory" : "Defeat");
            string outcome = playerWon ? GetText(saves.IsGarrisonBattle ? "engagementOutcomeGarrison" : "engagementOutcomeHost") : GetText("CompanyShattered");
            string caption = isCustomBattle ? GetText("customBattleButton") : GetText("Act") + " " + Memori.UI.MemoriUI.ConvertNumberToRomanNumeral(Mathf.Max(1, SaveDataHandler.Load().bookNumber));
            string losses = string.Format(GetText("engagementTroopsCount"), troopsLost.ToString("N0"));
            if (troopsLost > 0) losses = $"<color={ColorData.Negative}>{losses}</color>";
            // A lost campaign battle ends the run; the map's result says so again with the run summary.
            string defeatLine = !playerWon && !isCustomBattle ? GetText("engagementDefeatLine") : null;
            endBattleStats = DamageReportTooltip.Build(yours, enemy, string.Format(GetText("engagementResultSub"), outcome, caption),
                endBattleView.DamageIcon, yourRace, slain, troopsLost, squadsLost, pinned: true);

            endBattleView.ClearBadges();
            endBattleView.Show(playerWon, title, outcome, caption, GetText(enemyRace.ToString()), slain.ToString("N0"), losses, defeatLine, isCustomBattle);
            ShowEndBattleBadges(results);

            endBattleView.DetailedStatsButton.onClick.RemoveAllListeners();
            endBattleView.DetailedStatsButton.onClick.AddListener(ToggleEndBattleStats);
            endBattleView.ContinueButton.onClick.RemoveAllListeners();
            endBattleView.ContinueButton.onClick.AddListener(() =>
            {
                CloseEndBattleStats();
                BattleManager.Instance.BattleCleanUpManager.LeaveBattleLoadMap();
            });
            endBattleView.ExitButton.onClick.RemoveAllListeners();
            endBattleView.ExitButton.onClick.AddListener(() =>
            {
                CloseEndBattleStats();
                BattleManager.Instance.BattleCleanUpManager.LeaveBattleLoadMainMenu();
            });
            endBattleView.RematchButton.onClick.RemoveAllListeners();
            endBattleView.RematchButton.onClick.AddListener(() =>
            {
                CloseEndBattleStats();
                HandleRestartCustomBattle();
            });
        }

        // Kills and losses on each surviving card; the squad with the most kills gets the gold frame.
        private void ShowEndBattleBadges(List<ArmySpawnManager.EndBattleSquad> results)
        {
            int best = 0, bestKills = 0;
            foreach (ArmySpawnManager.EndBattleSquad squad in results)
                if (squad.IsPlayer && squad.Kills > bestKills && GetSquadCard(squad.SquadId) != null)
                {
                    best = squad.SquadId;
                    bestKills = squad.Kills;
                }
            string mostSlain = GetText("endBattleMostSlain");
            foreach (ArmySpawnManager.EndBattleSquad squad in results)
            {
                if (!squad.IsPlayer) continue;
                SquadDisplayCardBattle card = GetSquadCard(squad.SquadId);
                if (card == null) continue;
                endBattleView.AddBadge((RectTransform)card.transform, squad.Kills, squad.Lost, bestKills > 0 && squad.SquadId == best, mostSlain);
            }
        }

        // Detailed stats opens the shared Damage dealt panel pinned under the card; a second click or its red X closes it.
        private void ToggleEndBattleStats()
        {
            if (endBattleStatsOpen)
            {
                CloseEndBattleStats();
                return;
            }
            if (endBattleStats == null) return;
            endBattleStatsOpen = true;
            endBattleView.SetStatsOpen(true);
            TooltipManager.Instance.Pin(endBattleStats, endBattleView.Card, TooltipSide.Below, () =>
            {
                endBattleStatsOpen = false;
                if (endBattleView != null) endBattleView.SetStatsOpen(false);
            });
        }

        private void CloseEndBattleStats()
        {
            if (endBattleStatsOpen && TooltipManager.HasInstance) TooltipManager.Instance.Unpin();
        }

        private static string GetText(string key) => LocalizationManager.Instance.GetText(key);
        #endregion
        private void HandleRestartCustomBattle()
        {
            SceneHandler.Instance.RequestCustomBattleRestart();
            SceneHandler.Instance.RequestSceneCleanUpFunction(GameStateEnum.MainMenu);
        }

        public void HideSpawnErrorMessage()
        {
            spawnErrorMessage.CGDisable();
        }
        /// <summary>
        /// A short instruction under the cursor's context, e.g. "Select a squad to blink" while a
        /// placement spell waits for a selection. Rides on the position-error label so it needs no
        /// scene wiring; the two never show at the same time.
        /// </summary>
        public void ShowCursorHint(string message)
        {
            spawnErrorText.text = message;
            spawnErrorMessage.FadeInAsync(0.25f, false, false);
        }
        public void HideCursorHint() => spawnErrorMessage.CGDisable();
        /// <summary>
        /// The fixed label above the unit cards while a spell is armed: the current targeting cursor
        /// and either "Invalid target. Valid targets: X" or the click instructions. Rich text is allowed.
        /// </summary>
        public void ShowSpellTargetHint(Texture icon, string message)
        {
            if(spellTargetHint == null) { Debug.LogError("UIManager: spellTargetHint is not wired", this); return; }
            spellTargetHintIcon.texture = icon;
            spellTargetHintIcon.enabled = icon != null;
            spellTargetHintText.text = message;
            if(spellTargetHint.alpha < 1f) spellTargetHint.FadeInAsync(0.15f, false, false);
        }
        public void HideSpellTargetHint()
        {
            if(spellTargetHint == null) return;
            spellTargetHint.CGDisable();
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
            weatherDescriptionText.text = WeatherInfo.GetDescription(weather);
        }
        private void ShowQueueingOrderIcon()
        {
            if (queueingOrderIcon == null) { Debug.LogWarning("queueingOrderIcon not assigned in Inspector"); return; }
            queueingOrderIcon.enabled = true;
        }
        private void HideQueueingOrderIcon()
        {
            if (queueingOrderIcon == null) return;
            queueingOrderIcon.enabled = false;
        }
        private void ShowAddingUnitsIcon()
        {
            if (addingUnitsIcon == null) { Debug.LogWarning("addingUnitsIcon not assigned in Inspector"); return; }
            addingUnitsIcon.enabled = true;
        }
        private void HideAddingUnitsIcon()
        {
            if (addingUnitsIcon == null) return;
            addingUnitsIcon.enabled = false;
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
                // The morale penalty is invisible otherwise, and full-health squads breaking reads as a bug.
                NotificationManager.Instance.DisplayNotification(LocalizationManager.Instance.GetText("ArmyLossesNotification"));
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
