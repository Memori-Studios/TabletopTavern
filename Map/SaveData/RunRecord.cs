using System;
using System.Collections.Generic;
using TJ;
using TJ.Spells;

namespace Memori.SaveData
{
    public enum RunOutcome { Win, Loss, Abandon }

    /// <summary>
    /// One finished campaign, as it stood the moment it ended. Written once by
    /// <see cref="SaveDataHandler.RecordRunInHistory"/> and never updated, so the main-menu Run
    /// History board can show the warband that won (or fell) exactly as the player last saw it.
    ///
    /// The army is the whole <c>playerArmy</c> array rather than the living squads, because slot
    /// order is what tells the board which squads were in reserve (index 10 and up), and
    /// <see cref="SquadToLoad.isEmptySquad"/> already marks the gaps.
    /// </summary>
    [Serializable]
    public class RunRecord
    {
        public string runUUID;
        public int heroID;
        public TT_Difficulty difficulty;
        public RunOutcome outcome;
        /// <summary>UTC ticks. JsonUtility cannot serialize a DateTime, so it travels as a long.</summary>
        public long endedAtUtcTicks;
        /// <summary>The act the run ended in (bookNumber). On a win it is the act that was finished.</summary>
        public int actReached;
        public int chaptersCompleted;
        public int battlesFought;
        /// <summary>Gold in the purse when the run ended.</summary>
        public int goldAtEnd;
        public int goldEarned;
        public int enemiesSlain;
        public int renownEarned;
        /// <summary>Seconds of real play, from <see cref="RunClock"/>.</summary>
        public double playTimeSeconds;
        public SquadToLoad[] army = Array.Empty<SquadToLoad>();
        public List<GearID> gear = new();
        public List<Spell> spells = new();

        public DateTime EndedAtUtc => new(endedAtUtcTicks, DateTimeKind.Utc);
    }
}
