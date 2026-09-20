using UnityEngine;
using System.Collections;
using Shapes;
using TJ.Shapes;
using Memori.Input;
using Memori.SaveData;

namespace TJ
{
    public class GarrisonGateRangeDrawer : MonoBehaviour
    {
        [SerializeField] private Disc _arc, _arc2;
        [SerializeField] private Line leftLine, rightLine;
        [SerializeField] private float range = 35f;
        [SerializeField] private SquadData _squadData;
        public Transform FiringPointA;
        public Transform FiringPointB;
        [SerializeField] private float _fadeDuration = 1f;

        private ShapesBloom _arcBloom, _arc2Bloom;
        private ShapesBloom leftLineBloom, rightLineBloom;
        private Color _arcTargetColor;
        private Color _arc2TargetOuter;
        private Color _lineTargetColor;
        private Coroutine _fadeRoutine;
        private bool _toggledOn = false;

        private void Awake()
        {
            _arcBloom      = _arc       != null ? _arc.GetComponent<ShapesBloom>()       : null;
            _arc2Bloom     = _arc2      != null ? _arc2.GetComponent<ShapesBloom>()      : null;
            leftLineBloom  = leftLine   != null ? leftLine.GetComponent<ShapesBloom>()   : null;
            rightLineBloom = rightLine  != null ? rightLine.GetComponent<ShapesBloom>()  : null;

            range = _squadData.stats.BaseRange;
            if (SaveDataHandler.Load().battleFieldPreset.weather == Weather.Fog)
                range *= WeatherRuleData.Fog.RangeModifier;
            Apply();

            if (_arc != null)
            {
                _arcTargetColor = _arc.Color;
                _arc.Color = new Color(_arcTargetColor.r, _arcTargetColor.g, _arcTargetColor.b, 0f);
            }

            if (_arc2 != null)
            {
                _arc2TargetOuter = _arc2.ColorOuter;
                _arc2.ColorInner = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);
                _arc2.ColorOuter = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, 0f);
            }

            if (leftLine != null)
            {
                _lineTargetColor = leftLine.Color;
                leftLine.Color  = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, 0f);
                rightLine.Color = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, 0f);
            }
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

        private void Apply()
        {
            if (_arc == null) return;
            _arc.Radius = range;
            _arcBloom?.Bloom();

            if (_arc2 == null) return;
            _arc2.Radius = range;
            _arc2Bloom?.Bloom();

            if (leftLine == null) return;

            Vector3 center = Vector3.zero;

            static Vector3 CalculateArcPoint(Vector3 c, float r, float angleDeg)
            {
                float rad = Mathf.Deg2Rad * angleDeg;
                return new Vector3(c.x + r * Mathf.Cos(rad), c.y, c.z + r * Mathf.Sin(rad));
            }

            Vector3 startPoint = CalculateArcPoint(center, range, 5f);
            Vector3 endPoint   = CalculateArcPoint(center, range, 175f);

            leftLine.Start  = center;
            leftLine.End    = endPoint;

            rightLine.Start = center;
            rightLine.End   = startPoint;

            leftLineBloom.Bloom();
            rightLineBloom.Bloom();
        }

        public void TurnOn()
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Fade(targetAlpha: 1f));
        }

        public void TurnOff()
        {
            if (_toggledOn) return;
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(Fade(targetAlpha: 0f));
        }

        private IEnumerator Fade(float targetAlpha)
        {
            float startAlpha1    = _arc.Color.a;
            float startAlpha2Out = _arc2.ColorOuter.a;
            float startAlphaLine = leftLine.Color.a;
            float elapsed = 0f;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _fadeDuration);

                _arc.Color = new Color(_arcTargetColor.r, _arcTargetColor.g, _arcTargetColor.b,
                    Mathf.Lerp(startAlpha1, _arcTargetColor.a * targetAlpha, t));

                float outerA = Mathf.Lerp(startAlpha2Out, _arc2TargetOuter.a * targetAlpha / 2f, t);
                _arc2.ColorInner = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, outerA/4f);
                _arc2.ColorOuter = new Color(_arc2TargetOuter.r, _arc2TargetOuter.g, _arc2TargetOuter.b, outerA);

                float lineA = Mathf.Lerp(startAlphaLine, _lineTargetColor.a * targetAlpha, t);
                leftLine.Color  = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, lineA);
                rightLine.Color = new Color(_lineTargetColor.r, _lineTargetColor.g, _lineTargetColor.b, lineA);

                yield return null;
            }
        }

        private void OnDestroy()
        {
            if (InputHandler.HasInstance)
                InputHandler.Instance.OnShowUnitMovement -= ToggleRange;
        }
    }
}
