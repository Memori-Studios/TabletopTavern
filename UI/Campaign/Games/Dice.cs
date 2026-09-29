using MoreMountains.Feedbacks;
using QuickOutline;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TJ.Games
{
    public class Dice : MonoBehaviour
    {
        [SerializeField] private MMF_Player _loadFeedback;
        [SerializeField] private Transform _diceModel;
        [SerializeField] private Quaternion[] _faceRotations = new Quaternion[6];
        [SerializeField] private float _spinDuration = 2f;
        [SerializeField] private float _spinSpeed = 540f;

        [Header("Editor Tools")]
        [SerializeField] private int _editorFace = 1;
        [SerializeField] private Camera _alignCamera;

        // Standard Western d6: face 2 at +Z, face 1 at +Y, face 3 at +X, face 4 at -X, face 5 at -Z, face 6 at -Y
        static readonly Vector3[] CanonicalFaceNormals = new Vector3[]
        {
            new( 0,  1,  0),  // face 1
            new( 0,  0,  1),  // face 2
            new( 1,  0,  0),  // face 3
            new(-1,  0,  0),  // face 4
            new( 0,  0, -1),  // face 5
            new( 0, -1,  0),  // face 6
        };

        private Transform Model => _diceModel != null ? _diceModel : transform;

        [SerializeField] private Color _outlineColor = Color.white;

        private Outline _outline;
        private bool _prespinning;
        private Vector3 _prespinAxis;
        private Vector3 _prespinAxis2;

        private void Awake()
        {
            _outline = GetComponentInChildren<Outline>();
            if (_outline != null)
            {
                _outline.OutlineWidth = 0f;
                _outline.OutlineColor = _outlineColor;
            }
        }

        public void SetOutlineColor(Color color)
        {
            _outlineColor = color;
            if (_outline != null) _outline.OutlineColor = color;
        }

#if UNITY_EDITOR
        [ContextMenu("Align to Scene Camera")]
        private void AlignToSceneCamera()
        {
            Camera cam = _alignCamera;
            if (cam == null)
            {
                if (SceneView.lastActiveSceneView == null) { Debug.LogWarning("[Dice] No active Scene view found."); return; }
                cam = SceneView.lastActiveSceneView.camera;
            }
            Undo.RecordObject(Model, "Align Dice to Camera");
            Model.rotation = Quaternion.LookRotation(cam.transform.position - Model.position);
            EditorUtility.SetDirty(this);
        }

        [ContextMenu("Save Rotation for Face")]
        private void SaveRotationForFace()
        {
            int index = Mathf.Clamp(_editorFace - 1, 0, 5);
            Undo.RecordObject(this, "Save Dice Face Rotation");
            _faceRotations[index] = Model.rotation;
            EditorUtility.SetDirty(this);
            Debug.Log($"[Dice] Saved face {_editorFace}: {Model.rotation.eulerAngles}");
        }

        [ContextMenu("Generate All Face Rotations")]
        private void GenerateAllFaceRotations()
        {
            int refIndex = Mathf.Clamp(_editorFace - 1, 0, 5);
            Undo.RecordObject(this, "Generate Dice Face Rotations");

            // Recover the canonical base rotation from the one known face
            Quaternion refRotation = _faceRotations[refIndex];
            Quaternion refOffset = Quaternion.FromToRotation(CanonicalFaceNormals[refIndex], Vector3.forward);
            Quaternion baseRotation = refRotation * Quaternion.Inverse(refOffset);

            for (int i = 0; i < 6; i++)
                _faceRotations[i] = baseRotation * Quaternion.FromToRotation(CanonicalFaceNormals[i], Vector3.forward);

            EditorUtility.SetDirty(this);
            Debug.Log("[Dice] Generated all 6 face rotations from face " + _editorFace);
        }

        [ContextMenu("Preview Face Rotation")]
        private void PreviewFaceRotation()
        {
            int index = Mathf.Clamp(_editorFace - 1, 0, 5);
            Undo.RecordObject(Model, "Preview Dice Face Rotation");
            Model.rotation = _faceRotations[index];
            EditorUtility.SetDirty(this);
        }
#endif

        public void PlayLoadFeedback() => _loadFeedback?.PlayFeedbacks();

        public async Task AnimateToFace(int face)
        {
            Transform model = Model;
            Quaternion targetRotation = _faceRotations[Mathf.Clamp(face - 1, 0, 5)];
            Vector3 spinAxis = Random.onUnitSphere;
            float spinTime = _spinDuration * 0.95f;
            float settleTime = _spinDuration * 0.05f;
            float elapsed = 0f;

            while (elapsed < spinTime)
            {
                // A quit to the menu mid-roll destroys the die while this loop is still running.
                if (model == null) return;
                model.Rotate(spinAxis * (_spinSpeed * Time.unscaledDeltaTime), Space.World);
                elapsed += Time.unscaledDeltaTime;
                await Task.Yield();
            }

            Quaternion preSettleRotation = model.rotation;
            elapsed = 0f;
            while (elapsed < settleTime)
            {
                if (model == null) return;
                model.rotation = Quaternion.Lerp(preSettleRotation, targetRotation, elapsed / settleTime);
                elapsed += Time.unscaledDeltaTime;
                await Task.Yield();
            }

            if (model == null) return;
            model.rotation = targetRotation;
        }
        public async void PulseOutline()
        {
            if (_outline == null) return;
            const float targetWidth = 5f;
            const float halfDuration = 0.5f;
            float elapsed = 0f;

            while (elapsed < halfDuration)
            {
                if (_outline == null) return;
                _outline.OutlineWidth = Mathf.Lerp(0f, targetWidth, elapsed / halfDuration);
                elapsed += Time.unscaledDeltaTime;
                await Task.Yield();
            }

            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                if (_outline == null) return;
                _outline.OutlineWidth = Mathf.Lerp(targetWidth, 0f, elapsed / halfDuration);
                elapsed += Time.unscaledDeltaTime;
                await Task.Yield();
            }

            if (_outline == null) return;
            _outline.OutlineWidth = 0f;
        }

        public void StartPrespin()
        {
            if (_prespinning) return;
            _prespinAxis  = Random.onUnitSphere;
            _prespinAxis2 = Random.onUnitSphere;
            _prespinning  = true;
            PrespinLoop();
        }

        public void StopPrespin() => _prespinning = false;

        private async void PrespinLoop()
        {
            Transform model = Model;
            float speed = _spinSpeed * 0.25f;
            while (_prespinning)
            {
                model.Rotate(_prespinAxis  * (speed        * Time.unscaledDeltaTime/10), Space.World);
                model.Rotate(_prespinAxis2 * (speed * 0.7f * Time.unscaledDeltaTime/10), Space.World);
                await Task.Yield();
            }
        }

        public void ResetScale()
        {
            Model.localScale = Vector3.zero;
        }
    }
}
