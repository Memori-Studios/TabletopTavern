using System;
using System.Collections.Generic;
using Memori.Tooltip;
using TJ.MainMenu;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace TJ.RunSetup.EditorTools
{
    /// <summary>
    /// Generates the run-setup hero screen (Commander Screen UI.prefab) and the warband build panel and bars
    /// (Warband Build UI.prefab) on the game's Basic Background, and installs both in MainMenu.unity.
    /// Rebuilding keeps hand edits to the part prefabs.
    /// </summary>
    public static class RunSetupBuilder
    {
        public const string PartFolder = "Assets/Data/Prefabs/UI/Menu/Run Setup";
        public const string CommanderPath = PartFolder + "/Commander Screen UI.prefab";
        public const string WarbandPath = PartFolder + "/Warband Build UI.prefab";
        const string TilePath = PartFolder + "/Hero Roster Tile.prefab";
        const string ArmyTilePath = PartFolder + "/Warband Army Tile.prefab";
        const string ScenePath = "Assets/Scenes/MainMenu.unity";
        const string ButtonFolder = "Assets/Data/Prefabs/UI/Reuseable/Buttons";
        const string BasicBackgroundPath = "Assets/Data/Prefabs/UI/Reuseable/Basic Background.prefab";
        const string GearTilePath = "Assets/Data/Prefabs/UI/Menu/Warband Gear Tile.prefab";
        // Shared art: the sheen on a screen's main action and the flare a commit lands with.
        const string ButtonSheenPath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Button Sheen.prefab";
        const string LandFlarePath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Land Flare.prefab";
        const string HaloFlarePath = "Assets/Data/Prefabs/UI/Reuseable/Ornaments/Halo Flare.prefab";
        const string SheetPath = "Assets/Scripts/Memori.Tooltip/Art/TooltipSheet.png";
        const string TableName = "MainLocalizationTable";

        const float SideWidth = 600f;
        const float PanelHeight = 940f;
        const float Margin = 20f;
        const float BarHeight = 82f;
        const float TextureTop = 103f;
        const float TexturePixelsPerUnit = 12f;
        const int Factions = 8;
        const float TileWidth = 124f;
        const float TileHeight = 150f;
        const float ArmySquare = 96f;
        const float SlotSquare = 80f;
        // Every hero panel block has a fixed height so switching heroes never moves the blocks below.
        const float EffectHeight = 76f;
        // Section titles: the Drop font in Gold at normal case, on a row tall enough for the 18 unit line.
        const float SectionTitleSize = 18f;
        const float HeadingHeight = 22f;
        // Text boxes sized for the 13 unit floor: a unit name or a gear hint on two lines, a gear title on one.
        const float ArmyNameHeight = 40f;
        const float SlotTitleHeight = 20f;
        const float SlotHintHeight = 42f;

        #region Style
        static readonly Color Brass = Hex("B08A3E");
        static readonly Color Gold = Hex("E9C06A");
        static readonly Color Cream = Hex("ECE6D8");
        static readonly Color Body = Hex("D9D3C5");
        static readonly Color Flavour = Hex("A99F8A");
        static readonly Color Cap = Hex("8C9AA2");
        static readonly Color TileFill = Hex("1C2A30");
        static readonly Color Dim = Hex("4A5C66");
        static readonly Color Error = Hex("E8A15F");

        static TMP_FontAsset displayDrop, display;
        static Sprite mount, solid, flatShadow, frameSmall, heroFrame, edgeFade, circle, lockIcon, heroIcon, campaignIcon, battleIcon, shade, arrowIcon;
        static GameObject standardButton, backButton, primaryButton, basicBackground, gearTilePrefab, buttonSheen, landFlare, haloFlare;

        static void LoadAssets()
        {
            displayDrop = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF Drop.asset");
            display = Load<TMP_FontAsset>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Fonts/Texturina/Texturina_18pt-SemiBold SDF.asset");
            var sheet = new Dictionary<string, Sprite>();
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(SheetPath))
                if (asset is Sprite sprite) sheet[sprite.name] = sprite;
            sheet.TryGetValue("TooltipMount", out mount);
            sheet.TryGetValue("TooltipSolid", out solid);
            if (mount == null || solid == null) Debug.LogError("RunSetupBuilder: tooltip sheet sprites missing.");
            flatShadow = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Flat Shadow.png");
            frameSmall = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Frame_Box_Small01.png");
            heroFrame = Load<Sprite>("Assets/Art/UI/Difficulty Frames/SPR_FantasyWarrior_Frame_Box_Small01_Bronze.png");
            edgeFade = Load<Sprite>("Assets/ImportedPackages/ModernUIPack/Textures/Shadow/Vertical Shadow.png");
            circle = Load<Sprite>("Assets/Art/Icons/UI/Circle.png");
            lockIcon = Load<Sprite>("Assets/Art/Icons/UI/LockClosedGold.png");
            arrowIcon = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Arrow02.png");
            heroIcon = Load<Sprite>("Assets/Art/Icons/Stats/Leadership.png");
            campaignIcon = Load<Sprite>("Assets/Art/Icons/Map/Town.png");
            battleIcon = Load<Sprite>("Assets/Art/Icons/Map/Engagement.png");
            shade = Load<Sprite>("Assets/ImportedPackages/InterfaceFantasyWarriorHUD/Sprites/HUD/SPR_HUD_FantasyWarrior_Gradient_Vertical_Smooth01.png");
            standardButton = Load<GameObject>(ButtonFolder + "/Button - Standard.prefab");
            backButton = Load<GameObject>(ButtonFolder + "/Button - Back.prefab");
            primaryButton = Load<GameObject>(ButtonFolder + "/Button - Primary.prefab");
            basicBackground = Load<GameObject>(BasicBackgroundPath);
            gearTilePrefab = Load<GameObject>(GearTilePath);
            buttonSheen = Load<GameObject>(ButtonSheenPath);
            landFlare = Load<GameObject>(LandFlarePath);
            haloFlare = Load<GameObject>(HaloFlarePath);
        }

        // The sheen fills its button; it ignores the button's layout.
        static void AddSheen(GameObject button)
        {
            GameObject sheen = Instance(buttonSheen, button.transform, "Sheen");
            Stretch((RectTransform)sheen.transform);
            Ignore(sheen);
        }

        // A flare centred on a point of its parent; it ignores the parent's layout.
        static Memori.UI.UIFlare AddFlare(Transform parent, Vector2 anchor, Vector2 position)
        {
            GameObject flare = Instance(landFlare, parent, "Land Flare");
            return PlaceFlare(flare, anchor, position);
        }

        // A round glow drawn under everything else in its parent, so it rings the icon instead of covering it.
        static Memori.UI.UIFlare AddHalo(Transform parent, Vector2 anchor, Vector2 position)
        {
            GameObject flare = Instance(haloFlare, parent, "Halo Flare");
            flare.transform.SetAsFirstSibling();
            return PlaceFlare(flare, anchor, position);
        }

        static Memori.UI.UIFlare PlaceFlare(GameObject flare, Vector2 anchor, Vector2 position)
        {
            var rect = (RectTransform)flare.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            Ignore(flare);
            return flare.GetComponent<Memori.UI.UIFlare>();
        }

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) Debug.LogError($"RunSetupBuilder: missing {typeof(T).Name} at {path}");
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
        // Keys this screen adds, in every in-scope locale.
        static readonly (string key, string en, string de, string es, string fr, string ja, string ko, string ru, string zh, string zhHant)[] Keys =
        {
            ("heroSelectCount", "{0} heroes · {1} factions", "{0} Helden · {1} Fraktionen", "{0} héroes · {1} facciones", "{0} héros · {1} factions",
                "ヒーロー{0}人 · 勢力{1}", "영웅 {0}명 · 세력 {1}개", "Героев: {0} · Фракций: {1}", "{0} 名英雄 · {1} 个派系", "{0} 名英雄 · {1} 個派系"),
            ("heroSelectComingSoon", "{0} and one more faction are on the way.", "{0} und eine weitere Fraktion sind unterwegs.", "{0} y una facción más están en camino.",
                "{0} et une autre faction arrivent bientôt.", "{0}ともう一つの勢力が近日登場。", "{0}와(과) 또 하나의 세력이 곧 찾아옵니다.", "{0} и ещё одна фракция уже в пути.",
                "{0} 和另一个派系即将到来。", "{0} 和另一個派系即將到來。"),
            ("heroSignature", "Signature", "Signatur", "Insignia", "Emblématique", "スペシャル", "시그니쳐", "Ключевое", "专属", "專屬"),
            ("heroRecordTitle", "Record with {0}", "Bilanz mit {0}", "Historial con {0}", "Palmarès avec {0}", "{0}の戦績", "{0}의 전적", "Достижения: {0}", "{0} 的战绩", "{0} 的戰績"),
            ("heroRecordWon", "Won", "Gewonnen", "Ganada", "Gagnée", "勝利", "승리", "Победа", "已胜利", "已勝利"),
            ("heroRecordNotYet", "Not yet", "Noch nicht", "Aún no", "Pas encore", "未達成", "아직", "Пока нет", "尚未", "尚未"),
            ("warbandBuildTitle", "Your starting build", "Startaufstellung", "Configuración inicial", "Votre composition de départ", "初期編成", "시작 편성",
                "Стартовый состав", "初始配置", "初始配置"),
            ("warbandPurseBreakdown", "{0} start · {1} army · {2} gear", "{0} Start · {1} Armee · {2} Ausrüstung", "{0} inicio · {1} ejército · {2} equipamiento",
                "{0} départ · {1} armée · {2} équipement", "初期 {0} · 軍勢 {1} · 装備 {2}", "시작 {0} · 부대 {1} · 장비 {2}", "{0} старт · {1} армия · {2} снаряжение",
                "初始 {0} · 部队 {1} · 装备 {2}", "初始 {0} · 部隊 {1} · 裝備 {2}"),
            ("heroTreasuryBreakdown", "{0} base {1} renown", "{0} Basis {1} Ruhm", "{0} base {1} fama", "{0} base {1} renommée", "基本 {0} 名声 {1}", "기본 {0} 명성 {1}",
                "{0} база {1} известность", "基础 {0} 声望 {1}", "基礎 {0} 聲望 {1}"),
            // The gear column is narrow; "Starting Gear" in most languages is too long for it at the 13 unit floor.
            ("warbandGearHeading", "Starting Gear", "Ausrüstung", "Equipo", "Équipement", "装備", "장비", "Снаряжение", "装备", "裝備"),
        };

        static StringTableCollection collection;
        static StringTable english;

        static void LoadTable()
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            english = (StringTable)collection.GetTable("en");
        }

        [MenuItem("Tabletop Tavern/Run Setup/Add Text Keys")]
        public static void AddKeysMenu() => Debug.Log("RunSetupBuilder: " + EnsureKeys());

        /// <summary>Adds any missing key with its text in every locale. Existing keys and their text are left alone.</summary>
        public static string EnsureKeys()
        {
            LoadTable();
            var tables = new Dictionary<string, StringTable>();
            foreach (StringTable table in collection.StringTables) tables[table.LocaleIdentifier.Code] = table;
            int added = 0;
            var missingLocales = new HashSet<string>();
            foreach (var k in Keys)
            {
                if (collection.SharedData.GetEntry(k.key) != null) continue;
                collection.SharedData.AddKey(k.key);
                var texts = new (string code, string text)[]
                {
                    ("en", k.en), ("de", k.de), ("es", k.es), ("fr", k.fr), ("ja", k.ja), ("ko", k.ko), ("ru", k.ru), ("zh", k.zh), ("zh-Hant", k.zhHant),
                };
                foreach ((string code, string text) in texts)
                {
                    StringTable table = FindTable(tables, code);
                    if (table == null) { missingLocales.Add(code); continue; }
                    table.AddEntry(k.key, text);
                    EditorUtility.SetDirty(table);
                }
                added++;
            }
            if (added == 0) return "all keys present.";
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            return $"added {added} keys" + (missingLocales.Count > 0 ? "; no table for " + string.Join(", ", missingLocales) : ".");
        }

        static StringTable FindTable(Dictionary<string, StringTable> tables, string code)
        {
            if (tables.TryGetValue(code, out StringTable table)) return table;
            if (code == "zh" && tables.TryGetValue("zh-Hans", out table)) return table;
            if (code == "zh" && tables.TryGetValue("zh-CN", out table)) return table;
            if (code == "zh-Hant" && tables.TryGetValue("zh-TW", out table)) return table;
            return null;
        }

        static long KeyId(string key)
        {
            SharedTableData.SharedTableEntry entry = collection.SharedData.GetEntry(key);
            if (entry == null) throw new InvalidOperationException($"RunSetupBuilder: no localization key '{key}'.");
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

        // A text the screen fills at runtime must not keep a localizer, or a locale change would reset it.
        static void Unlocalize(TMP_Text text, string placeholder)
        {
            foreach (LocalizeStringEvent localizer in text.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(localizer);
            text.text = placeholder;
        }
        #endregion

        #region Entry points
        [MenuItem("Tabletop Tavern/Run Setup/Rebuild Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureParts(false);
            BuildRoot(CommanderPath, "Commander Screen UI", BuildCommander);
            BuildRoot(WarbandPath, "Warband Build UI", BuildWarband);
            Debug.Log("RunSetupBuilder: " + RewireMainMenu());
        }

        [MenuItem("Tabletop Tavern/Run Setup/Reset Part Prefabs")]
        static void ResetPartsMenu()
        {
            if (!EditorUtility.DisplayDialog("Reset run setup parts",
                    $"The Warband Army Tile in {PartFolder} is rebuilt from code, which discards your edits to it. The Hero Roster Tile is kept. The screens are rebuilt after.",
                    "Reset", "Cancel"))
                return;
            ResetParts();
        }

        public static void ResetParts()
        {
            EnsureKeys();
            LoadAssets();
            LoadTable();
            EnsureParts(true);
            BuildRoot(CommanderPath, "Commander Screen UI", BuildCommander);
            BuildRoot(WarbandPath, "Warband Build UI", BuildWarband);
            Debug.Log("RunSetupBuilder: " + RewireMainMenu());
        }

        [MenuItem("Tabletop Tavern/Run Setup/Rewire Main Menu")]
        public static void RewireMenu() => Debug.Log("RunSetupBuilder: " + RewireMainMenu());

        [MenuItem("Tabletop Tavern/Run Setup/Install In Main Menu")]
        public static void InstallMenu() => Debug.Log("RunSetupBuilder: " + InstallInMainMenu());

        /// <summary>
        /// Replaces the old commander children and the warband Top bar and Right Panel in MainMenu.unity with the two
        /// prefabs, wires everything to them and saves only MainMenu.unity. Stops without saving if anything else in
        /// the scene still points into the old UI. Once installed it only rewires.
        /// </summary>
        public static string InstallInMainMenu()
        {
            GameObject commanderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CommanderPath);
            GameObject warbandPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WarbandPath);
            if (commanderPrefab == null || warbandPrefab == null) return "prefabs missing; run Rebuild Prefabs first.";
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) return "MainMenu.unity is open; close it first so a stopped install can be discarded.";
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                PlayPanel play = Find<PlayPanel>(scene);
                if (play == null) return "no PlayPanel in MainMenu.unity.";
                var playSo = new SerializedObject(play);
                Transform commander = ((Component)playSo.FindProperty("commanderScreen").objectReferenceValue).transform;
                Transform warbandScreen = ((Component)playSo.FindProperty("warbandScreen").objectReferenceValue).transform;
                if (Instance(commander, commanderPrefab) != null) return "already installed; use Rewire Main Menu.";

                var old = new List<Transform>();
                foreach (string name in new[] { "Select Hero - Left", "Difficulty - Middle", "Dossier - Right", "Bottom" })
                {
                    Transform child = commander.Find(name);
                    if (child == null) return $"no '{name}' under the commander screen.";
                    old.Add(child);
                }
                Transform top = warbandScreen.Find("Top");
                Transform oldRight = null;
                foreach (Transform child in warbandScreen)
                    if (child.name == "Right Panel" && child.gameObject.activeSelf) oldRight = child;
                if (top == null || oldRight == null) return "warband Top or Right Panel not found.";
                old.Add(top);
                old.Add(oldRight);

                var commanderUI = (GameObject)PrefabUtility.InstantiatePrefab(commanderPrefab, commander);
                commanderUI.transform.SetSiblingIndex(0);
                var warbandUI = (GameObject)PrefabUtility.InstantiatePrefab(warbandPrefab, warbandScreen);
                warbandUI.transform.SetAsLastSibling();

                // The signature unit's hover panel sits left of the hero panel, as it sat left of the old dossier.
                var armySo = new SerializedObject(Find<StartingArmyManager>(scene));
                var battleInfo = (RectTransform)((Component)armySo.FindProperty("commanderSquadBattleInfo").objectReferenceValue).transform;
                battleInfo.SetParent(commander, false);
                battleInfo.anchorMin = battleInfo.anchorMax = new Vector2(1f, 0.5f);
                battleInfo.anchoredPosition = new Vector2(-(Margin + SideWidth + 20f + 125f), battleInfo.anchoredPosition.y);
                battleInfo.SetAsLastSibling();
                foreach (string field in new[] { "discordUnlock", "newsletterUnlock" })
                {
                    Transform popup = ((Component)playSo.FindProperty(field).objectReferenceValue).transform;
                    popup.SetParent(commander, false);
                    popup.SetAsLastSibling();
                }

                string wired = Wire(scene);
                if (!string.IsNullOrEmpty(wired)) return "stopped without saving: " + wired;
                string outside = CheckReferences(scene, old);
                if (!string.IsNullOrEmpty(outside)) return "stopped without saving, outside references into the old UI:\n" + outside;

                foreach (Transform child in old) Object.DestroyImmediate(child.gameObject);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) return "MainMenu.unity did not save.";
                return "installed; removed " + old.Count + " old objects.";
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// Moves the objects other code still uses back into the rebuilt layout and points PlayPanel, WarbandPanel,
        /// StartingArmyManager and the validation strip at the current prefab parts. Saves only MainMenu.unity.
        /// </summary>
        public static string RewireMainMenu()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            if (!scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                opened = true;
            }
            else if (scene.isDirty) return "MainMenu.unity has unsaved changes; save or revert them, then run Rewire Main Menu.";
            try
            {
                string wired = Wire(scene);
                if (!string.IsNullOrEmpty(wired)) return "rewire stopped: " + wired;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) return "MainMenu.unity did not save.";
                return "rewired MainMenu.unity.";
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static GameObject Instance(Transform parent, GameObject prefab)
        {
            foreach (Transform child in parent)
                if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(child.gameObject) == prefab) return child.gameObject;
            return null;
        }

        // Returns an empty string when every part was found and wired, otherwise what was missing.
        static string Wire(Scene scene)
        {
            PlayPanel play = Find<PlayPanel>(scene);
            WarbandPanel warband = Find<WarbandPanel>(scene);
            StartingArmyManager army = Find<StartingArmyManager>(scene);
            RunSetupValidation validation = Find<RunSetupValidation>(scene);
            GameObject armyTilePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArmyTilePath);
            if (play == null || warband == null || army == null || validation == null || armyTilePrefab == null) return "run setup components not found.";
            var playSo = new SerializedObject(play);
            var warbandSo = new SerializedObject(warband);
            var armySo = new SerializedObject(army);
            var validationSo = new SerializedObject(validation);
            Transform commander = ((Component)playSo.FindProperty("commanderScreen").objectReferenceValue).transform;
            Transform warbandScreen = ((Component)playSo.FindProperty("warbandScreen").objectReferenceValue).transform;
            GameObject commanderUI = Instance(commander, AssetDatabase.LoadAssetAtPath<GameObject>(CommanderPath));
            GameObject warbandUI = Instance(warbandScreen, AssetDatabase.LoadAssetAtPath<GameObject>(WarbandPath));
            if (commanderUI == null || warbandUI == null) return "prefab instances not found; run Install In Main Menu.";
            Transform w = warbandUI.transform;
            CommanderScreenView commanderView = commanderUI.GetComponent<CommanderScreenView>();

            // The crest set and its spawn feedback keep working as they are, only in the Difficulty section.
            var feedback = playSo.FindProperty("crestSpawnFeedback").objectReferenceValue as Component;
            if (feedback == null) return "no crest spawn feedback.";
            var crests = (RectTransform)feedback.transform.parent;
            crests.SetParent(Path(w, "Build Panel/Difficulty/Body/Crest Column/Crest Anchor"), false);
            crests.anchorMin = crests.anchorMax = crests.pivot = new Vector2(0.5f, 0.5f);
            crests.anchoredPosition = Vector2.zero;
            crests.sizeDelta = new Vector2(190f, 112f);
            crests.localScale = new Vector3(0.55f, 0.55f, 1f);

            // The spell slots keep their wiring; they sit in the Spells section at the new size.
            Transform slotRow = Path(w, "Build Panel/Gear And Spells/Spells/Spell Slots");
            SerializedProperty slots = warbandSo.FindProperty("spellSlots");
            for (int i = 0; i < slots.arraySize; i++)
            {
                var slot = (RectTransform)((Component)slots.GetArrayElementAtIndex(i).objectReferenceValue).transform;
                slot.SetParent(slotRow, false);
                slot.SetSiblingIndex(i);
                slot.sizeDelta = new Vector2(SlotSquare, SlotSquare);
                var icon = (RectTransform)slot.Find("Spell Icon");
                if (icon != null) icon.sizeDelta = new Vector2(50f, 50f);
            }
            Transform updateLock = FindDeep(warbandScreen, "Update Locked Blocker");
            if (updateLock != null)
            {
                updateLock.SetParent(Path(w, "Build Panel/Gear And Spells/Spells"), false);
                Stretch((RectTransform)updateLock);
                GetOrAdd<LayoutElement>(updateLock.gameObject).ignoreLayout = true;
                updateLock.SetAsLastSibling();
            }

            Button start = Path(w, "Right Bar/Start Campaign").GetComponent<Button>();
            Transform units = Path(w, "Build Panel/Army/Slots/Starting Units Parent");
            Ref(playSo, "commanderView", commanderView);
            Ref(playSo, "toWarbandButton", commanderView.BuildArmyButton);
            Ref(playSo, "returnToMainMenuButton", commanderView.ReturnButton);
            Ref(playSo, "startButton", start);
            Ref(playSo, "_difficultyTitle", Path(w, "Build Panel/Difficulty/Body/Crest Column/Spinner/Level").GetComponent<TMP_Text>());
            Ref(playSo, "difficultyDescriptionText", Path(w, "Build Panel/Difficulty/Body/Modifiers/Difficulty Description").GetComponent<TMP_Text>());
            Ref(playSo, "increaseDifficultyButton", Path(w, "Build Panel/Difficulty/Body/Crest Column/Spinner/Harder").GetComponent<Button>());
            Ref(playSo, "decreaseDifficultyButton", Path(w, "Build Panel/Difficulty/Body/Crest Column/Spinner/Easier").GetComponent<Button>());
            Ref(playSo, "extraInfo", Path(w, "Build Panel/Difficulty/Body/Modifiers/Extra Info").gameObject);
            Ref(playSo, "additionalDifficultyInfoTooltipTrigger", Path(w, "Build Panel/Difficulty/Body/Modifiers/Extra Info/More").GetComponent<MemoriTooltipTrigger>());
            Ref(playSo, "difficultyButtonText", null);
            Ref(playSo, "startingUnitsParent", units);
            playSo.ApplyModifiedPropertiesWithoutUndo();

            Transform purse = Path(w, "Right Bar/Purse");
            Ref(warbandSo, "commanderSummaryText", null);
            Ref(warbandSo, "backToCommanderButton", Path(w, "Left Bar/Return").GetComponent<Button>());
            Ref(warbandSo, "buildView", warbandUI.GetComponent<WarbandBuildView>());
            Ref(warbandSo, "remainingTreasuryText", Path(w, "Right Bar/Purse/Ready/Gold").GetComponent<TMP_Text>());
            Ref(warbandSo, "treasuryTooltipTrigger", GetOrAdd<MemoriTooltipTrigger>(purse.gameObject));
            string[] sections = { "Build Panel/Army", "Build Panel/Gear And Spells/Gear", "Build Panel/Gear And Spells/Spells" };
            var areas = new Object[3];
            for (int i = 0; i < 3; i++) areas[i] = Path(w, sections[i]).GetComponent<WarbandSectionHoverArea>();
            Refs(warbandSo, "sectionHoverAreas", areas);
            Ref(warbandSo, "armySectionHighlight", Path(w, sections[0] + "/Highlight").gameObject);
            Ref(warbandSo, "gearSectionHighlight", Path(w, sections[1] + "/Highlight").gameObject);
            Ref(warbandSo, "spellSectionHighlight", Path(w, sections[2] + "/Highlight").gameObject);
            Ref(warbandSo, "armyCountText", Path(w, sections[0] + "/Heading/Count").GetComponent<TMP_Text>());
            Ref(warbandSo, "gearCountText", Path(w, sections[1] + "/Heading/Count").GetComponent<TMP_Text>());
            Ref(warbandSo, "spellCountText", Path(w, sections[2] + "/Heading/Count").GetComponent<TMP_Text>());
            warbandSo.ApplyModifiedPropertiesWithoutUndo();

            Ref(armySo, "startingUnitsParent", units);
            Ref(armySo, "armyTilePrefab", armyTilePrefab.GetComponent<WarbandArmyTile>());
            Ref(armySo, "gearSlot", Path(w, sections[1] + "/Gear Slot").GetComponent<WarbandGearSlot>());
            armySo.ApplyModifiedPropertiesWithoutUndo();

            Ref(validationSo, "blockedRoot", Path(w, "Right Bar/Purse/Blocked").gameObject);
            Ref(validationSo, "readyRoot", Path(w, "Right Bar/Purse/Ready").gameObject);
            Ref(validationSo, "blockedText", Path(w, "Right Bar/Purse/Blocked/Text").GetComponent<TMP_Text>());
            Ref(validationSo, "startButton", start);
            validationSo.ApplyModifiedPropertiesWithoutUndo();
            return string.Empty;
        }

        static Transform Path(Transform root, string path)
        {
            Transform found = root.Find(path);
            if (found == null) throw new InvalidOperationException($"RunSetupBuilder: no '{path}' under {root.name}.");
            return found;
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
            if (root == null) return null;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        // Anything outside the old UI that points into it would lose its target when the old UI is deleted.
        static string CheckReferences(Scene scene, List<Transform> old)
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
                        if (component == null) continue;
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

        static void BuildRoot(string path, string name, Action<GameObject> build)
        {
            // Rebuild inside the existing prefab so the root keeps its id; the scene instance references it.
            bool existing = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            Scene stage = default;
            GameObject root;
            if (existing) root = PrefabUtility.LoadPrefabContents(path);
            else
            {
                stage = EditorSceneManager.NewPreviewScene();
                root = new GameObject(name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
            }
            try
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                root.layer = 5;
                Stretch((RectTransform)root.transform);
                build(root);
                Normalize(root);
                CentreInkLabels(root);
                RecordOverrides(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"RunSetupBuilder: wrote {path}");
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

        static readonly List<TMP_Text> inkLabels = new List<TMP_Text>();

        // Marks a short label to be centred by its glyph ink; TMP centres the font's line box, which leaves "?" high and digits low.
        static void CentreInk(TMP_Text label) => inkLabels.Add(label);

        static void CentreInkLabels(GameObject root)
        {
            var rect = (RectTransform)root.transform;
            Vector2 anchorMin = rect.anchorMin, anchorMax = rect.anchorMax, pivot = rect.pivot, offsetMin = rect.offsetMin, offsetMax = rect.offsetMax;
            // TMP builds no mesh outside a canvas, so a temporary world-space one hosts the measurement.
            Canvas temp = root.GetComponent<Canvas>() == null ? root.AddComponent<Canvas>() : null;
            if (temp != null) temp.renderMode = RenderMode.WorldSpace;
            try
            {
                for (int i = inkLabels.Count - 1; i >= 0; i--)
                {
                    TMP_Text label = inkLabels[i];
                    if (label == null || !label.transform.IsChildOf(root.transform)) continue;
                    inkLabels.RemoveAt(i);
                    label.ForceMeshUpdate(true, true);
                    if (!InkCentre(label, out Vector2 ink))
                    {
                        Debug.LogWarning($"RunSetupBuilder: '{label.text}' on {label.name} has no visible glyph to centre.");
                        continue;
                    }
                    Vector2 shift = label.rectTransform.rect.center - ink;
                    Vector4 margin = label.margin;
                    label.margin = new Vector4(margin.x + shift.x, margin.y - shift.y, margin.z - shift.x, margin.w + shift.y);
                }
            }
            finally
            {
                if (temp != null) Object.DestroyImmediate(temp);
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.offsetMin = offsetMin;
                rect.offsetMax = offsetMax;
            }
        }

        static bool InkCentre(TMP_Text label, out Vector2 centre)
        {
            TMP_TextInfo info = label.textInfo;
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo character = info.characterInfo[i];
                if (!character.isVisible) continue;
                minX = Mathf.Min(minX, character.bottomLeft.x);
                maxX = Mathf.Max(maxX, character.topRight.x);
                minY = Mathf.Min(minY, character.bottomLeft.y);
                maxY = Mathf.Max(maxY, character.topRight.y);
            }
            centre = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f);
            return minX <= maxX;
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
        static GameObject tilePart, armyTilePart;

        static void EnsureParts(bool overwrite)
        {
            if (!AssetDatabase.IsValidFolder(PartFolder)) AssetDatabase.CreateFolder("Assets/Data/Prefabs/UI/Menu", "Run Setup");
            // The hero tile is styled by hand in its prefab, so a reset never replaces it; code only creates it when missing.
            tilePart = AssetDatabase.LoadAssetAtPath<GameObject>(TilePath);
            if (tilePart == null) tilePart = SavePart(TilePath, "Hero Roster Tile", RosterTilePart);
            armyTilePart = overwrite ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ArmyTilePath);
            if (armyTilePart == null) armyTilePart = SavePart(ArmyTilePath, "Warband Army Tile", ArmyTilePart);
        }

        static GameObject SavePart(string path, string name, Action<GameObject> build)
        {
            Scene stage = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject(name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(root, stage);
                root.layer = 5;
                build(root);
                Normalize(root);
                CentreInkLabels(root);
                RecordOverrides(root);
                GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"RunSetupBuilder: wrote {path}");
                return asset;
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(stage);
            }
        }

        // One hero: portrait, name, a frame in the metal of the best level won, four level gems.
        static void RosterTilePart(GameObject go)
        {
            RectTransform tile = (RectTransform)go.transform;
            Fixed(go, TileWidth, TileHeight);
            Image hit = Img(tile, solid, TileFill);
            hit.raycastTarget = true;
            Button button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;

            RectTransform mask = Stretch(Rect("Mask", tile), 3f, 3f, 3f, 3f);
            mask.gameObject.AddComponent<RectMask2D>();
            float portraitWidth = TileWidth - 6f;
            Image portrait = Img(Rect("Portrait", mask), null, Color.white);
            TopCentre(portrait.rectTransform, portraitWidth, portraitWidth * 1.5f, 4f);

            Image fade = Img(Rect("Shade", tile), shade, A(Color.black, 0.92f));
            Anchor(fade.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(3f, 3f), new Vector2(-3f, 70f));
            // The gradient sprite is dark at its top; flipped so the tile darkens toward the name.
            fade.rectTransform.localScale = new Vector3(1f, -1f, 1f);

            TMP_Text name = Text("Name", tile, displayDrop, 13.5f, Cream, "Hero Name");
            name.alignment = TextAlignmentOptions.Bottom;
            Wrap(name);
            name.enableAutoSizing = true;
            name.fontSizeMin = 10f;
            name.fontSizeMax = 13.5f;
            name.lineSpacing = -12f;
            Anchor(name.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(6f, 20f), new Vector2(-6f, 54f));

            RectTransform gems = Rect("Gems", tile);
            Anchor(gems, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-40f, 6f), new Vector2(40f, 16f));
            HLayout(gems, 11f, TextAnchor.MiddleCenter, new RectOffset()).childControlWidth = false;
            var gemImages = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                Image gem = Img(Rect("Gem " + (i + 1), gems), solid, DifficultyMetal.Unwon);
                Fixed(gem.gameObject, 7f, 7f);
                gem.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                gemImages[i] = gem;
            }

            Image frame = Img(Stretch(Rect("Frame", tile)), heroFrame, DifficultyMetal.PlainFrame, Image.Type.Sliced);
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = 3f;

            RectTransform selected = Stretch(Rect("Selected", tile), -10f, -10f, -10f, -10f);
            Image ring = Img(selected, flatShadow, Color.white, Image.Type.Sliced);
            ring.fillCenter = false;
            ring.pixelsPerUnitMultiplier = 2f;
            selected.gameObject.SetActive(false);

            RectTransform locked = Rect("Locked", tile);
            Image lockImage = Img(locked, lockIcon, Hex("C9D2D8"));
            lockImage.preserveAspect = true;
            Anchor(locked, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-14f, 2f), new Vector2(14f, 30f));
            locked.gameObject.SetActive(false);

            HeroRosterTile component = go.AddComponent<HeroRosterTile>();
            var so = new SerializedObject(component);
            Ref(so, "button", button);
            Ref(so, "portrait", portrait);
            Ref(so, "frame", frame);
            Refs(so, "gems", gemImages);
            Ref(so, "nameText", name);
            Ref(so, "lockedMark", locked.gameObject);
            Ref(so, "selectedMark", selected.gameObject);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // One squad in the starting army: portrait in a rarity frame, name under it.
        static void ArmyTilePart(GameObject go)
        {
            RectTransform tile = (RectTransform)go.transform;
            Fixed(go, ArmySquare, ArmySquare + ArmyNameHeight + 4f);
            Image hit = Img(tile, null, Color.clear);
            hit.raycastTarget = true;

            RectTransform square = Rect("Square", tile);
            TopCentre(square, ArmySquare, ArmySquare, 0f);
            Img(square, solid, TileFill);
            RectTransform mask = Stretch(Rect("Mask", square), 2f, 2f, 2f, 2f);
            mask.gameObject.AddComponent<RectMask2D>();
            float width = ArmySquare - 4f;
            Image portrait = Img(Rect("Portrait", mask), null, Color.white);
            TopCentre(portrait.rectTransform, width, width * 1.5f, 6f);
            Image frame = Img(Stretch(Rect("Frame", square)), frameSmall, Hex("BDC3C7"), Image.Type.Sliced);
            frame.fillCenter = false;
            frame.pixelsPerUnitMultiplier = 4f;

            TMP_Text name = Text("Name", tile, displayDrop, 13f, Cream, "Unit Name");
            name.alignment = TextAlignmentOptions.Top;
            Wrap(name);
            name.enableAutoSizing = true;
            name.fontSizeMin = 13f;
            name.fontSizeMax = 13f;
            name.lineSpacing = -12f;
            Anchor(name.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(-4f, 0f), new Vector2(4f, ArmyNameHeight));

            Memori.UI.UIFlare flare = AddHalo(tile, new Vector2(0.5f, 1f), new Vector2(0f, -ArmySquare / 2f));

            WarbandArmyTile component = go.AddComponent<WarbandArmyTile>();
            var so = new SerializedObject(component);
            Ref(so, "portrait", portrait);
            Ref(so, "frame", frame);
            Ref(so, "nameText", name);
            Ref(so, "landFlare", flare);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        #endregion

        #region Hero screen
        static void BuildCommander(GameObject rootGo)
        {
            var root = (RectTransform)rootGo.transform;
            CommanderScreenView view = GetOrAdd<CommanderScreenView>(rootGo);
            var so = new SerializedObject(view);

            BuildRoster(Panel(root, "Roster Panel", true), so);
            BuildHeroPanel(Panel(root, "Hero Panel", false), so);

            RectTransform left = Bar(root, "Left Bar", true);
            GameObject back = Instance(backButton, left, "Return");
            Fixed(back, 160f, 46f);
            Localize(Child<TMP_Text>(back.transform, "Button Label"), "returnButton");
            Spacer(left, true);
            Ref(so, "returnButton", back.GetComponent<Button>());

            RectTransform right = Bar(root, "Right Bar", false);
            TMP_Text treasury = Text("Treasury", right, display, 19f, Cream, "Treasury 18");
            treasury.alignment = TextAlignmentOptions.MidlineLeft;
            Flexible(treasury.gameObject, 1f).preferredWidth = 0f;
            GameObject build = Instance(primaryButton, right, "Build Army");
            Fixed(build, 250f, 46f);
            Localize(Child<TMP_Text>(build.transform, "Button Label"), "WarbandReady");
            AddSheen(build);
            Ref(so, "treasuryText", treasury);
            Ref(so, "buildArmyButton", build.GetComponent<Button>());
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildRoster(RectTransform panel, SerializedObject so)
        {
            VLayout(panel, 0f, new RectOffset(24, 24, 22, 20));
            RectTransform head = Rect("Header", panel);
            HLayout(head, 10f, TextAnchor.LowerLeft, new RectOffset());
            Fixed(head.gameObject, -1f, 40f);
            TMP_Text title = Text("Title", head, displayDrop, 30f, Gold, "Select Hero");
            Localize(title, "Select Hero");
            Flexible(title.gameObject, 1f).preferredWidth = 0f;
            Gap(panel, 10f);

            RectTransform grid = Rect("Factions", panel);
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            // The extra 21 px is room for the gem row that hangs under each tile.
            layout.cellSize = new Vector2(266f, HeadingHeight + 8f + TileHeight + 21f);
            layout.spacing = new Vector2(20f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            var tiles = new Object[Factions * 2];
            var names = new Object[Factions];
            var pips = new Object[Factions];
            for (int card = 0; card < Factions; card++)
            {
                RectTransform box = Rect("Faction " + (card + 1), grid);
                VLayout(box, 8f, new RectOffset());
                // CommanderScreenView colours each faction's pip with its display colour; the name stays grey (TJ, 2026-10-09).
                TMP_Text factionName = Heading(box, "Faction", out Image pip);
                factionName.color = Hex("C9D2D8");
                pips[card] = pip;
                Unlocalize(factionName, "Faction");
                names[card] = factionName;
                RectTransform row = Rect("Heroes", box);
                HLayout(row, 12f, TextAnchor.UpperLeft, new RectOffset());
                for (int i = 0; i < 2; i++)
                {
                    GameObject tile = Instance(tilePart, row, "Hero " + (card * 2 + i + 1));
                    tiles[card * 2 + i] = tile.GetComponent<HeroRosterTile>();
                }
            }
            Refs(so, "tiles", tiles);
            Refs(so, "factionNames", names);
            Refs(so, "factionPips", pips);

            Spacer(panel, false);
            RectTransform soon = Rect("Coming Soon", panel);
            HLayout(soon, 10f, TextAnchor.MiddleLeft, new RectOffset(2, 0, 0, 0));
            Image mark = Img(Rect("Mark", soon), mount, Color.white);
            mark.preserveAspect = true;
            Fixed(mark.gameObject, 16f, 16f);
            TMP_Text soonText = Text("Text", soon, display, 14f, Cap, "Olympian Phalanx and one more faction are on the way.");
            soonText.fontStyle = FontStyles.Italic;
            Unlocalize(soonText, "Olympian Phalanx and one more faction are on the way.");
            Ref(so, "comingSoonText", soonText);
            soon.gameObject.SetActive(false);
        }

        static void BuildHeroPanel(RectTransform panel, SerializedObject so)
        {
            VLayout(panel, 0f, new RectOffset(30, 30, 26, 24));

            RectTransform nameRow = Rect("Name Row", panel);
            HLayout(nameRow, 12f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(nameRow.gameObject, -1f, 42f);
            TMP_Text heroName = Text("Name", nameRow, displayDrop, 34f, Gold, "Boblin the Goblin King");
            heroName.enableAutoSizing = true;
            heroName.fontSizeMin = 22f;
            heroName.fontSizeMax = 34f;
            Fixed(heroName.gameObject, -1f, 42f);
            Flexible(heroName.gameObject, 1f).preferredWidth = 0f;
            GameObject story = Instance(standardButton, nameRow, "Story");
            Fixed(story, 30f, 30f);
            HideButtonText(story, keepLabel: true);
            TMP_Text mark = Child<TMP_Text>(story.transform, "Button Label");
            Unlocalize(mark, "?");
            mark.alignment = TextAlignmentOptions.Midline;
            mark.fontSize = 18f;
            CentreInk(mark);
            Ref(so, "heroNameText", heroName);
            Ref(so, "storyTrigger", GetOrAdd<MemoriTooltipTrigger>(story));

            RectTransform factionRow = Rect("Faction Row", panel);
            HLayout(factionRow, 10f, TextAnchor.MiddleLeft, new RectOffset(0, 0, 6, 0));
            Fixed(factionRow.gameObject, -1f, 28f);
            Image pip = Img(Rect("Pip", factionRow), solid, Hex("6DC47C"));
            Fixed(pip.gameObject, 10f, 10f);
            TMP_Text faction = Text("Faction", factionRow, displayDrop, 15f, Hex("6DC47C"), "Gruntkin");
            Unlocalize(faction, "Gruntkin");
            TMP_Text region = Text("Region", factionRow, display, 14f, Cap, "The Mudfen Wastes");
            region.fontStyle = FontStyles.Italic;
            Ref(so, "heroFactionPip", pip);
            Ref(so, "heroFactionText", faction);
            Ref(so, "heroRegionText", region);

            Gap(panel, 10f);
            ScrollRect loreScroll = LoreScroll(panel, out RectTransform loreContent);
            TMP_Text lore = Text("Lore", loreContent, display, 15f, Flavour, "Lore");
            lore.fontStyle = FontStyles.Italic;
            Wrap(lore);
            lore.alignment = TextAlignmentOptions.TopLeft;
            Ref(so, "loreText", lore);
            Ref(so, "loreScroll", loreScroll);
            Gap(panel, 24f);

            Heading(panel, "Hero Effects", out _);
            Gap(panel, 14f);
            // Holds both rows' height, so a hero with one effect leaves the space empty.
            RectTransform heroEffects = Rect("Hero Effect Rows", panel);
            VLayout(heroEffects, 12f, new RectOffset());
            Fixed(heroEffects.gameObject, -1f, 2 * EffectHeight + 12f);
            var titles = new Object[2];
            var bodies = new Object[2];
            var rows = new Object[2];
            for (int i = 0; i < 2; i++)
            {
                rows[i] = EffectRow(heroEffects, "Hero Effect " + (i + 1), heroIcon, null, out TMP_Text title, out TMP_Text body).gameObject;
                titles[i] = title;
                bodies[i] = body;
            }
            Refs(so, "heroEffectTitles", titles);
            Refs(so, "heroEffectBodies", bodies);
            Refs(so, "heroEffectRows", rows);

            Gap(panel, 24f);
            Heading(panel, "CollectionFactionEffects", out Image factionPip, pipColour: Hex("6DC47C"));
            Ref(so, "factionEffectsPip", factionPip);
            Gap(panel, 14f);
            EffectRow(panel, "Campaign Effect", campaignIcon, "CampaignEffectLabel", out TMP_Text campaignTitle, out TMP_Text campaignBody);
            Gap(panel, 12f);
            EffectRow(panel, "Battle Effect", battleIcon, "BattleEffectLabel", out TMP_Text battleTitle, out TMP_Text battleBody);
            Ref(so, "campaignTitle", campaignTitle);
            Ref(so, "campaignBody", campaignBody);
            Ref(so, "battleTitle", battleTitle);
            Ref(so, "battleBody", battleBody);

            Gap(panel, 24f);
            Heading(panel, "heroSignature", out _);
            Gap(panel, 14f);
            RectTransform sig = Rect("Signature", panel);
            HorizontalLayoutGroup sigRow = HLayout(sig, 14f, TextAnchor.UpperLeft, new RectOffset());
            sigRow.childForceExpandWidth = true;
            Fixed(sig.gameObject, -1f, 68f);
            RectTransform unitCard = Card(sig, "Unit", "Unit", out RectTransform unitSquare, out TMP_Text unitName);
            Img(unitSquare, solid, TileFill);
            RectTransform unitMask = Stretch(Rect("Mask", unitSquare), 2f, 2f, 2f, 2f);
            unitMask.gameObject.AddComponent<RectMask2D>();
            Image unitPortrait = Img(Rect("Portrait", unitMask), null, Color.white);
            Centre(unitPortrait.rectTransform, 44f, 66f);
            Image unitFrame = Img(Stretch(Rect("Frame", unitSquare)), frameSmall, Hex("F1C40F"), Image.Type.Sliced);
            unitFrame.fillCenter = false;
            unitFrame.pixelsPerUnitMultiplier = 4f;
            Ref(so, "unitPortrait", unitPortrait);
            Ref(so, "unitFrame", unitFrame);
            Ref(so, "unitNameText", unitName);
            Ref(so, "unitHover", unitCard.gameObject.AddComponent<TroopHoverPlayPanel>());

            RectTransform spellCard = Card(sig, "Spell", "Spell", out RectTransform spellSquare, out TMP_Text spellName);
            Image wash = Img(Stretch(Rect("Wash", spellSquare)), solid, A(Hex("6DC47C"), 0.14f));
            Image spellIcon = Img(Rect("Icon", spellSquare), null, Hex("6DC47C"));
            spellIcon.preserveAspect = true;
            Centre(spellIcon.rectTransform, 28f, 28f);
            Image spellFrame = Img(Stretch(Rect("Frame", spellSquare)), frameSmall, Hex("6DC47C"), Image.Type.Sliced);
            spellFrame.fillCenter = false;
            spellFrame.pixelsPerUnitMultiplier = 4f;
            Ref(so, "spellCard", spellCard.gameObject);
            Ref(so, "spellWash", wash);
            Ref(so, "spellIcon", spellIcon);
            Ref(so, "spellFrame", spellFrame);
            Ref(so, "spellNameText", spellName);
            Ref(so, "spellTooltip", spellCard.gameObject.AddComponent<MemoriTooltipTrigger>());

            Gap(panel, 24f);
            TMP_Text recordTitle = Heading(panel, "heroRecordTitle", out _);
            Unlocalize(recordTitle, "Record with Boblin the Goblin King");
            Ref(so, "recordTitle", recordTitle);
            Gap(panel, 12f);
            RectTransform record = Rect("Record", panel);
            HorizontalLayoutGroup recordRow = HLayout(record, 10f, TextAnchor.UpperCenter, new RectOffset());
            recordRow.childForceExpandWidth = true;
            Fixed(record.gameObject, -1f, 108f);
            var crests = new Object[4];
            var levels = new Object[4];
            var states = new Object[4];
            for (int i = 0; i < 4; i++)
            {
                RectTransform item = Rect("Level " + (i + 1), record);
                VerticalLayoutGroup itemLayout = VLayout(item, 4f, new RectOffset());
                itemLayout.childAlignment = TextAnchor.UpperCenter;
                itemLayout.childForceExpandWidth = false;
                RectTransform crestSlot = Rect("Crest", item);
                Fixed(crestSlot.gameObject, 110f, 62f);
                crests[i] = crestSlot.gameObject.AddComponent<CanvasGroup>();
                Crest(crestSlot, i, 96f);
                TMP_Text level = Text("Level", item, displayDrop, 15f, DifficultyMetal.ForRank(i), "Level");
                level.alignment = TextAlignmentOptions.Center;
                Fixed(level.gameObject, -1f, 20f);
                Unlocalize(level, "Level");
                TMP_Text state = Text("State", item, display, 13f, Hex("7BD66F"), "Won");
                state.fontStyle = FontStyles.Italic;
                state.alignment = TextAlignmentOptions.Center;
                Fixed(state.gameObject, -1f, 18f);
                Unlocalize(state, "Won");
                levels[i] = level;
                states[i] = state;
            }
            Refs(so, "recordCrests", crests);
            Refs(so, "recordLevelNames", levels);
            Refs(so, "recordStates", states);
        }
        #endregion

        #region Warband build panel
        static void BuildWarband(GameObject rootGo)
        {
            var root = (RectTransform)rootGo.transform;
            WarbandBuildView view = GetOrAdd<WarbandBuildView>(rootGo);
            var so = new SerializedObject(view);

            RectTransform panel = Panel(root, "Build Panel", false);
            VLayout(panel, 0f, new RectOffset(16, 16, 24, 24));
            RectTransform head = Rect("Head", panel);
            VLayout(head, 0f, new RectOffset(14, 14, 0, 0));
            TMP_Text title = Text("Title", head, displayDrop, 30f, Gold, "Your starting build");
            Localize(title, "warbandBuildTitle");
            Fixed(title.gameObject, -1f, 40f);
            Gap(head, 8f);

            RectTransform army = Section(panel, "Army", WarbandSection.Army, "startingArmy", out TMP_Text armyCount, out GameObject armyHighlight);
            RectTransform slots = Rect("Slots", army);
            float gridWidth = 5 * ArmySquare + 4 * 12f;
            float gridHeight = 2 * (ArmySquare + ArmyNameHeight + 4f) + 10f;
            Fixed(slots.gameObject, gridWidth, gridHeight);
            RectTransform underlay = Stretch(Rect("Empty Slots", slots));
            GridLayoutGroup underGrid = ArmyGrid(underlay);
            for (int i = 0; i < 10; i++)
            {
                RectTransform cell = Rect("Empty " + (i + 1), underlay);
                RectTransform box = Rect("Box", cell);
                TopCentre(box, ArmySquare, ArmySquare, 0f);
                Img(box, solid, A(Hex("08100F"), 0.3f));
                Image edge = Img(Stretch(Rect("Edge", box)), frameSmall, A(Dim, 0.8f), Image.Type.Sliced);
                edge.fillCenter = false;
                edge.pixelsPerUnitMultiplier = 4f;
                PlusMark(box);
            }
            RectTransform units = Stretch(Rect("Starting Units Parent", slots));
            ArmyGrid(units);

            FlexibleGap(panel, 8f);
            RectTransform row = Rect("Gear And Spells", panel);
            HorizontalLayoutGroup rowLayout = HLayout(row, 28f, TextAnchor.UpperLeft, new RectOffset());
            rowLayout.childForceExpandHeight = true;
            // The moved spell slots carry a flexible height; pinned here so spare space goes to the gaps instead.
            GetOrAdd<LayoutElement>(row.gameObject).flexibleHeight = 0f;
            // Wide enough for the gear heading at the 13 unit floor; the spell slots sit closer to pay for it.
            RectTransform gear = Section(row, "Gear", WarbandSection.Gear, "warbandGearHeading", out TMP_Text gearCount, out GameObject gearHighlight);
            Fixed(gear.gameObject, 226f, -1f);
            WarbandGearSlot gearSlot = GearSlot(gear);
            RectTransform spells = Section(row, "Spells", WarbandSection.Spells, "Spells", out TMP_Text spellCount, out GameObject spellHighlight);
            Flexible(spells.gameObject, 1f).preferredWidth = 0f;
            RectTransform spellSlots = Rect("Spell Slots", spells);
            HorizontalLayoutGroup spellRow = HLayout(spellSlots, 16f, TextAnchor.UpperLeft, new RectOffset(4, 0, 0, 0));
            spellRow.childControlWidth = false;
            spellRow.childControlHeight = false;
            // Room for a spell name on three lines under each slot (German "Kleiner Schadenszauber").
            Fixed(spellSlots.gameObject, -1f, SlotSquare + 64f);

            FlexibleGap(panel, 8f);
            RectTransform difficulty = Section(panel, "Difficulty", null, "Difficulty", out TMP_Text _, out GameObject _, rightLabel: "heroRecordWon", gems: true, gemImages: out Image[] gems);
            RectTransform body = Rect("Body", difficulty);
            HLayout(body, 24f, TextAnchor.MiddleLeft, new RectOffset());
            RectTransform left = Rect("Crest Column", body);
            VerticalLayoutGroup leftLayout = VLayout(left, 8f, new RectOffset());
            leftLayout.childAlignment = TextAnchor.UpperCenter;
            leftLayout.childForceExpandWidth = false;
            Fixed(left.gameObject, 190f, -1f);
            RectTransform crestAnchor = Rect("Crest Anchor", left);
            Fixed(crestAnchor.gameObject, 190f, 112f);
            RectTransform spinner = Rect("Spinner", left);
            HLayout(spinner, 8f, TextAnchor.MiddleCenter, new RectOffset());
            Fixed(spinner.gameObject, 190f, 34f);
            Button easier = ArrowButton(spinner, "Easier", false);
            TMP_Text level = Text("Level", spinner, displayDrop, 24f, Gold, "Hard");
            level.alignment = TextAlignmentOptions.Center;
            level.enableAutoSizing = true;
            level.fontSizeMin = 14f;
            level.fontSizeMax = 24f;
            Unlocalize(level, "Hard");
            Fixed(level.gameObject, 106f, 34f);
            Button harder = ArrowButton(spinner, "Harder", true);

            RectTransform right = Rect("Modifiers", body);
            VLayout(right, 10f, new RectOffset());
            Flexible(right.gameObject, 1f).preferredWidth = 0f;
            TMP_Text description = Text("Difficulty Description", right, display, 15f, Body, "Stronger enemy armies\nNo modifying rolls in events");
            Wrap(description);
            description.lineSpacing = 12f;
            description.alignment = TextAlignmentOptions.TopLeft;
            description.enableAutoSizing = true;
            description.fontSizeMin = 11f;
            description.fontSizeMax = 15f;
            Unlocalize(description, "Stronger enemy armies\nNo modifying rolls in events\nLess healing when entering towns\nSettlements have stronger garrisons");
            Fixed(description.gameObject, -1f, 112f);
            RectTransform extra = Rect("Extra Info", right);
            HLayout(extra, 8f, TextAnchor.MiddleLeft, new RectOffset());
            TMP_Text extraText = Text("Text", extra, display, 13.5f, Cap, "Applies all previous modifiers");
            extraText.fontStyle = FontStyles.Italic;
            Localize(extraText, "Applies all previous modifiers");
            GameObject extraButton = Instance(standardButton, extra, "More");
            Fixed(extraButton, 22f, 22f);
            HideButtonText(extraButton, keepLabel: true);
            TMP_Text extraMark = Child<TMP_Text>(extraButton.transform, "Button Label");
            Unlocalize(extraMark, "?");
            extraMark.alignment = TextAlignmentOptions.Midline;
            extraMark.fontSize = 14f;
            CentreInk(extraMark);
            MemoriTooltipTrigger extraTooltip = GetOrAdd<MemoriTooltipTrigger>(extraButton);

            RectTransform leftBar = Bar(root, "Left Bar", true);
            GameObject back = Instance(backButton, leftBar, "Return");
            Fixed(back, 160f, 46f);
            Localize(Child<TMP_Text>(back.transform, "Button Label"), "returnButton");
            Spacer(leftBar, true);

            RectTransform rightBar = Bar(root, "Right Bar", false);
            RectTransform purse = Rect("Purse", rightBar);
            Flexible(purse.gameObject, 1f).preferredWidth = 0f;
            Fixed(purse.gameObject, -1f, 60f);
            Image purseHit = Img(purse, null, Color.clear);
            purseHit.raycastTarget = true;
            RectTransform ready = Stretch(Rect("Ready", purse));
            TMP_Text purseText = Text("Gold", ready, display, 19f, Cream, "Gold left 4");
            Stretch(purseText.rectTransform);
            RectTransform blocked = Stretch(Rect("Blocked", purse));
            TMP_Text blockedText = Text("Text", blocked, display, 15f, Error, "Blocked");
            Wrap(blockedText);
            blockedText.enableAutoSizing = true;
            blockedText.fontSizeMin = 11f;
            blockedText.fontSizeMax = 15f;
            Stretch(blockedText.rectTransform);
            blocked.gameObject.SetActive(false);
            GameObject start = Instance(primaryButton, rightBar, "Start Campaign");
            Fixed(start, 250f, 46f);
            Localize(Child<TMP_Text>(start.transform, "Button Label"), "startCampaignButton");
            AddSheen(start);
            AddFlare(start.transform, new Vector2(0.5f, 0.5f), Vector2.zero);

            Refs(so, "wonGems", gems);
            so.ApplyModifiedPropertiesWithoutUndo();

        }

        static GridLayoutGroup ArmyGrid(RectTransform target)
        {
            GridLayoutGroup grid = target.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(ArmySquare, ArmySquare + ArmyNameHeight + 4f);
            grid.spacing = new Vector2(12f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            return grid;
        }

        // The equipped item, an empty slot or the Renown lock, each a square with its name under it.
        static WarbandGearSlot GearSlot(RectTransform parent)
        {
            RectTransform slot = Rect("Gear Slot", parent);
            // Square, title and a hint on two lines, at the 13 unit floor.
            Fixed(slot.gameObject, -1f, SlotSquare + 4f + SlotTitleHeight + 4f + SlotHintHeight);

            RectTransform equipped = SlotState(slot, "Equipped", out RectTransform equippedSquare, out TMP_Text equippedName, out TMP_Text equippedSub);
            GameObject tile = Instance(gearTilePrefab, equippedSquare, "Warband Gear Tile");
            RectTransform tileRect = (RectTransform)tile.transform;
            Stretch(tileRect);
            LayoutElement tileLayout = tile.GetComponent<LayoutElement>();
            if (tileLayout != null) tileLayout.ignoreLayout = true;
            RectTransform remove = Rect("Remove", equippedSquare);
            Anchor(remove, Vector2.one, Vector2.one, new Vector2(-14f, -14f), new Vector2(6f, 6f));
            Image removeImage = Img(remove, circle, Hex("4A272A"));
            removeImage.raycastTarget = true;
            Button removeButton = remove.gameObject.AddComponent<Button>();
            removeButton.targetGraphic = removeImage;
            CrossMark(remove);
            TMP_Text description = Text("Description", equipped, display, 12f, Flavour, "Description");
            description.gameObject.SetActive(false);
            Ignore(description.gameObject);

            RectTransform empty = SlotState(slot, "Empty", out RectTransform emptySquare, out TMP_Text emptyTitle, out TMP_Text emptyHint);
            Image emptyEdge = Img(Stretch(Rect("Edge", emptySquare)), frameSmall, Dim, Image.Type.Sliced);
            emptyEdge.fillCenter = false;
            emptyEdge.pixelsPerUnitMultiplier = 4f;
            PlusMark(emptySquare);

            RectTransform locked = SlotState(slot, "Locked", out RectTransform lockedSquare, out TMP_Text lockedTitle, out TMP_Text lockedHint);
            Image lockedEdge = Img(Stretch(Rect("Edge", lockedSquare)), frameSmall, Dim, Image.Type.Sliced);
            lockedEdge.fillCenter = false;
            lockedEdge.pixelsPerUnitMultiplier = 4f;
            Image lockImage = Img(Rect("Lock", lockedSquare), lockIcon, Hex("8C9AA2"));
            lockImage.preserveAspect = true;
            Centre(lockImage.rectTransform, 30f, 30f);
            equipped.gameObject.SetActive(false);
            locked.gameObject.SetActive(false);

            Memori.UI.UIFlare equipFlare = AddHalo(slot, new Vector2(0f, 1f), new Vector2(SlotSquare / 2f, -SlotSquare / 2f));

            WarbandGearSlot component = slot.gameObject.AddComponent<WarbandGearSlot>();
            var so = new SerializedObject(component);
            Ref(so, "equipFlare", equipFlare);
            Ref(so, "equippedRoot", equipped.gameObject);
            Ref(so, "emptyRoot", empty.gameObject);
            Ref(so, "lockedRoot", locked.gameObject);
            Ref(so, "tile", tile.GetComponent<WarbandGearTile>());
            Ref(so, "nameText", equippedName);
            Ref(so, "descriptionText", description);
            Ref(so, "priceText", equippedSub);
            Ref(so, "removeButton", removeButton);
            Ref(so, "emptyTitleText", emptyTitle);
            Ref(so, "emptyHintText", emptyHint);
            Ref(so, "lockedTitleText", lockedTitle);
            Ref(so, "lockedHintText", lockedHint);
            so.ApplyModifiedPropertiesWithoutUndo();
            return component;
        }

        static RectTransform SlotState(RectTransform slot, string name, out RectTransform square, out TMP_Text title, out TMP_Text sub)
        {
            RectTransform state = Stretch(Rect(name, slot));
            VerticalLayoutGroup layout = VLayout(state, 4f, new RectOffset());
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childForceExpandWidth = false;
            square = Rect("Square", state);
            Fixed(square.gameObject, SlotSquare, SlotSquare);
            Img(square, solid, A(Hex("08100F"), 0.4f));
            title = Text("Title", state, displayDrop, 14f, Cream, name);
            title.enableAutoSizing = true;
            title.fontSizeMin = 13f;
            title.fontSizeMax = 14f;
            Fixed(title.gameObject, 170f, SlotTitleHeight);
            Unlocalize(title, name);
            sub = Text("Hint", state, display, 13f, Cap, "Hint");
            sub.fontStyle = FontStyles.Italic;
            Wrap(sub);
            sub.enableAutoSizing = true;
            sub.fontSizeMin = 13f;
            sub.fontSizeMax = 13f;
            sub.alignment = TextAlignmentOptions.TopLeft;
            Fixed(sub.gameObject, 170f, SlotHintHeight);
            Unlocalize(sub, "Hint");
            return state;
        }

        static Button ArrowButton(RectTransform parent, string name, bool right)
        {
            GameObject button = Instance(standardButton, parent, name);
            Fixed(button, 32f, 32f);
            HideButtonText(button, keepLabel: false);
            Image arrow = Img(Rect("Arrow", button.transform), arrowIcon, Gold);
            arrow.preserveAspect = true;
            Centre(arrow.rectTransform, 16f, 16f);
            arrow.rectTransform.localEulerAngles = new Vector3(0f, 0f, right ? 0f : 180f);
            return button.GetComponent<Button>();
        }
        #endregion

        #region Shared widgets
        static RectTransform Panel(RectTransform root, string name, bool left)
        {
            RectTransform panel = Rect(name, root);
            panel.anchorMin = panel.anchorMax = left ? new Vector2(0f, 1f) : Vector2.one;
            panel.pivot = left ? new Vector2(0f, 1f) : Vector2.one;
            panel.sizeDelta = new Vector2(SideWidth, PanelHeight);
            panel.anchoredPosition = new Vector2(left ? Margin : -Margin, -Margin);
            Background(panel);
            return panel;
        }

        static RectTransform Bar(RectTransform root, string name, bool left)
        {
            RectTransform bar = Rect(name, root);
            bar.anchorMin = bar.anchorMax = left ? Vector2.zero : new Vector2(1f, 0f);
            bar.pivot = left ? Vector2.zero : new Vector2(1f, 0f);
            bar.sizeDelta = new Vector2(SideWidth, BarHeight);
            bar.anchoredPosition = new Vector2(left ? Margin : -Margin, Margin);
            Background(bar);
            HLayout(bar, 18f, TextAnchor.MiddleLeft, new RectOffset(18, 24, 0, 0));
            return bar;
        }

        // The standard panel background most of the game's UI already uses.
        static void Background(RectTransform panel)
        {
            GameObject background = Instance(basicBackground, panel, "Basic Background");
            Stretch((RectTransform)background.transform);
            Ignore(background);
            Image fill = Child<Image>(background.transform, "SPR_Background");
            if (fill != null) fill.raycastTarget = true;
            RectTransform texture = Child<RectTransform>(background.transform, "Texture");
            if (texture == null) return;
            texture.offsetMax = new Vector2(texture.offsetMax.x, -TextureTop);
            Image textureImage = texture.GetComponent<Image>();
            if (textureImage != null) textureImage.pixelsPerUnitMultiplier = TexturePixelsPerUnit;
        }

        // A loadout block: hover area, highlight wash, heading with a count, then its content.
        static RectTransform Section(RectTransform parent, string name, WarbandSection? section, string headingKey, out TMP_Text count, out GameObject highlight,
                                     string rightLabel = null, bool gems = false)
            => Section(parent, name, section, headingKey, out count, out highlight, rightLabel, gems, out _);

        static RectTransform Section(RectTransform parent, string name, WarbandSection? section, string headingKey, out TMP_Text count, out GameObject highlight,
                                     string rightLabel, bool gems, out Image[] gemImages)
        {
            RectTransform block = Rect(name, parent);
            VLayout(block, 12f, new RectOffset(14, 14, 14, 14));
            Image hit = Img(block, null, Color.clear);
            hit.raycastTarget = section.HasValue;

            Image wash = Img(Stretch(Rect("Highlight", block)), edgeFade, A(Brass, 0.14f));
            Ignore(wash.gameObject);
            Image bar = Img(Rect("Edge", wash.transform), solid, Brass);
            Anchor(bar.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(2f, 0f));
            highlight = wash.gameObject;
            wash.gameObject.SetActive(false);

            RectTransform head = Rect("Heading", block);
            HLayout(head, 10f, TextAnchor.MiddleLeft, new RectOffset());
            TMP_Text label = Text("Label", head, displayDrop, SectionTitleSize, Gold, headingKey);
            Localize(label, headingKey);
            GetOrAdd<LayoutElement>(label.gameObject).minWidth = 40f;
            // Holds the count or the won gems at the block's right edge.
            Spacer(head, true);
            count = null;
            gemImages = null;
            if (gems)
            {
                TMP_Text won = Text("Won", head, displayDrop, 15f, Cap, rightLabel);
                Localize(won, rightLabel);
                RectTransform gemRow = Rect("Gems", head);
                HLayout(gemRow, 9f, TextAnchor.MiddleCenter, new RectOffset(2, 2, 0, 0)).childControlWidth = false;
                gemImages = new Image[4];
                for (int i = 0; i < 4; i++)
                {
                    Image gem = Img(Rect("Gem " + (i + 1), gemRow), solid, DifficultyMetal.ForRank(i));
                    Fixed(gem.gameObject, 8f, 8f);
                    gem.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
                    gemImages[i] = gem;
                }
            }
            else
            {
                count = Text("Count", head, displayDrop, 15f, Flavour, "0 / 0");
                count.alignment = TextAlignmentOptions.MidlineRight;
                GetOrAdd<LayoutElement>(count.gameObject).minWidth = 34f;
                Unlocalize(count, "0 / 0");
            }

            if (section.HasValue)
            {
                // WarbandPanel dims the wash and its edge together through this group on hover.
                GetOrAdd<CanvasGroup>(wash.gameObject);
                WarbandSectionHoverArea area = block.gameObject.AddComponent<WarbandSectionHoverArea>();
                var so = new SerializedObject(area);
                so.FindProperty("section").intValue = (int)section.Value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            return block;
        }

        // A section title. CommanderScreenView deals the hero panel in by blocks that start at each "Heading " child.
        // Grey caps with a coloured pip, as before Phase 1 (TJ kept this look, 2026-10-09); no trailing line.
        static TMP_Text Heading(RectTransform parent, string key, out Image pip, Color? pipColour = null)
        {
            RectTransform head = Rect("Heading " + key, parent);
            HLayout(head, 8f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(head.gameObject, -1f, HeadingHeight);
            pip = null;
            if (pipColour.HasValue || key == "Faction")
            {
                pip = Img(Rect("Pip", head), solid, pipColour ?? Hex("6DC47C"));
                Fixed(pip.gameObject, 9f, 9f);
            }
            TMP_Text label = Text("Label", head, displayDrop, 13f, Cap, key);
            label.fontStyle = FontStyles.UpperCase;
            label.characterSpacing = 12f;
            if (key != "Faction") Localize(label, key);
            return label;
        }

        static RectTransform EffectRow(RectTransform parent, string name, Sprite icon, string tagKey, out TMP_Text title, out TMP_Text body)
        {
            RectTransform row = Rect(name, parent);
            HLayout(row, 14f, TextAnchor.UpperLeft, new RectOffset());
            Fixed(row.gameObject, -1f, EffectHeight);
            Mount(row, "Mount", 36f, icon, 18f);
            RectTransform texts = Rect("Texts", row);
            VLayout(texts, 3f, new RectOffset(0, 0, 2, 0));
            LayoutElement textsLayout = Flexible(texts.gameObject, 1f);
            textsLayout.preferredWidth = 0f;
            textsLayout.flexibleHeight = 1f;
            RectTransform titleRow = Rect("Title Row", texts);
            HLayout(titleRow, 10f, TextAnchor.MiddleLeft, new RectOffset());
            Fixed(titleRow.gameObject, -1f, 22f);
            title = Text("Title", titleRow, displayDrop, 16.5f, Gold, "Effect");
            Unlocalize(title, "Effect");
            if (tagKey != null)
            {
                TMP_Text tag = Text("Tag", titleRow, displayDrop, 15f, Cap, tagKey);
                Localize(tag, tagKey);
            }
            body = Text("Body", texts, display, 15f, Body, "Body");
            Wrap(body);
            body.alignment = TextAlignmentOptions.TopLeft;
            // Long text shrinks inside the row's fixed height instead of growing it.
            body.enableAutoSizing = true;
            body.fontSizeMin = 12f;
            body.fontSizeMax = 15f;
            body.overflowMode = TextOverflowModes.Ellipsis;
            LayoutElement bodyLayout = GetOrAdd<LayoutElement>(body.gameObject);
            bodyLayout.minHeight = 0f;
            bodyLayout.preferredHeight = 0f;
            bodyLayout.flexibleHeight = 1f;
            Unlocalize(body, "Body");
            return row;
        }

        static RectTransform Card(RectTransform parent, string name, string labelKey, out RectTransform square, out TMP_Text nameText)
        {
            RectTransform card = Rect(name + " Card", parent);
            HLayout(card, 14f, TextAnchor.MiddleLeft, new RectOffset(10, 12, 10, 10));
            Flexible(card.gameObject, 1f).preferredWidth = 0f;
            // The shared panel background with only its fill and frame showing.
            GameObject background = Instance(basicBackground, card, "Basic Background");
            Stretch((RectTransform)background.transform);
            Ignore(background);
            Child<RectTransform>(background.transform, "corners").gameObject.SetActive(false);
            Child<RectTransform>(background.transform, "Texture").gameObject.SetActive(false);
            Child<Image>(background.transform, "frame").pixelsPerUnitMultiplier = 12f;
            square = Rect("Square", card);
            Fixed(square.gameObject, 48f, 48f);
            RectTransform texts = Rect("Texts", card);
            VLayout(texts, 6f, new RectOffset());
            Flexible(texts.gameObject, 1f).preferredWidth = 0f;
            RectTransform labelRow = Rect("Label Row", texts);
            HLayout(labelRow, 8f, TextAnchor.MiddleLeft, new RectOffset());
            TMP_Text label = Text("Label", labelRow, displayDrop, 14f, Cap, labelKey);
            Localize(label, labelKey);
            nameText = Text("Name", texts, displayDrop, 19f, Cream, name);
            nameText.enableAutoSizing = true;
            nameText.fontSizeMin = 13f;
            nameText.fontSizeMax = 19f;
            Unlocalize(nameText, name);
            return card;
        }

        // Fixed-height story box; the full lore scrolls inside it with the codex's slim brass bar.
        static ScrollRect LoreScroll(RectTransform parent, out RectTransform content)
        {
            RectTransform rect = Rect("Lore Scroll", parent);
            Fixed(rect.gameObject, -1f, 64f);
            ScrollRect scroll = rect.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Stretch(Rect("Viewport", rect), 0f, 14f, 0f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A clear graphic lets the wheel scroll from anywhere over the text.
            Img(viewport, null, Color.clear).raycastTarget = true;
            content = Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            VLayout(content, 0f, new RectOffset());
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 20f;
            scroll.inertia = true;

            RectTransform barRect = Rect("Scrollbar", rect);
            Anchor(barRect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-4f, 0f), Vector2.zero);
            Img(barRect, null, A(Brass, 0.08f)).raycastTarget = true;
            RectTransform slidingArea = Stretch(Rect("Sliding Area", barRect));
            RectTransform handle = Stretch(Rect("Handle", slidingArea));
            Image handleImage = Img(handle, null, A(Brass, 0.5f));
            handleImage.raycastTarget = true;
            Scrollbar scrollbar = barRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            Navigation nav = scrollbar.navigation;
            nav.mode = Navigation.Mode.None;
            scrollbar.navigation = nav;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }

        // Drawn from two bars, because the font's "+" glyph sits below the centre of its box.
        static void PlusMark(RectTransform parent)
        {
            RectTransform plus = Rect("Plus", parent);
            Centre(plus, 20f, 20f);
            Centre(Img(Rect("Across", plus), solid, Hex("4F616B")).rectTransform, 20f, 2f);
            Centre(Img(Rect("Down", plus), solid, Hex("4F616B")).rectTransform, 2f, 20f);
        }

        // Drawn from two bars, because the display font has no "✕" glyph and draws a box instead.
        static void CrossMark(RectTransform parent)
        {
            RectTransform cross = Rect("X", parent);
            Centre(cross, 10f, 10f);
            for (int i = 0; i < 2; i++)
            {
                Image bar = Img(Rect(i == 0 ? "Bar A" : "Bar B", cross), solid, Hex("FFDCD8"));
                Centre(bar.rectTransform, 12f, 2f);
                bar.rectTransform.localEulerAngles = new Vector3(0f, 0f, i == 0 ? 45f : -45f);
            }
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

        #region Crests
        // The shared difficulty crest prefabs, Squire to Godking, which the difficulty picker shows too.
        static readonly string[] CrestPaths =
        {
            "Assets/Data/Prefabs/UI/Menu/Difficulty Crest Bronze.prefab",
            "Assets/Data/Prefabs/UI/Menu/Difficulty Crest Silver.prefab",
            "Assets/Data/Prefabs/UI/Menu/Difficulty Crest Gold.prefab",
            "Assets/Data/Prefabs/UI/Menu/Difficulty Crest Godking.prefab",
        };

        // A level's crest, scaled from the picker's size to the record slot's width.
        static void Crest(RectTransform parent, int rank, float width)
        {
            GameObject crestPrefab = Load<GameObject>(CrestPaths[rank]);
            GameObject crest = Instance(crestPrefab, parent, crestPrefab.name);
            crest.SetActive(true);
            RectTransform rect = (RectTransform)crest.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            // The metal crests' art is at most 1130 x 830 units around y 13, which fits the slot's 62 px height; Godking's
            // dragon wings span about 1900 units around y -150.
            bool godking = rank == CrestPaths.Length - 1;
            float scale = width / (godking ? 1900f : 1280f);
            rect.localScale = new Vector3(scale, scale, 1f);
            rect.anchoredPosition = new Vector2(0f, (godking ? -150f : -13f) * scale);
        }
        #endregion

        static void HideButtonText(GameObject button, bool keepLabel)
        {
            foreach (string name in new[] { "Button Label", "Secondary Label", "Icon" })
            {
                if (keepLabel && name == "Button Label") continue;
                Transform child = button.transform.Find(name);
                if (child != null) child.gameObject.SetActive(false);
            }
            Transform labelNode = button.transform.Find("Button Label");
            TMP_Text label = labelNode != null ? labelNode.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                foreach (LocalizeStringEvent localizer in label.GetComponents<LocalizeStringEvent>()) Object.DestroyImmediate(localizer);
                label.enableAutoSizing = false;
            }
        }

        static void Gap(RectTransform parent, float height)
        {
            RectTransform gap = Rect("Gap", parent);
            Fixed(gap.gameObject, -1f, height);
        }

        // A gap that also takes an equal share of the panel's spare height.
        static void FlexibleGap(RectTransform parent, float height)
        {
            RectTransform gap = Rect("Gap", parent);
            LayoutElement element = gap.gameObject.AddComponent<LayoutElement>();
            element.minHeight = height;
            element.preferredHeight = height;
            element.flexibleHeight = 1f;
        }

        static void Spacer(RectTransform parent, bool horizontal)
        {
            LayoutElement spacer = Rect("Spacer", parent).gameObject.AddComponent<LayoutElement>();
            if (horizontal) spacer.flexibleWidth = 1f;
            else spacer.flexibleHeight = 1f;
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
            if (component == null) Debug.LogError($"RunSetupBuilder: {root.name} has no {typeof(T).Name} at '{path}'.");
            return component;
        }

        static void Wrap(TMP_Text text) => text.textWrappingMode = TextWrappingModes.Normal;

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

        static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        static void Centre(RectTransform rect, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = Vector2.zero;
        }

        // Pinned to the top centre of its parent, nudged up by lift so tall portraits show the head.
        static void TopCentre(RectTransform rect, float width, float height, float lift)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(0f, lift);
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
            if (property == null) { Debug.LogError($"RunSetupBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            if (property.isArray) return;
            property.objectReferenceValue = value;
        }

        static void Refs(SerializedObject so, string field, Object[] values)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null) { Debug.LogError($"RunSetupBuilder: no field '{field}' on {so.targetObject.GetType().Name}."); return; }
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        #endregion
    }
}
