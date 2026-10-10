using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TJ.Campfire.EditorTools
{
    /// <summary>
    /// Generates the campfire panel (Campfire Panel UI.prefab and its parts) on the game's Basic Background and installs it
    /// in Map.unity. Rebuilding the panel keeps hand edits to the part prefabs.
    /// </summary>
    public static class CampfirePanelBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Map/Campfire";
        public const string PanelPath = PartFolder + "/Campfire Panel UI.prefab";
        const string ColumnPath = PartFolder + "/Campfire Choice Column.prefab";
        const string SlotPath = PartFolder + "/Campfire Train Slot.prefab";
        const string StatPlaquePath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Stat Plaque.prefab";
        const string BandGradientPath = "Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01.png";
        const string ScenePath = "Assets/Scenes/Map.unity";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string SquadCardPath = "Assets/Data/Prefabs/UI/Squad Display Cards/Squad Display Card Menu Variant.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";
        static readonly string[] OldChildren = { "Options Panel", "Results Panel", "Train Picker" };
        const string ContinueName = "Continue Parent";
        const string HaloFlarePath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Halo Flare.prefab";
        const float HaloSize = 112f;
        const float PanelWidth = 1160f;
        // The card's top edge sits 200 px below the top of a 1080 screen, so every state keeps its header in place.
        // The anchor height brings it to 130 on an 864-tall canvas (UI Scale 125%), clear of the Campfire banner.
        const float PanelAnchorY = 0.676f;
        const float PanelTopY = 150f;
        const float BandAlpha = 0.12f;
        const float PanelPadding = 5f;
        const float HeaderHeight = 92f;
        const float TextureTop = 103f;
        const float TexturePixelsPerUnit = 12f;
        // Four equal whole-pixel columns and three 1 px dividers fill the panel inside its 5 px frame edge.
        const float ColumnWidth = 286f;
        const float ChoiceButtonWidth = 246f;
        const float SlotWidth = 66f;
        const float SlotGap = 6f;
        // Space alone separates the reserve slots from the deployed ten.
        const float TrayGroupGap = 24f;
        const float PlaqueGap = 10f;
        const float PlaqueRowGap = 6f;
        const int DeployedSlots = 10;
        const int ReserveSlots = 3;

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Well = Hex("162023");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Sub = Hex("B4AA94");
        static readonly Color Flavour = Hex("A99F8A");
        static readonly Color Positive = Hex("7BD66F");
        static readonly Color Coin = Hex("E3BB71");
        static readonly Color Fire = Hex("E88C40");
        static readonly Color WellEdge = Hex("605635", 0.9f);
        static readonly Color MarkEmpty = Hex("56605F");

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, solid, frameEdge, bandGradient;
        static Sprite fireIcon, healIcon, prestigeIcon, gearIcon, eyeIcon, goldIcon, chestIcon, mapIcon;
        static GameObject standardButton, backButton, basicBackground, statPlaque;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            sheet.TryGetValue("TooltipSolid", out solid);
            if (mount == null || solid == null) Debug.LogError("CampfirePanelBuilder: tooltip sheet sprites missing.");
                        frameEdge = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Frame_Box_Small01.png");
            bandGradient = Load<Sprite>(BandGradientPath);
            fireIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Status/ICON_FantasyWarrior_Status_Burninating01_Clean.png");
            healIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/Icons_Map/ICON_FantasyWarrior_Map_Healing01_Clean.png");
            prestigeIcon = Load<Sprite>("Assets/Art/Icons/Events/PrestigeUnit.png");
            gearIcon = Load<Sprite>("Assets/Art/Icons/Events/GearDrop.png");
            eyeIcon = Load<Sprite>("Assets/Art/Icons/Stats/Range.png");
            goldIcon = Load<Sprite>("Assets/Art/Icons/Events/Gold.png");
            chestIcon = Load<Sprite>("Assets/Art/Icons/Map/Treasure.png");
            mapIcon = Load<Sprite>("Assets/Art/Icons/Map/Unknown.png");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            backButton = Load<GameObject>(ButtonFolder + "/Button - Back.prefab");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            statPlaque = Load<GameObject>(StatPlaquePath);
        }

        // The view writes into every plaque Value, so a panel built without them would fail at runtime.
        static bool PlaqueLoaded()
        {
            if (statPlaque != null) return true;
            Debug.LogError($"CampfirePanelBuilder: no Stat Plaque at {StatPlaquePath}; nothing was rebuilt.");
            return false;
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"CampfirePanelBuilder: missing {typeof(T).Name} at {path}");
            return asset;
        }

        static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color colour);
            colour.a = alpha;
            return colour;
        }

        static Color A(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
        #endregion

        #region Localization
        // English source text for the keys this panel adds. Other locales are filled in separately.
        static readonly (string key, string english)[] Keys =
        {
            ("campfirePickOne", "Pick One"),
            ("campfireReadyTitle", "Ready to March"),
            ("campfireRestLine", "Every squad heals {0}% of its health, reserves too."),
            ("campfireNoRisk", "No gold, no risk."),
            ("campfireTrainLine", "{0} of your {1} squads can train."),
            ("campfireTrainPrices", "Prestige 1 costs {0} gold, Prestige 2 costs {1}."),
            ("campfireScavengeLine", "See {0} gear items and keep one."),
            ("campfireScoutLine", "Every hidden node in this act is revealed."),
            ("campfireScoutConsumable", "You also find a consumable."),
            ("campfireScoutSlotsFull", "Your consumable slots are full."),
            ("campfireChooseSquad", "Choose a Squad"),
            ("campfireNoSquadCanTrain", "No squad can train"),
            ("campfireTrainTitle", "Choose a squad to train"),
            ("campfireCanTrainCount", "{0} of {1} can train"),
            ("campfireMax", "Max"),
            ("campfireTrainDetail", "{0}: Prestige {1} for {2}"),
            ("campfireTrainDetailMax", "{0} is already at Prestige 2."),
            ("campfireCostRange", "{0} or {1}"),
            ("campfireGearValue", "1 of {0}"),
            ("campfireFree", "Free"),
            ("campfireCaptionGear", "Gear"),
            ("campfireCaptionReveals", "Reveals"),
            ("campfireCaptionSquad", "Squad"),
            ("campfireCaptionSquads", "Squads"),
            ("campfireCaptionFound", "Found"),
            ("campfireThisAct", "This act"),
            ("campfireNothing", "Nothing"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/Campfire Panel/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("CampfirePanelBuilder: " + EnsureKeys());

        /// <summary>Adds any missing campfire key with its English text. Existing keys and their text are left alone.</summary>
        public static string EnsureKeys()
        {
            LoadTable();
            int added = 0;
            foreach ((string key, string text) in Keys)
            {
                if (collection.SharedData.GetEntry(key) != null) continue;
                collection.SharedData.AddKey(key);
                english.AddEntry(key, text);
                added++;
            }
            if (added == 0) return "all keys present.";
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(english);
            AssetDatabase.SaveAssetIfDirty(collection.SharedData);
            AssetDatabase.SaveAssetIfDirty(english);
            return $"added {added} keys.";
        }

        static long KeyId(string key)
        {
            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);
            if (entry == null) throw new InvalidOperationException($"CampfirePanelBuilder: no localization key '{key}'.");
            return entry.Id;
        }

        static string English(string key)
        {
            StringTableEntry entry = english.GetEntry(key);
            return entry != null ? entry.Value : key;
        }

        // Points the text's localizer at a key, adding one when the text has none. Shows the English value in the Editor.
        static void Localize(TMP_Text text, string key)
        {
            LocalizeStringEvent localizer = text.GetComponent<LocalizeStringEvent>();
            if (localizer == null)
            {
                localizer = text.gameObject.AddComponent<LocalizeStringEvent>();
                var setter = (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), text, typeof(TMP_Text).GetProperty("text").GetSetMethod());
                UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setter);
            }
            var so = new SerializedObject(localizer);
            so.FindProperty("m_StringReference.m_TableReference.m_TableCollectionName").stringValue = "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            so.FindProperty("m_StringReference.m_TableEntryReference.m_KeyId").longValue = KeyId(key);
            so.FindProperty("m_StringReference.m_TableEntryReference.m_Key").stringValue = "";
            so.ApplyModifiedPropertiesWithoutUndo();
            text.text = English(key);
        }

        // A text the panel fills at runtime must not keep a localizer, or a locale change would reset it.
        static void Unlocalize(TMP_Text text, string placeholder)
        {
            foreach (LocalizeStringEvent localizer in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(localizer);
            text.text = placeholder;
        }
        #endregion

        #region Entry points
        /// <summary>Adds missing keys, creates any missing part prefab, then rebuilds the panel from them.</summary>
        [MenuItem("Tabletop Tavern/Campfire Panel/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            if (!PlaqueLoaded()) return;
            EnsureParts(false);
            if (!PartsCurrent()) return;
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Campfire Panel/Reset Part Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset campfire panel parts",
                    $"The part prefabs in {PartFolder} are rebuilt from code, which discards your edits to them. The panel is rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetParts();
        }

        /// <summary>Rebuilds both part prefabs from code, then the panel.</summary>
        public static void ResetParts()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            if (!PlaqueLoaded()) return;
            EnsureParts(true);
            BuildPanel();
        }

        [MenuItem("Tabletop Tavern/Campfire Panel/Install In Map")]
        public static void InstallMenu() => Debug.Log("CampfirePanelBuilder: " + InstallInMap());

        static void BuildPanel()
        {
            // Rebuild inside the existing prefab so the root keeps its id; the Map scene instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath) != null;
            Scene stage = default;
            GameObject root;
            if (existing) root = PrefabUtility.LoadPrefabContents(PanelPath);
            else
            {
                // A first build happens in a preview scene so no open scene is marked changed.
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Campfire Panel UI", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                Build(root);
                Normalize(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
                Debug.Log($"CampfirePanelBuilder: wrote {PanelPath}");
            }
            finally
            {
                if (existing) PrefabUtility.UnloadPrefabContents(root);
                else
                {
                    Object.DestroyImmediate(root);
                    EditorSceneManager.ClosePreviewScene(stage);
                }
            }
        }

        /// <summary>
        /// Replaces the old campfire UI under Map.unity's Campfire Panel with the prefab, moves Continue into the corner slot
        /// the Town panel uses, points CampfirePanel at the new parts and saves only Map.unity.
        /// </summary>
        public static string InstallInMap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);
            if (prefab == null) return $"no prefab at {PanelPath}; run Rebuild Prefab first.";
            Scene map = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!map.isLoaded)
            {
                map = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            try
            {
                if (map.isDirty && !opened) return "Map.unity has unsaved changes; save or revert them first.";
                CampfirePanel campfire = Find<CampfirePanel>(map);
                if (campfire == null) return "no CampfirePanel in Map.unity.";

                // Continue lives in the old results pop-up; it moves to the panel root before that pop-up goes.
                Transform continueParent = FindDeep(campfire.transform, ContinueName);
                if (continueParent != null && continueParent.parent != campfire.transform)
                    continueParent.SetParent(campfire.transform, false);

                var old = new List<Transform>();
                foreach (string name in OldChildren)
                {
                    Transform child = campfire.transform.Find(name);
                    if (child != null) old.Add(child);
                }
                string outside = CheckReferences(map, old, campfire);
                if (!string.IsNullOrEmpty(outside)) return "stopped, outside references into the old UI:\n" + outside;

                int index = campfire.transform.childCount;
                foreach (Transform child in old)
                {
                    index = Mathf.Min(index, child.GetSiblingIndex());
                    Object.DestroyImmediate(child.gameObject);
                }
                for (int i = campfire.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = campfire.transform.GetChild(i).gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child) == prefab)
                    {
                        index = Mathf.Min(index, i);
                        Object.DestroyImmediate(child);
                    }
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, campfire.transform);
                instance.transform.SetSiblingIndex(Mathf.Min(index, campfire.transform.childCount - 1));
                if (continueParent != null)
                {
                    continueParent.SetAsLastSibling();
                    CopyCornerSlot(map, (RectTransform)continueParent);
                }

                var so = new SerializedObject(campfire);
                Ref(so, "view", instance.GetComponent<CampfirePanelView>());
                Ref(so, "squadCardPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(SquadCardPath).GetComponent<SquadDisplayCardMenu>());
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(map);
                if (!EditorSceneManager.SaveScene(map)) return "Map.unity did not save.";
                return $"installed; removed {old.Count} old objects" + (continueParent != null ? ", Continue moved to the corner slot." : ", no Continue found.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(map, true);
            }
        }

        // The Town panel's Continue already sits in the Engagement panel's corner slot, so its placement is copied.
        static void CopyCornerSlot(Scene map, RectTransform target)
        {
            TJ.Town.TownPanel town = Find<TJ.Town.TownPanel>(map);
            if (town == null) { Debug.LogError("CampfirePanelBuilder: no TownPanel to copy the corner slot from."); return; }
            var townButton = new SerializedObject(town).FindProperty("continueButton").objectReferenceValue as Button;
            if (townButton == null) { Debug.LogError("CampfirePanelBuilder: TownPanel has no Continue to copy."); return; }
            var source = (RectTransform)townButton.transform.parent;
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition;
            target.localScale = source.localScale;
            UIFitToCanvas sourceFit = source.GetComponent<UIFitToCanvas>();
            if (sourceFit == null) return;
            UIFitToCanvas fit = target.GetComponent<UIFitToCanvas>();
            if (fit == null) fit = target.gameObject.AddComponent<UIFitToCanvas>();
            ComponentUtility.CopyComponent(sourceFit);
            ComponentUtility.PasteComponentValues(fit);
        }

        static T Find<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // Anything outside the old UI that points into it would lose its target when the old UI is deleted.
        static string CheckReferences(Scene scene, List<Transform> old, CampfirePanel campfire)
        {
            var inside = new HashSet<Object>();
            foreach (Transform root in old)
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    inside.Add(t.gameObject);
                    foreach (Component component in t.GetComponents<Component>())
                        if (component != null) inside.Add(component);
                }
            var report = new System.Text.StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (inside.Contains(t.gameObject)) continue;
                    foreach (Component component in t.GetComponents<Component>())
                    {
                        if (component == null || component == campfire) continue;
                        var so = new SerializedObject(component);
                        SerializedProperty property = so.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (component is Transform && (property.propertyPath == "m_Father" || property.propertyPath.StartsWith("m_Children"))) continue;
                            Object target = property.objectReferenceValue;
                            if (target == null || !inside.Contains(target)) continue;
                            report.AppendLine($"{t.name} {component.GetType().Name}.{property.propertyPath} -> {target.name}");
                        }
                    }
                }
            return report.ToString();
        }

        // Store what an instance computes on load, so the scene instance does not record layout and TMP values as overrides.
        static void Normalize(GameObject root)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var so = new SerializedObject(text);
                so.FindProperty("m_fontColor32").colorValue = text.color;
                so.FindProperty("m_TextStyleHashCode").intValue = TMP_Style.NormalStyle.hashCode;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Canvas.ForceUpdateCanvases();
            for (int pass = 0; pass < 2; pass++)
                foreach (RectTransform rect in root.GetComponentsInChildren<RectTransform>())
                    if (rect.GetComponent<LayoutGroup>() != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }

        // Direct writes to a nested prefab instance are dropped on save unless they are recorded as overrides.
        static void RecordOverrides(GameObject root)
        {
            foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(node.gameObject) == null) continue;
                PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
                foreach (Component component in node.GetComponents<Component>())
                    if (component != null && PrefabUtility.GetCorrespondingObjectFromSource(component) != null)
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
        }
        #endregion

        #region Part prefabs
        static GameObject columnPart, slotPart;

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Map", "Campfire");
            columnPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ColumnPath);
            if (columnPart == null) columnPart = SavePart(ColumnPath, "Campfire Choice Column", ColumnPart);
            slotPart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
            if (slotPart == null) slotPart = SavePart(SlotPath, "Campfire Train Slot", SlotPart);
        }

        // A column part saved before the Stat Plaque strip has no plaques to fill; only Reset Part Prefabs replaces it.
        static bool PartsCurrent()
        {
            if (columnPart.transform.Find("Strip/Plaque 1") != null) return true;
            Debug.LogError($"CampfirePanelBuilder: {ColumnPath} has no Stat Plaques; run Tabletop Tavern > Campfire Panel > Reset Part Prefabs.");
            return false;
        }

        static GameObject SavePart(string path, string name, Action<GameObject> build)
        {
            // Built in a preview scene so the open scenes are never touched or marked changed.
            Scene stage = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
                root.layer = 5;
                build(root);
                Normalize(root);
                RecordOverrides(root);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"CampfirePanelBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // One choice: head, two stat plaques, two lines on what it does, and its button.
        static void ColumnPart(GameObject go)
        {
            RectTransform column = (RectTransform)go.transform;
            VerticalLayoutGroup layout = VLayout(column, 14f, new RectOffset(20, 20, 18, 20));
            layout.childForceExpandWidth = true;
            // Fixed, because a text's preferred width ignores wrapping and would widen whichever column holds the longer line.
            Fixed(go, ColumnWidth, -1f);

            RectTransform head = Rect("Head", column);
            HLayout(head, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Mount(head, "Mount", 44f, healIcon, 20f);
            RectTransform titles = Rect("Titles", head);
            VLayout(titles, 1f, new RectOffset());
            Flexible(titles.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", titles, displayDrop, 22f, Gold, "Title");
            title.enableAutoSizing = true;
            title.fontSizeMin = 15f;
            title.fontSizeMax = 22f;

            // One plaque per row: two side by side overflow the 246 px column ("Reveals" beside "This act").
            RectTransform strip = Rect("Strip", column);
            VLayout(strip, PlaqueRowGap, new RectOffset());
            for (int i = 0; i < 2; i++) Instance(statPlaque, strip, "Plaque " + (i + 1));

            RectTransform body = Rect("Body", column);
            VLayout(body, 10f, new RectOffset());
            // Keeps the buttons of all four columns on one line whatever the text length.
            GetOrAdd<LayoutElement>(body.gameObject).minHeight = 96f;
            Line(body, "Line 1", healIcon);
            Line(body, "Line 2", goldIcon);

            Spacer(column);
            RectTransform action = Rect("Action", column);
            Fixed(action.gameObject, -1f, 62f);
            HLayout(action, 8f, TextAnchor.MiddleCenter, new RectOffset());
            GameObject button = Instance(standardButton, action, "Button");
            Fixed(button, ChoiceButtonWidth, 62f);
            TMP_Text unavailable = Text("Unavailable", action, display, 16f, Flavour, "Unavailable");
            unavailable.fontStyle = FontStyles.Italic;
            unavailable.alignment = TextAlignmentOptions.Center;
            Wrap(unavailable);
            unavailable.gameObject.SetActive(false);
        }

        // One squad in the Train strip: its army card, two Prestige marks and its price. The whole slot is the button.
        static void SlotPart(GameObject go)
        {
            RectTransform slot = (RectTransform)go.transform;
            VerticalLayoutGroup layout = VLayout(slot, 4f, new RectOffset(0, 0, 3, 0));
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            Fixed(go, SlotWidth, 172f);
            Image hit = Img(slot, null, Color.clear);
            hit.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            RectTransform holder = Rect("Card Holder", slot);
            Fixed(holder.gameObject, 60f, 130f);
            Image ring = Img(Stretch(Rect("Hover Ring", holder), -3f, -3f, -3f, -3f), frameEdge, Gold, Image.Type.Sliced);
            ring.fillCenter = false;
            ring.pixelsPerUnitMultiplier = 4f;
            ring.enabled = false;

            RectTransform marks = Rect("Marks", slot);
            HLayout(marks, 7f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(marks.gameObject, SlotWidth, 12f);
            var markImages = new Object[2];
            for (int i = 0; i < 2; i++)
            {
                Image mark = Img(Rect("Mark " + (i + 1), marks), null, MarkEmpty);
                Fixed(mark.gameObject, 8f, 8f);
                mark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                markImages[i] = mark;
            }

            TMP_Text price = Text("Price", slot, displayDrop, 15f, Cream, "10");
            price.alignment = TextAlignmentOptions.Center;
            price.enableAutoSizing = true;
            price.fontSizeMin = 13f;
            price.fontSizeMax = 15f;
            Fixed(price.gameObject, SlotWidth, 18f);

            CampfireTrainSlot trainSlot = go.AddComponent<CampfireTrainSlot>();
            var so = new SerializedObject(trainSlot);
            Ref(so, "button", button);
            Ref(so, "cardHolder", holder);
            Ref(so, "hoverRing", ring);
            Refs(so, "marks", markImages);
            Ref(so, "priceText", price);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Layout
        static void Build(GameObject rootGo)
        {
            rootGo.layer = 5;
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = root.anchorMax = new Vector2(0.5f, PanelAnchorY);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(PanelWidth, 460f);
            root.anchoredPosition = new Vector2(0f, PanelTopY);
            // Keeps its 1080 size on screen when UI Scale shortens the canvas (0.8 at 125%), so it never crowds the map.
            UIFitToCanvas fit = GetOrAdd<UIFitToCanvas>(rootGo);
            var fitSo = new SerializedObject(fit);
            fitSo.FindProperty("designSize").vector2Value = new Vector2(1f, 1080f);
            fitSo.FindProperty("reservedSize").vector2Value = Vector2.zero;
            fitSo.FindProperty("minScale").floatValue = 0.5f;
            fitSo.ApplyModifiedPropertiesWithoutUndo();
            VerticalLayoutGroup rootLayout = GetOrAdd<VerticalLayoutGroup>(rootGo);
            rootLayout.padding = new RectOffset((int)PanelPadding, (int)PanelPadding, (int)PanelPadding, (int)PanelPadding);
            rootLayout.spacing = 0f;
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;
            ContentSizeFitter fitter = GetOrAdd<ContentSizeFitter>(rootGo);
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            CampfirePanelView view = GetOrAdd<CampfirePanelView>(rootGo);
            var so = new SerializedObject(view);

            // The standard panel background most of the game's UI already uses.
            GameObject background = Instance(basicBackground, root, "Basic Background");
            Stretch((RectTransform)background.transform);
            Ignore(background);
            Image fill = Child<Image>(background.transform, "SPR_Background");
            if (fill != null) fill.raycastTarget = true;
            RectTransform texture = Child<RectTransform>(background.transform, "Texture");
            if (texture != null)
            {
                texture.offsetMax = new Vector2(texture.offsetMax.x, -TextureTop);
                Image textureImage = texture.GetComponent<Image>();
                if (textureImage != null) textureImage.pixelsPerUnitMultiplier = TexturePixelsPerUnit;
            }

            // A firelight wash inside the background, under its frame and corner ornaments, falling from the top edge over the header.
            Image band = Img(Rect("Band", background.transform), bandGradient, A(Fire, BandAlpha));
            RectTransform bandRect = band.rectTransform;
            bandRect.anchorMin = new Vector2(0f, 1f);
            bandRect.anchorMax = Vector2.one;
            bandRect.pivot = new Vector2(0.5f, 1f);
            bandRect.offsetMin = new Vector2(0f, -(PanelPadding + HeaderHeight));
            bandRect.offsetMax = Vector2.zero;
            if (texture != null) band.transform.SetSiblingIndex(texture.GetSiblingIndex() + 1);

            BuildHeader(root, so);
            BuildChoosing(root, so);
            BuildTrain(root, so);
            BuildResult(root, so);
            Dress(root, "Mount", new Vector2(130f, 130f));

            Ref(so, "restIcon", healIcon);
            Ref(so, "trainIcon", prestigeIcon);
            Ref(so, "scavengeIcon", gearIcon);
            Ref(so, "scoutIcon", eyeIcon);
            Ref(so, "slotPrefab", slotPart.GetComponent<CampfireTrainSlot>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        #region Synty dressing
        // ui-design.md, Art richness: the shared crest on the panel's top edge, the shared divider in each column split,
        // and one pulsing glow behind the focal element. Runs after Build and finds its targets by name.
        const string CrestPath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Panel Crest.prefab";
        const string DividerPath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Column Divider.prefab";
        const string SyntyGlow = "Assets/Synty/InterfaceFantasyMenus/Sprites/FX/SPR_FantasyMenus_FX_Glow_01.png";

        static void Dress(RectTransform panel, string focalName, Vector2 glowSize)
        {
            GameObject crest = AssetDatabase.LoadAssetAtPath<GameObject>(CrestPath);
            GameObject divider = AssetDatabase.LoadAssetAtPath<GameObject>(DividerPath);
            Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(SyntyGlow);
            if (crest == null || divider == null || glow == null) { Debug.LogError("Synty dressing assets missing."); return; }

            PrefabUtility.InstantiatePrefab(crest, panel);

            // Column splits are one-unit-wide "Divider" cells; the shared divider replaces their plain "Line".
            var cells = new List<Transform>();
            foreach (RectTransform t in panel.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name != "Divider" || t.Find("Line") == null) continue;
                LayoutElement cell = t.GetComponent<LayoutElement>();
                if (cell != null && cell.preferredWidth > 0f && cell.preferredWidth <= 2f) cells.Add(t);
            }
            foreach (Transform cell in cells)
            {
                Object.DestroyImmediate(cell.Find("Line").gameObject);
                PrefabUtility.InstantiatePrefab(divider, cell);
            }

            Transform focal = null;
            foreach (Transform t in panel.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != focalName) continue;
                if (focalName == "Mount" && (t.parent == null || t.parent.name != "Header")) continue;
                focal = t;
                break;
            }
            if (focal == null) { Debug.LogWarning("Synty dressing: no focal '" + focalName + "' under " + panel.name); return; }
            Image glowImage = Img(Rect("Focal Glow", focal), glow, new Color(0.914f, 0.753f, 0.416f, 0.32f));
            Ignore(glowImage.gameObject);
            glowImage.transform.SetAsFirstSibling();
            RectTransform gr = glowImage.rectTransform;
            gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
            gr.pivot = new Vector2(0.5f, 0.5f);
            gr.sizeDelta = glowSize;
            gr.anchoredPosition = Vector2.zero;
            System.Type idle = System.Type.GetType("Memori.UI.UIIdleGlow, Memori.UI");
            if (idle != null) glowImage.gameObject.AddComponent(idle);
            else Debug.LogError("Synty dressing: Memori.UI.UIIdleGlow not found.");
        }
        #endregion

        static void BuildHeader(RectTransform root, SerializedObject so)
        {
            RectTransform header = Rect("Header", root);
            Fixed(header.gameObject, -1f, HeaderHeight);
            HLayout(header, 16f, TextAnchor.MiddleLeft, new RectOffset(22, 22, 0, 0));

            Mount(header, "Mount", 64f, fireIcon, 28f);
            RectTransform names = Rect("Names", header);
            VLayout(names, 2f, new RectOffset());
            Flexible(names.gameObject, 1f).preferredWidth = 0f;
            TMP_Text title = Text("Title", names, displayDrop, 32f, Gold, English("campfirePickOne"));
            Ref(so, "headerTitle", title);
        }

        static void BuildChoosing(RectTransform root, SerializedObject so)
        {
            RectTransform body = Rect("Choosing", root);
            HorizontalLayoutGroup row = HLayout(body, 0f, TextAnchor.UpperCenter, new RectOffset());
            row.childForceExpandHeight = true;
            Ref(so, "choosingBody", body.gameObject);

            // Rest
            GameObject rest = ChoiceColumn(body, "Rest Column", healIcon, "CampfireRest");
            TMP_Text[] restValues = Cells(rest, new[] { "townHeal", "Cost" }, new[] { healIcon, goldIcon }, new[] { Positive, Positive });
            restValues[0].color = Positive;
            Unlocalize(restValues[0], "30%");
            Localize(restValues[1], "campfireFree");
            restValues[1].color = Positive;
            TMP_Text restLine = Lines(rest, healIcon, Positive, null, "Every squad heals 30% of its health, reserves too.", goldIcon, Coin, null);
            rest.transform.Find("Body/Line 2").gameObject.SetActive(false);
            Ref(so, "restHealValue", restValues[0]);
            Ref(so, "restLine", restLine);
            Ref(so, "restButton", ColumnButton(rest, "CampfireRest"));
            Divider(body);

            // Train
            GameObject train = ChoiceColumn(body, "Train Column", prestigeIcon, "Train");
            TMP_Text[] trainValues = Cells(train, new[] { "Prestige", "Cost" }, new[] { prestigeIcon, goldIcon }, new[] { Gold, Coin });
            trainValues[0].text = "+1";
            trainValues[0].color = Gold;
            Unlocalize(trainValues[1], "10 or 20");
            trainValues[1].color = Coin;
            TMP_Text trainCount = Lines(train, prestigeIcon, Gold, null, "11 of your 13 squads can train.", goldIcon, Coin, null);
            TMP_Text trainPrices = Child<TMP_Text>(train.transform, "Body/Line 2/Text");
            Unlocalize(trainPrices, "Prestige 1 costs 10 gold, Prestige 2 costs 20.");
            Ref(so, "trainCostValue", trainValues[1]);
            Ref(so, "trainCountLine", trainCount);
            Ref(so, "trainPricesLine", trainPrices);
            Ref(so, "trainButton", ColumnButton(train, "campfireChooseSquad"));
            TMP_Text unavailable = Child<TMP_Text>(train.transform, "Action/Unavailable");
            Localize(unavailable, "campfireNoSquadCanTrain");
            Ref(so, "trainUnavailable", unavailable.gameObject);
            Divider(body);

            // Scavenge
            GameObject scavenge = ChoiceColumn(body, "Scavenge Column", gearIcon, "Scavenge");
            TMP_Text[] scavengeValues = Cells(scavenge, new[] { "campfireCaptionGear", "Cost" }, new[] { gearIcon, goldIcon }, new[] { Sub, Positive });
            Unlocalize(scavengeValues[0], "1 of 3");
            Localize(scavengeValues[1], "campfireFree");
            scavengeValues[1].color = Positive;
            TMP_Text scavengeLine = Lines(scavenge, gearIcon, Sub, null, "See 3 gear items and keep one.", goldIcon, Coin, null);
            scavenge.transform.Find("Body/Line 2").gameObject.SetActive(false);
            Ref(so, "scavengeGearValue", scavengeValues[0]);
            Ref(so, "scavengeLine", scavengeLine);
            Ref(so, "scavengeButton", ColumnButton(scavenge, "Scavenge"));
            Divider(body);

            // Scout Ahead
            GameObject scout = ChoiceColumn(body, "Scout Column", eyeIcon, "CampfireScoutAhead");
            TMP_Text[] scoutValues = Cells(scout, new[] { "campfireCaptionReveals", "Gold" }, new[] { eyeIcon, goldIcon }, new[] { Sub, Coin });
            Localize(scoutValues[0], "campfireThisAct");
            Unlocalize(scoutValues[1], "+5");
            scoutValues[1].color = Coin;
            Lines(scout, mapIcon, Sub, "campfireScoutLine", null, chestIcon, Sub, null);
            TMP_Text consumable = Child<TMP_Text>(scout.transform, "Body/Line 2/Text");
            Unlocalize(consumable, "You also find a consumable.");
            Ref(so, "scoutGoldValue", scoutValues[1]);
            Ref(so, "scoutConsumableLine", consumable);
            Ref(so, "scoutConsumableIcon", Child<Image>(scout.transform, "Body/Line 2/Icon"));
            Ref(so, "scoutButton", ColumnButton(scout, "CampfireScoutAhead"));

            // Hovering the map button opens the act overview, as the old "?" button did. It shares the button's width.
            RectTransform scoutAction = Child<RectTransform>(scout.transform, "Action");
            Fixed(scoutAction.Find("Button").gameObject, ChoiceButtonWidth - 56f, 62f);
            GameObject mapButton = Instance(standardButton, scoutAction, "Map");
            Fixed(mapButton, 48f, 62f);
            HideButtonText(mapButton);
            Image mapImage = Img(Rect("Map Icon", mapButton.transform), mapIcon, Cream);
            mapImage.preserveAspect = true;
            Centre(mapImage.rectTransform, 24f, 24f);
            Ref(so, "scoutMapTrigger", mapButton.AddComponent<MapOverviewHoverTrigger>());
        }

        static void BuildTrain(RectTransform root, SerializedObject so)
        {
            RectTransform body = Rect("Train", root);
            VLayout(body, 12f, new RectOffset(22, 22, 16, 0));
            Ref(so, "trainBody", body.gameObject);

            RectTransform head = Rect("Head", body);
            HLayout(head, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Mount(head, "Mount", 44f, prestigeIcon, 20f);
            RectTransform titles = Rect("Titles", head);
            VLayout(titles, 1f, new RectOffset());
            Flexible(titles.gameObject, 1f).preferredWidth = 0f;
            Localize(Text("Title", titles, displayDrop, 22f, Gold, "Title"), "campfireTrainTitle");
            TMP_Text count = Text("Count", head, displayDrop, 17f, Cream, "11 of 13 can train");
            count.alignment = TextAlignmentOptions.MidlineRight;
            Ref(so, "trainCanCount", count);

            // The strip mirrors the army bar: ten deployed slots, then the reserve slots.
            RectTransform tray = Rect("Tray", body);
            Img(tray, solid, Well);
            Image trayEdge = Img(Stretch(Rect("Edge", tray)), frameEdge, WellEdge, Image.Type.Sliced);
            trayEdge.fillCenter = false;
            trayEdge.pixelsPerUnitMultiplier = 4f;
            Ignore(trayEdge.gameObject);
            HLayout(tray, TrayGroupGap, TextAnchor.UpperCenter, new RectOffset(12, 12, 10, 10));

            SlotGroup(tray, "Deployed", "Deployed", DeployedSlots, out RectTransform deployedRow);
            RectTransform reserve = SlotGroup(tray, "Reserve", "Reserve", ReserveSlots, out RectTransform reserveRow);
            Ref(so, "deployedRow", deployedRow);
            Ref(so, "reserveRow", reserveRow);
            Ref(so, "reserveGroup", reserve.gameObject);

            // Blank until a slot is hovered; the line keeps its height so the footer never jumps.
            TMP_Text detail = Text("Detail", body, display, 16f, Cream, "");
            detail.alignment = TextAlignmentOptions.Center;
            Fixed(detail.gameObject, -1f, 24f);
            Ref(so, "trainDetail", detail);

            RectTransform footer = Rect("Footer", body);
            VLayout(footer, 0f, new RectOffset(0, 0, 0, 14)).childAlignment = TextAnchor.UpperCenter;
            RectTransform actions = Rect("Actions", footer);
            HLayout(actions, 0f, TextAnchor.MiddleCenter, new RectOffset(0, 0, 12, 0));
            GameObject back = Instance(backButton, actions, "Back");
            Fixed(back, 260f, 54f);
            Ref(so, "backButton", back.GetComponent<Button>());
        }

        static RectTransform SlotGroup(RectTransform tray, string name, string captionKey, int slots, out RectTransform row)
        {
            RectTransform group = Rect(name, tray);
            VLayout(group, 6f, new RectOffset());
            // Fixed to the full slot count, so a smaller army keeps every card in its army bar position.
            Fixed(group.gameObject, slots * SlotWidth + (slots - 1) * SlotGap, -1f);
            Localize(SectionTitle(group, "Caption"), captionKey);
            row = Rect("Row", group);
            HorizontalLayoutGroup layout = HLayout(row, SlotGap, TextAnchor.UpperLeft, new RectOffset());
            layout.childForceExpandWidth = false;
            Fixed(row.gameObject, -1f, 172f);
            return group;
        }

        static void BuildResult(RectTransform root, SerializedObject so)
        {
            RectTransform body = Rect("Result", root);
            VerticalLayoutGroup layout = VLayout(body, 14f, new RectOffset(40, 40, 24, 30));
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = false;
            Ref(so, "resultBody", body.gameObject);

            RectTransform mountRow = Rect("Mount Row", body);
            HLayout(mountRow, 0f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(mountRow.gameObject, 1070f, 84f);
            RectTransform mountCell = Mount(mountRow, "Mount", 84f, healIcon, 36f);
            Ref(so, "resultIcon", Child<Image>(mountCell, "Icon"));
            // A round glow under the result's diamond, so a choice blooms behind it (RunSetupBuilder.AddHalo).
            GameObject halo = Instance(Load<GameObject>(HaloFlarePath), mountCell, "Halo Flare");
            halo.transform.SetAsFirstSibling();
            RectTransform haloRect = (RectTransform)halo.transform;
            haloRect.anchorMin = haloRect.anchorMax = new Vector2(0.5f, 0.5f);
            haloRect.anchoredPosition = Vector2.zero;
            haloRect.sizeDelta = new Vector2(HaloSize, HaloSize);
            Ignore(halo);
            // By name: this assembly does not reference Memori.UI.
            Ref(so, "resultHalo", halo.GetComponent("UIFlare"));

            TMP_Text title = Text("Title", body, displayDrop, 30f, Gold, "Title");
            title.alignment = TextAlignmentOptions.Center;
            Fixed(title.gameObject, 1070f, -1f);
            Ref(so, "resultTitle", title);
            TMP_Text description = Text("Description", body, display, 17f, Cream, "Description");
            description.alignment = TextAlignmentOptions.Center;
            Wrap(description);
            Fixed(description.gameObject, 760f, -1f);
            Ref(so, "resultDescription", description);

            // Up to three plaques side by side; at about 246 px each a squad name still fits.
            RectTransform strip = Rect("Strip", body);
            Fixed(strip.gameObject, 760f, -1f);
            HLayout(strip, PlaqueGap, TextAnchor.MiddleLeft, new RectOffset());
            var cells = new Object[3];
            var labels = new Object[3];
            var values = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                GameObject plaque = Instance(statPlaque, strip, "Plaque " + (i + 1));
                Child<Image>(plaque.transform, "Icon").gameObject.SetActive(false);
                // CampfirePanel writes each label per result, so the plaque label keeps no localizer.
                TMP_Text label = Child<TMP_Text>(plaque.transform, "Label");
                Unlocalize(label, "Label");
                TMP_Text value = Child<TMP_Text>(plaque.transform, "Value");
                Unlocalize(value, "Value");
                cells[i] = plaque;
                labels[i] = label;
                values[i] = value;
            }
            Refs(so, "resultCells", cells);
            Refs(so, "resultLabels", labels);
            Refs(so, "resultValues", values);
        }
        #endregion

        #region Widgets
        static GameObject ChoiceColumn(RectTransform parent, string name, Sprite icon, string titleKey)
        {
            GameObject column = Instance(columnPart, parent, name);
            Child<Image>(column.transform, "Head/Mount/Icon").sprite = icon;
            Localize(Child<TMP_Text>(column.transform, "Head/Titles/Title"), titleKey);
            return column;
        }

        // Sets both stat plaques' labels and icons. Gold values carry the coin sprite in their text, so the coin slot stays clear.
        static TMP_Text[] Cells(GameObject column, string[] keys, Sprite[] icons, Color[] tints)
        {
            var values = new TMP_Text[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                Transform plaque = column.transform.Find("Strip/Plaque " + (i + 1));
                Localize(Child<TMP_Text>(plaque, "Label"), keys[i]);
                Image image = Child<Image>(plaque, "Icon");
                image.sprite = icons[i];
                image.color = icons[i] == goldIcon ? Color.clear : tints[i];
                values[i] = Child<TMP_Text>(plaque, "Value");
            }
            return values;
        }

        // Fills the two body lines. A line with a key is fixed text; one without keeps its placeholder for the panel to fill.
        static TMP_Text Lines(GameObject column, Sprite firstIcon, Color firstTint, string firstKey, string firstPlaceholder, Sprite secondIcon, Color secondTint, string secondKey)
        {
            Image first = Child<Image>(column.transform, "Body/Line 1/Icon");
            first.sprite = firstIcon;
            first.color = firstTint;
            TMP_Text firstText = Child<TMP_Text>(column.transform, "Body/Line 1/Text");
            if (firstKey != null) Localize(firstText, firstKey);
            else Unlocalize(firstText, firstPlaceholder);
            Image second = Child<Image>(column.transform, "Body/Line 2/Icon");
            second.sprite = secondIcon;
            second.color = secondTint;
            if (secondKey != null) Localize(Child<TMP_Text>(column.transform, "Body/Line 2/Text"), secondKey);
            return firstText;
        }

        static Button ColumnButton(GameObject column, string labelKey)
        {
            Transform button = column.transform.Find("Action/Button");
            Localize(Child<TMP_Text>(button, "Button Label"), labelKey);
            return button.GetComponent<Button>();
        }

        static void HideButtonText(GameObject button)
        {
            foreach (string name in new[] { "Button Label", "Secondary Label", "Icon" })
            {
                Transform child = button.transform.Find(name);
                if (child != null) child.gameObject.SetActive(false);
            }
        }

        static void Divider(RectTransform parent)
        {
            RectTransform divider = Rect("Divider", parent);
            Fixed(divider.gameObject, 1f, -1f);
            Img(Stretch(Rect("Line", divider), 0f, 0f, 16f, 16f), null, A(Brass, 0.45f));
        }

        static void Line(RectTransform parent, string name, Sprite icon)
        {
            RectTransform line = Rect(name, parent);
            HLayout(line, 9f, TextAnchor.UpperLeft, new RectOffset());
            Image image = Img(Rect("Icon", line), icon, Sub);
            image.preserveAspect = true;
            Fixed(image.gameObject, 16f, 16f);
            TMP_Text text = Text("Text", line, display, 15f, Cream, name);
            Wrap(text);
            text.alignment = TextAlignmentOptions.TopLeft;
            Flexible(text.gameObject, 1f).preferredWidth = 0f;
        }

        // Section labels are normal case with no letter spacing; spaced capitals read as a web dashboard.
        static TMP_Text SectionTitle(RectTransform parent, string name) => Text(name, parent, displayDrop, 18f, Gold, name);

        static void Spacer(RectTransform parent)
        {
            LayoutElement spacer = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            spacer.flexibleHeight = 1f;
        }

        static RectTransform Mount(RectTransform parent, string name, float size, Sprite icon, float iconSize)
        {
            RectTransform cell = Rect(name, parent);
            Fixed(cell.gameObject, size, size);
            // The mount art has transparent margins, so it is drawn a little larger than its cell, as in the tooltip.
            Image diamond = Img(Rect("Diamond", cell), mount, Color.white);
            Centre(diamond.rectTransform, size * 52f / 44f, size * 52f / 44f);
            Image image = Img(Rect("Icon", cell), icon, Gold);
            image.preserveAspect = true;
            Centre(image.rectTransform, iconSize, iconSize);
            return cell;
        }

        static GameObject Instance(GameObject prefab, Transform parent, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.layer = 5;
            return instance;
        }

        static T Child<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            T component = child != null ? child.GetComponent<T>() : null;
            if (component == null) Debug.LogError($"CampfirePanelBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
            return component;
        }

        static void Wrap(TMP_Text text)
        {
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        static void Ignore(GameObject go) => GetOrAdd<LayoutElement>(go).ignoreLayout = true;
        #endregion

        #region Primitives
        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        static RectTransform Stretch(RectTransform rect, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        static void Centre(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        static Image Img(RectTransform rect, Sprite sprite, Color colour, Image.Type type = Image.Type.Simple)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = colour;
            image.type = sprite != null ? type : Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, Color colour, string text)
        {
            TextMeshProUGUI label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = colour;
            label.text = text;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.richText = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }

        static VerticalLayoutGroup VLayout(Transform target, float spacing, RectOffset padding)
        {
            VerticalLayoutGroup layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperLeft;
            return layout;
        }

        static HorizontalLayoutGroup HLayout(Transform target, float spacing, TextAnchor alignment, RectOffset padding)
        {
            HorizontalLayoutGroup layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        static LayoutElement Flexible(GameObject go, float width)
        {
            LayoutElement element = GetOrAdd<LayoutElement>(go);
            element.flexibleWidth = width;
            return element;
        }

        static void Fixed(GameObject go, float width, float height)
        {
            LayoutElement element = GetOrAdd<LayoutElement>(go);
            if (width >= 0f) { element.minWidth = width; element.preferredWidth = width; element.flexibleWidth = 0f; }
            if (height >= 0f) { element.minHeight = height; element.preferredHeight = height; element.flexibleHeight = 0f; }
            ((RectTransform)go.transform).sizeDelta = new Vector2(width >= 0f ? width : 100f, height >= 0f ? height : 30f);
        }

        static void Ref(SerializedObject so, string field, Object value)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"CampfirePanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"CampfirePanelBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
