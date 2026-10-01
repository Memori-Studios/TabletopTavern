using System;
using System.Collections.Generic;
using TJ;
using UnityEngine;

/// <summary>
/// Blood splat colour per faction, with per-unit overrides. The splat images are white, so this tint is their whole
/// colour. A colour with alpha 0 leaves no blood.
/// </summary>
[CreateAssetMenu(fileName = "BloodColors", menuName = "GameData/Blood Colors", order = 1)]
public class BloodColors : ScriptableObject
{
    // The red the splat images had baked in; used for explosions, factions missing from the list, and a missing asset.
    public static readonly Color FallbackColor = new Color32(94, 13, 6, 255);

    [Serializable]
    public struct FactionBlood
    {
        public Race Race;
        public Color Color;
    }

    [Serializable]
    public struct UnitBlood
    {
        public UnitName Unit;
        public Color Color;
    }

    public Color DefaultColor = FallbackColor;
    public List<FactionBlood> Factions = new();
    public List<UnitBlood> UnitOverrides = new();

    [NonSerialized] private Dictionary<UnitName, Color> _cache;

    public Color GetColor(UnitName unit)
    {
        _cache ??= new Dictionary<UnitName, Color>();
        if (!_cache.TryGetValue(unit, out Color color))
        {
            color = Resolve(unit);
            _cache[unit] = color;
        }
        return color;
    }

    private Color Resolve(UnitName unit)
    {
        foreach (UnitBlood entry in UnitOverrides)
            if (entry.Unit == unit) return entry.Color;

        if (!TabletopTavernData.Instance.SquadAssetsDictionary.TryGetValue(unit, out SquadAssets assets)) return DefaultColor;
        foreach (FactionBlood entry in Factions)
            if (entry.Race == assets.race) return entry.Color;
        return DefaultColor;
    }

    // An Inspector edit during Play shows on the next splat.
    private void OnValidate() => _cache = null;
}
