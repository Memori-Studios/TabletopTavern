using System.Collections.Generic;

namespace TabletopTavern.Analytics
{
    // What the current map node offered and what the player took, held until nodeCompleted sends it as `detail`.
    // Anything logged between two nodes (a disband on the map, say) lands on the next node.
    public static class NodeLog
    {
        // Caps each list, so a looped action can never push nodeCompleted past the event size limit.
        public const int MaxItemsPerList = 30;

        private static Dictionary<string, object> s_detail = new Dictionary<string, object>();

        /// <summary>Forgets everything logged. Called when a run starts, so a new run never inherits the last one's entries.</summary>
        public static void Clear()
        {
            s_detail = new Dictionary<string, object>();
        }

        /// <summary>Hands over the node's detail and starts the next node empty. Null when nothing was logged.</summary>
        public static Dictionary<string, object> Take()
        {
            Dictionary<string, object> detail = s_detail;
            s_detail = new Dictionary<string, object>();
            return detail.Count > 0 ? detail : null;
        }

        public static void Set(string key, object value)
        {
            s_detail[key] = value;
        }

        public static void Add(string key, Dictionary<string, object> item)
        {
            if (!(s_detail.TryGetValue(key, out object existing) && existing is List<Dictionary<string, object>> list))
            {
                list = new List<Dictionary<string, object>>();
                s_detail[key] = list;
            }
            if (list.Count < MaxItemsPerList) list.Add(item);
        }

        /// <summary>Runs one hook's logging so a bug in it costs the entry, never the game action around it.</summary>
        public static void Try(string what, System.Action log)
        {
            GameEventTracker.TryRun("node log " + what, log);
        }

        public static void Count(string key)
        {
            s_detail[key] = (s_detail.TryGetValue(key, out object existing) && existing is int n ? n : 0) + 1;
        }
    }
}
