using System.Collections.Generic;
using Memori.Steamworks;
using UnityEngine;

namespace TJ
{
    /// <summary>
    /// The art for every Steam achievement the Quests panel can show: one row per
    /// <see cref="AchievementId"/> with its unlocked and locked composited icon.
    ///
    /// The sprites are the finished 256px composites exported from the achievement icon rig
    /// (Assets/Art/Icons/Achievements/Composited, copied from the Steam upload folder). Names and
    /// descriptions are not here: they are localization keys derived from the id
    /// (Quest_&lt;Id&gt; and Quest_&lt;Id&gt;_Desc), and completion state is read live from Steam.
    ///
    /// Sync With Registry (context menu) adds a row for every registry entry and fills the sprites
    /// by API name, so a new achievement is a registry entry, two exported icons and one click.
    /// QuestCatalogTests asserts every live achievement resolves to both sprites.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestCatalog", menuName = "GameData/Quest Catalog", order = 4)]
    public class QuestCatalogSO : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public AchievementId id;
            public Sprite unlocked;
            public Sprite locked;
        }

        [SerializeField] private Entry[] entries;

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGet(AchievementId id, out Entry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].id != id) continue;
                    entry = entries[i];
                    return true;
                }
            }

            entry = default;
            return false;
        }

#if UNITY_EDITOR
        public const string CompositedFolder = "Assets/Art/Icons/Achievements/Composited";

        // Adds a row for every registry entry that has none, then fills any empty sprite slot from the
        // composited folder by API name. Never removes or reorders rows. Returns how many rows changed.
        [ContextMenu("Sync With Registry")]
        public void SyncWithRegistry()
        {
            var list = entries != null ? new List<Entry>(entries) : new List<Entry>();
            int changed = 0;

            foreach (AchievementDefinition def in AchievementRegistry.All)
            {
                int index = list.FindIndex(e => e.id == def.Id);
                if (index < 0)
                {
                    list.Add(new Entry { id = def.Id });
                    index = list.Count - 1;
                }

                Entry entry = list[index];
                bool touched = false;
                if (entry.unlocked == null)
                {
                    entry.unlocked = LoadSprite(def.ApiName);
                    touched |= entry.unlocked != null;
                }
                if (entry.locked == null)
                {
                    entry.locked = LoadSprite(def.ApiName + "_locked");
                    touched |= entry.locked != null;
                }
                if (touched) changed++;
                list[index] = entry;
            }

            entries = list.ToArray();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[QuestCatalog] Synced: {entries.Length} rows, {changed} rows received sprites.");
        }

        private static Sprite LoadSprite(string fileName)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"{CompositedFolder}/{fileName}.png");
        }
#endif
    }
}
