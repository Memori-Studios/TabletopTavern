using System.Collections.Generic;
using Unity.Mathematics;

namespace TJ.Battle
{
    /// <summary>
    /// Spreads a locked group's attack order across the enemies in front of it, the way a Total War
    /// Formation Group does, instead of piling every member onto the one squad that was clicked.
    /// Pure geometry over squad centres so it can be tested without an EntityManager.
    /// </summary>
    public static class LockedGroupTargeting
    {
        public struct SquadPoint
        {
            public int SquadId;
            public float3 Center;
        }

        // About one infantry squad width: an enemy further off a member's lane than this is not "in front".
        public const float LateralTolerance = 20f;
        // Enemies this far behind the group centre are not candidates at all.
        public const float BehindTolerance = 10f;

        /// <summary>
        /// Member squad id -> enemy squad id. Every member gets an entry. The clicked squad is always
        /// taken by the member it sits most squarely in front of, so the order the player gave is honoured.
        /// </summary>
        public static Dictionary<int, int> Assign(List<SquadPoint> members, List<SquadPoint> enemies, int clickedSquadId)
        {
            Dictionary<int, int> assignment = new();
            if (members.Count == 0) return assignment;

            int clickedIndex = enemies.FindIndex(e => e.SquadId == clickedSquadId);
            if (clickedIndex < 0)
            {
                foreach (SquadPoint m in members) assignment[m.SquadId] = clickedSquadId;
                return assignment;
            }

            float3 groupCenter = float3.zero;
            foreach (SquadPoint m in members) groupCenter += m.Center;
            groupCenter /= members.Count;

            float3 toClicked = enemies[clickedIndex].Center - groupCenter;
            toClicked.y = 0f;
            float clickedDistance = math.length(toClicked);
            float3 forward = clickedDistance > 1e-3f ? toClicked / clickedDistance : math.forward();
            float3 right = math.cross(math.up(), forward);
            float maxForward = math.max(clickedDistance * 1.5f, clickedDistance + 30f);

            List<int> candidates = new();
            for (int i = 0; i < enemies.Count; i++)
            {
                float fwd = math.dot(enemies[i].Center - groupCenter, forward);
                if (fwd < -BehindTolerance || fwd > maxForward) continue;
                candidates.Add(i);
            }

            float clickedLateral = math.dot(toClicked, right);
            int bestMemberForClicked = -1;
            float bestMemberCost = float.MaxValue;

            for (int m = 0; m < members.Count; m++)
            {
                float3 offset = members[m].Center - groupCenter;
                float memberLateral = math.dot(offset, right);

                int bestEnemy = clickedIndex;
                float bestCost = float.MaxValue;
                foreach (int e in candidates)
                {
                    float3 enemyOffset = enemies[e].Center - groupCenter;
                    float lateralGap = math.abs(math.dot(enemyOffset, right) - memberLateral);
                    if (lateralGap > LateralTolerance) continue;
                    float cost = lateralGap + 0.5f * math.abs(math.dot(enemyOffset, forward) - clickedDistance);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestEnemy = e;
                    }
                }
                assignment[members[m].SquadId] = enemies[bestEnemy].SquadId;

                float costToClicked = math.abs(clickedLateral - memberLateral);
                if (costToClicked < bestMemberCost)
                {
                    bestMemberCost = costToClicked;
                    bestMemberForClicked = m;
                }
            }

            if (bestMemberForClicked >= 0 && !assignment.ContainsValue(clickedSquadId))
                assignment[members[bestMemberForClicked].SquadId] = clickedSquadId;

            return assignment;
        }
    }
}
