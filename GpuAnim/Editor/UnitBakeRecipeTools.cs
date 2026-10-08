using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using MemoriStudios.TJBake;
using MemoriStudios.TJBake.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TabletopTavern.GpuAnim.Editor
{
    /// <summary>Bakes unit recipes into StreamingAssets/UnitVisuals.</summary>
    public static class UnitBakeRecipeTools
    {
        public const string OutputRoot = "Assets/StreamingAssets/UnitVisuals";

        #region Bake

        [MenuItem("Tools/TJBake/Bake Selected Unit Recipes")]
        private static void BakeSelected()
        {
            var recipes = new List<UnitBakeRecipe>();
            foreach (UnityEngine.Object o in Selection.objects)
                if (o is UnitBakeRecipe recipe) recipes.Add(recipe);
            var report = new StringBuilder();
            try
            {
                for (int i = 0; i < recipes.Count; i++) report.AppendLine(BakeToDefault(recipes[i], ProgressBar(recipes[i], i, recipes.Count)));
            }
            catch (OperationCanceledException) { report.AppendLine("Cancelled. The unit being baked kept its old folder."); }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[TJBake] " + report);
        }

        [MenuItem("Tools/TJBake/Bake All Unit Recipes")]
        private static void BakeAll()
        {
            var report = new StringBuilder();
            string[] guids = AssetDatabase.FindAssets("t:UnitBakeRecipe");
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    var recipe = AssetDatabase.LoadAssetAtPath<UnitBakeRecipe>(AssetDatabase.GUIDToAssetPath(guids[i]));
                    try { report.AppendLine(BakeToDefault(recipe, ProgressBar(recipe, i, guids.Length))); }
                    catch (OperationCanceledException)
                    {
                        report.AppendLine($"Cancelled at {recipe.unitName} ({i + 1} of {guids.Length}). It and every unit after it kept their old folders.");
                        break;
                    }
                    catch (Exception e) { report.AppendLine($"{recipe.unitName}: FAILED {e.Message}"); }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            Debug.Log("[TJBake] " + report);
        }

        private static Func<string, float, bool> ProgressBar(UnitBakeRecipe recipe, int index, int count) =>
            (step, fraction) => EditorUtility.DisplayCancelableProgressBar("TJBake", $"{recipe.unitName} ({index + 1} of {count}): {step}", (index + fraction) / count);

        public static string BakeToDefault(UnitBakeRecipe recipe, Func<string, float, bool> progress = null) =>
            Bake(recipe, Path.GetFullPath(Path.Combine(OutputRoot, recipe.unitName)), progress);

        /// <summary>Bakes one recipe into <paramref name="outputFolder"/>, replacing what is there only when the bake succeeds. Returns the bake report.</summary>
        public static string Bake(UnitBakeRecipe recipe, string outputFolder, Func<string, float, bool> progress = null)
        {
            if (recipe.variants.Count == 0 || recipe.variants[0].rig == null) throw new Exception($"{recipe.name}: no first variant rig");
            string staging = SiblingFolder(outputFolder, "baking");
            if (Directory.Exists(staging)) Retry(() => Directory.Delete(staging, true));
            Scene preview = EditorSceneManager.NewPreviewScene();
            var report = new StringBuilder();
            try
            {
                if (progress != null && progress("placing rigs", 0f)) throw new OperationCanceledException("TJBake: bake cancelled");
                GameObject root = Place(recipe.variants[0].rig, preview);
                MountRigs(root, recipe.variants[0].attachedRigs, recipe.anchors);
                var request = new TJBakeRequest
                {
                    UnitName = recipe.unitName, OutputFolder = staging, Root = root, RootScale = recipe.rootScale,
                    BakerName = "TJBake (Tabletop Tavern unit recipe)", TextureReference = UnitVisualTextureAddresses.Reference,
                    PlaceholderWhenNoMesh = recipe.placeholderWhenNoMesh, Progress = progress,
                };
                AddSlots(request, recipe.slots, false);
                AddAnchors(request, recipe.anchors, root);
                AddProps(request.Attachments, recipe.variants[0].props, preview);
                for (int v = 1; v < recipe.variants.Count; v++)
                {
                    var extra = new TJBakeVariantRequest { Root = Place(recipe.variants[v].rig, preview) };
                    MountRigs(extra.Root, recipe.variants[v].attachedRigs, recipe.anchors);
                    AddProps(extra.Attachments, recipe.variants[v].props, preview);
                    request.ExtraVariants.Add(extra);
                }
                if (recipe.hasRider)
                {
                    GameObject riderRoot = Place(recipe.rider.rig, preview);
                    var rider = new TJBakeRequest { UnitName = recipe.unitName, IsRider = true, Root = riderRoot, RootScale = recipe.rider.rootScale, RootOffset = recipe.rider.rootOffset, BakerName = request.BakerName };
                    AddSlots(rider, recipe.rider.slots, true);
                    AddAnchors(rider, recipe.rider.anchors, riderRoot);
                    AddProps(rider.Attachments, recipe.rider.props, preview);
                    request.Rider = rider;
                }
                TJBakeSampler.Bake(request, report);
                SwapIn(staging, outputFolder);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (Directory.Exists(staging)) TryDelete(staging);
            }
            return report.ToString();
        }

        // Antivirus or the indexer can hold a just-written folder for a moment, so a rename or delete is retried.
        private static void Retry(Action io)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    io();
                    return;
                }
                catch (IOException) when (attempt < IoAttempts) { }
                catch (UnauthorizedAccessException) when (attempt < IoAttempts) { }
                Thread.Sleep(100 * attempt);
            }
        }

        private const int IoAttempts = 10;

        // A cleanup must not throw over a cancel or a finished swap; a leftover is skipped by the loader and Unity and removed by the next bake.
        private static void TryDelete(string folder)
        {
            try { Retry(() => Directory.Delete(folder, true)); }
            catch (Exception e) { Debug.LogWarning($"[TJBake] could not remove {folder}: {e.Message}"); }
        }

        // The underscore keeps UnitVisualOverrideLoader off a leftover folder; the tilde keeps Unity from importing it.
        private static string SiblingFolder(string folder, string purpose) =>
            Path.Combine(Path.GetDirectoryName(folder), $"_{Path.GetFileName(folder)}.{purpose}~");

        // A cancelled or failed bake never reaches here, so the unit keeps its old folder and never spawns without a visual.
        private static void SwapIn(string staging, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Retry(() => Directory.Move(staging, outputFolder));
                return;
            }
            CarryMetas(outputFolder, staging);
            string old = SiblingFolder(outputFolder, "old");
            if (Directory.Exists(old)) Retry(() => Directory.Delete(old, true));
            Retry(() => Directory.Move(outputFolder, old));
            try { Retry(() => Directory.Move(staging, outputFolder)); }
            catch
            {
                Retry(() => Directory.Move(old, outputFolder));
                throw;
            }
            TryDelete(old);
        }

        // Keeping each file's .meta keeps its GUID, so an unchanged re-bake leaves the folder byte-identical.
        private static void CarryMetas(string from, string to)
        {
            foreach (string meta in Directory.GetFiles(from, "*.meta", SearchOption.AllDirectories))
            {
                string target = Path.Combine(to, Path.GetRelativePath(from, meta));
                string asset = target.Substring(0, target.Length - ".meta".Length);
                if (File.Exists(asset) || Directory.Exists(asset)) File.Copy(meta, target, true);
            }
        }

        // A mounted rig follows its anchor bone exactly, as the old attachment did at runtime.
        private static void MountRigs(GameObject root, List<UnitBakeRecipe.AttachedRig> rigs, List<UnitBakeRecipe.Anchor> anchors)
        {
            foreach (UnitBakeRecipe.AttachedRig mounted in rigs)
            {
                UnitBakeRecipe.Anchor anchor = anchors.Find(a => a.name == mounted.anchor);
                Transform bone = anchor == null ? null : string.IsNullOrEmpty(anchor.bonePath) ? root.transform : root.transform.Find(anchor.bonePath);
                if (bone == null || mounted.rig == null) throw new Exception($"attached rig on anchor '{mounted.anchor}' cannot be placed");
                var go = (GameObject)PrefabUtility.InstantiatePrefab(mounted.rig, bone);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
        }

        private static GameObject Place(GameObject prefab, Scene preview)
        {
            if (prefab == null) throw new Exception("a recipe rig is missing");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            return go;
        }

        private static void AddSlots(TJBakeRequest request, List<UnitBakeRecipe.Slot> slots, bool rider)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                UnitBakeRecipe.Slot s = slots[i];
                TJBakeContract.SlotKind(i, rider, out bool loop, out bool returns);
                var slot = new TJBakeSlotRequest { Name = s.name, Clip = s.clip, Loop = loop, ReturnToIdle = returns, StateName = s.stateName };
                foreach (UnitBakeRecipe.Parameter p in s.parameters)
                    slot.Parameters.Add(new TJBakeParameter { Name = p.name, Type = p.type, Float = p.floatValue, Int = p.intValue, Bool = p.boolValue });
                request.Slots.Add(slot);
            }
        }

        private static void AddAnchors(TJBakeRequest request, List<UnitBakeRecipe.Anchor> anchors, GameObject root)
        {
            foreach (UnitBakeRecipe.Anchor a in anchors)
            {
                Transform t = string.IsNullOrEmpty(a.bonePath) ? root.transform : root.transform.Find(a.bonePath);
                if (t == null) throw new Exception($"anchor {a.name}: no bone at '{a.bonePath}' under {root.name}");
                request.Anchors.Add(new TJBakeAnchorRequest { Name = a.name, Transform = t });
            }
        }

        private static void AddProps(List<TJBakeAttachmentRequest> into, List<UnitBakeRecipe.Prop> props, Scene preview)
        {
            foreach (UnitBakeRecipe.Prop p in props)
            {
                if (p.prefab == null) throw new Exception($"prop {p.name} has no prefab");
                GameObject holder = Place(p.prefab, preview);
                string tag = holder.GetComponentInChildren<ShieldRandomizerAuthoring>(true) != null ? UnitVisualTags.RandomShield : "";
                into.Add(new TJBakeAttachmentRequest { Name = p.name, Holder = holder.transform, Anchor = p.anchor, Role = p.role, Tag = tag });
            }
        }

        #endregion

    }
}
