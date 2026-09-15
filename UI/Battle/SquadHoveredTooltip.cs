using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Entities;
using Memori.Localization;
using TJ.Morale;

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
        [SerializeField] private GameObject huntersMarkGO;

        [Header("Combat Status Indicators")]
        [SerializeField] private TMP_Text _combatStatusText;
        [SerializeField] private Image _winningImage, _losingImage, _neutralImage;
        MemoriCanvasGroup squadHoverPopup;
        SquadEntity squadEntity;
        bool shown, loading;
        EntityManager entityManager;
        RectTransform rt;

        private void Awake()
        {
            squadHoverPopup = GetComponent<MemoriCanvasGroup>();
            rt = transform as RectTransform;
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
            if(mousePos.y < 250) {
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
                    _neutralImage.gameObject.SetActive(combatStatus == CombatStatus.None);
                    string localizedCombatStatusText = LocalizationManager.Instance.GetText("CombatStatus" + combatStatus.ToString());
                    _combatStatusText.text = localizedCombatStatusText;
                }
            }
            isChargingGO.SetActive(entityManager.HasComponent<ChargeBonus>(squadEntity.SelfEntity));
            isTerrifiedGO.SetActive(entityManager.IsComponentEnabled<IsTerrified>(squadEntity.SelfEntity));
            inForestGO.SetActive(entityManager.HasComponent<InForestTag>(squadEntity.SelfEntity));
            inSwampGO.SetActive(entityManager.HasComponent<InSwampTag>(squadEntity.SelfEntity));
            bloodFrenzyGO.SetActive(entityManager.HasComponent<BloodFrenzyActiveTag>(squadEntity.SelfEntity));
            rageGO.SetActive(entityManager.HasComponent<RageActiveTag>(squadEntity.SelfEntity) || entityManager.HasComponent<SlayerActiveTag>(squadEntity.SelfEntity));
            armorSunderedGO.SetActive(entityManager.HasComponent<ArmorSunderedTag>(squadEntity.SelfEntity));
            if (huntersMarkGO != null) huntersMarkGO.SetActive(entityManager.HasComponent<HuntersMarkTag>(squadEntity.SelfEntity));
            attackedInFlanksGO.SetActive(entityManager.IsComponentEnabled<TakingFlankingDamage>(squadEntity.SelfEntity));
            onFireGO.SetActive(entityManager.IsComponentEnabled<TakingFireDamage>(squadEntity.SelfEntity));
            bracedGO.SetActive(entityManager.IsComponentEnabled<BracedTag>(squadEntity.SelfEntity));
            defensiveStanceGO.SetActive(entityManager.IsComponentEnabled<DefensiveStanceTag>(squadEntity.SelfEntity));
            retreatingAlliesGO.SetActive(entityManager.IsComponentEnabled<RetreatingNearbyAllies>(squadEntity.SelfEntity));
            garrisonDefenderGO.SetActive(entityManager.HasComponent<GarrisonDefenderComponent>(squadEntity.SelfEntity));
            defendersResolveGO.SetActive(entityManager.HasComponent<DefendersResolveComponent>(squadEntity.SelfEntity));
            
            bool isExhausted = entityManager.HasComponent<ExhaustedTag>(squadEntity.SelfEntity);
            if(isExhausted) {
                isChargingGO.SetActive(false);
            }
            exhaustedGO.SetActive(isExhausted && squadStats.unitName != UnitName.Gate);
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(this.transform as RectTransform);
            loading = false;
        }
    }
}
