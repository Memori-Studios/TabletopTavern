using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resolves an icon name to its sprite through the <see cref="TJ.IconLibrarySO"/> in Resources.
/// Keys are gear names, enum ToString() values and a handful of literals - see the library asset.
/// </summary>
public static class SpriteData
{
    public const string ResourcePath = "IconData/IconLibrary";

    private static Dictionary<string, Sprite> _icons;

    public static Sprite GetSprite(string _iconName)
    {
        _icons ??= Load();
        if (_icons.TryGetValue(_iconName, out Sprite sprite) && sprite != null) return sprite;
        Debug.LogError($"[SpriteData] No icon registered for '{_iconName}' in Resources/{ResourcePath}");
        return null;
    }

    private static Dictionary<string, Sprite> Load()
    {
        var icons = new Dictionary<string, Sprite>();
        TJ.IconLibrarySO library = Resources.Load<TJ.IconLibrarySO>(ResourcePath);
        if (library == null)
        {
            Debug.LogError($"[SpriteData] No IconLibrarySO found at Resources/{ResourcePath}. Every named icon will be missing.");
            return icons;
        }
        foreach (TJ.IconLibrarySO.Entry entry in library.Entries)
        {
            if (string.IsNullOrEmpty(entry.key)) continue;
            if (icons.ContainsKey(entry.key))
            {
                Debug.LogError($"[SpriteData] Icon key '{entry.key}' is registered twice in {library.name}. Keeping the first.");
                continue;
            }
            icons.Add(entry.key, entry.sprite);
        }
        return icons;
    }

    /// <summary>Drops the cached lookup so the next call re-reads the asset. Tests use this after editing the library.</summary>
    internal static void ResetCache() => _icons = null;
}
