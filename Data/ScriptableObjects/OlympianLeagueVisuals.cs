using System;
using Memori.Audio;
using UnityEngine;

/// <summary>
/// What each Olympian League blessing looks and sounds like on the field. One asset, loaded from Resources by EntityWatcher.
/// </summary>
[CreateAssetMenu(fileName = "OlympianLeagueVisuals", menuName = "GameData/Olympian League Visuals", order = 3)]
public class OlympianLeagueVisuals : ScriptableObject
{
    [Serializable]
    public struct Moment
    {
        public OlympianVisual kind;
        public GameObject prefab;
        public SFXCue cue;
        // Multiplies the prefab's own scale; 0 keeps it.
        public float scale;
        public float lifetime;
        // Camera shake force before distance falloff; 0 for none.
        public float shake;
    }

    public Moment[] moments = Array.Empty<Moment>();

    public bool TryGet(OlympianVisual kind, out Moment moment)
    {
        for (int i = 0; i < moments.Length; i++)
        {
            if (moments[i].kind != kind) continue;
            moment = moments[i];
            return true;
        }
        moment = default;
        return false;
    }
}
