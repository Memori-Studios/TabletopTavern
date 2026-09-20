using System;
using Memori.Localization;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
/// <summary>
/// One selected mage squad's spell on the caster rail: a SpellCastButton (icon, frame, cooldown
/// sweep, tooltip, hotkey digit) plus the charge pips and the badge tying it to the squad's card.
/// View only: SpellManager arms, MageCastRail feeds it ECS state each frame.
/// </summary>
public class MageCastTile : MonoBehaviour
{
    [SerializeField] private SpellCastButton button;
    // Pips read as a count, which is what three charges are; a bar reads as a quantity.
    [SerializeField] private Image[] pips;
    [SerializeField] private Color pipOnColor = new Color(0.498f, 0.769f, 1f, 1f);
    [SerializeField] private Color pipOffColor = new Color(0.235f, 0.290f, 0.384f, 1f);
    [SerializeField] private TMP_Text badgeText;

    public int SquadId { get; private set; }
    public Entity SquadEntity { get; private set; }

    private UnitName unitName;
    private SpellData spell;
    private int cardNumber;
    private int maxCharges;
    private int charges = -1;
    private float range;
    private float cooldown;

    public void Load(MageTileInfo info, int hotkeyNumber, Action onSelect, Action<bool> onHover)
    {
        SquadId = info.SquadId;
        SquadEntity = info.Entity;
        unitName = info.UnitName;
        spell = info.Spell;
        cardNumber = info.CardNumber;
        maxCharges = Mathf.Max(1, info.MaxCharges);
        range = info.Range;
        cooldown = info.Cooldown;
        charges = -1;

        button.LoadSpellUI(spell, onSelect, hotkeyNumber,
            () => onHover?.Invoke(true), () => onHover?.Invoke(false), StatBlock(info.MaxCharges));
        // The base prefab ships its Renown lock overlay on; a mage's spell is never locked.
        button.SetLocked(false);
        // The tile sits on the squad's card now, so the badge that used to point at it is off.
        if(badgeText != null) badgeText.transform.parent.gameObject.SetActive(false);

        for(int i = 0; i < pips.Length; i++)
            if(pips[i] != null) pips[i].gameObject.SetActive(i < maxCharges);
        SetCharges(info.MaxCharges);
    }

    public void SetCharges(int value)
    {
        if(value == charges) return;
        charges = value;
        for(int i = 0; i < pips.Length; i++)
            if(pips[i] != null) pips[i].color = i < value ? pipOnColor : pipOffColor;
        button.RefreshTooltip(StatBlock(value));
    }

    public void RenderCooldown(float remainingFraction01, bool onCooldown) => button.RenderCooldown(remainingFraction01, onCooldown);
    public void SetSelected(bool selected) => button.SetSelected(selected);
    public void SetPending(bool pending) => button.SetPending(pending);
    public void SetMenuOpen(bool open) => button.SetMenuOpen(open);

    // The lines the hotbar's mana-cost line stands in for: who casts it, what it has left, how far.
    private string StatBlock(int chargesLeft)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        return string.Join("\n",
            string.Format(loc.GetText("MageTileCaster"), loc.GetText(unitName.ToString()), cardNumber),
            string.Format(loc.GetText("MageTileCharges"), chargesLeft, maxCharges),
            string.Format(loc.GetText("MageTileRange"), Mathf.RoundToInt(range)),
            string.Format(loc.GetText("MageTileCooldown"), Mathf.RoundToInt(cooldown)),
            string.Format(loc.GetText("MageTileTargets"), SpellManager.ValidTargetsLabel(spell)));
    }
}
}
