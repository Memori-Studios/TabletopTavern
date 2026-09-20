using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectDawn.Navigation;

/// <summary>
/// Walks an idle unit back to its formation slot after a shove. Three guards stop two units from
/// trading places forever: the unit must be more than RETURN_TO_SLOT_DISTANCE off its slot, must have
/// had no contact for RETURN_TO_SLOT_SETTLE_TIME, and gets RETURN_TO_SLOT_BUDGET walks per slot
/// assignment. The budget resets whenever the squad gives the unit a new slot.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
partial struct UnitReturnToSlotSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleHasStarted>();
        state.RequireForUpdate<UnitCollisionState>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new ReturnToSlotJob().ScheduleParallel();
    }

    [BurstCompile]
    [WithNone(typeof(ThrowUnit), typeof(InCombat))]
    [WithDisabled(typeof(MoveOverride))]
    partial struct ReturnToSlotJob : IJobEntity
    {
        public void Execute(
            ref AgentBody body,
            ref UnitCollisionState collisionState,
            in Unit unit,
            in SetDestination setDestination,
            in LocalTransform transform)
        {
            if (!setDestination.squadPosition.Equals(collisionState.LastSlot))
            {
                collisionState.LastSlot = setDestination.squadPosition;
                collisionState.ReturnsLeft = TabletopTavernConstants.RETURN_TO_SLOT_BUDGET;
            }

            if (unit.unitState != UnitState.Idle || !body.IsStopped) return;
            if (collisionState.ReturnsLeft <= 0) return;
            if (collisionState.SettledTime < TabletopTavernConstants.RETURN_TO_SLOT_SETTLE_TIME) return;

            float offSlot = math.distance(transform.Position.xz, setDestination.squadPosition.xz);
            if (offSlot <= TabletopTavernConstants.RETURN_TO_SLOT_DISTANCE) return;

            collisionState.ReturnsLeft--;
            // Destination already holds the slot; resuming is enough for the nav agent to walk back.
            body.IsStopped = false;
        }
    }
}
