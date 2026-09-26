using Memori.SaveData;
using Memori.Scenes;

namespace TJ
{
    // Enemy warlords: in an endless act's final battle, the act's enemy general leads his army with
    // his own hero bonus rules, the same ones a player gets from that hero. Built but not live yet:
    // Enabled is the one switch, and Tabletop Tavern > Force Enemy Warlords turns it on in the Editor.
    public static class EnemyWarlord
    {
        public static readonly bool Enabled = false;

        public static bool IsActive => Enabled || DevOverrides.ForceEnemyWarlords;

        // The hero leading an enemy army built for this battle, or 0 for none. Decided where the army
        // is built, because auto-resolve predicts before the map records which node was picked.
        public static int ResolveHeroID(CampaignSaveData run, bool isFinalBattle, bool isGarrison)
        {
            if (!IsActive || run == null || !isFinalBattle || isGarrison) return 0;
            if (TabletopTavernConstants.EndlessActs(run.bookNumber) <= 0) return 0;
            return TabletopTavernData.Instance.GetEnemyHeroForCampaign(run.heroID, run.bookNumber, run.seed).HeroID;
        }
    }
}
