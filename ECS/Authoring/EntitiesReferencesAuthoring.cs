using Unity.Entities;
using UnityEngine;

public class EntitiesReferencesAuthoring : MonoBehaviour {
    public GameObject debugEntityPrefab;
    public GameObject arrowPrefabGameObject, bulletPrefabGameObject, cannonBallPrefabGameObject, throwingAxePrefabGameObject, ballistaBoltPrefabEntity, gobberLobberPrefabEntity, starHurlerPrefabEntity, siegeclawPrefabEntity, flamingArrowPrefabEntity;
    public GameObject arrowImpactPrefabGameObject;
    public GameObject debugPlayerUnitPositionPrefab, debugEnemyUnitPositionPrefab;
    public GameObject battlefieldBonusPrefabGameObject;
    public GameObject basePlayerUnitPrefabGameObject;
    public GameObject gateUnitPrefabGameObject;


    public class Baker : Baker<EntitiesReferencesAuthoring>
    {
        public override void Bake(EntitiesReferencesAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntitiesReferences
            {
                debugEntityPrefab = GetEntity(authoring.debugEntityPrefab, TransformUsageFlags.Dynamic),
                arrowPrefabEntity = GetEntity(authoring.arrowPrefabGameObject, TransformUsageFlags.Dynamic),
                bulletPrefabEntity = GetEntity(authoring.bulletPrefabGameObject, TransformUsageFlags.Dynamic),
                cannonBallPrefabEntity = GetEntity(authoring.cannonBallPrefabGameObject, TransformUsageFlags.Dynamic),
                throwingAxePrefabEntity = GetEntity(authoring.throwingAxePrefabGameObject, TransformUsageFlags.Dynamic),
                ballistaBoltPrefabEntity = GetEntity(authoring.ballistaBoltPrefabEntity, TransformUsageFlags.Dynamic),
                gobberLobberProjectilePrefabEntity = GetEntity(authoring.gobberLobberPrefabEntity, TransformUsageFlags.Dynamic),
                starHurlerProjectilePrefabEntity = GetEntity(authoring.starHurlerPrefabEntity, TransformUsageFlags.Dynamic),
                flamingArrowPrefabEntity = GetEntity(authoring.flamingArrowPrefabEntity, TransformUsageFlags.Dynamic),
                siegeclawProjectilePrefabEntity = GetEntity(authoring.siegeclawPrefabEntity, TransformUsageFlags.Dynamic),

                arrowImpactPrefabEntity = GetEntity(authoring.arrowImpactPrefabGameObject, TransformUsageFlags.Dynamic),
                debugPlayerUnitPositionPrefab = GetEntity(authoring.debugPlayerUnitPositionPrefab, TransformUsageFlags.Dynamic),
                debugEnemyUnitPositionPrefab = GetEntity(authoring.debugEnemyUnitPositionPrefab, TransformUsageFlags.Dynamic),
                battlefieldBonusPrefabEntity = GetEntity(authoring.battlefieldBonusPrefabGameObject, TransformUsageFlags.Dynamic),

                // Units
                basePlayerUnitPrefabEntity = GetEntity(authoring.basePlayerUnitPrefabGameObject, TransformUsageFlags.Dynamic),
                gateUnitPrefabEntity = GetEntity(authoring.gateUnitPrefabGameObject, TransformUsageFlags.Dynamic),

            });
        }
    }
}
