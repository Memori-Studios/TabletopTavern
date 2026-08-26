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
/// One slot on the battle hotbar. Follows the same split as <see cref="SpellBrowseSlot"/>:
///
///   INTERIOR is chromatic - the gradient, the rail and the icon carry the faction's display colour,
///   so a spell looks the same here as it did in the picker.
///   FRAME is achromatic - <see cref="outlineImage"/> alone carries hover, armed-to-cast and
///   being-swapped, because every hue is already spent on faction identity.
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

    [Header("Interior (faction)")]
    [SerializeField] private Image raceGradientImage;
    [SerializeField] private Image raceRailImage;

    // Optional numeral showing this spell's mana cost. Null-guarded, so the cost still reaches the
    // player through the tooltip if the prefab has no such label.
    [SerializeField] private TMP_Text manaCostText;
    // Optional. Shown on a slot the player has not unlocked with Renown. A locked slot already holds
    // no SpellData, so SelectSpell refuses it either way - this only stops it reading as a bug.
    [SerializeField] private GameObject lockedOverlay;

    private const float RAIL_ALPHA = 0.9f;
    // Unaffordable reads on brightness, never hue - every hue is spent on faction identity. Same
    // channel SpellBrowseSlot dims an equipped row with.
    private const float UNAFFORDABLE_ICON_ALPHA = 0.3f;

    private Color cachedCooldownColor;
    private Color factionColor = Color.white;
    private bool cachedSelected;
    private bool cachedOnCooldown;
    private bool cachedHovered;
    private bool cachedBrowseTarget;
    private bool cachedAffordable = true;
    private bool hasSpell;

    // Pre-battle spell browsing (custom battle only). Null when browsing is disabled, in which case
    // hovering this button never opens the browse menu.
    private Action onBrowseHoverEnter, onBrowseHoverExit;

    private void Awake()
    {
        if(cooldownImage != null) cachedCooldownColor = cooldownImage.color;
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

    public void LoadSpellUI(SpellData spellData, Action onSelectRequested, int hotkeyNumber,
        Action _onBrowseHoverEnter = null, Action _onBrowseHoverExit = null)
    {
        if(!ReferencesAssigned()) return;

        onBrowseHoverEnter = _onBrowseHoverEnter;
        onBrowseHoverExit = _onBrowseHoverExit;

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

        if(manaCostText != null) manaCostText.gameObject.SetActive(hasSpell);

        if(!hasSpell)
        {
            tooltipTrigger.SetUpToolTip();
            return;
        }

        spellIcon.sprite = spellData.SpellSprite;
        selectSpellButton.onClick.AddListener(() => onSelectRequested?.Invoke());

        string localizedCost = string.Format(
            LocalizationManager.Instance.GetText("SpellManaCostLine"), spellData.SpellManaCost);
        if(manaCostText != null) manaCostText.text = spellData.SpellManaCost.ToString();

        string localizedSpellName = LocalizationManager.Instance.GetText(spellData.Spell.ToString()) + " (" + GetHotkeyLabel(hotkeyNumber) + ")";
        // Cost leads the description. A player deciding whether to arm this spell needs the price
        // before the flavour, and the hotbar numeral is optional so this is the only guaranteed place.
        string localizedSpellDescription = localizedCost + "\n\n" + spellData.GetLocalizedSpellDescription();

        tooltipTrigger.SetUpToolTip(localizedSpellName, localizedSpellDescription);
    }

    private string GetHotkeyLabel(int hotkeyNumber)
    {
        InputAction hotkeyAction = hotkeyNumber switch
        {
            1 => InputHandler.Instance.GameControls.Battle.SelectSpell1,
            2 => InputHandler.Instance.GameControls.Battle.SelectSpell2,
            3 => InputHandler.Instance.GameControls.Battle.SelectSpell3,
            4 => InputHandler.Instance.GameControls.Battle.SelectSpell4,
            _ => null
        };
        if (hotkeyAction == null) return "";

        return InputControlPath.ToHumanReadableString(
            hotkeyAction.bindings[0].effectivePath,
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
    }

    private void RefreshIconAlpha()
    {
        if(spellIcon == null) return;

        Color c = factionColor;
        if(!cachedAffordable) c.a *= UNAFFORDABLE_ICON_ALPHA;
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
        if(!hasSpell)
        {
            outlineImage.color = ColorData.SpellFrameRest;
            if(targetBrackets != null) targetBrackets.SetActive(false);
            return;
        }

        outlineImage.color = cachedBrowseTarget || cachedSelected ? ColorData.SpellFrameActive
                           : cachedHovered                       ? ColorData.SpellFrameHover
                                                                 : ColorData.SpellFrameRest;

        if(targetBrackets != null) targetBrackets.SetActive(cachedBrowseTarget);
    }

    public void RenderCooldown(float remainingFraction01, bool onCooldown)
    {
        cachedOnCooldown = onCooldown;
        cooldownImage.fillAmount = onCooldown ? Mathf.Clamp01(remainingFraction01) : 0f;
        RefreshGems();
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
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        cachedHovered = false;
        RefreshFrame();

        onBrowseHoverExit?.Invoke();
    }
}
}
