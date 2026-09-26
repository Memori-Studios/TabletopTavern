using System.Collections.Generic;
using Memori.SaveData;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Which guide topics the player has read. Stored in PlayerSaveData.BattlefieldInfoSectionsViewed, the list the old tip popup used.
    /// </summary>
    public static class BattleGuideProgress
    {
        // Written once so a save from before the guide rewrite gets its new topics marked from the old ones.
        const string MigrationMarker = "guide2";
        const string GarrisonId = "garrison";
        public const string LegacyGarrisonPref = "GarrisonTutorialSeen";

        // New topic id -> old ids that covered the same ground.
        static readonly Dictionary<string, string[]> LegacyIds = new()
        {
            { "traits", new[] { "stats" } },
            { "camera", new[] { "advanced", "controls" } },
        };

        /// <summary>Read only: never writes the save, so previews and the table of contents cannot change progress.</summary>
        public static bool IsSeen(string id)
        {
            List<string> seen = SaveDataHandler.LoadPlayerSaveData().BattlefieldInfoSectionsViewed;
            if (seen.Contains(id)) return true;
            if (id == GarrisonId && PlayerPrefs.GetInt(LegacyGarrisonPref, 0) == 1) return true;
            if (seen.Contains(MigrationMarker) || !LegacyIds.TryGetValue(id, out string[] legacy)) return false;
            foreach (string old in legacy)
                if (seen.Contains(old)) return true;
            return false;
        }

        public static void MarkSeen(string id)
        {
            if (!Application.isPlaying) return;
            PlayerSaveData save = SaveDataHandler.LoadPlayerSaveData();
            bool changed = Migrate(save);
            if (!save.BattlefieldInfoSectionsViewed.Contains(id))
            {
                save.BattlefieldInfoSectionsViewed.Add(id);
                changed = true;
            }
            if (changed) SaveDataHandler.SavePlayerSaveData(save);
        }

        /// <summary>Returns true when it changed the save.</summary>
        public static bool Migrate(PlayerSaveData save)
        {
            List<string> seen = save.BattlefieldInfoSectionsViewed;
            bool changed = false;
            // The old garrison popup kept its own flag in PlayerPrefs; a player who saw it has read this topic.
            if (!seen.Contains(GarrisonId) && PlayerPrefs.GetInt(LegacyGarrisonPref, 0) == 1)
            {
                seen.Add(GarrisonId);
                changed = true;
            }
            if (seen.Contains(MigrationMarker)) return changed;
            foreach (KeyValuePair<string, string[]> pair in LegacyIds)
            {
                if (seen.Contains(pair.Key)) continue;
                foreach (string legacy in pair.Value)
                {
                    if (!seen.Contains(legacy)) continue;
                    seen.Add(pair.Key);
                    break;
                }
            }
            seen.Add(MigrationMarker);
            return true;
        }

        /// <summary>Spell and mage topics exist only in builds with the spell feature.</summary>
        public static bool IsAvailable(GuideTopic topic)
        {
#if SPELLS
            return true;
#else
            return topic.condition != BattlefieldTutorial.BattlefieldInfoCondition.SpellsEnabled
                && topic.condition != BattlefieldTutorial.BattlefieldInfoCondition.PlayerArmyContainsMage;
#endif
        }

        public static string ReasonKey(BattlefieldTutorial.BattlefieldInfoCondition condition) => condition switch
        {
            BattlefieldTutorial.BattlefieldInfoCondition.PlayerArmyContainsMage => "Guide_Reason_Mage",
            BattlefieldTutorial.BattlefieldInfoCondition.SpellsEnabled => "Guide_Reason_Spells",
            BattlefieldTutorial.BattlefieldInfoCondition.PlayerArmyContainsRanged => "Guide_Reason_Ranged",
            BattlefieldTutorial.BattlefieldInfoCondition.PlayerArmyContainsShields => "Guide_Reason_Shields",
            BattlefieldTutorial.BattlefieldInfoCondition.PlayerArmyContainsLarge => "Guide_Reason_Large",
            BattlefieldTutorial.BattlefieldInfoCondition.PlayerArmyContainsAntiLarge => "Guide_Reason_AntiLarge",
            BattlefieldTutorial.BattlefieldInfoCondition.GarrisonBattle => "Guide_Reason_Garrison",
            _ => "Guide_Reason_General",
        };
    }
}
