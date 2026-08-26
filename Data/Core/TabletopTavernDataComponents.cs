using UnityEngine;
using UnityEngine.AddressableAssets;

[System.Serializable]
public struct SquadAssets
{
    public Race race;
    public Sprite unitIcon;
    public SquadIcon squadIcon;
    public AssetReferenceGameObject unitRecruitmentPrefab;
    public VoiceSFX voiceSFX;
    public MeleeAttackSFX meleeAttackSFX;
    public FireProjectileSFX fireProjectileSFX;
    public FormationDiscipline formationDiscipline;
    public AssetReferenceGameObject ArtilleryCrewPrefab;

    // UnitType.Mage only. The spell this unit casts, read by EntityWatcher when the squad spawns.
    // Kept on the unit rather than in a separate registry because "which spell does a Bishop cast"
    // is a property of the unit, and this way it is authored in the same inspector as its stats.
    // Null on every non-mage unit, and a mage with none logs an error at spawn instead of failing
    // silently at cast time.
    public TJ.Spells.SpellData mageSpell;
}
