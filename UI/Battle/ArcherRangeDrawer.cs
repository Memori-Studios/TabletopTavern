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
    // One Quad per side line, coloured on the line edge and clear one BandWidth into the cone. That
    // is the same inner/outer fade arc2 draws along the arc, so the whole cone edge glows the same way.
    [SerializeField] private Quad leftBand, rightBand;
    [SerializeField] private Color playerColor, enemyColor;
    // A caster's ring is a different shape from an archer's cone (see ApplyCasterShape), and reusing
    // the archer's gold made the two read as the same kind of threat. Blue says "spell range" at a
    // glance. Still split by team, so an enemy caster does not borrow the player's hue - the drawer
    // is created for enemy squads too, unlike AttackArrowDrawer which destroys itself for them.
    [SerializeField] private Color casterPlayerColor = new Color(0.235f, 0.647f, 0.960f, 1f);
    [SerializeField] private Color casterEnemyColor = new Color(0.612f, 0.325f, 0.941f, 1f);
    [SerializeField] private float _fadeDuration = 0.11f;

    // The soft band inside the cone edge is the team colour with every channel lifted toward white
    // by this much, hue kept. On the player gold that gives the pale yellow the prefab used to author.
    const float BandLift = 0.26f;

    // Depth of the soft glow inside the cone edge. arc2 and both side bands share it so they meet flush.
    const float BandWidth = 4f;
    // The cone starts this many model-spacings outside the formation's front corners, so the lines
    // clear the outermost models instead of cutting through them.
    const float CornerMarginInSpreads = 0.5f;
    // Half-angle of the cone. The prefab authors 45 for the discs, but the value here wins: the
    // discs and the line ends are all set from it so the arc never sticks out past the lines.
    const float ConeHalfAngleDeg = 40f;

    float range;
    float _spread;
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

        // Resolved from the unit type rather than from MageSquad, which is what Recalculate reads.
        // Two reasons: Recalculate runs after the colour is chosen just below, and
        // SquadRanOutOfAmmoSystem strips MageSquad once the last charge is spent, so the component
        // is not a durable answer to "is this a caster" while the type is.
        _isCaster = TabletopTavernConstants.Casts(TJ.TabletopTavernData.Instance.GetUnitTypeFromUnitName(_squadEntity.UnitName));
        _spread = TabletopTavernConstants.GetSpread(TJ.TabletopTavernData.Instance.GetUnitSizeFromUnitName(_squadEntity.UnitName));

        Color teamColor = _isCaster
            ? (squadId > 0 ? casterPlayerColor : casterEnemyColor)
            : (squadId > 0 ? playerColor : enemyColor);
        leftLineBloom.SetColor(teamColor);
        rightLineBloom.SetColor(teamColor);
        arcBloom.SetColor(teamColor);

        arc2Bloom.SetColor(new Color(
            Mathf.Min(1f, teamColor.r + BandLift),
            Mathf.Min(1f, teamColor.g + BandLift),
            Mathf.Min(1f, teamColor.b + BandLift), 1f));

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
        SetBandAlpha(0f);

        isSetUp = true;
    }

    // The bands borrow arc2's bloomed colour instead of carrying a ShapesBloom of their own, so the
    // pale yellow (or the caster blue) is the same value by construction. ColorRight is the edge
    // inside the cone and stays clear; only the line-side edge fades.
    private void SetBandAlpha(float alpha)
    {
        Color edge  = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, alpha);
        Color clear = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);
        leftBand.ColorLeft   = edge;
        leftBand.ColorRight  = clear;
        rightBand.ColorLeft  = edge;
        rightBand.ColorRight = clear;
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
        // Models sit on a grid of _spread, so the formation's half extent is count * spread / 2. This
        // used to be a hardcoded 0.75 per model, which was half of an older infantry spread and drew
        // the cone narrower than the squad once the spacing changed.
        int2 widthAndDepth = squadMovementComponent.SquadWidthAndDepth;
        float width  = (widthAndDepth.x + CornerMarginInSpreads * 2f) * _spread * 0.5f;
        float height = widthAndDepth.y * _spread * 0.5f;

        if (entityManager.HasComponent<RangedSquad>(cachedEntity))
            range = entityManager.GetComponentData<RangedSquad>(cachedEntity).AttackRange;
        // A mage carries MageSquad instead of RangedSquad, so without this its ring stayed at the
        // field default of 0 and drew nothing. MageSquadRangeSystem keeps AttackRange in sync with
        // the unit's MageCast.Range, exactly as RangedSquadRangeSystem does for archers.
        // _isCaster is deliberately not set here - SetUp resolves it before this ever runs.
        else if (entityManager.HasComponent<MageSquad>(cachedEntity))
            range = entityManager.GetComponentData<MageSquad>(cachedEntity).AttackRange;

        arc.Radius     = range;
        arc2.Radius    = range - BandWidth * 0.5f;
        arc2.Thickness = BandWidth;

        if (_isCaster) ApplyCasterShape();

        Vector3 center = Vector3.zero;

        static Vector3 CalculateArcPoint(Vector3 center, float range, float angleInDegrees)
        {
            float angleInRadians = Mathf.Deg2Rad * angleInDegrees;
            float x = center.x + range * Mathf.Cos(angleInRadians);
            float z = center.z + range * Mathf.Sin(angleInRadians);
            return new Vector3(x, center.y, z);
        }

        float startDeg = 90f - ConeHalfAngleDeg;
        float endDeg   = 90f + ConeHalfAngleDeg;
        arc.AngRadiansStart  = startDeg * Mathf.Deg2Rad;
        arc.AngRadiansEnd    = endDeg   * Mathf.Deg2Rad;
        arc2.AngRadiansStart = startDeg * Mathf.Deg2Rad;
        arc2.AngRadiansEnd   = endDeg   * Mathf.Deg2Rad;

        Vector3 startPoint = CalculateArcPoint(center, range, startDeg);
        Vector3 endPoint   = CalculateArcPoint(center, range, endDeg);

        leftLine.Start  = center - (width * Vector3.right) + (height * Vector3.forward);
        leftLine.End    = endPoint;
        rightLine.Start = center + (width * Vector3.right) + (height * Vector3.forward);
        rightLine.End   = startPoint;

        PlaceBand(leftBand,  leftLine.Start,  leftLine.End,  rightLine.Start);
        PlaceBand(rightBand, rightLine.Start, rightLine.End, leftLine.Start);
    }

    // A and B sit on the line, C and D are pushed BandWidth into the cone. The normal is flipped
    // toward the other line's start so both bands fall inside the cone whichever side they are on.
    private static void PlaceBand(Quad band, Vector3 start, Vector3 end, Vector3 otherLineStart)
    {
        Vector3 dir = (end - start).normalized;
        Vector3 n   = Vector3.Cross(dir, Vector3.up).normalized;
        if (Vector3.Dot(n, otherLineStart - start) < 0f) n = -n;

        band.A = start;
        band.B = end;
        band.C = end   + n * BandWidth;
        band.D = start + n * BandWidth;
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
        leftBand.enabled  = false;
        rightBand.enabled = false;
    }

    public void TurnOn()
    {
        if (!Cursor.visible) return;
        if (inMeleeMode) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(1f));
        isOn = true;
    }

    // The band inside the ring at full strength instead of its usual half, while a rail tile is hovered.
    private bool _highlighted;
    public void SetHighlighted(bool highlighted)
    {
        if (_highlighted == highlighted) return;
        _highlighted = highlighted;
        if (!isOn) return;
        // Re-run the fade-in: it lands on the band strength for the new state.
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(Fade(1f));
    }
    private float BandStrength => _highlighted ? 1f : 0.5f;

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

            float outerA = Mathf.Lerp(startAlphaArc2Out, _arc2TargetOuter.a * targetAlpha * BandStrength, t);
            arc2.ColorInner = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);
            arc2.ColorOuter = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, outerA);
            SetBandAlpha(outerA);

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
