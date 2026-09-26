using System.Collections.Generic;

namespace TabletopTavern.Analytics
{
    // Plain data filled at each hook, so GameEventTracker never reaches into scene objects.

    /// <summary>What the run setup screen knew when the run started. A quick restart knows only its source.</summary>
    public class AnalyticsRunSetup
    {
        public string Source = "unknown";
        public bool FromMenu;
        public bool ArmyLocked;
        public bool ArmyCustomized;
        public List<string> ArmyOptions = new List<string>();
        public int TreasuryBase;
        public int TreasuryRenownBonus;
        public int ArmySpend;
        public int GearSpend;
    }

    /// <summary>One squad's battle, start to end.</summary>
    public class AnalyticsSquadResult
    {
        public string Unit;
        // Army slot, player squads only.
        public int Slot = -1;
        public int Prestige;
        public string Trait;
        public int UnitsStart;
        public int UnitsEnd;
        public int Kills;
        // Stand, Withdrew, Broke or Dead.
        public string Status;
    }

    /// <summary>A finished campaign battle, fought or auto-resolved.</summary>
    public class AnalyticsBattleReport
    {
        // manual or auto
        public string Mode;
        // Win, Loss, Concede or Abandon
        public string Result;
        public bool Garrison;
        public string EnemyRace;
        // Deployment layout, fought battles only.
        public string Layout;
        // -1 when there was no mana pool (auto-resolve, or a build without SPELLS).
        public int ManaMax = -1;
        public int ManaLeft = -1;
        public int SpellKills;
        public bool ArmyLossTriggered;
        public bool PauseUsed;
        public Dictionary<string, int> SpellCasts = new Dictionary<string, int>();
        public List<AnalyticsSquadResult> Player = new List<AnalyticsSquadResult>();
        public List<AnalyticsSquadResult> Enemy = new List<AnalyticsSquadResult>();
    }

    /// <summary>A resolved map node and the army as it left it.</summary>
    public class AnalyticsNodeReport
    {
        public int Layer;
        public int NodeIndex;
        public string NodeType;
        public int GoldAfter;
        // Interest and the King gold tax, paid after the node. 0 on an act's last layer.
        public int Interest;
        public int SquadsLost;
        public int ArmySize;
        public int DeployedSquads;
        public int ArmyValue;
        public int UnitsAlive;
        public int UnitsMax;
        public int HealthNow;
        public int HealthMax;
    }
}
