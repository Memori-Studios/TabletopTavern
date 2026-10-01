using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using Memori.Scenes;

namespace TJ
{
public class UnitStatsUIContainer : MonoBehaviour
{
    [Header("Unit Attributes")]
    [SerializeField] private UnitStatUI unitStatUIPrefab;
    [SerializeField] private Transform unitAttributesParent;

    // Set on a compare panel: each row shows its difference from the same stat here.
    UnitStatsUIContainer compareBase;
    // Set on the base: the compare panel whose differences go stale when these stats change.
    UnitStatsUIContainer comparer;
    List<UnitStatUI> loadedRows = new();

    public void CompareAgainst(UnitStatsUIContainer _base)
    {
        compareBase = _base;
        _base.comparer = this;
    }

    public void Load(UnitName _unitName, bool _applyGearBonuses, int _prestige, UnitAttribute _prestigeTrait = UnitAttribute.None, Unity.Entities.Entity _liveSquad = default)
    {
        List<UnitStatValue> unitStats = TabletopTavernData.Instance.GetUnitStatsForDisplay( _unitName);
        List<UnitStatUI> unitStatUI = unitAttributesParent.GetComponentsInChildren<UnitStatUI>().ToList();

        if(_unitName == UnitName.Gate)
        {
            unitStats.RemoveAll(s =>
                s.unitStat == UnitStat.MeleeAttack      ||
                s.unitStat == UnitStat.WeaponStrength    ||
                s.unitStat == UnitStat.HitPoints         ||
                s.unitStat == UnitStat.None              ||
                s.unitStat == UnitStat.Speed             ||
                s.unitStat == UnitStat.Armor             ||
                s.unitStat == UnitStat.ChargeBonus       ||
                s.unitStat == UnitStat.Leadership        ||
                s.unitStat == UnitStat.Ammunition        ||
                s.unitStat == UnitStat.ChargeImpactDamage);
        }

        //if more attributes than the ones already loaded add them
        if(unitStats.Count > unitStatUI.Count) {
            for(int i = unitStatUI.Count; i < unitStats.Count; i++) {
                unitStatUI.Add(Instantiate(unitStatUIPrefab, unitAttributesParent));
            }
        } else if(unitStats.Count < unitStatUI.Count) {
            for(int i = unitStatUI.Count - 1; i >= unitStats.Count; i--) {
                Destroy(unitStatUI[i].gameObject);
                unitStatUI.RemoveAt(i);
            }
        }

        for(int i = 0; i< unitStatUI.Count; i++) {
            unitStatUI[i].LoadUnitStatUI(unitStats[i], _prestige, _unitName, _applyGearBonuses, _prestigeTrait, _liveSquad);
        }
        loadedRows = unitStatUI;

        if (compareBase != null) ApplyDeltas();
        else if (comparer != null) comparer.ApplyDeltas();
    }
    // A stat the base squad lacks (Range on a melee unit) gets no chip.
    void ApplyDeltas()
    {
        foreach (UnitStatUI row in loadedRows)
        {
            UnitStatUI match = compareBase.loadedRows.Find(r => r.Stat == row.Stat);
            if (match == null) row.HideDelta();
            else row.ShowDelta(row.Total - match.Total);
        }
    }
    public void Refresh()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(unitAttributesParent as RectTransform);
    }
    // The stat's root Image is what the tooltip trigger receives pointer events through.
    public void DisableTooltips()
    {
        foreach (UnitStatUI stat in unitAttributesParent.GetComponentsInChildren<UnitStatUI>())
            stat.GetComponent<Image>().raycastTarget = false;
    }
}
}