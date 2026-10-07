using System;
using System.Text;
using MemoriStudios.TJBake;
using MemoriStudios.TJBake.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TabletopTavern.GpuAnim.Editor
{
    /// <summary>
    /// Builds a TJBake request from this project's unit prefabs: the slot list from the baker component's serialized
    /// data, anchors from its anchor list, props and roles from the GPU animator variant prefab. This is the migration
    /// path for the existing roster; the modder's path is the TJBake window.
    /// </summary>
    public static class UnitVisualExporter
    {
        private const string BakerTypeName = "GpuEcsAnimationBakerBehaviour";
        private const string AttachmentInitMarker = "AttachmentInitializer";

        public sealed class Options
        {
            public string UnitName;
            public string SourcePrefabPath;
            public string VariantPrefabPath;
            public string OutputFolder;
            public bool IsRider;
            public Options Rider;
            public float DefaultReturnAt = 0.9f;
        }

        public static string Export(Options o)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder();
            try
            {
                TJBakeRequest request = BuildRequest(o, preview, report);
                if (o.Rider != null)
                {
                    o.Rider.IsRider = true;
                    request.Rider = BuildRequest(o.Rider, preview, report);
                }
                TJBakeSampler.Bake(request, report);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            return report.ToString();
        }

        private static TJBakeRequest BuildRequest(Options o, Scene preview, StringBuilder report)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(o.SourcePrefabPath);
            if (source == null) throw new Exception("source prefab not found: " + o.SourcePrefabPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            var request = new TJBakeRequest
            {
                UnitName = o.UnitName, IsRider = o.IsRider, OutputFolder = o.OutputFolder, Root = root,
                DefaultReturnAt = o.DefaultReturnAt, BakerName = "TJBake (Tabletop Tavern roster export)",
            };

            Component baker = null;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Name == BakerTypeName) { baker = mb; break; }
            if (baker == null) throw new Exception("no " + BakerTypeName + " on the source prefab");
            var so = new SerializedObject(baker);
            SerializedProperty animations = so.FindProperty("bakerData.animations");
            if (animations == null || !animations.isArray) throw new Exception("bakerData.animations not found");
            for (int i = 0; i < animations.arraySize; i++)
            {
                SerializedProperty a = animations.GetArrayElementAtIndex(i);
                var clip = a.FindPropertyRelative("singleClipData.animationClip").objectReferenceValue as AnimationClip;
                if (clip == null) clip = a.FindPropertyRelative("dualClipBlendData.clip1.animationClip").objectReferenceValue as AnimationClip;
                TJBakeContract.SlotKind(i, o.IsRider, out bool loop, out bool returns);
                request.Slots.Add(new TJBakeSlotRequest { Name = a.FindPropertyRelative("animationID").stringValue, Clip = clip, Loop = loop, ReturnToIdle = returns });
            }
            SerializedProperty anchorList = so.FindProperty("bakerData.attachmentAnchors");
            if (anchorList != null && anchorList.isArray)
                for (int i = 0; i < anchorList.arraySize; i++)
                {
                    SerializedProperty a = anchorList.GetArrayElementAtIndex(i);
                    request.Anchors.Add(new TJBakeAnchorRequest { Name = a.FindPropertyRelative("attachmentAnchorID").stringValue, Transform = a.FindPropertyRelative("attachmentAnchorTransform").objectReferenceValue as Transform });
                }

            if (!string.IsNullOrEmpty(o.VariantPrefabPath))
            {
                var variant = AssetDatabase.LoadAssetAtPath<GameObject>(o.VariantPrefabPath);
                if (variant == null) throw new Exception("variant prefab not found: " + o.VariantPrefabPath);
                var variantRoot = (GameObject)PrefabUtility.InstantiatePrefab(variant, preview);
                request.RootScale = variantRoot.transform.localScale.x;
                foreach (MonoBehaviour mb in variantRoot.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || !mb.GetType().Name.Contains(AttachmentInitMarker)) continue;
                    SerializedProperty idProp = new SerializedObject(mb).FindProperty("attachmentAnchorId");
                    int anchorId = idProp != null ? idProp.intValue : -1;
                    if (anchorId < 0 || anchorId >= request.Anchors.Count) { report.AppendLine($"  skipped {mb.gameObject.name}: anchor id {anchorId}"); continue; }
                    request.Attachments.Add(new TJBakeAttachmentRequest { Name = mb.gameObject.name, Holder = mb.transform, Anchor = request.Anchors[anchorId].Name, Role = RoleOf(mb.transform) });
                }
            }
            return request;
        }

        private static TJBakePropRole RoleOf(Transform holder)
        {
            if (holder.GetComponentInChildren<BowSetUpAuthoring>(true) != null) return TJBakePropRole.Bow;
            if (holder.GetComponentInChildren<SwordSetUpAuthoring>(true) != null) return TJBakePropRole.Sword;
            if (holder.GetComponentInChildren<ShieldSetUpAuthoring>(true) != null) return TJBakePropRole.Shield;
            if (holder.GetComponentInChildren<SaddleSetUpAuthoring>(true) != null) return TJBakePropRole.Saddle;
            return TJBakePropRole.Prop;
        }
    }
}
