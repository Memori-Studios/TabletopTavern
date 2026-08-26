using System;
using System.Collections.Generic;
using UnityEngine;

namespace TJ.Battle
{
    /// <summary>
    /// Single source of truth for the display order of squad cards in battle.
    /// All mutations must go through this class. Subscribers (UIManager, SquadManager,
    /// GroupManager) react to OnSquadOrderChanged and never modify _squadOrder themselves.
    /// </summary>
    public class SquadOrderManager : MonoBehaviour
    {
        private List<int> _squadOrder = new();
        public IReadOnlyList<int> SquadOrder => _squadOrder.AsReadOnly();

        private bool _initialized;
        /// <summary>
        /// True once Initialize has established the starting order. Lets callers tell a squad arriving
        /// during the initial load from one joining later (e.g. summoned by a spell).
        /// </summary>
        public bool IsInitialized => _initialized;

        public event Action<IReadOnlyList<int>> OnSquadOrderChanged;

        /// <summary>
        /// Called once after all squad cards are created at battle start.
        /// Replaces any existing order. Idempotent — safe to call again on scene reload.
        /// </summary>
        public void Initialize(List<int> orderedIds)
        {
            _squadOrder = new List<int>(orderedIds);
            _initialized = true;
            OnSquadOrderChanged?.Invoke(_squadOrder.AsReadOnly());
        }

        /// <summary>
        /// Replaces the order wholesale. Validates all IDs are present before accepting.
        /// Only fires the event if the order actually changed. Used by load-from-save.
        /// </summary>
        public void SetOrder(List<int> newOrder)
        {
            bool changed = false;
            if (newOrder.Count != _squadOrder.Count)
            {
                changed = true;
            }
            else
            {
                for (int i = 0; i < newOrder.Count; i++)
                {
                    if (newOrder[i] != _squadOrder[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed) return;

            _squadOrder = new List<int>(newOrder);
            OnSquadOrderChanged?.Invoke(_squadOrder.AsReadOnly());
        }

        /// <summary>
        /// Moves a single squad to a new display position.
        /// No-op if the squad is already at that index.
        /// </summary>
        public void MoveSquad(int squadId, int toIndex)
        {
            int currentIndex = _squadOrder.IndexOf(squadId);
            if (currentIndex < 0)
            {
                Debug.LogWarning($"SquadOrderManager.MoveSquad: squadId {squadId} not found in order.");
                return;
            }

            toIndex = Mathf.Clamp(toIndex, 0, _squadOrder.Count - 1);
            if (currentIndex == toIndex) return;

            _squadOrder.RemoveAt(currentIndex);
            _squadOrder.Insert(toIndex, squadId);
            OnSquadOrderChanged?.Invoke(_squadOrder.AsReadOnly());
        }

        /// <summary>
        /// Reorders all squads so groups appear in numerical order (group 1 leftmost, then 2, etc.),
        /// followed by ungrouped squads in their current relative positions.
        /// </summary>
        public void ArrangeGroupsInOrder(SquadGroup[] squadGroups)
        {
            List<int> newOrder = new();

            // Grouped squads in group-number order, preserving relative display order within each group
            for (int i = 0; i < squadGroups.Length; i++)
            {
                foreach (int id in _squadOrder)
                {
                    if (squadGroups[i].squadIds.Contains(id))
                        newOrder.Add(id);
                }
            }

            // Ungrouped squads in their current relative order
            HashSet<int> grouped = new(newOrder);
            foreach (int id in _squadOrder)
            {
                if (!grouped.Contains(id))
                    newOrder.Add(id);
            }

            SetOrder(newOrder);
        }

        /// <summary>
        /// Appends a squad to the end of the order. Called when a squad joins after Initialize has
        /// already run, e.g. a squad summoned by a spell mid-battle. Without this its card exists but
        /// has no entry in the order, so MoveSquad would reject any attempt to drag it.
        /// </summary>
        public void AddSquad(int squadId)
        {
            if (_squadOrder.Contains(squadId)) return;

            _squadOrder.Add(squadId);
            OnSquadOrderChanged?.Invoke(_squadOrder.AsReadOnly());
        }

        /// <summary>
        /// Removes a squad from the order. Called when a squad is disbanded mid-battle.
        /// </summary>
        public void RemoveSquad(int squadId)
        {
            if (_squadOrder.Remove(squadId))
                OnSquadOrderChanged?.Invoke(_squadOrder.AsReadOnly());
        }
    }
}
