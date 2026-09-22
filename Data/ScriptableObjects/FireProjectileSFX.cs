using UnityEngine;

[CreateAssetMenu(fileName = "FireProjectileSFX", menuName = "GameData/FireProjectileSFX", order = 1)]
public class FireProjectileSFX : ScriptableObject
{
    public AudioClip[] fireProjectileSFX;
    // Played where the shot lands. Empty means the hit is silent (guns have no pack clip).
    public AudioClip[] hitSFX;
}