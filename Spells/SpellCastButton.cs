using System;
using System.Threading.Tasks;
using Memori.Input;
using Memori.Tooltip;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Memori.Localization;

namespace TJ.Spells
{
public class SpellCastButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Icon")]
    [SerializeField] private Image spellIcon;
    [SerializeField] private Button selectSpellButton;
    [SerializeField] private MemoriTooltipTrigger tooltipTrigger;

    [Header("Cooldown")]
    [SerializeField] private Image cooldownImage;
    [SerializeField] private Image availableGem, selectedGem, onCooldownGem;

    [Header("Outline")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private Color outlineDefaultColor;
    [SerializeField] private Color mouseOverOutlineColor, selectedOutlineColor;

    private Color cachedCooldownColor;
    private bool cachedSelected;
    private bool cachedOnCooldown;

    // Pre-battle spell browsing (custom battle only). Null when browsing is disabled, in which case
    // hovering this button never opens the browse menu.
    private Action onBrowseHoverEnter, onBrowseHoverExit;

    private void Awake()
    {
        cachedCooldownColor = cooldownImage.color;
    }

    public void LoadSpellUI(SpellData spellData, Action onSelectRequested, int hotkeyNumber,
        Action _onBrowseHoverEnter = null, Action _onBrowseHoverExit = null)
    {
        onBrowseHoverEnter = _onBrowseHoverEnter;
        onBrowseHoverExit = _onBrowseHoverExit;

        spellIcon.sprite = spellData.SpellSprite;

        selectSpellButton.onClick.RemoveAllListeners();
        selectSpellButton.onClick.AddListener(() => onSelectRequested?.Invoke());

        SetSelected(false);
        RenderCooldown(0f, false);

        string localizedSpellName = LocalizationManager.Instance.GetText(spellData.Spell.ToString()) + " (" + GetHotkeyLabel(hotkeyNumber) + ")";
        string localizedSpellDescription = spellData.GetLocalizedSpellDescription();

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

    public void SetSelected(bool selected)
    {
        cachedSelected = selected;
        outlineImage.color = selected ? selectedOutlineColor : outlineDefaultColor;
        RefreshGems();
    }

    public void RenderCooldown(float remainingFraction01, bool onCooldown)
    {
        cachedOnCooldown = onCooldown;
        cooldownImage.fillAmount = onCooldown ? Mathf.Clamp01(remainingFraction01) : 0f;
        RefreshGems();
    }

    private void RefreshGems()
    {
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
        if(!cachedOnCooldown && !cachedSelected)
            outlineImage.color = mouseOverOutlineColor;

        onBrowseHoverEnter?.Invoke();
    }
    public void OnPointerExit(PointerEventData eventData)
    {
        if(!cachedOnCooldown && !cachedSelected)
            outlineImage.color = outlineDefaultColor;

        onBrowseHoverExit?.Invoke();
    }
}
}
