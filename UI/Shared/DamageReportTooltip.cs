using System.Collections.Generic;
using Memori.Localization;
using Memori.Tooltip;
using UnityEngine;

namespace TJ
{
    /// <summary>One squad's line in the Damage dealt report. Kills or Lost of -1 means the battle did not keep it.</summary>
    public struct DamageReportRow
    {
        public UnitName Unit;
        public int Damage;
        public int Kills;
        public int Lost;
    }

    /// <summary>The Damage dealt panel shared by the map's Engagement panel and the battle's end screen.</summary>
    public static class DamageReportTooltip
    {
        // One table is 600 wide; the enemy's sits beside it with a 24 gap.
        public const float SingleWidth = 600f;
        public const float BothWidth = 1224f;

        /// <summary>Your squads, and the enemy's beside them when given; each list sorted by damage by the caller.</summary>
        public static TooltipContent Build(IReadOnlyList<DamageReportRow> yours, IReadOnlyList<DamageReportRow> enemy, string subtitle,
            Sprite icon, Race race, int slain, int troopsLost, int squadsLost)
        {
            bool bothSides = enemy != null && enemy.Count > 0;
            var content = new TooltipContent
            {
                Title = Text("engagementDamageTitle"),
                Subtitle = bothSides ? subtitle : Text("engagementDamageSub"),
                Icon = icon,
                IconColor = ParseColour("#E9C06A"),
                Accent = ColorData.GetRaceDisplayColor(race),
                Detail = Text("engagementDamageFooter"),
                Width = bothSides ? BothWidth : SingleWidth,
                RowCaptions = new[] { Text(bothSides ? "engagementYourArmy" : "engagementColSquad"), Text("engagementDamageTitle"), Text("engagementColKills"), Text("engagementColLost") },
                SideRowCaptions = new[] { Text("engagementEnemyHost"), Text("engagementDamageTitle"), Text("engagementColKills"), Text("engagementColLost") },
            };
            int total = 0, top = 1;
            foreach (DamageReportRow row in yours)
            {
                total += row.Damage;
                top = Mathf.Max(top, row.Damage);
            }
            // One scale for both armies, so the bars compare across the two tables.
            if (bothSides) foreach (DamageReportRow row in enemy) top = Mathf.Max(top, row.Damage);
            content.Stats.Add(new TooltipStat { Value = total.ToString("N0"), Label = Text("engagementTotalDamage"), IconColor = Color.white });
            content.Stats.Add(new TooltipStat { Value = slain.ToString("N0"), Label = Text("engagementCellSlain"), IconColor = Color.white });
            content.Stats.Add(new TooltipStat { Value = troopsLost.ToString("N0"), Label = Text("engagementTroopsLost"), Warn = troopsLost > 0, IconColor = Color.white });
            content.Stats.Add(new TooltipStat { Value = squadsLost == 0 ? Text("engagementNone") : squadsLost.ToString(), Label = Text("engagementCellSquadsLost"), Warn = squadsLost > 0, IconColor = Color.white });

            string negative = ColorData.Negative;
            foreach (DamageReportRow row in yours)
            {
                content.Rows.Add(new TooltipRow
                {
                    Icon = TabletopTavernData.Instance.GetUnitIcon(row.Unit),
                    Label = Text(row.Unit.ToString()),
                    Bar = (float)row.Damage / top,
                    Value = row.Damage.ToString("N0"),
                    ColumnA = row.Kills.ToString(),
                    ColumnB = LostText(row.Lost, negative),
                });
            }
            if (bothSides)
                foreach (DamageReportRow row in enemy)
                {
                    content.SideRows.Add(new TooltipRow
                    {
                        Icon = TabletopTavernData.Instance.GetUnitIcon(row.Unit),
                        Label = Text(row.Unit.ToString()),
                        Bar = (float)row.Damage / top,
                        Value = row.Damage.ToString("N0"),
                        ColumnA = row.Kills < 0 ? "-" : row.Kills.ToString(),
                        ColumnB = row.Lost < 0 ? "-" : LostText(row.Lost, negative),
                    });
                }
            return content;
        }

        // Troops lost read as a plain count in red, on both sides; the column caption already says Lost.
        private static string LostText(int lost, string negative) => lost > 0 ? $"<color={negative}>{lost}</color>" : "0";

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);
        private static Color ParseColour(string hex) => ColorUtility.TryParseHtmlString(hex, out Color colour) ? colour : Color.white;
    }
}
