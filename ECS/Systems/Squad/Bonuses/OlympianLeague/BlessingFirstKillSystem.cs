#if FACTIONUPDATE
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

// Ares' Fury and Apollo's Sun fire on the squad's first kill. Runs after ProcessUnitDeathSystem, the one frame
// its SquadKillTag events exist before EntityWatcher destroys them.
[UpdateInGroup(typeof(LateSimulationSystemGroup))]
[UpdateAfter(typeof(ProcessUnitDeathSystem))]
partial struct BlessingFirstKillSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
        state.RequireForUpdate<SquadStatsData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var killers = new NativeHashSet<int>(8, Allocator.Temp);
        foreach (var kill in SystemAPI.Query<RefRO<SquadKillTag>>())
            killers.Add(kill.ValueRO.SquadId);
        if (killers.Count == 0) { killers.Dispose(); return; }

        var statsData = SystemAPI.GetSingleton<SquadStatsData>();
        ref var statsBlob = ref statsData.StatsBlob.Value;
        double now = SystemAPI.Time.ElapsedTime;
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);

        foreach (var (fury, squad, movement, bonuses) in SystemAPI.Query<
            RefRW<AresFuryBlessing>, RefRO<SquadEntity>, RefRO<SquadMovementComponent>, DynamicBuffer<BattlefieldBonusBufferElement>>())
        {
            if (fury.ValueRO.Spent == 1 || !killers.Contains(squad.ValueRO.SquadId)) continue;
            fury.ValueRW.Spent = 1;
            int weaponStrength = (int)(statsBlob.GetStats(squad.ValueRO.UnitName).WeaponStrength * TabletopTavernConstants.ARES_FURY_WEAPON_STRENGTH_SHARE);
            double expires = now + TabletopTavernConstants.ARES_FURY_SECONDS;
            bonuses.Add(Timed(UnitStat.WeaponStrength, BattlefieldBonusEnum.AresFury, weaponStrength, expires));
            bonuses.Add(Timed(UnitStat.MeleeAttack, BattlefieldBonusEnum.AresFury, TabletopTavernConstants.ARES_FURY_MELEE_ATTACK, expires));
            if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = movement.ValueRO.SquadCenter, Kind = OlympianVisual.Fury });
        }

        var ecb = new EntityCommandBuffer(Allocator.Temp);
        bool hasProjectiles = SystemAPI.TryGetSingleton(out EntitiesReferences references);
        foreach (var (sun, squad, movement, bonuses, units) in SystemAPI.Query<
            RefRW<ApollosSunBlessing>, RefRO<SquadEntity>, RefRO<SquadMovementComponent>,
            DynamicBuffer<BattlefieldBonusBufferElement>, DynamicBuffer<EntityReferenceBufferElement>>())
        {
            if (sun.ValueRO.Spent == 1 || !killers.Contains(squad.ValueRO.SquadId)) continue;
            sun.ValueRW.Spent = 1;
            bonuses.Add(Timed(UnitStat.Accuracy, BattlefieldBonusEnum.ApollosSun, TabletopTavernConstants.APOLLO_SUN_ACCURACY, 0));
            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Entity;
                if (!SystemAPI.Exists(unit)) continue;
                ecb.AddComponent<FlamingRangedAttackTag>(unit);
                if (hasProjectiles && SystemAPI.HasComponent<ShootAttack>(unit))
                {
                    ShootAttack shoot = SystemAPI.GetComponent<ShootAttack>(unit);
                    shoot.ProjectileEntity = references.flamingArrowPrefabEntity;
                    ecb.SetComponent(unit, shoot);
                }
            }
            if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = movement.ValueRO.SquadCenter, Kind = OlympianVisual.Sun });
        }
        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        killers.Dispose();
    }

    // ExpiresAtTime 0 never expires; BattlefieldBonusSystem applies the value to every model and takes it back on expiry.
    private static BattlefieldBonusBufferElement Timed(UnitStat stat, BattlefieldBonusEnum kind, float value, double expiresAt) => new()
    {
        Value = new BattlefieldBonus
        {
            UnitStat = stat,
            BattlefieldBonusEnum = kind,
            Team = Team.Neutral,
            Value = value,
            Range = Mathf.Infinity,
            ExpiresAtTime = expiresAt,
        }
    };
}
#endif
