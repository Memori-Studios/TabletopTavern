using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Audio;
using Memori.Input;
using Memori.Notifications;
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
    // Pool the player can swap from before the battle starts. Curated in the inspector.
    [SerializeField] private SpellData[] availableSpells;
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

    // Pre-battle browse state.
    private bool browsingEnabled;
    private int hoveredButtonSlot = -1;
    private bool pointerOverBrowseMenu;
    private Coroutine browseCloseRoutine;

    private bool validSpellCastPoint;
    private Vector3 spellCursorOrigin;
    public Vector3 SpellCursorOrigin => spellCursorOrigin;
    public bool ValidSpellCastPoint => validSpellCastPoint;
    public float SelectedSpellRadius => selectedSpellIndex >= 0 ? slotStates[selectedSpellIndex].SpellData.SpellRadius : 0f;
    int spellsCast = 0;
    bool mouseReleased = true;
    public bool MouseReleased => mouseReleased;

    private void Start()
    {
#if UNITY_EDITOR || SPELLS
        BattleManager.Instance.OnCursorModeChanged += CursorModeChanged;
        BattleManager.Instance.OnGamePhaseChanged += GamePhaseChanged;
        InputHandler.Instance.OnSelectSpell1 += SelectSpellHotkey1;
        InputHandler.Instance.OnSelectSpell2 += SelectSpellHotkey2;
        InputHandler.Instance.OnSelectSpell3 += SelectSpellHotkey3;
        InputHandler.Instance.OnSelectSpell4 += SelectSpellHotkey4;
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
        if(_spells != null) {
            defaultSpells = _spells;
        }

        // Swapping is a pre-battle, custom-battle convenience only. It stays off for campaign battles.
        browsingEnabled = BattleManager.Instance.BattleSaveManager.IsCustomBattle;

        slotStates = new SpellSlotState[spellCastButtons.Length];
        for (int i = 0; i < spellCastButtons.Length; i++) {
            slotStates[i] = new SpellSlotState { SpellData = defaultSpells[i] };
            int slotIndex = i;
            WireSlotButton(slotIndex, defaultSpells[i]);
        }
        selectedSpellIndex = -1;

        spellQuickCastMenu.Load(defaultSpells);

        if(browsingEnabled && spellBrowseMenu != null)
            spellBrowseMenu.Initialize(availableSpells, SwapSpell, OnBrowseMenuHoverEnter, OnBrowseMenuHoverExit);
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
        spellBrowseMenu.Open(slotIndex, GetEquippedSpells());
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
            spellBrowseMenu.Close();
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
        spellQuickCastMenu.Load(GetEquippedSpells());

        // Rebuild the list so the swapped-out spell reappears and the swapped-in one drops off.
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

#if !UNITY_EDITOR && !SPELLS
            Debug.Log($"SpellManager: Select failed for slot {slotIndex}, spell selection is disabled in this build (define SPELLS to enable)");
            return;
#endif

        if(slotStates[slotIndex].OnCooldown) {
            Debug.Log($"SpellManager: Select failed for slot {slotIndex} ({slotStates[slotIndex].SpellData.name}), on cooldown ({slotStates[slotIndex].CooldownRemaining:F1}s remaining)");
            spellCastButtons[slotIndex].FlashCooldownImage(Color.red);
            return;
        }

        if(selectedSpellIndex >= 0 && selectedSpellIndex != slotIndex)
            spellCastButtons[selectedSpellIndex].SetSelected(false);

        selectedSpellIndex = slotIndex;
        spellCastButtons[selectedSpellIndex].SetSelected(true);
        // Debug.Log($"SpellManager: Selected slot {slotIndex} ({slotStates[slotIndex].SpellData.name})");

        if(BattleManager.Instance.CursorMode != CursorMode.CastSpell){
            BattleManager.Instance.SetCursorMode(CursorMode.CastSpell);
        }
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
        ActiveSpell spellInstance = Instantiate(slot.SpellData.SpellPrefab, spellCursorOrigin, Quaternion.identity);
        spellInstance.Load(slot.SpellData, spellCursorOrigin, targetedSquadSelfEntity);
        // Debug.Log($"SpellManager: Cast succeeded, {slot.SpellData.name} at {spellCursorOrigin} (targeting={slot.SpellData.SpellTargetingType}, targetSquad={targetedSquadSelfEntity})");

        slot.CooldownDuration = slot.SpellData.SpellCooldown;
        slot.CooldownRemaining = slot.CooldownDuration;
        spellCastButtons[selectedSpellIndex].RenderCooldown(1f, true);

        spellsCast++;

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
    public void CursorModeChanged(CursorMode _cursorMode)
    {
        if(_cursorMode == CursorMode.CastSpell) {
            StartCoroutine(GetMouseCursorPosition());
        } else {
            DeselectSpell();
        }
    }
    private void OnDestroy()
    {
#if UNITY_EDITOR || SPELLS
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
#endif
    }
}
}
