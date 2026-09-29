using System;
using System.Collections.Generic;
using Memori.SaveData;
using Memori.Steamworks;
using UnityEngine;

namespace TJ.Achievements
{
    /// <summary>Keeps unlocks earned while Steam was unreachable, and awards what the save already proves.</summary>
    public static class AchievementSync
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SteamAchievements.UnlockDeferred -= OnUnlockDeferred;
            SteamAchievements.UnlockDeferred += OnUnlockDeferred;
            SteamAchievements.StatsReceived -= OnStatsReceived;
            SteamAchievements.StatsReceived += OnStatsReceived;
            if (SteamAchievements.StatusAvailable) OnStatsReceived();
        }

        // The Editor never queues, so a dev session without Steam cannot unlock anything later on a real account.
        private static void OnUnlockDeferred(AchievementId id)
        {
            if (Application.isEditor) return;

            PlayerSaveData save = SaveDataHandler.LoadPlayerSaveData();
            save.pendingAchievements ??= new List<string>();
            string name = id.ToString();
            if (save.pendingAchievements.Contains(name)) return;
            save.pendingAchievements.Add(name);
            SaveDataHandler.SavePlayerSaveData(save);
        }

        private static void OnStatsReceived()
        {
            PlayerSaveData save = SaveDataHandler.LoadPlayerSaveData();
            if (save.pendingAchievements != null && save.pendingAchievements.Count > 0)
            {
                List<string> pending = new(save.pendingAchievements);
                save.pendingAchievements.Clear();
                SaveDataHandler.SavePlayerSaveData(save);
                foreach (string name in pending)
                    if (Enum.TryParse(name, out AchievementId id)) SteamAchievements.Unlock(id);
            }
            AwardHeroVictories(save);
        }

        // Wins recorded before the hero victory achievements existed still count.
        private static void AwardHeroVictories(PlayerSaveData save)
        {
            if (save.HeroDifficultiesCompleted == null) return;
            foreach (HeroDifficultiesCompleted hero in save.HeroDifficultiesCompleted)
            {
                if (hero.DifficultiesCompleted == null || hero.DifficultiesCompleted.Count == 0) continue;
                if (AchievementRules.TryGetHeroVictory(hero.HeroID, out AchievementId id)) SteamAchievements.Unlock(id);
            }
        }
    }
}
