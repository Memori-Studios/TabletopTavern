using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine.Rendering;
using UnityEngine;
using Unity.Collections;

namespace TJ
{
public partial struct ShieldRandomizerSystem : ISystem
{
    private NativeArray<UnityObjectRef<Mesh>> _shieldMeshes; // Store meshes for random selection
    private UnityObjectRef<Material> _shieldMaterial; // Single material for all shields
    // TJBake visuals draw by registered ids, so each shield mesh is registered once per world.
    private NativeArray<BatchMeshID> _registeredMeshes;
    private BatchMaterialID _registeredMaterial;
    private bool _isInitialized;
    private Unity.Mathematics.Random _random;

    // [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // Initialize shield meshes
        _shieldMeshes = new NativeArray<UnityObjectRef<Mesh>>(80, Allocator.Persistent);
        // Load meshes from model GameObjects
        string[] shieldPaths = new[]
        {
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_01",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_02",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_03",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_04",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_05",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_06",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_07",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_08",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_09",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_10",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_11",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_12",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_13",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_14",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_15",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_16",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_17",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_18",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_19",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_20",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_21",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_22",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_23",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_24",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_25",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_26",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_27",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_28",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_29",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_30",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_31",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_32",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_33",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_34",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_35",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_36",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_37",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_38",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_39",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_40",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_41",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_42",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_43",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_44",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_45",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_46",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_47",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_48",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_49",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_50",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_51",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_52",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_53",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_54",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_55",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_56",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_57",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_58",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_59",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_60",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_61",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_62",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_63",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_64",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_65",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_66",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_67",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_68",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_69",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_70",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_71",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_72",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_73",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_74",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_75",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_76",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_77",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_78",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_79",
            "ShieldMeshes/SM_Wep_Shield_Set_Optimised_80"
        };

        for (int i = 0; i < shieldPaths.Length; i++)
        {
            GameObject model = Resources.Load<GameObject>(shieldPaths[i]);
            if (model != null)
            {
                MeshFilter meshFilter = model.GetComponentInChildren<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    _shieldMeshes[i] = meshFilter.sharedMesh;
                }
                else
                {
                    Debug.LogError($"No MeshFilter or Mesh found in {shieldPaths[i]}");
                }
            }
            else
            {
                Debug.LogError($"Failed to load model at {shieldPaths[i]}");
            }
        }

        // Load single material
        _shieldMaterial = Resources.Load<Material>("ShieldMeshes/Shield_Set_Optimised");
            
        for (int i = 0; i < _shieldMeshes.Length; i++)
        {
            if (!_shieldMeshes[i].IsValid())
            {
                Debug.LogError($"Invalid shield mesh at index {i}");
            }
        }

        // Validate material
        if (!_shieldMaterial.IsValid())
        {
            Debug.LogError("Failed to load shield material");
        }

        _registeredMeshes = new NativeArray<BatchMeshID>(_shieldMeshes.Length, Allocator.Persistent);
        _isInitialized = true;
        _random = Unity.Mathematics.Random.CreateFromIndex(0);
    }

    // [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        // Clean up NativeArray
        if (_shieldMeshes.IsCreated)
        {
            _shieldMeshes.Dispose();
        }
        // No system lookups are allowed during teardown; the registrations end with the world's EntitiesGraphicsSystem.
        if (_registeredMeshes.IsCreated)
        {
            _registeredMeshes.Dispose();
        }
    }

    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!_isInitialized || _shieldMeshes.Length == 0) return;

        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);


        foreach (var (shield, renderMeshArray, MaterialMeshInfo, entity) in SystemAPI
            .Query<ShieldRandomMesh, RenderMeshArray, RefRW<MaterialMeshInfo>>()
            .WithEntityAccess())
        {
            // Choose a random shield mesh
            int meshIndex = _random.NextInt(0, _shieldMeshes.Length);
            Mesh randomMesh = _shieldMeshes[meshIndex].Value;

            if (randomMesh == null) continue;

            // Create new RenderMeshArray with random mesh
            RenderMeshArray newRenderMeshArray = new RenderMeshArray(
                    new[] { _shieldMaterial.Value }, // Single material
                    new[] { randomMesh }             // Random shield mesh
                );

            // Update RenderMeshArray
            ecb.SetSharedComponentManaged(entity, newRenderMeshArray);

            // Update RenderBounds to match new mesh
            AABB newBounds = new AABB
            {
                Center = randomMesh.bounds.center,
                Extents = randomMesh.bounds.extents
            };
            ecb.SetComponent(entity, new RenderBounds { Value = newBounds });

            MaterialMeshInfo.ValueRW.Material= -1;
            MaterialMeshInfo.ValueRW.Mesh = -1;

            // Remove ShieldRandomMesh component
            ecb.RemoveComponent<ShieldRandomMesh>(entity);

            // Debug.Log($"Assigned shield mesh {randomMesh.name} to entity {entity}");
        }

        EntitiesGraphicsSystem graphics = null;
        foreach (var (info, entity) in SystemAPI
            .Query<RefRW<MaterialMeshInfo>>()
            .WithAll<ShieldRandomMesh>()
            .WithNone<RenderMeshArray>()
            .WithEntityAccess())
        {
            ecb.RemoveComponent<ShieldRandomMesh>(entity);
            int meshIndex = _random.NextInt(0, _shieldMeshes.Length);
            Mesh randomMesh = _shieldMeshes[meshIndex].Value;
            if (randomMesh == null || _shieldMaterial.Value == null) continue;
            if (graphics == null) graphics = state.World.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null) continue;
            if (_registeredMeshes[meshIndex] == BatchMeshID.Null) _registeredMeshes[meshIndex] = graphics.RegisterMesh(randomMesh);
            if (_registeredMaterial == BatchMaterialID.Null) _registeredMaterial = graphics.RegisterMaterial(_shieldMaterial.Value);
            info.ValueRW = new MaterialMeshInfo(_registeredMaterial, _registeredMeshes[meshIndex]);
            ecb.SetComponent(entity, new RenderBounds { Value = new AABB { Center = randomMesh.bounds.center, Extents = randomMesh.bounds.extents } });
        }
    }
}
}
