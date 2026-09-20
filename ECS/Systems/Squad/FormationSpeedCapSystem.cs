using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using ProjectDawn.Navigation;

/// <summary>
/// Holds every unit of a locked group to the group's slowest member while the group marches.
/// The cap scales AgentBody.Force instead of writing AgentLocomotion.Speed, so swamp, Shieldwall
/// and battlefield-bonus speed math keep working underneath it and nothing has to be restored.
/// Only squads carrying both FormationSpeedCap and SquadMoveOverrideTag are capped, so a charge
/// or an engagement (both drop the override tag) runs at full speed on its own.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(AgentLocomotionSystemGroup))]
[UpdateBefore(typeof(AgentLocomotionSystem))]
partial struct FormationSpeedCapSystem : ISystem
{
    ComponentLookup<AgentBody>       m_BodyLookup;
    ComponentLookup<AgentLocomotion> m_LocomotionLookup;
    ComponentLookup<MoveOverride>    m_MoveOverrideLookup;
    ComponentLookup<InCombat>        m_InCombatLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        state.RequireForUpdate<FormationSpeedCap>();
        m_BodyLookup         = state.GetComponentLookup<AgentBody>(isReadOnly: false);
        m_LocomotionLookup   = state.GetComponentLookup<AgentLocomotion>(isReadOnly: true);
        m_MoveOverrideLookup = state.GetComponentLookup<MoveOverride>(isReadOnly: true);
        m_InCombatLookup     = state.GetComponentLookup<InCombat>(isReadOnly: true);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_BodyLookup.Update(ref state);
        m_LocomotionLookup.Update(ref state);
        m_MoveOverrideLookup.Update(ref state);
        m_InCombatLookup.Update(ref state);

        new FormationSpeedCapJob
        {
            BodyLookup         = m_BodyLookup,
            LocomotionLookup   = m_LocomotionLookup,
            MoveOverrideLookup = m_MoveOverrideLookup,
            InCombatLookup     = m_InCombatLookup,
        }.Schedule();
    }

    [BurstCompile]
    [WithAll(typeof(SquadMoveOverrideTag))]
    partial struct FormationSpeedCapJob : IJobEntity
    {
        public ComponentLookup<AgentBody>                  BodyLookup;
        [Unity.Collections.ReadOnly] public ComponentLookup<AgentLocomotion> LocomotionLookup;
        [Unity.Collections.ReadOnly] public ComponentLookup<MoveOverride>    MoveOverrideLookup;
        [Unity.Collections.ReadOnly] public ComponentLookup<InCombat>        InCombatLookup;

        public void Execute(in FormationSpeedCap cap, DynamicBuffer<EntityReferenceBufferElement> units)
        {
            if (cap.MaxSpeed <= 0f) return;

            for (int i = 0; i < units.Length; i++)
            {
                Entity unit = units[i].Entity;
                if (!BodyLookup.HasComponent(unit) || !LocomotionLookup.HasComponent(unit)) continue;
                if (!MoveOverrideLookup.HasComponent(unit) || !MoveOverrideLookup.IsComponentEnabled(unit)) continue;
                if (InCombatLookup.HasComponent(unit)) continue;

                float speed = LocomotionLookup[unit].Speed;
                if (speed <= cap.MaxSpeed) continue;

                AgentBody body = BodyLookup[unit];
                if (body.IsStopped) continue;

                // Locomotion clamps Force to unit length, so normalise first or a long avoidance
                // force would still resolve to full speed.
                float length = math.length(body.Force);
                if (length < 1e-4f) continue;
                if (length > 1f) body.Force /= length;
                body.Force *= cap.MaxSpeed / speed;
                BodyLookup[unit] = body;
            }
        }
    }
}
