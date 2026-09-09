using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
#endif
using System.Collections.Generic;

namespace TJ
{
    [ExecuteInEditMode]
    public class PoseSkinnedMeshEditor : MonoBehaviour
    {
        public SkinnedMeshRenderer skinnedMeshRenderer;
        public Animator animator;       // The Animator component

        [Header("Direct Clip Mode")]
        [Tooltip("Optional. When set, the clip is sampled directly and the Animator / AnimatorController is ignored.")]
        public AnimationClip clip;
        [Tooltip("Hierarchy the clip's curve paths are relative to. Defaults to this GameObject.")]
        public Transform sampleRoot;

        [HideInInspector] public string animationState;  // The selected animation state name
        public int frame;              // The target frame
        private int frameRate = 30;     // Fallback frame rate if clip doesn't provide one
        [HideInInspector] public int selectedStateIndex = 0; // Index for state dropdown
        private string[] stateNames = new string[0]; // Cached animation state names

        // Called when the script is added to a GameObject or reset in the Inspector
        private void Reset()
        {
            if (animator != null) return;

            animator = GetComponent<Animator>();
            if (animator == null)
            {
                Debug.LogWarning("No Animator component found on this GameObject.");
            }
            UpdateStateNames();
        }

        // Called when the script is loaded or a value changes in the Inspector
        private void OnValidate()
        {
            UpdateStateNames();
            if (stateNames.Length > 0 && selectedStateIndex >= 0 && selectedStateIndex < stateNames.Length)
            {
                animationState = stateNames[selectedStateIndex];
            }
        }

        // Fetch animation state names from the Animator Controller
        private void UpdateStateNames()
        {
            List<string> names = new List<string>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
#if UNITY_EDITOR
                var controller = animator.runtimeAnimatorController as AnimatorController;
                if (controller != null)
                {
                    foreach (var layer in controller.layers)
                    {
                        foreach (var state in layer.stateMachine.states)
                        {
                            names.Add(state.state.name);
                        }
                    }
                }
#endif
            }

            stateNames = names.ToArray();
            // Ensure the selected index is valid
            if (selectedStateIndex >= stateNames.Length)
            {
                selectedStateIndex = 0;
            }
        }

        private AnimationClip GetClipForState(string stateName)
        {
#if UNITY_EDITOR
            var controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller != null)
            {
                foreach (var layer in controller.layers)
                {
                    foreach (var state in layer.stateMachine.states)
                    {
                        if (state.state.name == stateName)
                        {
                            if (state.state.motion is AnimationClip clip)
                            {
                                return clip;
                            }
                        }
                    }
                }
            }
#endif
            return null;
        }

        public void SetAnimationToFrame()
        {
#if UNITY_EDITOR
            if (clip != null)
            {
                SampleClipToFrame();
                return;
            }
#endif
            if (!animator)
            {
                animator = GetComponent<Animator>();
                if (!animator)
                {
                    Debug.LogError("No Animator component found.");
                    return;
                }
            }
            if (string.IsNullOrEmpty(animationState))
            {
                Debug.LogError("No animation state specified.");
                return;
            }

            var stateClip = GetClipForState(animationState);
            if (stateClip == null)
            {
                Debug.LogError("Could not find AnimationClip for state " + animationState);
                return;
            }

            float clipFrameRate = stateClip.frameRate > 0 ? stateClip.frameRate : frameRate;
            float length = stateClip.length;
            int maxFrame = Mathf.RoundToInt(length * clipFrameRate);

            if (frame < 0) frame = 0;
            if (frame > maxFrame) frame = maxFrame;

            float normalizedTime = maxFrame > 0 ? (float)frame / maxFrame : 0f;

            // Play the animation state and set the normalized time
            animator.Play(animationState, 0, normalizedTime);
            animator.Update(0); // Force the Animator to update immediately

            Debug.Log($"Set animation state '{animationState}' to frame {frame}.");
        }
#if UNITY_EDITOR
        // Poses the hierarchy straight from an AnimationClip. No Animator and no AnimatorController
        // required - the clip is sampled in AnimationMode, the resulting local TRS is read back, then
        // AnimationMode is exited (which reverts the sample) and the values are written on permanently
        // through Undo so the pose survives and can be undone.
        private void SampleClipToFrame()
        {
            string[] curvePaths = GetDistinctCurvePaths(clip);

            Transform root = sampleRoot;
            if (root == null) root = transform;

            // A clip's curve paths are relative to the object the clip is authored against - for an FBX
            // that is the imported root, not the SkinnedMeshRenderer. Point Sample Root at the wrong
            // object and every binding misses, so the sample silently does nothing.
            if (CountResolvedPaths(root, curvePaths) == 0)
            {
                Transform better = FindBestSampleRoot(root, curvePaths);
                if (better == null)
                {
                    Debug.LogError($"None of the {curvePaths.Length} curve paths in clip '{clip.name}' resolve anywhere in this hierarchy. Wrong clip for this rig?", this);
                    return;
                }

                Debug.LogWarning($"Sample Root '{root.name}' matches none of the curve paths in clip '{clip.name}'. Sampling against '{better.name}' instead - assign that to Sample Root to silence this.", better);
                root = better;
            }

            float clipFrameRate = clip.frameRate > 0 ? clip.frameRate : frameRate;
            int maxFrame = Mathf.RoundToInt(clip.length * clipFrameRate);

            if (frame < 0) frame = 0;
            if (frame > maxFrame) frame = maxFrame;

            float time = maxFrame > 0 ? clip.length * ((float)frame / maxFrame) : 0f;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = new Vector3[transforms.Length];
            Quaternion[] rotations = new Quaternion[transforms.Length];
            Vector3[] scales = new Vector3[transforms.Length];

            for (int i = 0; i < transforms.Length; i++)
            {
                positions[i] = transforms[i].localPosition;
                rotations[i] = transforms[i].localRotation;
                scales[i] = transforms[i].localScale;
            }

            bool alreadyInAnimationMode = AnimationMode.InAnimationMode();
            if (!alreadyInAnimationMode) AnimationMode.StartAnimationMode();

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(root.gameObject, clip, time);
            AnimationMode.EndSampling();

            int changed = 0;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i].localPosition != positions[i] ||
                    transforms[i].localRotation != rotations[i] ||
                    transforms[i].localScale != scales[i]) changed++;

                positions[i] = transforms[i].localPosition;
                rotations[i] = transforms[i].localRotation;
                scales[i] = transforms[i].localScale;
            }

            if (!alreadyInAnimationMode) AnimationMode.StopAnimationMode();

            Undo.RecordObjects(transforms, "Pose Skinned Mesh From Clip");

            if (changed > 0)
            {
                for (int i = 0; i < transforms.Length; i++)
                {
                    transforms[i].localPosition = positions[i];
                    transforms[i].localRotation = rotations[i];
                    transforms[i].localScale = scales[i];
                }
            }
            else
            {
                // AnimationMode can decline some clips (legacy ones especially). SampleAnimation applies
                // straight to the hierarchy and is not reverted, so it only runs as a fallback.
                clip.SampleAnimation(root.gameObject, time);
                for (int i = 0; i < transforms.Length; i++)
                {
                    if (transforms[i].localPosition != positions[i] ||
                        transforms[i].localRotation != rotations[i] ||
                        transforms[i].localScale != scales[i]) changed++;
                }
            }

            if (skinnedMeshRenderer != null) EditorUtility.SetDirty(skinnedMeshRenderer);
            EditorUtility.SetDirty(this);
            SceneView.RepaintAll();

            if (changed == 0)
                Debug.LogWarning($"Sampled clip '{clip.name}' at frame {frame} against '{root.name}' but no transform moved.", this);
            else
                Debug.Log($"Sampled clip '{clip.name}' at frame {frame} / {maxFrame} onto '{root.name}' ({changed} transforms posed).", this);
        }

        // Distinct object paths the clip animates, relative to whatever it is sampled against.
        private static string[] GetDistinctCurvePaths(AnimationClip clip)
        {
            HashSet<string> paths = new HashSet<string>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                paths.Add(binding.path);

            string[] result = new string[paths.Count];
            paths.CopyTo(result);
            return result;
        }

        private static int CountResolvedPaths(Transform root, string[] curvePaths)
        {
            int count = 0;
            for (int i = 0; i < curvePaths.Length; i++)
            {
                if (string.IsNullOrEmpty(curvePaths[i])) count++;
                else if (root.Find(curvePaths[i]) != null) count++;
            }
            return count;
        }

        // Recovers from a misassigned Sample Root rather than producing a silent no-op. Nearest-first:
        // this component's own object, then ancestors of the assigned root, then its descendants. The
        // order matters when a hierarchy holds two identical rigs (Mystic_Arms_Top_2 / _Bottom_2) - a
        // blind sweep would pick whichever came first rather than the one this component sits on.
        private Transform FindBestSampleRoot(Transform start, string[] curvePaths)
        {
            if (CountResolvedPaths(transform, curvePaths) > 0) return transform;

            for (Transform ancestor = start.parent; ancestor != null; ancestor = ancestor.parent)
                if (CountResolvedPaths(ancestor, curvePaths) > 0) return ancestor;

            foreach (Transform descendant in start.GetComponentsInChildren<Transform>(true))
                if (CountResolvedPaths(descendant, curvePaths) > 0) return descendant;

            return null;
        }
#endif

#if UNITY_EDITOR
        // Bakes the SkinnedMeshRenderer's CURRENT pose into a static Mesh asset. The point being that a
        // posed static mesh can be swapped onto a plain MeshFilter + MeshRenderer, which is what the GPU
        // ECS attachment system can actually drive - it cannot skin anything.
        public void SaveBakedPoseMesh()
        {
            SkinnedMeshRenderer target = skinnedMeshRenderer;
            if (target == null) target = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (target == null)
            {
                Debug.LogError("No SkinnedMeshRenderer assigned, and none found in children.", this);
                return;
            }

            // Default next to the prefab being edited, which is almost always where this belongs.
            string defaultFolder = "Assets";
            var prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && !string.IsNullOrEmpty(prefabStage.assetPath))
                defaultFolder = System.IO.Path.GetDirectoryName(prefabStage.assetPath).Replace(System.IO.Path.DirectorySeparatorChar, '/');

            string path = EditorUtility.SaveFilePanelInProject(
                "Save Posed Mesh", target.name + "_Posed", "asset",
                "Save the current pose as a static Mesh asset.", defaultFolder);
            if (string.IsNullOrEmpty(path)) return;

            Mesh baked = new Mesh();
            baked.name = System.IO.Path.GetFileNameWithoutExtension(path);

            // useScale false keeps the mesh in the renderer's own local space, so assigning it to a
            // MeshFilter on this same GameObject reproduces the pose exactly.
            target.BakeMesh(baked, false);

            AssetDatabase.CreateAsset(baked, path);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(baked);

            Debug.Log($"Baked current pose of '{target.name}' to '{path}' ({baked.vertexCount} verts).", baked);
        }
#endif

        public void ResetToBindPose()
        {
            if (skinnedMeshRenderer == null)
                skinnedMeshRenderer = GetComponent<SkinnedMeshRenderer>();

            if (skinnedMeshRenderer == null)
            {
                Debug.LogError("No SkinnedMeshRenderer found!");
                return;
            }

            var bones = skinnedMeshRenderer.bones;
            var bindPoses = skinnedMeshRenderer.sharedMesh.bindposes;

            if (bones.Length != bindPoses.Length)
            {
                Debug.LogError($"Bone count mismatch: {bones.Length} bones vs {bindPoses.Length} bindposes!");
                return;
            }

            // Map bone Transforms to their index in the bones array
            var boneIndex = new System.Collections.Generic.Dictionary<Transform, int>();
            for (int i = 0; i < bones.Length; i++)
                boneIndex[bones[i]] = i;

            // Start recursion from root bone
            ResetBoneRecurse(skinnedMeshRenderer.rootBone, boneIndex, bindPoses, null);

            // Force editor refresh (better than non-existent Update())
#if UNITY_EDITOR
            EditorUtility.SetDirty(skinnedMeshRenderer);
            SceneView.RepaintAll();  // Refresh scene view immediately
#endif
        }
        private void ResetBoneRecurse(Transform bone, System.Collections.Generic.Dictionary<Transform, int> boneIndex, Matrix4x4[] bindPoses, Matrix4x4? parentWorldMatrix)
        {
            if (bone == null || !boneIndex.TryGetValue(bone, out int boneIdx))
                return;

            // Desired world matrix at bind pose = inverse(bindPose)
            Matrix4x4 desiredWorldMatrix = Matrix4x4.Inverse(bindPoses[boneIdx]);

            // Compute local matrix relative to parent
            Matrix4x4 localMatrix;
            if (parentWorldMatrix.HasValue)
            {
                localMatrix = parentWorldMatrix.Value.inverse * desiredWorldMatrix;
            }
            else
            {
                localMatrix = desiredWorldMatrix;
            }
#if UNITY_EDITOR
            Undo.RecordObject(bone, "Reset Bone to Bind Pose");
#endif
            // Translation: last column (row-major access: m03, m13, m23)
            Vector3 localPosition = new Vector3(
                localMatrix.m03,
                localMatrix.m13,
                localMatrix.m23
            );

            // Rotation: built-in property (safe for uniform/positive scale)
            Quaternion localRotation = localMatrix.rotation;

            // Scale: built-in (lossy but usually accurate enough for bind pose)
            Vector3 localScale = localMatrix.lossyScale;

            bone.localPosition = localPosition;
            bone.localRotation = localRotation;
            bone.localScale   = localScale;

            // Recurse to children
            foreach (Transform child in bone)
            {
                ResetBoneRecurse(child, boneIndex, bindPoses, desiredWorldMatrix);
            }
        }
        public void SetIdle()
        {
            animator.Play("walk", 0, 0f);
            animator.SetFloat("velocity", 0f);     // or whatever value picks your desired motion
            animator.Update(0f);
        }
    }

#if UNITY_EDITOR

    // Custom Editor to display the dropdown for animation states
    [CustomEditor(typeof(PoseSkinnedMeshEditor))]
    public class PoseSkinnedMeshEditorInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            PoseSkinnedMeshEditor script = (PoseSkinnedMeshEditor)target;

            // Draw default inspector fields
            DrawDefaultInspector();

            // Get the state names
            string[] stateNames = (string[])script.GetType().GetField("stateNames", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(script);

            if (stateNames != null && stateNames.Length > 0)
            {
                // Draw the dropdown for animation states
                int selectedIndex = EditorGUILayout.Popup("Animation State", script.selectedStateIndex, stateNames);
                if (selectedIndex != script.selectedStateIndex)
                {
                    script.selectedStateIndex = selectedIndex;
                    script.animationState = stateNames[selectedIndex];
                    EditorUtility.SetDirty(script); // Mark the object as dirty to save changes
                }
            }
            else if (script.clip == null)
            {
                EditorGUILayout.LabelField("No animation states found. Assign a Clip above, or an Animator with a valid Controller.");
            }

            if (script.clip != null)
            {
                EditorGUILayout.HelpBox("Clip mode: sampling '" + script.clip.name + "' directly. The Animator and its Controller are ignored.", MessageType.Info);
            }

            // Button to trigger SetAnimationToFrame
            EditorGUILayout.Space(10);
            if (GUILayout.Button("Set Animation Frame"))
            {
                script.SetAnimationToFrame();
            }

            // Buttons to step forward and backward
            if (GUILayout.Button("Next Frame"))
            {
                script.frame += 1;
                script.SetAnimationToFrame();
            }

            if (GUILayout.Button("Previous Frame"))
            {
                script.frame -= 1;
                script.SetAnimationToFrame();
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Save Posed Mesh Asset"))
            {
                script.SaveBakedPoseMesh();
            }

            EditorGUILayout.Space(10);
            if (GUILayout.Button("RESET TO T-POSE"))
            {
                script.ResetToBindPose();
            }

            if (GUILayout.Button("Set Idle Pose"))
            {
                script.SetIdle();
            }
        }
    }
#endif
}