using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using Unity.Mathematics;
using Shapes;
using System.Collections.Generic;
using TJ.Shapes;
using Memori.Utilities;

public class BattlefieldBonusGameObject : MonoBehaviour
{
    [SerializeField] private float range;
    [SerializeField] private BattlefieldBonus battlefieldBonus;
    public BattlefieldBonus BattlefieldBonus => battlefieldBonus;
    [SerializeField] private Disc disc1, disc2;
    [SerializeField] private Color playerColor, enemyColor, neutralColor;
    [SerializeField] private Color playerInnerColor, enemyInnerColor, neutralInnerColor;
    [SerializeField] private Transform colliderTransform;
    Entity entity;

    private void Start()
    {
        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<EntitiesReferences>());
        EntitiesReferences entitiesReferences = query.GetSingleton<EntitiesReferences>();
        entity  = entityManager.Instantiate(entitiesReferences.battlefieldBonusPrefabEntity);
        BattlefieldBonusApplicator bonusApplicator = entityManager.GetComponentData<BattlefieldBonusApplicator>(entity);

        battlefieldBonus.OriginationPoint = transform.position;
        battlefieldBonus.Range = UnityEngine.Random.Range(0.9f, 1.2f) * range;
        battlefieldBonus.Guid = System.Guid.NewGuid(); // Ensure each bonus has a unique Guid

        bonusApplicator.BattlefieldBonus = battlefieldBonus;
        
        entityManager.SetComponentData(entity, bonusApplicator);
        SetDisplayOfBonus(battlefieldBonus);
        query.Dispose();
    }
    private void SetDisplayOfBonus(BattlefieldBonus _battlefieldBonus)
    {
        _bonusTeam = _battlefieldBonus.Team;
        ApplyBonusColors();
        ColorVision.Changed -= ApplyBonusColors;
        ColorVision.Changed += ApplyBonusColors;
        disc1.Radius = battlefieldBonus.Range;
        disc2.Radius = battlefieldBonus.Range;
        if(colliderTransform != null)
            colliderTransform.localScale = new Vector3(battlefieldBonus.Range, battlefieldBonus.Range, 1f);

        if(_battlefieldBonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Rain || _battlefieldBonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Fog || _battlefieldBonus.BattlefieldBonusEnum == BattlefieldBonusEnum.Snow || colliderTransform == null)
        {
            UnityEngine.MeshCollider meshCollider = colliderTransform.GetComponent<UnityEngine.MeshCollider>();
            meshCollider.enabled = false;
        }
    }
    private Team _bonusTeam;

    private void ApplyBonusColors()
    {
        Color color = _bonusTeam switch
        {
            Team.Player => ColorVision.Good(playerColor),
            Team.Enemy => ColorVision.Bad(enemyColor),
            Team.Neutral => neutralColor,
            _ => Color.white
        };
        disc1.GetComponent<ShapesBloom>().Bloom(color);
        color = _bonusTeam switch
        {
            Team.Player => ColorVision.Good(playerInnerColor),
            Team.Enemy => ColorVision.Bad(enemyInnerColor),
            Team.Neutral => neutralInnerColor,
            _ => Color.white
        };
        disc2.ColorOuter = color;
    }
    public void OnDestroy()
    {
        ColorVision.Changed -= ApplyBonusColors;
        if (entity == Entity.Null) return;
        World world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;
        if (!world.EntityManager.Exists(entity)) return;

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        entityManager.DestroyEntity(entity);
    }
}