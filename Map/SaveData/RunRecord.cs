using System;
using System.Collections.Generic;
using TJ;
using TJ.Map;
using TJ.Spells;

namespace Memori.SaveData
{
    public enum RunOutcome { Win, Loss, Abandon }

    /// <summary>
    /// One finished campaign, as it stood the moment it ended. Written once by
    /// <see cref="SaveDataHandler.RecordRunInHistory"/> and never updated, so the main-menu Run
    /// History board can show the warband that won (or fell) exactly as the player last saw it.
    /// </summary>
    [Serializable]
    public class RunRecord
    {
        /// <summary>What a new record holds; records below it predate the detail fields and show them as unknown.</summary>
        public const int CURRENT_DETAIL_VERSION = 1;

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
        /// <summary>Battles survived on the March after the act 3 win; 0 for a run that never marched on.</summary>
        public int marchBattles;
        /// <summary>Gold in the purse when the run ended.</summary>
        public int goldAtEnd;
        public int goldEarned;
        public int enemiesSlain;
        public int renownEarned;
        /// <summary>Seconds of real play, from <see cref="RunClock"/>.</summary>
        public double playTimeSeconds;
        public List<RunSquad> squads = new();
        /// <summary>The old full army copy. Kept only so old records load; moved into <see cref="squads"/> on load.</summary>
        public SquadToLoad[] army = Array.Empty<SquadToLoad>();
        public List<GearID> gear = new();
        public List<Spell> spells = new();

        #region Detail
        /// <summary>0 on records written before the detail fields below existed.</summary>
        public int detailVersion;
        public int startingGold;
        public int goldSpent;
        public int unitsLost;
        public int unitsPrestiged;
        public int unitsRecruited;
        public int gearFound;
        public int shopPurchases;
        public int goldWagered;
        public int campfireRests;
        public int campfireTrainings;
        public int consumablesUsed;
        public int healingItemsUsed;
        public int townsSacked;
        public int villagesSacked;
        public int castlesSacked;
        public int citiesSacked;
        public List<SpellCastStored> spellsCast = new();
        public List<RunSquad> fallen = new();
        public List<RunAct> acts = new();
        /// <summary>Emptied once the record is older than <see cref="SaveDataHandler.MAX_BATTLE_LOGS"/> runs.</summary>
        public List<RunBattle> battles = new();
        #endregion

        public DateTime EndedAtUtc => new(endedAtUtcTicks, DateTimeKind.Utc);
        public bool HasDetail => detailVersion >= 1;
    }

    /// <summary>One army slot of a recorded run. Slot 10 and up was the reserve; -1 is a squad that fell.</summary>
    [Serializable]
    public struct RunSquad
    {
        public int slot;
        public UnitName unit;
        public int prestige;
        public UnitAttribute trait;
        public int unitCount;
        public int hitPointsPerUnit;
        public int health;
        public int maxHealth;
        /// <summary>Kills and units lost over the run. 0 on records from before they were kept.</summary>
        public int kills;
        public int lost;

        public static RunSquad From(SquadToLoad squad, int slot, List<SquadKillsStored> kills = null, List<SquadLossesStored> losses = null)
        {
            return new RunSquad
            {
                slot = slot,
                unit = squad.UnitName,
                prestige = squad.UnitPrestige,
                trait = squad.PrestigeTrait,
                unitCount = squad.maxUnitCount,
                hitPointsPerUnit = squad.HitPointsPerUnit,
                health = squad.SquadCurrentHealth,
                maxHealth = squad.SquadMaxHealth,
                kills = KillsOf(squad.UniqueID, kills),
                lost = LossesOf(squad.UniqueID, losses),
            };
        }

        /// <summary>The filled slots of an army in slot order. Empty slots hold nothing worth keeping.</summary>
        public static List<RunSquad> FromArmy(SquadToLoad[] army, List<SquadKillsStored> kills = null, List<SquadLossesStored> losses = null)
        {
            var squads = new List<RunSquad>();
            if (army == null) return squads;
            for (int i = 0; i < army.Length; i++)
            {
                if (!IsFilled(army[i])) continue;
                squads.Add(From(army[i], i, kills, losses));
            }
            return squads;
        }

        public static bool IsFilled(SquadToLoad squad) => !squad.isEmptySquad && squad.UnitIndex != -1 && squad.maxUnitCount > 0;

        private static int KillsOf(string id, List<SquadKillsStored> kills)
        {
            if (kills == null || string.IsNullOrEmpty(id)) return 0;
            foreach (SquadKillsStored entry in kills)
                if (entry.SquadGUID == id) return entry.Kills;
            return 0;
        }

        private static int LossesOf(string id, List<SquadLossesStored> losses)
        {
            if (losses == null || string.IsNullOrEmpty(id)) return 0;
            foreach (SquadLossesStored entry in losses)
                if (entry.SquadGUID == id) return entry.Losses;
            return 0;
        }
    }

    /// <summary>The army as one act ended.</summary>
    [Serializable]
    public struct RunAct
    {
        public int act;
        public int gold;
        public List<RunSquad> squads;
    }

    /// <summary>One resolved battle of a run.</summary>
    [Serializable]
    public struct RunBattle
    {
        public int act;
        public NodeType node;
        /// <summary>The <see cref="TownSize"/> of a sieged town, or -1 for a field battle.</summary>
        public int town;
        public Race race;
        /// <summary>The enemy warlord's hero id, or 0 for none.</summary>
        public int warlord;
        public Weather weather;
        public bool fought;
        public bool won;
        public int kills;
        public int lost;
        /// <summary>Player squads wiped out in this battle.</summary>
        public int fallen;
        /// <summary>Spoils gold claimed after the battle.</summary>
        public int gold;
    }

    /// <summary>Totals over every recorded run, kept past the Run History cap.</summary>
    [Serializable]
    public class LifetimeStats
    {
        /// <summary>Set once the totals have taken in the runs that were already in Run History.</summary>
        public bool seeded;
        public int runs;
        public int wins;
        public int losses;
        public int abandons;
        public double playTimeSeconds;
        public int battlesFought;
        public int goldEarned;
        public int goldSpent;
        public int enemiesSlain;
        public int unitsLost;
        public int unitsPrestiged;
        public int unitsRecruited;
        public int townsSacked;
        public int villagesSacked;
        public int castlesSacked;
        public int citiesSacked;
        public List<HeroTally> heroes = new();

        public void Add(RunRecord run)
        {
            runs++;
            if (run.outcome == RunOutcome.Win) wins++;
            else if (run.outcome == RunOutcome.Loss) losses++;
            else abandons++;
            playTimeSeconds += run.playTimeSeconds;
            battlesFought += run.battlesFought;
            goldEarned += run.goldEarned;
            goldSpent += run.goldSpent;
            enemiesSlain += run.enemiesSlain;
            unitsLost += run.unitsLost;
            unitsPrestiged += run.unitsPrestiged;
            unitsRecruited += run.unitsRecruited;
            townsSacked += run.townsSacked;
            villagesSacked += run.villagesSacked;
            castlesSacked += run.castlesSacked;
            citiesSacked += run.citiesSacked;

            heroes ??= new List<HeroTally>();
            int index = heroes.FindIndex(tally => tally.heroID == run.heroID);
            HeroTally hero = index >= 0 ? heroes[index] : new HeroTally { heroID = run.heroID };
            hero.runs++;
            if (run.outcome == RunOutcome.Win) hero.wins++;
            if (index >= 0) heroes[index] = hero;
            else heroes.Add(hero);
        }
    }

    [Serializable]
    public struct HeroTally
    {
        public int heroID;
        public int runs;
        public int wins;
    }
}
