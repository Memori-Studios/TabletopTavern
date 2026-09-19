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
    }

    public override void DrawShapes( Camera cam )
    {
        if(_ringAlpha <= 0f) return;

        // Before the transparent pass: the squad flags do not write depth, so they must draw after this to sit on top.
        using( Draw.Command( cam, UnityEngine.Rendering.Universal.RenderPassEvent.AfterRenderingSkybox ) ){
            // Normal depth test: units in front hide the drawing, as a ground decal should.
            Draw.ZTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            DrawAreaBand();

            Color ringColor = _spellRingColor * _spellRingIntensity;
            ringColor.a = _ringAlpha;

            Draw.Ring(
                _ringPosition,
                Quaternion.Euler(90, 0, 0),
                _ringRadius,
                _spellRingThickness,
                ringColor
            );
            Color starColor = _pentagramColor * _pentagramIntensity;
            starColor.a = _ringAlpha;
            if(_showStar) DrawPentagram(starColor);
            DrawWisps(ringColor, starColor);
        }
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
            Draw.Polyline(_pentagramPath, true, _pentagramThickness, PolylineJoins.Miter, color);
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
                Draw.Polyline(_ringWisps[k], true, _wispThickness, PolylineJoins.Simple, ringColor);
        }
        if(!_showStar) return;
        using(Draw.MatrixScope) {
            Draw.Matrix = Matrix4x4.TRS(_ringPosition, StarRotation(), Vector3.one);
            for(int k = 0; k < _wispStrands; k++)
                Draw.Polyline(_starWisps[k], true, _wispThickness, PolylineJoins.Simple, starColor);
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
