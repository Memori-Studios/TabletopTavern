using Memori.SaveData;
using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace TJ.Engagement
{
    [Serializable] public struct AutoResolveSquad
    {
        public SquadStats squadStats;
        public int UnitsAlive;
        public int maxUnits;
        public int SquadIndex;
        public int TargetIndex;
        public string UniqueID;
        public int UnitsSlain;
        public int finalHealth;
        public int healthPerKill;
        public float armorMitigation;
        public int ChargeBonus;
        public float shieldBlockChance;
        // Scales every point of damage this squad RECEIVES. 1 = normal. Raised by a damage-amplifying
        // mark (Blood Scent), lowered by a defensive brace (Runeward). A multiplier rather than an
        // armour tweak because a mark applies to unarmoured squads too, where armourMitigation is 0
        // and there is nothing to scale.
        public float damageTakenMultiplier;
        public Race Race;

        // Simulation state, owned by AutoResolveSimulation. Set up on the first tick.
        [NonSerialized] public bool SimReady;
        [NonSerialized] public float X, Z;              // field position, player side is -Z
        [NonSerialized] public int ContactIndex;        // SquadIndex of the squad this one is locked in melee with, -1 if none
        [NonSerialized] public float Morale;
        [NonSerialized] public bool Broken;
        [NonSerialized] public float Ammo;
        [NonSerialized] public bool FireAtWill;
        [NonSerialized] public bool ArmyLosses;
        [NonSerialized] public bool DefensiveStance;
        [NonSerialized] public int HordeStacks;         // Crashing Horde stacks this second, player side only
        [NonSerialized] public float ChargeWindow;      // seconds of charge bonus left
        [NonSerialized] public float MoveSeconds;       // seconds spent closing on the current target
        [NonSerialized] public float EngagedSeconds;
        [NonSerialized] public float FlankedTimer;
        [NonSerialized] public float OnFireTimer;
        [NonSerialized] public float RetreatingAlliesTimer;
        [NonSerialized] public float HitCarry, ShotCarry;
        [NonSerialized] public int ThrownModels;
        [NonSerialized] public int FormationWidth;
        [NonSerialized] public int[] UnitHealth;        // per model, living models first
        [NonSerialized] public float[] RecentLoss, RecentDealt;   // last five seconds, ring
        [NonSerialized] public int RingIndex;
        [NonSerialized] public float TickLoss, TickDealt;
    }
    // The hero inputs the live battle reads from CampaignSaveDataHolder, as plain data so the
    // difficulty sim can supply them without a campaign. default(...) means no hero.
    public readonly struct AutoResolveHeroContext
    {
        public readonly bool HasHero;
        public readonly int HeroID;
        public readonly Race HeroRace;
        public readonly Race EnemyRace;
        // The live Bushido Discipline gate: every player squad is Sakura Dynasty.
        public readonly bool OnlySakuraUnits;

        public AutoResolveHeroContext(int heroID, Race heroRace, Race enemyRace, bool onlySakuraUnits)
        {
            HasHero = heroID != -1;
            HeroID = heroID;
            HeroRace = heroRace;
            EnemyRace = enemyRace;
            OnlySakuraUnits = onlySakuraUnits;
        }

        public static AutoResolveHeroContext None => default;

        // Same inputs BattleCleanUpManager derives for the live battle, from the same arrays.
        public static AutoResolveHeroContext From(int heroID, SquadToLoad[] playerArmy, SquadToLoad[] enemyArmy)
        {
            if (heroID == -1) return None;
            Race enemyRace = enemyArmy != null && enemyArmy.Length > 0
                ? TabletopTavernData.Instance.GetRaceFromUnitName(enemyArmy[0].UnitName)
                : Race.Special;
            bool onlySakura = playerArmy != null && playerArmy.Length > 0;
            if (onlySakura)
                foreach (SquadToLoad squad in playerArmy)
                    if (TabletopTavernData.Instance.GetRaceFromUnitName(squad.UnitName) != Race.SakuraDynasty) { onlySakura = false; break; }
            return new AutoResolveHeroContext(heroID, HeroData.GetRaceFromHero(heroID), enemyRace, onlySakura);
        }
    }
    public class AutoResolveBattleManager : MonoBehaviour
    {
        // [SerializeField] private float turnTickRate = 1f, meleeMultiplier = 1f, rangedMultiplier = 1f;
        [Header("Testing")]
        [SerializeField] private ArmySaveData testPlayerArmySaveData;
        [SerializeField] private ArmySaveData testEnemyArmySaveData, playerArmyForEnemyScoringSaveData;
        [SerializeField] private SquadToLoad[] playerArmy, enemyArmy;
        // internal, not private: the EditMode tests assert on the outcome of a simulation run.
        internal bool playerArmyIsDefeated, enemyArmyIsDefeated;
        // internal so tests can seed hand-built armies instead of reflecting into private fields.
        [SerializeField] internal AutoResolveSquad[] playerAutoResolveStats, enemyAutoResolveStats;
        public SquadToLoad[] PredictedPlayerArmy => playerArmy;
        public AutoResolveSquad[] PlayerAutoResolveStats => playerAutoResolveStats;
        public AutoResolveSquad[] EnemyAutoResolveStats => enemyAutoResolveStats;
        internal List<int3> unitsSlainData = new();
        // Enemy damage in a garrison assault. Stands in for the walls and gates the model does not
        // simulate. The per-act x1.25 / x1.5 enemy bonus was removed 2026-09-21: it compensated for
        // the old round-based model being too kind, and the calibrated model no longer needs it.
        private const float GARRISON_AUTORESOLVE_BONUS = 1.4f;
        // Hard ceiling on simulated seconds. A real battle resolves in a few hundred. This exists because both target pools in AssignTargets are positive whitelists,
        // so a UnitType that falls out of both is untargetable, neither side is ever defeated,
        // and the loop below spins forever. That has already happened once, with mages.
        // FindAnySquadTarget is the real fix; this is the backstop that turns a frozen campaign
        // into a log line.
        private const int MAX_AUTORESOLVE_ROUNDS = 10000;
        private bool _isGarrisonBattle;
        // Only Load() sets the garrison flag, and Load() needs a campaign. The difficulty sim
        // sets it directly.
        internal bool IsGarrisonBattle { set => _isGarrisonBattle = value; }
        // One-shot per battle. Reset wherever the armies are (re)built, not in RunSimulationLoop,
        // which is called once per round.
        internal bool _mageAlphaStrikeApplied;

        private struct CachedBattleResult
        {
            public SquadToLoad[] playerArmy;
            public SquadToLoad[] enemyArmy;
            public AutoResolveSquad[] playerAutoResolveStats;
            public AutoResolveSquad[] enemyAutoResolveStats;
            public bool playerArmyIsDefeated;
            public bool enemyArmyIsDefeated;
        }
        private readonly Dictionary<string, CachedBattleResult> _resultCache = new();
        private string _currentBattleKey = string.Empty;

        // Resolved from the hierarchy, never from CampaignManager.Instance. This component is a child
        // of the CampaignManager GameObject, and the singleton getter fabricates an unconfigured
        // manager on a miss - whose ConsumableManager is null - then caches it for the session.
        private CampaignManager _campaignManager;

        private void Start()
        {
            _campaignManager = GetComponentInParent<CampaignManager>();
            if (_campaignManager == null) _campaignManager = CampaignManager.InstanceIfExists;

            // No campaign at all is an ORDINARY state, not a fault: a custom battle, the editor
            // prediction button and the tests all run without one, and there is no auto-resolve
            // prediction for a consumable to invalidate. Staying silent here matters - this was a
            // LogError, which meant every custom battle logged an error before anything had gone
            // wrong, and an "a battle logs no errors" check is worthless once it is red by default.
            if (_campaignManager == null) return;

            // A campaign IS present but its ConsumableManager is not wired. That is a real defect, so
            // it is worth saying - as a warning rather than an error, because the only consequence is
            // a stale prediction until the next re-simulate.
            if (_campaignManager.ConsumableManager == null)
            {
                Debug.LogWarning("[AutoResolve] CampaignManager has no ConsumableManager, so the prediction will not re-simulate when a consumable is used.");
                return;
            }
            _campaignManager.ConsumableManager.OnConsumableUsed += OnConsumableUsed;
        }
        private void OnDestroy()
        {
            if (_campaignManager != null && _campaignManager.ConsumableManager != null)
                _campaignManager.ConsumableManager.OnConsumableUsed -= OnConsumableUsed;
        }
        private void OnConsumableUsed()
        {
            Debug.Log("[AutoResolve] Consumable used — clearing cache and re-running simulation.");
            _resultCache.Clear();
            Load(_isGarrisonBattle);
        }

        private string GetPlayerArmyKey()
        {
            if (playerArmy == null) return string.Empty;
            var ids = new string[playerArmy.Length];
            for (int i = 0; i < playerArmy.Length; i++)
                ids[i] = playerArmy[i].UniqueID;
            Array.Sort(ids, StringComparer.Ordinal);
            return string.Join(",", ids);
        }

        private string GetEnemyArmyKey()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var squad in enemyArmy)
                sb.Append(squad.UniqueID).Append(',');
            return sb.ToString();
        }

        public void Load(bool isGarrisonBattle = false)
        {
            _isGarrisonBattle = isGarrisonBattle;
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.battleCompleted == true)
            {
                Debug.Log("Battle already completed, cannot load armies again.");
                return;
            }

            //remove any unit with a unit index of -1
            List<SquadToLoad> playerArmyList = new();
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy == null) return;
            for (int i = 0; i < CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy.Length && i < 10; i++)
            {
                if (CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i].UnitIndex != -1)
                {
                    // Debug.Log($"Adding {CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i].UnitName} to player army");
                    playerArmyList.Add(CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i]);
                }
            }
            // Debug.Log($"playerArmyList count: {playerArmyList.Count}");
            playerArmy = playerArmyList.ToArray();
            // Copy, never alias. RecordResults writes the simulated outcome back into enemyArmy, and a
            // prediction must not leave those numbers in the campaign save - a predicted win zeroed every
            // enemy squad's health, which then rendered as 0 HP on the pre-battle enemy cards.
            SquadToLoad[] savedEnemyArmy = CampaignManager.Instance.CampaignSaveManager.SaveData.enemyArmy;
            enemyArmy = savedEnemyArmy == null ? null : (SquadToLoad[])savedEnemyArmy.Clone();

            if (enemyArmy == null || enemyArmy.Length == 0 || enemyArmy[0].SquadMaxHealth == 0)
            {
                // Debug.LogError($"No enemy army found this will fire when conscripting enemies after a battle or reordeing units outside of battle");
                return;
            }

            //set enemy armies to be max unit count, need to keep this for when changing order of units and having to redo auto resolve
            for (int i = 0; i < enemyArmy.Length; i++)
            {
                enemyArmy[i].SquadCurrentHealth = enemyArmy[i].SquadMaxHealth;
            }

            string battleKey = GetEnemyArmyKey();
            if (battleKey != _currentBattleKey)
            {
                _resultCache.Clear();
                _currentBattleKey = battleKey;
                // Debug.Log("[AutoResolve] New battle detected — cache cleared.");
            }

            string playerKey = GetPlayerArmyKey();
            if (_resultCache.TryGetValue(playerKey, out CachedBattleResult cached))
            {
                playerArmy = cached.playerArmy;
                enemyArmy = cached.enemyArmy;
                playerAutoResolveStats = cached.playerAutoResolveStats;
                enemyAutoResolveStats = cached.enemyAutoResolveStats;
                playerArmyIsDefeated = cached.playerArmyIsDefeated;
                enemyArmyIsDefeated = cached.enemyArmyIsDefeated;
                // Debug.Log($"[AutoResolve] Cache HIT — skipping simulation. Player defeated: {playerArmyIsDefeated}, Enemy defeated: {enemyArmyIsDefeated}");
                if (Application.isPlaying)
                    CampaignManager.Instance.MapSceneUIManager.EngagementPanel.AlertOfBattleResults(enemyArmyIsDefeated);
                return;
            }

            // Debug.Log($"[AutoResolve] Cache MISS — running simulation. Cache size: {_resultCache.Count}");
            PredictAutoResolve();
        }
    internal static AutoResolveSquad GenerateAutoResolveSquadStats(SquadToLoad _squadToLoad, int _squadIndex, Team _team, CampaignSaveManager campaignSaveManager = null, bool allowGearModifiers = true, AutoResolveHeroContext hero = default)
    {
        bool useCampaign = allowGearModifiers && campaignSaveManager != null;
        return GenerateAutoResolveSquadStats(_squadToLoad, _squadIndex, _team,
            useCampaign ? (Func<GearID, bool>)campaignSaveManager.CheckForGear : null,
            useCampaign && campaignSaveManager.SaveData.battleFieldPreset.weather == Weather.Rain,
            hero);
    }
    // Gear, weather and hero as plain inputs. The game passes them from the campaign save through
    // the overload above; the difficulty sim (Tests.Editor) passes them as data, because it has no
    // campaign. A null hasGear means no gear at all; a default hero means no hero.
    internal static AutoResolveSquad GenerateAutoResolveSquadStats(SquadToLoad _squadToLoad, int _squadIndex, Team _team, Func<GearID, bool> hasGear, bool rain, AutoResolveHeroContext hero = default)
    {
        SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(_squadToLoad.UnitName);
        UnitType unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(_squadToLoad.UnitName);
        if (_squadToLoad.PrestigeTrait != UnitAttribute.None) {
            TabletopTavernConstants.SetAttribute(ref squadStats.SquadAttributes, _squadToLoad.PrestigeTrait);
        }

        int meleeAttack = squadStats.MeleeAttack;
        int meleeDefense = squadStats.MeleeDefense;
        float accuracy = squadStats.attackAccuracy/100f;
        float range = squadStats.BaseRange;
        int maxHitPoints = _squadToLoad.SquadCurrentHealth;
        float accuracyMultiplier = 1;
        int WeaponStrength = squadStats.WeaponStrength;
        int missileStrength = squadStats.MissileStrength;
        int ChargeBonus = squadStats.ChargeBonus;
        float shieldBlockChance = 0;

        if (rain) accuracyMultiplier *= WeatherRuleData.Rain.AutoResolveAccuracyModifier;

        // Hero rules, player squads only, ahead of gear as in UnitSetUpSystem. Each stat lands on
        // the same local the rest of this method reads, so a rule reaches the simulation the way
        // it reaches the live battle. Accuracy rules are in percent points; this method holds a
        // fraction.
        if (hero.HasHero && _team == Team.Player)
        {
            float HeroBonus(UnitStat stat, float current)
            {
                float total = HeroBonusRuleEvaluator.SumHeroStatBonus(stat, _squadToLoad.UnitName, hero.HeroID, squadStats, hero.EnemyRace, current);
                if (hero.OnlySakuraUnits)
                    total += HeroBonusRuleEvaluator.SumFactionStatBonus(stat, hero.HeroRace, current);
                return total;
            }

            meleeAttack += (int)HeroBonus(UnitStat.MeleeAttack, meleeAttack);
            meleeDefense += (int)HeroBonus(UnitStat.MeleeDefense, meleeDefense);
            accuracy += HeroBonus(UnitStat.Accuracy, accuracy * 100f) / 100f;
            WeaponStrength += (int)HeroBonus(UnitStat.WeaponStrength, WeaponStrength);
            squadStats.Armor += (int)HeroBonus(UnitStat.Armor, squadStats.Armor);
            range += HeroBonus(UnitStat.Range, range);
            missileStrength += (int)HeroBonus(UnitStat.MissileStrength, missileStrength);
            ChargeBonus += (int)HeroBonus(UnitStat.ChargeBonus, ChargeBonus);
            squadStats.Leadership += HeroBonus(UnitStat.Leadership, squadStats.Leadership);
            squadStats.Ammunition += (int)HeroBonus(UnitStat.Ammunition, squadStats.Ammunition);
            // The simulation reads these three off the stat copy; ChargeCount, ExplosionRange and
            // ExplosionForce have no model here and are left alone.
            squadStats.attackCooldown = Mathf.Max(0.1f, squadStats.attackCooldown + HeroBonus(UnitStat.AttackCooldown, squadStats.attackCooldown));
            squadStats.rateOfFire = Mathf.Max(0.1f, squadStats.rateOfFire + HeroBonus(UnitStat.RateOfFire, squadStats.rateOfFire));
            squadStats.ExplosionDamage = Mathf.Max(0, squadStats.ExplosionDamage + (int)HeroBonus(UnitStat.ExplosionDamage, squadStats.ExplosionDamage));
            HeroBonusRuleEvaluator.ApplyHeroAttributes(ref squadStats.SquadAttributes, _squadToLoad.UnitName, hero.HeroID, squadStats, hero.EnemyRace);
        }

        if(hasGear != null)
        {
            if(_team == Team.Player)
            {
                if(hasGear(GearID.ArmingSwords) && TabletopTavernConstants.FightsInMelee(unitType))
                    meleeAttack += GearData.GetGear(GearID.ArmingSwords).GearModifierValue;
                if(hasGear(GearID.BucklerShields) && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields))
                    meleeDefense += GearData.GetGear(GearID.BucklerShields).GearModifierValue;
                if(hasGear(GearID.DiamondTippedArrows) && unitType == UnitType.Ranged) 
                    squadStats.SquadAttributes.ArmorPiercing = true;
                if(hasGear(GearID.HeavyWeapons) && squadStats.RarityTier == UnitRarity.Rare) 
                    squadStats.SquadAttributes.ArmorPiercing = true; 
                if(hasGear(GearID.Turkey) && unitType == UnitType.Ranged) 
                    squadStats.SquadAttributes.AntiLarge = true;
                if(hasGear(GearID.Longbows) && unitType == UnitType.Ranged) 
                    range += GearData.GetGear(GearID.Longbows).GearModifierValue;
                if(hasGear(GearID.Glaives) && squadStats.SquadAttributes.AntiLarge) 
                    WeaponStrength += GearData.GetGear(GearID.Glaives).GearModifierValue;
                if(hasGear(GearID.TexanBBQ) && TabletopTavernConstants.FightsInMelee(unitType))
                    WeaponStrength += GearData.GetGear(GearID.TexanBBQ).GearModifierValue;
                if(hasGear(GearID.BallisticCharts)) 
                    accuracy += (GearData.GetGear(GearID.BallisticCharts).GearModifierValue/100f);
                if(hasGear(GearID.ConscriptionOrders) && squadStats.RarityTier == UnitRarity.Common) {
                    meleeAttack += GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue;
                    meleeDefense += GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue;
                }
                if(hasGear(GearID.JoustingLances) && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields)) 
                    meleeDefense += GearData.GetGear(GearID.JoustingLances).GearModifierValue;
                if(hasGear(GearID.GnomishArmorers) && squadStats.RarityTier == UnitRarity.Rare)
                    meleeDefense += GearData.GetGear(GearID.GnomishArmorers).GearModifierValue;
                if(hasGear(GearID.WellHonedAxes) && squadStats.SquadAttributes.ArmorPiercing) //must apply after diamond tipped arrows
                    meleeAttack += GearData.GetGear(GearID.WellHonedAxes).GearModifierValue;
                if(hasGear(GearID.RavensEye) && squadStats.RarityTier != UnitRarity.Common && unitType == UnitType.Ranged) 
                    accuracy += (GearData.GetGear(GearID.RavensEye).GearModifierValue/100f);
                if(hasGear(GearID.RingoftheElvenKing) && squadStats.unitType == UnitType.Ranged)
                    missileStrength += GearData.GetGear(GearID.RingoftheElvenKing).GearModifierValue;
            }
        }

        // The block chances UnitSetUpSystem gives every shielded unit, either team. Tower Shields is
        // commented out in the live setup, so it changes nothing here either.
        if (squadStats.SquadAttributes.HeavyShields) shieldBlockChance = AutoResolveSimulation.Model.HeavyShieldBlock;
        else if (squadStats.SquadAttributes.StandardShields) shieldBlockChance = AutoResolveSimulation.Model.StandardShieldBlock;

        int totalHealth = _squadToLoad.SquadCurrentHealth;
        float armorMitigation = (float)squadStats.Armor/(float)(squadStats.Armor + 100f);

        if (TabletopTavernConstants.FightsInMelee(squadStats.unitType))
        {
            meleeAttack += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
            meleeDefense += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
            squadStats.Leadership += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
        }
        else if (squadStats.unitType == UnitType.Ranged || squadStats.unitType == UnitType.Artillery)
        {
            accuracy += (_squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS) / 100f;
            range += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
            // The ammo EntityWatcher hands a live squad: prestige per level, Deep Quivers on shooters only.
            squadStats.Ammunition += _squadToLoad.UnitPrestige * (squadStats.unitType == UnitType.Ranged
                ? TabletopTavernConstants.PRESTIGE_AMMO_BONUS_RANGED
                : TabletopTavernConstants.PRESTIGE_AMMO_BONUS_ARTILLERY);
            if (squadStats.unitType == UnitType.Ranged && squadStats.SquadAttributes.DeepQuivers)
                squadStats.Ammunition += TabletopTavernConstants.DEEP_QUIVERS_AMMO_BONUS;
            if (squadStats.unitType == UnitType.Artillery && squadStats.SquadAttributes.PowderReserves)
                squadStats.Ammunition = (int)(squadStats.Ammunition * TabletopTavernConstants.POWDER_RESERVES_AMMO_MULTIPLIER);
        }
        // Mages match the live path: Range and Leadership, no Accuracy. Without this branch a
        // mage matches neither condition above - the second is an explicit whitelist - and gets
        // no prestige at all in auto-resolve while getting it correctly in a manual battle.
        else if (TabletopTavernConstants.Casts(squadStats.unitType))
        {
            range += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
            squadStats.Leadership += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_BONUS;
            // Charges are the mage's damage budget here, so prestige has to reach them or the alpha
            // strike ignores prestige entirely. Same expression the live path uses in EntityWatcher's
            // mage branch, folded into the stat copy so HandleMageCasts can just read Ammunition.
            squadStats.Ammunition += _squadToLoad.UnitPrestige * TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE;
        }

        if (squadStats.SquadAttributes.Overdraw)
            range *= TabletopTavernConstants.OVERDRAW_RANGE_MULTIPLIER;

        // Steady Aim waives the Fire-at-Will accuracy penalty, and auto-resolve has no fire modes.
        // Squads default to Volley, so the live trait only pays off while the player is in rapid
        // fire - modelled here as roughly half the 20 point penalty rather than the full amount.
        // Shooters only, matching live battle: artillery has no fire mode to switch.
        if (squadStats.SquadAttributes.SteadyAim && TabletopTavernConstants.FightsAtRange(squadStats.unitType))
            accuracy += (TabletopTavernConstants.FIRE_AT_WILL_ACCURACY_PENALTY / 2f) / 100f;

        squadStats.MeleeAttack = meleeAttack;
        squadStats.MeleeDefense = meleeDefense;
        squadStats.attackAccuracy = accuracy * accuracyMultiplier;
        squadStats.BaseRange = range;
        squadStats.WeaponStrength = WeaponStrength;
        squadStats.MissileStrength = missileStrength;

        int startingUnits = (int)(totalHealth / squadStats.HitPointsPerUnit);
        return new() {
            squadStats = squadStats,
            SquadIndex = _squadIndex+1,
            TargetIndex = -1,
            UniqueID = _squadToLoad.UniqueID,
            finalHealth = totalHealth,
            healthPerKill = squadStats.HitPointsPerUnit,
            UnitsAlive = startingUnits,
            maxUnits = startingUnits,
            armorMitigation = armorMitigation,
            shieldBlockChance = shieldBlockChance,
            damageTakenMultiplier = 1f,
            ChargeBonus = ChargeBonus,
            Race = TabletopTavernData.Instance.GetRaceFromUnitName(_squadToLoad.UnitName),
        };
    }
    public void SetUpArmies()
    {
        if (CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy == null) return;
        if (CampaignManager.Instance.CampaignSaveManager.SaveData.enemyArmy == null) return;
        List<SquadToLoad> playerArmyList = new();
        for (int i = 0; i < CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy.Length && i < 10; i++) {
            if(CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i].UnitIndex != -1)
                playerArmyList.Add(CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy[i]);
        }
        playerArmy = playerArmyList.ToArray();

        // Copy, never alias - see the matching note in Load().
        enemyArmy = (SquadToLoad[])CampaignManager.Instance.CampaignSaveManager.SaveData.enemyArmy.Clone();
        playerAutoResolveStats = new AutoResolveSquad[playerArmy.Length];
        enemyAutoResolveStats = new AutoResolveSquad[enemyArmy.Length];
        _mageAlphaStrikeApplied = false;
        playerArmyIsDefeated = false;
        enemyArmyIsDefeated = false;

        AutoResolveHeroContext hero = AutoResolveHeroContext.From(
            CampaignManager.Instance.CampaignSaveManager.SaveData.heroID, playerArmy, enemyArmy);
        for (int i = 0; i < playerArmy.Length; i++) {
            playerAutoResolveStats[i] = GenerateAutoResolveSquadStats(playerArmy[i], i, Team.Player, CampaignManager.Instance.CampaignSaveManager, hero: hero);
            // Debug.Log($"Squad {playerArmy[i].UnitName} has {playerAutoResolveStats[i].UnitsAlive} units alive");
        }
        for (int i = 0; i < enemyArmy.Length; i++) {
            enemyAutoResolveStats[i] = GenerateAutoResolveSquadStats(enemyArmy[i], i + playerArmy.Length, Team.Enemy, CampaignManager.Instance.CampaignSaveManager);
            // Debug.Log($"Squad {enemyArmy[i].UnitName} has {enemyAutoResolveStats[i].UnitsAlive} units alive");
        }
    }
    private void GenerateTestPlayerArmy()
    {
        playerArmy = new SquadToLoad[testPlayerArmySaveData.SquadsInArmy.Length];
        for (int i = 0; i < testPlayerArmySaveData.SquadsInArmy.Length; i++) {
            playerArmy[i] = new SquadToLoad(
                testPlayerArmySaveData.SquadsInArmy[i], 
                _prestige: 0, 
                _unitIndex: i
            );

            //int get base unit count
            int baseUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(playerArmy[i].UnitName);
            int maxUnitCount = TabletopTavernData.Instance.GetHitPointsPerUnit(playerArmy[i].UnitName);
            playerArmy[i].SquadCurrentHealth = baseUnitCount * maxUnitCount;
            playerArmy[i].maxUnitCount = baseUnitCount;
            playerArmy[i].HitPointsPerUnit = maxUnitCount;
        }
        playerAutoResolveStats = new AutoResolveSquad[playerArmy.Length];
        _mageAlphaStrikeApplied = false;
        playerArmyIsDefeated = false;

        for (int i = 0; i < playerArmy.Length; i++) {
            playerAutoResolveStats[i] = GenerateAutoResolveSquadStats(playerArmy[i], i, Team.Player, allowGearModifiers: false);
        }
    }
    private void GenerateTestEnemyArmy()
    {
        enemyArmy = new SquadToLoad[testEnemyArmySaveData.SquadsInArmy.Length];
        for (int i = 0; i < testEnemyArmySaveData.SquadsInArmy.Length; i++) {
            enemyArmy[i] = new SquadToLoad(
                testEnemyArmySaveData.SquadsInArmy[i],
                _prestige: 0,
                _unitIndex: i
            );

            //int get base unit count
            int baseUnitCount = TabletopTavernData.Instance.GetBaseUnitCount(enemyArmy[i].UnitName);
            int maxUnitCount = TabletopTavernData.Instance.GetHitPointsPerUnit(enemyArmy[i].UnitName);
            enemyArmy[i].SquadCurrentHealth = baseUnitCount * maxUnitCount;
            enemyArmy[i].maxUnitCount = baseUnitCount;
        }
        enemyAutoResolveStats = new AutoResolveSquad[enemyArmy.Length];
        enemyArmyIsDefeated = false;

        for (int i = 0; i < enemyArmy.Length; i++) {
            enemyAutoResolveStats[i] = GenerateAutoResolveSquadStats(enemyArmy[i], i + enemyArmy.Length, Team.Enemy, allowGearModifiers: false);
        }
    }
    public void SetUpArmiesTest()
    {
        GenerateTestPlayerArmy();
        GenerateTestEnemyArmy();
    }
    [ContextMenu("Predict Auto Resolve")]
    public void PredictAutoResolve()
    {
        SetUpArmies();
        RunToCompletion();
        RecordResults(false);
    }
    public void AutoResolveThroughEditor()
    {
        RunToCompletion();
        RecordResults(false);
    }
    /// <summary>
    /// Runs rounds until one side is defeated, aborting loudly at MAX_AUTORESOLVE_ROUNDS rather
    /// than hanging. On abort the side holding less health is treated as defeated, so an engine
    /// bug costs the player a plausible result instead of an unearned loss.
    /// </summary>
    internal void RunToCompletion()
    {
        int rounds = 0;
        _idleSeconds = 0;
        while (!playerArmyIsDefeated && !enemyArmyIsDefeated)
        {
            RunSimulationLoop();
            bool stalemate = _idleSeconds >= STALEMATE_SECONDS;
            if (++rounds < MAX_AUTORESOLVE_ROUNDS && !stalemate) continue;

            int playerHealth = TotalHealthRemaining(playerAutoResolveStats);
            int enemyHealth = TotalHealthRemaining(enemyAutoResolveStats);
            if (!stalemate)
                Debug.LogError(
                    $"[AutoResolve] Aborted after {MAX_AUTORESOLVE_ROUNDS} rounds with neither side defeated. " +
                    $"Player health {playerHealth}, enemy health {enemyHealth}. " +
                    "A squad is most likely untargetable - check the target pools in AssignTargets.");
            if (enemyHealth <= playerHealth) enemyArmyIsDefeated = true;
            else playerArmyIsDefeated = true;
            AutoResolveSimulation.Settle(playerAutoResolveStats);
            AutoResolveSimulation.Settle(enemyAutoResolveStats);
            break;
        }
    }
    private static int TotalHealthRemaining(AutoResolveSquad[] _squads)
    {
        int total = 0;
        for (int i = 0; i < _squads.Length; i++) total += math.max(0, _squads[i].finalHealth);
        return total;
    }
    public void AutoResolve()
    {
        RecordResults(true);
    }
    /// <summary>One simulated second. Mage charges are spent on the first call, then the field advances.</summary>
    public void RunSimulationLoop()
    {
        unitsSlainData = new();
        AutoResolveSimulation.Initialize(playerAutoResolveStats, enemyAutoResolveStats);
        AssignTargets();
        HandleMageCasts();
        bool active = AutoResolveSimulation.Tick(playerAutoResolveStats, enemyAutoResolveStats,
            _isGarrisonBattle ? GARRISON_AUTORESOLVE_BONUS : 1f, unitsSlainData);
        _idleSeconds = active ? 0 : _idleSeconds + 1;
        CheckArmyStatus();
    }
    // Seconds in a row in which nobody dealt damage or closed on a target.
    private int _idleSeconds;
    // A field where nobody can reach anybody (two spent artillery batteries, say) is called on health.
    private const int STALEMATE_SECONDS = 120;
    internal void AssignTargets()
    {
        AutoResolveSimulation.Initialize(playerAutoResolveStats, enemyAutoResolveStats);
        AutoResolveSimulation.AssignTargets(playerAutoResolveStats, enemyAutoResolveStats);
        AutoResolveSimulation.AssignTargets(enemyAutoResolveStats, playerAutoResolveStats);
    }
    // ApplyDamageSystem's physical path for one hit, with the mark or brace multiplier last so it
    // scales the fully resolved figure.
    internal void ModifyDamageDealt(ref int _damageDealt, AutoResolveSquad _defendingSquad, SquadStats _attackingSquad)
    {
        _damageDealt = AutoResolveSimulation.ModifyHit(_damageDealt, ref _attackingSquad, ref _defendingSquad, false);
    }
    // A mage's entire contribution to auto-resolve, resolved once at the start of the battle.
    //
    // In a live battle a mage spends one charge per cast on a long cooldown and converts to a melee
    // body when the pool empties. The whole pool is spent up front here, scaled by the spell's area
    // of effect, and the mage then closes to melee like every other caster in the simulation.
    //
    // Damage is queued into unitsSlainData and AutoResolveSimulation spreads it over the target's
    // models on the next tick, crediting kills the same way. ModifyDamageDealt is deliberately NOT
    // applied: spell damage is DamageType.Magical, which ignores armor in the live pipeline, and the
    // rest of that method is weapon-flavoured multipliers a spell has no business picking up.
    //
    // This used to assume every mage spell was damage, because Smite was the only one. It is not:
    // SpellModifierValue means healing on a HoT, a percentage on a mark, and a NEGATIVE stat delta on a
    // debuff. Read as damage, a brace dealt 900 a cast while a morale drain dealt 1. Each spell shape
    // now maps onto the stat this simulation already reads for it.
    internal void HandleMageCasts()
    {
        if (_mageAlphaStrikeApplied) return;
        _mageAlphaStrikeApplied = true;

        // Damage queued but not yet applied, keyed by SquadIndex. unitsSlainData is not drained until
        // the next tick, so without this every charge would be measured against full health and an
        // already-dead squad would keep absorbing casts.
        Dictionary<int, int> queuedDamage = new();

        // How many times a persistent spell actually lands. Auto-resolve has no clock, so a ticking
        // spell is worth its per-tick value times the ticks it would get in a live battle. Without
        // this a damage-over-time spell was valued at a single tick.
        static float ApplicationCount(TJ.Spells.SpellData spell)
        {
            if (spell.IsOneOff || spell.TickInterval <= 0f) return 1f;
            float ticks = math.max(1f, spell.SpellDuration / spell.TickInterval);
            return math.max(1f, ticks * TabletopTavernConstants.AUTORESOLVE_PERSISTENT_SPELL_UPTIME);
        }

        // Maps a battlefield-bonus stat onto the auto-resolve stat standing in for it. Returns false for
        // stats this simulation does not model (Range, Speed, ChargeBonus...), so an unmodelled buff is
        // skipped rather than silently mis-applied.
        static bool ApplyStat(ref AutoResolveSquad squad, UnitStat stat, float value)
        {
            switch (stat)
            {
                case UnitStat.Accuracy:
                    // Floor at zero, not one: a melee squad already sits at zero accuracy, and a
                    // floor of one would have a debuff RAISE it. A zero-accuracy shooter simply
                    // lands no hits.
                    squad.squadStats.attackAccuracy = math.max(0f, squad.squadStats.attackAccuracy + value);
                    return true;
                case UnitStat.Leadership:
                    // HasRouted reads Leadership, so draining it makes a squad break with more models
                    // still standing - which is what a morale spell does.
                    squad.squadStats.Leadership = math.clamp(squad.squadStats.Leadership + value, 0f, 100f);
                    return true;
                case UnitStat.MeleeAttack:
                    squad.squadStats.MeleeAttack = (int)math.max(0, squad.squadStats.MeleeAttack + value);
                    return true;
                case UnitStat.MeleeDefense:
                    squad.squadStats.MeleeDefense = (int)math.max(0, squad.squadStats.MeleeDefense + value);
                    return true;
                case UnitStat.WeaponStrength:
                    squad.squadStats.WeaponStrength = (int)math.max(0, squad.squadStats.WeaponStrength + value);
                    return true;
                case UnitStat.Armor:
                    squad.armorMitigation = math.clamp(squad.armorMitigation + value, 0f, 0.9f);
                    return true;
                default:
                    return false;
            }
        }

        void CastFrom(AutoResolveSquad[] _casters, AutoResolveSquad[] _foes, AutoResolveSquad[] _allies, float _bonusModifier)
        {
            // Index loops throughout: AutoResolveSquad is a struct, so a foreach hands out copies and
            // every stat change would be silently discarded.
            for (int c = 0; c < _casters.Length; c++)
            {
                AutoResolveSquad mageSquad = _casters[c];
                if (!TabletopTavernConstants.Casts(mageSquad.squadStats.unitType)) continue;
                if (HasRouted(mageSquad)) continue;

                // A mage whose SquadData has no mageSpell assigned contributes nothing rather than
                // throwing - the same posture SpellManager takes on an unauthored spell asset.
                var mageSpell = TabletopTavernData.Instance.SquadAssetsDictionary[mageSquad.squadStats.unitName].mageSpell;
                if (mageSpell == null) continue;

                // Charges are a SQUAD-level pool (SquadAmmunition), never per model.
                int charges = mageSquad.squadStats.Ammunition;
                if (charges <= 0) continue;

                float applications = ApplicationCount(mageSpell);
                bool onAllies = mageSpell.MageTargetPriority == MageTargetPriority.FriendlyNearestEnemy;
                AutoResolveSquad[] pool = onAllies ? _allies : _foes;

                // A friendly-target spell must never land on a caster - itself included. This
                // mirrors MageSquadFindTargetSystem, which excludes the caster and every other mage
                // from a FriendlyNearestEnemy search: a one-model support unit standing behind the
                // line is never "the ally doing the fighting". Without it a lone mage spent most of
                // its charges bracing and healing itself. Skipping mages covers the self case too,
                // since the caster is one.
                System.Func<int, bool> ineligible = i =>
                    HasRouted(pool[i]) ||
                    (onAllies && TabletopTavernConstants.Casts(pool[i].squadStats.unitType));

                // ---- a heal is effective HP, added straight back to the pool ----
                if (mageSpell.HealsInsteadOfDamage)
                {
                    int healPerCast = math.max(1, (int)(mageSpell.SpellModifierValue * applications
                                                        * TabletopTavernConstants.MAGE_AOE_MODELS_HIT));
                    for (int spent = 0; spent < charges; spent++)
                    {
                        int pick = -1, worstMissing = 0;
                        for (int i = 0; i < pool.Length; i++)
                        {
                            if (ineligible(i)) continue;
                            int missing = (pool[i].maxUnits * pool[i].healthPerKill) - pool[i].finalHealth;
                            if (missing > worstMissing) { worstMissing = missing; pick = i; }
                        }
                        if (pick == -1) break;   // nothing hurt enough to be worth a charge
                        AutoResolveSimulation.Heal(ref pool[pick], healPerCast);
                    }
                    continue;
                }

                // ---- a brace cuts damage taken; a mark raises it ----
                if (mageSpell.BracesTarget || mageSpell.MarksTarget)
                {
                    float delta = mageSpell.MarksTarget
                        ? mageSpell.SpellModifierValue / 100f
                        : -TabletopTavernConstants.AUTORESOLVE_BRACE_DAMAGE_REDUCTION;
                    for (int spent = 0; spent < charges; spent++)
                    {
                        int pick = -1;
                        for (int i = 0; i < pool.Length; i++)
                        {
                            if (ineligible(i)) continue;
                            // Spread across squads rather than stacking on one: take the least-affected.
                            bool better = pick == -1 || (mageSpell.MarksTarget
                                ? pool[i].damageTakenMultiplier < pool[pick].damageTakenMultiplier
                                : pool[i].damageTakenMultiplier > pool[pick].damageTakenMultiplier);
                            if (better) pick = i;
                        }
                        if (pick == -1) break;
                        pool[pick].damageTakenMultiplier = math.clamp(pool[pick].damageTakenMultiplier + delta, 0.25f, 3f);
                    }
                    continue;
                }

                // ---- a stat aura, buff or debuff ----
                if (mageSpell.GrantsBattlefieldBonus)
                {
                    // Spread across squads. Unlike the brace and mark branches, which self-balance
                    // because they pick by the very multiplier they are changing, a stat debuff does
                    // not alter UnitsAlive - so picking purely by size re-picks the same squad every
                    // charge and dumps the whole pool on it, which zeroed an archer's accuracy
                    // outright. Fewest charges received first, biggest squad as the tie-break.
                    int[] chargesApplied = new int[pool.Length];
                    for (int spent = 0; spent < charges; spent++)
                    {
                        int pick = -1;
                        for (int i = 0; i < pool.Length; i++)
                        {
                            if (ineligible(i)) continue;
                            if (pick == -1) { pick = i; continue; }
                            if (chargesApplied[i] < chargesApplied[pick] ||
                                (chargesApplied[i] == chargesApplied[pick] && pool[i].UnitsAlive > pool[pick].UnitsAlive))
                                pick = i;
                        }
                        if (pick == -1) break;
                        chargesApplied[pick]++;

                        // BonusStats takes precedence when non-empty, matching ActiveSpell.
                        bool applied = false;
                        if (mageSpell.BonusStats != null && mageSpell.BonusStats.Count > 0)
                        {
                            foreach (var bonus in mageSpell.BonusStats)
                                applied |= ApplyStat(ref pool[pick], bonus.UnitStat, bonus.Value);
                        }
                        else
                        {
                            applied = ApplyStat(ref pool[pick], mageSpell.BonusUnitStat, mageSpell.SpellModifierValue);
                        }
                        if (!applied) break;   // a stat this simulation cannot model - stop burning charges
                    }
                    continue;
                }

                // ---- a spell that actually deals damage ----
                if (mageSpell.SpellModifierValue <= 0) continue;
                if (mageSquad.TargetIndex == -1) continue;

                int chargesLeft = charges;

                // Spend on the assigned target first, then spill into anything else still standing. One
                // cast removes a large fraction of a squad, so committing every charge to a single
                // target would throw away everything past its last model - in a live battle the mage
                // simply retargets and keeps casting.
                List<int> targetOrder = new() { mageSquad.TargetIndex };
                for (int i = 0; i < _foes.Length; i++)
                    if (_foes[i].SquadIndex != mageSquad.TargetIndex) targetOrder.Add(_foes[i].SquadIndex);

                foreach (int targetIndex in targetOrder)
                {
                    if (chargesLeft <= 0) break;

                    AutoResolveSquad targetSquad = TargetSquadFromIndex(targetIndex, _foes);
                    if (HasRouted(targetSquad)) continue;

                    queuedDamage.TryGetValue(targetIndex, out int alreadyQueued);
                    int healthLeft = targetSquad.finalHealth - alreadyQueued;
                    if (healthLeft <= 0) continue;

                    // The blast cannot catch more models than the squad still has standing.
                    int modelsHit = math.min(TabletopTavernConstants.MAGE_AOE_MODELS_HIT, targetSquad.UnitsAlive);
                    int perCast   = math.max(1, (int)(mageSpell.SpellModifierValue * applications * modelsHit * _bonusModifier));

                    int castsSpent = math.min(chargesLeft, (healthLeft + perCast - 1) / perCast);
                    int damage     = math.min(castsSpent * perCast, healthLeft);

                    queuedDamage[targetIndex] = alreadyQueued + damage;
                    unitsSlainData.Add(new int3(mageSquad.SquadIndex, targetIndex, damage));
                    chargesLeft -= castsSpent;
                }
            }
        }

        CastFrom(playerAutoResolveStats, enemyAutoResolveStats, playerAutoResolveStats, 1f);
        CastFrom(enemyAutoResolveStats, playerAutoResolveStats, enemyAutoResolveStats,
            _isGarrisonBattle ? GARRISON_AUTORESOLVE_BONUS : 1f);
    }
    // internal: the difficulty sim counts routed squads with the same rule the loop uses.
    internal static bool HasRouted(AutoResolveSquad squad)
    {
        if (squad.UnitsAlive <= 0) return true;
        if (squad.SimReady) return squad.Broken;
        // Not yet simulated (hand-built armies before the first tick): the old fixed threshold.
        float routeThreshold = (1f - squad.squadStats.Leadership / 100f) * squad.maxUnits;
        return squad.UnitsAlive <= routeThreshold;
    }
    private void CheckArmyStatus()
    {
        bool playerArmyDead = true;
        bool enemyArmyDead = true;
        for (int i = 0; i < playerAutoResolveStats.Length; i++)
        {
            if (!HasRouted(playerAutoResolveStats[i])) playerArmyDead = false;
        }
        for (int i = 0; i < enemyAutoResolveStats.Length; i++)
        {
            if (!HasRouted(enemyAutoResolveStats[i])) enemyArmyDead = false;
        }

        if (playerArmyDead) {
            playerArmyIsDefeated = true;
        }
        if (enemyArmyDead) {
            enemyArmyIsDefeated = true;
        }
        // Survivors leave the field at full health, as the live post-battle save writes them.
        if (playerArmyDead || enemyArmyDead)
        {
            AutoResolveSimulation.Settle(playerAutoResolveStats);
            AutoResolveSimulation.Settle(enemyAutoResolveStats);
        }

        // if (playerArmyIsDefeated || enemyArmyIsDefeated)
        // {
        //     if (playerArmyIsDefeated) Debug.Log("Player Army Defeated");
        //     if (enemyArmyIsDefeated) Debug.Log("Enemy Army Defeated");
        // }
    }
    private AutoResolveSquad TargetSquadFromIndex(int _targetIndex, AutoResolveSquad[] _targetSquadStats)
    {
        for (int i = 0; i < _targetSquadStats.Length; i++)
        {
            if (_targetSquadStats[i].SquadIndex == _targetIndex)
                return _targetSquadStats[i];
        }
        return new();
    }
    private void RecordResults(bool _save)
    {
        string playerKey = GetPlayerArmyKey();
        List<SquadKillsStored> squadKillsStored = new();
        List<SquadLossesStored> squadLossesStored = new();
        // Debug.Log($"playerAutoResolveStats length: {playerAutoResolveStats.Length}");
        // Debug.Log($"playerArmy length: {playerArmy.Length}");
        for (int i = 0; i < playerAutoResolveStats.Length; i++)
            {
                for (int j = 0; j < playerArmy.Length; j++)
                {
                    if (playerArmy[j].UnitIndex == -1) continue;

                    if (playerAutoResolveStats[i].UniqueID == playerArmy[j].UniqueID)
                    {
                        // playerArmy[j].currentUnitCount = playerAutoResolveStats[i].UnitsAlive;
                        //clamping health to 0
                        playerAutoResolveStats[i].finalHealth = math.max(0, playerAutoResolveStats[i].finalHealth);
                        playerArmy[j].SquadCurrentHealth = playerAutoResolveStats[i].finalHealth;
                        squadKillsStored.Add(new SquadKillsStored() { SquadGUID = playerArmy[j].UniqueID, Kills = playerAutoResolveStats[i].UnitsSlain });
                        // UnitsAlive is ceiling-rounded from pooled health, so it can read as 1 even when only a
                        // sliver of a unit's health remains - floor-dividing finalHealth avoids undercounting losses by 1.
                        int endingUnits = playerAutoResolveStats[i].healthPerKill > 0 ? playerAutoResolveStats[i].finalHealth / playerAutoResolveStats[i].healthPerKill : playerAutoResolveStats[i].UnitsAlive;
                        squadLossesStored.Add(new SquadLossesStored() { SquadGUID = playerArmy[j].UniqueID, Losses = math.max(0, playerAutoResolveStats[i].maxUnits - endingUnits) });
                        //get how many units were killed
                        // Debug.Log($"Unit {playerArmy[j].UnitName} now at {playerArmy[j].SquadCurrentHealth} health");
                    }
                }
            }
        for (int i = 0; i < enemyAutoResolveStats.Length; i++)
        {
            for (int j = 0; j < enemyArmy.Length; j++)
            {
                if (enemyAutoResolveStats[i].UniqueID == enemyArmy[j].UniqueID)
                {
                    // enemyArmy[j].currentUnitCount = enemyAutoResolveStats[i].UnitsAlive;
                    //clamping health to 0
                    enemyAutoResolveStats[i].finalHealth = math.max(0, enemyAutoResolveStats[i].finalHealth);
                    enemyArmy[j].SquadCurrentHealth = enemyAutoResolveStats[i].finalHealth;
                    // Debug.Log($"Unit {enemyArmy[j].UnitName} now at {enemyArmy[j].SquadCurrentHealth} health");
                }
            }
        }

        // Zero out the defeated side's health to avoid confusion from retreating squads with remaining health
        // Check enemyArmyIsDefeated first — simultaneous defeat in one round sets both flags true,
        // and the game reports a player win (AlertOfBattleResults uses enemyArmyIsDefeated), so enemy should be zeroed.
        if (enemyArmyIsDefeated)
        {
            for (int i = 0; i < enemyArmy.Length; i++)
                enemyArmy[i].SquadCurrentHealth = 0;
        }
        else if (playerArmyIsDefeated)
        {
            for (int i = 0; i < playerArmy.Length; i++)
                playerArmy[i].SquadCurrentHealth = 0;
        }

        if(Application.isPlaying) {
            CampaignManager.Instance.MapSceneUIManager.EngagementPanel.AlertOfBattleResults(enemyArmyIsDefeated);
            if (!_save && !string.IsNullOrEmpty(playerKey))
            {
                _resultCache[playerKey] = new CachedBattleResult
                {
                    playerArmy = (SquadToLoad[])playerArmy.Clone(),
                    enemyArmy = (SquadToLoad[])enemyArmy.Clone(),
                    playerAutoResolveStats = (AutoResolveSquad[])playerAutoResolveStats.Clone(),
                    enemyAutoResolveStats = (AutoResolveSquad[])enemyAutoResolveStats.Clone(),
                    playerArmyIsDefeated = playerArmyIsDefeated,
                    enemyArmyIsDefeated = enemyArmyIsDefeated,
                };
                Debug.Log($"[AutoResolve] Result cached. Cache size: {_resultCache.Count}. Player defeated: {playerArmyIsDefeated}, Enemy defeated: {enemyArmyIsDefeated}");
            }
            if(_save){
                CampaignManager.Instance.CampaignSaveManager.SaveSquadsPostAutoresolve(playerArmy, enemyArmy, enemyArmyIsDefeated, squadKillsStored, squadLossesStored);
            }
        } else {
            //reset unit counts to max unit counts this is just for testing in editor
            for (int i = 0; i < playerArmy.Length; i++)
            {
                playerArmy[i].SquadCurrentHealth = playerArmy[i].SquadMaxHealth;
            }
            for (int i = 0; i < enemyArmy.Length; i++)
            {
                enemyArmy[i].SquadCurrentHealth = enemyArmy[i].SquadMaxHealth;
            }
        }
    }
}
}
