using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;

namespace TJ
{
    /// <summary>
    /// Turns [Id] and [Id|shown text] keyword tags in localized text into coloured TMP text, and
    /// signed numbers into green or red. The one place tagged text is drawn.
    /// </summary>
    public static class KeywordText
    {
        // No colon in the prefix: several screens split "Name: effect" lines at the first colon.
        public const string LinkPrefix = "kw.";
        // The tooltip's title gold, so the keyword list reads as part of the tooltip.
        const string GlossaryNameColour = "#E9C06A";
        const int GlossaryCap = 3;

        // A TMP tag is copied untouched; a bracket pair is a keyword candidate.
        static readonly Regex Token = new(@"<[^>]*>|\[([^\[\]<>{}\r\n]{1,60})\]", RegexOptions.Compiled);
        static readonly Regex SignedNumber = new(@"(?<![\w.])[+\-−]\d+(?:[.,]\d+)?%?", RegexOptions.Compiled);

        /// <summary>
        /// Renders tags and signed numbers. Hoverable keywords get a link and a faint underline; pass
        /// false for text inside a tooltip or on an element that has its own tooltip.
        /// </summary>
        public static string Render(string text, bool hoverable = true, List<Keyword> found = null)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;

            var builder = new StringBuilder(text.Length + 96);
            int last = 0;
            foreach (Match match in Token.Matches(text))
            {
                AppendPlain(builder, text, last, match.Index - last);
                if (match.Groups[1].Success) AppendKeyword(builder, match.Value, match.Groups[1].Value, hoverable, found);
                else builder.Append(match.Value);
                last = match.Index + match.Length;
            }
            AppendPlain(builder, text, last, text.Length - last);
            return builder.ToString();
        }

        /// <summary>Tooltip text: rendered without hover cues, then a short list defining the traits, classes and conditions it names.</summary>
        public static string ForTooltip(string text, string selfId = null)
        {
            var found = new List<Keyword>();
            string body = Render(text, false, found);
            string glossary = Glossary(found, selfId);
            return glossary.Length == 0 ? body : $"{body}\n\n{glossary}";
        }

        /// <summary>Renders hoverable text into a panel label and wires the hover.</summary>
        public static void Apply(TMP_Text target, string text) => Show(target, Render(text));

        /// <summary>Sets already rendered text; a label holding keyword links gets hover and a raycast target.</summary>
        public static void Show(TMP_Text target, string rendered)
        {
            target.text = rendered;
            if (string.IsNullOrEmpty(rendered) || rendered.IndexOf("<link=\"" + LinkPrefix, System.StringComparison.Ordinal) < 0) return;
            if (!target.TryGetComponent(out KeywordLinkHover _)) target.gameObject.AddComponent<KeywordLinkHover>();
            target.raycastTarget = true;
        }

        #region Rendering

        static void AppendKeyword(StringBuilder builder, string raw, string content, bool hoverable, List<Keyword> found)
        {
            string id = content;
            string shown = null;
            int pipe = content.IndexOf('|');
            if (pipe >= 0)
            {
                id = content.Substring(0, pipe).Trim();
                shown = content.Substring(pipe + 1);
            }

            if (!KeywordRegistry.TryGet(id, out Keyword keyword))
            {
                // Text written before tags used ids, including mods, names the keyword in its own language.
                if (pipe < 0 && KeywordRegistry.TryGetByName(content, out keyword)) shown = content;
                else
                {
                    builder.Append(pipe >= 0 ? shown : raw);
                    return;
                }
            }

            shown ??= keyword.Name;
            if (found != null && !found.Contains(keyword)) found.Add(keyword);

            string colour = keyword.Colour;
            bool link = hoverable && keyword.HasDescription;
            if (link) builder.Append("<link=\"").Append(LinkPrefix).Append(keyword.Id).Append("\">");
            builder.Append("<color=").Append(colour).Append('>');
            if (link) builder.Append("<u color=").Append(colour).Append("66>");
            builder.Append(shown);
            if (link) builder.Append("</u>");
            builder.Append("</color>");
            if (link) builder.Append("</link>");
        }

        static void AppendPlain(StringBuilder builder, string text, int start, int length)
        {
            if (length <= 0) return;
            string part = text.Substring(start, length);
            int last = 0;
            foreach (Match match in SignedNumber.Matches(part))
            {
                builder.Append(part, last, match.Index - last);
                string colour = match.Value[0] == '+' ? ColorData.Green : ColorData.Error;
                builder.Append("<color=").Append(colour).Append('>').Append(match.Value).Append("</color>");
                last = match.Index + match.Length;
            }
            builder.Append(part, last, part.Length - last);
        }

        static string Glossary(List<Keyword> found, string selfId)
        {
            var builder = new StringBuilder();
            int count = 0;
            foreach (Keyword keyword in found)
            {
                if (count == GlossaryCap) break;
                if (!InGlossary(keyword) || keyword.Id == selfId) continue;
                if (count > 0) builder.Append('\n');
                builder.Append("<size=90%><color=").Append(GlossaryNameColour).Append('>').Append(keyword.Name).Append("</color>: ")
                       .Append(Render(keyword.Description, false)).Append("</size>");
                count++;
            }
            return builder.ToString();
        }

        // Stats already have their own rows and tooltips; rarities have no definition.
        static bool InGlossary(Keyword keyword) => keyword.HasDescription &&
            (keyword.Kind == KeywordKind.Trait || keyword.Kind == KeywordKind.UnitClass || keyword.Kind == KeywordKind.Condition);

        #endregion
    }
}
