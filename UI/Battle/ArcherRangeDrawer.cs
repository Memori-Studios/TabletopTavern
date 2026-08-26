using UnityEngine;
using Shapes;
using System.Collections;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Transforms;
using TJ.Shapes;
using Memori.Input;

public class ArcherRangeDrawer : MonoBehaviour
{
    [SerializeField] private Line leftLine, rightLine;
    [SerializeField] private Disc arc, arc2;
    [SerializeField] private Color playerColor, enemyColor;
    [SerializeField] private float _fadeDuration = 0.11f;

    float range;
    ShapesBloom leftLineBloom, rightLineBloom, arcBloom, arc2Bloom;
    Entity cachedEntity;
    bool isSetUp = false, cachedOn;
    bool isOn = false;
    bool _toggledOn = false;
    int squadId;
    bool inMeleeMode = false;
    bool _isCaster = false;

    Coroutine _fadeRoutine;
    Color _arcTargetColor, _arc2TargetOuter, _lineTargetColor;

    private void Awake()
    {
        leftLineBloom = leftLine.GetComponent<ShapesBloom>();
        rightLineBloom = rightLine.GetComponent<ShapesBloom>();
        arcBloom = arc.GetComponent<ShapesBloom>();
        arc2Bloom = arc2.GetComponent<ShapesBloom>();
    }

    private void Start()
    {
        InputHandler.Instance.OnShowUnitMovement += ToggleRange;
    }

    private void ToggleRange()
    {
        _toggledOn = !_toggledOn;
        if (_toggledOn) TurnOn();
        else TurnOff();
    }

    private void OnDestroy()
    {
        if (InputHandler.HasInstance)
            InputHandler.Instance.OnShowUnitMovement -= ToggleRange;
    }

    public void Update()
    {
        if (!isSetUp) return;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!entityManager.Exists(cachedEntity))
        {
            BattleManager.Instance.SquadManager.RemoveArcherRangeDrawer(squadId);
            return;
        }
        SquadMovementComponent _squadEntity = entityManager.GetComponentData<SquadMovementComponent>(cachedEntity);
        transform.SetPositionAndRotation(_squadEntity.SquadCenter, _squadEntity.SquadRotation);
    }

    public void SetUp(SquadEntity _squadEntity)
    {
        cachedEntity = _squadEntity.SelfEntity;
        squadId = _squadEntity.SquadId;
        Color teamColor = squadId > 0 ? playerColor : enemyColor;
        leftLineBloom.SetColor(teamColor);
        rightLineBloom.SetColor(teamColor);
        arcBloom.SetColor(teamColor);
        // arc2Bloom.SetColor(teamColor);

        Recalculate();

        leftLineBloom.Bloom();
        rightLineBloom.Bloom();
        arcBloom.Bloom();
        arc2Bloom.Bloom();

        // Cache target colors after bloom sets them, then start invisible
        _lineTargetColor = leftLine.Color;
        _arcTargetColor  = arc.Color;
        _arc2TargetOuter = arc2.ColorOuter;

        leftLine.Color  = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, 0f);
        rightLine.Color = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, 0f);
        arc.Color       = new Color(_arcTargetColor.r,  _arcTargetColor.g,  _arcTargetColor.b,  0f);
        arc2.ColorInner = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);
        arc2.ColorOuter = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);

        isSetUp = true;
    }

    public void Recalculate()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        if (!entityManager.Exists(cachedEntity))
        {
            Debug.LogError($"Entity {cachedEntity} does not exist anymore. Should remove ArcherRangeDrawer.");
            return;
        }
        if (!entityManager.HasComponent<SquadMovementComponent>(cachedEntity))
        {
            Debug.LogError($"Entity {cachedEntity} does not have SquadMovementComponent.");
            return;
        }
        SquadMovementComponent squadMovementComponent = entityManager.GetComponentData<SquadMovementComponent>(cachedEntity);
        int2 widthAndDepth = squadMovementComponent.SquadWidthAndDepth;
        float width  = widthAndDepth.x * 0.75f;
        float height = widthAndDepth.y * 0.75f;

        if (entityManager.HasComponent<RangedSquad>(cachedEntity))
            range = entityManager.GetComponentData<RangedSquad>(cachedEntity).AttackRange;
        // A mage carries MageSquad instead of RangedSquad, so without this its ring stayed at the
        // field default of 0 and drew nothing. MageSquadRangeSystem keeps AttackRange in sync with
        // the unit's MageCast.Range, exactly as RangedSquadRangeSystem does for archers.
        else if (entityManager.HasComponent<MageSquad>(cachedEntity))
        {
            range = entityManager.GetComponentData<MageSquad>(cachedEntity).AttackRange;
            _isCaster = true;
        }

        arc.Radius     = range;
        arc2.Radius    = range - 3.75f;
        arc2.Thickness = 7.5f;

        if (_isCaster) ApplyCasterShape();

        Vector3 center = Vector3.zero;

        static Vector3 CalculateArcPoint(Vector3 center, float range, float angleInDegrees)
        {
            float angleInRadians = Mathf.Deg2Rad * angleInDegrees;
            float x = center.x + range * Mathf.Cos(angleInRadians);
            float z = center.z + range * Mathf.Sin(angleInRadians);
            return new Vector3(x, center.y, z);
        }

        Vector3 startPoint = CalculateArcPoint(center, range, 45f);
        Vector3 endPoint   = CalculateArcPoint(center, range, 135f);

        leftLine.Start  = center - (width * Vector3.right) + (height * Vector3.forward);
        leftLine.End    = endPoint;
        rightLine.Start = center + (width * Vector3.right) + (height * Vector3.forward);
        rightLine.End   = startPoint;
    }

    // An archer faces its target, and the prefab authors that honestly: both discs are DiscType.Arc
    // spanning 45 to 135 degrees, with two lines running out from the squad's front corners to the
    // arc ends. A caster has no such cone. MageSquadFindTargetSystem and MageCastSystem both gate on
    // math.distance(SquadCenter, targetCenter) > AttackRange with no facing term anywhere, so a mage
    // casts in every direction and the inherited arc was drawing a limit that does not exist - the
    // player would read three quarters of the real threat range as safe.
    //
    // A full ring is the truthful shape. The blast radius is deliberately NOT drawn here: the spell
    // lands on the target, not on the caster, so a second ring centred on the mage would read as a
    // minimum range or a self-aura and be a worse lie than the one being fixed.
    //
    // Only this branch writes Type and the line enables, so the archer presentation stays exactly as
    // authored and cannot regress. Every squad instantiates its own drawer, so mutating this instance
    // affects nothing else. Disabling a ShapeRenderer hides it (OnDisable clears the MeshRenderer),
    // and the Fade coroutine writing Color to a disabled line is a harmless property set.
    private void ApplyCasterShape()
    {
        arc.Type  = DiscType.Ring;
        arc2.Type = DiscType.Ring;

        leftLine.enabled  = false;
        rightLine.enabled = false;
    }

    public void TurnOn()
    {
        if (!Cursor.visible) return;
        if (inMeleeMode) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(1f));
        isOn = true;
    }

    public void TurnOff()
    {
        if (_toggledOn) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(0f));
        isOn = false;
    }

    private IEnumerator Fade(float targetAlpha)
    {
        float startAlphaLine    = leftLine.Color.a;
        float startAlphaArc     = arc.Color.a;
        float startAlphaArc2Out = arc2.ColorOuter.a;
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);

            float lineA = Mathf.Lerp(startAlphaLine, _lineTargetColor.a * targetAlpha, t);
            leftLine.Color  = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, lineA);
            rightLine.Color = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, lineA);

            arc.Color = new Color(_arcTargetColor.r, _arcTargetColor.g, _arcTargetColor.b,
                Mathf.Lerp(startAlphaArc, _arcTargetColor.a * targetAlpha, t));

            float outerA = Mathf.Lerp(startAlphaArc2Out, _arc2TargetOuter.a * targetAlpha / 2f, t);
            arc2.ColorInner = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);
            arc2.ColorOuter = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, outerA);

            yield return null;
        }
    }

    public void ShowAllRanges(bool _start)
    {
        if (_start)
        {
            cachedOn = isOn;
            TurnOn();
        }
        else
        {
            if (!cachedOn && !_toggledOn) TurnOff();
        }
    }

    public void SwitchToMelee(bool _toMelee)
    {
        // A caster has no melee mode to switch into - it never carries RangedSquad, so the ECS half
        // of this toggle is inert for it. The button is shown for any selection containing a shooter
        // though, and SquadManager.SetMeleeMode skips only UnitType.Melee, so a mage caught in a
        // mixed selection would latch inMeleeMode and TurnOn() would early-return from then on,
        // hiding its range ring until the player happened to toggle back.
        if (_isCaster) return;

        inMeleeMode = _toMelee;
        if (inMeleeMode) TurnOff();
        else TurnOn();
    }
}
