using Memori.SaveData;
using Memori.Scenes;

namespace TJ
{
    // Enemy warlords: every fifth battle of the March, a hero of the host's faction leads it with his own hero
    // bonus rules, the same ones a player gets from that hero. Enabled is the one switch, and
    // Tabletop Tavern > Force Enemy Warlords turns it on in the Editor when it is off.
    public static class EnemyWarlord
    {
        public static readonly bool Enabled = true;

        public static bool IsActive => Enabled || DevOverrides.ForceEnemyWarlords;

        // Keeps the hero draw clear of the map's own draws on the same seed.
        private const int WARLORD_SEED_PER_NODE = 8191;

        // The hero leading an enemy army built for this battle, or 0 for none. Decided where the army
        // is built, because auto-resolve predicts before the map records which node was picked.
        public static int ResolveHeroID(CampaignSaveData run, bool isFinalBattle, bool isGarrison, Race hostRace, int nodeIndex)
        {
            if (!IsActive || run == null || !isFinalBattle || isGarrison) return 0;
            if (!run.InMarch) return 0;
            return TabletopTavernData.Instance.GetHeroLeadingRace(hostRace, run.seed + run.bookNumber * 13 + nodeIndex * WARLORD_SEED_PER_NODE).HeroID;
        }
    }
}
