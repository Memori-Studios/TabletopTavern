using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using Unity.Entities;
using Memori.Localization;
using TJ.Morale;
using TJ.Spells;
using Memori.Utilities;

namespace TJ
{
    [RequireComponent(typeof(MemoriCanvasGroup))]
    public class SquadHoveredTooltip : MonoBehaviour
    {
        [SerializeField] private Image factionImage, troopTypeImage;
        [SerializeField] private TMP_Text troopNameText, troopTypeText, unitCountText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image squadFactionColorImage;
        // [SerializeField] private Transform unitAttributesParent;
        // [SerializeField] private UnitAttributesUI unitAttributePrefab;
        [SerializeField] private GameObject isChargingGO, inCombatGO, isTerrifiedGO, inForestGO, inSwampGO, chargeBonusCooldownGO, exhaustedGO, bloodFrenzyGO, rageGO, armorSunderedGO, attackedInFlanksGO, onFireGO, defensiveStanceGO, bracedGO, retreatingAlliesGO, garrisonDefenderGO, defendersResolveGO;
        // Template for the spell rows below the statuses; never shown itself.
        [SerializeField] private GameObject huntersMarkGO;

        [Header("Combat Status Indicators")]
        [SerializeField] private TMP_Text _combatStatusText;
        [SerializeField] private Image _winningImage, _losingImage, _neutralImage;
        MemoriCanvasGroup squadHoverPopup;
        SquadEntity squadEntity;
        bool shown, loading;
        EntityManager entityManager;
        RectTransform rt;

        Color _winningOff, _losingOff;

        private void Awake()
        {
            squadHoverPopup = GetComponent<MemoriCanvasGroup>();
            rt = transform as RectTransform;
            _winningOff = _winningImage.color;
            _losingOff = _losingImage.color;
        }
        public void Load(SquadEntity _squadEntity)
        {
            loading = true;
            // Debug.Log($"Loading squad tooltip for {_squadEntity.UnitName}");
            squadEntity = _squadEntity;

            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            squadEntity = entityManager.GetComponentData<SquadEntity>(squadEntity.SelfEntity);
            SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(squadEntity.UnitName);
            bool playerFaction = squadEntity.Team == Team.Player;

            factionImage.sprite = SpriteData.GetSprite($"{(playerFaction ? "playerFaction" : "enemyFaction")}");
            Color color = ColorData.HexToRgba(playerFaction ? ColorData.Player : ColorData.Enemy);
            color.a = 100/255f;
            squadFactionColorImage.color = color;

            unitCountText.text = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity.SelfEntity).Length.ToString();

            troopNameText.text = LocalizationManager.Instance.GetText(squadEntity.UnitName.ToString());

            string unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squadEntity.UnitName).ToString();
            string localizedTroopTypeText = LocalizationManager.Instance.GetText(unitType);
            troopTypeText.text = localizedTroopTypeText;
            troopTypeImage.sprite = TabletopTavernData.Instance.GetSquadTypeIcon(squadEntity.UnitName);

            DisplayUnitStatuses(squadStats);
        }
        public void Update()
        {
            if(!shown) return;
            
            Vector3 mousePos = transform.parent.position;
            // 250 canvas units, so the flip point follows the UI scale instead of raw pixels.
            if(mousePos.y < 250f * rt.lossyScale.y) {
                rt.pivot = new Vector2(rt.pivot.x, 0);
            } else {
                rt.pivot = new Vector2(rt.pivot.x, 1.25f);
            }
        }
        public void Unhover()
        {
            if(!shown) return;
        
            shown = false;
            squadHoverPopup.CGDisable();
        }
        public void Hover()
        {
            if(shown || loading) return;

            shown = true;
            squadHoverPopup.FadeInAsync(0.1f, false, false);
        }
        private void DisplayUnitStatuses(SquadStats squadStats)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if(entityManager == null) return;
            if(!entityManager.Exists(squadEntity.SelfEntity)) return;

            squadEntity = entityManager.GetComponentData<SquadEntity>(squadEntity.SelfEntity);
            SquadStateComponent squadTotalHealth = entityManager.GetComponentData<SquadStateComponent>(squadEntity.SelfEntity);
            healthSlider.maxValue = squadTotalHealth.MaxHealthValue;
            healthSlider.value = squadTotalHealth.CurrentHealthValue;

            // Update unit status indicators
            inCombatGO.SetActive(entityManager.HasComponent<InCombat>(squadEntity.SelfEntity));
            if(inCombatGO.activeSelf)
            {
                if(entityManager.HasComponent<HealthLossPercent>(squadEntity.SelfEntity))
                {
                    CombatStatus combatStatus = entityManager.GetComponentData<HealthLossPercent>(squadEntity.SelfEntity).CombatStatus;

                    _winningImage.gameObject.SetActive(combatStatus == CombatStatus.Winning);
                    _losingImage.gameObject.SetActive(combatStatus == CombatStatus.Losing);
                    _winningImage.color = ColorVision.Good(_winningOff);
                    _losingImage.color = ColorVision.Bad(_losingOff);
                    _neutralImage.gameObject.SetActive(combatStatus == CombatStatus.None);
                    string localizedCombatStatusText = LocalizationManager.Instance.GetText("CombatStatus" + combatStatus.ToString());
                    _combatStatusText.text = localizedCombatStatusText;
                }
            }
            isChargingGO.SetActive(entityManager.HasComponent<SprintingTag>(squadEntity.SelfEntity) || entityManager.HasComponent<ChargeBonus>(squadEntity.SelfEntity));
            isTerrifiedGO.SetActive(entityManager.IsComponentEnabled<IsTerrified>(squadEntity.SelfEntity));
            inForestGO.SetActive(entityManager.HasComponent<InForestTag>(squadEntity.SelfEntity));
            inSwampGO.SetActive(entityManager.HasComponent<InSwampTag>(squadEntity.SelfEntity));
            bloodFrenzyGO.SetActive(entityManager.HasComponent<BloodFrenzyActiveTag>(squadEntity.SelfEntity));
            rageGO.SetActive(entityManager.HasComponent<RageActiveTag>(squadEntity.SelfEntity) || entityManager.HasComponent<SlayerActiveTag>(squadEntity.SelfEntity));
            armorSunderedGO.SetActive(entityManager.HasComponent<ArmorSunderedTag>(squadEntity.SelfEntity));
            attackedInFlanksGO.SetActive(entityManager.IsComponentEnabled<TakingFlankingDamage>(squadEntity.SelfEntity));
            onFireGO.SetActive(entityManager.IsComponentEnabled<TakingFireDamage>(squadEntity.SelfEntity));
            bracedGO.SetActive(entityManager.IsComponentEnabled<BracedTag>(squadEntity.SelfEntity));
            defensiveStanceGO.SetActive(entityManager.IsComponentEnabled<DefensiveStanceTag>(squadEntity.SelfEntity));
            retreatingAlliesGO.SetActive(entityManager.IsComponentEnabled<RetreatingNearbyAllies>(squadEntity.SelfEntity));
            garrisonDefenderGO.SetActive(entityManager.HasComponent<GarrisonDefenderComponent>(squadEntity.SelfEntity));
            defendersResolveGO.SetActive(entityManager.HasComponent<DefendersResolveComponent>(squadEntity.SelfEntity));
            
            exhaustedGO.SetActive(entityManager.HasComponent<WearyTag>(squadEntity.SelfEntity));

            DisplaySpellEffects();

            LayoutRebuilder.ForceRebuildLayoutImmediate(this.transform as RectTransform);
            loading = false;
        }

        #region Spell effects
        // One block per lasting spell on the squad, the way Total War lists an active ability: the spell's
        // icon and name, then one indented line per effect. Rows are runtime clones of huntersMarkGO.
        private const float EFFECT_INDENT = 16f;
        private static readonly Color SpellNameColor = new(0.95f, 0.92f, 0.84f);
        private readonly List<GameObject> spellRows = new();
        private Vector2 templateIconPosition, templateTextPosition, templateTextSize;
        private float templateFontMax;
        // The section's last child is an empty spacer (min height 50, offset by the section's -20 bottom
        // padding) that gives the panel its bottom margin; spell rows must sit above it, not below.
        private Transform bottomSpacer;

        private struct EffectLine
        {
            public string Icon;
            public string Text;
            public bool Good;
        }
        private static readonly List<EffectLine> effectLines = new();

        private void DisplaySpellEffects()
        {
            if (huntersMarkGO == null) return;
            huntersMarkGO.SetActive(false);

            int used = 0;
            // The only battle readout of the Prestige III trait while Hide Unit Info in Battle is on.
            UnitAttribute prestigeTrait = BattleManager.Instance.SquadManager.GetSquadPrestigeTrait(squadEntity.SquadId);
            if (prestigeTrait != UnitAttribute.None)
            {
                Color gold = ColorData.HexToRgba(ColorData.Gold);
                SetSpellRow(used++, SpriteData.GetSprite("Prestige"), gold, LocalizationManager.Instance.GetText(prestigeTrait.ToString()), gold, false);
            }
            if (entityManager.HasBuffer<SpellStatusBufferElement>(squadEntity.SelfEntity))
            {
                DynamicBuffer<SpellStatusBufferElement> buffer = entityManager.GetBuffer<SpellStatusBufferElement>(squadEntity.SelfEntity, true);
                double now = World.DefaultGameObjectInjectionWorld.Time.ElapsedTime;
                for (int i = 0; i < buffer.Length; i++)
                {
                    // Expired entries stay until a writer prunes them, as in SquadFlagGameObject.HandleSpellStatus.
                    if (buffer[i].ExpiresAtTime <= now) continue;
                    if (!SpellStatusIcons.TryGetData(buffer[i].SpellId, out SpellData spell)) continue;

                    SetSpellRow(used++, spell.SpellSprite, ColorData.GetRaceDisplayColor(spell.Race),
                        LocalizationManager.Instance.GetText(spell.Spell.ToString()), SpellNameColor, false);
                    foreach (EffectLine line in EffectLinesFor(spell))
                    {
                        Color colour = ColorData.HexToRgba(line.Good ? ColorData.Green : ColorData.Error);
                        SetSpellRow(used++, SpriteData.GetSprite(line.Icon), colour, line.Text, colour, true);
                    }
                }
            }
            for (int i = used; i < spellRows.Count; i++) spellRows[i].SetActive(false);
            if (bottomSpacer != null) bottomSpacer.SetAsLastSibling();
        }

        private void SetSpellRow(int index, Sprite icon, Color iconColour, string text, Color textColour, bool indented)
        {
            if (index == spellRows.Count)
            {
                if (spellRows.Count == 0) CacheTemplateLayout();
                GameObject clone = Instantiate(huntersMarkGO, huntersMarkGO.transform.parent);
                // The text is set from code; the template's localizer would put its own string back on enable.
                foreach (LocalizeStringEvent localizer in clone.GetComponentsInChildren<LocalizeStringEvent>(true))
                    localizer.enabled = false;
                // The template's animator owns the row's colours, which would undo the ones set here.
                if (clone.TryGetComponent(out Animator animator)) animator.enabled = false;
                spellRows.Add(clone);
            }

            GameObject row = spellRows[index];
            row.SetActive(true);
            // Rows are filled in display order, so moving each to the end keeps them in that order below the statuses.
            row.transform.SetAsLastSibling();

            float indent = indented ? EFFECT_INDENT : 0f;
            TMP_Text label = row.GetComponentInChildren<TMP_Text>(true);
            label.text = text;
            label.color = textColour;
            label.fontSizeMax = indented ? templateFontMax - 2f : templateFontMax;
            label.rectTransform.anchoredPosition = templateTextPosition + new Vector2(indent * 0.5f, 0f);
            label.rectTransform.sizeDelta = templateTextSize - new Vector2(indent, 0f);

            Image iconImage = RowIcon(row);
            if (iconImage == null) return;
            iconImage.enabled = icon != null;
            iconImage.sprite = icon;
            iconImage.color = iconColour;
            iconImage.rectTransform.anchoredPosition = templateIconPosition + new Vector2(indent, 0f);
        }

        private void CacheTemplateLayout()
        {
            Transform section = huntersMarkGO.transform.parent;
            bottomSpacer = section.GetChild(section.childCount - 1);
            TMP_Text label = huntersMarkGO.GetComponentInChildren<TMP_Text>(true);
            templateTextPosition = label.rectTransform.anchoredPosition;
            templateTextSize = label.rectTransform.sizeDelta;
            templateFontMax = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            Image iconImage = RowIcon(huntersMarkGO);
            if (iconImage != null) templateIconPosition = iconImage.rectTransform.anchoredPosition;
        }

        private static Image RowIcon(GameObject row)
        {
            foreach (Image image in row.GetComponentsInChildren<Image>(true))
                if (image.name == "Icon") return image;
            return null;
        }

        // What one spell does to the squad it sits on, as "Stat: +5" lines. Stat labels are the card's own
        // stat names; rates and marks reuse the spell card's keys.
        private static List<EffectLine> EffectLinesFor(SpellData spell)
        {
            effectLines.Clear();
            LocalizationManager loc = LocalizationManager.Instance;

            void Add(string icon, string labelKey, string value, bool good) => effectLines.Add(new EffectLine
            {
                Icon = icon,
                Text = value == null ? loc.GetText(labelKey) : string.Format(loc.GetText("SpellEffectLine"), loc.GetText(labelKey), value),
                Good = good
            });
            static string Signed(float value, string suffix = "") => (value > 0f ? "+" : "") + Mathf.RoundToInt(value) + suffix;

            if (spell.HealsInsteadOfDamage)
            {
                Add("Health", "SpellStatHealingPerSecond", Signed(spell.SpellModifierValue, spell.HealsPercentOfMaxHealth ? "%" : ""), true);
                return effectLines;
            }
            if (spell.MarksTarget)
            {
                Add("MissileStrength", "SpellStatDamageTaken", Signed(spell.SpellModifierValue, "%"), false);
                return effectLines;
            }
            if (spell.BracesTarget)
            {
                Add("HasShield", "SpellStatNoKnockback", null, true);
                Add("Speed", "Speed", "-50%", false);
                return effectLines;
            }

            if (spell.BonusStats != null)
                foreach (SpellBonusStat bonus in spell.BonusStats)
                {
                    // Armour is a mitigation fraction and Speed a locomotion value; the card shows both scaled.
                    string value = bonus.UnitStat == UnitStat.Armor ? Signed(bonus.Value * 100f, "%")
                        : bonus.UnitStat == UnitStat.Speed ? Signed(bonus.Value * 10f)
                        : Signed(bonus.Value);
                    Add(bonus.UnitStat.ToString(), bonus.UnitStat.ToString(), value, bonus.Value > 0f);
                }

            if (spell.GrantsBattlefieldBonus && spell.BonusType == BattlefieldBonusEnum.LesserMoraleSpell)
                Add("Leadership", "SpellStatMoralePerSecond", Signed(spell.SpellModifierValue), spell.SpellModifierValue > 0f);
            else if (spell.GrantsBattlefieldBonus && spell.BonusType == BattlefieldBonusEnum.RallyTheBanners)
                Add("ChargeBonus", "ChargeBonus", Signed(spell.SpellModifierValue), true);
            else if (spell.GrantsBattlefieldBonus && spell.BonusType != BattlefieldBonusEnum.SpellStatBonus && spell.BonusType != BattlefieldBonusEnum.None)
                Add(spell.BonusUnitStat.ToString(), spell.BonusUnitStat.ToString(), Signed(spell.SpellModifierValue), spell.SpellModifierValue > 0f);
            else if (!spell.GrantsBattlefieldBonus && spell.SpellModifierValue > 0)
                Add("Health", "SpellStatDamagePerSecond", Mathf.RoundToInt(spell.SpellModifierValue).ToString(), false);

            return effectLines;
        }
        #endregion
    }
}
