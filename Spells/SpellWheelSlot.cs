using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
/// <summary>
/// One spell on the spell wheel: a diamond mount with the spell's icon, mana gem and cooldown sweep.
/// The faction colour is the spell's identity; lit, the mount glows and turns light gold.
/// </summary>
public class SpellWheelSlot : MonoBehaviour
{
    [SerializeField] private Image glow;
    [SerializeField] private Image edge;
    [SerializeField] private Image fill;
    [SerializeField] private Image cooldown;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private Image icon;
    [SerializeField] private GameObject costGem;
    [SerializeField] private Image costGemFill;
    [SerializeField] private TMP_Text costText;
    // Optional. The shared locked-slot art, shown on a slot the player has not unlocked with Renown.
    [SerializeField] private GameObject lockedBlocker;

    private const float LitScale = 1.1f;
    // Unaffordable reads on brightness, never hue, the same as the hotbar tile.
    private const float UnaffordableIconAlpha = 0.3f;
    private const float UnaffordableEdgeAlpha = 0.35f;
    private const float LitWash = 0.18f;
    private static readonly Color LitEdge = new Color32(243, 227, 166, 255);
    private static readonly Color Well = new Color32(22, 32, 35, 255);

    private Color factionColor = Color.white;
    private bool hasSpell;
    private bool affordable = true;
    private bool lit;
    private int shownSeconds = -1;

    public void Show(SpellData spell, bool canAfford, bool locked)
    {
        hasSpell = spell != null;
        affordable = canAfford;
        factionColor = hasSpell ? ColorData.GetRaceDisplayColor(spell.Race) : Color.white;
        icon.enabled = hasSpell;
        if(hasSpell) icon.sprite = spell.SpellSprite;
        // Mage spells spend charges, not mana, and carry cost 0: no gem for them.
        bool showCost = hasSpell && spell.SpellManaCost > 0;
        costGem.SetActive(showCost);
        if(showCost)
        {
            costText.text = spell.SpellManaCost.ToString();
            costGemFill.color = affordable ? ColorData.ManaCostGem : ColorData.ManaUnaffordable;
        }
        if(lockedBlocker != null) lockedBlocker.SetActive(locked);
        SetCooldown(0f, 0f);
        Refresh();
    }

    public void SetCooldown(float remainingFraction01, float secondsLeft)
    {
        cooldown.fillAmount = Mathf.Clamp01(remainingFraction01);
        int seconds = remainingFraction01 > 0f ? Mathf.CeilToInt(secondsLeft) : 0;
        if(seconds == shownSeconds) return;
        shownSeconds = seconds;
        cooldownText.text = seconds > 0 ? seconds.ToString() : "";
    }

    public void SetLit(bool on)
    {
        if(lit == on) return;
        lit = on;
        Refresh();
    }

    private void Refresh()
    {
        bool showLit = lit && hasSpell;
        Color iconColour = factionColor;
        if(!affordable) iconColour.a *= UnaffordableIconAlpha;
        icon.color = iconColour;

        Color edgeColour = factionColor;
        if(!affordable) edgeColour.a *= UnaffordableEdgeAlpha;
        edge.color = !hasSpell ? ColorData.SpellFrameRest : showLit ? LitEdge : edgeColour;
        fill.color = showLit ? Color.Lerp(Well, factionColor, LitWash) : Well;

        glow.enabled = showLit;
        glow.color = factionColor;
        transform.localScale = Vector3.one * (showLit ? LitScale : 1f);
    }
}
}
