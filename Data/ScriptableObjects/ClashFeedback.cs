using Memori.Audio;
using UnityEngine;

/// <summary>
/// Sounds and feel values for the moment two squads meet in melee. One asset, loaded from Resources by EntityWatcher.
/// </summary>
[CreateAssetMenu(fileName = "ClashFeedback", menuName = "GameData/Clash Feedback", order = 2)]
public class ClashFeedback : ScriptableObject
{
    [Header("Impact sounds, by the charging squad's size")]
    public SFXCue impactInfantry;
    public SFXCue impactCavalry;
    public SFXCue impactMonster;
    [Header("A charge stopped by spears from the front")]
    public SFXCue impactBlocked;
    [Header("Two squads meeting with no charge")]
    public SFXCue contact;

    [Header("Loudness")]
    // Model count at which an impact plays at full volume; smaller squads scale down to minCountVolume.
    public int fullVolumeCount = 48;
    [Range(0f, 1f)] public float minCountVolume = 0.55f;
    // A second impact this close in time and space plays quieter so a wide front does not stack into noise.
    public float repeatWindowSeconds = 0.5f;
    public float repeatRadius = 30f;
    [Range(0f, 1f)] public float repeatVolume = 0.5f;

    [Header("Music dip on a landed charge near the camera")]
    [Range(0f, 1f)] public float musicDipDepth = 0.35f;
    public float musicDipSeconds = 0.6f;
    public float musicDipRange = 90f;

    [Header("Dust line at contact")]
    public float dustSpacing = 2.5f;
    public int dustMax = 10;

    public SFXCue ImpactFor(ChargeImpactKind kind, UnitSize size)
    {
        if (kind == ChargeImpactKind.Contact) return contact;
        if (kind == ChargeImpactKind.Blocked) return impactBlocked;
        return size switch
        {
            UnitSize.Cavalry => impactCavalry,
            UnitSize.Monstrous => impactMonster,
            UnitSize.SingleUnit => impactMonster,
            _ => impactInfantry,
        };
    }

    // Shake force before distance falloff: a mob of rabble is a tremor, heavy cavalry or a monster is a jolt.
    public static float ShakeForce(UnitSize size, int count)
    {
        return size switch
        {
            UnitSize.Cavalry => Mathf.Lerp(0.45f, 0.85f, count / 24f),
            UnitSize.Monstrous => Mathf.Lerp(0.6f, 0.95f, count / 8f),
            UnitSize.SingleUnit => 1f,
            _ => Mathf.Lerp(0.2f, 0.5f, count / 48f),
        };
    }
}
