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
        // The worth of the enemy troops that damage destroyed, in unit value points.
        public float Value;
        public int Kills;
        public int Lost;
        // No troops left; only your army's rows are marked.
        public bool Fallen;
    }

    /// <summary>The Damage dealt panel shared by the map's Engagement panel and the battle's end screen.</summary>
    public static class DamageReportTooltip
    {
        // One table is 600 wide; the enemy's sits beside it with a 24 gap.
        public const float SingleWidth = 600f;
        public const float BothWidth = 1224f;
        // Unit value points are shown times ten as whole numbers, so a small contribution does not round away to nothing.
        private const float ValueScale = 10f;

        /// <summary>The order both screens list squads in: most value first, damage breaking ties (a battle saved before value existed has none).</summary>
        public static int ByValue(DamageReportRow a, DamageReportRow b)
        {
            int order = b.Value.CompareTo(a.Value);
            return order != 0 ? order : b.Damage.CompareTo(a.Damage);
        }

        /// <summary>
        /// Your squads, and the enemy's beside them when given; each list sorted by the caller with ByValue.
        /// A pinned panel explains Value from the ? beside its caption. A hover tooltip cannot be pointed at, so it carries a line instead.
        /// </summary>
        public static TooltipContent Build(IReadOnlyList<DamageReportRow> yours, IReadOnlyList<DamageReportRow> enemy, string subtitle,
            Sprite icon, Race race, int slain, int troopsLost, int squadsLost, bool pinned = false)
        {
            bool bothSides = enemy != null && enemy.Count > 0;
            int total = 0;
            float topValue = 0f;
            int topDamage = 1;
            foreach (DamageReportRow row in yours)
            {
                total += row.Damage;
                topValue = Mathf.Max(topValue, row.Value);
                topDamage = Mathf.Max(topDamage, row.Damage);
            }
            // One scale for both armies, so the bars compare across the two tables.
            if (bothSides)
                foreach (DamageReportRow row in enemy)
                {
                    topValue = Mathf.Max(topValue, row.Value);
                    topDamage = Mathf.Max(topDamage, row.Damage);
                }
            // A battle saved before value existed has none: its bars fall back to damage and the value column stays empty.
            bool hasValue = topValue > 0f;

            var content = new TooltipContent
            {
                Title = Text("engagementDamageTitle"),
                Subtitle = bothSides ? subtitle : Text("engagementDamageSub"),
                Icon = icon,
                IconColor = ParseColour("#E9C06A"),
                Accent = ColorData.GetRaceDisplayColor(race),
                Width = bothSides ? BothWidth : SingleWidth,
                FallenColor = ParseColour(ColorData.Negative),
                Detail = hasValue && !pinned ? Text("engagementValueNote") : "",
                CaptionHelp = hasValue ? new[] { null, Text("engagementValueHelp") } : null,
                RowCaptions = new[] { Text(bothSides ? "engagementYourArmy" : "engagementColSquad"), Text("engagementColValue"), Text("engagementColDamage"), Text("engagementColKills"), Text("engagementColLost") },
                SideRowCaptions = new[] { Text("engagementEnemyHost"), Text("engagementColValue"), Text("engagementColDamage"), Text("engagementColKills"), Text("engagementColLost") },
            };
            content.Stats.Add(new TooltipStat { Value = total.ToString("N0"), Label = Text("engagementTotalDamage"), IconColor = Color.white });
            content.Stats.Add(new TooltipStat { Value = slain.ToString("N0"), Label = Text("engagementCellSlain"), IconColor = Color.white });
            content.Stats.Add(new TooltipStat { Value = troopsLost.ToString("N0"), Label = Text("engagementTroopsLost"), Warn = troopsLost > 0, IconColor = Color.white });
            content.Stats.Add(new TooltipStat { Value = squadsLost == 0 ? Text("engagementNone") : squadsLost.ToString(), Label = Text("engagementCellSquadsLost"), Warn = squadsLost > 0, IconColor = Color.white });

            string negative = ColorData.Negative;
            TooltipRow Line(DamageReportRow row, bool yoursSide) => new TooltipRow
            {
                Icon = TabletopTavernData.Instance.GetUnitIcon(row.Unit),
                Label = Text(row.Unit.ToString()),
                Bar = hasValue ? row.Value / topValue : (float)row.Damage / topDamage,
                Value = hasValue ? Mathf.RoundToInt(row.Value * ValueScale).ToString("N0") : "",
                SecondValue = row.Damage.ToString("N0"),
                ColumnA = row.Kills < 0 ? "-" : row.Kills.ToString(),
                ColumnB = row.Lost < 0 ? "-" : LostText(row.Lost, negative),
                Fallen = yoursSide && row.Fallen,
            };
            foreach (DamageReportRow row in yours) content.Rows.Add(Line(row, true));
            if (bothSides)
                foreach (DamageReportRow row in enemy) content.SideRows.Add(Line(row, false));
            return content;
        }

        // Troops lost read as a plain count in red, on both sides; the column caption already says Lost.
        private static string LostText(int lost, string negative) => lost > 0 ? $"<color={negative}>{lost}</color>" : "0";

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);
        private static Color ParseColour(string hex) => ColorUtility.TryParseHtmlString(hex, out Color colour) ? colour : Color.white;
    }
}
