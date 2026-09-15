using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Audio;
using Memori.Input;
using Memori.Localization;
using Memori.Notifications;
using Memori.SaveData;
using Memori.Steamworks;
using Unity.Entities;
using UnityEngine;

namespace TJ.Spells
{
public class SpellManager : MonoBehaviour
{
    [SerializeField] private LayerMask validSpellCastLayerMask;
    //temp, only for testing
    [SerializeField] private SpellData[] defaultSpells;
    [SerializeField] private SpellCastButton[] spellCastButtons;
    [SerializeField] private SpellQuickCastMenu spellQuickCastMenu;

    [Header("Pre-Battle Browsing (custom battle only)")]
    // The pool the player can swap from is every registered spell (SpellRegistry.All). It used to be
    // a hand-curated inspector list, which silently fell behind when spells were added.
    [SerializeField] private SpellBrowseMenu spellBrowseMenu;
    // Seconds the browse menu lingers after the pointer leaves both the spell buttons and the menu,
    // so crossing the gap between them does not flicker it closed.
    [SerializeField] private float browseCloseDelay = 0.12f;

    private class SpellSlotState
    {
        public SpellData SpellData;
        public float CooldownDuration;
        public float CooldownRemaining;
        public bool OnCooldown => CooldownRemaining > 0f;
    }
    private SpellSlotState[] slotStates;
    private int selectedSpellIndex = -1;
    private Entity targetedSquadSelfEntity = Entity.Null;

    // Placement spells. Starstep first asks for a squad, then both it and Raise Dead draw a formation
    // on the cursor and resolve on the right-click that confirms it (right-drag rotates and widens,
    // left-click cancels - the same hands as a move order or a deployment spawn). While a phase is
    // active the ordinary spell cursor (GetMouseCursorPosition) stands down.
    public enum SpellPlacementPhase { None, AwaitingSquad, Placing }
    public SpellPlacementPhase PlacementPhase => placementPhase;
    private SpellPlacementPhase placementPhase = SpellPlacementPhase.None;
    private int placementSlot = -1;
    private static bool IsPlacementSpell(SpellData s) => s != null && (s.TeleportsSquad || s.SummonsSquad);

    // Per-battle mana. Granted whole in LoadSpellManager, spent permanently, never regenerated and
    // never carried over - so battle length does not change how many casts a player gets. This
    // component dies with the TavernBattle scene, which is exactly the lifetime the pool wants.
    private int manaRemaining;
    private int manaMax;
    public int ManaRemaining => manaRemaining;
    public int ManaMax => manaMax;
    public event Action<int, int> OnManaChanged;

    // Pre-battle browse state.
    private bool browsingEnabled;
    private int hoveredButtonSlot = -1;
    private bool pointerOverBrowseMenu;
    private Coroutine browseCloseRoutine;
    // Slot whose cast button is currently highlighted green because the browse menu is open for it (-1 = none).
    private int browseHighlightSlot = -1;

    private bool validSpellCastPoint;
    private Vector3 spellCursorOrigin;
    public Vector3 SpellCursorOrigin => spellCursorOrigin;
    public bool ValidSpellCastPoint => validSpellCastPoint;
    public float SelectedSpellRadius => selectedSpellIndex >= 0 ? slotStates[selectedSpellIndex].SpellData.SpellRadius : 0f;
    int spellsCast = 0;
    // Bitmask of slots cast at least once this battle, for the "Full Arsenal" achievement.
    int slotsCastMask = 0;
    bool mouseReleased = true;
    public bool MouseReleased => mouseReleased;

    private void Start()
    {
#if SPELLS
        BattleManager.Instance.OnCursorModeChanged += CursorModeChanged;
        BattleManager.Instance.OnGamePhaseChanged += GamePhaseChanged;
        InputHandler.Instance.OnSelectSpell1 += SelectSpellHotkey1;
        InputHandler.Instance.OnSelectSpell2 += SelectSpellHotkey2;
        InputHandler.Instance.OnSelectSpell3 += SelectSpellHotkey3;
        InputHandler.Instance.OnSelectSpell4 += SelectSpellHotkey4;
        if(UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.OnSelectedSquadsChanged -= OnSelectedSquadsChangedForPlacement;
            UnitSelectionManager.Instance.OnSelectedSquadsChanged += OnSelectedSquadsChangedForPlacement;
        }
#endif
    }
    private void Update()
    {
        if(slotStates == null) return;

        for (int i = 0; i < slotStates.Length; i++) {
            SpellSlotState slot = slotStates[i];
            if(slot.CooldownRemaining <= 0f) continue;

            slot.CooldownRemaining -= Time.deltaTime;
            bool justFinished = slot.CooldownRemaining <= 0f;
            if(justFinished) slot.CooldownRemaining = 0f;

            spellCastButtons[i].RenderCooldown(slot.CooldownRemaining / slot.CooldownDuration, !justFinished);
            if(justFinished) spellCastButtons[i].FlashCooldownImage(Color.white);
        }
    }
    public void LoadSpellManager(SpellData[] _spells = null)
    {
        // Swapping is a pre-battle, custom-battle convenience only. It stays off for campaign battles.
        browsingEnabled = BattleManager.Instance.BattleSaveManager.IsCustomBattle;

        if(_spells != null) {
            defaultSpells = _spells;
        } else if(!browsingEnabled) {
            // Campaign battle: the loadout was chosen at run setup and persisted on the campaign
            // save. Custom battles keep the serialized inspector list, which the browse menu edits.
            SpellData[] campaignSpells = SaveDataHandler.GetCampaignSpells();
            if(campaignSpells != null && campaignSpells.Length > 0) defaultSpells = campaignSpells;
        }

        EnsureLoadoutCoversHotbar();

        if(_spells == null && browsingEnabled) ApplySavedCustomBattleSpells();

        // Two passes on purpose. State is fully populated before any View code runs, so a missing
        // serialized reference on a hotbar prefab throws ONCE and stays readable - filling and wiring
        // in one loop left slotStates half-null, and Update() then NRE'd every frame on the null tail
        // and buried the real exception under the spam. LoadSpellManager is called from an async Task
        // (BattleCleanUpManager), so a swallowed root exception is a live hazard here.
        slotStates = new SpellSlotState[spellCastButtons.Length];
        for (int i = 0; i < spellCastButtons.Length; i++) {
            // A hotbar with more buttons than the loadout has slots leaves the extras empty rather
            // than throwing - every consumer of a loadout array handles a null SpellData slot.
            slotStates[i] = new SpellSlotState { SpellData = defaultSpells[i] };
        }

        manaMax = SaveDataHandler.GetSpellManaPool();
        manaRemaining = manaMax;

        for (int i = 0; i < spellCastButtons.Length; i++) {
            if(spellCastButtons[i] == null) {
                Debug.LogError($"SpellManager: spellCastButtons[{i}] is not assigned. That slot is unusable.");
                // No button means no way to render or select it, so the slot reads as empty rather
                // than as a castable spell that would NRE the moment it went on cooldown.
                slotStates[i].SpellData = null;
                continue;
            }
            int slotIndex = i;
            WireSlotButton(slotIndex, slotStates[i].SpellData);
            // Custom battles are a sandbox and bypass the unlock gate entirely - the browse pool
            // already ignores IsUnlocked - so slot locking is a campaign-only concern.
            spellCastButtons[i].SetLocked(!browsingEnabled && SpellLoadout.IsSlotLocked(slotIndex));
            // The picker's info panel describes a slot the moment it is hovered, so the button's own
            // floating tooltip stands down for as long as the picker is available.
            spellCastButtons[i].SetBrowseModeActive(browsingEnabled);
        }
        selectedSpellIndex = -1;
        slotsCastMask = 0;

        // After the wiring pass - LoadSpellUI resets each button's affordability to true.
        RefreshAffordability();
        OnManaChanged?.Invoke(manaRemaining, manaMax);

        spellQuickCastMenu.Load(defaultSpells);

        if(browsingEnabled && spellBrowseMenu != null)
            spellBrowseMenu.Initialize(new List<SpellData>(SpellRegistry.All).ToArray(), SwapSpell, OnBrowseMenuHoverEnter, OnBrowseMenuHoverExit);
    }
    /// <summary>
    /// Grows the loadout so it covers every hotbar button, because SwapSpell writes back into
    /// defaultSpells by slot index. Grow only - a loadout longer than the hotbar keeps its extras.
    /// </summary>
    private void EnsureLoadoutCoversHotbar()
    {
        if(defaultSpells != null && defaultSpells.Length >= spellCastButtons.Length) return;

        SpellData[] resized = new SpellData[spellCastButtons.Length];
        if(defaultSpells != null) Array.Copy(defaultSpells, resized, defaultSpells.Length);
        defaultSpells = resized;
    }
    private void WireSlotButton(int slotIndex, SpellData spellData)
    {
        Action browseEnter = browsingEnabled ? () => OnButtonBrowseHoverEnter(slotIndex) : null;
        Action browseExit = browsingEnabled ? () => OnButtonBrowseHoverExit(slotIndex) : null;
        spellCastButtons[slotIndex].LoadSpellUI(spellData, () => SelectSpell(slotIndex), slotIndex + 1, browseEnter, browseExit);
    }
    /// <summary>
    /// Unit names any equipped spell can summon, so their GPU anim prefabs can be preloaded with the
    /// two armies at battle start. Reads the serialized spell list directly rather than slotStates so
    /// this does not depend on LoadSpellManager having run yet.
    /// </summary>
    public IEnumerable<UnitName> GetSummonUnitNames()
    {
        List<UnitName> summonUnitNames = new();
        if(defaultSpells == null) return summonUnitNames;

        foreach(SpellData spell in defaultSpells)
        {
            if(spell == null || !spell.SummonsSquad) continue;
            summonUnitNames.Add(spell.SummonedUnitName);
        }
        return summonUnitNames;
    }
    #region Custom battle persistence
    /// <summary>
    /// The hotbar as enum values, slot by slot, for the custom-battle save written by
    /// SquadManager.SaveFormation. An empty slot is recorded as Spell.None so the array keeps its
    /// slot alignment; ApplySavedCustomBattleSpells treats None as "leave the inspector default".
    /// </summary>
    public Spell[] GetEquippedSpellEnums()
    {
        Spell[] equipped = new Spell[slotStates.Length];
        for (int i = 0; i < slotStates.Length; i++)
            equipped[i] = slotStates[i].SpellData == null ? Spell.None : slotStates[i].SpellData.Spell;
        return equipped;
    }
    /// <summary>
    /// Overlays the spells saved with the custom-battle army onto the inspector defaults. Slot by
    /// slot, so a save that predates the field (empty array) or names a spell the registry no longer
    /// has (None / missing asset) leaves that slot on its default rather than emptying it. Deliberately
    /// not SpellLoadout.Sanitize: that enforces a hero's signature in slot 0, and a custom battle is a
    /// sandbox with no hero.
    /// </summary>
    private void ApplySavedCustomBattleSpells()
    {
        Spell[] saved = SaveDataHandler.LoadCustomBattleSaveData().playerCustomBattleSpells;
        if(saved == null) return;
        for (int i = 0; i < saved.Length && i < defaultSpells.Length; i++) {
            if(saved[i] == Spell.None) continue;
            SpellData spell = SpellRegistry.Get(saved[i]);
            if(spell != null) defaultSpells[i] = spell;
        }
    }
    #endregion
    #region Pre-Battle Browsing
    private SpellData[] GetEquippedSpells()
    {
        SpellData[] equipped = new SpellData[slotStates.Length];
        for (int i = 0; i < slotStates.Length; i++)
            equipped[i] = slotStates[i].SpellData;
        return equipped;
    }

    // Hover routing. Both the spell buttons and the browse menu report enter/exit here; the menu stays
    // open while either is hovered and closes shortly after both are left (browseCloseDelay).
    private void OnButtonBrowseHoverEnter(int slotIndex)
    {
        if(!browsingEnabled) return;
        hoveredButtonSlot = slotIndex;
        CancelPendingClose();
        OpenBrowse(slotIndex);
    }
    private void OnButtonBrowseHoverExit(int slotIndex)
    {
        if(hoveredButtonSlot == slotIndex) hoveredButtonSlot = -1;
        ScheduleBrowseClose();
    }
    private void OnBrowseMenuHoverEnter()
    {
        pointerOverBrowseMenu = true;
        CancelPendingClose();
    }
    private void OnBrowseMenuHoverExit()
    {
        pointerOverBrowseMenu = false;
        ScheduleBrowseClose();
    }

    private void OpenBrowse(int slotIndex)
    {
        if(!browsingEnabled || spellBrowseMenu == null) return;
        RectTransform anchor = spellCastButtons[slotIndex].transform as RectTransform;
        spellBrowseMenu.Open(slotIndex, GetEquippedSpells(), anchor);
        HighlightBrowseButton(slotIndex);
    }

    private void HighlightBrowseButton(int slotIndex)
    {
        if(browseHighlightSlot >= 0 && browseHighlightSlot != slotIndex)
            spellCastButtons[browseHighlightSlot].SetBrowseHighlighted(false);

        browseHighlightSlot = slotIndex;
        spellCastButtons[slotIndex].SetBrowseHighlighted(true);
    }
    private void ClearBrowseHighlight()
    {
        if(browseHighlightSlot < 0) return;
        spellCastButtons[browseHighlightSlot].SetBrowseHighlighted(false);
        browseHighlightSlot = -1;
    }

    private void ScheduleBrowseClose()
    {
        CancelPendingClose();
        if(!isActiveAndEnabled) return;
        browseCloseRoutine = StartCoroutine(BrowseCloseAfterDelay());
    }
    private void CancelPendingClose()
    {
        if(browseCloseRoutine != null)
        {
            StopCoroutine(browseCloseRoutine);
            browseCloseRoutine = null;
        }
    }
    private IEnumerator BrowseCloseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(browseCloseDelay);
        browseCloseRoutine = null;
        if(hoveredButtonSlot < 0 && !pointerOverBrowseMenu && spellBrowseMenu != null)
        {
            spellBrowseMenu.Close();
            ClearBrowseHighlight();
        }
    }

    /// <summary>
    /// Replaces the spell in <paramref name="slotIndex"/> with <paramref name="newSpell"/>. Pre-battle
    /// only. Clears any selection/cooldown on that slot, refreshes the button + quick-cast menu, and
    /// keeps defaultSpells in sync so summon preloading and a re-open of the browse list stay correct.
    /// </summary>
    public void SwapSpell(int slotIndex, SpellData newSpell)
    {
        if(!browsingEnabled) return;
        if(slotStates == null || slotIndex < 0 || slotIndex >= slotStates.Length) return;
        if(newSpell == null) return;

        if(selectedSpellIndex == slotIndex)
        {
            spellCastButtons[slotIndex].SetSelected(false);
            selectedSpellIndex = -1;
            if(BattleManager.Instance.CursorMode == CursorMode.CastSpell)
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
        }

        slotStates[slotIndex].SpellData = newSpell;
        slotStates[slotIndex].CooldownRemaining = 0f;
        slotStates[slotIndex].CooldownDuration = 0f;
        defaultSpells[slotIndex] = newSpell;

        // The bulk army preload has already run by deployment, so a summon spell swapped in now would
        // otherwise stall on an async load when first cast. Preload its unit here (idempotent).
        if(newSpell.SummonsSquad)
            BattleManager.Instance.UnitGPUAnimLoader.PreloadAdditionalUnit(newSpell.SummonedUnitName);

        WireSlotButton(slotIndex, newSpell);
        RefreshAffordability();
        spellQuickCastMenu.Load(GetEquippedSpells());

        // Re-open rather than rebuild: rows keep their fixed order, this just refreshes which ones read
        // as equipped and repoints the info panel at what now occupies the slot.
        OpenBrowse(slotIndex);
    }

    private void GamePhaseChanged(GamePhase gamePhase)
    {
        if(gamePhase != GamePhase.Battle) return;

        // Battle has begun - lock the loadout and close the picker.
        browsingEnabled = false;
        hoveredButtonSlot = -1;
        pointerOverBrowseMenu = false;
        CancelPendingClose();
        if(spellBrowseMenu != null) spellBrowseMenu.Close();
        ClearBrowseHighlight();

        // With the picker gone its info panel goes too, so the floating tooltips are the only
        // description left in battle. Hand them back.
        for (int i = 0; i < spellCastButtons.Length; i++) {
            if(spellCastButtons[i] != null) spellCastButtons[i].SetBrowseModeActive(false);
        }
    }
    #endregion

    #region Mana
    /// <summary>An empty slot is never "unaffordable" - it renders as empty and cannot be selected anyway.</summary>
    private bool CanAfford(SpellData spellData) => spellData == null || spellData.SpellManaCost <= manaRemaining;

    /// <summary>Pushes affordability to every button. Called on load and after every spend.</summary>
    private void RefreshAffordability()
    {
        if(slotStates == null) return;

        for (int i = 0; i < slotStates.Length; i++) {
            if(spellCastButtons[i] == null) continue;
            spellCastButtons[i].SetAffordable(CanAfford(slotStates[i].SpellData));
        }
    }

    private void SpendMana(int amount)
    {
        manaRemaining = Mathf.Max(0, manaRemaining - amount);
        RefreshAffordability();
        OnManaChanged?.Invoke(manaRemaining, manaMax);
    }

    private void RejectCast(int slotIndex, string reason)
    {
        Debug.Log($"SpellManager: cast rejected for slot {slotIndex} - {reason}");
        if(spellCastButtons[slotIndex] != null) spellCastButtons[slotIndex].FlashCooldownImage(Color.red);
    }
    #endregion

    private void SelectSpellHotkey1() => SelectSpellByHotkeyIndex(1);
    private void SelectSpellHotkey2() => SelectSpellByHotkeyIndex(2);
    private void SelectSpellHotkey3() => SelectSpellByHotkeyIndex(3);
    private void SelectSpellHotkey4() => SelectSpellByHotkeyIndex(4);

    public void SelectSpellByHotkeyIndex(int _hotkeyIndex)
    {
        if(slotStates == null) return;

        int slotIndex = _hotkeyIndex - 1;
        if(slotIndex < 0 || slotIndex >= slotStates.Length) return;

        SelectSpell(slotIndex);
    }

    public void SelectSpell(int slotIndex)
    {
        if(slotStates == null || slotIndex < 0 || slotIndex >= slotStates.Length) return;
        // An empty slot (hotbar longer than the loadout) must not enter cast mode - the cast
        // coroutine dereferences SpellData every frame.
        if(slotStates[slotIndex].SpellData == null) return;

#if !SPELLS
            Debug.Log($"SpellManager: Select failed for slot {slotIndex}, spell selection is disabled in this build (define SPELLS to enable)");
            return;
#endif

        // SpellSystem only runs once BattleHasStarted, so a Deployment cast used to spend mana and
        // cooldown immediately and then sit as a SpellEntity that detonated on Start Battle - a free
        // delayed bomb whose VFX had long since played out. Casting is a battle-phase action.
        if(BattleManager.Instance.GamePhase != GamePhase.Battle) {
            RejectCast(slotIndex, $"game phase is {BattleManager.Instance.GamePhase}, spells cast only during Battle");
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("SpellsLockedUntilBattle"));
            return;
        }

        if(slotStates[slotIndex].OnCooldown) {
            RejectCast(slotIndex, $"{slotStates[slotIndex].SpellData.name} on cooldown ({slotStates[slotIndex].CooldownRemaining:F1}s remaining)");
            return;
        }

        if(!CanAfford(slotStates[slotIndex].SpellData)) {
            RejectCast(slotIndex, $"{slotStates[slotIndex].SpellData.name} costs {slotStates[slotIndex].SpellData.SpellManaCost}, {manaRemaining} mana remaining");
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("notEnoughManaError"));
            return;
        }

        if(IsPlacementSpell(slotStates[slotIndex].SpellData)) {
            BeginPlacementFlow(slotIndex);
            return;
        }
        if(placementPhase != SpellPlacementPhase.None) CancelPlacement(false);

        if(selectedSpellIndex >= 0 && selectedSpellIndex != slotIndex)
            spellCastButtons[selectedSpellIndex].SetSelected(false);

        selectedSpellIndex = slotIndex;
        spellCastButtons[selectedSpellIndex].SetSelected(true);
        // Debug.Log($"SpellManager: Selected slot {slotIndex} ({slotStates[slotIndex].SpellData.name})");

        if(BattleManager.Instance.CursorMode != CursorMode.CastSpell){
            BattleManager.Instance.SetCursorMode(CursorMode.CastSpell);
        }
    }

    #region Placement spells (Starstep, Raise Dead)
    private void BeginPlacementFlow(int slotIndex)
    {
        if(placementPhase != SpellPlacementPhase.None) CancelPlacement(false);
        SpellData spell = slotStates[slotIndex].SpellData;
        UnitSelectionManager selection = BattleManager.Instance.UnitSelectionManager;
        UIManager ui = BattleManager.Instance.UIManager;

        // Deselect FIRST: the selection-changed handler may drop the cursor mode to Free, and leaving
        // CastSpell clears the armed button, which must therefore be set after this line.
        selection.DeselectSquadsBeforeDeletionOrSpawning();

        if(selectedSpellIndex >= 0 && selectedSpellIndex != slotIndex)
            spellCastButtons[selectedSpellIndex].SetSelected(false);
        selectedSpellIndex = slotIndex;
        spellCastButtons[slotIndex].SetSelected(true);
        placementSlot = slotIndex;

        if(spell.TeleportsSquad)
        {
            // The blink needs a squad first; the selection-changed handler moves us on to placing.
            placementPhase = SpellPlacementPhase.AwaitingSquad;
            ui.ShowCursorHint(LocalizationManager.Instance.GetText("SpellHintSelectSquad"));
        }
        else
        {
            // A summon knows its squad already: preview its footprint on the cursor straight away.
            UnitName unit = spell.SummonedUnitName;
            int count = TabletopTavernData.Instance.GetSquadStats(unit).baseUnitCount;
            float spread = TabletopTavernConstants.GetSpread(TabletopTavernData.Instance.GetUnitSizeFromUnitName(unit));
            // Face the enemy line, as a deployment spawn does, rather than whatever facing the last
            // selection left in the drawer.
            BattleManager.Instance.PositionDrawer.SetLookRotation(Quaternion.Euler(0f, -90f, 0f));
            BattleManager.Instance.PositionDrawer.PreviewSpawnFormation(MouseWorldPosition.Instance.GetWorldPosition(), count, spread);
            placementPhase = SpellPlacementPhase.Placing;
            ui.ShowCursorHint(LocalizationManager.Instance.GetText("SpellHintPlaceFormation"));
        }

        if(BattleManager.Instance.CursorMode != CursorMode.CastSpell)
            BattleManager.Instance.SetCursorMode(CursorMode.CastSpell);
    }
    // Subscribed to UnitSelectionManager.OnSelectedSquadsChanged. The only transition it owns is
    // "a player squad was picked while Starstep waits for one".
    private void OnSelectedSquadsChangedForPlacement(List<int> selectedSquadIds)
    {
        if(placementPhase != SpellPlacementPhase.AwaitingSquad) return;
        bool playerSquadSelected = false;
        foreach(int id in selectedSquadIds) if(id > 0) { playerSquadSelected = true; break; }
        if(!playerSquadSelected) return;
        // Next frame, not now: UnitSelectionManager rebuilds its per-squad unit counts in its own
        // handler for this same event, and subscription order between the two managers is not
        // something to depend on.
        StartCoroutine(BeginPlacingSelectedSquadNextFrame());
    }
    private IEnumerator BeginPlacingSelectedSquadNextFrame()
    {
        yield return null;
        if(placementPhase != SpellPlacementPhase.AwaitingSquad) yield break;
        UnitSelectionManager selection = BattleManager.Instance.UnitSelectionManager;
        if(selection.SelectedSquadEntityAndEntitiesCountDict.Count == 0) yield break;
        selection.RefreshSelectedUnitCounts();
        // The drawer lays a formation out with its parent yaw at facing - 90, which is exactly what
        // BattleInputManager.Angle holds after a selection (and what a drag rewrites). Selection also
        // stored the raw facing as lookRotation, and TurnOn would use THAT as the parent yaw - a
        // preview 90 degrees off the squad's real orientation. HandleRotateFormation makes the same
        // correction before its TurnOn for an ordinary move order.
        BattleManager.Instance.PositionDrawer.SetLookRotation(Quaternion.Euler(0f, BattleInputManager.Instance.Angle, 0f));
        BattleManager.Instance.PositionDrawer.TurnOn(selection.GetMousePositionOffsetByFormationCenter(), selection.SelectedSquadEntityAndEntitiesCountDict);
        placementPhase = SpellPlacementPhase.Placing;
        BattleManager.Instance.UIManager.ShowCursorHint(LocalizationManager.Instance.GetText("SpellHintPlaceFormation"));
    }
    /// <summary>The right-click that confirms a drawn formation. Called by BattleInputManager.HandleSpellPlacementCursorMode.</summary>
    public void ConfirmPlacement()
    {
        if(placementPhase != SpellPlacementPhase.Placing || placementSlot < 0) return;
        SpellData spell = slotStates[placementSlot].SpellData;
        PositionDrawer drawer = BattleManager.Instance.PositionDrawer;

        SpellPlacement placement = new SpellPlacement {
            Positions = drawer.UnitPrefabPointPositions(),
            // Same convention as SpawnManager.SpawnFormation and TeleportUnits: the drawn parent's
            // yaw plus 90 is the squad's facing.
            Rotation = drawer.PositionsParent.rotation * Quaternion.Euler(0f, 90f, 0f),
            WidthAndDepth = spell.SummonsSquad ? drawer.Formation.GetWidthAndDepth(0) : default,
            Center = drawer.PositionsParent.position
        };

        int slot = placementSlot;
        bool teleport = spell.TeleportsSquad;
        if(teleport)
        {
            // Apply the blink now, from the points on screen. TeleportUnits reads the drawer, turns it
            // off, and carries the combat clear-out. Its facing comes from BattleInputManager.Angle,
            // which selection already set to the squad's current facing and a right-drag updates -
            // the same bookkeeping a deployment reposition relies on. Do not derive it from the
            // drawer's parent yaw: that yaw follows two different conventions depending on whether
            // the player dragged (2026-09-13: a click-to-place landed the squad 90 degrees off).
            BattleManager.Instance.UnitPositioningManager.TeleportUnits(true);
        }
        else
        {
            drawer.TurnOff();
        }

        CancelPlacement(false);
        CastPlacedSpell(slot, placement.Center, teleport ? null : placement, teleport);
        // A blinked squad stays selected, exactly as it would after a move order.
        bool squadsStillSelected = BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Count > 0;
        BattleManager.Instance.SetCursorMode(squadsStillSelected ? CursorMode.UnitsSelected : CursorMode.Free);
    }
    /// <summary>
    /// Leaves the placement flow. Nothing has been spent, so there is nothing to refund. Pass
    /// resetCursor=false when the caller is about to set the cursor mode itself.
    /// </summary>
    public void CancelPlacement(bool resetCursor = true)
    {
        if(placementPhase == SpellPlacementPhase.None) return;
        placementPhase = SpellPlacementPhase.None;
        placementSlot = -1;
        BattleManager.Instance.PositionDrawer.TurnOff();
        BattleManager.Instance.UIManager.HideCursorHint();
        if(resetCursor && BattleManager.Instance.CursorMode == CursorMode.CastSpell)
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
    }
    // The cast itself for a placed spell: the same gates and bookkeeping as CastSpell, minus the
    // mouse-release wait (the confirming right-click has already been released).
    private void CastPlacedSpell(int slotIndex, Vector3 origin, SpellPlacement placement, bool effectHandledByCaster)
    {
        SpellSlotState slot = slotStates[slotIndex];
        if(slot.SpellData == null || slot.OnCooldown || !CanAfford(slot.SpellData)
           || BattleManager.Instance.GamePhase != GamePhase.Battle) {
            RejectCast(slotIndex, "failed re-check at placement time");
            return;
        }
        if(slot.SpellData.SpellPrefab == null) {
            Debug.LogError($"SpellManager: '{slot.SpellData.name}' has no SpellPrefab assigned and cannot be cast.", slot.SpellData);
            return;
        }

        IAudioRequester.Instance.PlaySFX("cast-spell");
        ActiveSpell spellInstance = Instantiate(slot.SpellData.SpellPrefab, origin, Quaternion.identity);
        spellInstance.Load(slot.SpellData, origin, Entity.Null, Team.Player, 0, placement, effectHandledByCaster);

        SpendMana(slot.SpellData.SpellManaCost);
        Debug.Log($"SpellManager: cast {slot.SpellData.name} (placed) for {slot.SpellData.SpellManaCost} mana, {manaRemaining}/{manaMax} remaining");

        slot.CooldownDuration = slot.SpellData.SpellCooldown;
        slot.CooldownRemaining = slot.CooldownDuration;
        spellCastButtons[slotIndex].RenderCooldown(1f, true);

        spellsCast++;
        slotsCastMask |= 1 << slotIndex;
        CheckFullArsenal();
    }
    #endregion
    /// <summary>
    /// "Full Arsenal" - every slot on a fully-equipped hotbar cast at least once this battle.
    /// A partly-filled bar can never qualify, so the achievement always means all four spells.
    /// </summary>
    private void CheckFullArsenal()
    {
        for (int i = 0; i < slotStates.Length; i++)
        {
            if(slotStates[i].SpellData == null) return;
            if((slotsCastMask & (1 << i)) == 0) return;
        }
        SteamAchievements.Unlock(AchievementId.FullArsenal);
    }
    public void DeselectSpell()
    {
        if(selectedSpellIndex < 0) return;

        spellCastButtons[selectedSpellIndex].SetSelected(false);
        selectedSpellIndex = -1;
    }
    public void AttemptCastSpell()
    {
        if(!validSpellCastPoint){
            Debug.Log($"SpellManager: Cast failed, invalid cast point (selected slot {selectedSpellIndex}, cursor {spellCursorOrigin})");
            NotificationManager.Instance.ErrorNotification("Invalid Spell Cast Point");
            return;
        }

        IAudioRequester.Instance.PlaySFX("cast-spell");
        CastSpell();
    }
    public IEnumerator GetMouseCursorPosition()
    {
        while(BattleManager.Instance.CursorMode == CursorMode.CastSpell)
        {
            // A placement spell owns the mouse while its phase is active (BattleInputManager.
            // HandleSpellPlacementCursorMode); the ordinary click-to-cast and right-click-cancel below
            // would fight it - right-click is "place" there.
            if(placementPhase != SpellPlacementPhase.None) { yield return null; continue; }

            if(Input.GetMouseButtonDown(1)){
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
                yield break;
            }

            if(Input.GetMouseButtonDown(0)){
                AttemptCastSpell();
            }

            if(selectedSpellIndex < 0) { yield return null; continue; }
            SpellData selectedSpellData = slotStates[selectedSpellIndex].SpellData;

            Vector3 castPoint = MouseWorldPosition.Instance.GetWorldPosition() + (Vector3.up*10f);
            targetedSquadSelfEntity = Entity.Null;

            if(selectedSpellData.SpellTargetingType == SpellTargetingType.Squad)
            {
                int hoveredSquadIndex = BattleManager.Instance.UIManager.HoveredSquadId;
                bool validTarget = false;

                if(hoveredSquadIndex != 0) {
                    //positive squadId = player squad, negative = enemy squad (see UnitSelectionManager.IsHoveringEnemySquad)
                    bool hoveredIsPlayerSquad = hoveredSquadIndex > 0;
                    validTarget = (selectedSpellData.TargetTeam == Team.Player && hoveredIsPlayerSquad)
                               || (selectedSpellData.TargetTeam == Team.Enemy && !hoveredIsPlayerSquad)
                               || selectedSpellData.TargetTeam == Team.Neutral; //Neutral spells can target either team

                    if(validTarget) {
                        SquadEntity hoveredSquad = BattleManager.Instance.SquadManager.GetSquad(hoveredSquadIndex);
                        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                        if(hoveredSquad.SelfEntity != Entity.Null &&
                            entityManager.Exists(hoveredSquad.SelfEntity) &&
                            entityManager.HasComponent<SquadMovementComponent>(hoveredSquad.SelfEntity)) {
                            castPoint = entityManager.GetComponentData<SquadMovementComponent>(hoveredSquad.SelfEntity).SquadCenter;
                            targetedSquadSelfEntity = hoveredSquad.SelfEntity;
                        } else {
                            validTarget = false;
                        }
                    }
                }

                validSpellCastPoint = validTarget;
                spellCursorOrigin = validTarget ? castPoint : MouseWorldPosition.Instance.GetWorldPosition();
            } else if (selectedSpellData.SpellTargetingType == SpellTargetingType.World) {

                if(Physics.Raycast(castPoint, Vector3.down, 20, validSpellCastLayerMask)) {
                    validSpellCastPoint = true;
                } else {
                    validSpellCastPoint = false;
                }
                spellCursorOrigin = MouseWorldPosition.Instance.GetWorldPosition();
            }

            yield return null;
        }
    }
    public async void CastSpell()
    {
        if(selectedSpellIndex < 0) {
            Debug.Log("SpellManager: Cast failed, no spell selected");
            return;
        }

        SpellSlotState slot = slotStates[selectedSpellIndex];

        // Re-checked here rather than trusting SelectSpell. CastSpell and AttemptCastSpell are both
        // public and this one is async void, so SelectSpell's gates are not on the only path in - and
        // mana is the first spell resource that can be driven negative by a second entry point.
        // The cooldown re-check rides along; it was previously only tested at select time.
        if(slot.SpellData == null || slot.OnCooldown || !CanAfford(slot.SpellData)
           || BattleManager.Instance.GamePhase != GamePhase.Battle) {
            RejectCast(selectedSpellIndex, "failed re-check at cast time");
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
            return;
        }

        // Null prefab would throw here. Only the browse menu guarded against it before.
        if(slot.SpellData.SpellPrefab == null) {
            Debug.LogError($"SpellManager: '{slot.SpellData.name}' has no SpellPrefab assigned and cannot be cast.", slot.SpellData);
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
            return;
        }

        ActiveSpell spellInstance = Instantiate(slot.SpellData.SpellPrefab, spellCursorOrigin, Quaternion.identity);
        spellInstance.Load(slot.SpellData, spellCursorOrigin, targetedSquadSelfEntity);
        // Debug.Log($"SpellManager: Cast succeeded, {slot.SpellData.name} at {spellCursorOrigin} (targeting={slot.SpellData.SpellTargetingType}, targetSquad={targetedSquadSelfEntity})");

        SpendMana(slot.SpellData.SpellManaCost);
        Debug.Log($"SpellManager: cast {slot.SpellData.name} for {slot.SpellData.SpellManaCost} mana, {manaRemaining}/{manaMax} remaining");

        slot.CooldownDuration = slot.SpellData.SpellCooldown;
        slot.CooldownRemaining = slot.CooldownDuration;
        spellCastButtons[selectedSpellIndex].RenderCooldown(1f, true);

        spellsCast++;
        slotsCastMask |= 1 << selectedSpellIndex;
        CheckFullArsenal();

        mouseReleased = false;
        while(!mouseReleased){
            if(Input.GetMouseButtonUp(0)){
                mouseReleased = true;
                BattleManager.Instance.SetCursorMode(CursorMode.Free);
                // Debug.Log($"SpellManager: Mouse released, exiting cast mode (spells cast this session: {spellsCast})");
            }
            await Task.Yield();
        }
    }
    /// <summary>
    /// Casts a spell on behalf of a mage unit rather than the player's hotbar. Drained out of the
    /// MageCastRequestBufferElement stream by EntityWatcher, because an ISystem cannot instantiate a
    /// MonoBehaviour prefab.
    ///
    /// Deliberately shares nothing with the hotbar path but the Instantiate + Load: a unit cast has
    /// no slot, spends no mana, and drives no cooldown UI. Its cadence is MageCast.Timer and its
    /// budget is the squad's charges. Kept on SpellManager purely so every ActiveSpell in the game
    /// is still created in one place.
    /// </summary>
    public void CastUnitSpell(SpellData spellData, Vector3 position, Team sourceTeam, int sourceSquadId, Entity targetSquadEntity)
    {
        if(spellData == null) {
            Debug.LogError($"SpellManager: squad {sourceSquadId} requested a cast with no SpellData assigned.");
            return;
        }
        // Same guard the hotbar needs: 'Iron Legion Spell 1 - IL.asset' is an unauthored stub whose
        // SpellPrefab is null, and Instantiate would throw rather than log anything useful.
        if(spellData.SpellPrefab == null) {
            Debug.LogError($"SpellManager: '{spellData.name}' has no SpellPrefab assigned and cannot be cast by squad {sourceSquadId}.", spellData);
            return;
        }

        ActiveSpell spellInstance = Instantiate(spellData.SpellPrefab, position, Quaternion.identity);
        spellInstance.Load(spellData, position, targetSquadEntity, sourceTeam, sourceSquadId);
    }
    public void CursorModeChanged(CursorMode _cursorMode)
    {
        if(_cursorMode == CursorMode.CastSpell) {
            StartCoroutine(GetMouseCursorPosition());
        } else {
            // Anything that pulls the cursor out of spell mode (battle end, another manager taking
            // over) also ends a placement in progress. Nothing was spent yet.
            if(placementPhase != SpellPlacementPhase.None) CancelPlacement(false);
            DeselectSpell();
        }
    }
    private void OnDestroy()
    {
#if SPELLS
        if(BattleManager.HasInstance)
        {
            BattleManager.Instance.OnCursorModeChanged -= CursorModeChanged;
            BattleManager.Instance.OnGamePhaseChanged -= GamePhaseChanged;
        }
        if(InputHandler.HasInstance)
        {
            InputHandler.Instance.OnSelectSpell1 -= SelectSpellHotkey1;
            InputHandler.Instance.OnSelectSpell2 -= SelectSpellHotkey2;
            InputHandler.Instance.OnSelectSpell3 -= SelectSpellHotkey3;
            InputHandler.Instance.OnSelectSpell4 -= SelectSpellHotkey4;
        }
        if(UnitSelectionManager.Instance != null)
            UnitSelectionManager.Instance.OnSelectedSquadsChanged -= OnSelectedSquadsChangedForPlacement;
#endif
    }
}
}
