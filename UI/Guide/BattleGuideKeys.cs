using System.Collections.Generic;
using Memori.Input;
using Memori.Localization;
using UnityEngine.InputSystem;

namespace TJ
{
    public enum GuideKeyPartKind { Keycap, MouseLeft, MouseRight, MouseMiddle, Plus, Separator, Word }

    public struct GuideKeyPart
    {
        public GuideKeyPartKind kind;
        public string text;

        public GuideKeyPart(GuideKeyPartKind kind, string text = "")
        {
            this.kind = kind;
            this.text = text;
        }
    }

    /// <summary>
    /// Turns a guide row's key string into keycaps, reading the player's current bindings so a rebind shows in the guide.
    /// </summary>
    public static class BattleGuideKeys
    {
        // Used only when no InputHandler exists (edit mode), so the guide still shows the default bindings.
        static GameControls fallbackControls;

        public static List<GuideKeyPart> Parse(string keys)
        {
            var parts = new List<GuideKeyPart>();
            if (string.IsNullOrWhiteSpace(keys)) return parts;

            foreach (string token in keys.Split(' ', System.StringSplitOptions.RemoveEmptyEntries))
            {
                switch (token)
                {
                    case "+": parts.Add(new GuideKeyPart(GuideKeyPartKind.Plus, "+")); break;
                    case "/": parts.Add(new GuideKeyPart(GuideKeyPartKind.Separator, "/")); break;
                    case "-": parts.Add(new GuideKeyPart(GuideKeyPartKind.Separator, "-")); break;
                    case "LMB": parts.Add(new GuideKeyPart(GuideKeyPartKind.MouseLeft)); break;
                    case "RMB": parts.Add(new GuideKeyPart(GuideKeyPartKind.MouseRight)); break;
                    case "MMB": parts.Add(new GuideKeyPart(GuideKeyPartKind.MouseMiddle)); break;
                    default:
                        if (token.StartsWith("@")) AddAction(parts, token.Substring(1));
                        else if (token.StartsWith("~")) parts.Add(new GuideKeyPart(GuideKeyPartKind.Word, Localize(token.Substring(1))));
                        else parts.Add(new GuideKeyPart(GuideKeyPartKind.Keycap, token));
                        break;
                }
            }
            return parts;
        }

        static string Localize(string key)
        {
            LocalizationManager localization = LocalizationManager.InstanceIfExists;
            return localization != null ? localization.GetText(key) : key;
        }

        static GameControls Controls()
        {
            InputHandler handler = InputHandler.InstanceIfExists;
            if (handler != null && handler.GameControls != null) return handler.GameControls;
            fallbackControls ??= new GameControls();
            return fallbackControls;
        }

        static void AddAction(List<GuideKeyPart> parts, string actionName)
        {
            InputAction action = Controls().FindAction(actionName);
            if (action == null)
            {
                UnityEngine.Debug.LogError($"BattleGuideKeys: no input action named '{actionName}'.");
                parts.Add(new GuideKeyPart(GuideKeyPartKind.Keycap, actionName));
                return;
            }

            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                InputBinding binding = bindings[i];
                if (binding.isComposite)
                {
                    // A modifier composite (Select All = Ctrl + A) shows every part joined by a plus.
                    bool first = true;
                    for (int j = i + 1; j < bindings.Count && bindings[j].isPartOfComposite; j++)
                    {
                        if (!first) parts.Add(new GuideKeyPart(GuideKeyPartKind.Plus, "+"));
                        AddPath(parts, bindings[j].effectivePath);
                        first = false;
                    }
                    return;
                }
                if (binding.isPartOfComposite) continue;
                // Index 0 is the keyboard and mouse binding; the rebind screen edits the same one.
                AddPath(parts, binding.effectivePath);
                return;
            }
        }

        static void AddPath(List<GuideKeyPart> parts, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            string lower = path.ToLowerInvariant();
            if (lower.StartsWith("<mouse>/"))
            {
                if (lower.EndsWith("leftbutton")) { parts.Add(new GuideKeyPart(GuideKeyPartKind.MouseLeft)); return; }
                if (lower.EndsWith("rightbutton")) { parts.Add(new GuideKeyPart(GuideKeyPartKind.MouseRight)); return; }
                if (lower.EndsWith("middlebutton")) { parts.Add(new GuideKeyPart(GuideKeyPartKind.MouseMiddle)); return; }
            }
            string text = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            parts.Add(new GuideKeyPart(GuideKeyPartKind.Keycap, Shorten(text)));
        }

        static string Shorten(string text)
        {
            if (text.StartsWith("Left ") || text.StartsWith("Right "))
            {
                string rest = text.Substring(text.IndexOf(' ') + 1);
                if (rest == "Ctrl" || rest == "Control" || rest == "Shift" || rest == "Alt") text = rest;
                if (text == "Control") text = "Ctrl";
            }
            return text switch
            {
                "Escape" => "Esc",
                "Backquote" => "`",
                "Left Bracket" => "[",
                "Right Bracket" => "]",
                "Space" => "Space",
                _ => text
            };
        }

        /// <summary>Plain text of the keys, for the search box.</summary>
        public static string ToSearchText(string keys)
        {
            var builder = new System.Text.StringBuilder();
            foreach (GuideKeyPart part in Parse(keys))
                if (part.kind == GuideKeyPartKind.Keycap) builder.Append(part.text).Append(' ');
            return builder.ToString();
        }
    }
}
