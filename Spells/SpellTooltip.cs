using Memori.Localization;
using Memori.Tooltip;
using UnityEngine;

namespace TJ.Spells
{
    /// <summary>
    /// Builds the one tooltip every spell surface shows, so the hotbar, mage tiles and run setup read
    /// the same. Every number comes off the SpellData asset or the caller's live state.
    /// </summary>
    public static class SpellTooltip
    {
        public struct Context
        {
            // 1-10 shows the spell-menu key caps; anything else shows none.
            public int HotkeyNumber;
            public bool Pinned;
            // Hotbar live state: mana still missing, whole seconds of cooldown left.
            public int ManaShort;
            public int CooldownLeft;

            // Mage tile: charges replace mana, and the caster's own range and cadence apply.
            public bool IsMage;
            public int Charges, MaxCharges;
            public float Range, Cooldown;
            public string CasterLine;
        }

        public static TooltipContent Build(SpellData spell, Context ctx = default)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            string rawDescription = loc.GetText(spell.Spell + "_Desc");
            Color raceColour = ColorData.GetRaceDisplayColor(spell.Race);

            var content = new TooltipContent
            {
                Title = loc.GetText(spell.Spell.ToString()),
                Subtitle = Subtitle(spell, raceColour),
                Icon = spell.SpellSprite,
                IconColor = (Color)ColorData.HexToRgba(ColorData.Primary),
                Accent = raceColour,
                KeyCaps = KeyCaps(ctx.HotkeyNumber),
                Body = Description(spell, rawDescription),
                Detail = ctx.CasterLine ?? "",
                Footer = Footer(ctx, loc),
            };
            AddStats(content, spell, ctx, rawDescription, loc);
            return content;
        }

        private static string Subtitle(SpellData spell, Color raceColour)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(raceColour)}>{SpellRaceLabel.Get(spell.Race)}</color> - {TargetingLine(spell)}";
        }

        /// <summary>"Area of effect - Friendly": the spell's shape and who it hits, without the faction.</summary>
        public static string TargetingLine(SpellData spell)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            string shape = loc.GetText(spell.SpellType == SpellType.AOE ? "SpellTargeting_World" : "SpellTargeting_Squad");
            string targets = spell.TargetTeam switch
            {
                Team.Player => loc.GetText("SpellTargetsFriendly"),
                Team.Enemy => loc.GetText("Enemy"),
                _ => loc.GetText("SpellTargetsAny"),
            };
            return $"{shape} - {targets}";
        }

        private static string[] KeyCaps(int hotkeyNumber)
        {
            if (hotkeyNumber < 1 || hotkeyNumber > 10) return null;
            return new[] { SpellCastButton.GetSpellMenuKeyName(), (hotkeyNumber % 10).ToString() };
        }

        // The numbers are bolded before formatting, so they stand out in every locale without touching the text.
        private static string Description(SpellData spell, string rawDescription)
        {
            if (string.IsNullOrEmpty(rawDescription)) return spell.Spell.ToString();
            string description = string.Format(rawDescription, spell.SpellType,
                $"<b>{spell.SpellModifierValue}</b>", $"<b>{spell.SpellDuration}</b>");
            return KeywordText.ForTooltip(description);
        }

        private static void AddStats(TooltipContent content, SpellData spell, Context ctx, string rawDescription, LocalizationManager loc)
        {
            Color iconColour = (Color)ColorData.GetUnitStatColor(UnitStat.Range);
            string Seconds(float value) => string.Format(loc.GetText("CooldownSeconds"), Mathf.RoundToInt(value));
            void Add(string iconKey, Color colour, string value, string labelKey, bool warn = false)
                => content.Stats.Add(new TooltipStat { Icon = SpriteData.GetSprite(iconKey), IconColor = colour, Value = value, Label = loc.GetText(labelKey), Warn = warn });

            if (ctx.IsMage)
                Add("SpellStatCharges", iconColour, $"{ctx.Charges}/{ctx.MaxCharges}", "SpellStatCharges", ctx.Charges <= 0);
            // Mage spells carry cost 0: they spend charges, and outside the rail there is no charge count to show.
            else if (spell.SpellManaCost > 0)
                Add("Mana",ColorData.ManaCostGem, spell.SpellManaCost.ToString(), "SpellStatMana", ctx.ManaShort > 0);

            // Only a spell whose text names a duration has one worth showing; an instant spell's field is unused.
            if (rawDescription.Contains("{2}") && spell.SpellDuration > 0f)
                Add("SpellStatDuration", iconColour, Seconds(spell.SpellDuration), "SpellStatDuration");

            if (spell.SpellType == SpellType.AOE && spell.SpellRadius > 0f)
                Add("SpellStatArea", iconColour, Mathf.RoundToInt(spell.SpellRadius).ToString(), "SpellStatArea");

            if (ctx.IsMage)
                Add("Range", iconColour, Mathf.RoundToInt(ctx.Range).ToString(), "SpellStatCastRange");

            Add("SpellStatCooldown", iconColour, Seconds(ctx.IsMage ? ctx.Cooldown : spell.SpellCooldown), "SpellStatCooldown");
        }

        private static string Footer(Context ctx, LocalizationManager loc)
        {
            if (ctx.Pinned)
                return $"<color={ColorData.Tier4}>{loc.GetText("SignatureSpellPinned")}</color>";

            string warn = ColorData.Negative;
            string footer = "";
            if (!ctx.IsMage && ctx.ManaShort > 0)
                footer = $"<color={warn}>{string.Format(loc.GetText("SpellNeedsMana"), ctx.ManaShort)}</color>";
            if (ctx.CooldownLeft > 0)
            {
                if (footer.Length > 0) footer += "\n";
                footer += $"<color={warn}>{string.Format(loc.GetText("SpellReadyIn"), ctx.CooldownLeft)}</color>";
            }
            return footer;
        }
    }
}
