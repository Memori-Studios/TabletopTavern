using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Memori.Input;
using System;
using Memori.Notifications;
using Memori.SaveData;

namespace TJ.Battle
{
[System.Serializable]
public class SquadGroup
{
    public List<int> squadIds = new List<int>();
    [NonSerialized] public bool IsLocked = false;
    [NonSerialized] public List<SetDestination> LockedPositions = new();
}
    public class GroupManager : MonoBehaviour
    {
        [Header("Group UI")]
        [SerializeField] private SquadGroup[] squadGroups = new SquadGroup[6];
        [SerializeField] private GroupUI groupUIPrefab;
        [SerializeField] private List<GroupUI> groupUIs = new ();
        [SerializeField] private Transform groupUIParent;

        [Header("Squad Display")]
        [SerializeField] private int groupHovered;
        public int GroupHovered => groupHovered;
        [SerializeField] private List<Color> colors;
        private List<SquadDisplayCardBattle> squadDisplays = new ();
        public SquadGroup[] SquadGroups => squadGroups;
        private List<SavedSquadGroup> _pendingSavedGroups;

        // Locked group formations are code-complete but parked (see ToggleLockGroup), so the
        // lock button stays hidden rather than toggling with no effect. Flip to true to surface it.
        private const bool LOCK_FEATURE_ENABLED = false;

        public void Load()
        {
            // Only needed once – creates the actual objects
            for (int i = 0; i < squadGroups.Length; i++)
            {
                squadGroups[i] = new SquadGroup();
            }

            // Unsubscribe first to prevent double-subscription if Load is called more than once
            InputHandler.Instance.GroupButtonPressed -= CreateGroup;
            InputHandler.Instance.OnSelectedGroup1 -= SelectGroup1;
            InputHandler.Instance.OnSelectedGroup2 -= SelectGroup2;
            InputHandler.Instance.OnSelectedGroup3 -= SelectGroup3;
            InputHandler.Instance.OnSelectedGroup4 -= SelectGroup4;
            InputHandler.Instance.OnSelectedGroup5 -= SelectGroup5;
            InputHandler.Instance.OnSelectedGroup6 -= SelectGroup6;
            InputHandler.Instance.OnSelectAll -= SelectAllSquads;

            BattleManager.Instance.UIManager.OnSquadDisplaysChanged -= OnSquadDisplaysChanged;
            BattleManager.Instance.SquadManager.OnDestroyedSquad -= RemoveSquadFromGroups;
            BattleManager.Instance.UnitSelectionManager.OnSelectedSquadsChanged -= OnSelectedSquadsChanged;

            InputHandler.Instance.GroupButtonPressed += CreateGroup;
            InputHandler.Instance.OnSelectedGroup1 += SelectGroup1;
            InputHandler.Instance.OnSelectedGroup2 += SelectGroup2;
            InputHandler.Instance.OnSelectedGroup3 += SelectGroup3;
            InputHandler.Instance.OnSelectedGroup4 += SelectGroup4;
            InputHandler.Instance.OnSelectedGroup5 += SelectGroup5;
            InputHandler.Instance.OnSelectedGroup6 += SelectGroup6;
            InputHandler.Instance.OnSelectAll += SelectAllSquads;

            BattleManager.Instance.UIManager.OnSquadDisplaysChanged += OnSquadDisplaysChanged;
            BattleManager.Instance.SquadManager.OnDestroyedSquad += RemoveSquadFromGroups;
            BattleManager.Instance.UnitSelectionManager.OnSelectedSquadsChanged += OnSelectedSquadsChanged;
        }
        private void SelectAllSquads()
        {
            if (BattleManager.Instance.GamePhase == GamePhase.SetUp) return;

            BattleManager.Instance.UnitSelectionManager.SelectAllPlayerSquads();
        }
        private void SelectGroup1()
        {
            TryToSelectGroup(1);
        }
        private void SelectGroup2()
        {
            TryToSelectGroup(2);
        }
        private void SelectGroup3()
        {
            TryToSelectGroup(3);
        }
        private void SelectGroup4()
        {
            TryToSelectGroup(4);
        }
        private void SelectGroup5()
        {
            TryToSelectGroup(5);
        }
        private void SelectGroup6()
        {
            TryToSelectGroup(6);
        }
        public void CreateGroup()
        {
            CreateGroup(-1);
        }
        public void CreateGroup(int _groupNumber = -1)
        {
            // Debug.Log($"Creating Group...");
            bool SelectedSquadsPerfectlyMatchAGroup(List<int> _squadIds)
            {
                for (int i = 0; i < squadGroups.Length; i++)
                {
                    if (squadGroups[i].squadIds.Count == _squadIds.Count)
                    {
                        bool allSquadsMatch = true;
                        for (int j = 0; j < _squadIds.Count; j++)
                        {
                            if (!squadGroups[i].squadIds.Contains(_squadIds[j]))
                            {
                                allSquadsMatch = false;
                                break;
                            }
                        }
                        if (allSquadsMatch) return true;
                    }
                }
                return false;
            }

            int GetFirstOpenGroupSlot()
            {
                for (int i = 0; i < squadGroups.Length; i++)
                {
                    if (squadGroups[i].squadIds.Count == 0)
                    {
                        // Debug.Log($"Found open group slot at index {i}");
                        return i;
                    }
                }
                return -1;
            }

            void RemoveSelectedSquadsFromExistingGroups(List<int> selectedSquadIds)
            {
                for(int i = 0; i < squadGroups.Length; i++)
                {
                    SquadGroup squadGroup = squadGroups[i];
                    for (int j = squadGroup.squadIds.Count -1; j >=0; j--)
                    {
                        int squadId = squadGroup.squadIds[j];
                        if (selectedSquadIds.Contains(squadId))
                        {
                            squadGroup.squadIds.RemoveAt(j);
                            // Debug.Log($"Removing squad {squadId} from group {i+1}");
                        }
                    }
                }
            }

            if (_groupNumber != -1 && (_groupNumber < 1 || _groupNumber > squadGroups.Length))
            {
                Debug.LogWarning($"CreateGroup called with out-of-range group number {_groupNumber}.");
                return;
            }

            //this is copying it, I want to create a new list to avoid modifying the original while iterating
            List<int> selectedSquadIds = new List<int>(BattleManager.Instance.UnitSelectionManager.SelectedSquadIds);
            // Debug.Log($"Selected squads for grouping: {string.Join(", ", selectedSquadIds)}");

            // An empty selection matches any empty slot, so it normally no-ops. With every slot
            // full it would fall through and overwrite the requested group with an empty one.
            if (selectedSquadIds.Count == 0) return;

            if (SelectedSquadsPerfectlyMatchAGroup(selectedSquadIds))
            {
                RemoveSelectedSquadsFromExistingGroups(selectedSquadIds);
                // Debug.Log($"Selected squads perfectly match an existing group, removing group instead of creating a new one.");
            }
            else
            {
                RemoveSelectedSquadsFromExistingGroups(selectedSquadIds);

                int openGroupIndex = _groupNumber == -1 ? GetFirstOpenGroupSlot() : _groupNumber - 1;

                if(openGroupIndex == -1)
                {
                    NotificationManager.Instance.DisplayNotification("All group slots are full!");
                    return;
                }

                if (_groupNumber != -1 && squadGroups[openGroupIndex].squadIds.Count > 0)
                    NotificationManager.Instance.DisplayNotification($"Group {_groupNumber} overwritten!");

                squadGroups[openGroupIndex] = new () { squadIds = selectedSquadIds };

                Debug.Log($"Created group {openGroupIndex + 1} with squads: {string.Join(", ", selectedSquadIds)}");
                // Debug.Log($"created group in slot {openGroupIndex+1} with squads: {string.Join(", ", selectedSquadIds)}");
                BattleManager.Instance.SquadOrderManager.ArrangeGroupsInOrder(squadGroups);
            }

            RefreshGroupUIs();
        }
        private void RefreshGroupUIs()
        {
            // Debug.Log($"Refreshing Group UIs...");
            // Unity never sends OnPointerExit to a destroyed object, so a bracket torn down while
            // hovered would leave groupHovered latched and block every squad card from being
            // hovered or clicked. The EventSystem re-raycasts each frame, so the replacement
            // bracket under the cursor still fires its own OnPointerEnter.
            UnhoverGroup();
            foreach (GroupUI groupUI in groupUIs) {
                Destroy(groupUI.gameObject);
            }
            groupUIs.Clear();

            // Layout is already forced by UIManager.OnSquadOrderReceived before OnSquadDisplaysChanged
            // fires, so card positions are settled and we can read them synchronously.
            Transform GetGroupUIPosition(int squadId) {
                SquadDisplayCardBattle squadDisplay = squadDisplays.Find(s => s.SquadId == squadId);
                if(squadDisplay == null) return null;
                return squadDisplay.transform;
            }

            if (colors == null || colors.Count == 0)
            {
                Debug.LogError("GroupManager: colors list is empty, cannot create Group UIs.");
                return;
            }

            for (int i = 0; i < squadGroups.Length; i++)
            {
                SquadGroup squadGroup = squadGroups[i];
                if(squadGroup.squadIds.Count == 0) continue;

                int firstSquadId = squadGroup.squadIds
                    .OrderBy(id => {
                        SquadDisplayCardBattle d = squadDisplays.Find(s => s.SquadId == id);
                        return d != null ? d.transform.GetSiblingIndex() : int.MaxValue;
                    })
                    .First();
                Transform groupUIPosition = GetGroupUIPosition(firstSquadId);
                if(groupUIPosition == null)
                {
                    continue;
                }

                GroupUI groupUI = Instantiate(groupUIPrefab, groupUIPosition);
                groupUI.SetUpGroupUI(i+1, squadGroup.squadIds.Count, this, colors[i % colors.Count], squadGroup.IsLocked, LOCK_FEATURE_ENABLED);
                groupUIs.Add(groupUI);
                groupUI.transform.SetParent(groupUIParent);
            }

            // SetUpGroupUI always starts deselected, so re-apply the live selection or a group that
            // was selected when a card was added or removed renders dim until the next selection change.
            OnSelectedSquadsChanged(BattleManager.Instance.UnitSelectionManager.SelectedSquadIds);
        }
        public void TryToSelectGroup(int _groupNumber)
        {
            if (SettingsManager.Instance.SettingsPanelOpen) return;
            // SelectGroup writes selectedSquadIds directly, so it would otherwise sidestep the
            // GamePhase.SetUp guard that AttemptSquadSelect applies to every other selection route.
            if (BattleManager.Instance.GamePhase == GamePhase.SetUp) return;

            // Debug.Log($"Trying to select Group {_groupNumber}...");
            if(InputHandler.Instance.ControlInput)
            {
                CreateGroup(_groupNumber);
                return;
            }

            SelectGroup(_groupNumber);
        }
        public void SelectGroup(int _groupNumber)
        {
            // Debug.Log($"Selecting Group {_groupNumber}...");
            if (_groupNumber < 1 || _groupNumber > squadGroups.Length)
            {
                Debug.LogWarning($"SelectGroup called with out-of-range group number {_groupNumber}.");
                return;
            }
            if(squadGroups[_groupNumber-1].squadIds.Count == 0) {
                Debug.Log($"cant select group {_groupNumber}");
                return;
            }
            SquadGroup squadGroup = squadGroups[_groupNumber-1];
            List<int> squadIds = squadGroup.squadIds.GetRange(0, squadGroup.squadIds.Count);
            BattleManager.Instance.UnitSelectionManager.SelectSquadsByGroup(squadIds);
        }
        public void HoverGroup(int _groupNumber)
        {
            groupHovered = _groupNumber;
        }
        public void UnhoverGroup()
        {
            groupHovered = 0;
        }
        // Returns 1-6 if the squad is in a group, -1 if not.
        public int GetGroupNumberForSquad(int _squadId)
        {
            for (int i = 0; i < squadGroups.Length; i++)
            {
                if (squadGroups[i].squadIds.Contains(_squadId)) return i + 1;
            }
            return -1;
        }
        // Returns the group number (1-6) of the card at the given display index, or -1 if ungrouped/out of range.
        public int GetGroupNumberForSquadAtIndex(int _index)
        {
            if(_index < 0 || _index >= squadDisplays.Count) return -1;
            return GetGroupNumberForSquad(squadDisplays[_index].SquadId);
        }
        public void ToggleLockGroup(int groupNumber)
        {
            return; // Feature disabled — locking not yet implemented
            // if (groupNumber < 1 || groupNumber > squadGroups.Length) return;
            // SquadGroup group = squadGroups[groupNumber - 1];
            // if (group.IsLocked) UnlockGroup(groupNumber);
            // else LockGroup(groupNumber);
        }
        private void LockGroup(int groupNumber)
        {
            SquadGroup group = squadGroups[groupNumber - 1];
            if (group.squadIds.Count == 0) return;
            group.LockedPositions = BattleManager.Instance.UnitSelectionManager.GetSelectedUnitsPositions();
            group.IsLocked = true;
            Debug.Log($"Locked group {groupNumber} with {group.LockedPositions.Count} unit positions.");
            RefreshGroupUIs();
        }
        private void UnlockGroup(int groupNumber)
        {
            SquadGroup group = squadGroups[groupNumber - 1];
            group.IsLocked = false;
            group.LockedPositions.Clear();
            RefreshGroupUIs();
        }
        public bool AreSelectedSquadsInLockedGroup(List<int> selectedIds, out SquadGroup lockedGroup)
        {
            lockedGroup = null;
            foreach (SquadGroup g in squadGroups)
            {
                if (!g.IsLocked) continue;
                if (g.squadIds.Count != selectedIds.Count) continue;
                if (g.squadIds.All(id => selectedIds.Contains(id)))
                {
                    lockedGroup = g;
                    return true;
                }
            }
            return false;
        }
        public void OnDestroy()
        {
            if(InputHandler.HasInstance)
            {
                InputHandler.Instance.GroupButtonPressed -= CreateGroup;
                InputHandler.Instance.OnSelectedGroup1 -= SelectGroup1;
                InputHandler.Instance.OnSelectedGroup2 -= SelectGroup2;
                InputHandler.Instance.OnSelectedGroup3 -= SelectGroup3;
                InputHandler.Instance.OnSelectedGroup4 -= SelectGroup4;
                InputHandler.Instance.OnSelectedGroup5 -= SelectGroup5;
                InputHandler.Instance.OnSelectedGroup6 -= SelectGroup6;
                InputHandler.Instance.OnSelectAll -= SelectAllSquads;
            }
            if(BattleManager.HasInstance && BattleManager.Instance.UIManager != null)
                BattleManager.Instance.UIManager.OnSquadDisplaysChanged -= OnSquadDisplaysChanged;

            if(BattleManager.HasInstance && BattleManager.Instance.SquadManager != null)
                BattleManager.Instance.SquadManager.OnDestroyedSquad -= RemoveSquadFromGroups;

            if(BattleManager.HasInstance && BattleManager.Instance.UnitSelectionManager != null)
                BattleManager.Instance.UnitSelectionManager.OnSelectedSquadsChanged -= OnSelectedSquadsChanged;
        }
        public void RemoveSquadFromGroups(int _squadId)
        {
            foreach(SquadGroup squadGroup in squadGroups)
            {
                if(squadGroup.squadIds.Contains(_squadId))
                {
                    squadGroup.squadIds.Remove(_squadId);
                    Debug.Log($"Removed squad {_squadId} from its group.");
                }
            }
            // Don't call RefreshGroupUIs here — UIManager hasn't removed the card yet so
            // squadDisplays still contains the dead card with stale sibling indices.
            // RefreshGroupUIs will be called correctly via OnSquadDisplaysChanged once
            // UIManager.RemoveSquad fires and forces a layout rebuild.
        }
        public void SetPendingGroups(List<SavedSquadGroup> savedGroups)
        {
            _pendingSavedGroups = savedGroups;
            // Campaign battles spawn every squad into the staging rows before the dice roll, so all
            // cards already exist by the time LoadBothArmies gets here and no further
            // OnSquadDisplaysChanged is guaranteed. Apply straight away when the cards are ready.
            TryApplyPendingGroups();
        }
        // True once the pending groups have been consumed.
        private bool TryApplyPendingGroups()
        {
            if (_pendingSavedGroups == null) return false;
            if (squadDisplays.Count == 0) return false;
            if (squadDisplays.Count < BattleManager.Instance.BattleSaveManager.PlayerSquadsToSpawn) return false;

            List<SavedSquadGroup> groups = _pendingSavedGroups;
            _pendingSavedGroups = null;
            LoadGroupsFromSave(groups);
            return true;
        }
        public void OnSquadDisplaysChanged(List<SquadDisplayCardBattle> _squadDisplays)
        {
            // Debug.Log($"Updating OnSquadDisplaysChanged...");
            squadDisplays = _squadDisplays;

            if (TryApplyPendingGroups()) return;

            RefreshGroupUIs();
        }
        public void LoadGroupsFromSave(List<SavedSquadGroup> savedGroups)
        {
            foreach (var group in squadGroups)
                group.squadIds.Clear();

            foreach (SavedSquadGroup saved in savedGroups)
            {
                if (saved.slotIndex < 0 || saved.slotIndex >= squadGroups.Length) continue;
                foreach (string uniqueId in saved.squadUniqueIds)
                {
                    int squadId = BattleManager.Instance.ArmySpawnManager.GetSquadIDFromUnitUniqueID(uniqueId);
                    if (squadId > 0)
                        squadGroups[saved.slotIndex].squadIds.Add(squadId);
                }
            }
            // A GroupUI is one bracket anchored on the group's leftmost card and sized by member
            // count, so members have to be adjacent. CreateGroup guarantees that; a loaded save does
            // not, because casualties and new recruits can reshuffle playerArmy between battles.
            BattleManager.Instance.SquadOrderManager.ArrangeGroupsInOrder(squadGroups);
            RefreshGroupUIs();
        }
        private void OnSelectedSquadsChanged(List<int> selectedSquadIds)
        {
            foreach (GroupUI groupUI in groupUIs)
            {
                SquadGroup group = squadGroups[groupUI.GroupID - 1];
                bool allInGroup = group.squadIds.Count > 0 && group.squadIds.All(id => selectedSquadIds.Contains(id));
                groupUI.SetSelected(allInGroup);
            }
        }
        public void CleanUp()
        {
            ResetAllGroups();
        }
        public void ResetAllGroups()
        {
            // Debug.Log("Resetting all groups...");
            _pendingSavedGroups = null;
            groupHovered = 0;
            foreach (var group in squadGroups)
            {
                group.squadIds.Clear();
                group.IsLocked = false;
                group.LockedPositions.Clear();
            }
            // Clear UI...
            foreach (var ui in groupUIs) Destroy(ui.gameObject);
            groupUIs.Clear();
        }
    }
}
