using UnityEngine;

[CreateAssetMenu(fileName = "MountSFX", menuName = "GameData/MountSFX", order = 1)]
public class MountSFX : ScriptableObject
{
    // Replaces the squad flag's running loop while the squad moves.
    public AudioClip moveLoop;
    // Played between charge shouts.
    public AudioClip[] calls;
}
