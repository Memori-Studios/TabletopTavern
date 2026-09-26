using System;
using System.Threading.Tasks;
using TMPro;
using Memori.Input;
using Memori.Tooltip;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Memori.Localization;

namespace TJ.Spells
{
/// <summary>
/// One slot on the battle hotbar: a rounded square whose icon and resting border carry the faction's
/// display colour. State reads on brightness, never a new hue, because every hue is spent on faction
/// identity: hover lightens the border, armed-to-cast and being-swapped turn it white and light the
/// faction-coloured glow behind the tile.
///
/// The icon used to be tinted green while the browse menu was open for this slot. That is the
/// collision this system removes: green is also Gruntkin.
/// </summary>
public class SpellCastButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Icon")]
    [SerializeField] private Image spellIcon;
    [SerializeField] private Button selectSpellButton;
    [SerializeField] private MemoriTooltipTrigger tooltipTrigger;

    [Header("Cooldown")]
    [SerializeField] private Image cooldownImage;
    // Superseded by the frame plus the cooldown sweep, which read at battle resolution in a way a 7px
    // lozenge does not. Null-guarded so they can be deleted from the prefab without a code change.
    [SerializeField] private Image availableGem, selectedGem, onCooldownGem;

    [Header("Frame (state)")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject targetBrackets;
    // Optional. Lit behind the tile while it is armed or being swapped.
    [SerializeField] private Image selectedGlow;

    [Header("Interior (faction)")]
    [SerializeField] private Image raceGradientImage;
    [SerializeField] private Image raceRailImage;

    // Optional numeral showing this spell's mana cost. Null-guarded, so the cost still reaches the
    // player through the tooltip if the prefab has no such label.
    [SerializeField] private TMP_Text manaCostText;
    // Optional gem behind manaCostText; toggled with it and tinted red while the pool cannot cover the cost.
    [SerializeField] private Image manaCostGem;
    // Optional. Shown on a slot the player has not unlocked with Renown. A locked slot already holds
    // no SpellData, so SelectSpell refuses it either way - this only stops it reading as a bug.
    [SerializeField] private GameObject lockedOverlay;

    [Header("Hotkey")]
    // Optional. The spell menu digit, hidden at rest and large over the icon while the menu key is held.
    [SerializeField] private TMP_Text hotkeyText;
    private const float HOTKEY_OPEN_SIZE = 30f;
    private const float HOTKEY_REST_ALPHA = 0.7f;
    private RectTransform hotkeyRect;
    private Vector2 hotkeyRestAnchorMin, hotkeyRestAnchorMax, hotkeyRestPivot, hotkeyRestPosition, hotkeyRestSize;
    private float hotkeyRestFontSize;
    private HorizontalAlignmentOptions hotkeyRestHorizontal;
    private VerticalAlignmentOptions hotkeyRestVertical;

    private const float RAIL_ALPHA = 0.9f;
    // The resting border steps back with the icon when the pool cannot cover the spell.
    private const float UNAFFORDABLE_BORDER_ALPHA = 0.35f;
    // Unaffordable reads on brightness, never hue - every hue is spent on faction identity. Same
    // channel SpellBrowseSlot dims an equipped row with.
    private const float UNAFFORDABLE_ICON_ALPHA = 0.3f;
    // The icon steps back while the Y-menu digit sits over it, so the digit reads first.
    private const float MENU_OPEN_ICON_ALPHA = 0.35f;
    private bool cachedMenuOpen;

    private Color cachedCooldownColor;
    private Color factionColor = Color.white;
    private bool cachedSelected;
    private bool cachedOnCooldown;
    private bool cachedHovered;
    private bool cachedBrowseTarget;
    // A mage walking to a manual cast: the frame breathes between rest and active until it lands.
    private bool cachedPending;
    private const float PENDING_PULSE_SECONDS = 1.2f;
    private bool cachedAffordable = true;
    private bool hasSpell;
    private bool readOnly;
    private SpellData cachedSpell;
    private int cachedHotkeyNumber;
    // The mage tile's caster facts, or default for a hotbar slot. Kept so live state can rebuild the tooltip.
    private SpellTooltip.Context cachedTooltipContext;
    // Live tooltip state, only repainted when the shown whole number changes.
    private int cachedManaShort;
    private int cachedCooldownSeconds;

    // Pre-battle spell browsing (custom battle only). Null when browsing is disabled, in which case
    // hovering this button never opens the browse menu.
    private Action onBrowseHoverEnter, onBrowseHoverExit;
    // Hotbar only: lets SpellManager preview this spell's cost on the mana bar. Null elsewhere.
    private Action<bool> onHoverChanged;

    private void Awake()
    {
        if(cooldownImage != null) cachedCooldownColor = cooldownImage.color;
        if(hotkeyText != null)
        {
            // The prefab authors the rest layout; the open layout is derived in code.
            hotkeyRect = hotkeyText.rectTransform;
            hotkeyRestAnchorMin = hotkeyRect.anchorMin;
            hotkeyRestAnchorMax = hotkeyRect.anchorMax;
            hotkeyRestPivot = hotkeyRect.pivot;
            hotkeyRestPosition = hotkeyRect.anchoredPosition;
            hotkeyRestSize = hotkeyRect.sizeDelta;
            hotkeyRestFontSize = hotkeyText.fontSize;
            hotkeyRestHorizontal = hotkeyText.horizontalAlignment;
            hotkeyRestVertical = hotkeyText.verticalAlignment;
            hotkeyText.alpha = HOTKEY_REST_ALPHA;
            hotkeyText.enabled = cachedMenuOpen;
        }
    }

    /// <summary>
    /// Names a missing reference instead of letting it surface as a bare NullReferenceException.
    /// LoadSpellUI runs inside BattleCleanUpManager's async chain, where the stack tells you the type
    /// that failed but not which of the four hotbar buttons it was. Passing `this` as the log context
    /// makes the message clickable straight to the object.
    /// </summary>
    private bool ReferencesAssigned()
    {
        if(spellIcon != null && selectSpellButton != null && cooldownImage != null
            && outlineImage != null && tooltipTrigger != null) return true;

        Debug.LogError($"SpellCastButton on '{name}' is missing a required reference and will not render. " +
            $"spellIcon={spellIcon != null}, selectSpellButton={selectSpellButton != null}, " +
            $"cooldownImage={cooldownImage != null}, outlineImage={outlineImage != null}, " +
            $"tooltipTrigger={tooltipTrigger != null}", this);
        return false;
    }

    /// <summary>
    /// <paramref name="tooltipContext"/> carries a mage tile's caster facts; the hotbar leaves it default.
    /// </summary>
    public void LoadSpellUI(SpellData spellData, Action onSelectRequested, int hotkeyNumber,
        Action _onBrowseHoverEnter = null, Action _onBrowseHoverExit = null, SpellTooltip.Context tooltipContext = default,
        Action<bool> _onHoverChanged = null)
    {
        if(!ReferencesAssigned()) return;

        onBrowseHoverEnter = _onBrowseHoverEnter;
        onBrowseHoverExit = _onBrowseHoverExit;
        onHoverChanged = _onHoverChanged;
        cachedSpell = spellData;
        cachedHotkeyNumber = hotkeyNumber;
        cachedTooltipContext = tooltipContext;
        cachedManaShort = 0;
        cachedCooldownSeconds = 0;
        if(hotkeyText != null)
        {
            // 10 is the 0 key; anything past the ten digits has no key and shows nothing.
            hotkeyText.text = hotkeyNumber >= 1 && hotkeyNumber <= 10 ? (hotkeyNumber % 10).ToString() : "";
            hotkeyText.gameObject.SetActive(spellData != null);
        }

        // Assigned before SetSelected, which repaints the frame off it.
        hasSpell = spellData != null;

        cachedBrowseTarget = false;
        cachedHovered = false;
        // Reset before ApplyFactionColour below, which repaints the icon off it.
        cachedAffordable = true;
        SetSelected(false);
        RenderCooldown(0f, false);
        selectSpellButton.onClick.RemoveAllListeners();

        // An empty slot (hotbar longer than the loadout) shows no icon and cannot be clicked.
        spellIcon.enabled = hasSpell;
        selectSpellButton.interactable = hasSpell;
        ApplyFactionColour(spellData);
        RefreshFrame();

        // Mage spells spend charges, not mana, and carry cost 0: no gem for them.
        bool showCost = hasSpell && spellData.SpellManaCost > 0;
        if(manaCostText != null) manaCostText.gameObject.SetActive(showCost);
        if(manaCostGem != null) manaCostGem.gameObject.SetActive(showCost);
        RefreshCostGem();

        if(!hasSpell)
        {
            tooltipTrigger.SetUpToolTip();
            return;
        }

        spellIcon.sprite = spellData.SpellSprite;
        selectSpellButton.onClick.AddListener(() => onSelectRequested?.Invoke());
        if(manaCostText != null) manaCostText.text = spellData.SpellManaCost.ToString();

        tooltipTrigger.SetContentProvider(BuildTooltip);
    }

    /// <summary>Rebuilds the tooltip with new caster facts (a mage tile after spending a charge).</summary>
    public void RefreshTooltip(SpellTooltip.Context tooltipContext)
    {
        cachedTooltipContext = tooltipContext;
        RefreshLiveTooltip();
    }

    /// <summary>Hotbar only: how much mana the pool is short of this spell's cost, 0 when affordable.</summary>
    public void SetManaShort(int manaShort)
    {
        if(cachedManaShort == manaShort) return;
        cachedManaShort = manaShort;
        RefreshLiveTooltip();
    }

    private void RefreshLiveTooltip()
    {
        if(cachedSpell == null || tooltipTrigger == null) return;
        tooltipTrigger.RefreshContent();
    }

    private TooltipContent BuildTooltip()
    {
        SpellTooltip.Context context = cachedTooltipContext;
        context.HotkeyNumber = cachedHotkeyNumber;
        context.ManaShort = cachedManaShort;
        context.CooldownLeft = cachedCooldownSeconds;
        return SpellTooltip.Build(cachedSpell, context);
    }

    /// <summary>
    /// The digit shows only while the spell menu key is held, filling the tile so the player reads
    /// "press 3" at a glance; released, it hides and drops back to the layout the prefab authored.
    /// </summary>
    public void SetMenuOpen(bool open)
    {
        if(hotkeyText == null || hotkeyRect == null) return;
        cachedMenuOpen = open;
        hotkeyText.enabled = open;
        RefreshIconAlpha();
        if(open)
        {
            hotkeyRect.anchorMin = Vector2.zero;
            hotkeyRect.anchorMax = Vector2.one;
            hotkeyRect.pivot = new Vector2(0.5f, 0.5f);
            hotkeyRect.anchoredPosition = Vector2.zero;
            hotkeyRect.sizeDelta = Vector2.zero;
            hotkeyText.fontSize = HOTKEY_OPEN_SIZE;
            hotkeyText.horizontalAlignment = HorizontalAlignmentOptions.Center;
            hotkeyText.verticalAlignment = VerticalAlignmentOptions.Middle;
            hotkeyText.alpha = 1f;
        }
        else
        {
            hotkeyRect.anchorMin = hotkeyRestAnchorMin;
            hotkeyRect.anchorMax = hotkeyRestAnchorMax;
            hotkeyRect.pivot = hotkeyRestPivot;
            hotkeyRect.anchoredPosition = hotkeyRestPosition;
            hotkeyRect.sizeDelta = hotkeyRestSize;
            hotkeyText.fontSize = hotkeyRestFontSize;
            hotkeyText.horizontalAlignment = hotkeyRestHorizontal;
            hotkeyText.verticalAlignment = hotkeyRestVertical;
            hotkeyText.alpha = HOTKEY_REST_ALPHA;
        }
    }

    // The spell menu key from its binding, so a rebind shows.
    public static string GetSpellMenuKeyName()
    {
        return InputControlPath.ToHumanReadableString(
            InputHandler.Instance.GameControls.Battle.SpellMenu.bindings[0].effectivePath,
            InputControlPath.HumanReadableStringOptions.OmitDevice
        );
    }

    /// <summary>
    /// Paints the three interior layers from the spell's race. Keyed on the Race enum rather than
    /// RaceData, so the four Lesser spells get a real neutral instead of falling through to white.
    /// </summary>
    private void ApplyFactionColour(SpellData spellData)
    {
        factionColor = spellData == null ? Color.white : ColorData.GetRaceDisplayColor(spellData.Race);

        RefreshIconAlpha();

        if(raceGradientImage != null)
        {
            raceGradientImage.enabled = spellData != null;
            if(spellData != null) raceGradientImage.color = ColorData.GetRaceDisplayTint(spellData.Race);
        }
        if(raceRailImage != null)
        {
            raceRailImage.enabled = spellData != null;
            if(spellData != null) raceRailImage.color = ColorData.WithAlpha255(factionColor, RAIL_ALPHA * 255f);
        }
    }

    public void SetSelected(bool selected)
    {
        cachedSelected = selected;
        RefreshFrame();
        RefreshGems();
    }

    /// <summary>
    /// Dims the icon while the mana pool cannot cover this spell. Independent of cooldown - a slot can
    /// be off cooldown and still unaffordable, and the two states look different on purpose: cooldown
    /// is a sweep that visibly refills, unaffordable is flat and only changes when you spend elsewhere.
    /// </summary>
    public void SetAffordable(bool affordable)
    {
        if(cachedAffordable == affordable) return;

        cachedAffordable = affordable;
        RefreshIconAlpha();
        RefreshCostGem();
        RefreshFrame();
    }

    private void RefreshCostGem()
    {
        if(manaCostGem == null) return;
        manaCostGem.color = cachedAffordable ? ColorData.ManaCostGem : ColorData.ManaUnaffordable;
    }

    private void RefreshIconAlpha()
    {
        if(spellIcon == null) return;

        Color c = factionColor;
        if(!cachedAffordable) c.a *= UNAFFORDABLE_ICON_ALPHA;
        if(cachedMenuOpen) c.a *= MENU_OPEN_ICON_ALPHA;
        spellIcon.color = c;
    }

    /// <summary>
    /// Marks a slot the player has not unlocked. Purely presentational: a locked slot carries no
    /// SpellData, so it is already unselectable and uncastable.
    /// </summary>
    public void SetLocked(bool locked)
    {
        if(lockedOverlay != null) lockedOverlay.SetActive(locked);
    }

    /// <summary>
    /// Display only (the map HUD): the button is disabled and the hover frame stays off, so the tile
    /// reads as information rather than something to arm. The tooltip still works.
    /// </summary>
    public void SetReadOnly()
    {
        readOnly = true;
        selectSpellButton.interactable = false;
        RefreshFrame();
    }

    /// <summary>Marks this slot as the one the pre-battle browse menu is currently open for.</summary>
    public void SetBrowseHighlighted(bool highlighted)
    {
        cachedBrowseTarget = highlighted;
        RefreshFrame();
    }

    /// <summary>
    /// Suppresses the floating tooltip while the pre-battle picker is available, because the picker's
    /// fixed info panel already describes this slot the moment you hover it and two descriptions
    /// competing in two places is worse than either alone.
    ///
    /// Suppressed for the whole browsing window rather than only while the menu is open: hovering the
    /// button is what OPENS the menu, so those two events are simultaneous and gating on "is it open"
    /// would race the tooltip's own delay. Once GamePhase.Battle locks the loadout the floating tooltip
    /// is the only description left, and SpellManager hands it back.
    ///
    /// Safe to flip mid-hover - MemoriTooltipTrigger.OnDisable hides anything already showing.
    /// </summary>
    public void SetBrowseModeActive(bool active)
    {
        if(tooltipTrigger == null) return;
        tooltipTrigger.enabled = !active;
    }

    /// <summary>
    /// The whole state channel. Priority runs browse target, then armed to cast, then hover - the
    /// slot you are editing outranks the slot you are holding, because only one can be true at a time
    /// in practice and the swap is the more destructive action.
    /// </summary>
    private void RefreshFrame()
    {
        bool active = hasSpell && (cachedBrowseTarget || cachedSelected);
        if(selectedGlow != null)
        {
            selectedGlow.enabled = active;
            selectedGlow.color = factionColor;
        }

        if(!hasSpell)
        {
            outlineImage.color = ColorData.SpellFrameRest;
            if(targetBrackets != null) targetBrackets.SetActive(false);
            return;
        }

        outlineImage.color = active                              ? ColorData.SpellFrameActive
                           : cachedHovered && !readOnly          ? ColorData.SpellFrameHover
                           : cachedPending                       ? PendingFrameColor()
                                                                 : RestFrameColor();

        if(targetBrackets != null) targetBrackets.SetActive(cachedBrowseTarget);
    }

    /// <summary>On while the squad holds an unfired manual cast order. The frame pulses until it fires or is voided.</summary>
    public void SetPending(bool pending)
    {
        if(cachedPending == pending) return;
        cachedPending = pending;
        RefreshFrame();
    }
    private Color RestFrameColor()
    {
        Color c = factionColor;
        if(!cachedAffordable) c.a *= UNAFFORDABLE_BORDER_ALPHA;
        return c;
    }
    private Color PendingFrameColor()
    {
        float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / PENDING_PULSE_SECONDS));
        return Color.Lerp(RestFrameColor(), ColorData.SpellFrameActive, t);
    }
    private void Update()
    {
        // Only the pulse animates; every other frame state is event-driven.
        if(cachedPending && !cachedSelected && !cachedHovered && !cachedBrowseTarget) RefreshFrame();
    }
    public void RenderCooldown(float remainingFraction01, bool onCooldown, float secondsLeft = 0f)
    {
        cachedOnCooldown = onCooldown;
        cooldownImage.fillAmount = onCooldown ? Mathf.Clamp01(remainingFraction01) : 0f;
        RefreshGems();

        int seconds = onCooldown ? Mathf.CeilToInt(secondsLeft) : 0;
        if(seconds == cachedCooldownSeconds) return;
        cachedCooldownSeconds = seconds;
        RefreshLiveTooltip();
    }

    private void RefreshGems()
    {
        if(onCooldownGem == null || selectedGem == null || availableGem == null) return;

        onCooldownGem.gameObject.SetActive(cachedOnCooldown);
        selectedGem.gameObject.SetActive(cachedSelected && !cachedOnCooldown);
        availableGem.gameObject.SetActive(!cachedSelected && !cachedOnCooldown);
    }

    public async void FlashCooldownImage(Color _color)
    {
        _color.a = 0.75f;
        cooldownImage.color = _color;
        await Task.Delay(100);
        cooldownImage.color = cachedCooldownColor;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        cachedHovered = true;
        RefreshFrame();

        onBrowseHoverEnter?.Invoke();
        onHoverChanged?.Invoke(true);
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        cachedHovered = false;
        RefreshFrame();

        onBrowseHoverExit?.Invoke();
        onHoverChanged?.Invoke(false);
    }
}
}
