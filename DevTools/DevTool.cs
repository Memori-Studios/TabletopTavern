using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJ.DevTools
{
    /// <summary>Where a dev tool can run. The page greys a tool whose scope is not live.</summary>
    public enum DevScope { Anywhere, Map, Battle, Editor }

    public enum DevButtonStyle { Standard, Danger }

    /// <summary>What a dev tool did, for the page's log line.</summary>
    public readonly struct DevResult
    {
        public readonly bool Ok;
        public readonly string Message;

        private DevResult(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }

        public static DevResult Done(string message) => new(true, message);
        public static DevResult Refused(string message) => new(false, message);
        /// <summary>For a control that only changes its own label, such as a cycle button.</summary>
        public static DevResult Silent => new(true, null);
    }

    /// <summary>One button on a tool row. The label is read again after every click, so a cycle or toggle shows its value.</summary>
    public class DevControl
    {
        public Func<string> Label;
        public DevButtonStyle Style;
        public Func<DevResult> Run;

        public DevControl(string label, Func<DevResult> run, DevButtonStyle style = DevButtonStyle.Standard)
        {
            Label = () => label;
            Run = run;
            Style = style;
        }

        public DevControl(Func<string> label, Func<DevResult> run, DevButtonStyle style = DevButtonStyle.Standard)
        {
            Label = label;
            Run = run;
            Style = style;
        }
    }

    /// <summary>One row on the Dev Tools page.</summary>
    public class DevTool
    {
        public DevScope Scope;
        public string Group;
        public string Label;
        public DevControl[] Controls;

        public DevTool(DevScope scope, string group, string label, params DevControl[] controls)
        {
            Scope = scope;
            Group = group;
            Label = label;
            Controls = controls;
        }
    }

    /// <summary>One tile in the browser: a unit, a gear item, a consumable, an Ordeal or an event.</summary>
    public class DevEntry
    {
        public string Id;
        public string Name;
        /// <summary>The filter chip this entry sits under, such as its faction.</summary>
        public string Group;
        public Color GroupColour = Color.white;
        /// <summary>Lower case. The search box matches every typed word against it.</summary>
        public string Search;
        public Sprite Icon;
        public Sprite TypeIcon;
        public Color Rarity = Color.white;
        /// <summary>True while the run holds this entry. Null for entries that are never held, such as units.</summary>
        public Func<bool> Held;
    }

    /// <summary>One tab of the browser.</summary>
    public class DevBrowserTab
    {
        public string Name;
        public DevScope Scope;
        public bool UnitTiles;
        public string SearchHint;
        public Func<List<DevEntry>> Entries;
        public Func<DevEntry, DevResult> Pick;
        /// <summary>Controls shown above the grid, such as the Prestige a new squad arrives at.</summary>
        public DevControl[] Options = Array.Empty<DevControl>();
    }
}
