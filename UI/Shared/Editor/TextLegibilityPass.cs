using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TJ.SteamDeck
{
    /// <summary>
    /// Raises small screen text so it reads at 12 px or more on the Steam Deck: named labels on the screens Valve flagged,
    /// and a 13-unit floor everywhere else. Only ever raises a size. Run it again after rebuilding a generated prefab.
    /// </summary>
    public static class TextLegibilityPass
    {
        // One canvas unit is 0.926 px on the Deck at 125% UI Scale, so 13 units is the smallest size that reaches 12 px.
        public const float Floor = 13f;
        public const string ReportPath = "Temp/TextLegibilityReport.txt";

        static readonly string[] Roots = { "Assets/Data/Prefabs", "Assets/Scripts", "Assets/Memori.BugReporter" };
        static readonly string[] Scenes =
        {
            "Assets/Scenes/Core.unity", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/Map.unity",
            "Assets/Scenes/Tavern.unity", "Assets/Scenes/TavernBattle.unity", "Assets/Scenes/Collection.unity",
        };
        // Drawn in the world or decorative: these keep their sizes.
        static readonly string[] SkipFiles = { "Release Version Text.prefab", "Shop Price Canvas.prefab" };

        #region Targets
        struct Target
        {
            public string File, Contains, EndsWith;
            public float Size, Min, Max;
            public Target(string file, string contains, string endsWith, float size, float min = 0f, float max = 0f)
            { File = file; Contains = contains; EndsWith = endsWith; Size = size; Min = min > 0f ? min : size; Max = max > 0f ? max : size; }
        }

        static readonly Target[] Targets =
        {
            // Battle Guide: shown about 0.86 scale on the Deck once it takes the rail's space.
            new("Battle Guide.prefab", "Templates/", "/Chapter", 15f),
            new("Battle Guide.prefab", "Templates/Keycap/", "/Text", 16f),
            new("Battle Guide.prefab", "Topic Header/", "/Eyebrow", 16f),
            new("Battle Guide.prefab", "See Also/", "/Label", 15f),
            new("Battle Guide.prefab", "Left Column/", "/Caption", 16f, 14f, 16f),
            new("Battle Guide.prefab", "Topic Header/", "/Summary", 20f, 16f, 20f),
            new("Battle Guide.prefab", "Templates/Inline Term/", "/Label", 16f),
            new("Battle Guide.prefab", "Templates/", "/Text", 16f, 15f, 17f),
            new("Battle Guide.prefab", "Templates/See Also Link/", "/Label", 17f, 15f, 17f),
            new("Battle Guide.prefab", "Text Area/", "", 17f, 15f, 17f),
            new("Battle Guide.prefab", "Tip Header/", "/Reason", 18f, 15f, 18f),
            new("Battle Guide.prefab", "Tip Header/", "/Count", 16f, 14f, 16f),
            new("Battle Guide.prefab", "", "/No Results", 16f, 15f, 16f),

            // Select Hero: the Play Panel fits by height only, about 0.8 scale on the Deck.
            new("Commander Screen UI.prefab", "/Heroes/Hero ", "/Name", 16f, 13f, 16f),
            new("Hero Roster Tile.prefab", "", "Name", 16f, 13f, 16f),
            new("Commander Screen UI.prefab", "Heading Faction/", "/Label", 15f),
            new("Commander Screen UI.prefab", "Faction Row/", "/Faction", 15f),
            new("Commander Screen UI.prefab", "Faction Row/", "/Region", 15f),
            new("Commander Screen UI.prefab", "Lore Scroll/", "/Lore", 16f),
            new("Commander Screen UI.prefab", "/Heading ", "/Label", 14f),
            new("Commander Screen UI.prefab", "Title Row/", "/Title", 17f),
            new("Commander Screen UI.prefab", "Title Row/Tag/", "/Text", 13f),
            new("Commander Screen UI.prefab", "/Texts/", "/Body", 16f, 14f, 16f),
            new("Commander Screen UI.prefab", "Label Row/", "/Label", 13f),
            new("Commander Screen UI.prefab", "/Record/", "/State", 14f),
            new("Commander Screen UI.prefab", "/Record/", "/Level", 16f),
            new("Commander Screen UI.prefab", "/Steps/", "/Label", 16f),
            new("Commander Screen UI.prefab", "Coming Soon/", "/Text", 15f),

            // Map hover card: fitted to about 0.86 on the Deck.
            new("Squad Battle Info.prefab", "", "/Stat Name Text", 15f, 15f, 15f),
            new("Squad Battle Info.prefab", "", "/Stat Score Text", 15f),
            new("Squad Battle Info.prefab", "", "/Faction Effect Title", 14f, 13f, 14f),
            new("Squad Battle Info.prefab", "", "/Passive Name Text", 15f, 14f, 15f),
            new("Squad Battle Info.prefab", "", "/Unit Type Text", 16f, 14f, 16f),
            new("Squad Battle Info.prefab", "", "/Spell Category", 13f),
            new("Squad Battle Info.prefab", "", "/Bonus Description", 16f, 13f, 16f),
            new("Unit Stat UI.prefab", "", "Stat Name Text", 15f, 15f, 15f),
            new("Unit Stat UI.prefab", "", "Stat Score Text", 15f),

            // Settings and Collection rail labels.
            new("Rail Section.prefab", "", "", 14f, 13f, 14f),
            new("Stat Strip Cell.prefab", "", "", 14f, 13f, 14f),

            // Map top bar and legend.
            new("Map.unity", "Top Section/", "/Difficulty Text", 18f, 13f, 18f),
            new("Map.unity", "Top Section/", "/Hero Name Text", 18f, 13f, 18f),
            new("Map.unity", "Top Section/", "/Hero Race Text", 18f, 13f, 18f),
            new("Map.unity", "Legend Section/", "/Legend Label_1", 15f, 13f, 15f),
        };
        #endregion

        static Queue<string> s_queue;
        static StringBuilder s_report;
        static int s_changed;

        [MenuItem("Tabletop Tavern/Steam Deck/Apply Text Legibility")]
        public static void RunAll()
        {
            Begin();
            while (Step(float.MaxValue) > 0) { }
            Debug.Log($"TextLegibilityPass: {s_changed} labels raised. Report: {ReportPath}");
        }

        /// <summary>Queues every UI prefab and scene. Call Step until it returns 0.</summary>
        public static int Begin()
        {
            var files = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", Roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (SkipFiles.Any(s => path.EndsWith(s))) continue;
                files.Add(path);
            }
            files.AddRange(Scenes.Where(File.Exists));
            s_queue = new Queue<string>(files);
            s_report = new StringBuilder();
            s_changed = 0;
            return s_queue.Count;
        }

        /// <summary>Processes queued files until the time budget runs out; returns how many are left.</summary>
        public static int Step(float seconds)
        {
            if (s_queue == null) return 0;
            double end = EditorApplication.timeSinceStartup + seconds;
            while (s_queue.Count > 0 && EditorApplication.timeSinceStartup < end)
            {
                string path = s_queue.Dequeue();
                if (path.EndsWith(".unity")) ProcessScene(path);
                else ProcessPrefab(path);
            }
            if (s_queue.Count == 0) File.WriteAllText(ReportPath, $"{s_changed} labels raised\n{s_report}");
            return s_queue.Count;
        }

        static void ProcessPrefab(string path)
        {
            // Scanning the asset first keeps untouched prefabs from being opened and re-saved.
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponentsInChildren<TextMeshProUGUI>(true).Length == 0) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (RaiseAll(root.transform, Path.GetFileName(path), path) > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void ProcessScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasOpen = scene.IsValid() && scene.isLoaded;
            if (wasOpen && scene.isDirty)
            {
                s_report.Append($"SKIPPED {path}: it has unsaved changes from someone else.\n");
                return;
            }
            if (!wasOpen) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            int changed = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                changed += RaiseAll(root.transform, Path.GetFileName(path), path);
            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!wasOpen) EditorSceneManager.CloseScene(scene, true);
        }

        static int RaiseAll(Transform root, string file, string assetPath)
        {
            int changed = 0;
            foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (label.name == "Release Version Text") continue;
                if (InWorld(label))
                {
                    s_report.Append($"world {assetPath} :: {PathOf(label.transform, root)}").Append('\n');
                    continue;
                }
                string labelPath = PathOf(label.transform, root);
                Target? target = Find(file, labelPath) ?? FindThroughInstance(label);
                if (!OwnedHere(label, target.HasValue)) continue;
                if (Raise(label, target))
                {
                    changed++;
                    s_changed++;
                    s_report.Append($"{assetPath} :: {labelPath} -> {(label.enableAutoSizing ? $"{label.fontSizeMin}-{label.fontSizeMax}" : label.fontSize.ToString())}\n");
                }
            }
            return changed;
        }

        // A nested canvas saves as world space in its own prefab file; real world canvases here are scaled down to about 0.01.
        static bool InWorld(TextMeshProUGUI label)
        {
            Canvas canvas = label.GetComponentInParent<Canvas>(true);
            if (canvas == null) return false;
            Canvas root = canvas.rootCanvas;
            return root.renderMode == RenderMode.WorldSpace && root.transform.lossyScale.x < 0.1f;
        }

        // A label inside a nested prefab belongs to that prefab unless this file already overrides its size.
        static bool OwnedHere(TextMeshProUGUI label, bool targeted)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(label)) return true;
            if (targeted) return true;
            var so = new SerializedObject(label);
            return so.FindProperty("m_fontSize").prefabOverride || so.FindProperty("m_fontSizeMin").prefabOverride;
        }

        static Target? Find(string file, string labelPath)
        {
            foreach (Target t in Targets)
            {
                if (t.File != file) continue;
                if (t.Contains.Length > 0 && !labelPath.Contains(t.Contains)) continue;
                if (t.EndsWith.Length > 0 && !labelPath.EndsWith(t.EndsWith)) continue;
                return t;
            }
            return null;
        }

        // An instance can override a targeted label, so the target is looked up in the prefab the label comes from too.
        static Target? FindThroughInstance(TextMeshProUGUI label)
        {
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(label);
            if (instanceRoot == null) return null;
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(label);
            if (string.IsNullOrEmpty(source)) return null;
            string relative = PathOf(label.transform, instanceRoot.transform);
            string sourceRootName = Path.GetFileNameWithoutExtension(source);
            int slash = relative.IndexOf('/');
            relative = slash < 0 ? sourceRootName : sourceRootName + relative.Substring(slash);
            return Find(Path.GetFileName(source), relative);
        }

        static bool Raise(TextMeshProUGUI label, Target? target)
        {
            float size = target.HasValue ? target.Value.Size : Floor;
            float min = target.HasValue ? target.Value.Min : Floor;
            float max = target.HasValue ? target.Value.Max : Floor;
            var so = new SerializedObject(label);
            bool auto = so.FindProperty("m_enableAutoSizing").boolValue;
            bool changed = false;
            if (auto)
            {
                changed |= RaiseTo(so, "m_fontSizeMin", min);
                changed |= RaiseTo(so, "m_fontSizeMax", Mathf.Max(max, so.FindProperty("m_fontSizeMin").floatValue));
                changed |= RaiseTo(so, "m_fontSize", so.FindProperty("m_fontSizeMin").floatValue);
            }
            else
            {
                changed |= RaiseTo(so, "m_fontSize", size);
            }
            if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        static bool RaiseTo(SerializedObject so, string property, float value)
        {
            SerializedProperty p = so.FindProperty(property);
            if (p.floatValue >= value - 0.001f) return false;
            p.floatValue = value;
            return true;
        }

        static string PathOf(Transform t, Transform root)
        {
            var parts = new List<string>();
            for (Transform c = t; c != null; c = c.parent)
            {
                parts.Add(c.name);
                if (c == root) break;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
