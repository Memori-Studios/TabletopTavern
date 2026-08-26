using Memori.Localization;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
/// <summary>
/// Battle HUD readout for the per-battle mana pool. Pure View: it never decides anything, it only
/// renders what SpellManager reports.
///
/// Takes a direct scene reference to SpellManager rather than going through BattleManager.Instance,
/// because a singleton getter that auto-creates on miss would fabricate a phantom manager if this
/// ran before the battle scene finished loading.
/// </summary>
public class SpellManaBar : MonoBehaviour
{
    [SerializeField] private SpellManager spellManager;

    [Header("Bar")]
    [SerializeField] private Slider slider;
    // Hidden while the pool is unavailable - spells disabled in this build, or LoadSpellManager has
    // not run yet. Assign a CHILD wrapper holding the visuals; leaving it empty falls back to this
    // object, which then deactivates itself. That still recovers (the event subscription outlives it
    // and reactivates on the next Refresh) but only because this object starts active in the scene.
    // An object saved inactive never runs Start, never subscribes, and stays dark forever.
    [SerializeField] private GameObject root;

    [Header("Text (optional)")]
    [SerializeField] private TMP_Text manaValueText;
    [SerializeField] private TMP_Text manaLabelText;

    [Header("Tooltip (optional)")]
    [SerializeField] private MemoriTooltipTrigger tooltipTrigger;

    // 0 disables the animation and snaps instead. Unscaled because Time.timeScale is owned by the
    // battle scene and is 0 while paused, where this should still settle.
    [SerializeField] private float fillAnimationDuration = 0.25f;

    private float displayedFraction;
    private float targetFraction;
    private float animationVelocity;

    private bool subscribed;

    private void Start()
    {
        if(!ReferencesAssigned()) return;

        Subscribe();

        // Pulled as well as subscribed. LoadSpellManager fires OnManaChanged once when it grants the
        // pool, but whether that happens before or after this Start depends on scene-load ordering,
        // so the bar seeds itself from the live values rather than relying on catching that event.
        Refresh(spellManager.ManaRemaining, spellManager.ManaMax);
        displayedFraction = targetFraction;
        ApplyFraction(displayedFraction);

        if(manaLabelText != null) manaLabelText.text = LocalizationManager.Instance.GetText("SpellManaLabel");

        if(tooltipTrigger != null)
        {
            tooltipTrigger.SetUpToolTip(
                LocalizationManager.Instance.GetText("SpellManaLabel"),
                LocalizationManager.Instance.GetText("SpellManaTooltipDesc"));
        }
    }

    private bool ReferencesAssigned()
    {
        if(spellManager != null && slider != null) return true;

        Debug.LogError($"SpellManaBar on '{name}' is missing a required reference and will not render. " +
            $"spellManager={spellManager != null}, slider={slider != null}", this);
        return false;
    }

    private void Subscribe()
    {
        if(subscribed) return;
        spellManager.OnManaChanged += Refresh;
        subscribed = true;
    }

    private void Update()
    {
        if(fillAnimationDuration <= 0f || Mathf.Approximately(displayedFraction, targetFraction)) return;

        displayedFraction = Mathf.SmoothDamp(displayedFraction, targetFraction,
            ref animationVelocity, fillAnimationDuration, Mathf.Infinity, Time.unscaledDeltaTime);
        ApplyFraction(displayedFraction);
    }

    private void Refresh(int remaining, int max)
    {
        // max is 0 before the pool is granted, and stays 0 in a build without the SPELLS define,
        // where SpellManager never loads. Either way there is nothing meaningful to show.
        GameObject target = root != null ? root : gameObject;
        bool available = max > 0;
        if(target.activeSelf != available) target.SetActive(available);
        if(!available) return;

        targetFraction = Mathf.Clamp01((float)remaining / max);
        if(fillAnimationDuration <= 0f)
        {
            displayedFraction = targetFraction;
            ApplyFraction(displayedFraction);
        }

        if(manaValueText != null) manaValueText.text = $"{remaining} / {max}";
    }

    private void ApplyFraction(float fraction01)
    {
        slider.value = fraction01;
    }

    private void OnDestroy()
    {
        if(subscribed && spellManager != null) spellManager.OnManaChanged -= Refresh;
    }
}
}
