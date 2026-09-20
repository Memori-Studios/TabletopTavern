using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectDawn.Navigation;

/// <summary>
/// Mass-weighted, team-aware unit collision. Replaces the package AgentColliderSystem (disabled per
/// unit in UnitSetUpSystem) and the old LargeUnitPushSystem. Each unit computes and applies only its
/// own share of every overlap, so no pair bookkeeping is needed. Runs after NavMeshDisplacementSystem
/// and clamps its own displacement to the navmesh, so the package never rewrites Velocity from a
/// collision delta; only the side losing a shove is slowed.
/// </summary>
[BurstCompile]
[UpdateInGroup(typeof(AgentDisplacementSystemGroup), OrderLast = true)]
partial struct UnitCollisionSystem : ISystem
{
    ComponentLookup<Unit>              m_UnitLookup;
    ComponentLookup<UnitCollisionBody> m_BodyLookup;
    ComponentLookup<ThrowUnit>         m_ThrowLookup;
    ComponentLookup<RetreatingUnit>    m_RetreatingLookup;
    ComponentLookup<BracedTag>         m_BracedLookup;
    ComponentLookup<GateCollisionShape> m_GateLookup;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        m_UnitLookup       = state.GetComponentLookup<Unit>(isReadOnly: true);
        m_BodyLookup       = state.GetComponentLookup<UnitCollisionBody>(isReadOnly: true);
        m_ThrowLookup      = state.GetComponentLookup<ThrowUnit>(isReadOnly: true);
        m_RetreatingLookup = state.GetComponentLookup<RetreatingUnit>(isReadOnly: true);
        m_BracedLookup     = state.GetComponentLookup<BracedTag>(isReadOnly: true);
        m_GateLookup       = state.GetComponentLookup<GateCollisionShape>(isReadOnly: true);
        state.RequireForUpdate<UnitCollisionBody>();
        state.RequireForUpdate<AgentSpatialPartitioningSystem.Singleton>();
        state.RequireForUpdate<NavMeshQuerySystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        m_UnitLookup.Update(ref state);
        m_BodyLookup.Update(ref state);
        m_ThrowLookup.Update(ref state);
        m_RetreatingLookup.Update(ref state);
        m_BracedLookup.Update(ref state);
        m_GateLookup.Update(ref state);

        var navmesh = SystemAPI.GetSingleton<NavMeshQuerySystem.Singleton>();

        // Gates are few and their capsules reach past any spatial query, so they are tested directly.
        var gates = new NativeList<GateData>(4, state.WorldUpdateAllocator);
        new GatherGatesJob { Gates = gates }.Schedule();

        new UnitCollisionJob
        {
            Gates            = gates,
            Spatial          = SystemAPI.GetSingleton<AgentSpatialPartitioningSystem.Singleton>(),
            NavMesh          = navmesh,
            UnitLookup       = m_UnitLookup,
            BodyLookup       = m_BodyLookup,
            ThrowLookup      = m_ThrowLookup,
            RetreatingLookup = m_RetreatingLookup,
            BracedLookup     = m_BracedLookup,
            GateLookup       = m_GateLookup,
            DeltaTime        = SystemAPI.Time.DeltaTime,
        }.ScheduleParallel();

        navmesh.World.AddDependency(state.Dependency);
    }

    #region Job

    [BurstCompile]
    [WithAll(typeof(Agent))]
    [WithNone(typeof(ThrowUnit))]
    partial struct UnitCollisionJob : IJobEntity
    {
        [ReadOnly] public AgentSpatialPartitioningSystem.Singleton Spatial;
        [ReadOnly] public NavMeshQuerySystem.Singleton NavMesh;
        [ReadOnly] public ComponentLookup<Unit> UnitLookup;
        [ReadOnly] public ComponentLookup<UnitCollisionBody> BodyLookup;
        [ReadOnly] public ComponentLookup<ThrowUnit> ThrowLookup;
        [ReadOnly] public ComponentLookup<RetreatingUnit> RetreatingLookup;
        [ReadOnly] public ComponentLookup<BracedTag> BracedLookup;
        [ReadOnly] public ComponentLookup<GateCollisionShape> GateLookup;
        [ReadOnly] public NativeList<GateData> Gates;
        public float DeltaTime;

        public void Execute(
            Entity entity,
            ref LocalTransform transform,
            ref AgentBody body,
            ref NavMeshPath path,
            ref DynamicBuffer<NavMeshNode> nodes,
            ref UnitCollisionState collisionState,
            in AgentShape shape,
            in Unit unit,
            in UnitCollisionBody collisionBody)
        {
            if (IsGhost(entity, unit.unitState, RetreatingLookup)) return;

            var action = new CollisionAction
            {
                SelfEntity       = entity,
                SelfPosition     = transform.Position,
                SelfRadius       = shape.Radius,
                SelfHeight       = shape.Height,
                SelfTeam         = unit.Team,
                SelfSquadId      = unit.squadId,
                SelfState        = unit.unitState,
                SelfStopped      = body.IsStopped,
                SelfMass         = collisionBody.Mass,
                SelfImmovable    = collisionBody.Immovable,
                SelfBraced       = IsBraced(unit.squadEntity, BracedLookup),
                SelfVelocity     = body.Velocity,
                SelfVelocityXZ   = body.Velocity.xz,
                UnitLookup       = UnitLookup,
                BodyLookup       = BodyLookup,
                ThrowLookup      = ThrowLookup,
                RetreatingLookup = RetreatingLookup,
                BracedLookup     = BracedLookup,
                GateLookup       = GateLookup,
            };

            Spatial.QueryCylinder(
                transform.Position,
                shape.Radius + TabletopTavernConstants.COLLISION_MAX_RADIUS,
                shape.Height,
                ref action,
                NavigationLayers.Everything);

            for (int i = 0; i < Gates.Length; i++)
                action.ResolveGate(Gates[i]);

            collisionState.SettledTime = action.Contact ? 0f : collisionState.SettledTime + DeltaTime;

            // The velocity component pointing into a contact this unit is losing is gone, so it slides
            // along a line it cannot shove instead of wedging between two of its units.
            if (!body.IsStopped)
                body.Velocity = new float3(action.SelfVelocityXZ.x, body.Velocity.y, action.SelfVelocityXZ.y);

            if (math.lengthsq(action.Displacement) <= 1e-10f) return;

            float length = math.length(action.Displacement);
            if (length > TabletopTavernConstants.COLLISION_MAX_STEP)
                action.Displacement *= TabletopTavernConstants.COLLISION_MAX_STEP / length;

            transform.Position += new float3(action.Displacement.x, 0f, action.Displacement.y);

            // Clamp to the navmesh here so NavMeshDisplacementSystem never sees a collision delta.
            if (!path.Location.polygon.IsNull())
            {
                var newLocation = NavMesh.MoveLocation(path.Location, transform.Position, path.AreaMask);
                if (path.Grounded == Grounded.XZ)
                    transform.Position.xz = ((float3)newLocation.position).xz;
                else if (path.Grounded == Grounded.XYZ)
                    transform.Position = newLocation.position;
                NavMesh.ProgressPath(ref nodes, path.Location.polygon, newLocation.polygon);
                path.Location = newLocation;
            }
        }

        static bool IsGhost(Entity entity, UnitState unitState, in ComponentLookup<RetreatingUnit> retreatingLookup)
        {
            if (unitState == UnitState.Broken || unitState == UnitState.Dead) return true;
            return retreatingLookup.HasComponent(entity) && retreatingLookup.IsComponentEnabled(entity);
        }

        static bool IsReforming(UnitState unitState)
        {
            return unitState == UnitState.Moving || unitState == UnitState.Spawn;
        }

        static bool IsBraced(Entity squadEntity, in ComponentLookup<BracedTag> bracedLookup)
        {
            return bracedLookup.HasComponent(squadEntity) && bracedLookup.IsComponentEnabled(squadEntity);
        }

        // Mass a unit presents to one neighbour. Idle friends yield, idle enemies stand firm,
        // planted and charging units are heavier, braced squads resist enemies.
        static float EffectiveMass(float mass, UnitState unitState, bool stopped, bool braced, bool friendly)
        {
            if (unitState == UnitState.Charge)
                mass *= TabletopTavernConstants.COLLISION_CHARGE_MULT;
            if (stopped)
            {
                if (unitState == UnitState.InCombat)
                    mass *= TabletopTavernConstants.COLLISION_PLANTED_MULT;
                else if (friendly)
                    mass *= TabletopTavernConstants.COLLISION_IDLE_YIELD;
                else
                    mass *= TabletopTavernConstants.COLLISION_STANCE_MULT;
            }
            if (braced && !friendly)
                mass *= TabletopTavernConstants.COLLISION_BRACED_MULT;
            return mass;
        }

        struct CollisionAction : ISpatialQueryEntity
        {
            public Entity SelfEntity;
            public float3 SelfPosition;
            public float SelfRadius;
            public float SelfHeight;
            public Team SelfTeam;
            public int SelfSquadId;
            public UnitState SelfState;
            public bool SelfStopped;
            public float SelfMass;
            public bool SelfImmovable;
            public bool SelfBraced;
            public float3 SelfVelocity;
            public float2 SelfVelocityXZ;

            [ReadOnly] public ComponentLookup<Unit> UnitLookup;
            [ReadOnly] public ComponentLookup<UnitCollisionBody> BodyLookup;
            [ReadOnly] public ComponentLookup<ThrowUnit> ThrowLookup;
            [ReadOnly] public ComponentLookup<RetreatingUnit> RetreatingLookup;
            [ReadOnly] public ComponentLookup<BracedTag> BracedLookup;
            [ReadOnly] public ComponentLookup<GateCollisionShape> GateLookup;

            public float2 Displacement;
            public bool Contact;

            // The package query can report one neighbour once per hash cell it touches.
            FixedList512Bytes<Entity> m_Seen;

            public void Execute(Entity otherEntity, AgentBody otherBody, AgentShape otherShape, LocalTransform otherTransform)
            {
                if (otherEntity == SelfEntity) return;
                if (otherShape.Type != ShapeType.Cylinder) return;
                if (!UnitLookup.TryGetComponent(otherEntity, out Unit other)) return;
                if (!BodyLookup.TryGetComponent(otherEntity, out UnitCollisionBody otherCollision)) return;
                if (ThrowLookup.HasComponent(otherEntity)) return;
                if (GateLookup.HasComponent(otherEntity)) return;
                if (IsGhost(otherEntity, other.unitState, RetreatingLookup)) return;

                float extent = SelfHeight * 0.5f;
                float otherExtent = otherShape.Height * 0.5f;
                if (math.abs((SelfPosition.y + extent) - (otherTransform.Position.y + otherExtent)) > extent + otherExtent)
                    return;

                float2 towards = SelfPosition.xz - otherTransform.Position.xz;
                float distanceSq = math.lengthsq(towards);
                float contactRadius = SelfRadius + otherShape.Radius;
                if (distanceSq >= contactRadius * contactRadius) return;

                if (m_Seen.Length < m_Seen.Capacity)
                {
                    for (int i = 0; i < m_Seen.Length; i++)
                        if (m_Seen[i] == otherEntity) return;
                    m_Seen.Add(otherEntity);
                }

                bool friendly = other.Team == SelfTeam;
                bool bothMoving = !SelfStopped && !otherBody.IsStopped;
                // A squad reforms through itself; while charging or in combat its units must not stack.
                bool sameSquad = other.squadId == SelfSquadId;
                if (sameSquad && (IsReforming(SelfState) || IsReforming(other.unitState))) return;

                float distance = math.sqrt(distanceSq);
                float penetration = contactRadius - distance;
                if (penetration > TabletopTavernConstants.COLLISION_CONTACT_EPSILON) Contact = true;

                float2 direction;
                if (distance < 1e-4f)
                {
                    direction = math.normalizesafe(otherEntity.Index > SelfEntity.Index ? -SelfVelocity.xz : SelfVelocity.xz);
                    if (math.lengthsq(direction) < 1e-8f)
                        direction = new Unity.Mathematics.Random((uint)SelfEntity.Index + 1).NextFloat2Direction();
                    penetration = 0.01f;
                }
                else
                {
                    direction = towards / distance;
                }

                float share;
                if (SelfImmovable) share = 0f;
                else if (otherCollision.Immovable) share = 1f;
                // A standing enemy line cannot be wedged apart by movers no heavier than itself.
                else if (!friendly && !SelfStopped && otherBody.IsStopped && SelfMass <= otherCollision.Mass) share = 1f;
                else if (!friendly && SelfStopped && !otherBody.IsStopped && otherCollision.Mass <= SelfMass) share = 0f;
                else
                {
                    float selfMass = EffectiveMass(SelfMass, SelfState, SelfStopped, SelfBraced, friendly);
                    float otherMass = EffectiveMass(otherCollision.Mass, other.unitState, otherBody.IsStopped,
                        IsBraced(other.squadEntity, BracedLookup), friendly);
                    share = otherMass / (selfMass + otherMass);
                }
                if (share <= 0f) return;

                // A wall does not move, so resolve against it in full.
                float resolve = share >= 1f ? 1f : TabletopTavernConstants.COLLISION_RESOLVE_FACTOR;
                bool soft = friendly && bothMoving && !sameSquad;
                if (soft) resolve *= TabletopTavernConstants.COLLISION_FRIENDLY_CROSS_RESOLVE;

                Displacement += direction * (penetration * share * resolve);

                if (!soft && share >= 0.5f)
                {
                    float into = math.dot(SelfVelocityXZ, -direction);
                    if (into > 0f) SelfVelocityXZ += direction * into;
                }
            }

            // A gate is an immovable capsule across its wall opening: full resolution, slide along it.
            public void ResolveGate(in GateData gate)
            {
                if (gate.Entity == SelfEntity || SelfImmovable) return;

                float extent = SelfHeight * 0.5f;
                float gateExtent = gate.Height * 0.5f;
                if (math.abs((SelfPosition.y + extent) - (gate.Y + gateExtent)) > extent + gateExtent) return;

                float along = math.clamp(math.dot(SelfPosition.xz - gate.Centre, gate.Axis), -gate.HalfWidth, gate.HalfWidth);
                float2 closest = gate.Centre + gate.Axis * along;
                float2 towards = SelfPosition.xz - closest;
                float distance = math.length(towards);
                float contactRadius = SelfRadius + gate.Radius;
                if (distance >= contactRadius) return;

                float penetration = contactRadius - distance;
                Contact = true;
                float2 direction = distance < 1e-4f
                    ? new float2(-gate.Axis.y, gate.Axis.x)
                    : towards / distance;

                Displacement += direction * penetration;
                float into = math.dot(SelfVelocityXZ, -direction);
                if (into > 0f) SelfVelocityXZ += direction * into;
            }
        }
    }

    public struct GateData
    {
        public Entity Entity;
        public float2 Centre;
        public float Y;
        public float Height;
        public float Radius;
        public float2 Axis;
        public float HalfWidth;
    }

    [BurstCompile]
    partial struct GatherGatesJob : IJobEntity
    {
        public NativeList<GateData> Gates;

        public void Execute(Entity entity, in GateCollisionShape gate, in LocalTransform transform, in AgentShape shape)
        {
            Gates.Add(new GateData
            {
                Entity = entity,
                Centre = transform.Position.xz,
                Y = transform.Position.y,
                Height = shape.Height,
                Radius = shape.Radius,
                Axis = gate.Axis,
                HalfWidth = gate.HalfWidth,
            });
        }
    }

    #endregion
}
