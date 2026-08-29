using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Memori.SaveData;
using Memori.Utilities;
using Memori.Tooltip;
using Unity.Entities;
using Unity.Mathematics;
using Memori.Scenes;
using Memori.Localization;
using TJ.Morale;

namespace TJ
{
    [RequireComponent(typeof(UnitAttributesUIContainer), typeof(UnitStatsUIContainer))]
    public class SquadBattleInfo : MonoBehaviour, IPointerEnterHandler
    {
        [SerializeField] protected CanvasGroup tooltipCanvasGroup;
        [SerializeField] protected TMP_Text unitNameText, unitTypeText, unitCount, unitKillsText;
        [SerializeField] protected Image unitIcon;

        [Header("Health Bar")]
        [SerializeField] private TMP_Text healthbarText;

        // Optional. Only artillery and casters ever populate this, and the run-setup copies of this
        // panel have no live squad to read a timer from, so both are null-guarded throughout.
        [SerializeField] private GameObject cooldownGroup;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private MemoriTooltipTrigger cooldownTooltipTrigger;
        [SerializeField] private Color friendlyColor;
        [SerializeField] private Color enemyColor;
        [SerializeField] protected Slider healthBarSlider;
        [SerializeField] protected Image healthBarFillImage;

        [Header("Prestige")]
        [SerializeField] protected TMP_Text prestigeText;
        [SerializeField] protected MemoriTooltipTrigger prestigeTooltipTrigger;
        [SerializeField] protected GameObject bronzePrestige, silverPrestige, goldPrestige;

        [Header("Unit Rarity")]
        [SerializeField] private Image unitRarityImage;
        [SerializeField] private TMP_Text unitRarityText;

        [Header("Race Passive")]
        [SerializeField] private TMP_Text passiveNameText;
        [SerializeField] private TMP_Text passiveTitleText;
        [SerializeField] private Image raceColorImage1;
        [SerializeField] private Image raceColorImage2;
        [SerializeField] private MemoriTooltipTrigger passiveTooltipTrigger;

        [Header("Battlefield Attributes")]
        [SerializeField] private UnitAttributesUI inForestAttribute;
        [SerializeField] private UnitAttributesUI inSwampAttribute, isChargingAttribute, inCombatAttribute, isTerrifiedAttribute, isExhaustedAttribute, isOutOfAmmoAttribute, bloodFrenzyAttribute, rageAttribute, armorSunderedAttribute, isOnFireAttribute, garrisonDefenderAttribute, defendersResolveAttribute;

        int currentEntityCount, maxEntityCount, prestige, health, maxHealth, battlefieldBonusCount, lastCrashingHordeStacks = -1, lastDeathcryBonus = -1, lastHuntersPatienceBonus = -1, lastKenseiEyeStage = -1, lastOathcarvedDeaths = -1, lastApexHuntersStacks = -1, lastAmmunition = -1, lastHealth = -1, lastEntityCount = -1;
        UnitAttribute prestigeTrait;
        const float AMMO_REFRESH_INTERVAL = 0.5f;
        float ammoRefreshTimer;

        // A countdown wants a finer cadence than the ammo readout: at 0.5s a seconds figure visibly
        // jumps, and this is the one surface whose whole job is the precise number.
        const float COOLDOWN_REFRESH_INTERVAL = 0.1f;
        float cooldownRefreshTimer;
        SquadToLoad squadToLoad;
        SquadEntity squadEntity;
        public SquadEntity SquadEntity => squadEntity;
        UnitAttributesUIContainer unitAttributesUIContainer;
        UnitStatsUIContainer unitStatsUIContainer;
        SquadStats squadStats;
        Team team;
        bool applyGearBonuses = false, isCustomBattle = false;
        CampaignSaveData cachedSnapshot;

        private void Start()
        {
            isCustomBattle = SaveDataHandler.LoadPlayerSaveData().customBattle;
            if(!isCustomBattle)
                cachedSnapshot = SaveDataHandler.LoadSnapshot();
        }
        public void SetUpCampaign(SquadToLoad _squadToLoad, Team _team)
        {
            if(_squadToLoad.HitPointsPerUnit == 0)
            {
                Debug.LogError($"[SquadBattleInfo] SetUpCampaign: {_squadToLoad.UnitName} (team={_team}, index={_squadToLoad.UnitIndex}, health={_squadToLoad.SquadCurrentHealth}, maxUnits={_squadToLoad.maxUnitCount}) has HitPointsPerUnit=0 — defaulting to 1 to avoid divide-by-zero.");
                _squadToLoad.HitPointsPerUnit = 1;
            }

            isCustomBattle = false;
            squadToLoad = _squadToLoad;
            team = _team;
            currentEntityCount = squadToLoad.SquadCurrentHealth / squadToLoad.HitPointsPerUnit;
            if (squadToLoad.SquadCurrentHealth > 0 && currentEntityCount == 0) currentEntityCount = 1;
            maxEntityCount = squadToLoad.maxUnitCount;
            prestige = squadToLoad.UnitPrestige;
            prestigeTrait = squadToLoad.PrestigeTrait;
            healthBarFillImage.color = friendlyColor;
            applyGearBonuses = team == Team.Player;

            squadStats = TabletopTavernData.Instance.GetSquadStats(squadToLoad.UnitName);
            health = squadToLoad.SquadCurrentHealth;
            maxHealth = maxEntityCount * squadStats.HitPointsPerUnit;
            int displayCount = health / squadStats.HitPointsPerUnit;
            if (health > 0 && displayCount == 0) displayCount = 1;
            // Guard against drift between the squad's saved HitPointsPerUnit and the live squadStats value (e.g. after a balance change)
            displayCount = Mathf.Min(displayCount, maxEntityCount);
            unitCount.text = $"{displayCount} ({maxEntityCount})";

            GetHistoricalSquadKillCount();
            Load();
            TurnOffBattlefieldConditions();
        }
        public void SetUpCollection(SquadToLoad _squadToLoad, Team _team)
        {
            squadToLoad = _squadToLoad;
            team = _team;
            currentEntityCount = squadToLoad.SquadCurrentHealth / squadToLoad.HitPointsPerUnit;
            maxEntityCount = squadToLoad.maxUnitCount;
            prestige = squadToLoad.UnitPrestige;
            prestigeTrait = squadToLoad.PrestigeTrait;
            healthBarFillImage.color = friendlyColor;
            applyGearBonuses = team == Team.Player;

            squadStats = TabletopTavernData.Instance.GetSquadStats(squadToLoad.UnitName);
            health = squadToLoad.SquadCurrentHealth;
            maxHealth = maxEntityCount * squadStats.HitPointsPerUnit;
            unitCount.text = $"{maxEntityCount} ({maxEntityCount})";

            GetUnitNameHistoricalKillCount();
            Load();
            TurnOffBattlefieldConditions();
        }
        public void SetUpBattle(SquadEntity _squadEntity, int _currentEntityCount, int _prestige)
        {
            if (SettingsManager.Instance.HideSquadInfoInBattle.Value)
            {
                tooltipCanvasGroup.CGDisable();
                return;
            }
            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            team = _squadEntity.Team;
            if (team == Team.Player)
                cachedSnapshot = null;
            applyGearBonuses = team == Team.Player && !isCustomBattle;

            squadEntity = _squadEntity;
            currentEntityCount = _currentEntityCount;
            prestige = _prestige;
            prestigeTrait = BattleManager.Instance.SquadManager.GetSquadPrestigeTrait(squadEntity.SquadId);
            maxEntityCount = squadEntity.initialSquadSize;
            healthBarFillImage.color = squadEntity.SquadId > 0 ? friendlyColor : enemyColor;
            UpdateSquadKillCount();

            squadStats = TabletopTavernData.Instance.GetSquadStats(squadEntity.UnitName);

            //check dynamic buffer for batlefield bonuses
            if (entityManager.Exists(squadEntity.SelfEntity))
            {
                HandleBattlefieldConditions(entityManager, squadEntity);
                SquadStateComponent squadTotalHealth = entityManager.GetComponentData<SquadStateComponent>(squadEntity.SelfEntity);
                health = squadTotalHealth.CurrentHealthValue;
                maxHealth = squadTotalHealth.MaxHealthValue;
            }
            else
            {
                health = currentEntityCount * squadStats.HitPointsPerUnit;
                maxHealth = maxEntityCount * squadStats.HitPointsPerUnit;
            }

            Load();
        }
        public void SetUpSpawn(SquadStats _squadStats, int _prestige)
        {
            squadStats = _squadStats;
            currentEntityCount = _squadStats.baseUnitCount;
            maxEntityCount = _squadStats.baseUnitCount;

            health = currentEntityCount * squadStats.HitPointsPerUnit;
            maxHealth = maxEntityCount * squadStats.HitPointsPerUnit;

            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = healthBarSlider.maxValue;


            unitCount.text = $"{maxEntityCount} ({maxEntityCount})";
            prestige = _prestige;
            prestigeTrait = UnitAttribute.None;
            healthBarFillImage.color = friendlyColor;
            squadEntity = default;
            Load();
            TurnOffBattlefieldConditions();
        }
        private void Update()
        {
            if (squadEntity.SquadId == 0) return;
            if (tooltipCanvasGroup.alpha == 0) return;

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if (!entityManager.Exists(squadEntity.SelfEntity))
            {
                healthBarSlider.value = healthBarSlider.maxValue;
                healthbarText.text = $"{healthBarSlider.maxValue}";

                unitCount.text = $"{maxEntityCount} ({maxEntityCount})";
                return;
            }

            SquadStateComponent squadTotalHealth = entityManager.GetComponentData<SquadStateComponent>(squadEntity.SelfEntity);
            squadEntity = entityManager.GetComponentData<SquadEntity>(squadEntity.SelfEntity);
            currentEntityCount = entityManager.GetBuffer<EntityReferenceBufferElement>(squadEntity.SelfEntity).Length;
            if (currentEntityCount != lastEntityCount)
            {
                lastEntityCount = currentEntityCount;
                unitCount.text = $"{currentEntityCount} ({maxEntityCount})";
            }
            int currentBonusBufferSize = entityManager.GetBuffer<BattlefieldBonusBufferElement>(squadEntity.SelfEntity).Length;
            if (battlefieldBonusCount != currentBonusBufferSize)
            {
                battlefieldBonusCount = currentBonusBufferSize;
                unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
            }
            else if (entityManager.HasComponent<CrashingHordeComponent>(squadEntity.SelfEntity))
            {
                int currentWarbandStacks = entityManager.GetComponentData<CrashingHordeComponent>(squadEntity.SelfEntity).AppliedStacks;
                if (lastCrashingHordeStacks != currentWarbandStacks)
                {
                    lastCrashingHordeStacks = currentWarbandStacks;
                    unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                }
            }
            else if (entityManager.HasComponent<DeathcryComponent>(squadEntity.SelfEntity))
            {
                int currentDeathcryBonus = entityManager.GetComponentData<DeathcryComponent>(squadEntity.SelfEntity).AppliedBonus;
                if (lastDeathcryBonus != currentDeathcryBonus)
                {
                    lastDeathcryBonus = currentDeathcryBonus;
                    unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                }
            }
            else if (entityManager.HasComponent<HuntersPatienceComponent>(squadEntity.SelfEntity))
            {
                int currentPatienceBonus = entityManager.GetComponentData<HuntersPatienceComponent>(squadEntity.SelfEntity).CurrentBonus;
                if (lastHuntersPatienceBonus != currentPatienceBonus)
                {
                    lastHuntersPatienceBonus = currentPatienceBonus;
                    unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                }
            }
            else if (entityManager.HasComponent<KenseiEyeComponent>(squadEntity.SelfEntity))
            {
                int currentStage = entityManager.GetComponentData<KenseiEyeComponent>(squadEntity.SelfEntity).CurrentStage;
                if (lastKenseiEyeStage != currentStage)
                {
                    lastKenseiEyeStage = currentStage;
                    unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                }
            }
            else if (entityManager.HasComponent<OathcarvedComponent>(squadEntity.SelfEntity))
            {
                int currentDeaths = entityManager.GetComponentData<OathcarvedComponent>(squadEntity.SelfEntity).DeathCount;
                if (lastOathcarvedDeaths != currentDeaths)
                {
                    lastOathcarvedDeaths = currentDeaths;
                    unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                }
            }
            else if (entityManager.HasComponent<ApexHuntersComponent>(squadEntity.SelfEntity))
            {
                int currentStacks = entityManager.GetComponentData<ApexHuntersComponent>(squadEntity.SelfEntity).AppliedStacks;
                if (lastApexHuntersStacks != currentStacks)
                {
                    lastApexHuntersStacks = currentStacks;
                    unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                }
            }

            cooldownRefreshTimer += Time.deltaTime;
            if (cooldownRefreshTimer >= COOLDOWN_REFRESH_INTERVAL)
            {
                cooldownRefreshTimer = 0f;
                RefreshCooldown(entityManager);
            }

            //ranged/artillery ammo and mage charges only need to be checked a couple times a second, not every frame
            ammoRefreshTimer += Time.deltaTime;
            if (ammoRefreshTimer >= AMMO_REFRESH_INTERVAL)
            {
                ammoRefreshTimer = 0f;
                if (entityManager.HasComponent<SquadAmmunition>(squadEntity.SelfEntity))
                {
                    int currentAmmunition = entityManager.GetComponentData<SquadAmmunition>(squadEntity.SelfEntity).Value;
                    if (lastAmmunition != currentAmmunition)
                    {
                        lastAmmunition = currentAmmunition;
                        unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                    }
                }
            }

            health = squadTotalHealth.CurrentHealthValue;
            if (health != lastHealth)
            {
                lastHealth = health;
                healthBarSlider.value = health;
                healthbarText.text = $"{health}";
            }
        }
        // The precise counterpart to the squad flag's cooldown bar: the bar answers "roughly how
        // soon", this answers "how many seconds". Both go through SquadCooldown, so the two readouts
        // cannot drift apart or disagree about which unit is the representative.
        private void RefreshCooldown(EntityManager entityManager)
        {
            if (cooldownGroup == null) return;

            // TryGet reports false for anything without a cooldown worth showing, and for a spent
            // mage whose MageCast was stripped by SquadRanOutOfAmmoSystem, so the row retires itself
            // rather than freezing on its last value.
            if (!SquadCooldown.TryGet(entityManager, squadEntity.SelfEntity, squadStats.unitType,
                out _, out float secondsRemaining))
            {
                if (cooldownGroup.activeSelf) cooldownGroup.SetActive(false);
                return;
            }

            if (!cooldownGroup.activeSelf) cooldownGroup.SetActive(true);
            if (cooldownText == null) return;

            cooldownText.text = secondsRemaining <= 0f
                ? LocalizationManager.Instance.GetText("CooldownReady")
                : string.Format(LocalizationManager.Instance.GetText("CooldownSeconds"), secondsRemaining.ToString("F1"));
        }
        private void Load()
        {
            // Hidden up front so a squad with no cooldown never inherits the previously hovered
            // squad's value for the frames before RefreshCooldown next ticks.
            if (cooldownGroup != null) cooldownGroup.SetActive(false);

            // MemoriTooltipTrigger stores raw display strings rather than localization keys - the
            // neighbouring Unit Kills cell has literal English baked into the scene - so the text is
            // pushed in from here instead of authored, keeping it out of the binary scene.
            if (cooldownTooltipTrigger != null)
                cooldownTooltipTrigger.SetUpToolTip(
                    LocalizationManager.Instance.GetText("CooldownTitle"),
                    LocalizationManager.Instance.GetText("CooldownDesc"));

            // Debug.Log($"Loading SquadBattleInfo for {applyGearBonuses} applying gear bonuses.");
            unitAttributesUIContainer = GetComponent<UnitAttributesUIContainer>();
            unitAttributesUIContainer.Load(squadStats.unitName, applyGearBonuses, prestigeTrait);

            unitStatsUIContainer = GetComponent<UnitStatsUIContainer>();
            unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);

            string displayName = LocalizationManager.Instance.GetText(squadStats.unitName.ToString());
            if (team == Team.Player && !isCustomBattle)
            {
                if (cachedSnapshot == null) 
                    cachedSnapshot = SaveDataHandler.LoadSnapshotNullAllowed();

                if(cachedSnapshot != null && cachedSnapshot.playerArmy != null)
                {
                    string uniqueID = squadToLoad.UniqueID;
                    if (string.IsNullOrEmpty(uniqueID) && squadEntity.SquadId > 0)
                    {
                        int armyIndex = squadEntity.SquadId - 1;
                        if (armyIndex >= 0 && armyIndex < cachedSnapshot.playerArmy.Length)
                            uniqueID = cachedSnapshot.playerArmy[armyIndex].UniqueID;
                    }
                    if (!string.IsNullOrEmpty(uniqueID) && cachedSnapshot.unitNameOverrides != null)
                    {
                        UnitNameOverrides match = cachedSnapshot.unitNameOverrides.Find(x => x.unitGUID == uniqueID);
                        if (match.unitGUID != null)
                            displayName = match.unitNameOverride;
                    }
                } 
            }
            unitNameText.text = displayName;
            string unitTypeLocalised = LocalizationManager.Instance.GetText(squadStats.unitType.ToString());
            string unitSizeLocalised = (squadStats.unitSize != UnitSize.Artillery && squadStats.unitType != UnitType.Structure) ? " " + LocalizationManager.Instance.GetText(squadStats.unitSize.ToString()) : "";

            unitTypeText.text = $"{unitTypeLocalised}{unitSizeLocalised}";

            // unitCount.text = $"{TabletopTavernData.Instance.GetSquadCurrentUnitCount(squadToLoad)} ({maxEntityCount})";
            unitIcon.sprite = TabletopTavernData.Instance.GetSquadTypeIcon(squadStats.unitName);

            HandlePrestige();
            Color tierColor = ColorData.GetRarityTierColor(squadStats.RarityTier);
            unitRarityImage.color = tierColor;
            unitRarityText.text = LocalizationManager.Instance.GetText(squadStats.RarityTier.ToString());

            LoadRacePassive();

            tooltipCanvasGroup.CGEnable();

            //force refresh of ui
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            unitAttributesUIContainer.Refresh();
            unitStatsUIContainer.Refresh();

            healthbarText.text = $"{health}";
            healthBarSlider.maxValue = maxHealth;
            healthBarSlider.value = health;
        }
        private void LoadRacePassive()
        {
            Race race = TabletopTavernData.Instance.GetRaceFromUnitName(squadStats.unitName);
            RaceData raceData = TabletopTavernData.Instance.GetRaceData(race);

            Color passiveColor = ColorData.GetRacePassiveColor(race, raceData);
            raceColorImage1.color = passiveColor;
            raceColorImage2.color = ColorData.WithAlpha255(passiveColor, ColorData.GetRacePassiveAlpha(race));

            string campaignBonusTitle = LocalizationManager.Instance.GetText("Campaign Bonus");
            string campaignRaceTitle = LocalizationManager.Instance.GetText(race.ToString());
            passiveTitleText.text = $"{campaignBonusTitle} - {campaignRaceTitle}";
            string passiveName = LocalizationManager.Instance.GetText(race.ToString() + "PassiveName");
            string passiveDesc = RacePassiveInfo.GetDescription(race);
            passiveNameText.text = passiveName;

            passiveTooltipTrigger.SetUpToolTip(_title: passiveName, _description: passiveDesc);
        }
        private void HandlePrestige()
        {
            static string PrestigeRomanNumeral(int _prestige)
            {
                return _prestige switch
                {
                    0 => "I",
                    1 => "II",
                    2 => "III",
                    _ => "",
                };
            }
            prestigeText.text = PrestigeRomanNumeral(prestige);
            bronzePrestige.SetActive(prestige == 0);
            silverPrestige.SetActive(prestige == 1);
            goldPrestige.SetActive(prestige == 2);
            string prestigeLocalised = LocalizationManager.Instance.GetText("Prestige");
            prestigeTooltipTrigger.SetUpToolTip(_description: $"{prestigeLocalised}: " + PrestigeRomanNumeral(prestige));
        }
        public void InvalidateSnapshotCache()
        {
            cachedSnapshot = null;
        }
        public void Unhover()
        {
            if (tooltipCanvasGroup.alpha > 0)
            {
                tooltipCanvasGroup.CGDisable();
            }
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (UnitSelectionManager.Instance == null) return;
            if (!UnitSelectionManager.Instance.HasCursorMovedSinceHover()) return;
            BattleManager.Instance.UIManager.HideSquadHoveredTooltip();
        }
        public void GetHistoricalSquadKillCount()
        {
            // The state flips to Map before the Map scene finishes loading, so the manager can still
            // be absent here - resolve without creating one rather than fabricating an empty manager.
            CampaignManager campaignManager = SceneHandler.Instance.CurrentGameState == GameStateEnum.Map ? CampaignManager.InstanceIfExists : null;
            if (campaignManager == null)
            {
                unitKillsText.text = "";
                return;
            }
            unitKillsText.text = campaignManager.CampaignSaveManager.GetSquadHistoricalKillCount(squadToLoad.UniqueID).ToString();
        }
        public void GetUnitNameHistoricalKillCount()
        {
            unitKillsText.text = SaveDataHandler.GetUnitNameHistoricalKillCount(squadToLoad.UnitName).ToString();
        }
        public void UpdateSquadKillCount()
        {
            unitKillsText.text = BattleManager.Instance.ArmySpawnManager.GetSquadKillCount(squadEntity.SquadId).ToString();
        }
        private void HandleBattlefieldConditions(EntityManager entityManager, SquadEntity squadEntity)
        {
            inForestAttribute.gameObject.SetActive(entityManager.HasComponent<InForestTag>(squadEntity.SelfEntity));
            if (inForestAttribute.gameObject.activeSelf)
            {
                inForestAttribute.Load(UnitCondition.InForest);
            }
            inSwampAttribute.gameObject.SetActive(entityManager.HasComponent<InSwampTag>(squadEntity.SelfEntity));
            if (inSwampAttribute.gameObject.activeSelf)
            {
                inSwampAttribute.Load(UnitCondition.InSwamp);
            }
            inCombatAttribute.gameObject.SetActive(entityManager.HasComponent<InCombat>(squadEntity.SelfEntity));
            if (inCombatAttribute.gameObject.activeSelf)
            {
                inCombatAttribute.Load(UnitCondition.InCombat);
            }
            isChargingAttribute.gameObject.SetActive(entityManager.HasComponent<ChargeBonus>(squadEntity.SelfEntity));
            if (isChargingAttribute.gameObject.activeSelf)
            {
                isChargingAttribute.Load(UnitCondition.IsCharging);
            }
            isTerrifiedAttribute.gameObject.SetActive(entityManager.IsComponentEnabled<IsTerrified>(squadEntity.SelfEntity));
            if (isTerrifiedAttribute.gameObject.activeSelf)
            {
                isTerrifiedAttribute.Load(UnitCondition.IsTerrified);
            }
            isExhaustedAttribute.gameObject.SetActive(entityManager.HasComponent<ExhaustedTag>(squadEntity.SelfEntity));
            if (isExhaustedAttribute.gameObject.activeSelf)
            {
                isExhaustedAttribute.Load(UnitCondition.IsExhausted);
                isChargingAttribute.gameObject.SetActive(false);
            }
            isOutOfAmmoAttribute.gameObject.SetActive(entityManager.HasComponent<AmmuntionSpent>(squadEntity.SelfEntity));
            if (isOutOfAmmoAttribute.gameObject.activeSelf)
            {
                isOutOfAmmoAttribute.Load(UnitCondition.IsOutOfAmmo);
            }
            bloodFrenzyAttribute.gameObject.SetActive(entityManager.HasComponent<BloodFrenzyActiveTag>(squadEntity.SelfEntity));
            if (bloodFrenzyAttribute.gameObject.activeSelf)
            {
                bloodFrenzyAttribute.Load(UnitAttribute.BloodFrenzy);
            }
            bool isRageActive = entityManager.HasComponent<RageActiveTag>(squadEntity.SelfEntity);
            bool isSlayerActive = entityManager.HasComponent<SlayerActiveTag>(squadEntity.SelfEntity);
            rageAttribute.gameObject.SetActive(isRageActive || isSlayerActive);
            if (rageAttribute.gameObject.activeSelf)
            {
                rageAttribute.Load(UnitAttribute.Rage);
            }
            armorSunderedAttribute.gameObject.SetActive(entityManager.HasComponent<ArmorSunderedTag>(squadEntity.SelfEntity));
            if (armorSunderedAttribute.gameObject.activeSelf)
            {
                armorSunderedAttribute.Load(UnitAttribute.Emblazing);
            }
            isOnFireAttribute.gameObject.SetActive(entityManager.IsComponentEnabled<TakingFireDamage>(squadEntity.SelfEntity));
            if (isOnFireAttribute.gameObject.activeSelf)
            {
                isOnFireAttribute.Load(UnitAttribute.IsOnFire);
            }
            garrisonDefenderAttribute.gameObject.SetActive(entityManager.HasComponent<GarrisonDefenderComponent>(squadEntity.SelfEntity));
            if (garrisonDefenderAttribute.gameObject.activeSelf)
                garrisonDefenderAttribute.Load(UnitCondition.GarrisonDefender);
            defendersResolveAttribute.gameObject.SetActive(entityManager.HasComponent<DefendersResolveComponent>(squadEntity.SelfEntity));
            if (defendersResolveAttribute.gameObject.activeSelf)
                defendersResolveAttribute.Load(UnitCondition.DefendersResolve);
        }
        private void TurnOffBattlefieldConditions()
        {
            inForestAttribute.gameObject.SetActive(false);
            inSwampAttribute.gameObject.SetActive(false);
            inCombatAttribute.gameObject.SetActive(false);
            isChargingAttribute.gameObject.SetActive(false);
            isTerrifiedAttribute.gameObject.SetActive(false);
            isExhaustedAttribute.gameObject.SetActive(false);
            isOutOfAmmoAttribute.gameObject.SetActive(false);
            bloodFrenzyAttribute.gameObject.SetActive(false);
            rageAttribute.gameObject.SetActive(false);
            armorSunderedAttribute.gameObject.SetActive(false);
            isOnFireAttribute.gameObject.SetActive(false);
            garrisonDefenderAttribute.gameObject.SetActive(false);
            defendersResolveAttribute.gameObject.SetActive(false);
        }
    }
}
