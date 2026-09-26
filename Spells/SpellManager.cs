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
using Unity.Mathematics;
using UnityEngine;

namespace TJ.Spells
{
public class SpellManager : MonoBehaviour
{
    [SerializeField] private LayerMask validSpellCastLayerMask;
    //temp, only for testing
    [SerializeField] private SpellData[] defaultSpells;
    [SerializeField] private SFXCue castSound;
    [SerializeField] private SFXCue selectSound;
    [SerializeField] private SFXCue targetHoverSound;
    [SerializeField] private SFXCue targetHoverLoop;
    [Header("Targeting cursor")]
    [SerializeField] private Texture2D validTargetCursor;
    [SerializeField] private Texture2D invalidTargetCursor;
    // Which cursor is up, so SetCursor runs on a change and not every frame. Null = the default cursor.
    private Texture2D activeCursor;
    private bool targetHintValid;
    private bool targetHintOverSquad;
    private SpellData targetHintSpell;
    // The one ActiveSpell prefab every cast starts from (AOE Spell); per-spell art is SpellData.SpellVisualPrefab.
    [SerializeField] private ActiveSpell aoeSpellPrefab;
    [SerializeField] private SpellCastButton[] spellCastButtons;
    [Header("Caster rail")]
    // Optional until the scene is wired: every call is null-guarded so the hotbar works without it.
    [SerializeField] private MageCastRail mageCastRail;

    [Header("Pre-Battle Browsing (custom battle only)")]
    // The pool the player can swap from is every registered spell (SpellRegistry.All). It used to be
    // a hand-curated inspector list, which silently fell behind when spells were added.
    [SerializeField] private SpellBrowseMenu spellBrowseMenu;
    // Seconds the browse menu lingers after the pointer leaves both the spell buttons and the menu,
    // so crossing the gap between them does not flicker it closed.
    [SerializeField] private float browseCloseDelay = 0.12f;
    [Header("Spell test mode")]
    // The custom battle panel. The cast menu takes its top-right corner once the battle starts and the
    // deployment canvas (and that panel with it) hides.
    [SerializeField] private RectTransform testCastMenuAnchor;

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

    #region Mage spells
    // A mage squad's spell armed through the caster rail or the Y menu. It shares the cursor,
    // targeting feedback and click with the hotbar, but spends a charge instead of mana and casts
    // through MageManualCastOrder / MageCastSystem rather than spawning the ActiveSpell here.
    // 0 = no mage spell armed; then selectedSpellIndex says whether a hotbar spell is.
    private int armedMageSquadId;
    private SpellData armedMageSpell;
    public bool MageSpellArmed => armedMageSquadId != 0;
    public int ArmedMageSquadId => armedMageSquadId;
    // Read each frame while a mage spell is armed, for the leash line and the approach hint.
    public Vector3 ArmedMageCenter { get; private set; }
    public float ArmedMageRange { get; private set; }
    public bool ArmedMageOutOfRange { get; private set; }
    // How far inside casting range an out-of-range ground cast walks the mage, so it stops in range
    // rather than exactly on the edge where the squad's own footprint can leave it short.
    private const float APPROACH_RANGE_FRACTION = 0.9f;
    #endregion

    // Friendly-target spells draw their aim in the friendly colour, never attack red.
    public bool ArmedSpellTargetsFriends => ArmedSpell != null && ArmedSpell.TargetTeam == Team.Player;

    // Whatever is armed right now, from either source. Null when nothing is.
    private SpellData ArmedSpell
    {
        get
        {
            if(armedMageSquadId != 0) return armedMageSpell;
            if(selectedSpellIndex >= 0 && slotStates != null) return slotStates[selectedSpellIndex].SpellData;
            return null;
        }
    }

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
    // Mana the hovered (else armed) hotbar spell would spend, for the mana bar's preview. 0 = none.
    private int manaPreview;
    public int ManaPreview => manaPreview;
    public event Action<int> OnManaPreviewChanged;
    private int hoveredHotbarSlot = -1;

    // Pre-battle browse state.
    private bool browsingEnabled;
    private int hoveredButtonSlot = -1;
    private bool pointerOverBrowseMenu;
    private Coroutine browseCloseRoutine;
    // Slot whose cast button is currently highlighted green because the browse menu is open for it (-1 = none).
    private int browseHighlightSlot = -1;

    private bool validSpellCastPoint;
    // The squad the hover audio last reacted to, so the one-shot fires once per new target.
    private Entity hoverAudioTarget = Entity.Null;
    private Vector3 spellCursorOrigin;
    public Vector3 SpellCursorOrigin => spellCursorOrigin;
    public bool ValidSpellCastPoint => validSpellCastPoint;
    public float SelectedSpellRadius => ArmedSpell != null ? ArmedSpell.SpellRadius : 0f;
    // The targeting star only marks single-target spells; an area spell's cursor is the ring alone.
    public bool SelectedSpellShowsStar => ArmedSpell != null && ArmedSpell.SpellType == SpellType.SingleTarget;
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
        InputHandler.Instance.OnSpellMenuDigit -= OnSpellMenuDigit;
        InputHandler.Instance.OnSpellMenuDigit += OnSpellMenuDigit;
        InputHandler.Instance.OnSpellMenu -= OnSpellMenuOpened;
        InputHandler.Instance.OnSpellMenu += OnSpellMenuOpened;
        InputHandler.Instance.OnSpellMenuCanceled -= OnSpellMenuClosed;
        InputHandler.Instance.OnSpellMenuCanceled += OnSpellMenuClosed;
        if(UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.OnSelectedSquadsChanged -= OnSelectedSquadsChangedForPlacement;
            UnitSelectionManager.Instance.OnSelectedSquadsChanged += OnSelectedSquadsChangedForPlacement;
            UnitSelectionManager.Instance.OnSelectedSquadsChanged -= OnSelectedSquadsChangedForRail;
            UnitSelectionManager.Instance.OnSelectedSquadsChanged += OnSelectedSquadsChangedForRail;
        }
#endif
    }
    private void Update()
    {
        if(slotStates == null) return;

        // Polled rather than pushed: the armed slot is cleared from many paths, and this reads them all.
        RefreshManaPreview();
        RefreshTestMenuArmed();

        for (int i = 0; i < slotStates.Length; i++) {
            SpellSlotState slot = slotStates[i];
            if(slot.CooldownRemaining <= 0f) continue;

            slot.CooldownRemaining -= Time.deltaTime;
            bool justFinished = slot.CooldownRemaining <= 0f;
            if(justFinished) slot.CooldownRemaining = 0f;

            spellCastButtons[i].RenderCooldown(slot.CooldownRemaining / slot.CooldownDuration, !justFinished, slot.CooldownRemaining);
            if(justFinished) spellCastButtons[i].FlashCooldownImage(Color.white);
        }
    }
    public void LoadSpellManager(SpellData[] _spells = null)
    {
        bool isCustomBattle = BattleManager.Instance.BattleSaveManager.IsCustomBattle;
        // Swapping is a pre-battle, custom-battle convenience only. It stays off for campaign battles,
        // and in test mode every spell is already on screen so there is nothing to swap for.
        browsingEnabled = isCustomBattle && !SpellTestMode.Active;

        if(_spells != null) {
            defaultSpells = _spells;
        } else if(!isCustomBattle) {
            // Campaign battle: the loadout was chosen at run setup and persisted on the campaign
            // save. Custom battles keep the serialized inspector list, which the browse menu edits.
            SpellData[] campaignSpells = SaveDataHandler.GetCampaignSpells();
            if(campaignSpells != null && campaignSpells.Length > 0) defaultSpells = campaignSpells;
        }

        EnsureLoadoutCoversHotbar();

        if(_spells == null && isCustomBattle) ApplySavedCustomBattleSpells();

        if(hotbarSlotCount < 0) hotbarSlotCount = spellCastButtons.Length;
        if(SpellTestMode.Active) BuildTestGrid();
        else RemoveTestGrid();
        PreloadSummonedUnits();

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
            spellCastButtons[i].SetLocked(!isCustomBattle && SpellLoadout.IsSlotLocked(slotIndex));
            // The picker's info panel describes a slot the moment it is hovered, so the button's own
            // floating tooltip stands down for as long as the picker is available.
            spellCastButtons[i].SetBrowseModeActive(browsingEnabled);
        }
        selectedSpellIndex = -1;
        slotsCastMask = 0;
        // A custom battle has no run to report to, and a stale tally must not leak into the next campaign battle.
        SaveDataHandler.SpellsCastThisBattle.Clear();

        // After the wiring pass - LoadSpellUI resets each button's affordability to true.
        RefreshAffordability();
        OnManaChanged?.Invoke(manaRemaining, manaMax);

        if(mageCastRail != null)
        {
            mageCastRail.Initialize(hotbarSlotCount, ArmMageSpell, OnMageTileHover, () => armedMageSquadId, RefreshMageRail);
            RefreshMageRail();
        }

        if(browsingEnabled && spellBrowseMenu != null)
            spellBrowseMenu.Initialize(new List<SpellData>(SpellRegistry.All).ToArray(), SwapSpell, OnBrowseMenuHoverEnter, OnBrowseMenuHoverExit);
        if(SpellTestMode.Active) OpenTestCastMenu();
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
    #region Spell test mode (Editor only)
    // The scene hotbar's button count, so the custom-battle save and Full Arsenal never see the grid.
    private int hotbarSlotCount = -1;
    private RectTransform testGrid;

    /// <summary>
    /// Every registered spell not already on the hotbar gets a cloned hotbar button in a hidden grid.
    /// The clones are appended to spellCastButtons and defaultSpells so the rest of this class treats
    /// them as ordinary slots, which is what the test cast menu arms; hotkeys still cover only the
    /// real hotbar. The grid dies with the scene, like the hotbar it copies.
    /// </summary>
    private void BuildTestGrid()
    {
        Debug.LogWarning($"[SpellTestMode] active: mana {SpellTestMode.ManaPool}, cooldowns {SpellTestMode.CooldownSeconds}s, every registered spell on the grid. Tabletop Tavern > Spell Test Mode to turn off.");

        // A second load starts from the real hotbar so clones never stack.
        if(testGrid != null) Destroy(testGrid.gameObject);
        Array.Resize(ref spellCastButtons, hotbarSlotCount);
        Array.Resize(ref defaultSpells, hotbarSlotCount);

        List<SpellData> extras = new();
        foreach(SpellData spell in SpellRegistry.All)
            if(Array.IndexOf(defaultSpells, spell) < 0) extras.Add(spell);
        if(extras.Count == 0) return;

        SpellCastButton template = spellCastButtons[0];
        if(template == null) {
            Debug.LogError("SpellManager: spellCastButtons[0] is not assigned, so there is no button to clone for the test grid.");
            return;
        }
        testGrid = SpellTestMode.CreateGrid(template.transform as RectTransform);
        // The grimoire is the test UI now; the clones stay only as the slots it casts through. Hidden by
        // alpha, not SetActive, so each clone still runs Awake.
        CanvasGroup gridGroup = testGrid.gameObject.AddComponent<CanvasGroup>();
        gridGroup.alpha = 0f;
        gridGroup.blocksRaycasts = false;
        gridGroup.interactable = false;

        int first = hotbarSlotCount;
        Array.Resize(ref spellCastButtons, first + extras.Count);
        Array.Resize(ref defaultSpells, first + extras.Count);
        for (int i = 0; i < extras.Count; i++) {
            SpellCastButton clone = Instantiate(template, testGrid);
            clone.name = $"Test Grid {extras[i].Spell}";
            spellCastButtons[first + i] = clone;
            defaultSpells[first + i] = extras[i];
        }
    }

    // Turning test mode off mid-deployment drops the clones so the hotbar is back to its real slots.
    private void RemoveTestGrid()
    {
        if(testGrid == null) return;
        Destroy(testGrid.gameObject);
        testGrid = null;
        Array.Resize(ref spellCastButtons, hotbarSlotCount);
        Array.Resize(ref defaultSpells, hotbarSlotCount);
    }

    /// <summary>
    /// The custom battle panel's toggle. Applies at once during Deployment by reloading the spell bar on
    /// the current hotbar; later it only stores the choice for the next battle.
    /// </summary>
    public void SetTestMode(bool on)
    {
        SpellTestMode.Enabled = on;
        if(BattleManager.Instance.GamePhase != GamePhase.Deployment || hotbarSlotCount < 0) return;

        SpellData[] hotbar = new SpellData[hotbarSlotCount];
        Array.Copy(defaultSpells, hotbar, hotbarSlotCount);
        LoadSpellManager(hotbar);
    }

    private static float CooldownFor(SpellData spellData)
        => SpellTestMode.Active ? SpellTestMode.CooldownSeconds : spellData.SpellCooldown;

    // The spell the test menu currently shows as armed, so it is repainted only on a change.
    private SpellData testMenuArmedSpell;

    /// <summary>
    /// Test mode casts from the grimoire. Every spell already owns a slot (hotbar or hidden grid
    /// clone), so a click just arms that slot through SelectSpell. The menu shows only in Battle:
    /// nothing can be cast in Deployment, and there it would sit under the custom battle panel.
    /// </summary>
    private void OpenTestCastMenu()
    {
        if(spellBrowseMenu == null) return;
        spellBrowseMenu.Initialize(new List<SpellData>(SpellRegistry.All).ToArray(),
            (_, spell) => SelectSpell(Array.FindIndex(slotStates, slot => slot.SpellData == spell)), null, null);
        testMenuArmedSpell = null;
        if(BattleManager.Instance.GamePhase == GamePhase.Battle) spellBrowseMenu.OpenForCasting(testCastMenuAnchor);
    }

    // Polled like the mana preview: the armed slot is cleared from many paths.
    private void RefreshTestMenuArmed()
    {
        if(!SpellTestMode.Active || spellBrowseMenu == null) return;
        SpellData armed = selectedSpellIndex >= 0 ? slotStates[selectedSpellIndex].SpellData : null;
        if(armed == testMenuArmedSpell) return;
        testMenuArmedSpell = armed;
        spellBrowseMenu.SetArmedSpell(armed);
    }
    #endregion
    private void WireSlotButton(int slotIndex, SpellData spellData)
    {
        Action browseEnter = browsingEnabled ? () => OnButtonBrowseHoverEnter(slotIndex) : null;
        Action browseExit = browsingEnabled ? () => OnButtonBrowseHoverExit(slotIndex) : null;
        Action<bool> hoverChanged = hovered => {
            if(hovered) hoveredHotbarSlot = slotIndex;
            else if(hoveredHotbarSlot == slotIndex) hoveredHotbarSlot = -1;
        };
        spellCastButtons[slotIndex].LoadSpellUI(spellData, () => SelectSpell(slotIndex), slotIndex + 1, browseEnter, browseExit,
            default, hoverChanged);
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
        // The test grid's clones sit past hotbarSlotCount and must not reach the save.
        Spell[] equipped = new Spell[hotbarSlotCount];
        for (int i = 0; i < hotbarSlotCount; i++)
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
        if(SpellTestMode.Active && spellBrowseMenu != null) {
            spellBrowseMenu.OpenForCasting(testCastMenuAnchor);
            testMenuArmedSpell = null;
        }

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
            SpellData spellData = slotStates[i].SpellData;
            spellCastButtons[i].SetManaShort(spellData == null ? 0 : Mathf.Max(0, spellData.SpellManaCost - manaRemaining));
        }
    }

    // Only a spell the pool can pay for previews; an unaffordable one already reads red on its cost gem.
    private int PreviewCostOf(int slotIndex)
    {
        if(slotIndex < 0 || slotIndex >= slotStates.Length) return 0;
        SpellData spell = slotStates[slotIndex].SpellData;
        return spell != null && CanAfford(spell) ? spell.SpellManaCost : 0;
    }

    private void RefreshManaPreview()
    {
        int preview = PreviewCostOf(hoveredHotbarSlot);
        if(preview == 0) preview = PreviewCostOf(selectedSpellIndex);
        if(preview == manaPreview) return;

        manaPreview = preview;
        OnManaPreviewChanged?.Invoke(manaPreview);
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

    /// <summary>
    /// Y menu: digits 1..hotbarSlotCount are the hotbar, the rest are the selected mage squads in
    /// selection order (5 is the mage nearest the hotbar). A digit past the last mage does nothing.
    /// </summary>
    private void OnSpellMenuDigit(int digit)
    {
        if(slotStates == null || hotbarSlotCount < 0) return;

        if(digit <= hotbarSlotCount) {
            SelectSpell(digit - 1);
            return;
        }
        int mageIndex = digit - hotbarSlotCount - 1;
        List<int> mages = GetSelectedMageSquadIds();
        if(mageIndex < mages.Count) ArmMageSpell(mages[mageIndex]);
    }
    /// <summary>
    /// Selected player squads that still carry MageSquad (a spent mage is a melee body), in card
    /// order left to right, which is the order the tiles sit in and the digits count in.
    /// </summary>
    public List<int> GetSelectedMageSquadIds()
    {
        List<int> mages = new();
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        foreach(int squadId in BattleManager.Instance.UnitSelectionManager.SelectedSquadIds) {
            if(squadId <= 0) continue;
            SquadEntity squad = BattleManager.Instance.SquadManager.GetSquadEntityFromId(squadId, true);
            if(squad.SelfEntity == Entity.Null || !entityManager.HasComponent<MageSquad>(squad.SelfEntity)) continue;
            mages.Add(squadId);
        }
        UIManager ui = BattleManager.Instance.UIManager;
        mages.Sort((a, b) => ui.GetSquadCardNumber(a).CompareTo(ui.GetSquadCardNumber(b)));
        return mages;
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

        if(!SummonPrefabsReady(slotStates[slotIndex].SpellData)) {
            RejectCast(slotIndex, $"{slotStates[slotIndex].SpellData.name} summon prefabs for {slotStates[slotIndex].SpellData.SummonedUnitName} are not loaded yet");
            return;
        }

        IAudioRequester.Instance.Play(selectSound, ignoreDucking: true);

        if(IsPlacementSpell(slotStates[slotIndex].SpellData)) {
            BeginPlacementFlow(slotIndex);
            return;
        }
        if(placementPhase != SpellPlacementPhase.None) CancelPlacement(false);

        if(selectedSpellIndex >= 0 && selectedSpellIndex != slotIndex)
            spellCastButtons[selectedSpellIndex].SetSelected(false);

        // Only one thing is ever armed.
        armedMageSquadId = 0;
        armedMageSpell = null;
        selectedSpellIndex = slotIndex;
        spellCastButtons[selectedSpellIndex].SetSelected(true);
        // Debug.Log($"SpellManager: Selected slot {slotIndex} ({slotStates[slotIndex].SpellData.name})");

        if(BattleManager.Instance.CursorMode != CursorMode.CastSpell){
            BattleManager.Instance.SetCursorMode(CursorMode.CastSpell);
        }
        // After the mode switch: any hop through Free on the way here un-ducks in CursorModeChanged.
        IAudioRequester.Instance.SetDucked(true);
    }

    #region Placement spells (Starstep, Raise Dead)
    // Custom battles in a player build never run the army preload, so every equipped summon requests its own unit.
    private void PreloadSummonedUnits()
    {
        foreach(SpellData spell in defaultSpells)
            if(spell != null && spell.SummonsSquad)
                BattleManager.Instance.UnitGPUAnimLoader.PreloadAdditionalUnit(spell.SummonedUnitName);
    }
    // A summon cast before its unit prefabs have loaded would spend mana and spawn nothing.
    private bool SummonPrefabsReady(SpellData spell)
    {
        if(!spell.SummonsSquad) return true;
        if(UnitGPUAnimPrefabs.Find(World.DefaultGameObjectInjectionWorld.EntityManager, spell.SummonedUnitName) != null) return true;
        BattleManager.Instance.UnitGPUAnimLoader.PreloadAdditionalUnit(spell.SummonedUnitName);
        return false;
    }
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
            ui.ShowSpellTargetHint(validTargetCursor, LocalizationManager.Instance.GetText("SpellHintSelectSquad"));
        }
        else
        {
            // A summon knows its squad already: preview its footprint on the cursor straight away.
            UnitName unit = spell.SummonedUnitName;
            int count = HeroBonusManager.GetPlayerBaseUnitCount(unit, HeroBonusManager.Instance.ActiveHeroID);
            float spread = TabletopTavernConstants.GetSpread(TabletopTavernData.Instance.GetUnitSizeFromUnitName(unit));
            // Face the enemy line, as a deployment spawn does, rather than whatever facing the last
            // selection left in the drawer.
            BattleManager.Instance.PositionDrawer.SetLookRotation(Quaternion.Euler(0f, -90f, 0f));
            BattleManager.Instance.PositionDrawer.PreviewSpawnFormation(MouseWorldPosition.Instance.GetWorldPosition(), count, spread);
            placementPhase = SpellPlacementPhase.Placing;
            ui.ShowSpellTargetHint(validTargetCursor, LocalizationManager.Instance.GetText("SpellHintPlaceFormation"));
        }

        if(BattleManager.Instance.CursorMode != CursorMode.CastSpell)
            BattleManager.Instance.SetCursorMode(CursorMode.CastSpell);
        IAudioRequester.Instance.SetDucked(true);
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
        BattleManager.Instance.UIManager.ShowSpellTargetHint(validTargetCursor, LocalizationManager.Instance.GetText("SpellHintPlaceFormation"));
    }
    /// <summary>The right-click that confirms a drawn formation. Called by BattleInputManager.HandleSpellPlacementCursorMode.</summary>
    public void ConfirmPlacement()
    {
        if(placementPhase != SpellPlacementPhase.Placing || placementSlot < 0) return;
        SpellData spell = slotStates[placementSlot].SpellData;
        PositionDrawer drawer = BattleManager.Instance.PositionDrawer;

        List<float3> points = drawer.UnitPrefabPointPositions();
        SpellPlacement placement = new SpellPlacement {
            Positions = points,
            // Same convention as SpawnManager.SpawnFormation and TeleportUnits: the drawn parent's
            // yaw plus 90 is the squad's facing.
            Rotation = drawer.PositionsParent.rotation * Quaternion.Euler(0f, 90f, 0f),
            WidthAndDepth = spell.SummonsSquad ? drawer.Formation.GetWidthAndDepth(0) : default,
            // The drawer's parent is the mouse anchor at the formation's edge; the visuals belong on the squad's centre.
            Center = FormationCenter(points, drawer.PositionsParent.position)
        };

        int slot = placementSlot;
        bool teleport = spell.TeleportsSquad;
        Vector3 departure = Vector3.zero;
        bool hasDeparture = teleport && TryGetFirstSelectedSquadCenter(out departure);
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
        // The blink reads at both ends: a second, visual-only instance marks where the squad left.
        if(hasDeparture) {
            ActiveSpell departureVisual = SpawnActiveSpell(departure);
            if(departureVisual != null) departureVisual.Load(spell, departure, Entity.Null, Team.Player, 0, null, true);
        }
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
        BattleManager.Instance.UIManager.HideSpellTargetHint();
        if(resetCursor && BattleManager.Instance.CursorMode == CursorMode.CastSpell)
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
    }
    // The cast itself for a placed spell: the same gates and bookkeeping as CastSpell, minus the
    // mouse-release wait (the confirming right-click has already been released).
    private static Vector3 FormationCenter(List<float3> points, Vector3 fallback)
    {
        if(points == null || points.Count == 0) return fallback;
        float3 sum = float3.zero;
        foreach(float3 point in points) sum += point;
        return sum / points.Count;
    }
    // The squad Starstep is about to move; false when nothing of the player's is selected.
    private bool TryGetFirstSelectedSquadCenter(out Vector3 center)
    {
        center = Vector3.zero;
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        foreach(int squadId in BattleManager.Instance.UnitSelectionManager.SelectedSquadIds) {
            if(squadId <= 0) continue;
            SquadEntity squad = BattleManager.Instance.SquadManager.GetSquadEntityFromId(squadId, true);
            if(squad.SelfEntity == Entity.Null || !entityManager.HasComponent<SquadMovementComponent>(squad.SelfEntity)) continue;
            center = entityManager.GetComponentData<SquadMovementComponent>(squad.SelfEntity).SquadCenter;
            return true;
        }
        return false;
    }
    private void CastPlacedSpell(int slotIndex, Vector3 origin, SpellPlacement placement, bool effectHandledByCaster)
    {
        SpellSlotState slot = slotStates[slotIndex];
        if(slot.SpellData == null || slot.OnCooldown || !CanAfford(slot.SpellData)
           || BattleManager.Instance.GamePhase != GamePhase.Battle || !SummonPrefabsReady(slot.SpellData)) {
            RejectCast(slotIndex, "failed re-check at placement time");
            return;
        }
        IAudioRequester.Instance.Play(castSound, ignoreDucking: true);
        IAudioRequester.Instance.SetDucked(false);
        ActiveSpell spellInstance = SpawnActiveSpell(origin);
        if(spellInstance == null) return;
        spellInstance.Load(slot.SpellData, origin, Entity.Null, Team.Player, 0, placement, effectHandledByCaster);

        SpendMana(slot.SpellData.SpellManaCost);
        Debug.Log($"SpellManager: cast {slot.SpellData.name} (placed) for {slot.SpellData.SpellManaCost} mana, {manaRemaining}/{manaMax} remaining");

        slot.CooldownDuration = CooldownFor(slot.SpellData);
        slot.CooldownRemaining = slot.CooldownDuration;
        spellCastButtons[slotIndex].RenderCooldown(1f, true);

        spellsCast++;
        slotsCastMask |= 1 << slotIndex;
        CheckFullArsenal();
        RecordSpellCast(slot.SpellData);
    }
    #endregion
    /// <summary>
    /// "Full Arsenal" - every slot on a fully-equipped hotbar cast at least once this battle.
    /// A partly-filled bar can never qualify, so the achievement always means all four spells.
    /// </summary>
    private void CheckFullArsenal()
    {
        if(SpellTestMode.Active) return;
        for (int i = 0; i < slotStates.Length; i++)
        {
            if(slotStates[i].SpellData == null) return;
            if((slotsCastMask & (1 << i)) == 0) return;
        }
        SteamAchievements.Unlock(AchievementId.FullArsenal);
    }
    public void DeselectSpell()
    {
        int wasArmed = armedMageSquadId;
        armedMageSquadId = 0;
        armedMageSpell = null;
        if(wasArmed != 0) RefreshRingHighlight(wasArmed);
        if(selectedSpellIndex < 0) return;

        spellCastButtons[selectedSpellIndex].SetSelected(false);
        selectedSpellIndex = -1;
    }
    public void AttemptCastSpell()
    {
        if(!validSpellCastPoint){
            Debug.Log($"SpellManager: Cast failed, invalid cast point (selected slot {selectedSpellIndex}, mage {armedMageSquadId}, cursor {spellCursorOrigin})");
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("SpellTargetInvalid"));
            return;
        }

        IAudioRequester.Instance.Play(castSound, ignoreDucking: true);
        if(MageSpellArmed) {
            CastMageSpell();
            return;
        }
        CastSpell();
    }

    #region Mage spells
    /// <summary>
    /// Arms a selected mage squad's spell: same cursor and feedback as a hotbar spell, cast by the
    /// squad itself. Charges are the only gate; a cast queued on cooldown fires when the timer ends.
    /// </summary>
    public void ArmMageSpell(int squadId)
    {
#if !SPELLS
        return;
#endif
        if(BattleManager.Instance.GamePhase != GamePhase.Battle) {
            NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("SpellsLockedUntilBattle"));
            return;
        }
        SquadEntity squad = BattleManager.Instance.SquadManager.GetSquadEntityFromId(squadId, true);
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if(squad.SelfEntity == Entity.Null || !entityManager.HasComponent<MageSquad>(squad.SelfEntity)) {
            Debug.Log($"SpellManager: arm rejected, squad {squadId} is not a mage with charges left");
            return;
        }
        if(entityManager.HasComponent<SquadAmmunition>(squad.SelfEntity)
           && entityManager.GetComponentData<SquadAmmunition>(squad.SelfEntity).Value <= 0) {
            Debug.Log($"SpellManager: arm rejected, squad {squadId} has no charges left");
            return;
        }
        SpellData spell = TabletopTavernData.Instance.SquadAssetsDictionary[squad.UnitName].mageSpell;
        if(spell == null) {
            Debug.LogError($"SpellManager: {squad.UnitName} has no mageSpell assigned, nothing to arm.");
            return;
        }

        IAudioRequester.Instance.Play(selectSound, ignoreDucking: true);
        if(placementPhase != SpellPlacementPhase.None) CancelPlacement(false);
        DeselectSpell();
        armedMageSquadId = squadId;
        armedMageSpell = spell;
        RefreshRingHighlight(squadId);

        if(BattleManager.Instance.CursorMode != CursorMode.CastSpell)
            BattleManager.Instance.SetCursorMode(CursorMode.CastSpell);
        IAudioRequester.Instance.SetDucked(true);
    }
    /// <summary>
    /// The click: hands the cast point to the mage as a MageManualCastOrder. In range, MageCastSystem
    /// casts on its next tick with the timer at zero. Out of range it is also given an approach order
    /// (Attack for a squad target, Move to a point inside range for a ground target) and casts on
    /// arrival. One cast per arm: the cursor drops back to the selection afterwards.
    /// </summary>
    private void CastMageSpell()
    {
        int squadId = armedMageSquadId;
        SpellData spell = armedMageSpell;
        SquadEntity squad = BattleManager.Instance.SquadManager.GetSquadEntityFromId(squadId, true);
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        Entity self = squad.SelfEntity;
        bool castable = self != Entity.Null && entityManager.Exists(self)
            && entityManager.HasComponent<MageManualCastOrder>(self)
            && entityManager.HasComponent<MageSquad>(self)
            && entityManager.HasComponent<SquadMovementComponent>(self)
            && BattleManager.Instance.GamePhase == GamePhase.Battle;
        if(!castable) {
            Debug.Log($"SpellManager: mage cast dropped, squad {squadId} can no longer cast");
            ExitMageCast();
            return;
        }

        Entity target = spell.SpellTargetingType == SpellTargetingType.Squad ? targetedSquadSelfEntity : Entity.Null;
        float3 castPoint = spellCursorOrigin;
        entityManager.SetComponentData(self, new MageManualCastOrder { Position = castPoint, TargetSquadEntity = target });
        entityManager.SetComponentEnabled<MageManualCastOrder>(self, true);

        SquadMovementComponent movement = entityManager.GetComponentData<SquadMovementComponent>(self);
        float range = entityManager.GetComponentData<MageSquad>(self).AttackRange;
        float distance = math.distance(movement.SquadCenter, castPoint);
        if(distance > range) {
            // Same buffer QueueSquadCommand writes for a right-click order; a fresh order replaces
            // whatever the squad was doing, as a player order always does.
            DynamicBuffer<QueuedOrder> orders = entityManager.GetBuffer<QueuedOrder>(self);
            orders.Clear();
            if(target != Entity.Null) {
                orders.Add(QueuedOrder.Attack(entityManager.GetComponentData<SquadEntity>(target).SquadId));
            } else {
                float3 direction = math.normalizesafe(castPoint - movement.SquadCenter);
                float3 goal = castPoint - direction * (range * APPROACH_RANGE_FRACTION);
                // Move goals are authored on the ground plane, as QueueSquadCommand does.
                goal.y = 0f;
                QueuedOrder move = QueuedOrder.Move(goal, movement.SquadRotation);
                move.WidthAndDepth = movement.SquadWidthAndDepth;
                orders.Add(move);
            }
        }
        Debug.Log($"SpellManager: mage {squadId} ordered to cast {spell.name} at {castPoint} (distance {distance:F0}, range {range:F0}{(distance > range ? ", approaching" : "")})");
        ExitMageCast();
    }
    private void ExitMageCast()
    {
        IAudioRequester.Instance.SetDucked(false);
        // Leaving CastSpell runs CursorModeChanged, which clears the armed mage through DeselectSpell.
        bool squadsStillSelected = BattleManager.Instance.UnitSelectionManager.SelectedSquadIds.Count > 0;
        BattleManager.Instance.SetCursorMode(squadsStillSelected ? CursorMode.UnitsSelected : CursorMode.Free);
    }

    private void OnSelectedSquadsChangedForRail(List<int> selectedSquadIds) => RefreshMageRail();

    /// <summary>Rebuilds the rail from the current selection. Also the rail's own callback when a tile's mage is spent.</summary>
    private void RefreshMageRail()
    {
        if(mageCastRail == null || slotStates == null) return;

        List<MageTileInfo> mages = new();
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        foreach(int squadId in GetSelectedMageSquadIds())
        {
            SquadEntity squad = BattleManager.Instance.SquadManager.GetSquadEntityFromId(squadId, true);
            SpellData spell = TabletopTavernData.Instance.SquadAssetsDictionary[squad.UnitName].mageSpell;
            if(spell == null) continue;

            float cooldown = 0f;
            DynamicBuffer<EntityReferenceBufferElement> units = entityManager.GetBuffer<EntityReferenceBufferElement>(squad.SelfEntity);
            if(units.Length > 0 && entityManager.HasComponent<MageCast>(units[0].Entity))
                cooldown = entityManager.GetComponentData<MageCast>(units[0].Entity).Cooldown;

            SquadDisplayCardBattle card = BattleManager.Instance.UIManager.GetSquadCard(squadId);
            mages.Add(new MageTileInfo
            {
                SquadId = squadId,
                Entity = squad.SelfEntity,
                Card = card != null ? card.transform : null,
                UnitName = squad.UnitName,
                Spell = spell,
                CardNumber = BattleManager.Instance.UIManager.GetSquadCardNumber(squadId),
                // Same sum SquadManager.SetUpSquadFlag uses for the flag's charge bar.
                MaxCharges = TabletopTavernData.Instance.GetSquadStats(squad.UnitName).Ammunition
                    + TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE * BattleManager.Instance.SquadManager.GetSquadPrestige(squadId),
                Range = entityManager.GetComponentData<MageSquad>(squad.SelfEntity).AttackRange,
                Cooldown = cooldown,
            });
        }
        mageCastRail.Refresh(mages);
        // The hint strip sits just above the card row; the tiles need that space while they show.
        BattleManager.Instance.UIManager.SetSpellTargetHintLifted(mageCastRail.TileCount > 0);
    }

    // The mage's range ring holds its bright band while its tile is hovered or its spell is armed, so
    // with four Hexenjäger on the field the player can tell which one they are about to spend.
    private int hoveredTileSquadId;
    private void OnMageTileHover(int squadId, bool hovered)
    {
        int previous = hoveredTileSquadId;
        hoveredTileSquadId = hovered ? squadId : (hoveredTileSquadId == squadId ? 0 : hoveredTileSquadId);
        RefreshRingHighlight(previous);
        RefreshRingHighlight(squadId);
    }
    private void RefreshRingHighlight(int squadId)
    {
        if(squadId == 0) return;
        if(BattleManager.Instance.SquadManager.SquadRangeDrawers.TryGetValue(squadId, out ArcherRangeDrawer drawer) && drawer != null)
            drawer.SetHighlighted(squadId == armedMageSquadId || squadId == hoveredTileSquadId);
    }

    // While Y is held every spell tile lights its digit.
    private void OnSpellMenuOpened() => SetSpellMenuOpen(true);
    private void OnSpellMenuClosed() => SetSpellMenuOpen(false);
    private void SetSpellMenuOpen(bool open)
    {
        if(spellCastButtons != null)
        {
            int count = hotbarSlotCount > 0 ? Mathf.Min(hotbarSlotCount, spellCastButtons.Length) : spellCastButtons.Length;
            for(int i = 0; i < count; i++)
                if(spellCastButtons[i] != null) spellCastButtons[i].SetMenuOpen(open);
        }
        if(mageCastRail != null) mageCastRail.SetMenuOpen(open);
    }
    #endregion
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

            SpellData selectedSpellData = ArmedSpell;
            if(selectedSpellData == null) { yield return null; continue; }

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
                        // A broken squad is already leaving the field and cannot be targeted.
                        if(hoveredSquad.SelfEntity != Entity.Null &&
                            entityManager.Exists(hoveredSquad.SelfEntity) &&
                            entityManager.HasComponent<SquadMovementComponent>(hoveredSquad.SelfEntity) &&
                            !entityManager.HasComponent<BrokenSquadTag>(hoveredSquad.SelfEntity)) {
                            castPoint = entityManager.GetComponentData<SquadMovementComponent>(hoveredSquad.SelfEntity).SquadCenter;
                            targetedSquadSelfEntity = hoveredSquad.SelfEntity;
                        } else {
                            validTarget = false;
                        }
                    }
                }

                validSpellCastPoint = validTarget;
                spellCursorOrigin = validTarget ? castPoint : MouseWorldPosition.Instance.GetWorldPosition();
                UpdateTargetHoverAudio(validTarget ? targetedSquadSelfEntity : Entity.Null);
            } else if (selectedSpellData.SpellTargetingType == SpellTargetingType.World) {
                UpdateTargetHoverAudio(Entity.Null);

                if(Physics.Raycast(castPoint, Vector3.down, 20, validSpellCastLayerMask)) {
                    validSpellCastPoint = true;
                } else {
                    validSpellCastPoint = false;
                }
                spellCursorOrigin = MouseWorldPosition.Instance.GetWorldPosition();
            }

            if(MageSpellArmed) UpdateArmedMageRange();
            UpdateTargetingFeedback(selectedSpellData, validSpellCastPoint);
            yield return null;
        }
    }
    // Where the armed mage stands and whether the cursor point is past its reach. A mage that stopped
    // existing mid-aim (killed, or spent and converted) drops the arm.
    private void UpdateArmedMageRange()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        SquadEntity squad = BattleManager.Instance.SquadManager.GetSquadEntityFromId(armedMageSquadId, true);
        Entity self = squad.SelfEntity;
        if(self == Entity.Null || !entityManager.Exists(self) || !entityManager.HasComponent<MageSquad>(self)
           || !entityManager.HasComponent<SquadMovementComponent>(self)) {
            ExitMageCast();
            return;
        }
        float3 center = entityManager.GetComponentData<SquadMovementComponent>(self).SquadCenter;
        ArmedMageCenter = new Vector3(center.x, center.y, center.z);
        ArmedMageRange = entityManager.GetComponentData<MageSquad>(self).AttackRange;
        ArmedMageOutOfRange = Vector3.Distance(ArmedMageCenter, spellCursorOrigin) > ArmedMageRange;
    }
    // Cursor and the label above the unit cards. Runs every frame in cast mode; only writes on a change.
    private void UpdateTargetingFeedback(SpellData spell, bool valid)
    {
        Texture2D cursor = valid ? validTargetCursor : invalidTargetCursor;
        if(cursor != activeCursor) {
            activeCursor = cursor;
            Cursor.SetCursor(cursor, Vector2.zero, UnityEngine.CursorMode.Auto);
        }
        // A valid point past the armed mage's reach is still a cast and reads as one; the leash's
        // colour, not the hint, says the mage will walk first.
        // "Invalid target" in red only once the cursor is on a squad the spell cannot take; until then the
        // hint just says what to click, so a freshly armed friendly spell does not open on an error.
        bool overWrongSquad = !valid && BattleManager.Instance.UIManager.HoveredSquadId != 0;
        if(targetHintSpell == spell && targetHintValid == valid && targetHintOverSquad == overWrongSquad) return;
        targetHintSpell = spell;
        targetHintValid = valid;
        targetHintOverSquad = overWrongSquad;
        LocalizationManager loc = LocalizationManager.Instance;
        string validTargets = string.Format(loc.GetText("SpellTargetValidTargets"), ValidTargetsLabel(spell));
        string message = valid ? loc.GetText("SpellTargetCastHint")
            : overWrongSquad ? $"<color=#E04040>{loc.GetText("SpellTargetInvalid")}</color> {validTargets}"
            : validTargets;
        BattleManager.Instance.UIManager.ShowSpellTargetHint(cursor, message);
    }
    public static string ValidTargetsLabel(SpellData spell)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        if(spell.SpellTargetingType != SpellTargetingType.Squad) return loc.GetText("SpellTargetsGround");
        switch(spell.TargetTeam) {
            case Team.Player: return loc.GetText("SpellTargetsFriendly");
            case Team.Enemy: return loc.GetText("Enemy");
            default: return loc.GetText("SpellTargetsAny");
        }
    }
    private void ClearTargetingFeedback()
    {
        if(activeCursor != null) {
            activeCursor = null;
            Cursor.SetCursor(null, Vector2.zero, UnityEngine.CursorMode.Auto);
        }
        targetHintSpell = null;
        BattleManager.Instance.UIManager.HideSpellTargetHint();
    }
    // Squad-targeted spells only: a World spell's valid point is the whole ground, so it would never stop.
    private void UpdateTargetHoverAudio(Entity target)
    {
        if(target == hoverAudioTarget) return;
        hoverAudioTarget = target;
        if(target == Entity.Null) {
            IAudioRequester.Instance.StopLoop();
            return;
        }
        IAudioRequester.Instance.Play(targetHoverSound, ignoreDucking: true);
        IAudioRequester.Instance.PlayLoop(targetHoverLoop);
    }
    public async void CastSpell()
    {
        if(MageSpellArmed) {
            CastMageSpell();
            return;
        }
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

        ActiveSpell spellInstance = SpawnActiveSpell(spellCursorOrigin);
        if(spellInstance == null) {
            BattleManager.Instance.SetCursorMode(CursorMode.Free);
            return;
        }
        spellInstance.Load(slot.SpellData, spellCursorOrigin, targetedSquadSelfEntity);
        // Debug.Log($"SpellManager: Cast succeeded, {slot.SpellData.name} at {spellCursorOrigin} (targeting={slot.SpellData.SpellTargetingType}, targetSquad={targetedSquadSelfEntity})");

        SpendMana(slot.SpellData.SpellManaCost);
        IAudioRequester.Instance.SetDucked(false);
        Debug.Log($"SpellManager: cast {slot.SpellData.name} for {slot.SpellData.SpellManaCost} mana, {manaRemaining}/{manaMax} remaining");

        slot.CooldownDuration = CooldownFor(slot.SpellData);
        slot.CooldownRemaining = slot.CooldownDuration;
        spellCastButtons[selectedSpellIndex].RenderCooldown(1f, true);

        spellsCast++;
        slotsCastMask |= 1 << selectedSpellIndex;
        CheckFullArsenal();
        RecordSpellCast(slot.SpellData);

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
        ActiveSpell spellInstance = SpawnActiveSpell(position);
        if(spellInstance == null) return;
        spellInstance.Load(spellData, position, targetSquadEntity, sourceTeam, sourceSquadId);
        if(sourceTeam == Team.Player) RecordSpellCast(spellData);
    }

    // Per-battle tally for the runEnded analytics event; SaveSquadsPostBattle folds it into RunStats.
    private static void RecordSpellCast(SpellData spellData)
    {
        SaveDataHandler.SpellsCastThisBattle.TryGetValue(spellData.Spell, out int casts);
        SaveDataHandler.SpellsCastThisBattle[spellData.Spell] = casts + 1;
    }
    // Every cast in the game starts from the one shared prefab; per-spell art rides in as
    // SpellData.SpellVisualPrefab, which ActiveSpell.Load spawns underneath.
    private ActiveSpell SpawnActiveSpell(Vector3 position)
    {
        if(aoeSpellPrefab == null) {
            Debug.LogError("SpellManager: aoeSpellPrefab is not assigned, nothing can be cast.", this);
            return null;
        }
        return Instantiate(aoeSpellPrefab, position, Quaternion.identity);
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
            IAudioRequester.Instance.SetDucked(false);
            UpdateTargetHoverAudio(Entity.Null);
            ClearTargetingFeedback();
        }
    }
    private void OnDestroy()
    {
#if SPELLS
        if(IAudioRequester.HasInstance)
        {
            IAudioRequester.Instance.SetDucked(false);
            IAudioRequester.Instance.StopLoop();
        }
        if(BattleManager.HasInstance)
        {
            BattleManager.Instance.OnCursorModeChanged -= CursorModeChanged;
            BattleManager.Instance.OnGamePhaseChanged -= GamePhaseChanged;
        }
        if(InputHandler.HasInstance)
        {
            InputHandler.Instance.OnSpellMenuDigit -= OnSpellMenuDigit;
            InputHandler.Instance.OnSpellMenu -= OnSpellMenuOpened;
            InputHandler.Instance.OnSpellMenuCanceled -= OnSpellMenuClosed;
        }
        if(UnitSelectionManager.Instance != null)
        {
            UnitSelectionManager.Instance.OnSelectedSquadsChanged -= OnSelectedSquadsChangedForPlacement;
            UnitSelectionManager.Instance.OnSelectedSquadsChanged -= OnSelectedSquadsChangedForRail;
        }
#endif
    }
}
}
