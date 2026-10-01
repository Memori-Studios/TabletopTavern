using System.Collections;
using Memori.Audio;
using Memori.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.Spells
{
/// <summary>
/// The spell wheel's look: a brass seal with the three spells on diamond mounts, Cancel below, a needle
/// toward the lit choice and its name on a ribbon. SpellManager decides what it shows and when.
/// </summary>
public class SpellWheelView : MonoBehaviour
{
    public const int SlotCount = 3;
    // Above the squad cards (2) and their counts (101), below Settings (105).
    private const int SortingOrder = 103;
    private const float OpenTime = 0.12f;
    private const float CloseTime = 0.08f;
    private const float CancelLitScale = 1.1f;
    private static readonly Color Gold = new Color32(233, 192, 106, 255);
    private static readonly Color CancelRed = new Color32(227, 105, 94, 255);
    private static readonly Color CancelLight = new Color32(255, 220, 216, 255);
    private static readonly Color CancelFill = new Color32(42, 22, 24, 255);
    private static readonly Color CancelLitFill = new Color32(74, 39, 42, 255);
    private static readonly Color CancelLabel = new Color32(240, 165, 156, 255);

    [SerializeField] private Canvas canvas;
    // Placed at the press point and scaled from 0 to open.
    [SerializeField] private RectTransform wheel;
    // Left, up, right: hotbar slots 1, 2 and 3.
    [SerializeField] private SpellWheelSlot[] slots;
    [Header("Cancel")]
    [SerializeField] private RectTransform cancel;
    [SerializeField] private Image cancelGlow;
    [SerializeField] private Image cancelEdge;
    [SerializeField] private Image cancelFill;
    [SerializeField] private Image cancelIcon;
    [Header("Pointer")]
    [SerializeField] private RectTransform needle;
    [SerializeField] private GameObject ribbon;
    [SerializeField] private TMP_Text ribbonText;
    [Header("Size")]
    // Canvas units. The radius keeps the whole seal on screen; the hub is the dead zone.
    [SerializeField] private float radius = 124f;
    [SerializeField] private float hubRadius = 28f;

    private Coroutine scaleRoutine;

    // The battle canvas above this one; the wheel's own canvas is switched off while hidden.
    private Canvas RootCanvas
    {
        get
        {
            Canvas above = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            return above != null ? above.rootCanvas : canvas;
        }
    }
    public float RadiusPixels => radius * RootCanvas.scaleFactor;
    public float HubRadiusPixels => hubRadius * RootCanvas.scaleFactor;

    private void Awake()
    {
        wheel.localScale = Vector3.zero;
        canvas.enabled = false;
    }

    public void SetSlot(int index, SpellData spell, bool affordable, bool locked)
    {
        if(index < 0 || index >= slots.Length || slots[index] == null) return;
        slots[index].Show(spell, affordable, locked);
    }

    public void SetCooldown(int index, float remainingFraction01, float secondsLeft)
    {
        if(index < 0 || index >= slots.Length || slots[index] == null) return;
        slots[index].SetCooldown(remainingFraction01, secondsLeft);
    }

    public void Show(Vector2 screenPoint)
    {
        Canvas root = RootCanvas;
        Camera eye = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)wheel.parent, screenPoint, eye, out Vector2 local);
        wheel.localPosition = local;
        SetHighlight(WheelDirection.Centre, null);
        canvas.enabled = true;
        // A nested canvas saved by the builder has no sorting override, and a switched-off canvas drops one.
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;
        ScaleTo(1f, OpenTime);
        IAudioRequester.Instance.PlaySFX(SFXData.TinyClick);
    }

    public void Hide()
    {
        ScaleTo(0f, CloseTime);
    }

    public void SetHighlight(WheelDirection direction, string label)
    {
        int litSlot = SpellWheelMath.SlotOf(direction);
        for(int i = 0; i < slots.Length; i++)
            if(slots[i] != null) slots[i].SetLit(i == litSlot);

        bool cancelLit = direction == WheelDirection.Down;
        cancelGlow.enabled = cancelLit;
        cancelEdge.color = cancelLit ? CancelLight : CancelRed;
        cancelFill.color = cancelLit ? CancelLitFill : CancelFill;
        cancelIcon.color = cancelLit ? CancelLight : CancelRed;
        cancel.localScale = Vector3.one * (cancelLit ? CancelLitScale : 1f);

        needle.gameObject.SetActive(direction != WheelDirection.Centre);
        needle.localEulerAngles = new Vector3(0f, 0f, NeedleAngle(direction));

        bool named = !string.IsNullOrEmpty(label);
        ribbon.SetActive(named);
        if(named)
        {
            ribbonText.text = label;
            ribbonText.color = cancelLit ? CancelLabel : Gold;
        }
        if(direction != WheelDirection.Centre && canvas.enabled) IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
    }

    private static float NeedleAngle(WheelDirection direction) => direction switch
    {
        WheelDirection.Left => 90f,
        WheelDirection.Down => 180f,
        WheelDirection.Right => -90f,
        _ => 0f
    };

    private void ScaleTo(float target, float duration)
    {
        if(scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleRoutine(target, duration));
    }

    // Unscaled, so the wheel opens while the battle is paused.
    private IEnumerator ScaleRoutine(float target, float duration)
    {
        float from = wheel.localScale.x;
        for(float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            wheel.localScale = Vector3.one * Mathf.LerpUnclamped(from, target, UIJuice.EaseOutCubic(t / duration));
            yield return null;
        }
        wheel.localScale = Vector3.one * target;
        if(target <= 0f) canvas.enabled = false;
        scaleRoutine = null;
    }
}
}
