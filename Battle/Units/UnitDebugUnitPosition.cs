using UnityEngine;
using Unity.Entities;
using Unity.Transforms;
using TMPro;
using Shapes;
using Unity.Mathematics;

public class UnitDebugUnitPosition : MonoBehaviour
{
    public Entity EntityToFollow;
    public EntityManager EntityManager;
    [SerializeField] private TMP_Text indexText, supportingIndexText;
    [SerializeField] private Line line;
    [SerializeField] private Color canReachColor, cantReachColor;
    [SerializeField] private Canvas canvas;


    void Update()
    {
        if (EntityManager.Exists(EntityToFollow)){
            if(EntityManager.HasComponent<UnitPosition>(EntityToFollow)){
                float3 position = EntityManager.GetComponentData<LocalTransform>(EntityToFollow).Position;
                UnitPosition up = EntityManager.GetComponentData<UnitPosition>(EntityToFollow);
                // indexText.text = up.unitIndex.ToString(); 
                // if(EntityManager.Exists(up.supportingEntity) && EntityManager.HasComponent<UnitPosition>(up.supportingEntity)){
                //     UnitPosition supportingUp = EntityManager.GetComponentData<UnitPosition>(up.supportingEntity);
                //     supportingIndexText.text = $"({supportingUp.unitIndex})";
                // } else {
                //     supportingIndexText.text = "";
                // }

                indexText.text = EntityToFollow.Index.ToString();
                supportingIndexText.text = "";


                transform.position = position;
                Target target = EntityManager.GetComponentData<Target>(EntityToFollow);
                if(target.targetEntity != Entity.Null){
                    if(!EntityManager.Exists(target.targetEntity) || !EntityManager.HasComponent<LocalTransform>(target.targetEntity)){
                        return;
                    }
                    // Debug.Log($"Drawing line from {EntityToFollow.Index} to {target.targetEntity.Index}");
                    float3 targetPosition = EntityManager.GetComponentData<LocalTransform>(target.targetEntity).Position;
                    line.Start = new Vector3(0, 0, 0);
                    line.End = targetPosition - position;
                    // Same range rule as MeleeUnitCombatJob: both radii plus the attacker's reach.
                    float meleeAttackDistance = EntityManager.GetComponentData<ProjectDawn.Navigation.AgentShape>(EntityToFollow).Radius
                        + EntityManager.GetComponentData<ProjectDawn.Navigation.AgentShape>(target.targetEntity).Radius
                        + EntityManager.GetComponentData<UnitCollisionBody>(EntityToFollow).Reach;

                    bool isCloseEnoughToAttack = math.distance(position, targetPosition) < meleeAttackDistance;
                    line.Color = isCloseEnoughToAttack ? canReachColor : cantReachColor;

                } else {
                    Team team = EntityManager.GetComponentData<Unit>(EntityToFollow).Team;
                    line.Start = new Vector3(0, 0, 0);
                    // float3 targetPosition = EntityManager.GetComponentData<SetDestination>(EntityToFollow).destinationPosition;
                    //get squadcenter position
                    Unit unit = EntityManager.GetComponentData<Unit>(EntityToFollow);
                    Entity squadEntity = unit.squadEntity;
                    if(EntityManager.HasComponent<SquadMovementComponent>(squadEntity) == false){
                        return;
                    }
                    SquadMovementComponent SquadMovementComponent = EntityManager.GetComponentData<SquadMovementComponent>(squadEntity);
                    float3 centerPosition = SquadMovementComponent.GoalPosition;
                    float3 targetPosition = EntityManager.GetComponentData<SetDestination>(EntityToFollow).squadPosition - centerPosition + SquadMovementComponent.SquadCenter;
                    line.End = targetPosition - position;
                    // line.Color = team == Team.Player ? GameAssets.PLAYER_TRIANGLE_COLOR : GameAssets.ENEMY_TRIANGLE_COLOR;
                    line.Color = team == Team.Player ? TabletopTavernConstants.PLAYER_TRIANGLE_COLOR : TabletopTavernConstants.ENEMY_TRIANGLE_COLOR;
                    // line.Start = Vector3.zero;
                    // line.End = Vector3.zero;
                }
                //rotate to face camera
                canvas.transform.LookAt(Camera.main.transform);
            } else {
                gameObject.SetActive(false);
            }
        } else {
            gameObject.SetActive(false);
        }
    }

}
