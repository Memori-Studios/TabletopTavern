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
    }
    public class AutoResolveBattleManager : MonoBehaviour
    {
        public static float CHANCE_TO_HIT = 0.35f;
        // [SerializeField] private float turnTickRate = 1f, meleeMultiplier = 1f, rangedMultiplier = 1f;
        [Header("Testing")]
        [SerializeField] private ArmySaveData testPlayerArmySaveData;
        [SerializeField] private ArmySaveData testEnemyArmySaveData, playerArmyForEnemyScoringSaveData;
        [SerializeField] private SquadToLoad[] playerArmy, enemyArmy;
        bool playerArmyIsDefeated, enemyArmyIsDefeated;
        [SerializeField] AutoResolveSquad[] playerAutoResolveStats, enemyAutoResolveStats;
        public SquadToLoad[] PredictedPlayerArmy => playerArmy;
        public AutoResolveSquad[] PlayerAutoResolveStats => playerAutoResolveStats;
        public AutoResolveSquad[] EnemyAutoResolveStats => enemyAutoResolveStats;
        List<int3> unitsSlainData = new();
        private float ENEMY_AUTORESOLVE_SPECIAL_BONUS => CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber switch {
            2 => 1.25f,
            3 => 1.50f,
            _ => 1.00f,
        };
        private const float GARRISON_AUTORESOLVE_BONUS = 1.4f;
        private bool _isGarrisonBattle;
        // One-shot per battle. Reset wherever the armies are (re)built, not in RunSimulationLoop,
        // which is called once per round.
        private bool _mageAlphaStrikeApplied;

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

        private void Start()
        {
            CampaignManager.Instance.ConsumableManager.OnConsumableUsed += OnConsumableUsed;
        }
        private void OnDestroy()
        {
            if(CampaignManager.HasInstance && CampaignManager.Instance.ConsumableManager != null)
                CampaignManager.Instance.ConsumableManager.OnConsumableUsed -= OnConsumableUsed;
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
            enemyArmy = CampaignManager.Instance.CampaignSaveManager.SaveData.enemyArmy;

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
    private static AutoResolveSquad GenerateAutoResolveSquadStats(SquadToLoad _squadToLoad, int _squadIndex, Team _team, CampaignSaveManager campaignSaveManager = null, bool allowGearModifiers = true)
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

        if(allowGearModifiers && campaignSaveManager != null)
        {
            if(campaignSaveManager.SaveData.battleFieldPreset.weather == Weather.Rain) {
                accuracyMultiplier *= 0.5f;
            }

            if(_team == Team.Player)
            {
                if(campaignSaveManager.CheckForGear(GearID.ArmingSwords) && TabletopTavernConstants.FightsInMelee(unitType))
                    meleeAttack += GearData.GetGear(GearID.ArmingSwords).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.BucklerShields) && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields))
                    meleeDefense += GearData.GetGear(GearID.BucklerShields).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.DiamondTippedArrows) && unitType == UnitType.Ranged) 
                    squadStats.SquadAttributes.ArmorPiercing = true;
                if(campaignSaveManager.CheckForGear(GearID.HeavyWeapons) && squadStats.RarityTier == UnitRarity.Rare) 
                    squadStats.SquadAttributes.ArmorPiercing = true; 
                if(campaignSaveManager.CheckForGear(GearID.Turkey) && unitType == UnitType.Ranged) 
                    squadStats.SquadAttributes.AntiLarge = true;
                if(campaignSaveManager.CheckForGear(GearID.Longbows) && unitType == UnitType.Ranged) 
                    range += GearData.GetGear(GearID.Longbows).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.Glaives) && squadStats.SquadAttributes.AntiLarge) 
                    WeaponStrength += GearData.GetGear(GearID.Glaives).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.TexanBBQ) && TabletopTavernConstants.FightsInMelee(unitType))
                    WeaponStrength += GearData.GetGear(GearID.TexanBBQ).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.BallisticCharts)) 
                    accuracy += (GearData.GetGear(GearID.BallisticCharts).GearModifierValue/100f);
                if(campaignSaveManager.CheckForGear(GearID.ConscriptionOrders) && squadStats.RarityTier == UnitRarity.Common) {
                    meleeAttack += GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue;
                    meleeDefense += GearData.GetGear(GearID.ConscriptionOrders).GearModifierValue;
                }
                if(campaignSaveManager.CheckForGear(GearID.JoustingLances) && (squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields)) 
                    meleeDefense += GearData.GetGear(GearID.JoustingLances).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.GnomishArmorers) && squadStats.RarityTier == UnitRarity.Rare)
                    meleeDefense += GearData.GetGear(GearID.GnomishArmorers).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.WellHonedAxes) && squadStats.SquadAttributes.ArmorPiercing) //must apply after diamond tipped arrows
                    meleeAttack += GearData.GetGear(GearID.WellHonedAxes).GearModifierValue;
                if(campaignSaveManager.CheckForGear(GearID.RavensEye) && squadStats.RarityTier != UnitRarity.Common && unitType == UnitType.Ranged) 
                    accuracy += (GearData.GetGear(GearID.RavensEye).GearModifierValue/100f);
                if(campaignSaveManager.CheckForGear(GearID.RingoftheElvenKing) && squadStats.unitType == UnitType.Ranged)
                    missileStrength += GearData.GetGear(GearID.RingoftheElvenKing).GearModifierValue;
                if(squadStats.SquadAttributes.StandardShields || squadStats.SquadAttributes.HeavyShields) {
                    if(campaignSaveManager.CheckForGear(GearID.TowerShields)) {
                        shieldBlockChance = GearData.GetGear(GearID.TowerShields).GearModifierValue/100f;
                    } else {
                        shieldBlockChance = 0.5f;
                    }
                }
            }
        }

        if (squadStats.SquadAttributes.HeavyShields && shieldBlockChance < 0.75f)
            shieldBlockChance = 0.75f;

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
            // mage branch, folded into the stat copy so HandleMageAlphaStrike can just read Ammunition.
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
            ChargeBonus = ChargeBonus,
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

        enemyArmy = CampaignManager.Instance.CampaignSaveManager.SaveData.enemyArmy;
        playerAutoResolveStats = new AutoResolveSquad[playerArmy.Length];
        enemyAutoResolveStats = new AutoResolveSquad[enemyArmy.Length];
        _mageAlphaStrikeApplied = false;
        playerArmyIsDefeated = false;
        enemyArmyIsDefeated = false;

        for (int i = 0; i < playerArmy.Length; i++) {
            playerAutoResolveStats[i] = GenerateAutoResolveSquadStats(playerArmy[i], i, Team.Player, CampaignManager.Instance.CampaignSaveManager);
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
        while (!playerArmyIsDefeated && !enemyArmyIsDefeated)
        {
            RunSimulationLoop();
        }
        RecordResults(false);
    }
    public void AutoResolveThroughEditor()
    {
        while (!playerArmyIsDefeated && !enemyArmyIsDefeated)
        {
            RunSimulationLoop();
        }
        RecordResults(false);
    }
    public void AutoResolve()
    {
        RecordResults(true);
    }
    public void RunSimulationLoop()
    {
        unitsSlainData = new();
        AssignTargets();
        HandleMageAlphaStrike();
        HandleRangedUnits();
        HandleMeleeUnits();
        RemoveSlainUnits();
        CheckArmyStatus();
    }
    private void AssignTargets()
    {
        static int FindMeleeSquadTarget(AutoResolveSquad[] _targetSquadStats)
        {
            List<AutoResolveSquad> meleeSquads = new();
            for (int i = 0; i < _targetSquadStats.Length; i++)
            {
                if (HasRouted(_targetSquadStats[i])) continue;
                // Hybrids are front-line targets. AssignTargets only falls through to the ranged
                // pool once no melee squad is left, so this is what exposes them from turn one.
                if (TabletopTavernConstants.FightsInMelee(_targetSquadStats[i].squadStats.unitType))
                    meleeSquads.Add(_targetSquadStats[i]);
            }

            if (meleeSquads.Count == 0) return -1;

            return meleeSquads[UnityEngine.Random.Range(0, meleeSquads.Count)].SquadIndex;
        }
        static int FindRangedSquadTarget(AutoResolveSquad[] _targetSquadStats)
        {
            List<AutoResolveSquad> rangedSquads = new();
            for (int i = 0; i < _targetSquadStats.Length; i++)
            {
                if (HasRouted(_targetSquadStats[i])) continue;
                // Mages hold the back line beside archers and artillery, so they belong in the same
                // pool. Without this a mage is in NEITHER pool - FightsInMelee excludes it by design -
                // and a squad in neither pool can never be targeted, so it takes no damage all battle.
                if (_targetSquadStats[i].squadStats.unitType == UnitType.Ranged
                    || _targetSquadStats[i].squadStats.unitType == UnitType.Artillery
                    || TabletopTavernConstants.Casts(_targetSquadStats[i].squadStats.unitType))
                    rangedSquads.Add(_targetSquadStats[i]);
            }

            if (rangedSquads.Count == 0) return -1; //if there are no ranged squads, return -1

            return rangedSquads[UnityEngine.Random.Range(0, rangedSquads.Count)].SquadIndex; //random chance to target one of the ranged squads
        }
        // Both pools above are positive whitelists, so any UnitType appended later drops out of both
        // and silently becomes untargetable - it takes no damage for the whole battle, and if both
        // sides are reduced to such squads CheckArmyStatus never routs either one and the caller's
        // while loop spins forever. Falling back to anything still standing rules that out by
        // construction rather than by remembering to update two lists.
        static int FindAnySquadTarget(AutoResolveSquad[] _targetSquadStats)
        {
            List<AutoResolveSquad> standingSquads = new();
            for (int i = 0; i < _targetSquadStats.Length; i++)
            {
                if (HasRouted(_targetSquadStats[i])) continue;
                standingSquads.Add(_targetSquadStats[i]);
            }

            if (standingSquads.Count == 0) return -1;

            return standingSquads[UnityEngine.Random.Range(0, standingSquads.Count)].SquadIndex;
        }
        
        for (int i = 0; i < playerAutoResolveStats.Length; i++)
        {
            if (HasRouted(playerAutoResolveStats[i])) continue;

            int newTargetIndex = FindMeleeSquadTarget(enemyAutoResolveStats);

            if (newTargetIndex == -1 ) {
                int newTarget = FindRangedSquadTarget(enemyAutoResolveStats);
                if (newTarget != -1) newTargetIndex = newTarget;
            }
            if (newTargetIndex == -1) newTargetIndex = FindAnySquadTarget(enemyAutoResolveStats);

            playerAutoResolveStats[i].TargetIndex = newTargetIndex;
        }

        for (int i = 0; i < enemyAutoResolveStats.Length; i++)
        {
            if (HasRouted(enemyAutoResolveStats[i])) continue;

            int newTargetIndex = FindMeleeSquadTarget(playerAutoResolveStats);

            if (newTargetIndex == -1 ) {
                int newTarget = FindRangedSquadTarget(playerAutoResolveStats);
                if (newTarget != -1) newTargetIndex = newTarget;
            }
            if (newTargetIndex == -1) newTargetIndex = FindAnySquadTarget(playerAutoResolveStats);
            
            enemyAutoResolveStats[i].TargetIndex = newTargetIndex;
        }
    }
    private void ModifyDamageDealt(ref int _damageDealt, AutoResolveSquad _defendingSquad, SquadStats _attackingSquad)
    {
        if(_defendingSquad.armorMitigation > 0) {
            float effectiveMitigation = _defendingSquad.armorMitigation;
            if (_attackingSquad.SquadAttributes.ArmorPiercing) effectiveMitigation *= 0.5f;
            if (_attackingSquad.SquadAttributes.ArmorSundering || _attackingSquad.SquadAttributes.Emblazing)
                effectiveMitigation *= 0.5f;
            _damageDealt -= (int)(_damageDealt * effectiveMitigation);
        }

        if(_defendingSquad.squadStats.unitSize == UnitSize.Infantry && _attackingSquad.SquadAttributes.AntiInfantry) _damageDealt *= 2;
        if(_defendingSquad.squadStats.unitSize != UnitSize.Infantry && _attackingSquad.SquadAttributes.AntiLarge) _damageDealt *= 2;

        if (_attackingSquad.SquadAttributes.MonsterSlayer &&
            (_defendingSquad.squadStats.unitSize == UnitSize.Monstrous || _defendingSquad.squadStats.unitSize == UnitSize.SingleUnit))
            _damageDealt *= 2;

        if (_attackingSquad.SquadAttributes.Terrifying && !_defendingSquad.squadStats.SquadAttributes.Stalwart)
            _damageDealt = (int)(_damageDealt * 1.2f);

        if (_defendingSquad.squadStats.unitSize == UnitSize.Cavalry)
            _damageDealt = (int)(_damageDealt * 0.5f);

        if (_attackingSquad.unitSize == UnitSize.Cavalry)
            _damageDealt = (int)(_damageDealt * 1.5f);
    }
    private void HandleRangedUnits()
    {
        static int CalculateRangedDamage(AutoResolveSquad _attackingSquad, AutoResolveSquad _targetSquad, float bonusModifier = 1f)
        {
            float shieldBlockChance = _targetSquad.shieldBlockChance;
            float attacks = _attackingSquad.UnitsAlive;
            float hits = _attackingSquad.squadStats.attackAccuracy * attacks;
            hits *= 1 - shieldBlockChance;
            int damage = _attackingSquad.squadStats.MissileStrength * (int)hits * 2;
            if (_attackingSquad.squadStats.SquadAttributes.FlamingAmmo)
                damage = (int)(damage * 1.25f);

            // Shooter traits. Auto-resolve has no reload timer, blast radius or ammo count, so each
            // one maps to the damage throughput it buys in a live battle:
            // Shot Discipline -> shots per minute, Demolisher -> blast damage, Powder Reserves ->
            // not running dry mid-fight (the loosest of the three, since ammo is unmodelled here).
            if (_attackingSquad.squadStats.SquadAttributes.ShotDiscipline)
                damage = (int)(damage / TabletopTavernConstants.SHOT_DISCIPLINE_RELOAD_MULTIPLIER);
            if (_attackingSquad.squadStats.SquadAttributes.Demolisher)
                damage = (int)(damage * TabletopTavernConstants.DEMOLISHER_EXPLOSION_MULTIPLIER);
            if (_attackingSquad.squadStats.SquadAttributes.PowderReserves ||
                _attackingSquad.squadStats.SquadAttributes.DeepQuivers)
                damage = (int)(damage * 1.1f);

            damage = (int)(damage * bonusModifier);
            damage = math.max(1, damage);
            return damage;
        }

        foreach (AutoResolveSquad attackingSquad in playerAutoResolveStats)
        {
            if (HasRouted(attackingSquad)) continue;
            // Hybrids resolve entirely through HandleMeleeUnits. Auto-resolve has no approach phase
            // to shoot during, and they are front-line targets here, so they simply fight.
            // Mages skip this loop too, but for the opposite reason: they have no missile attack to
            // resolve here at all. Their contribution is the one-off alpha strike, applied before
            // normal resolution begins - without this guard a mage would deal ranged damage every
            // round using an accuracy and missile strength it does not have.
            if(TabletopTavernConstants.FightsInMelee(attackingSquad.squadStats.unitType)) continue;
            if(TabletopTavernConstants.Casts(attackingSquad.squadStats.unitType)) continue;
            if(attackingSquad.TargetIndex == -1) continue;

            AutoResolveSquad targetSquad = TargetSquadFromIndex(attackingSquad.TargetIndex, enemyAutoResolveStats);
            int damage = CalculateRangedDamage(attackingSquad, targetSquad);
            ModifyDamageDealt(ref damage, targetSquad, attackingSquad.squadStats);
            unitsSlainData.Add(new int3(attackingSquad.SquadIndex, targetSquad.SquadIndex, damage));
        }
        foreach (AutoResolveSquad attackingSquad in enemyAutoResolveStats)
        {
            if (HasRouted(attackingSquad)) continue;
            if(TabletopTavernConstants.FightsInMelee(attackingSquad.squadStats.unitType)) continue;
            if(TabletopTavernConstants.Casts(attackingSquad.squadStats.unitType)) continue;
            if(attackingSquad.TargetIndex == -1) continue;

            AutoResolveSquad targetSquad = TargetSquadFromIndex(attackingSquad.TargetIndex, playerAutoResolveStats);
            float rangedEnemyBonus = ENEMY_AUTORESOLVE_SPECIAL_BONUS * (_isGarrisonBattle ? GARRISON_AUTORESOLVE_BONUS : 1f);
            int damage = CalculateRangedDamage(attackingSquad, targetSquad, rangedEnemyBonus);
            ModifyDamageDealt(ref damage, targetSquad, attackingSquad.squadStats);
            unitsSlainData.Add(new int3(attackingSquad.SquadIndex, targetSquad.SquadIndex, damage));
        }
    }
    // A mage's entire ranged contribution, resolved once at the start of the battle.
    //
    // In a live battle it spends one charge per cast on a long cooldown and converts to a melee body
    // when the pool empties. Auto-resolve has no clock to spread that over, so the whole pool is spent
    // up front, scaled by the spell's area of effect, and the mage then fights on with its melee
    // stats, which is
    // exactly what HandleMeleeUnits already does for it (its attacker guard skips only Ranged and
    // Artillery). That makes the two paths agree on the mage's total output without modelling cooldowns.
    //
    // Damage is queued into unitsSlainData like every other source so RemoveSlainUnits applies it and
    // kill credit is recorded the same way. ModifyDamageDealt is deliberately NOT applied: spell damage
    // is DamageType.Magical, which ignores armor in the live pipeline, and the rest of that method is
    // weapon-flavoured multipliers (AntiInfantry, MonsterSlayer, cavalry) that a spell has no business
    // picking up.
    private void HandleMageAlphaStrike()
    {
        if (_mageAlphaStrikeApplied) return;
        _mageAlphaStrikeApplied = true;

        // Damage queued but not yet applied, keyed by SquadIndex. unitsSlainData is not drained until
        // RemoveSlainUnits, so without this every charge would be measured against full health and an
        // already-dead squad would keep absorbing casts.
        Dictionary<int, int> queuedDamage = new();

        void StrikeWith(AutoResolveSquad[] _attackingSquads, AutoResolveSquad[] _targetSquads, float _bonusModifier)
        {
            foreach (AutoResolveSquad mageSquad in _attackingSquads)
            {
                if (!TabletopTavernConstants.Casts(mageSquad.squadStats.unitType)) continue;
                if (HasRouted(mageSquad)) continue;
                if (mageSquad.TargetIndex == -1) continue;

                // A mage whose SquadData has no mageSpell assigned contributes nothing rather than
                // throwing - the same posture SpellManager takes on an unauthored spell asset.
                var mageSpell = TabletopTavernData.Instance.SquadAssetsDictionary[mageSquad.squadStats.unitName].mageSpell;
                if (mageSpell == null) continue;

                // Charges are a SQUAD-level pool (SquadAmmunition), never per model.
                int chargesLeft = mageSquad.squadStats.Ammunition;
                if (chargesLeft <= 0) continue;

                // Spend on the assigned target first, then spill into anything else still standing.
                // One cast removes a large fraction of a squad, so committing every charge to a single
                // target would throw away everything past its last model - in a live battle the mage
                // simply retargets and keeps casting.
                List<int> targetOrder = new() { mageSquad.TargetIndex };
                foreach (AutoResolveSquad candidate in _targetSquads)
                    if (candidate.SquadIndex != mageSquad.TargetIndex) targetOrder.Add(candidate.SquadIndex);

                foreach (int targetIndex in targetOrder)
                {
                    if (chargesLeft <= 0) break;

                    AutoResolveSquad targetSquad = TargetSquadFromIndex(targetIndex, _targetSquads);
                    if (HasRouted(targetSquad)) continue;

                    queuedDamage.TryGetValue(targetIndex, out int alreadyQueued);
                    int healthLeft = targetSquad.finalHealth - alreadyQueued;
                    if (healthLeft <= 0) continue;

                    // The blast cannot catch more models than the squad still has standing.
                    int modelsHit = math.min(TabletopTavernConstants.MAGE_AOE_MODELS_HIT, targetSquad.UnitsAlive);
                    int perCast   = math.max(1, (int)(mageSpell.SpellModifierValue * modelsHit * _bonusModifier));

                    int castsSpent = math.min(chargesLeft, (healthLeft + perCast - 1) / perCast);
                    int damage     = math.min(castsSpent * perCast, healthLeft);

                    queuedDamage[targetIndex] = alreadyQueued + damage;
                    unitsSlainData.Add(new int3(mageSquad.SquadIndex, targetIndex, damage));
                    chargesLeft -= castsSpent;
                }
            }
        }

        StrikeWith(playerAutoResolveStats, enemyAutoResolveStats, 1f);
        StrikeWith(enemyAutoResolveStats, playerAutoResolveStats,
            ENEMY_AUTORESOLVE_SPECIAL_BONUS * (_isGarrisonBattle ? GARRISON_AUTORESOLVE_BONUS : 1f));
    }
    private void HandleMeleeUnits()
    {
        static int CalculateMeleeDamage(AutoResolveSquad _attackingSquad, AutoResolveSquad _targetSquad, float bonusModifier = 1f)
        {
            // int formationWidth = DataTypes.GetFormationWidthFromUnitSize(TabletopTavernData.Instance.GetUnitSizeFromUnitName(_attackingSquad.squadStats.unitName));
            // int unitsAttacking = Mathf.Min(_attackingSquad.UnitsAlive, formationWidth);
            // Debug.Log($"unitsAttacking: {unitsAttacking}");
            float attacks = _attackingSquad.UnitsAlive;
            float chanceToHit = CHANCE_TO_HIT + (0.04f * (_attackingSquad.squadStats.MeleeAttack - _targetSquad.squadStats.MeleeDefense));
            chanceToHit = math.clamp(chanceToHit, 0.1f, 0.9f);
            // Debug.Log($"Chance to hit: {chanceToHit}");
            float hits = chanceToHit * attacks;

            int weaponStrength = _attackingSquad.squadStats.WeaponStrength;
            if (_attackingSquad.squadStats.SquadAttributes.Rage && _attackingSquad.UnitsAlive * 2 < _attackingSquad.maxUnits)
                weaponStrength *= 2;

            int damage = weaponStrength * (int)hits;
            if(_attackingSquad.squadStats.unitSize == UnitSize.SingleUnit) {
                damage = (int)(weaponStrength * chanceToHit / 10f);
                // Debug.Log($"Monstrous Unit {TabletopTavernData.Instance.GetUnitSizeFromUnitName(_attackingSquad.squadStats.unitName)} dealt {damage} damage");
            }
            if(_targetSquad.squadStats.unitSize == UnitSize.SingleUnit) {
                damage /= 5;
                // Debug.Log($"Monstrous Unit {TabletopTavernData.Instance.GetUnitSizeFromUnitName(_targetSquad.squadStats.unitName)} took {damage} damage");
            }

            if(_attackingSquad.squadStats.unitSize != UnitSize.Infantry && _targetSquad.squadStats.unitSize == UnitSize.Infantry && !_targetSquad.squadStats.SquadAttributes.AntiLarge) {
                damage = (int)(damage * 1.5f);
            }

            if (_attackingSquad.squadStats.SquadAttributes.BloodFrenzy && _attackingSquad.UnitsSlain > 0)
                damage = (int)(damage * 1.25f);
            if (_attackingSquad.squadStats.SquadAttributes.BackStabbers)
                damage = (int)(damage * 1.1f);
            if (_attackingSquad.squadStats.SquadAttributes.ThrowingAxes)
                damage = (int)(damage * 1.15f);

            damage = (int)(damage * bonusModifier);
            damage = math.max(1, damage);

            return damage;
        }

        foreach (AutoResolveSquad attackingSquad in playerAutoResolveStats)
        {
            if (HasRouted(attackingSquad)) continue;
            if(attackingSquad.squadStats.unitType == UnitType.Ranged || attackingSquad.squadStats.unitType == UnitType.Artillery) continue;
            if(attackingSquad.TargetIndex == -1) continue;

            AutoResolveSquad targetSquad = TargetSquadFromIndex(attackingSquad.TargetIndex, enemyAutoResolveStats);
            int damage = CalculateMeleeDamage(attackingSquad, targetSquad);
            ModifyDamageDealt(ref damage, targetSquad, attackingSquad.squadStats);
            unitsSlainData.Add(new int3(attackingSquad.SquadIndex, targetSquad.SquadIndex, damage));
        }
        foreach (AutoResolveSquad attackingSquad in enemyAutoResolveStats)
        {
            if (HasRouted(attackingSquad)) continue;
            if(attackingSquad.squadStats.unitType == UnitType.Ranged || attackingSquad.squadStats.unitType == UnitType.Artillery) continue;
            if(attackingSquad.TargetIndex == -1) continue;

            AutoResolveSquad targetSquad = TargetSquadFromIndex(attackingSquad.TargetIndex, playerAutoResolveStats);
            float meleeEnemyBonus = ENEMY_AUTORESOLVE_SPECIAL_BONUS * (_isGarrisonBattle ? GARRISON_AUTORESOLVE_BONUS : 1f);
            int damage = CalculateMeleeDamage(attackingSquad, targetSquad, meleeEnemyBonus);
            ModifyDamageDealt(ref damage, targetSquad, attackingSquad.squadStats);
            unitsSlainData.Add(new int3(attackingSquad.SquadIndex, targetSquad.SquadIndex, damage));
        }
    }
    private void RemoveSlainUnits()
    {
        foreach (int3 data in unitsSlainData) {
            // Debug.Log($"Squad {data.x} Slain {data.z} Units from Squad {data.y}");
            RemoveUnitsFromSquad(data.z, data.y, data.x);
        }
    }
    private static bool HasRouted(AutoResolveSquad squad)
    {
        if (squad.UnitsAlive <= 0) return true;
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
    private void RemoveUnitsFromSquad(int _damageDealt, int _targetIndex, int _slayingSquadIndex)
    {
        int unitsKilledThisTurn = 0;
        //handle deaths
        for (int i = 0; i < enemyAutoResolveStats.Length; i++)
        {
            if (enemyAutoResolveStats[i].SquadIndex == _targetIndex) {
                enemyAutoResolveStats[i].finalHealth -= _damageDealt;

                //health 100 damage 10 healthPerKill 10
                //get how many units were killed
                float unitsRemaingingFloat = (float)enemyAutoResolveStats[i].finalHealth / (float)enemyAutoResolveStats[i].healthPerKill;
                // 90/10 = 9
                // Debug.Log($"unitsRemaingingFloat: {unitsRemaingingFloat}");
                //if total health is 0, round down to 0
                if (enemyAutoResolveStats[i].finalHealth < 0) unitsRemaingingFloat = 0;
                // if less than 1 but not 0, round up to 1
                int unitsRemainging = (int)math.ceil(unitsRemaingingFloat);
                // Debug.Log($"units in squad {enemyAutoResolveStats[i].SquadIndex}: {unitsRemainging}");

                if (unitsRemainging < enemyAutoResolveStats[i].UnitsAlive) {
                    // unitsRemainging = Mathf.Min(enemyAutoResolveStats[i].UnitCount, unitsKilledThisTurn); //only records kills if there are enough units to kill
                    unitsKilledThisTurn = enemyAutoResolveStats[i].UnitsAlive - unitsRemainging;
                    enemyAutoResolveStats[i].UnitsAlive -= unitsKilledThisTurn;
                }
            }
        }
        for (int i = 0; i < playerAutoResolveStats.Length; i++)
        {
            if (playerAutoResolveStats[i].SquadIndex == _targetIndex) {
                playerAutoResolveStats[i].finalHealth -= _damageDealt;

                //get how many units were killed
                float unitsRemaingingFloat = (float)playerAutoResolveStats[i].finalHealth / (float)playerAutoResolveStats[i].healthPerKill;
                //if total health is 0, round down to 0
                if (playerAutoResolveStats[i].finalHealth < 0) unitsRemaingingFloat = 0;

                int unitsRemainging = (int)math.ceil(unitsRemaingingFloat);
                // Debug.Log($"units in squad: {playerAutoResolveStats[i].UnitCount} unitsRemainging: {unitsRemainging}");

                if (unitsRemainging < playerAutoResolveStats[i].UnitsAlive) {
                    // unitsRemainging = Mathf.Min(playerAutoResolveStats[i].UnitCount, unitsKilledThisTurn); //only records kills if there are enough units to kill
                    unitsKilledThisTurn = playerAutoResolveStats[i].UnitsAlive - unitsRemainging;
                    playerAutoResolveStats[i].UnitsAlive -= unitsKilledThisTurn;
                }
            }
        }

        //handle kills
        for (int i = 0; i < enemyAutoResolveStats.Length; i++)
        {
            if (enemyAutoResolveStats[i].SquadIndex == _slayingSquadIndex) {
                enemyAutoResolveStats[i].UnitsSlain += unitsKilledThisTurn;
            }
        }
        for (int i = 0; i < playerAutoResolveStats.Length; i++)
        {
            if (playerAutoResolveStats[i].SquadIndex == _slayingSquadIndex) {
                playerAutoResolveStats[i].UnitsSlain += unitsKilledThisTurn;
            }
        }
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
