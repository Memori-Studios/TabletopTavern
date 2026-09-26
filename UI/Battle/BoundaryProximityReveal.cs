using Shapes;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TJ
{
    /// <summary>
    /// Draws a zone Polyline as a glowing wall: the line itself at the base and a translucent curtain
    /// rising from it that fades out with height. Optionally only the stretch near the cursor shows,
    /// measured in screen pixels.
    /// </summary>
    public class BoundaryProximityReveal : ImmediateModeShapeDrawer
    {
        // Points closer than this many pixels to the cursor are fully visible.
        private const float RevealRadiusPx = 160f;
        // Width in pixels of the taper from fully visible to hidden, outside the reveal radius.
        private const float FadeWidthPx = 120f;
        // How fast a point's alpha moves toward its target, per second.
        private const float FadeSpeed = 8f;
        // Metres the curtain rises above the line.
        private const float WallHeight = 4f;
        // Alpha of the curtain at the ground. It fades to clear at the top.
        private const float WallAlpha = 0.25f;

        private Polyline line;
        private bool proximityFade = true;
        private float[] _alphas;

        public bool ProximityFade
        {
            get => proximityFade;
            set => proximityFade = value;
        }

        private void Awake()
        {
            line = GetComponent<Polyline>();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            // Opaque blend mode ignores vertex alpha, so the per-point fade would be invisible.
            if (line != null) line.BlendMode = ShapesBlendMode.Transparent;
        }

        private void LateUpdate()
        {
            if (line == null || !line.gameObject.activeInHierarchy) return;
            Camera camera = BattleManager.Instance.BattleCamera;
            if (camera == null) return;

            int count = line.Count;
            if (_alphas == null || _alphas.Length != count)
            {
                _alphas = new float[count];
            }

            Vector2 mouse = Input.mousePosition;
            float revealRadius = RevealRadiusPx * BattlefieldMarkerScale.Current;
            float fadeEnd = revealRadius + FadeWidthPx * BattlefieldMarkerScale.Current;
            float step = FadeSpeed * Time.unscaledDeltaTime;

            for (int i = 0; i < count; i++)
            {
                PolylinePoint point = line[i];
                float target = 1f;
                if (proximityFade)
                {
                    Vector3 screen = camera.WorldToScreenPoint(line.transform.TransformPoint(point.point));
                    target = 0f;
                    if (screen.z > 0f)
                    {
                        float dist = Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
                        target = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(revealRadius, fadeEnd, dist));
                    }
                }

                float alpha = Mathf.MoveTowards(_alphas[i], target, step);
                _alphas[i] = alpha;
                if (Mathf.Approximately(alpha, point.color.a)) continue;
                Color color = point.color;
                color.a = alpha;
                line.SetPointColor(i, color);
            }
        }

        public override void DrawShapes(Camera cam)
        {
            if (line == null || _alphas == null || !line.gameObject.activeInHierarchy) return;
            if (cam.cameraType != CameraType.SceneView && cam != BattleManager.Instance.BattleCamera) return;

            Color baseColor = line.Color;
            Color top = baseColor;
            top.a = 0f;
            Vector3 up = Vector3.up * WallHeight;
            Transform lineTransform = line.transform;
            int count = Mathf.Min(line.Count, _alphas.Length);

            // Before the transparent pass, like the spell ring, so squad flags still draw on top.
            using (Draw.Command(cam, RenderPassEvent.AfterRenderingSkybox))
            {
                Draw.ZTest = CompareFunction.LessEqual;
                Draw.BlendMode = ShapesBlendMode.Transparent;
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    float a0 = _alphas[i] * WallAlpha;
                    float a1 = _alphas[next] * WallAlpha;
                    if (a0 <= 0.001f && a1 <= 0.001f) continue;

                    Vector3 p0 = lineTransform.TransformPoint(line[i].point);
                    Vector3 p1 = lineTransform.TransformPoint(line[next].point);
                    Color bottom0 = baseColor;
                    bottom0.a = a0;
                    Color bottom1 = baseColor;
                    bottom1.a = a1;
                    Draw.Quad(p0, p0 + up, p1 + up, p1, bottom0, top, top, bottom1);
                }
            }
        }
    }
}
