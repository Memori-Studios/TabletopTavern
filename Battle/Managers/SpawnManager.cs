using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Transforms;
using Unity.VisualScripting;
using GPUECSAnimationBaker.Engine.AnimatorSystem;
using TJ;
using Memori.Audio;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private UnitGPUAnimLoader unitGPUAnimLoader;

    private int unitCount;
    public int UnitCount => unitCount;
    private int unitCountOverride = 0;
    private UnitName unitName;
    public UnitName UnitName => unitName;
    private Team team;
    public Team Team => team;
    private bool outrider = false;
    public bool Outrider => outrider;

    public void SetUnitCountOverride(int count)
    {
        unitCountOverride = Mathf.Clamp(count, 0, 500);
    }
    Unity.Mathematics.Random random = new Unity.Mathematics.Random(1);
    List<float3> positions = new();

    public void SpawnSquad(int _unitCount = default, UnitName _unitName = default)
    {
        if (_unitCount != default) unitCount = _unitCount;
        if (_unitName != default) unitName = _unitName;

        positions = BattleManager.Instance.PositionDrawer.UnitPrefabPointPositions();

        StartCoroutine(PreloadThenSpawn());
    }

    private IEnumerator PreloadThenSpawn()
    {
        // Only preload if this unit hasn't been loaded yet this session
        if (UnitGPUAnimPrefabs.Find(World.DefaultGameObjectInjectionWorld.EntityManager, unitName) == null)
        {
            bool loaded = false;
            unitGPUAnimLoader.PreloadUnitsAsync(new[] { unitName }, () => loaded = true);
            yield return new WaitUntil(() => loaded);
        }

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var entityCommandBuffer = new EntityCommandBuffer(Allocator.Temp);
        float noise = TabletopTavernData.Instance.GetSquadNoise(unitName);

        SquadSpawner ss = new() {
            unitCount = unitCount,
            unitName = unitName,
            team = team,
            noise = noise,
        };

        SpawnFormation(entityManager, ss, entityCommandBuffer);
        BattleManager.Instance.SetCursorMode(CursorMode.Free);
        if (team == Team.Enemy)
            BattleManager.Instance.ArmySpawnManager.NotifyEnemyDeployed();
    }
    public void SelectUnitToSpawn(UnitName _unitName)
    {
        unitName = _unitName;
        team = BattleManager.Instance.SelectedTeam;
        SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(unitName);
        outrider = squadStats.SquadAttributes.Outrider;
        unitCount = unitCountOverride > 0 ? unitCountOverride : squadStats.baseUnitCount;
    }
    public void SpawnFormation(EntityManager entityManager, SquadSpawner squadSpawner, EntityCommandBuffer entityCommandBuffer)
    {
        IAudioRequester.Instance.PlayVoice(TabletopTavernData.Instance.GetRandomBarkSFX(squadSpawner.unitName));

        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitiesReferences>());
        EntitiesReferences entitiesReferences = query.GetSingleton<EntitiesReferences>();
        
        quaternion squadRotation = BattleManager.Instance.PositionDrawer.PositionsParent.rotation *  Quaternion.Euler(0, 90, 0);
        NativeArray<Entity> spawnedEntities = new(squadSpawner.unitCount, Allocator.Temp);
        List<float3> entityPositions = new ();

        // Resolve GPU anim prefabs — prefer UnitGPUAnimPrefabs (migrated units) over EntitiesReferences
        UnitGPUAnimPrefabs? unitGPUAnims = UnitGPUAnimPrefabs.Find(entityManager, squadSpawner.unitName);
        if (!unitGPUAnims.HasValue)
        {
            Debug.LogError($"[SpawnManager] GPU anim prefabs not loaded for {squadSpawner.unitName}. Ensure PreloadThenSpawn is used.");
            return;
        }

        for (int i = 0; i < squadSpawner.unitCount; i++)
        {
            Entity spawnedEntity = entityManager.Instantiate(entitiesReferences.basePlayerUnitPrefabEntity);
            Vector3 spawnPos = TabletopTavernData.Instance.GetNoiseFromUnitName(squadSpawner.unitName, positions[i]);
            entityCommandBuffer.SetComponent(spawnedEntity, LocalTransform.FromPositionRotation(spawnPos, squadRotation));

            entityManager.AddComponentData(spawnedEntity, new Unit {
                squadId = 0,
                Team = squadSpawner.team,
                unitName = squadSpawner.unitName,
                unitState = UnitState.Spawn,
                unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadSpawner.unitName)
            });

            entityCommandBuffer.SetComponent(spawnedEntity, 
                new SetDestination { 
                    destinationPosition = spawnPos, 
                    // destinationRotation = squadRotation,
                    squadPosition = spawnPos
                });
            entityCommandBuffer.AddComponent(spawnedEntity, 
                new UnitPosition { 
                    unitIndex = i,
                });

            int randomIndex = random.NextInt(0, 9);

            Entity gpuAnimPrefab = unitGPUAnims.Value.Get(randomIndex);
            Entity childEntity = entityManager.Instantiate(gpuAnimPrefab);

            entityCommandBuffer.AddComponent(childEntity, new Parent { Value = spawnedEntity });

            AnimationDataHolder dat = entityManager.GetComponentData<AnimationDataHolder>(spawnedEntity);
            dat.gpuEcsAnimatorEntity = childEntity;
            bool isDwarf = TabletopTavernData.Instance.GetRaceFromUnitName(squadSpawner.unitName) == Race.DeepstoneHold;
            dat.RunSpeedThreshold = isDwarf ? 1f : 2f;
            dat.WalkSpeedThreshold = isDwarf ? 0.5f : 1f;
            entityCommandBuffer.SetComponent(spawnedEntity, dat);

            GpuEcsAnimatorControlComponent controlComp = entityManager.GetComponentData<GpuEcsAnimatorControlComponent>(dat.gpuEcsAnimatorEntity);
            controlComp.transitionSpeed = 0.5f;
            controlComp.startNormalizedTime = 0; //random.NextFloat(0, 1);
            entityManager.SetComponentData(dat.gpuEcsAnimatorEntity, controlComp);

            spawnedEntities[i] = spawnedEntity;

            entityPositions.Add(positions[i]);
            BattleManager.Instance.UnitDebugSetUp.SetUpPositionDebug(spawnedEntity, entityManager);
        }
    
        SquadSpawnData squadData = new () {
            unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadSpawner.unitName),
            unitName = squadSpawner.unitName,
            Team = squadSpawner.team,
            entityPositions = entityPositions,
            widthAndDepth = BattleManager.Instance.PositionDrawer.Formation.SpawnWidthAndDepth,
            squadRotation = squadRotation,
        };

        List<Entity> entityList = squadSpawner.team == Team.Player
            ? spawnedEntities.ToListPooled()
            : new List<Entity>(spawnedEntities);
        spawnedEntities.Dispose();

        BattleManager.Instance.SquadManager.RegisterSquad(entityList, squadData, BattleManager.Instance.UnitsToSpawnPrestige, System.Guid.NewGuid().ToString());

        entityCommandBuffer.Playback(entityManager);
        entityCommandBuffer.Dispose();
        query.Dispose();
    }

}

