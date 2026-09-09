using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
namespace TJ
{
public partial struct BowSetUpSystem : ISystem
{
    // [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);

        foreach (var (bow, BowEntity) in SystemAPI
            .Query<BowSetUpEntity>()
            .WithEntityAccess())
        {
            // Get the parent of the spawned entity
            if (SystemAPI.HasComponent<Parent>(BowEntity))
            {
                var parentComponent = SystemAPI.GetComponent<Parent>(BowEntity);
                Entity parentEntity = parentComponent.Value;

                if (SystemAPI.HasComponent<Parent>(parentEntity))
                {
                    var grandparentComponent = SystemAPI.GetComponent<Parent>(parentEntity);
                    // A shallower hierarchy than the archer prefab (placeholder art on a non-archer unit) has no great-grandparent.
                    if (!SystemAPI.HasComponent<Parent>(grandparentComponent.Value)) continue;

                    var greatGrandparentComponent = SystemAPI.GetComponent<Parent>(grandparentComponent.Value);
                    Entity grandparentEntity = greatGrandparentComponent.Value;

                    if(SystemAPI.HasComponent<RangedMeleeConverter>(grandparentEntity))
                    {
                        RangedMeleeConverter RangedMeleeConverter = SystemAPI.GetComponent<RangedMeleeConverter>(grandparentEntity);
                        RangedMeleeConverter.BowEntity = BowEntity;

                        ecb.SetComponent(grandparentEntity, RangedMeleeConverter);
                        ecb.RemoveComponent<BowSetUpEntity>(BowEntity);
                    }
                }
            }
        }
        ecb.Playback(state.EntityManager);
    }
}
}
