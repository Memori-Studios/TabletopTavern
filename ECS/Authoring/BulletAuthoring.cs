using Memori.Audio;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class BulletAuthoring : MonoBehaviour
{
    public float speed;
    public float arcHeight;
    public int damageAmount;
    public GameObject visualGameObject;
    public bool SmokeExplosionOnStart;
    public bool FlamingAmmo;

    public class Baker : Baker<BulletAuthoring> {
        public override void Bake(BulletAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            //get the localTransform of the first child
            AddComponent(entity, new Bullet
            {
                bulletTrajectoryTransform = GetEntity(authoring.visualGameObject, TransformUsageFlags.Dynamic),
                speed = authoring.speed,
                damageAmount = authoring.damageAmount,
                arcHeight = authoring.arcHeight
            });
            AddComponent(entity, 
                new NewArrowTag { 
                    SmokeExplosionOnStart = authoring.SmokeExplosionOnStart, 
                    FlamingAmmo = authoring.FlamingAmmo 
                }
            );
            // AddComponent<ApplyKnockbackOnContact>(entity);
            // SetComponentEnabled<ApplyKnockbackOnContact>(entity, false);
        }
    }
}

public struct Bullet : IComponentData
{
    public Entity bulletTrajectoryTransform;
    public float3 bulletInitialPosition;
    public float3 bulletTargetPosition;
    public float speed;
    public int damageAmount;
    public float totalDistance;
    public float arcHeight;
    public Team Team;
    public int squadId;
    public DamageAttributes damageAttributes;
    public bool shotIntoFlanks;
    public bool flaming;
    public bool sourceIsArtillery;
    // Picks the hit sound from the shooter's FireProjectileSFX bank when the shot lands.
    public UnitName shooterUnitName;
}
public struct NewArrowTag : IComponentData 
{ 
    public bool SmokeExplosionOnStart;
    public bool FlamingAmmo;
}
