namespace TJ
{
    // The difficulty gates that change what a battle looks like: which enemy tier table is used,
    // whether enemy squads can spawn prestiged, how big a garrison is, and how healthy the
    // starting army is. Everything else difficulty does (shop prices, gold per turn, heals) stays
    // at its own call site because it is economy, not battle.
    //
    // One place for these because two callers have to agree: the game
    // (EngagementPanel, CampaignSaveManager, SaveDataHandler) and the difficulty sim
    // (Tests.Editor), which measures the curve these gates produce. Each method carries the
    // player-facing modifier number from DifficultyData so it can be checked against the card
    // the player reads. DifficultyRulesTests pins every threshold.
    public static class DifficultyRules
    {
        // DifficultyMod 7 "Stronger enemy armies" (Baron) and DifficultyMod 19 "Enemy armies
        // scale in strength faster" (Godking). Each adds one battlesFought step, which moves the
        // army into the next tier band earlier.
        //
        // Mod 7 used to fire from Squire, two tiers before the card that lists it. Fixed
        // 2026-09-14 to match the card.
        public static int BattlesFoughtBonus(TT_Difficulty difficulty)
        {
            int bonus = 0;
            if (difficulty >= TT_Difficulty.Baron) bonus += 1;
            if (difficulty >= TT_Difficulty.Godking) bonus += 1;
            return bonus;
        }

        // DifficultyMod 6 "The Final Battle of each Act is more difficult" (Knight). Selects the
        // KnightDifficulty tier table for horde battles.
        public static bool HarderFinalBattle(TT_Difficulty difficulty) => difficulty >= TT_Difficulty.Knight;

        // DifficultyMod 10 "Enemy armies may contain prestiged units" (Duke).
        public static bool EnemyPrestigeEligible(TT_Difficulty difficulty) => difficulty >= TT_Difficulty.Duke;

        // DifficultyMod 14 "Increases the chance and severity of enemy unit prestige" (Emperor).
        public static bool EnemyPrestigeEnhanced(TT_Difficulty difficulty) => difficulty >= TT_Difficulty.Emperor;

        // DifficultyMod 16 "Settlements have stronger garrisons" (Imperator).
        public static bool StrongerGarrisons(TT_Difficulty difficulty) => difficulty >= TT_Difficulty.Imperator;

        // DifficultyMod 18 "Start each run with a weakened army" (Overlord). Fraction of max
        // health every starting squad is created with.
        public static float StartingHealth(TT_Difficulty difficulty) => difficulty >= TT_Difficulty.Overlord ? 0.75f : 1f;

        // DifficultyMod 20 "Auto-resolve disabled" (Godking). Not a battle input, but the sim
        // marks these rows because the player cannot use auto-resolve there.
        public static bool AutoResolveDisabled(TT_Difficulty difficulty) => difficulty >= TT_Difficulty.Godking;
    }
}
