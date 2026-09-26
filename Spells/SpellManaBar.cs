using System.Collections.Generic;
using Memori.Localization;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
/// <summary>
/// Battle HUD readout for the per-battle mana pool: an orb that drains top to bottom with the
/// remaining mana as one number, and a two-row strip with one gem per point. The mana a hovered or
/// armed spell would spend pulses on the strip and shows as a pale band on the orb. Pure View: it only
/// renders what SpellManager reports.
///
/// Takes a direct scene reference to SpellManager rather than going through BattleManager.Instance,
/// because a singleton getter that auto-creates on miss would fabricate a phantom manager if this
/// ran before the battle scene finished loading.
/// </summary>
public class SpellManaBar : MonoBehaviour
{
    [SerializeField] private SpellManager spellManager;
    // Hidden while the pool is unavailable - spells disabled in this build, or LoadSpellManager has
    // not run yet. Assign a CHILD wrapper holding the visuals; leaving it empty falls back to this
    // object, which then deactivates itself. That still recovers (the event subscription outlives it
    // and reactivates on the next Refresh) but only because this object starts active in the scene.
    // An object saved inactive never runs Start, never subscribes, and stays dark forever.
    [SerializeField] private GameObject root;

    [Header("Orb")]
    // Both are Filled / Vertical / Bottom images, so a lower fillAmount drains the orb from the top.
    [SerializeField] private Image orbLiquid;
    [SerializeField] private Image orbPreviewBand;
    [SerializeField] private TMP_Text manaValueText;

    [Header("Gem strip")]
    // The strip's frame; hidden with the gems when the pool is too large to draw.
    [SerializeField] private GameObject gemStrip;
    // Holds a GridLayoutGroup. The template is an inactive child that every gem is cloned from.
    [SerializeField] private GridLayoutGroup gemGrid;
    [SerializeField] private Image gemTemplate;
    [SerializeField] private float gemCellSize = 11f;
    // Cells shrink below gemCellSize once a large pool (stacked Mana Draughts) would outgrow this width.
    [SerializeField] private float gemStripMaxWidth = 214f;
    [SerializeField] private float gemPulseSeconds = 0.9f;
    // Past this the strip hides and the orb number carries the count alone (spell test mode grants 999).
    [SerializeField] private int maxGemsShown = 40;

    [Header("Tooltip (optional)")]
    [SerializeField] private MemoriTooltipTrigger tooltipTrigger;

    // 0 disables the animation and snaps instead. Unscaled because Time.timeScale is owned by the
    // battle scene and is 0 while paused, where this should still settle.
    [SerializeField] private float fillAnimationDuration = 0.25f;

    private const int GEM_ROWS = 2;

    private readonly List<Image> gems = new();
    private int remaining;
    private int max;
    private int preview;

    private float displayedFraction;
    private float targetFraction;
    private float animationVelocity;

    private bool subscribed;

    private void Start()
    {
        if(!ReferencesAssigned()) return;

        gemTemplate.gameObject.SetActive(false);
        Subscribe();

        // Pulled as well as subscribed. LoadSpellManager fires OnManaChanged once when it grants the
        // pool, but whether that happens before or after this Start depends on scene-load ordering,
        // so the bar seeds itself from the live values rather than relying on catching that event.
        preview = spellManager.ManaPreview;
        Refresh(spellManager.ManaRemaining, spellManager.ManaMax);
        displayedFraction = targetFraction;
        ApplyOrb();

        if(tooltipTrigger != null)
        {
            tooltipTrigger.SetUpToolTip(
                LocalizationManager.Instance.GetText("SpellManaLabel"),
                LocalizationManager.Instance.GetText("SpellManaTooltipDesc"));
        }
    }

    private bool ReferencesAssigned()
    {
        if(spellManager != null && orbLiquid != null && orbPreviewBand != null && gemGrid != null && gemTemplate != null) return true;

        Debug.LogError($"SpellManaBar on '{name}' is missing a required reference and will not render. " +
            $"spellManager={spellManager != null}, orbLiquid={orbLiquid != null}, orbPreviewBand={orbPreviewBand != null}, " +
            $"gemGrid={gemGrid != null}, gemTemplate={gemTemplate != null}", this);
        return false;
    }

    private void Subscribe()
    {
        if(subscribed) return;
        spellManager.OnManaChanged += Refresh;
        spellManager.OnManaPreviewChanged += OnPreviewChanged;
        subscribed = true;
    }

    private void Update()
    {
        if(preview > 0) PaintGems();

        if(fillAnimationDuration <= 0f || Mathf.Approximately(displayedFraction, targetFraction)) return;

        displayedFraction = Mathf.SmoothDamp(displayedFraction, targetFraction,
            ref animationVelocity, fillAnimationDuration, Mathf.Infinity, Time.unscaledDeltaTime);
        ApplyOrb();
    }

    private void Refresh(int _remaining, int _max)
    {
        // max is 0 before the pool is granted, and stays 0 in a build without the SPELLS define,
        // where SpellManager never loads. Either way there is nothing meaningful to show.
        GameObject target = root != null ? root : gameObject;
        bool available = _max > 0;
        if(target.activeSelf != available) target.SetActive(available);
        if(!available) return;

        if(_max != max) BuildGems(_max);
        remaining = _remaining;
        max = _max;

        targetFraction = Mathf.Clamp01((float)remaining / max);
        if(fillAnimationDuration <= 0f) displayedFraction = targetFraction;
        ApplyOrb();
        PaintGems();

        if(manaValueText != null) manaValueText.text = remaining.ToString();
    }

    private void OnPreviewChanged(int _preview)
    {
        preview = _preview;
        ApplyOrb();
        PaintGems();
    }

    private void BuildGems(int count)
    {
        bool showStrip = count <= maxGemsShown;
        (gemStrip != null ? gemStrip : gemGrid.gameObject).SetActive(showStrip);
        if(!showStrip) count = 0;

        while(gems.Count < count)
        {
            Image gem = Instantiate(gemTemplate, gemGrid.transform);
            gem.name = $"Gem {gems.Count + 1}";
            gems.Add(gem);
        }
        for(int i = 0; i < gems.Count; i++) gems[i].gameObject.SetActive(i < count);

        int columns = Mathf.Max(1, Mathf.CeilToInt(count / (float)GEM_ROWS));
        float fit = (gemStripMaxWidth - (columns - 1) * gemGrid.spacing.x) / columns;
        float cell = Mathf.Min(gemCellSize, fit);
        gemGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gemGrid.constraintCount = columns;
        gemGrid.cellSize = new Vector2(cell, cell);
    }

    private void ApplyOrb()
    {
        int after = Mathf.Max(0, remaining - preview);
        float afterFraction = max > 0 ? (float)after / max : 0f;
        orbPreviewBand.fillAmount = displayedFraction;
        orbLiquid.fillAmount = Mathf.Min(displayedFraction, afterFraction);
    }

    // Gems fill row by row, so spending empties the end of the bottom row first.
    private void PaintGems()
    {
        int after = Mathf.Max(0, remaining - preview);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / gemPulseSeconds));
        Color previewColor = Color.Lerp(ColorData.ManaFull, ColorData.ManaPreview, pulse);

        for(int i = 0; i < max && i < gems.Count; i++)
        {
            gems[i].color = i < after ? ColorData.ManaFull
                          : i < remaining ? previewColor
                          : ColorData.ManaEmpty;
        }
    }

    private void OnDestroy()
    {
        if(subscribed && spellManager != null) spellManager.OnManaChanged -= Refresh;
        if(subscribed && spellManager != null) spellManager.OnManaPreviewChanged -= OnPreviewChanged;
    }
}
}
