using System;
using System.Collections.Generic;
using Memori.Localization;

namespace TJ
{
    public enum KeywordKind { Stat, Trait, UnitClass, Condition, Rarity }

    /// <summary>A term that localized text can tag as [Id] or [Id|shown text].</summary>
    public sealed class Keyword
    {
        public readonly string Id;
        public readonly KeywordKind Kind;
        public readonly string NameKey;
        /// <summary>Null for a keyword that is only coloured, never explained (rarity).</summary>
        public readonly string DescKey;

        public Keyword(string id, KeywordKind kind, string nameKey = null, bool explained = true)
        {
            Id = id;
            Kind = kind;
            NameKey = nameKey ?? id;
            DescKey = explained ? id + "Desc" : null;
        }

        public bool HasDescription => DescKey != null;
        public string Name => LocalizationManager.Instance.GetText(NameKey);
        public string Description => HasDescription ? LocalizationManager.Instance.GetText(DescKey) : string.Empty;

        public string Colour => Kind == KeywordKind.Rarity && Enum.TryParse(Id, out UnitRarity rarity)
            ? ColorData.GetRarityTierColorString(rarity)
            : ColorData.UnitStat;
    }

    /// <summary>
    /// Every taggable term. Stats, traits, conditions and rarities come from their enums, so a new
    /// enum member is taggable at once and KeywordMarkupTests demands its name and description keys.
    /// </summary>
    public static class KeywordRegistry
    {
        // Hero-rule stats with no card row and no name key.
        static readonly HashSet<UnitStat> UntaggedStats = new()
        {
            UnitStat.None, UnitStat.AttackCooldown, UnitStat.ChargeCount, UnitStat.RateOfFire,
            UnitStat.ExplosionDamage, UnitStat.ExplosionRange, UnitStat.ExplosionForce, UnitStat.BaseUnitCount,
        };

        // Commented out of play in TabletopTavernData.GetUnitAttributesForDisplay; it has no name key.
        static readonly HashSet<UnitAttribute> UntaggedTraits = new() { UnitAttribute.None, UnitAttribute.TowerShields };

        // One keyword, two words in the text: "cause Terror" reads better than "cause Terrifying".
        static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Terror", nameof(UnitAttribute.Terrifying) },
        };

        static readonly Dictionary<string, Keyword> ById = Build();

        static Dictionary<string, Keyword> _byName;
        static bool _hooked;

        public static IEnumerable<Keyword> All => ById.Values;

        /// <summary>Finds a keyword by id or alias.</summary>
        public static bool TryGet(string id, out Keyword keyword)
        {
            if (ById.TryGetValue(id, out keyword)) return true;
            return Aliases.TryGetValue(id, out string target) && ById.TryGetValue(target, out keyword);
        }

        /// <summary>Finds a keyword by its name in the current locale, for text written before tags used ids.</summary>
        public static bool TryGetByName(string name, out Keyword keyword)
        {
            if (!_hooked)
            {
                _hooked = true;
                LocalizationManager.Instance.OnLocalizedStringsLoaded += () => _byName = null;
            }
            if (_byName == null)
            {
                _byName = new Dictionary<string, Keyword>(StringComparer.OrdinalIgnoreCase);
                foreach (Keyword each in ById.Values)
                {
                    string shown = each.Name;
                    if (!string.IsNullOrEmpty(shown) && !_byName.ContainsKey(shown)) _byName.Add(shown, each);
                }
            }
            return _byName.TryGetValue(name.Trim(), out keyword);
        }

        static Dictionary<string, Keyword> Build()
        {
            var all = new Dictionary<string, Keyword>(StringComparer.OrdinalIgnoreCase);
            void Add(Keyword keyword) => all[keyword.Id] = keyword;

            foreach (UnitStat stat in Enum.GetValues(typeof(UnitStat)))
                if (!UntaggedStats.Contains(stat)) Add(new Keyword(stat.ToString(), KeywordKind.Stat));
            Add(new Keyword("Morale", KeywordKind.Stat));

            foreach (UnitAttribute trait in Enum.GetValues(typeof(UnitAttribute)))
                if (!UntaggedTraits.Contains(trait)) Add(new Keyword(trait.ToString(), KeywordKind.Trait));

            foreach (UnitCondition condition in Enum.GetValues(typeof(UnitCondition)))
                if (condition != UnitCondition.None) Add(new Keyword(condition.ToString(), KeywordKind.Condition));

            foreach (UnitRarity rarity in Enum.GetValues(typeof(UnitRarity)))
                Add(new Keyword(rarity.ToString(), KeywordKind.Rarity, explained: false));

            Add(new Keyword("Ranged", KeywordKind.UnitClass));
            Add(new Keyword("Melee", KeywordKind.UnitClass));
            Add(new Keyword("Cavalry", KeywordKind.UnitClass));
            Add(new Keyword("Monstrous", KeywordKind.UnitClass));
            // The SingleUnit key reads "Monstrous" on unit cards, so the keyword keeps its own name.
            Add(new Keyword("SingleUnit", KeywordKind.UnitClass, nameKey: "SingleUnitName"));
            Add(new Keyword("Undead", KeywordKind.UnitClass));
            return all;
        }
    }
}
