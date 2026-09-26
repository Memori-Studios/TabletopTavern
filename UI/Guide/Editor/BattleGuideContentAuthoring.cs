using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.Video;
using Cond = TJ.BattlefieldTutorial.BattlefieldInfoCondition;

namespace TJ.GuideEditor
{
    /// <summary>
    /// The Battle Guide's topics and their English text. Rebuilding writes Battle Guide Content.asset and adds or
    /// updates every Guide_ key in the English table. Keys without the Guide_ prefix are reused and never written.
    /// </summary>
    public static class BattleGuideContentAuthoring
    {
        const string TableName = "MainLocalizationTable";
        const string Gifs = "Assets/Art/Gifs/";

        static readonly Dictionary<string, string> english = new();

        [MenuItem("Tabletop Tavern/Battle Guide/Rebuild Content (overwrites the asset)")]
        public static void Rebuild()
        {
            english.Clear();
            List<GuideTopic> topics = Topics();
            UiStrings();

            BattleGuideContent content = AssetDatabase.LoadAssetAtPath<BattleGuideContent>(BattleGuideBuilder.ContentPath);
            if (content == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(BattleGuideBuilder.ContentPath));
                content = ScriptableObject.CreateInstance<BattleGuideContent>();
                AssetDatabase.CreateAsset(content, BattleGuideBuilder.ContentPath);
            }
            content.topics = topics;
            EditorUtility.SetDirty(content);

            WriteEnglish();
            AssetDatabase.SaveAssets();
            Debug.Log($"BattleGuideContentAuthoring: {topics.Count} topics, {english.Count} Guide_ strings.");
        }

        static void WriteEnglish()
        {
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
            StringTable table = (StringTable)collection.GetTable("en");
            foreach (KeyValuePair<string, string> pair in english)
            {
                if (collection.SharedData.GetEntry(pair.Key) == null) collection.SharedData.AddKey(pair.Key);
                table.AddEntry(pair.Key, pair.Value);
            }
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
        }

        /// <summary>Every Guide_ key with its English text, for the translation pass.</summary>
        public static Dictionary<string, string> EnglishStrings()
        {
            english.Clear();
            Topics();
            UiStrings();
            return new Dictionary<string, string>(english);
        }

        #region Builders
        static string T(string key, string text)
        {
            english[key] = text;
            return key;
        }

        static VideoClip Clip(string file) => AssetDatabase.LoadAssetAtPath<VideoClip>(Gifs + file);

        static GuideTopic Topic(string id, string chapter, string title, string summary, string caption, string clip,
            Cond condition, int priority, string[] related, params GuideBlock[] blocks)
        {
            return new GuideTopic
            {
                id = id,
                chapterKey = chapter,
                titleKey = title,
                summaryKey = T($"Guide_{id}_Summary", summary),
                captionKey = T($"Guide_{id}_Caption", caption),
                video = Clip(clip),
                condition = condition,
                tipPriority = priority,
                related = new List<string>(related),
                blocks = new List<GuideBlock>(blocks),
            };
        }

        static GuideBlock Block(GuideBlockType type, string title, bool fullWidth, params GuideRow[] rows) =>
            new() { type = type, titleKey = title, fullWidth = fullWidth, rows = new List<GuideRow>(rows) };

        static GuideBlock Formula(string title, string formula, string text) =>
            new() { type = GuideBlockType.Formula, titleKey = title, fullWidth = true, formulaKey = formula, textKey = text };

        static GuideRow Key(string keys, string text) => new() { keys = keys, textKey = text };
        static GuideRow Term(string label, string text, string keys = "") => new() { labelKey = label, textKey = text, keys = keys };
        static GuideRow Item(string text) => new() { textKey = text };
        #endregion

        #region Topics
        static List<GuideTopic> Topics()
        {
            string chControls = T("Guide_Chapter_Controls", "Controls");
            string chUnits = T("Guide_Chapter_Units", "Units");
            string chCombat = T("Guide_Chapter_Combat", "Combat");
            string chSpells = T("Guide_Chapter_Spells", "Spells");

            var topics = new List<GuideTopic>
            {
                Topic("controls", chControls, T("Guide_controls_Title", "Selecting and Orders"),
                    "Pick squads with the mouse and give them orders. The keys shown here follow your own bindings from Settings.",
                    "Left click selects a squad. Right click sends it at an enemy.",
                    "controls.mp4", Cond.None, 0, new[] { "camera", "actions" },
                    Block(GuideBlockType.Keys, T("Guide_controls_Selecting", "Selecting"), false,
                        Key("LMB", T("Guide_controls_Select", "Select a squad")),
                        Key("LMB ~Guide_Key_Drag", T("Guide_controls_Box", "Draw a box to select several squads")),
                        Key("@AddToSelection + LMB", T("Guide_controls_Add", "Add or remove a squad")),
                        Key("@SelectAll", T("Guide_controls_All", "Select every squad")),
                        Key("LMB ~Guide_Key_Twice", T("Guide_controls_Type", "Double click a squad to select all of that type"))),
                    Block(GuideBlockType.Keys, T("Guide_controls_Orders", "Orders"), false,
                        Key("RMB", T("Guide_controls_Attack", "Attack an enemy, or move to a point")),
                        Key("RMB ~Guide_Key_Drag", T("Guide_controls_Face", "Drag to set the line's facing and width")),
                        Key("@QueueOrder + RMB", T("Guide_controls_Queue", "Queue an order after the current one")),
                        Key("@RepositionSelectedUnits + LMB ~Guide_Key_Drag", T("Guide_controls_Block", "Move the squads as one block, keeping their shape")),
                        Key("@ShowUnitMovementFinal", T("Guide_controls_Show", "Show or hide orders, arrows and ranges"))),
                    Block(GuideBlockType.Keys, T("Guide_controls_Groups", "Groups"), false,
                        Key("@Group", T("Guide_controls_Group", "Group the selected squads. Press again to ungroup")),
                        Key("@SelectGroup1 - @SelectGroup10", T("Guide_controls_PickGroup", "Select a group")),
                        Key("@AddToSelection + @SelectGroup1", T("Guide_controls_Assign", "Put the selection into that group"))),
                    Block(GuideBlockType.Keys, T("Guide_controls_Cards", "Squad cards"), false,
                        Key("LMB ~Guide_Key_Twice", T("Guide_controls_Focus", "Double click a card to move the camera to it")),
                        Key("@AddToSelection + LMB ~Guide_Key_Twice", T("Guide_controls_CardType", "Add every squad of that type")))),

                Topic("camera", chControls, T("Guide_camera_Title", "Camera and Speed"),
                    "Move around the table and set the pace of the fight.",
                    "Speed and pause work once the battle starts, not during deployment.",
                    "controls.mp4", Cond.None, 0, new[] { "controls", "actions" },
                    Block(GuideBlockType.Keys, T("Guide_camera_Camera", "Camera"), false,
                        Key("@Forward @Left @Back @Right", T("Guide_camera_Move", "Move the camera")),
                        Key("@MoveFast", T("Guide_camera_Fast", "Hold to move faster")),
                        Key("@RotateRight / @RotateLeft", T("Guide_camera_Rotate", "Turn the camera")),
                        Key("MMB ~Guide_Key_Drag", T("Guide_camera_Orbit", "Turn and tilt with the mouse")),
                        Key("@RaiseCamera / @LowerCamera", T("Guide_camera_Height", "Raise or lower the camera")),
                        Key("@PitchCameraUp / @PitchCameraDown", T("Guide_camera_Tilt", "Tilt the camera up or down")),
                        Key("MMB ~Guide_Key_Scroll", T("Guide_camera_Zoom", "Zoom in and out"))),
                    Block(GuideBlockType.Keys, T("Guide_camera_Speed", "Battle speed"), false,
                        Key("@SpeedDown / @SpeedUp", T("Guide_camera_Pace", "Slow down or speed up: half speed, normal or triple")),
                        Key("@PauseGame", T("Guide_camera_Pause", "Pause and resume")),
                        Key("@Settings", T("Guide_camera_Menu", "Open the menu. The battle waits while it is open")),
                        Key("@HideBattleUI", T("Guide_camera_Hide", "Hide the interface, flags and cursor for a clear view")))),

                Topic("actions", chControls, T("Guide_actions_Title", "Action Buttons"),
                    "The buttons under the squad cards change how the selected squads behave. Each one has a hotkey.",
                    "Guard Mode keeps a squad on its spot instead of chasing.",
                    "actions.mp4", Cond.None, 2, new[] { "archers", "shielded", "controls" },
                    Block(GuideBlockType.Terms, T("Guide_actions_Every", "Every squad"), false,
                        Term("Guard Mode", T("Guide_actions_Guard", "Melee squads hold their spot and only fight enemies that attack them. Ranged squads still shoot anything in range but do not advance."), "@ToggleGuardMode"),
                        Term("Halt", T("Guide_actions_Halt", "Cancels every order. The squad stops, holds its ground and switches to Guard Mode."), "@Halt"),
                        Term("Withdraw Squad", T("Guide_actions_Withdraw", "The squad leaves the battle for good and cannot be ordered again. Its survivors stay in your army. Works on one squad at a time."), "@Withdraw")),
                    Block(GuideBlockType.Terms, T("Guide_actions_ByType", "Added by unit type"), false,
                        Term("StandardShields", T("Guide_actions_Shielded", "Balanced Stance and Defensive Stance. See Shields and Stances.")),
                        Term(T("Guide_actions_RangedLabel", "Ranged"), T("Guide_actions_Ranged", "Volley Fire, Fire At Will, Melee Mode, Auto Retarget and Cease Fire. See Archers.")),
                        Term(T("Guide_actions_MagesLabel", "Mages"), T("Guide_actions_Mages", "Free Cast and Hold Spells. See Mages.")),
                        Term(T("Guide_actions_CustomLabel", "Custom battles"), T("Guide_actions_Custom", "During deployment, the Withdraw key removes the selected squads."), "@Withdraw"))),

                Topic("stats", chUnits, T("Guide_stats_Title", "Stats"),
                    "A handful of numbers describe every squad. Hover a squad card to read them.",
                    "The stat block on a squad card.",
                    "Stats.mp4", Cond.None, 2, new[] { "traits", "combat", "charge" },
                    Block(GuideBlockType.TermsInline, T("Guide_stats_Melee", "Melee"), false,
                        Term("MeleeAttack", "MeleeAttackDesc"),
                        Term("MeleeDefense", "MeleeDefenseDesc"),
                        Term("WeaponStrength", "WeaponStrengthDesc"),
                        Term("ChargeBonus", "ChargeBonusDesc")),
                    Block(GuideBlockType.TermsInline, T("Guide_stats_Ranged", "Ranged"), false,
                        Term("Accuracy", "AccuracyDesc"),
                        Term("MissileStrength", "MissileStrengthDesc"),
                        Term("Range", "RangeDesc")),
                    Block(GuideBlockType.TermsInline, T("Guide_stats_Staying", "Staying power"), false,
                        Term("HitPoints", "HitPointsDesc"),
                        Term("Armor", "ArmorDesc"),
                        Term("Leadership", "LeadershipDesc")),
                    Block(GuideBlockType.TermsInline, T("Guide_stats_Moving", "Moving"), false,
                        Term("Speed", "SpeedDesc"))),

                Topic("traits", chUnits, T("Guide_traits_Title", "Traits"),
                    "Traits are the tags on a squad card. Each one bends a rule for that squad. Rarer units carry more; hover any tag to read it.",
                    "Trait tags sit under the squad name on its card.",
                    "Stats.mp4", Cond.None, 2, new[] { "stats", "shielded", "antilarge" },
                    Block(GuideBlockType.TermsInline, T("Guide_traits_Offense", "Offense"), false,
                        Term("ArmorPiercing", "ArmorPiercingDesc"),
                        Term("AntiInfantry", "AntiInfantryDesc"),
                        Term("AntiLarge", "AntiLargeDesc"),
                        Term("Terrifying", "TerrifyingDesc")),
                    Block(GuideBlockType.TermsInline, T("Guide_traits_Defense", "Defense"), false,
                        Term("StandardShields", "StandardShieldsDesc"),
                        Term("HeavyShields", "HeavyShieldsDesc"),
                        Term("Armored", "ArmoredDesc"),
                        Term("Stalwart", "StalwartDesc"),
                        Term("Large", "LargeDesc"))),

                Topic("shielded", chUnits, T("Guide_shielded_Title", "Shields and Stances"),
                    "Shields stop projectiles unless they come from behind. Shielded squads can also trade attack for defense.",
                    "A shield wall turning volleys aside.",
                    "Shields.mp4", Cond.PlayerArmyContainsShields, 1, new[] { "flanking", "archers", "traits" },
                    Block(GuideBlockType.TermsInline, T("Guide_shielded_Block", "Block chance"), false,
                        Term("StandardShields", "StandardShieldsDesc"),
                        Term("HeavyShields", "HeavyShieldsDesc"),
                        Term(T("Guide_shielded_BehindLabel", "From behind"), T("Guide_shielded_Behind", "Nothing is blocked.")),
                        Term(T("Guide_shielded_NotLabel", "Never"), T("Guide_shielded_Not", "Shields do not help in melee or against spells."))),
                    Block(GuideBlockType.Terms, T("Guide_shielded_Stances", "Stances"), false,
                        Term("BalancedStanceTitle", T("Guide_shielded_Balanced", "The default. No change to stats."), "@SetBalancedStance"),
                        Term("DefensiveStanceTitle", T("Guide_shielded_Defensive", "Melee Attack -50%, Melee Defense +50%. Use it to hold a line, not to win a fight."), "@SetDefensiveStance"))),

                Topic("antilarge", chUnits, T("Guide_antilarge_Title", "Anti Large"),
                    "Spears and pikes answer cavalry and monsters. They hit large units twice as hard and blunt every charge.",
                    "A braced pike line meeting a cavalry charge.",
                    "braced.mp4", Cond.PlayerArmyContainsAntiLarge, 1, new[] { "charge", "traits" },
                    Block(GuideBlockType.Terms, T("Guide_antilarge_Against", "Against large units"), false,
                        Term(T("Guide_antilarge_DoubleLabel", "Double damage"), T("Guide_antilarge_Double", "Every hit on cavalry or a monster deals twice its damage.")),
                        Term(T("Guide_antilarge_NoChargeLabel", "No charge bonus"), T("Guide_antilarge_NoCharge", "Any squad that charges into an Anti Large squad loses its charge bonus."))),
                    Block(GuideBlockType.Terms, T("Guide_antilarge_Bracing", "Bracing"), false,
                        Term("Braced", T("Guide_antilarge_Braced", "The squad braces whenever it stands still. Charges into it cause no knockback, and it is twice as hard to push.")),
                        Term(T("Guide_antilarge_StillLabel", "Keep them still"), T("Guide_antilarge_Still", "A moving squad cannot brace. Set it in place and let the enemy come.")))),

                Topic("archers", chUnits, T("Guide_archers_Title", "Archers"),
                    "Ranged squads choose how fast they fire and whether they pick their own targets.",
                    "Volley Fire on the left, Fire At Will on the right.",
                    "archers.mp4", Cond.PlayerArmyContainsRanged, 1, new[] { "actions", "flanking", "shielded" },
                    Block(GuideBlockType.Terms, T("Guide_archers_Modes", "Fire modes"), false,
                        Term("VolleyFireTitle", T("Guide_archers_Volley", "The default. The squad fires together."), "@ToggleVolleyFireMode"),
                        Term("FireAtWillTitle", T("Guide_archers_FAW", "About twice the rate of fire, but 20 less Accuracy."), "@ToggleFireAtWillMode"),
                        Term("MeleeModeTitle", T("Guide_archers_Melee", "The squad stops shooting and charges into melee. Press again to go back to shooting."), "@ToggleMeleeMode")),
                    Block(GuideBlockType.Terms, T("Guide_archers_Targets", "Targeting"), false,
                        Term("AutoRetargetTitle", T("Guide_archers_Auto", "With no target, the squad picks the nearest enemy on its own. Without it, an idle squad waits."), "@ToggleAutoRetarget"),
                        Term("CeaseFireTitle", T("Guide_archers_Cease", "Stops shooting and holds position until you give an attack order."), "@CeaseFireCommand"),
                        Term(T("Guide_archers_FlankLabel", "Projectiles from behind"), T("Guide_archers_Flank", "They ignore shields. See Flanking.")))),

                Topic("mages", chUnits, T("Guide_mages_Title", "Mages"),
                    "A mage is a one-model unit that casts its spell at targets in range. It deploys with the back line and walks into range on its own.",
                    "The blue ring is the cast range. Hover a mage to see its spell card.",
                    "Mages.mp4", Cond.PlayerArmyContainsMage, 1, new[] { "spells", "actions" },
                    Block(GuideBlockType.Terms, T("Guide_mages_Casting", "Casting"), false,
                        Term(T("Guide_mages_RangeLabel", "Cast range"), T("Guide_mages_Range", "The blue ring reaches in every direction. A mage can cast at anything inside it, even behind it.")),
                        Term(T("Guide_mages_ChargesLabel", "Charges"), T("Guide_mages_Charges", "Each cast spends one charge from the bar under its flag, and the bar below counts down to the next cast. With no charges left, the mage fights on in melee.")),
                        Term(T("Guide_mages_MeleeLabel", "In melee"), T("Guide_mages_Melee", "A mage does not cast while it is locked in melee."))),
                    Block(GuideBlockType.Terms, T("Guide_mages_Orders", "Your orders"), false,
                        Term("MageFreeCastTitle", T("Guide_mages_Free", "On by default: the mage picks its own targets. Off: it casts only when you tell it to."), "@ToggleAutoRetarget"),
                        Term(T("Guide_mages_AimLabel", "Aim a cast"), T("Guide_mages_Aim", "Select the mage and click its spell tile above the card, or hold the key and press its number. Then click the target. It walks into range if it must."), "@SpellMenu + 4 - 0"),
                        Term("MageHoldSpellsTitle", T("Guide_mages_Hold", "The mage stops casting until you give an order. It keeps its charges."), "@CeaseFireCommand"))),

                Topic("combat", chCombat, T("Guide_combat_Title", "Hit Chance and Armor"),
                    "Two things decide every melee hit. Does the blow land? Then, how much of it gets through the armor?",
                    "Two squads trading blows in melee.",
                    "Combat.mp4", Cond.None, 2, new[] { "stats", "flanking", "traits" },
                    Formula(T("Guide_combat_LandTitle", "Does it land?"),
                        T("Guide_combat_LandFormula", "Hit chance = 35% + 2% for each point of Melee Attack above the target's Melee Defense"),
                        T("Guide_combat_LandText", "Each point below takes 2% off instead. The chance never drops below 10% or rises above 90%. A flanked target fights with half its Melee Defense.")),
                    Formula(T("Guide_combat_ArmorTitle", "How much gets through?"),
                        T("Guide_combat_ArmorFormula", "Share blocked = Armor / (Armor + 100)"),
                        T("Guide_combat_ArmorText", "50 Armor blocks a third of each hit. 100 Armor blocks half. Armor Piercing halves the share blocked, and spells ignore armor completely."))),

                Topic("charge", chCombat, T("Guide_charge_Title", "Charging"),
                    "A charge is a short, hard burst. Cavalry that stays in the fight after the bonus fades is just expensive infantry.",
                    "Cavalry hitting a flank at full speed.",
                    "Charge.mp4", Cond.PlayerArmyContainsLarge, 1, new[] { "antilarge", "flanking", "stats" },
                    Block(GuideBlockType.Terms, T("Guide_charge_Bonus", "The bonus"), false,
                        Term("ChargeBonus", "ChargeBonusDesc"),
                        Term(T("Guide_charge_TimingLabel", "Timing"), T("Guide_charge_Timing", "The squad must charge for 2 seconds to earn it. After contact it lasts 6 seconds.")),
                        Term(T("Guide_charge_CountLabel", "Limited charges"), T("Guide_charge_Count", "Each charge uses one of the squad's charges. When they run out the squad is Exhausted and earns no more bonuses.")),
                        Term(T("Guide_charge_ImpactLabel", "Impact"), T("Guide_charge_Impact", "Cavalry and monsters charging into infantry knock them back and deal impact damage."))),
                    Block(GuideBlockType.ListDown, T("Guide_charge_NoBonus", "No bonus when"), false,
                        Item(T("Guide_charge_No1", "The squad charges out of a forest or swamp")),
                        Item(T("Guide_charge_No2", "It is raining")),
                        Item(T("Guide_charge_No3", "The target is Anti Large")),
                        Item(T("Guide_charge_No4", "The target is a gate"))),
                    Block(GuideBlockType.Terms, T("Guide_charge_Using", "Using it"), true,
                        Term(T("Guide_charge_CycleLabel", "Cycle charging"), T("Guide_charge_Cycle", "Charge, pull out before the bonus fades, and charge again. Shock cavalry wins by never staying.")))),

                Topic("flanking", chCombat, T("Guide_flanking_Title", "Flanking"),
                    "Hitting a squad from behind is the fastest way to break it. The same goes for your own squads.",
                    "Markers show which squads are flanking and being flanked.",
                    "flanking.mp4", Cond.None, 2, new[] { "morale", "shielded", "charge" },
                    Block(GuideBlockType.Terms, T("Guide_flanking_You", "When you flank"), false,
                        Term(T("Guide_flanking_MeleeLabel", "In melee"), T("Guide_flanking_Melee", "The target's Melee Defense is halved, and it loses morale every second.")),
                        Term(T("Guide_flanking_RangedLabel", "At range"), T("Guide_flanking_Ranged", "Projectiles into the rear ignore shields."))),
                    Block(GuideBlockType.Terms, T("Guide_flanking_Them", "When you are flanked"), false,
                        Term(T("Guide_flanking_AnswerLabel", "Answer it"), T("Guide_flanking_Answer", "Turn the squad to face the threat, or send another squad into the attacker's own flank.")),
                        Term(T("Guide_flanking_SingleLabel", "Single models"), T("Guide_flanking_Single", "A squad of one model, such as a mage, cannot be flanked.")))),

                Topic("morale", chCombat, T("Guide_morale_Title", "Morale"),
                    "Every squad has a morale bar under its health. When morale runs out, the squad routs and flees for good.",
                    "A broken squad fleeing the line.",
                    "morale.mp4", Cond.None, 2, new[] { "flanking", "traits", "spells" },
                    Block(GuideBlockType.ListUp, T("Guide_morale_Up", "Raises morale"), false,
                        Item(T("Guide_morale_Up1", "Staying out of harm. After 5 seconds without damage, morale recovers")),
                        Item(T("Guide_morale_Up2", "Winning the fight: dealing clearly more damage than it takes")),
                        Item(T("Guide_morale_Up3", "Standing in a morale spell"))),
                    Block(GuideBlockType.ListDown, T("Guide_morale_Down", "Lowers morale"), false,
                        Item(T("Guide_morale_Down1", "Taking losses. The more it has lost, the faster morale drops")),
                        Item(T("Guide_morale_Down2", "Being flanked in melee")),
                        Item(T("Guide_morale_Down3", "A friendly squad routing nearby")),
                        Item(T("Guide_morale_Down4", "A Terrifying enemy close by. Stalwart squads ignore this")),
                        Item(T("Guide_morale_Down5", "Losing about three quarters of the army")),
                        Item(T("Guide_morale_Down6", "Burning, snow and some enemy spells"))),
                    Block(GuideBlockType.TermsInline, T("Guide_morale_Start", "Breaking point"), true,
                        Term("Leadership", "LeadershipDesc"),
                        Term(T("Guide_morale_WaverLabel", "Wavering"), T("Guide_morale_Waver", "Below 45% morale the squad wavers. Near zero it routs, and a routed squad cannot be rallied or ordered.")),
                        Term(T("Guide_morale_BalanceLabel", "Balance of Power"), T("Guide_morale_Balance", "The bar at the top of the screen compares what is left of each army. When a side falls to a quarter of its strength, all its squads lose morale faster for the rest of the battle.")))),

                Topic("garrison", chCombat, T("Guide_garrison_Title", "Garrison Battles"),
                    "Towns fight from behind walls. Break a gate to let your army in, or wear the defenders down from outside.",
                    "Break a gate, then take the fight inside.",
                    "Combat.mp4", Cond.GarrisonBattle, 0, new[] { "archers", "charge", "morale" },
                    Block(GuideBlockType.Terms, T("Guide_garrison_Walls", "Behind the walls"), false,
                        Term("GarrisonDefender", "GarrisonDefenderDesc"),
                        Term("DefendersResolve", "DefendersResolveDesc"),
                        Term(T("Guide_garrison_NoWayLabel", "No way over"), T("Guide_garrison_NoWay", "Walls cannot be climbed. The only way in is through a broken gate."))),
                    Block(GuideBlockType.Terms, T("Guide_garrison_Gates", "The gates"), false,
                        Term(T("Guide_garrison_HealthLabel", "Gate health"), T("Guide_garrison_Health", "Village 500, Castle 800, City 1200. A Village has one gate; Castles and Cities have three.")),
                        Term(T("Guide_garrison_ShootsLabel", "They shoot back"), T("Guide_garrison_Shoots", "Each gate fires at the nearest of your units in front of it, out to range 95, and never runs out of arrows.")),
                        Term(T("Guide_garrison_HitLabel", "Breaking one"), T("Guide_garrison_Hit", "Give an attack order on the gate."))),
                    Block(GuideBlockType.ListUp, T("Guide_garrison_Falls", "When a gate falls"), false,
                        Item(T("Guide_garrison_Falls1", "Every defender loses its protection from projectiles, at every gate")),
                        Item(T("Guide_garrison_Falls2", "Your troops can march through the gap")),
                        Item(T("Guide_garrison_Falls3", "Defenders at the other gates fall back behind their army"))),
                    Block(GuideBlockType.Terms, T("Guide_garrison_Win", "Winning"), false,
                        Term(T("Guide_garrison_GoalLabel", "The goal"), T("Guide_garrison_Goal", "Destroy or rout every defender. The gates do not have to fall: archers, artillery and spells can win from outside.")),
                        Term(T("Guide_garrison_RewardLabel", "The reward"), T("Guide_garrison_Reward", "Sack the town for gold and a gear item. Your reserves do not heal after a garrison battle.")))),

                Topic("spells", chSpells, T("Guide_spells_Title", "Spells"),
                    "Your hero brings spells into every battle. Mana is one pool for the whole fight, so pick your moments.",
                    "The spellbook sits on the battle bar.",
                    "Spells.mp4", Cond.SpellsEnabled, 1, new[] { "mages", "morale" },
                    Block(GuideBlockType.Terms, T("Guide_spells_Book", "Your spellbook"), false,
                        Term(T("Guide_spells_SlotsLabel", "Spell slots"), T("Guide_spells_Slots", "Up to three. The first is your hero's signature spell; the others are picked when you set up a run, once unlocked.")),
                        Term(T("Guide_spells_ManaLabel", "Mana"), T("Guide_spells_Mana", "A pool you get at the start of each battle. It never refills during the fight, but it grows with each act.")),
                        Term(T("Guide_spells_CooldownLabel", "Cooldown"), T("Guide_spells_Cooldown", "After a cast, the spell rests for 10 seconds.")),
                        Term(T("Guide_spells_CustomLabel", "Custom battles"), T("Guide_spells_Custom", "Before the battle starts, hover a spell slot to swap in any spell."))),
                    Block(GuideBlockType.Keys, T("Guide_spells_Casting", "Casting"), false,
                        Key("LMB", T("Guide_spells_Click", "Click a spell, then click where it lands. Some spells aim at the ground, others at a squad")),
                        Key("@SpellMenu + 1 - 3", T("Guide_spells_Hotkey", "Hold and press a number to pick a spell")),
                        Key("@SpellMenu + 4 - 0", T("Guide_spells_MageKey", "Hold and press a number to cast with a selected mage")),
                        Key("RMB", T("Guide_spells_Cancel", "Cancel aiming"))),
                    Block(GuideBlockType.Terms, T("Guide_spells_Notes", "Good to know"), true,
                        Term(T("Guide_spells_WhenLabel", "When"), T("Guide_spells_When", "Spells can be cast once the battle starts, not during deployment.")),
                        Term(T("Guide_spells_PlaceLabel", "Placing spells"), T("Guide_spells_Place", "Spells that place something swap the buttons: right click places it and left click cancels.")))),
            };
            return topics;
        }

        static void UiStrings()
        {
            T("Guide_Title", "Battle Guide");
            T("Guide_SearchPlaceholder", "Search topics and keys");
            T("Guide_NoResults", "No topics match.");
            T("Guide_SeeAlso", "See also");
            T("Guide_TopicCount", "{0} of {1}");
            T("Guide_TipCount", "Tip {0} of {1}");
            T("Guide_TipEyebrow", "Battle tip");
            T("Guide_BrowseAll", "Browse the full guide");
            T("Guide_New", "New");
            T("Guide_Key_Drag", "drag");
            T("Guide_Key_Twice", "x2");
            T("Guide_Key_Scroll", "scroll");
            T("Guide_Reason_General", "A quick lesson before the fight.");
            T("Guide_Reason_Mage", "You have a mage in your army.");
            T("Guide_Reason_Ranged", "You have ranged squads in your army.");
            T("Guide_Reason_Shields", "You have shielded squads in your army.");
            T("Guide_Reason_Large", "You have cavalry or monsters in your army.");
            T("Guide_Reason_AntiLarge", "You have Anti Large squads in your army.");
            T("Guide_Reason_Spells", "Your hero can cast spells.");
            T("Guide_Reason_Garrison", "You are attacking a walled town.");
        }
        #endregion
    }
}
