using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using Unity.Collections;
using System;
using ProjectDawn.Navigation;
using Unity.Mathematics;

namespace TJ
{
    public class ArtilleryCrewPrefabGO : MonoBehaviour
    {
        [SerializeField] private Animator _ArtilleryAnimator;
        [SerializeField] private ArtilleryCrewMemberGO[] crewGameObjects;
        [SerializeField] private Transform[] crewTransforms;


        private const float CREW_LEFT_BEHIND_DISTANCE = 10f;

        private Entity _artilleryCrewEntity;
        private Entity _animatorEntity;
        int cachedAnimationID = -1;
        bool battleHasStarted = false;
        bool battleHasEnded = false;
        bool inMeleeCombat = false;
        bool isDestroyed = false;
        private int _squadID;
        EntityWatcher entityWatcher;

        public void SetArtilleryEntity(Entity artilleryEntity, Entity animatorEntity, int squadID, EntityWatcher _entityWatcher)
        {
            _artilleryCrewEntity = artilleryEntity;
            _animatorEntity = animatorEntity;
            _squadID = squadID;
            //unparent all crew members
            foreach (ArtilleryCrewMemberGO crewTransform in crewGameObjects) {
                if (crewTransform == null) continue;
                crewTransform.gameObject.transform.SetParent(null);
            }
            entityWatcher = _entityWatcher;
            entityWatcher.OnArtilleryRemovedEvent += OnArtilleryRemoved;
        }

        private void Update()
        {
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (entityManager.Exists(_artilleryCrewEntity) == false) return;
            if (battleHasEnded) return;
            if (isDestroyed) return;

            SyncPositionAndRotation(entityManager);

            if (!battleHasStarted)
            {
                EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BattleHasStarted>());
                if (query.CalculateEntityCount() == 0)
                {
                    SnapCrewToSlots();
                    query.Dispose();
                    return;
                }
                query.Dispose();
                battleHasStarted = true;
            }

            // A teleport (deferred enemy deploy on Start Battle, Starstep) moves the gun without a Move animation.
            if (!inMeleeCombat && Vector3.Distance(crewGameObjects[0].transform.position, crewTransforms[0].position) > CREW_LEFT_BEHIND_DISTANCE)
                SnapCrewToSlots();

            if (!entityManager.HasComponent<GpuEcsAnimatorControlComponent>(_animatorEntity)) return;

            int animationID = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(_animatorEntity).animatorInfo.animationID;

            for (int i = 0; i < crewGameObjects.Length; i++)
            {
                crewGameObjects[i].transform.rotation = crewTransforms[i].rotation;
            }

            if (animationID != cachedAnimationID)
            {
                UpdateCrewAnimations(animationID);
            }

            if (animationID == 1 || animationID == 2)
            {
                //move crew members to their positions
                for (int i = 0; i < crewGameObjects.Length; i++)
                {
                    crewGameObjects[i].transform.position = Vector3.Lerp(crewGameObjects[i].transform.position, crewTransforms[i].position, 1f);
                    crewGameObjects[i].transform.rotation = crewTransforms[i].rotation;
                }
            }
        }
        private void UpdateCrewAnimations(int animationID)
        {
            // Debug.Log($"ArtilleryCrewPrefabGO: Changing animation to ID {animationID} for entity {_artilleryCrewEntity}");
            string animationName = GetAnimationNameFromID(animationID);
            // Debug.Log($"ArtilleryCrewPrefabGO: Changing animation to {animationName}");
            _ArtilleryAnimator.Play(animationName);
            cachedAnimationID = animationID;

            if (cachedAnimationID == 4 && !inMeleeCombat)
            {
                // Debug.Log($"ArtilleryCrewPrefabGO: Switching to melee mode for entity {_artilleryCrewEntity}");
                inMeleeCombat = true;
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.SetMeleeMode(true);
                }
            }
            if (cachedAnimationID == 14 && inMeleeCombat)
            {
                // Debug.Log($"ArtilleryCrewPrefabGO: Switching to ranged mode for entity {_artilleryCrewEntity}");
                inMeleeCombat = false;
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.SetMeleeMode(false);
                }
            }

            if (animationName == "Fire")
            {
                //play crew fire animations
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.PlayAnimation("Fire");
                }
            }
            else if (animationName == "Move")
            {
                //play crew move animations
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.CrewAnimator.SetBool("moving", true);
                    crewMember.PlayAnimation("Move");
                }
            }
            else if (animationName == "MeleeAttack")
            {
                //play crew move animations
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.PlayAnimationWithRandomDelay("MeleeAttack");
                }
            }
            else if (animationName == "Idle")
            {
                //play crew idle animations
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.CrewAnimator.SetBool("moving", false);
                }
            }
            else if (animationName == "Death")
            {
                isDestroyed = true;
                //play crew death animations
                foreach (ArtilleryCrewMemberGO crewMember in crewGameObjects)
                {
                    crewMember.PlayAnimation("Death");
                }
            }
        }
        private void SnapCrewToSlots()
        {
            for (int i = 0; i < crewGameObjects.Length; i++)
            {
                crewGameObjects[i].gameObject.transform.position = crewTransforms[i].position;
            }
        }
        private void SyncPositionAndRotation(EntityManager entityManager)
        {
            if (!entityManager.HasComponent<LocalTransform>(_artilleryCrewEntity)) return;

            LocalTransform artilleryTransform = entityManager.GetComponentData<LocalTransform>(_artilleryCrewEntity);

            //check if nans
            if(float.IsNaN(artilleryTransform.Position.x) || float.IsNaN(artilleryTransform.Position.y) || float.IsNaN(artilleryTransform.Position.z))
            {
                Debug.LogError($"ArtilleryCrewPrefabGO: NaN position for entity {_artilleryCrewEntity}");
                //manually fix by setting to zero
                //get agent body position
                AgentBody agentBody = entityManager.GetComponentData<AgentBody>(_artilleryCrewEntity);
                artilleryTransform.Position = agentBody.Destination;
                artilleryTransform.Rotation = quaternion.identity;
                entityManager.SetComponentData<LocalTransform>(_artilleryCrewEntity, artilleryTransform);
                return;
            }

            if (inMeleeCombat)
            {
                transform.position = artilleryTransform.Position;
            }
            else
            {
                transform.SetPositionAndRotation(artilleryTransform.Position, artilleryTransform.Rotation);
            }
        }
        private string GetAnimationNameFromID(int animationID)
        {
            switch (animationID) //4 meleemode 12 attack melee
            {
                case 0:
                    return "Idle";
                case 1:
                    return "Move";
                case 2:
                    return "Move";
                case 8:
                    return "Death";
                case 9:
                    return "Death";
                case 10:
                    return "Death";
                case 12:
                    return "MeleeAttack";
                case 14:
                    return "Fire";
                default:
                    return "Idle";
            }
        }
        public void OnArtilleryRemoved(int squadID)
        {
            if (squadID == _squadID)
            {
                // Debug.Log($"ArtilleryCrewPrefabGO: Removing crew for squad {squadID}");
                for (int i = 0; i < crewGameObjects.Length; i++)
                {
                    crewGameObjects[i].gameObject.transform.SetParent(this.transform);
                }
                Destroy(this.gameObject);
                entityWatcher.OnArtilleryRemovedEvent -= OnArtilleryRemoved;
            }
        }
    }
}