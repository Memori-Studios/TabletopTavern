using TJ.IrregularGrid;
using UnityEngine;
using Unity.Entities;


public class BattlefieldBiomeDetector : MonoBehaviour
{
    [SerializeField] private BattlefieldBonusEnum detectedBiome = BattlefieldBonusEnum.None;
    // The flag base sits on the ground plane, and a ray that starts on the surface misses it.
    const float RAY_START_HEIGHT = 2f;
    int squadEntityId;
    Entity squadEntity;
    void Update()
    {
        DetectBiome();
    }
    private void DetectBiome()
    {
        //Raycast down and get layer, check for tile or swamp
        if (Physics.Raycast(this.transform.position + Vector3.up * RAY_START_HEIGHT, Vector3.down, out RaycastHit hitInfo, Mathf.Infinity, ~(1 << TabletopTavernConstants.SQUAD_FLAG_LAYER | 1 << TabletopTavernConstants.BATTLEFIELD_BONUS_LAYER)))
        {
            // Debug.Log($"Hit {hitInfo.collider.gameObject.name}, layer: {hitInfo.collider.gameObject.layer}");
            if (hitInfo.collider.gameObject.layer.Equals(TabletopTavernConstants.TILE_LAYER))
            {
                if (detectedBiome == BattlefieldBonusEnum.None) return;
                SwapBiome(BattlefieldBonusEnum.None);
            }
            else if (hitInfo.collider.gameObject.layer.Equals(TabletopTavernConstants.SWAMP_LAYER))
            {
                if (detectedBiome == BattlefieldBonusEnum.Swamp) return;
                SwapBiome(BattlefieldBonusEnum.Swamp);
            }
            else if (hitInfo.collider.gameObject.layer.Equals(TabletopTavernConstants.FOREST_LAYER))
            {
                if (detectedBiome == BattlefieldBonusEnum.Forest) return;
                SwapBiome(BattlefieldBonusEnum.Forest);
            }
        }
    }
    private void SwapBiome(BattlefieldBonusEnum newBiome)
    {
        // Debug.Log($"Swapped BattlefieldBonusEnum from {detectedBiome} to {newBiome}");
        
        //remove old bonus
        if (detectedBiome != BattlefieldBonusEnum.None)
        {
            BattleManager.Instance.BattlefieldBiomeBonus.RemoveBonus(detectedBiome, squadEntity);
        }

        detectedBiome = newBiome;

        //apply new bonus
        if (detectedBiome != BattlefieldBonusEnum.None)
        {
            BattleManager.Instance.BattlefieldBiomeBonus.ApplyBonus(detectedBiome, squadEntity);
        }
    }
    public void SetSquadEntityId(int squadId, Entity entity)
    {
        squadEntity = entity;
        squadEntityId = squadId;
    }
}
