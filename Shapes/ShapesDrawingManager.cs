using UnityEngine;
using Shapes;
using TJ.Spells;

namespace TJ.Shapes
{
public class ShapesDrawingManager : ImmediateModeShapeDrawer
{
    [SerializeField] private SpellManager _spellManager;

    [Header("Spell Radius Ring")]
    [SerializeField] [ColorUsage(true, true)] private Color _spellRingColor = new Color(0.25f, 0.65f, 1f, 1f);
    // HDR multiplier on the colour so the ring blooms (the old drawer got the same effect from alpha = 100); alpha is reserved for the fade.
    [SerializeField] private float _spellRingIntensity = 100f;
    [SerializeField] private float _spellRingThickness = 0.01f;
    // Seconds to fade in on a valid target and out on an invalid one or when cast mode ends.
    [SerializeField] private float _spellRingFadeSeconds = 0.15f;
    // Metres above the cast point. Depth-tested, so this must clear the grass tops while unit bodies still occlude it.
    [SerializeField] private float _groundOffset = 0.5f;

    [Header("Pentagram")]
    // Star radius as a fraction of the ring radius, so its points sit just inside the ring.
    [SerializeField] [Range(0.1f, 1f)] private float _pentagramRadiusFraction = 0.92f;
    [SerializeField] private float _pentagramThickness = 0.01f;
    [SerializeField] [ColorUsage(true, true)] private Color _pentagramColor = new Color(0.75f, 0.45f, 1f, 1f);
    [SerializeField] private float _pentagramIntensity = 12f;
    // Degrees per second, clockwise seen from above. Unscaled so it keeps turning while paused.
    [SerializeField] private float _pentagramRotationSpeed = 10f;

    [Header("Area Band")]
    // The same soft band the AOE Spell prefab's Area Disc draws: a radial gradient from clear at the
    // inner edge to _bandColor at the ring, over the outer _bandFraction of the radius, Lighten blend.
    [SerializeField] private Color _bandColor = new Color(0.25f, 0.65f, 1f, 1f);
    [SerializeField] [Range(0.05f, 1f)] private float _bandFraction = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float _bandAlpha = 1f;
    // Mirror band outside the ring: full at the ring, clear at radius * (1 + _outerBandFraction).
    [SerializeField] [Range(0f, 1f)] private float _outerBandFraction = 0.25f;

    [Header("Wisps")]
    // Faint noisy strands drawn over the ring and star so the lines look like drifting energy.
    // Each strand is a closed polyline whose points are pushed off the base line by animated Perlin noise.
    [SerializeField] [Range(0, 4)] private int _wispStrands = 2;
    [SerializeField] [Range(24, 256)] private int _wispSegments = 96;
    // Push distance as a fraction of the ring radius.
    [SerializeField] private float _wispAmplitude = 0.05f;
    [SerializeField] private float _wispNoiseScale = 2.5f;
    [SerializeField] private float _wispSpeed = 0.5f;
    [SerializeField] private float _wispThickness = 0.01f;
    [SerializeField] [Range(0f, 1f)] private float _wispAlpha = 0.5f;

    [Header("Prefab Overlays")]
    // Particle prefabs played at the cast point while a valid target is under the cursor, for comparison
    // with the Shapes drawing. Each is scaled so its native radius matches the spell radius.
    [SerializeField] private bool _useMagicCircle;
    [SerializeField] private GameObject _magicCirclePrefab;
    [SerializeField] private float _magicCircleNativeRadius = 2f;
    [SerializeField] private bool _useMagicZone;
    [SerializeField] private GameObject _magicZonePrefab;
    [SerializeField] private float _magicZoneNativeRadius = 2f;
    // The AOE Spell prefab's edge ring; the AOE scales its root by the radius directly, so native radius is 1.
    [SerializeField] private bool _useEdgeParticles;
    [SerializeField] private GameObject _edgeParticlesPrefab;

    private float _ringAlpha;

    [Header("Mage leash")]
    // From the armed mage to the cursor: caster blue while the point is in reach. Out of range it
    // splits where the mage would come into reach: a solid movement-green leg with an arrow head up
    // to that point, then dashed attack-red on to the target.
    [SerializeField] [ColorUsage(true, true)] private Color _leashInRangeColor = new Color(0.235f, 0.647f, 0.960f, 1f);
    // Width, red and green, glow, dashes and the head shape are read off the Attack Arrow prefab, so
    // the leash matches the arrow the order becomes and follows every tune of that prefab.
    private bool _arrowStyleChecked, _arrowStyleValid;
    private float _arrowThickness, _arrowLineBloom, _arrowHeadBloom, _arrowHeadRoundness;
    private float _arrowDashSize, _arrowDashSpacing;
    // Read at draw time so a Colorblind Mode change reaches a leash already on screen.
    private AttackArrowDrawer _arrowPrefab;
    private Vector3 _arrowHeadA, _arrowHeadB, _arrowHeadC;
    private float _leashAlpha;
    private Vector3 _leashStart, _leashEnd;
    private bool _leashOutOfRange;
    private bool _leashFriendly;
    private float _leashRange;
    // The cast point without the ground offset, for the particle overlays.
    private Vector3 _castPoint;
    private GameObject _magicCircle;
    private GameObject _magicZone;
    private GameObject _edgeParticles;
    private PolylinePath[] _ringWisps;
    private PolylinePath[] _starWisps;
    private readonly Vector2[] _starPoints = new Vector2[5];
    // Five points visited 0,2,4,1,3: a closed polyline through them is the star. Mesh-backed, so disposed once.
    private PolylinePath _pentagramPath;
    private bool _showStar;
    private static readonly int[] StarOrder = { 0, 2, 4, 1, 3 };
    // Latched while a valid target is under the cursor, so a fade-out happens in place instead of following the mouse.
    private Vector3 _ringPosition;
    private float _ringRadius;

    // Widths follow the Battlefield Markers setting; radii are gameplay ranges and do not.
    private static float MarkerScale => BattlefieldMarkerScale.Current;
    private void OnDestroy()
    {
        if(_pentagramPath != null) { _pentagramPath.Dispose(); _pentagramPath = null; }
        DisposeWisps(ref _ringWisps);
        DisposeWisps(ref _starWisps);
    }
    // Stepped here rather than in DrawShapes, which runs once per camera.
    private void Update()
    {
        bool show = BattleManager.Instance.CursorMode == CursorMode.CastSpell
                    && _spellManager.MouseReleased
                    && _spellManager.ValidSpellCastPoint;
        if(show) {
            _castPoint = _spellManager.SpellCursorOrigin;
            _ringPosition = _castPoint + Vector3.up * _groundOffset;
            _ringRadius = _spellManager.SelectedSpellRadius;
            _showStar = _spellManager.SelectedSpellShowsStar;
        }
        UpdateOverlay(ref _magicCircle, _magicCirclePrefab, _useMagicCircle && show, _ringRadius / Mathf.Max(_magicCircleNativeRadius, 0.001f));
        UpdateOverlay(ref _magicZone, _magicZonePrefab, _useMagicZone && show, _ringRadius / Mathf.Max(_magicZoneNativeRadius, 0.001f));
        UpdateOverlay(ref _edgeParticles, _edgeParticlesPrefab, _useEdgeParticles && show, _ringRadius);
        float target = show ? 1f : 0f;
        _ringAlpha = _spellRingFadeSeconds <= 0f ? target
            : Mathf.MoveTowards(_ringAlpha, target, Time.unscaledDeltaTime / _spellRingFadeSeconds);

        // The leash follows the cursor whether or not the point is valid: the question it answers
        // is "can it reach", which the player asks before they find a target.
        bool leash = BattleManager.Instance.CursorMode == CursorMode.CastSpell
                     && _spellManager.MouseReleased
                     && _spellManager.MageSpellArmed;
        if(leash) {
            _leashStart = _spellManager.ArmedMageCenter + Vector3.up * _groundOffset;
            _leashEnd = _spellManager.SpellCursorOrigin + Vector3.up * _groundOffset;
            _leashOutOfRange = _spellManager.ArmedMageOutOfRange;
            _leashFriendly = _spellManager.ArmedSpellTargetsFriends;
            _leashRange = _spellManager.ArmedMageRange;
        }
        float leashTarget = leash ? 1f : 0f;
        _leashAlpha = _spellRingFadeSeconds <= 0f ? leashTarget
            : Mathf.MoveTowards(_leashAlpha, leashTarget, Time.unscaledDeltaTime / _spellRingFadeSeconds);
    }

    public override void DrawShapes( Camera cam )
    {
        if(_ringAlpha <= 0f && _leashAlpha <= 0f) return;

        // Before the transparent pass: the squad flags do not write depth, so they must draw after this to sit on top.
        using( Draw.Command( cam, UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingSkybox ) ){
            // The leash shows through units and terrain, as the Attack Arrow prefab does (ZTest Always).
            Draw.ZTest = UnityEngine.Rendering.CompareFunction.Always;
            if(_leashAlpha > 0f) DrawLeash();
            if(_ringAlpha <= 0f) return;
            // Normal depth test: units in front hide the drawing, as a ground decal should.
            Draw.ZTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            DrawAreaBand();

            Color ringColor = _spellRingColor * _spellRingIntensity;
            ringColor.a = _ringAlpha;

            Draw.Ring(
                _ringPosition,
                Quaternion.Euler(90, 0, 0),
                _ringRadius,
                _spellRingThickness * MarkerScale,
                ringColor
            );
            Color starColor = _pentagramColor * _pentagramIntensity;
            starColor.a = _ringAlpha;
            if(_showStar) DrawPentagram(starColor);
            DrawWisps(ringColor, starColor);
        }
    }
    private bool LoadArrowStyle()
    {
        if(_arrowStyleChecked) return _arrowStyleValid;
        _arrowStyleChecked = true;
        AttackArrowDrawer arrow = BattleManager.Instance.UIManager.AttackArrowPrefab;
        if(arrow == null || arrow.MovementLine == null || arrow.PointTriangle == null) {
            Debug.LogError("ShapesDrawingManager: UIManager has no Attack Arrow prefab, or it is missing its line or triangle - the mage leash will not draw.");
            return false;
        }
        Triangle head = arrow.PointTriangle as Triangle;
        if(head == null) {
            Debug.LogError("ShapesDrawingManager: the Attack Arrow prefab's point triangle is not a Shapes Triangle - the mage leash will not draw.");
            return false;
        }
        _arrowPrefab = arrow;
        _arrowThickness = arrow.MovementLine.Thickness;
        _arrowLineBloom = arrow.MovementLine.GetComponent<ShapesBloom>().BloomAmount;
        _arrowHeadBloom = head.GetComponent<ShapesBloom>().BloomAmount;
        _arrowHeadRoundness = head.Roundness;
        _arrowDashSize = arrow.ApproachDashSize;
        _arrowDashSpacing = arrow.ApproachDashSpacing;
        Vector3 scale = head.transform.localScale;
        _arrowHeadA = Vector3.Scale(head.A, scale);
        _arrowHeadB = Vector3.Scale(head.B, scale);
        _arrowHeadC = Vector3.Scale(head.C, scale);
        _arrowStyleValid = true;
        return true;
    }
    // Glow works as on the prefab: Lighten blend with the bloom amount in the alpha, which the shader multiplies into the colour.
    private Color ArrowColor(Color color, float bloom)
    {
        color.a = bloom * _leashAlpha;
        return color;
    }
    // In reach: one solid blue line. Out of reach: the walk (green, arrow head where the mage enters
    // range) and then the remaining gap to the target (dashed red).
    private void DrawLeash()
    {
        if(!LoadArrowStyle()) return;

        Draw.BlendMode = ShapesBlendMode.Lighten;
        Draw.ThicknessSpace = ThicknessSpace.Meters;
        Draw.LineGeometry = LineGeometry.Billboard;
        Draw.LineEndCaps = LineEndCap.Round;

        if(!_leashOutOfRange) {
            Draw.Line(_leashStart, _leashEnd, _arrowThickness * MarkerScale, ArrowColor(_leashInRangeColor, _arrowLineBloom));
            Draw.BlendMode = ShapesBlendMode.Transparent;
            return;
        }

        Vector3 delta = _leashEnd - _leashStart;
        float distance = delta.magnitude;
        if(distance <= 0.001f) return;
        Vector3 direction = delta / distance;
        // Where the squad centre first sits within range of the point: the approach order's own goal.
        Vector3 split = _leashStart + direction * Mathf.Max(0f, distance - _leashRange);

        Draw.Line(_leashStart, split, _arrowThickness * MarkerScale, ArrowColor(_arrowPrefab.MovementColor, _arrowLineBloom));
        DrawArrowHead(split, direction);

        DashStyle dashes = DashStyle.defaultDashStyle;
        dashes.size = _arrowDashSize;
        dashes.spacing = _arrowDashSpacing;
        // The cast leg is attack red only when the spell is thrown at the enemy.
        using(Draw.DashedScope(dashes)) {
            Draw.Line(split, _leashEnd, _arrowThickness * MarkerScale, ArrowColor(_leashFriendly ? _leashInRangeColor : _arrowPrefab.AttackColor, _arrowLineBloom));
        }
        Draw.BlendMode = ShapesBlendMode.Transparent;
    }
    // The prefab's triangle in world space: AttackArrowDrawer.RecalculateArrowPath sets its pivot one
    // unit short of the end point and LookAt turns its local z along the arrow.
    private void DrawArrowHead(Vector3 end, Vector3 direction)
    {
        Vector3 pivot = end - direction;
        Vector3 right = Vector3.Cross(Vector3.up, direction);
        Vector3 ToWorld(Vector3 local) => pivot + right * local.x + Vector3.up * local.y + direction * local.z;
        float s = MarkerScale;
        Draw.Triangle(ToWorld(_arrowHeadA * s), ToWorld(_arrowHeadB * s), ToWorld(_arrowHeadC * s), _arrowHeadRoundness, ArrowColor(_arrowPrefab.MovementColor, _arrowHeadBloom));
    }
    private void DrawAreaBand()
    {
        if(_bandAlpha <= 0f) return;
        float thickness = _ringRadius * _bandFraction;
        Color outer = new Color(_bandColor.r, _bandColor.g, _bandColor.b, _bandAlpha * _ringAlpha);
        Color inner = new Color(_bandColor.r, _bandColor.g, _bandColor.b, 0f);
        Draw.BlendMode = ShapesBlendMode.Lighten;
        Draw.Ring(
            _ringPosition,
            Quaternion.Euler(90, 0, 0),
            _ringRadius - thickness * 0.5f,
            thickness,
            DiscColors.Radial(inner, outer)
        );
        if(_outerBandFraction > 0f) {
            float outerThickness = _ringRadius * _outerBandFraction;
            Draw.Ring(
                _ringPosition,
                Quaternion.Euler(90, 0, 0),
                _ringRadius + outerThickness * 0.5f,
                outerThickness,
                DiscColors.Radial(outer, inner)
            );
        }
        Draw.BlendMode = ShapesBlendMode.Transparent;
    }
    private void DrawPentagram(Color color)
    {
        if(_pentagramPath == null) {
            _pentagramPath = new PolylinePath();
            for(int i = 0; i < 5; i++) _pentagramPath.AddPoint(Vector3.zero);
        }
        float r = _ringRadius * _pentagramRadiusFraction;
        for(int i = 0; i < 5; i++) {
            // Point 0 at the top; the ring's 90-degree X rotation lays local XY onto the ground.
            float angle = (90f + 72f * StarOrder[i]) * Mathf.Deg2Rad;
            _starPoints[i] = new Vector2(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r);
            _pentagramPath.SetPoint(i, (Vector3)_starPoints[i]);
        }
        using(Draw.MatrixScope) {
            Draw.Matrix = Matrix4x4.TRS(_ringPosition, StarRotation(), Vector3.one);
            Draw.PolylineGeometry = PolylineGeometry.Flat2D;
            Draw.Polyline(_pentagramPath, true, _pentagramThickness * MarkerScale, PolylineJoins.Miter, color);
        }
    }
    // Local Z points down into the ground after the X tilt, so a positive spin about it is clockwise from above.
    private Quaternion StarRotation() =>
        Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, _pentagramRotationSpeed * Time.unscaledTime);

    #region Prefab overlays
    // Instantiated on first use under this object so the battle scene owns it. Stop leaves the live
    // particles to die out, which stands in for the fade the Shapes drawing gets.
    private void UpdateOverlay(ref GameObject instance, GameObject prefab, bool wanted, float scale)
    {
        if(!wanted) {
            if(instance != null && instance.activeSelf) {
                foreach(ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>())
                    ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }
            return;
        }
        if(prefab == null) return;
        if(instance == null) {
            instance = Instantiate(prefab, transform);
            instance.name = prefab.name + " (cast preview)";
            // MagicCircle is authored as a one-shot; a preview has to keep going.
            foreach(ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>()) {
                ParticleSystem.MainModule main = ps.main;
                main.loop = true;
            }
        }
        instance.transform.position = _castPoint;
        instance.transform.localScale = Vector3.one * scale;
        instance.SetActive(true);
        foreach(ParticleSystem ps in instance.GetComponentsInChildren<ParticleSystem>())
            if(!ps.isEmitting) ps.Play(false);
    }
    #endregion

    #region Wisps
    private void DrawWisps(Color ringColor, Color starColor)
    {
        if(_wispStrands <= 0 || _wispAlpha <= 0f) return;
        EnsureWisps(ref _ringWisps);
        EnsureWisps(ref _starWisps);
        float t = Time.unscaledTime * _wispSpeed;
        float push = _ringRadius * _wispAmplitude;
        ringColor.a *= _wispAlpha;
        starColor.a *= _wispAlpha;

        for(int k = 0; k < _wispStrands; k++) {
            FillRingWisp(_ringWisps[k], k, t, push);
            if(_showStar) FillStarWisp(_starWisps[k], k, t, push);
        }
        Draw.PolylineGeometry = PolylineGeometry.Flat2D;
        using(Draw.MatrixScope) {
            Draw.Matrix = Matrix4x4.TRS(_ringPosition, Quaternion.Euler(90, 0, 0), Vector3.one);
            for(int k = 0; k < _wispStrands; k++)
                Draw.Polyline(_ringWisps[k], true, _wispThickness * MarkerScale, PolylineJoins.Simple, ringColor);
        }
        if(!_showStar) return;
        using(Draw.MatrixScope) {
            Draw.Matrix = Matrix4x4.TRS(_ringPosition, StarRotation(), Vector3.one);
            for(int k = 0; k < _wispStrands; k++)
                Draw.Polyline(_starWisps[k], true, _wispThickness * MarkerScale, PolylineJoins.Simple, starColor);
        }
    }
    // Sampling the noise on a circle keeps the strand seamless where it closes.
    private float Noise(float x, float y, int strand, float t) =>
        Mathf.PerlinNoise(x * _wispNoiseScale + strand * 7.3f + t, y * _wispNoiseScale + strand * 3.1f + t * 0.7f) * 2f - 1f;

    private void FillRingWisp(PolylinePath path, int strand, float t, float push)
    {
        int n = path.Count;
        for(int i = 0; i < n; i++) {
            float angle = Mathf.PI * 2f * i / n;
            float c = Mathf.Cos(angle), sn = Mathf.Sin(angle);
            float r = _ringRadius + push * Noise(c, sn, strand, t);
            path.SetPoint(i, new Vector3(c * r, sn * r, 0f));
        }
    }
    // Walks the five star edges; the push fades to zero at each vertex so the tips stay sharp.
    private void FillStarWisp(PolylinePath path, int strand, float t, float push)
    {
        int n = path.Count;
        float invR = _ringRadius > 0f ? 1f / _ringRadius : 0f;
        for(int i = 0; i < n; i++) {
            float u = 5f * i / n;
            int e = Mathf.Min((int)u, 4);
            float tl = u - e;
            Vector2 a = _starPoints[e], b = _starPoints[(e + 1) % 5];
            Vector2 p = Vector2.Lerp(a, b, tl);
            Vector2 dir = (b - a).normalized;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            float amount = push * Mathf.Sin(Mathf.PI * tl) * Noise(p.x * invR, p.y * invR, strand, t);
            path.SetPoint(i, (Vector3)(p + normal * amount));
        }
    }
    private void EnsureWisps(ref PolylinePath[] paths)
    {
        if(paths != null && paths.Length == _wispStrands && paths[0].Count == _wispSegments) return;
        DisposeWisps(ref paths);
        paths = new PolylinePath[_wispStrands];
        for(int k = 0; k < _wispStrands; k++) {
            paths[k] = new PolylinePath();
            for(int i = 0; i < _wispSegments; i++) paths[k].AddPoint(Vector3.zero);
        }
    }
    private static void DisposeWisps(ref PolylinePath[] paths)
    {
        if(paths == null) return;
        foreach(PolylinePath p in paths) if(p != null) p.Dispose();
        paths = null;
    }
    #endregion
}
}
