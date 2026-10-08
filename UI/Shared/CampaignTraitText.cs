using Memori.Localization;

namespace TJ
{
    /// <summary>
    /// Each unit's Campaign Trait in words, for the trait chip and its tooltip. The rules themselves live in the
    /// "Campaign Traits" regions of <see cref="TabletopTavernConstants"/> and CampaignSaveManager; the numbers here
    /// are read from the same constants.
    /// </summary>
    public static class CampaignTraitText
    {
        private readonly struct Trait
        {
            public readonly string Id;
            public readonly string[] LineKeys;
            public readonly object[] Args;

            public Trait(string id, string[] lineKeys, params object[] args)
            {
                Id = id;
                LineKeys = lineKeys;
                Args = args;
            }
        }

        public static string Caption => LocalizationManager.Instance.GetText("CampaignTrait");

        public static bool TryGet(UnitName unit, out string name, out string lines)
        {
            name = lines = null;
            Trait? found = Find(unit);
            if (!found.HasValue) return false;

            Trait trait = found.Value;
            name = LocalizationManager.Instance.GetText($"CampaignTrait_{trait.Id}");
            // A unit named in the text shows its localized name.
            object[] args = new object[trait.Args.Length];
            for (int i = 0; i < args.Length; i++)
                args[i] = trait.Args[i] is UnitName named ? LocalizationManager.Instance.GetText(named.ToString()) : trait.Args[i];
            string[] parts = new string[trait.LineKeys.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                string line = LocalizationManager.Instance.GetText(trait.LineKeys[i]);
                if (args.Length > 0) line = string.Format(line, args);
                parts[i] = line;
            }
            lines = string.Join("\n", parts);
            return true;
        }

        private static Trait? Find(UnitName unit)
        {
            if (TabletopTavernConstants.IsTrueGoblin(unit))
                return new Trait("UninvitedGuests", new[] { "GoblinRuleReserve" });
            if (TabletopTavernConstants.IsRestlessDead(unit))
                return Single("RestlessDead", UnityEngine.Mathf.RoundToInt(TabletopTavernConstants.RESTLESS_DEAD_RISE_HEALTH * 100f));
            return unit switch
            {
                UnitName.ThrallLevy => Single("EarnTheirFreedom", UnitName.Huskarls),
                UnitName.RoninWanderers => Single("MasterlessBlade", TabletopTavernConstants.MASTERLESS_BLADE_KILLS),
                UnitName.Treants => Single("SlowGrowth"),
                UnitName.ForestSpirits => Single("ReturnToTheGrove"),
                UnitName.RiftpickLaborers => Single("Prospectors", TabletopTavernConstants.PROSPECTORS_GOLD_PER_SQUAD, TabletopTavernConstants.PROSPECTORS_MAX_SQUADS),
                UnitName.BogmawTroll => Single("Regeneration", UnityEngine.Mathf.RoundToInt(TabletopTavernConstants.REGENERATION_HEAL_AMOUNT * 100f)),
                UnitName.RoyalCavaliers => Single("NoblePurse", TabletopTavernConstants.NOBLE_PURSE_GOLD_PER_SQUAD),
                UnitName.FieldPikemen => Single("GarrisonDuty"),
                UnitName.FleshshredderFanatics => Single("Feast", TabletopTavernConstants.FEAST_KILLS, UnityEngine.Mathf.RoundToInt(TabletopTavernConstants.FEAST_HEAL_AMOUNT * 100f)),
                UnitName.FeralHounds => Single("Scavengers", TabletopTavernConstants.SCAVENGERS_GOLD_PER_ENEMY_SQUAD),
                UnitName.CorpseClaws => Single("GraveRobbers", TabletopTavernConstants.GRAVE_ROBBERS_CHANCE),
                UnitName.BlackWardens => Single("CryptKeepers", UnityEngine.Mathf.RoundToInt(TabletopTavernConstants.CRYPT_KEEPERS_RISE_HEALTH * 100f)),
                UnitName.GoldenSaru => Single("LuckyCharm"),
                _ => null,
            };
        }

        private static Trait Single(string id, params object[] args) =>
            new(id, new[] { $"CampaignTrait_{id}Desc" }, args);
    }
}
