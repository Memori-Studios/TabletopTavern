using System.Collections.Generic;
using Memori.Localization;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// Locks the battle camera onto one squad and moves it by exactly what the squad moves, so the player's framing holds.
    /// Photo mode and the follow camera each own one; neither shows text from here.
    /// </summary>
    public class SquadFollower
    {
        // Head bob: one sway cycle every two strides, full strength at this ground speed, sizes in battlefield units.
        private const float BobStride = 0.7f;
        private const float BobFullSpeed = 2.5f;
        private const float BobLift = 0.035f;
        private const float BobSway = 0.025f;
        private const float BobEaseRate = 3f;
        // A goal this far off counts as the way the squad is walking; nearer, it faces its formation.
        private const float MarchingGoalDistance = 3f;

        /// <summary>How a new lock frames the camera.</summary>
        public enum Framing { Stay, KeepOffset, Centre, Behind }

        private readonly BattleCamera _camera;
        private Vector3 _last;
        private float _bobPhase, _bobStrength;

        public Entity Squad { get; private set; } = Entity.Null;
        public bool Following => Squad != Entity.Null;

        // The shot taken behind a squad, in battlefield units; the follow camera sets these from its Inspector fields.
        public float BehindDistance = 12f;
        public float BehindHeight = 5f;
        public float BehindAimHeight = 1f;
        // Multiplies the bob's lift and sway; photo mode keeps 1.
        public float BobScale = 1f;

        public SquadFollower(BattleCamera camera) => _camera = camera;

        #region Picking a squad
        /// <summary>The squad nearest the ground point within the radius, or Entity.Null.</summary>
        public static Entity FindNearest(Vector3 point, float radius)
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return Entity.Null;
            EntityManager entities = world.EntityManager;
            EntityQuery query = entities.CreateEntityQuery(ComponentType.ReadOnly<SquadMovementComponent>());
            using NativeArray<Entity> squads = query.ToEntityArray(Allocator.Temp);
            query.Dispose();
            Entity found = Entity.Null;
            float best = radius * radius;
            foreach (Entity squad in squads)
            {
                Vector3 centre = entities.GetComponentData<SquadMovementComponent>(squad).SquadCenter;
                float distance = (new Vector2(centre.x, centre.z) - new Vector2(point.x, point.z)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                found = squad;
            }
            return found;
        }

        /// <summary>
        /// The player's squad after (or before) the locked one, in id order and wrapping. With no player squad locked, the first
        /// or last. Entity.Null when the player has none left.
        /// </summary>
        public Entity NextPlayerSquad(int direction, out bool fromCurrent)
        {
            List<(int id, Entity entity)> squads = PlayerSquads();
            fromCurrent = false;
            if (squads.Count == 0) return Entity.Null;
            int current = -1;
            if (Following)
                for (int i = 0; i < squads.Count; i++)
                    if (squads[i].entity == Squad) current = i;
            fromCurrent = current >= 0;
            int next = current < 0 ? (direction > 0 ? 0 : squads.Count - 1) : (current + direction + squads.Count) % squads.Count;
            return squads[next].entity;
        }

        /// <summary>The player's living squads, sorted by id.</summary>
        public static List<(int id, Entity entity)> PlayerSquads()
        {
            var squads = new List<(int id, Entity entity)>();
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return squads;
            EntityManager entities = world.EntityManager;
            EntityQuery query = entities.CreateEntityQuery(ComponentType.ReadOnly<SquadEntity>(), ComponentType.ReadOnly<SquadMovementComponent>());
            using NativeArray<Entity> found = query.ToEntityArray(Allocator.Temp);
            query.Dispose();
            foreach (Entity squad in found)
            {
                int id = entities.GetComponentData<SquadEntity>(squad).SquadId;
                if (id > 0) squads.Add((id, squad));
            }
            squads.Sort((a, b) => a.id.CompareTo(b.id));
            return squads;
        }
        #endregion

        #region Locking
        /// <summary>Locks onto the squad and frames it at once, so the lock is seen even while time is frozen.</summary>
        public void Lock(Entity squad, Framing framing)
        {
            if (!TryGetMovement(squad, out SquadMovementComponent movement)) return;
            Vector3 centre = movement.SquadCenter;
            switch (framing)
            {
                case Framing.KeepOffset:
                    _camera.FollowShift(new Vector3(centre.x - _last.x, 0f, centre.z - _last.z));
                    break;
                case Framing.Centre:
                    CentreOn(centre);
                    break;
                case Framing.Behind:
                    PlaceBehind(movement);
                    break;
            }
            Squad = squad;
            _last = centre;
            _bobStrength = 0f;
        }

        /// <summary>Drops the lock and the bob.</summary>
        public void Stop()
        {
            Squad = Entity.Null;
            ResetBob();
        }

        /// <summary>Drops the lock without touching the camera, for a teardown.</summary>
        public void Clear() => Squad = Entity.Null;

        public void ResetBob()
        {
            _bobStrength = 0f;
            _camera.SetFollowBob(Vector3.zero);
        }

        public bool SquadAlive() => TryGetMovement(Squad, out _);

        public string SquadName()
        {
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated || !world.EntityManager.Exists(Squad) || !world.EntityManager.HasComponent<SquadEntity>(Squad))
                return string.Empty;
            return LocalizationManager.Instance.GetText(world.EntityManager.GetComponentData<SquadEntity>(Squad).UnitName.ToString());
        }
        #endregion

        #region Every frame
        /// <summary>Moves the camera by what the squad moved this frame. False, with the lock dropped, when the squad is gone.</summary>
        public bool Tick(bool headBob)
        {
            if (!Following) return false;
            if (!TryGetMovement(Squad, out SquadMovementComponent movement))
            {
                Stop();
                return false;
            }
            Vector3 centre = movement.SquadCenter;
            Vector3 moved = centre - _last;
            _last = centre;
            if (moved.sqrMagnitude > 0f) _camera.FollowShift(moved);
            HeadBob(moved, headBob);
            return true;
        }

        // Rises and falls with the squad's stride and grows with its speed. On game time, so it slows with the battle and holds still when frozen.
        private void HeadBob(Vector3 moved, bool on)
        {
            if (!on)
            {
                ResetBob();
                return;
            }
            float gameDelta = Time.deltaTime;
            if (gameDelta <= 0f) return;
            float distance = new Vector2(moved.x, moved.z).magnitude;
            float target = Mathf.Clamp01(distance / gameDelta / BobFullSpeed);
            _bobStrength = Mathf.MoveTowards(_bobStrength, target, BobEaseRate * gameDelta);
            _bobPhase = Mathf.Repeat(_bobPhase + distance * Mathf.PI / BobStride, Mathf.PI * 2f);
            _camera.SetFollowBob(new Vector3(Mathf.Sin(_bobPhase) * BobSway, Mathf.Sin(_bobPhase * 2f) * BobLift, 0f) * (_bobStrength * BobScale));
        }
        #endregion

        #region Framing
        private void CentreOn(Vector3 squadCentre)
        {
            if (GroundPoint(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out Vector3 viewCentre))
                _camera.FollowShift(new Vector3(squadCentre.x - viewCentre.x, 0f, squadCentre.z - viewCentre.z));
        }

        // Behind the squad along the way it is walking, or its formation facing when it stands, looking down at it.
        private void PlaceBehind(SquadMovementComponent movement)
        {
            Vector3 centre = movement.SquadCenter;
            Vector3 toGoal = (Vector3)movement.GoalPosition - centre;
            toGoal.y = 0f;
            Vector3 heading = toGoal.magnitude > MarchingGoalDistance ? toGoal : (Quaternion)movement.SquadRotation * Vector3.forward;
            heading.y = 0f;
            if (heading.sqrMagnitude < 0.0001f) heading = Vector3.forward;
            heading.Normalize();
            Vector3 position = centre - heading * BehindDistance + Vector3.up * BehindHeight;
            Vector3 aim = centre + Vector3.up * BehindAimHeight;
            _camera.FollowPlace(position, Quaternion.LookRotation(aim - position, Vector3.up));
        }

        public static bool GroundPoint(Vector2 screenPoint, out Vector3 point)
        {
            Ray ray = BattleManager.Instance.BattleCamera.ScreenPointToRay(screenPoint);
            var ground = new Plane(Vector3.up, Vector3.zero);
            bool hit = ground.Raycast(ray, out float distance);
            point = hit ? ray.GetPoint(distance) : Vector3.zero;
            return hit;
        }

        private static bool TryGetMovement(Entity squad, out SquadMovementComponent movement)
        {
            movement = default;
            World world = World.DefaultGameObjectInjectionWorld;
            if (squad == Entity.Null || world == null || !world.IsCreated) return false;
            EntityManager entities = world.EntityManager;
            if (!entities.Exists(squad) || !entities.HasComponent<SquadMovementComponent>(squad)) return false;
            movement = entities.GetComponentData<SquadMovementComponent>(squad);
            return true;
        }
        #endregion
    }
}
