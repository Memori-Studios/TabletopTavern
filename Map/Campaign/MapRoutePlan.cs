using System;
using System.Collections.Generic;

namespace TJ.Map
{
    /// <summary>The route the player marked on the campaign map: node indexes, at most one per layer.</summary>
    public static class MapRoutePlan
    {
        /// <summary>Marks the node, or clears it if it was marked. Returns whether the node is marked now.</summary>
        public static bool Toggle(List<int> marks, int nodeIndex, Func<int, int> layerOf)
        {
            if (marks.Remove(nodeIndex)) return false;
            // A route visits one node per layer, so a new mark replaces the layer's old one.
            int layer = layerOf(nodeIndex);
            marks.RemoveAll(mark => layerOf(mark) == layer);
            marks.Add(nodeIndex);
            return true;
        }

        /// <summary>A path is on the plan when it ends on a mark and starts on a mark or on the node the player stands on.</summary>
        public static bool IsPlannedLine(List<int> marks, int fromIndex, bool fromIsCurrentNode, int toIndex) =>
            marks.Contains(toIndex) && (fromIsCurrentNode || marks.Contains(fromIndex));

        /// <summary>Drops marks the player has reached or passed, and marks for nodes the map no longer has (layerOf gives -1).</summary>
        public static void Prune(List<int> marks, Func<int, int> layerOf, int reachedLayer) =>
            marks.RemoveAll(mark => layerOf(mark) <= reachedLayer);
    }
}
