using Unity.Entities;

public struct EntitiesReferences : IComponentData {

    public Entity debugEntityPrefab;
    public Entity arrowPrefabEntity;
    public Entity bulletPrefabEntity;
    public Entity cannonBallPrefabEntity;
    public Entity gobberLobberProjectilePrefabEntity;
    public Entity ballistaBoltPrefabEntity;
    public Entity flamingArrowPrefabEntity;
    public Entity throwingAxePrefabEntity;
    public Entity arrowImpactPrefabEntity;
    public Entity debugPlayerUnitPositionPrefab, debugEnemyUnitPositionPrefab;
    public Entity battlefieldBonusPrefabEntity;
    public Entity siegeclawProjectilePrefabEntity;
    public Entity starHurlerProjectilePrefabEntity;

    public Entity basePlayerUnitPrefabEntity;
    public Entity gateUnitPrefabEntity;
    public readonly Entity GetProjectileEntityForUnitName(UnitName _unitName, bool requestingFireProjectile)
    {
        if(requestingFireProjectile) return flamingArrowPrefabEntity;
        
        return _unitName switch
        {
            UnitName.EisenmannRegiment => bulletPrefabEntity,
            UnitName.EmperorsArquebusiers => bulletPrefabEntity,
            UnitName.DrakefireRiflers => bulletPrefabEntity,
            UnitName.Berserkers => throwingAxePrefabEntity,
            UnitName.KaiserCannon => cannonBallPrefabEntity,
            UnitName.BanryuBombardiers => cannonBallPrefabEntity,
            UnitName.LorandelStarhurler => starHurlerProjectilePrefabEntity,
            UnitName.Gobbopult => gobberLobberProjectilePrefabEntity,
            UnitName.DraugrBoltThrowers => ballistaBoltPrefabEntity,
            UnitName.GrimfireGuns => cannonBallPrefabEntity,
            UnitName.StormForgedBattery => cannonBallPrefabEntity,
            UnitName.JoseonHwacha => cannonBallPrefabEntity,
            UnitName.ArchersOfApollo => flamingArrowPrefabEntity,
            UnitName.Siegeclaws => siegeclawProjectilePrefabEntity,
            UnitName.TriceraPlatform => cannonBallPrefabEntity,
            UnitName.Gate => arrowPrefabEntity,
            _ => arrowPrefabEntity
        };
    }
}
