using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Memori.UI;
using TJ.Map;
using Memori.Scenes;
using System.Linq;
using Memori.Localization;

namespace TJ
{
    public class UnitAttributesUIContainer : MonoBehaviour
    {
        [Header("Unit Attributes")]
        [SerializeField] private UnitAttributesUI unitAttributePrefab;
        [SerializeField] private Transform unitAttributesParent;
        [Tooltip("Distinct-colored variant of unitAttributePrefab, used only for the slot showing the prestige-granted trait.")]
        [SerializeField] private UnitAttributesUI prestigeTraitAttributePrefab;

        [Header("Unit Bonuses")]
        [SerializeField] private UnitBonusUI unitBonusUIPrefab;
        [SerializeField] private Transform unitBonusesParent;
        /// <summary>
        /// The right-hand bonus stack. Exposed so a panel that parks its own always-visible card
        /// beneath it can measure how tall the stack currently is. Read-only on purpose - the pools
        /// in <see cref="Load"/> own everything under it.
        /// </summary>
        public Transform UnitBonusesParent => unitBonusesParent;
        /// <summary>The row of "+ Trait" entries.</summary>
        public Transform UnitAttributesParent => unitAttributesParent;
        /// <summary>
        /// How many attributes the last <see cref="Load"/> actually rendered. Read this rather than
        /// counting children: the pool trims with Destroy(), which does not take effect until the
        /// end of the frame, so the surplus entries are still there immediately after Load returns.
        /// </summary>
        public int DisplayedAttributeCount { get; private set; }
        /// <summary>
        /// The bonus boxes the last <see cref="Load"/> left alive, in stack order. Measure these
        /// rather than the children of <see cref="UnitBonusesParent"/>: surplus boxes and the "Large"
        /// box are trimmed with Destroy(), so they are still active children until end of frame and
        /// would be counted at full height.
        /// </summary>
        public IReadOnlyList<UnitBonusUI> DisplayedBonusUIs => _displayedBonusUIs;
        readonly List<UnitBonusUI> _displayedBonusUIs = new ();
        GearManager gearManager;
        bool overriden;
        List<UnitAttributesUI> _unitAttributeUIs = new ();
        private void Start()
        {
            if(overriden) return;
            unitBonusesParent.transform.localScale = Vector3.zero;
        }
        public void OverrideStatsDisplayOnStart()
        {
            unitBonusesParent.transform.localScale = Vector3.one;
            overriden = true;
        }
        public void Load(UnitName _unitName, bool _applyGearBonuses = false, UnitAttribute _prestigeTrait = UnitAttribute.None)
        {
            // Resolve without creating: a hover during a scene transition sees the new game state
            // before that scene's manager exists, and Instance would fabricate an unconfigured one.
            switch(SceneHandler.Instance.CurrentGameState)
            {
                case GameStateEnum.Battle:
                    BattleManager battleManager = BattleManager.InstanceIfExists;
                    gearManager = battleManager != null ? battleManager.GearManager : null;
                    break;
                case GameStateEnum.Map:
                    CampaignManager campaignManager = CampaignManager.InstanceIfExists;
                    gearManager = campaignManager != null ? campaignManager.GearManager : null;
                    break;
                default: //menu
                    gearManager = null;
                    break;
            }

            List<UnitAttribute> unitAttributes = TabletopTavernData.Instance.GetUnitAttributesForDisplay(_unitName);

            // Prestige-granted trait is intrinsic to this squad instance (not a player loadout bonus),
            // so it's shown regardless of team/gear settings.
            if (_prestigeTrait != UnitAttribute.None && !unitAttributes.Contains(_prestigeTrait))
                unitAttributes.Add(_prestigeTrait);

            if (gearManager != null && _applyGearBonuses)
            {
                List<UnitAttributeBonus> bonusAttributes = gearManager.GetGearAttributeBonus(_unitName, _prestigeTrait);
                foreach (UnitAttributeBonus bonus in bonusAttributes)
                {
                    if (!unitAttributes.Contains(bonus.UnitAttribute)) unitAttributes.Add(bonus.UnitAttribute);
                }
            }

            if(_applyGearBonuses)
            {
                //get heroes bonuses
                List<UnitAttributeBonus> heroBonuses = HeroBonusManager.Instance.GetHeroAttributeBonus(_unitName);
                foreach(UnitAttributeBonus bonus in heroBonuses) {
                    if (!unitAttributes.Contains(bonus.UnitAttribute)) unitAttributes.Add(bonus.UnitAttribute);
                }
            }

            DisplayedAttributeCount = unitAttributes.Count;

            _unitAttributeUIs = unitAttributesParent.GetComponentsInChildren<UnitAttributesUI>().ToList();
            List<UnitBonusUI> unitBonusUIs = unitBonusesParent.GetComponentsInChildren<UnitBonusUI>().ToList();

            // The prestige trait (if any) always needs the distinct-colored prefab, but which pooled
            // slot it lands on shifts depending on how many other attributes this unit has. Rather than
            // patch individual slots in place, wipe and rebuild the pool whenever the current layout
            // doesn't match — cheap for a handful of icons and avoids the special color sticking to the
            // wrong attribute after switching between units.
            int prestigeIndex = _prestigeTrait != UnitAttribute.None ? unitAttributes.IndexOf(_prestigeTrait) : -1;
            bool poolMatchesLayout = true;
            for (int i = 0; i < _unitAttributeUIs.Count; i++) {
                bool expectSpecial = i == prestigeIndex && prestigeTraitAttributePrefab != null;
                if (_unitAttributeUIs[i].IsPrestigeVariant != expectSpecial) {
                    poolMatchesLayout = false;
                    break;
                }
            }
            if (!poolMatchesLayout) {
                foreach (UnitAttributesUI ui in _unitAttributeUIs) TrimRow(ui);
                _unitAttributeUIs.Clear();
            }

            //if more attributes than the ones already loaded add them
            if(unitAttributes.Count > _unitAttributeUIs.Count) {
                for(int i = _unitAttributeUIs.Count; i < unitAttributes.Count; i++) {
                    bool useSpecialPrefab = i == prestigeIndex && prestigeTraitAttributePrefab != null;
                    _unitAttributeUIs.Add(Instantiate(useSpecialPrefab ? prestigeTraitAttributePrefab : unitAttributePrefab, unitAttributesParent));
                }
            } else if(unitAttributes.Count < _unitAttributeUIs.Count) {
                for(int i = _unitAttributeUIs.Count - 1; i >= unitAttributes.Count; i--) {
                    TrimRow(_unitAttributeUIs[i]);
                    _unitAttributeUIs.RemoveAt(i);
                }
            }

            // The description stack exists only where a screen keeps it open; elsewhere each chip explains itself.
            int stackCount = overriden ? unitAttributes.Count : 0;
            if(stackCount > unitBonusUIs.Count) {
                for(int i = unitBonusUIs.Count; i < stackCount; i++) {
                    unitBonusUIs.Add(Instantiate(unitBonusUIPrefab, unitBonusesParent));
                }
            } else if(stackCount < unitBonusUIs.Count) {
                for(int i = unitBonusUIs.Count - 1; i >= stackCount; i--) {
                    Destroy(unitBonusUIs[i].gameObject);
                    unitBonusUIs.RemoveAt(i);
                }
            }

            _displayedBonusUIs.Clear();
            for (int i = 0; i < unitAttributes.Count; i++)
            {
                _unitAttributeUIs[i].Load(unitAttributes[i]);
                _unitAttributeUIs[i].SetUpTooltip();
                if (i >= stackCount) continue;

                string unitAttributesLocalised = LocalizationManager.Instance.GetText(unitAttributes[i].ToString());
                string localizedDescription = LocalizationManager.Instance.GetText(unitAttributes[i].ToString() + "Desc");
                unitBonusUIs[i].LoadUnitBonusUI(unitAttributesLocalised, localizedDescription);

                if (unitAttributes[i] == UnitAttribute.Large) Destroy(unitBonusUIs[i].gameObject);
                else _displayedBonusUIs.Add(unitBonusUIs[i]);
            }
    }
        public void Refresh()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(unitAttributesParent as RectTransform);
        }
        // Destroy lands at end of frame; an active row would still count in the size fitter this frame.
        static void TrimRow(UnitAttributesUI row)
        {
            row.gameObject.SetActive(false);
            Destroy(row.gameObject);
        }
    }
}