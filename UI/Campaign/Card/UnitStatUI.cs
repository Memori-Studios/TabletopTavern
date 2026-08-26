using System.Collections.Generic;
using Memori.Tooltip;
using Memori.UI;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Memori.Scenes;
using Memori.SaveData;
using Memori.Localization;

namespace TJ
{
    [RequireComponent(typeof(MemoriTooltipTrigger))]
    public class UnitStatUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statScoreText, statNameText;
        [SerializeField] private Image statImage;
        [SerializeField] private Slider baseValueBar, bonusValueBar;
        float amount;
        int totalBonus;
        UnitStat unitStat;
        MemoriTooltipTrigger memoriTooltipTrigger;
        GearManager gearManager;
        public void LoadUnitStatUI(UnitStatValue _unitStatValue, int _prestige, UnitName _unitName, bool applyGearBonuses, UnitAttribute _prestigeTrait = UnitAttribute.None)
        {
            amount = _unitStatValue.Value;
            unitStat = _unitStatValue.unitStat;
            
            string baseValueLocalized = LocalizationManager.Instance.GetText("Base Value");
            string PrestigeLocalised = LocalizationManager.Instance.GetText("Prestige");

            // A hover can land here mid-transition, when CurrentGameState has already flipped but the
            // target scene's manager doesn't exist yet, so resolve without ever creating one.
            // battleManager stays null outside battle and gates the battlefield-bonus section below.
            BattleManager battleManager = null;
            switch (SceneHandler.Instance.CurrentGameState)
            {
                case GameStateEnum.Battle:
                    battleManager = BattleManager.InstanceIfExists;
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
            memoriTooltipTrigger = GetComponent<MemoriTooltipTrigger>();

            statImage.sprite = SpriteData.GetSprite(unitStat.ToString());
            statImage.color = ColorData.GetUnitStatColor(unitStat);
            statNameText.text = LocalizationManager.Instance.GetText(unitStat.ToString());

            totalBonus = 0;
            string description = LocalizationManager.Instance.GetText(unitStat.ToString()+"Desc");
            description += $"\n\n<color {ColorData.Green}>{baseValueLocalized}: {amount}</color>";
            
            UnitType unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(_unitName);

            if(_prestige > 0)
            {
                // Mages take Range, Leadership and charges. This has to come first: the melee test
                // below is negative-form, so a mage would satisfy it and the card would credit it
                // MeleeAttack and MeleeDefense that UnitPrestigeSystemSetUpSystem never grants -
                // the display drifting from the live math is exactly what the hero-bonus rules
                // were rebuilt to stop.
                if(TabletopTavernConstants.Casts(unitType))
                {
                    static string PrestigeRomanNumeral(int _prestige) {
                        return _prestige switch {
                            0 => "I",
                            1 => "II",
                            2 => "III",
                            _ => "",
                        };
                    }
                    if(UnitStat.Range == unitStat || UnitStat.Leadership == unitStat)
                    {
                        totalBonus += TabletopTavernConstants.PRESTIGE_BONUS * _prestige;
                        description += $"\n<color {ColorData.Green}>{PrestigeLocalised} {PrestigeRomanNumeral(_prestige)}: +{TabletopTavernConstants.PRESTIGE_BONUS * _prestige}</color>";
                    }
                    else if(UnitStat.Ammunition == unitStat)
                    {
                        totalBonus += TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE * _prestige;
                        description += $"\n<color {ColorData.Green}>{PrestigeLocalised} {PrestigeRomanNumeral(_prestige)}: +{TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE * _prestige}</color>";
                    }
                }
                // Hybrids take the melee prestige stats, so only pure shooters fall to the else.
                else if(unitType != UnitType.Ranged && unitType != UnitType.Artillery)
                {
                    if(UnitStat.MeleeAttack == unitStat || UnitStat.MeleeDefense == unitStat || UnitStat.Leadership == unitStat)
                    {
                        static string PrestigeRomanNumeral(int _prestige) {
                            return _prestige switch {
                                0 => "I",
                                1 => "II",
                                2 => "III",
                                _ => "",
                            };
                        }
                        totalBonus += TabletopTavernConstants.PRESTIGE_BONUS * _prestige;
                        description += $"\n<color {ColorData.Green}>{PrestigeLocalised} {PrestigeRomanNumeral(_prestige)}: +{TabletopTavernConstants.PRESTIGE_BONUS * _prestige}</color>";
                    }
                }
                else
                {
                    if(UnitStat.Range == unitStat || UnitStat.Accuracy == unitStat)
                    {
                        static string PrestigeRomanNumeral(int _prestige) {
                            return _prestige switch {
                                0 => "I",
                                1 => "II",
                                2 => "III",
                                _ => "",
                            };
                        }
                        totalBonus += TabletopTavernConstants.PRESTIGE_BONUS * _prestige;
                        description += $"\n<color {ColorData.Green}>{PrestigeLocalised} {PrestigeRomanNumeral(_prestige)}: +{TabletopTavernConstants.PRESTIGE_BONUS * _prestige}</color>";
                    }
                    else if(UnitStat.Ammunition == unitStat)
                    {
                        static string PrestigeRomanNumeral(int _prestige) {
                            return _prestige switch {
                                0 => "I",
                                1 => "II",
                                2 => "III",
                                _ => "",
                            };
                        }
                        int ammoBonusPerLevel = unitType == UnitType.Artillery ? TabletopTavernConstants.PRESTIGE_AMMO_BONUS_ARTILLERY : TabletopTavernConstants.PRESTIGE_AMMO_BONUS_RANGED;
                        totalBonus += ammoBonusPerLevel * _prestige;
                        description += $"\n<color {ColorData.Green}>{PrestigeLocalised} {PrestigeRomanNumeral(_prestige)}: +{ammoBonusPerLevel * _prestige}</color>";
                    }
                }
            }

            // Innate attributes plus any prestige-granted one. Drives both the Overdraw / Powder
            // Reserves bonuses immediately below and the Steady Aim check on the Fire-at-Will
            // penalty further down, so it's merged once here rather than at each use.
            SquadAttributes traitAttributes = TabletopTavernData.Instance.GetSquadStats(_unitName).SquadAttributes;
            if (_prestigeTrait != UnitAttribute.None)
                TabletopTavernConstants.SetAttribute(ref traitAttributes, _prestigeTrait);

            // Overdraw and Powder Reserves scale a displayed stat instead of adding a flat amount,
            // so their contribution is derived here rather than coming from a bonus list. Shot
            // Discipline and Demolisher have no stat row of their own and are conveyed by their
            // attribute chip; Steady Aim shows up as the absence of a penalty line below.
            {
                int traitBonus = 0;
                UnitAttribute sourceTrait = UnitAttribute.None;

                // Range is multiplied before the prestige bonus is added at runtime, ammunition
                // after it (UnitSetUpSystem / EntityWatcher), so they read off different bases.
                if (traitAttributes.Overdraw && unitStat == UnitStat.Range)
                {
                    traitBonus = (int)(amount * (TabletopTavernConstants.OVERDRAW_RANGE_MULTIPLIER - 1f));
                    sourceTrait = UnitAttribute.Overdraw;
                }
                else if (traitAttributes.PowderReserves && unitStat == UnitStat.Ammunition)
                {
                    traitBonus = (int)((amount + totalBonus) * (TabletopTavernConstants.POWDER_RESERVES_AMMO_MULTIPLIER - 1f));
                    sourceTrait = UnitAttribute.PowderReserves;
                }
                else if (traitAttributes.DeepQuivers && unitStat == UnitStat.Ammunition
                         && unitType == UnitType.Ranged)
                {
                    traitBonus = TabletopTavernConstants.DEEP_QUIVERS_AMMO_BONUS;
                    sourceTrait = UnitAttribute.DeepQuivers;
                }

                if (traitBonus > 0)
                {
                    totalBonus += traitBonus;
                    string traitName = LocalizationManager.Instance.GetText(sourceTrait.ToString());
                    description += $"\n<color {ColorData.Green}>{traitName}: +{traitBonus}</color>";
                }
            }

            if (gearManager != null && applyGearBonuses)
            {
                //get gear bonuses
                List<UnitStatBonus> unitBonues = gearManager.GetGearStatBonus(unitStat, _unitName, _prestigeTrait);
                foreach (UnitStatBonus unitBonus in unitBonues)
                {
                    totalBonus += (int)unitBonus.Value;
                    description += $"\n<color {ColorData.Green}>{unitBonus.BonusName}: +{unitBonus.Value}</color>";
                }

                //get heroes bonuses
                List<UnitStatBonus> heroBonuses = HeroBonusManager.Instance.GetHeroStatBonus(unitStat, _unitName, amount);
                foreach (UnitStatBonus unitBonus in heroBonuses)
                {
                    totalBonus += (int)unitBonus.Value;
                    description += $"\n<color {ColorData.Green}>{unitBonus.BonusName}: +{unitBonus.Value}</color>";
                }

                //get faction bonuses
                if (HeroBonusManager.Instance.ActiveHeroID == 11 || HeroBonusManager.Instance.ActiveHeroID == 12)
                {
                    //check if units only sakura dynasty
                    if (battleManager != null && battleManager.OnlySakuraUnits)
                    {
                        List<UnitStatBonus> factionBonuses = HeroBonusManager.GetFactionBonus(unitStat);

                        foreach (UnitStatBonus unitBonus in factionBonuses)
                        {
                            totalBonus += (int)unitBonus.Value;
                            description += $"\n<color {ColorData.Green}>{unitBonus.BonusName}: +{unitBonus.Value}</color>";
                        }
                    }
                }
            }

            //if battle, check squad for battlefield bonuses and defensive stance
            if(battleManager != null)
            {
                EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                SquadEntity squadEntity = battleManager.UIManager.SquadBattleInfo.SquadEntity;
                if(entityManager.Exists(squadEntity.SelfEntity))
                {
                    if(entityManager.HasComponent<BattlefieldBonusBufferElement>(squadEntity.SelfEntity))
                    {
                        DynamicBuffer<BattlefieldBonusBufferElement> battlefieldBonus = entityManager.GetBuffer<BattlefieldBonusBufferElement>(squadEntity.SelfEntity);
                        float speedMultiplier = 1f;
                        foreach(BattlefieldBonusBufferElement bonus in battlefieldBonus)
                        {
                            if (bonus.Value.UnitStat == unitStat)
                            {
                                if(unitStat == UnitStat.Armor)
                                {
                                    float armorPenalty = (bonus.Value.Value * 100) / (1 + bonus.Value.Value); // Convert mitigation to armor
                                    int roundedArmorPenalty = Mathf.RoundToInt(armorPenalty);
                                    totalBonus += roundedArmorPenalty;
                                    string localisedBonusName = LocalizationManager.Instance.GetText(bonus.Value.BattlefieldBonusEnum.ToString());
                                    description += $"\n<color {(bonus.Value.Value > 0 ? ColorData.Green : ColorData.Error)}>{localisedBonusName}: {(bonus.Value.Value > 0 ? "+" : "")}{roundedArmorPenalty} </color>";
                                }
                                else if (unitStat == UnitStat.Speed && (bonus.Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Swamp || bonus.Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Rain))
                                {
                                    bool isSwamp = bonus.Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Swamp;
                                    if (isSwamp && TabletopTavernData.Instance.IgnoresSwamp(_unitName))
                                    {
                                        continue;
                                    }
                                    // Swamp and Rain both scale remaining speed by a fraction (e.g. 0.5 = half) and compound multiplicatively,
                                    // so stack them as a running multiplier instead of subtracting from the base amount.
                                    // Rain reads the constant BattlefieldBonusSystem actually multiplies AgentLocomotion.Speed by rather than
                                    // the authored bonus value, which that branch never touches (only large units carry the buffer element).
                                    float speedFraction = isSwamp ? bonus.Value.Value : TabletopTavernConstants.RAIN_SPEED_MODIFIER;
                                    speedMultiplier *= speedFraction;
                                    string localisedBonusName = LocalizationManager.Instance.GetText(bonus.Value.BattlefieldBonusEnum.ToString());
                                    description += $"\n<color {ColorData.Error}>{localisedBonusName}: -{Mathf.RoundToInt((1f - speedFraction) * 100f)}% </color>";
                                }
                                else if (unitStat == UnitStat.Speed)
                                {
                                    // Speed bonuses are stored in AgentLocomotion scale (SquadStats.Speed / 10) for the movement mutation;
                                    // convert back up to SquadStats scale to match "amount" for display
                                    int displaySpeedBonus = Mathf.RoundToInt(bonus.Value.Value * 10f);
                                    totalBonus += displaySpeedBonus;
                                    string localisedBonusName = LocalizationManager.Instance.GetText(bonus.Value.BattlefieldBonusEnum.ToString());
                                    description += $"\n<color {(displaySpeedBonus > 0 ? ColorData.Green : ColorData.Error)}>{localisedBonusName}: {(displaySpeedBonus > 0 ? "+" : "")}{displaySpeedBonus} </color>";
                                }
                                else if (bonus.Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Fog)
                                {
                                    totalBonus -= (int)(amount * 0.5f);
                                    string localisedBonusName = LocalizationManager.Instance.GetText(bonus.Value.BattlefieldBonusEnum.ToString());
                                    description += $"\n<color {ColorData.Error}>{localisedBonusName}: -50% </color>";
                                }
                                else
                                {
                                    totalBonus += (int)bonus.Value.Value;
                                    string localisedBonusName = LocalizationManager.Instance.GetText(bonus.Value.BattlefieldBonusEnum.ToString());
                                    description += $"\n<color {(bonus.Value.Value > 0 ? ColorData.Green : ColorData.Error)}>{localisedBonusName}: {(bonus.Value.Value > 0 ? "+" : "")}{bonus.Value.Value} </color>";
                                }
                            }
                            
                            if(TabletopTavernData.Instance.IsForestDweller(_unitName))
                            {
                                // Debug.Log($"Squad is forest dweller, checking for forest bonuses");
                                if (bonus.Value.BattlefieldBonusEnum == BattlefieldBonusEnum.Forest)
                                {
                                    if (unitStat == UnitStat.MeleeAttack)
                                    {
                                        totalBonus += 5;
                                        string localisedBonusName = LocalizationManager.Instance.GetText("ForestDweller");
                                        description += $"\n<color {ColorData.Green}>{localisedBonusName}: +5 </color>";
                                    }
                                    else if (unitStat == UnitStat.MissileStrength)
                                    {
                                        totalBonus += 5;
                                        string localisedBonusName = LocalizationManager.Instance.GetText("ForestDweller");
                                        description += $"\n<color {ColorData.Green}>{localisedBonusName}: +5 </color>";
                                    }
                                }
                            }
                        }
                        if (unitStat == UnitStat.Speed && speedMultiplier < 1f)
                        {
                            totalBonus += Mathf.RoundToInt(amount * speedMultiplier) - Mathf.RoundToInt(amount);
                        }
                    }
                    if((unitStat == UnitStat.MeleeAttack || unitStat == UnitStat.MeleeDefense) && entityManager.HasComponent<ShieldedStanceSquadComponent>(squadEntity.SelfEntity))
                    {
                        ShieldedStanceSquadComponent shieldedStanceSquadComponent = entityManager.GetComponentData<ShieldedStanceSquadComponent>(squadEntity.SelfEntity);
                        if(shieldedStanceSquadComponent.Stance == ShieldedStance.Defensive)
                        {
                            if(unitStat == UnitStat.MeleeAttack)
                            {
                                string defensiveStanceLocalised = LocalizationManager.Instance.GetText("DefensiveStanceTitle");
                                totalBonus -= (int)(amount / 2); //defensive stance reduces melee attack by 50% of base
                                description += $"\n<color {ColorData.Error}>{defensiveStanceLocalised}: -50% </color>";
                            }
                            else if(unitStat == UnitStat.MeleeDefense)
                            {
                                string defensiveStanceLocalised = LocalizationManager.Instance.GetText("DefensiveStanceTitle");
                                totalBonus += (int)(amount / 2); //defensive stance increases melee defense by 50% of base
                                description += $"\n<color {ColorData.Green}>{defensiveStanceLocalised}: +50% </color>";
                            }
                        }
                    }
                    if(unitStat == UnitStat.Accuracy && entityManager.HasComponent<RangedFireModeSquadComponent>(squadEntity.SelfEntity))
                    {
                        RangedFireModeSquadComponent rangedFireModeSquadComponent = entityManager.GetComponentData<RangedFireModeSquadComponent>(squadEntity.SelfEntity);
                        // Steady Aim units take no penalty in this mode (RangedUnitAttackSystem
                        // skips it), so the card must not show one either.
                        if(rangedFireModeSquadComponent.FireMode == RangedFireMode.FireAtWill && !traitAttributes.SteadyAim)
                        {
                            totalBonus -= (int)(amount * 0.2);
                            string fireAtWillLocalised = LocalizationManager.Instance.GetText("FireAtWillTitle");
                            description += $"\n<color {ColorData.Error}>{fireAtWillLocalised}: -20% </color>";
                        }
                    }
                    if(unitStat == UnitStat.Ammunition && entityManager.HasComponent<SquadAmmunition>(squadEntity.SelfEntity))
                    {
                        SquadAmmunition squadAmmunition = entityManager.GetComponentData<SquadAmmunition>(squadEntity.SelfEntity);
                        int ammunitionLost = (int)(amount + totalBonus - squadAmmunition.Value);
                        if(ammunitionLost > 0)
                        {
                            totalBonus -= ammunitionLost;
                            string ammunitionDepletedLocalised = LocalizationManager.Instance.GetText("AmmunitionDepletedTitle");
                            description += $"\n<color {ColorData.Error}>{ammunitionDepletedLocalised}: -{ammunitionLost} </color>";
                        }
                    }
                    if (unitStat == UnitStat.Leadership && entityManager.HasComponent<DefendersResolveComponent>(squadEntity.SelfEntity))
                    {
                        int bonus = (int)TabletopTavernConstants.FORTIFIED_MORALE_BONUS;
                        totalBonus += bonus;
                        string fortifiedMoraleLocalised = LocalizationManager.Instance.GetText("DefendersResolve");
                        description += $"\n<color {ColorData.Green}>{fortifiedMoraleLocalised}: +{bonus} </color>";
                    }
                }
            }

            statScoreText.text = $"{amount + totalBonus}";
            Color textColor = Color.black;
            textColor = totalBonus switch
            {
                int n when n < 0 => (Color)ColorData.HexToRgba(ColorData.Error),
                int n when n > 0 => (Color)ColorData.HexToRgba(ColorData.Green),
                _ => (Color)ColorData.HexToRgba(ColorData.Primary),
            };
            statScoreText.color = textColor;

            memoriTooltipTrigger.SetUpToolTip(
                _description: description,
                _delay: 0.15f
            );

            SetUpBars();
        }
        private void SetUpBars()
        {
            int2 sliderRanges = GetSliderRanges();

            if(totalBonus>0)
            {
                baseValueBar.minValue = sliderRanges.x;
                baseValueBar.maxValue = sliderRanges.y;
                baseValueBar.value = amount;

                bonusValueBar.minValue = sliderRanges.x;
                bonusValueBar.maxValue = sliderRanges.y;
                bonusValueBar.value = amount + totalBonus;
            }
            else
            {
                baseValueBar.minValue = sliderRanges.x;
                baseValueBar.maxValue = sliderRanges.y;
                baseValueBar.value = amount + totalBonus;

                bonusValueBar.minValue = sliderRanges.x;
                bonusValueBar.maxValue = sliderRanges.y;
                bonusValueBar.value = amount;
            }
            bonusValueBar.fillRect.GetComponent<Image>().color = totalBonus switch
            {
                int n when n < 0 => (Color)ColorData.HexToRgba(ColorData.Error),
                int n when n > 0 => (Color)ColorData.HexToRgba(ColorData.Green),
                _ => (Color)ColorData.HexToRgba(ColorData.Primary),
            };
        }
        private int2 GetSliderRanges()
        {
            switch(unitStat)
            {
                case UnitStat.MeleeAttack:
                    return new int2(0, 100);
                case UnitStat.MeleeDefense:
                    return new int2(0, 100);
                case UnitStat.WeaponStrength:
                    return new int2(0, 50);
                case UnitStat.Speed:
                    return new int2(0, 100);
                case UnitStat.Armor:
                    return new int2(0, 150);
                case UnitStat.Range:
                    return new int2(0, 200);
                case UnitStat.Accuracy:
                    return new int2(0, 100);
                case UnitStat.MissileStrength:
                    return new int2(0, 50);
                case UnitStat.ChargeBonus:
                    return new int2(0, 100);
                case UnitStat.Leadership:
                    return new int2(0, 100);
                case UnitStat.Ammunition:
                    return new int2(0, 1000);
                case UnitStat.ChargeImpactDamage:
                    return new int2(0, 100);
                default:
                    return new int2(0, 0);
            }
        }
    }
}
