using Memori.Scenes;
using TJ.Spells;
using UnityEditor;
using UnityEngine;

namespace TJ.EditorTools
{
    /// <summary>
    /// Tabletop Tavern: every Editor-only dev switch as a checkmark item. All of them persist
    /// in EditorPrefs (see <see cref="DevOverrides"/> and <see cref="SpellTestMode"/>), so none of
    /// them can dirty a scene or ship in a build.
    /// </summary>
    public static class DevMenu
    {
        private const string Root = "Tabletop Tavern/";
        private const string SpellTest = Root + "Spell Test Mode";
        private const string GuardMode = Root + "Enemies Locked To Guard Mode";
        private const string CustomSave = Root + "Boot Loads Custom Battle Save";
        private const string CampaignBattle = Root + "Boot Loads Campaign Battle";
        private const string BootNone = Root + "Editor Boot/Normal (main menu)";
        private const string BootMap = Root + "Editor Boot/Map";
        private const string BootBattle = Root + "Editor Boot/Tavern Battle";

        #region Toggles
        [MenuItem(SpellTest, priority = 1)]
        private static void ToggleSpellTest() => Announce("Spell Test Mode", SpellTestMode.Enabled = !SpellTestMode.Enabled);
        [MenuItem(SpellTest, true)]
        private static bool ValidateSpellTest() => Check(SpellTest, SpellTestMode.Enabled);

        [MenuItem(GuardMode, priority = 2)]
        private static void ToggleGuardMode() => Announce("Enemies Locked To Guard Mode", DevOverrides.LockEnemiesToGuardMode = !DevOverrides.LockEnemiesToGuardMode);
        [MenuItem(GuardMode, true)]
        private static bool ValidateGuardMode() => Check(GuardMode, DevOverrides.LockEnemiesToGuardMode);

        [MenuItem(CustomSave, priority = 20)]
        private static void ToggleCustomSave() => Announce("Boot Loads Custom Battle Save", DevOverrides.LoadCustomBattleSaveData = !DevOverrides.LoadCustomBattleSaveData);
        [MenuItem(CustomSave, true)]
        private static bool ValidateCustomSave() => Check(CustomSave, DevOverrides.LoadCustomBattleSaveData);

        [MenuItem(CampaignBattle, priority = 21)]
        private static void ToggleCampaignBattle() => Announce("Boot Loads Campaign Battle", DevOverrides.LoadCampaignBattle = !DevOverrides.LoadCampaignBattle);
        [MenuItem(CampaignBattle, true)]
        private static bool ValidateCampaignBattle() => Check(CampaignBattle, DevOverrides.LoadCampaignBattle);
        #endregion

        #region Editor boot (one of three)
        // The active scene still wins: SceneHandler.Awake forces Map or TavernBattle when that scene is active.
        [MenuItem(BootNone, priority = 40)]
        private static void BootIntoNone() => SetBoot(SceneHandler.EditorOverrides.None);
        [MenuItem(BootNone, true)]
        private static bool ValidateBootNone() => Check(BootNone, DevOverrides.EditorOverride == SceneHandler.EditorOverrides.None);

        [MenuItem(BootMap, priority = 41)]
        private static void BootIntoMap() => SetBoot(SceneHandler.EditorOverrides.Map);
        [MenuItem(BootMap, true)]
        private static bool ValidateBootMap() => Check(BootMap, DevOverrides.EditorOverride == SceneHandler.EditorOverrides.Map);

        [MenuItem(BootBattle, priority = 42)]
        private static void BootIntoBattle() => SetBoot(SceneHandler.EditorOverrides.TavernBattle);
        [MenuItem(BootBattle, true)]
        private static bool ValidateBootBattle() => Check(BootBattle, DevOverrides.EditorOverride == SceneHandler.EditorOverrides.TavernBattle);

        private static void SetBoot(SceneHandler.EditorOverrides value)
        {
            DevOverrides.EditorOverride = value;
            Debug.Log($"[DevMenu] Editor Boot = {value}. Applies on the next Play.");
        }
        #endregion

        private static bool Check(string path, bool on)
        {
            Menu.SetChecked(path, on);
            return true;
        }

        private static void Announce(string label, bool on)
            => Debug.Log($"[DevMenu] {label} {(on ? "ON" : "OFF")}. Applies on the next Play.");
    }
}
