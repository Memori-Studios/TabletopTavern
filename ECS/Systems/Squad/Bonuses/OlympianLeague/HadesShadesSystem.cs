#if FACTIONUPDATE
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using TJ.Morale;

// Hades' Shades: the first time the squad wavers, every enemy squad within HADES_SHADES_RADIUS bleeds morale
// for HADES_SHADES_SECONDS, through the same squad-level drain the morale spells use.
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(MoraleSystem))]
partial struct HadesShadesSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattlePhase>();
    }

    public void OnUpdate(ref SystemState state)
    {
        var centres = new NativeList<float3>(2, Allocator.Temp);
        var teams = new NativeList<Team>(2, Allocator.Temp);
        bool hasVisuals = SystemAPI.TryGetSingletonBuffer(out DynamicBuffer<OlympianVisualRequest> visuals);

        foreach (var (shades, morale, squad, movement) in SystemAPI.Query<
            RefRW<HadesShadesBlessing>, RefRO<MoraleComponent>, RefRO<SquadEntity>, RefRO<SquadMovementComponent>>())
        {
            if (shades.ValueRO.Spent == 1 || morale.ValueRO.MoraleState == 0) continue;
            shades.ValueRW.Spent = 1;
            centres.Add(movement.ValueRO.SquadCenter);
            teams.Add(squad.ValueRO.Team);
            if (hasVisuals) visuals.Add(new OlympianVisualRequest { Position = movement.ValueRO.SquadCenter, Kind = OlympianVisual.Shades });
        }

        if (centres.Length > 0)
        {
            double expires = SystemAPI.Time.ElapsedTime + TabletopTavernConstants.HADES_SHADES_SECONDS;
            float radiusSq = TabletopTavernConstants.HADES_SHADES_RADIUS * TabletopTavernConstants.HADES_SHADES_RADIUS;
            foreach (var (squad, movement, bonuses) in SystemAPI.Query<
                RefRO<SquadEntity>, RefRO<SquadMovementComponent>, DynamicBuffer<BattlefieldBonusBufferElement>>())
            {
                for (int i = 0; i < centres.Length; i++)
                {
                    if (squad.ValueRO.Team == teams[i]) continue;
                    if (math.distancesq(movement.ValueRO.SquadCenter, centres[i]) > radiusSq) continue;
                    bonuses.Add(new BattlefieldBonusBufferElement
                    {
                        Value = new BattlefieldBonus
                        {
                            UnitStat = UnitStat.Leadership,
                            BattlefieldBonusEnum = BattlefieldBonusEnum.LesserMoraleSpell,
                            Team = Team.Neutral,
                            Value = TabletopTavernConstants.HADES_SHADES_MORALE_PER_SECOND,
                            Range = Mathf.Infinity,
                            ExpiresAtTime = expires,
                        }
                    });
                    break;
                }
            }
        }
        centres.Dispose();
        teams.Dispose();
    }
}
#endif
