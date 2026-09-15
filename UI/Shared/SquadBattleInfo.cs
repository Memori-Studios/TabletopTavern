using System.Collections.Generic;
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

        // Only casters populate these, and the run-setup / collection copies of this panel are
        // authored separately, so every field is null-guarded exactly like the cooldown row above.
        [Header("Mage Spell")]
        [SerializeField] private GameObject spellGroup;
        [SerializeField] private TMP_Text spellTitleText;
        [SerializeField] private TMP_Text spellDescriptionText;
        [SerializeField] private Image spellIcon;
        [SerializeField] private Image spellAccentImage;
        [SerializeField] private Image spellRaceRailImage;
        [SerializeField] private Image spellRaceGradientImage;
        [SerializeField] private MemoriTooltipTrigger spellTooltipTrigger;
        // The "+ Spell: Smite" line that sits in the attribute list alongside "+ Shielded". It has
        // no UnitAttributesUI component on purpose, so UnitAttributesUIContainer's pool - which
        // finds its children with GetComponentsInChildren<UnitAttributesUI> - never adopts or
        // destroys it.
        [SerializeField] private GameObject spellAttributeLine;
        [SerializeField] private TMP_Text spellAttributeText;
        // Holds the spell card out in the right-hand column. Deliberately NOT parented under the
        // bonus stack: that stack is hidden by scaling its parent to zero, and this card stays
        // visible whether or not the panel is hovered.
        [SerializeField] private RectTransform spellBonusRoot;

        [Header("Mage Spell Stats")]
        // Small uppercase line under the spell name: "Spell - Area of effect".
        [SerializeField] private TMP_Text spellCategoryText;
        [SerializeField] private TMP_Text spellStatsTitleText;
        [SerializeField] private SpellStatRowUI spellStatRowPrefab;
        [SerializeField] private Transform spellStatRowsParent;
        // The stat sprites live in Resources and go through SpriteData; these three do not.
        [SerializeField] private Sprite spellAreaSprite;
        [SerializeField] private Sprite spellCooldownSprite;
        [SerializeField] private Sprite spellDurationSprite;
        [SerializeField] private Sprite spellChargesSprite;
        [SerializeField] private Sprite spellDamageSprite;
        // The "+ Spell: Smite" attribute line, restyled as a small faction-tinted block.
        [SerializeField] private Image spellAttributeIcon;
        [SerializeField] private Image spellAttributeBackground;
        [SerializeField] private Image spellAttributeRail;

        private readonly List<SpellStatRowUI> spellStatRows = new();
        private SpellStatRowUI spellChargesRow;
        private int spellMaxCharges;

        // Bar lengths are a reading aid, not a scale that exists anywhere else: "big for a spell"
        // rather than a fraction of some real maximum. Tuned against the shipped spell assets.
        private const float SPELL_BAR_MAX_DAMAGE = 300f;
        private const float SPELL_BAR_MAX_HEALING = 40f;
        private const float SPELL_BAR_MAX_STAT = 50f;
        private const float SPELL_BAR_MAX_PERCENT = 100f;
        private const float SPELL_BAR_MAX_AREA = 30f;
        private const float SPELL_BAR_MAX_RANGE = 100f;
        private const float SPELL_BAR_MAX_COOLDOWN = 60f;
        private const float SPELL_BAR_MAX_DURATION = 30f;

        // Matches SpellLoadoutSlot / SpellBrowseSlot, so a spell reads the same here, in the
        // grimoire, in its loadout slot and on the battle hotbar.
        private const float SPELL_RAIL_ALPHA = 0.9f;
        // Mirrors the VerticalLayoutGroup spacing on Unit Bonuses Parent.
        private const float BONUS_STACK_SPACING = 10f;

        [Header("Battlefield Attributes")]
        [SerializeField] private UnitAttributesUI inForestAttribute;
        [SerializeField] private UnitAttributesUI inSwampAttribute, isChargingAttribute, inCombatAttribute, isTerrifiedAttribute, isExhaustedAttribute, isOutOfAmmoAttribute, bloodFrenzyAttribute, rageAttribute, armorSunderedAttribute, isOnFireAttribute, garrisonDefenderAttribute, defendersResolveAttribute;
        // Hunter's Mark badge. Optional because the run-setup copies of this panel have no live squad.
        [SerializeField] private UnitAttributesUI huntersMarkAttribute;

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
                RefreshHuntersMark(entityManager);
                if (entityManager.HasComponent<SquadAmmunition>(squadEntity.SelfEntity))
                {
                    int currentAmmunition = entityManager.GetComponentData<SquadAmmunition>(squadEntity.SelfEntity).Value;
                    if (lastAmmunition != currentAmmunition)
                    {
                        lastAmmunition = currentAmmunition;
                        unitStatsUIContainer.Load(squadStats.unitName, applyGearBonuses, prestige, prestigeTrait);
                        if (spellChargesRow != null)
                            spellChargesRow.SetChargeCount(Mathf.Clamp(currentAmmunition, 0, spellMaxCharges));
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
            LoadMageSpell();

            tooltipCanvasGroup.CGEnable();

            //force refresh of ui
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            unitAttributesUIContainer.Refresh();
            unitStatsUIContainer.Refresh();

            // Needs the bonus boxes to have been laid out, so it runs after the rebuild above.
            PositionSpellBonus();

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
        private void LoadMageSpell()
        {
            // Gate on the predicate rather than a bare unitType comparison, so a future caster
            // type is covered for free.
            bool casts = TabletopTavernConstants.Casts(squadStats.unitType);

            TJ.Spells.SpellData spell = null;
            if (casts)
            {
                spell = TabletopTavernData.Instance.SquadAssetsDictionary[squadStats.unitName].mageSpell;
                // EntityWatcher already logs this authoring error loudly at spawn, so stay quiet here.
            }

            if (spell == null)
            {
                ShowSpellUI(false);
                return;
            }

            ShowSpellUI(true);
            // Already run through ColorData.XMLTagColorApplicator - do not apply it a second time.
            RenderSpellCard(spell, LocalizationManager.Instance.GetText, spell.GetLocalizedSpellDescription());
        }

        /// <summary>
        /// Paints the spell card and the attribute line from a resolved spell. Text comes through
        /// <paramref name="text"/> rather than LocalizationManager directly so the Editor preview
        /// below can feed it the en table without waking a phantom manager outside Play mode.
        /// </summary>
        private void RenderSpellCard(TJ.Spells.SpellData spell, System.Func<string, string> text, string spellDescription)
        {
            string spellName = text(spell.Spell.ToString());
            string spellLabel = text("Spell");
            // Name on its own, with the "Spell - Area of effect" reading moved to the line beneath.
            if (spellTitleText != null) spellTitleText.text = spellName;
            if (spellCategoryText != null)
            {
                // Shape, not targeting: a mage spell is aimed at a squad but lands as an area.
                string targeting = text(spell.SpellType == TJ.Spells.SpellType.AOE ? "SpellTargeting_World" : "SpellTargeting_Squad");
                spellCategoryText.text = $"{spellLabel} - {targeting}";
            }
            if (spellDescriptionText != null) spellDescriptionText.text = spellDescription;

            // Display pair, not the passive pair: the icons are white sprites and the passive
            // colours are banner fills, four of which are too dark to read as a glyph.
            Color factionColour = ColorData.GetRaceDisplayColor(spell.Race);

            if (spellIcon != null)
            {
                spellIcon.sprite = spell.SpellSprite;
                spellIcon.color = ColorData.HexToRgba(ColorData.Primary);
            }

            // The glow disc behind the icon, the rail and the wash all carry the faction. Tinting
            // the disc here is what stops it keeping the red it inherited from the faction block.
            if (spellAccentImage != null) spellAccentImage.color = factionColour;
            if (spellRaceRailImage != null)
                spellRaceRailImage.color = ColorData.WithAlpha255(factionColour, SPELL_RAIL_ALPHA * 255f);
            if (spellRaceGradientImage != null)
                spellRaceGradientImage.color = ColorData.GetRaceDisplayTint(spell.Race);

            if (spellTooltipTrigger != null)
                spellTooltipTrigger.SetUpToolTip(_title: spellName, _description: spellDescription);

            if (spellAttributeText != null)
            {
                spellAttributeText.text = $"{spellLabel}: {spellName}";
                // The line reads as a label like its "+ Trait" neighbours; the block behind it and
                // the icon carry the faction, the text stays the UI's primary text colour.
                spellAttributeText.color = (Color)ColorData.HexToRgba(ColorData.Primary);
            }
            if (spellAttributeIcon != null)
            {
                spellAttributeIcon.sprite = spell.SpellSprite;
                spellAttributeIcon.color = factionColour;
            }
            // Hue only - the strength of the wash is authored on the prefab.
            if (spellAttributeBackground != null)
                spellAttributeBackground.color = ColorData.WithAlpha255(factionColour, spellAttributeBackground.color.a * 255f);
            if (spellAttributeRail != null) spellAttributeRail.color = factionColour;

            // The attribute pool instantiates its entries into this parent, so anything authored
            // there starts out above them. Push this line back to the bottom every load.
            if (spellAttributeLine != null) spellAttributeLine.transform.SetAsLastSibling();

            LoadSpellStatRows(spell, text);
        }

        #region Spell stat rows
        /// <summary>
        /// Fills the "Spell Stats" list under the description: what the spell does, how wide, how far
        /// the caster reaches, how often, and how many charges are left. Rows are pooled like the unit
        /// stat list. Nothing here is a new number - every value is read off the SpellData asset or the
        /// caster's SquadStats, so the card cannot disagree with what the spell actually does.
        /// </summary>
        private void LoadSpellStatRows(TJ.Spells.SpellData spell, System.Func<string, string> text)
        {
            spellChargesRow = null;
            if (spellStatRowPrefab == null || spellStatRowsParent == null) return;

            // Adopt rows already under the parent: the pool list is not serialized, so after a domain
            // reload (or an Editor preview that was not cleared) they would otherwise be doubled.
            if (spellStatRows.Count == 0)
                spellStatRows.AddRange(spellStatRowsParent.GetComponentsInChildren<SpellStatRowUI>(true));

            if (spellStatsTitleText != null)
                spellStatsTitleText.text = text("SpellStatsTitle");

            Color iconColour = (Color)ColorData.GetUnitStatColor(UnitStat.Range);
            int used = 0;

            SpellStatRowUI NextRow()
            {
                if (used >= spellStatRows.Count)
                    spellStatRows.Add(Instantiate(spellStatRowPrefab, spellStatRowsParent));
                SpellStatRowUI row = spellStatRows[used++];
                row.gameObject.SetActive(true);
                return row;
            }
            void Row(string key, Sprite icon, float value, float barMax, string valueText = null)
            {
                NextRow().Load(icon, iconColour,
                    text(key),
                    valueText ?? Mathf.RoundToInt(value).ToString(),
                    barMax > 0f ? Mathf.Abs(value) / barMax : 0f,
                    text(key),
                    text(key + "Desc"));
            }
            string Signed(float value) => value > 0f ? $"+{Mathf.RoundToInt(value)}" : Mathf.RoundToInt(value).ToString();

            // What it does. One row per shape, keyed the way auto-resolve reads the same asset.
            bool overTime = !spell.IsOneOff && spell.TickInterval > 0f;
            if (spell.HealsInsteadOfDamage)
                Row(overTime ? "SpellStatHealingPerSecond" : "SpellStatHealing", SpriteData.GetSprite("Health"), spell.SpellModifierValue, SPELL_BAR_MAX_HEALING);
            else if (spell.MarksTarget)
                Row("SpellStatBonusDamage", SpriteData.GetSprite("MissileStrength"), spell.SpellModifierValue, SPELL_BAR_MAX_PERCENT, $"+{spell.SpellModifierValue}%");
            else if (spell.BracesTarget)
            {
                // A brace has no magnitude worth a number; the description carries it.
            }
            else if (spell.BonusStats != null && spell.BonusStats.Count > 0)
            {
                foreach (TJ.Spells.SpellBonusStat bonus in spell.BonusStats)
                    Row(bonus.UnitStat.ToString(), SpriteData.GetSprite(bonus.UnitStat.ToString()), bonus.Value, SPELL_BAR_MAX_STAT, Signed(bonus.Value));
            }
            else if (spell.GrantsBattlefieldBonus)
            {
                if (spell.BonusType == BattlefieldBonusEnum.LesserMoraleSpell)
                    Row("SpellStatMoralePerSecond", SpriteData.GetSprite("Leadership"), spell.SpellModifierValue, SPELL_BAR_MAX_STAT, Signed(spell.SpellModifierValue));
                else
                    Row(spell.BonusUnitStat.ToString(), SpriteData.GetSprite(spell.BonusUnitStat.ToString()), spell.SpellModifierValue, SPELL_BAR_MAX_STAT, Signed(spell.SpellModifierValue));
            }
            else if (spell.SpellModifierValue > 0)
                Row(overTime ? "SpellStatDamagePerSecond" : "SpellStatDamage", spellDamageSprite, spell.SpellModifierValue, SPELL_BAR_MAX_DAMAGE);

            if (spell.SpellRadius > 0f)
                Row("SpellStatArea", spellAreaSprite, spell.SpellRadius, SPELL_BAR_MAX_AREA);

            // The caster's reach and cadence, not the spell's: MageCast is seeded from SquadStats.
            Row("SpellStatCastRange", SpriteData.GetSprite("Range"), squadStats.BaseRange, SPELL_BAR_MAX_RANGE);
            Row("SpellStatCooldown", spellCooldownSprite, squadStats.rateOfFire, SPELL_BAR_MAX_COOLDOWN,
                string.Format(text("CooldownSeconds"), Mathf.RoundToInt(squadStats.rateOfFire)));

            if (spell.SpellDuration > 0f && !spell.IsOneOff)
                Row("SpellStatDuration", spellDurationSprite, spell.SpellDuration, SPELL_BAR_MAX_DURATION,
                    string.Format(text("CooldownSeconds"), Mathf.RoundToInt(spell.SpellDuration)));

            // Charges: the same pool the flag's charge bar draws from. Live in battle, full otherwise.
            spellMaxCharges = squadStats.Ammunition + TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE * prestige;
            int currentCharges = TryGetLiveCharges(out int live) ? Mathf.Clamp(live, 0, spellMaxCharges) : spellMaxCharges;
            spellChargesRow = NextRow();
            spellChargesRow.LoadCharges(spellChargesSprite, iconColour,
                text("SpellStatCharges"), currentCharges, spellMaxCharges,
                text("SpellStatCharges"),
                text("SpellStatChargesDesc"));

            for (int i = used; i < spellStatRows.Count; i++) spellStatRows[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Reads the caster's remaining charges off its squad entity. False outside a live battle, or
        /// once SquadRanOutOfAmmoSystem has stripped SquadAmmunition from a spent mage.
        /// </summary>
        private bool TryGetLiveCharges(out int charges)
        {
            charges = 0;
            if (squadEntity.SelfEntity == Entity.Null) return false;
            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return false;
            EntityManager em = world.EntityManager;
            if (!em.Exists(squadEntity.SelfEntity) || !em.HasComponent<SquadAmmunition>(squadEntity.SelfEntity)) return false;
            charges = em.GetComponentData<SquadAmmunition>(squadEntity.SelfEntity).Value;
            return true;
        }
        #endregion

        private void ShowSpellUI(bool show)
        {
            if (spellGroup != null && spellGroup.activeSelf != show) spellGroup.SetActive(show);
            if (spellAttributeLine != null && spellAttributeLine.activeSelf != show) spellAttributeLine.SetActive(show);
            if (spellBonusRoot != null && spellBonusRoot.gameObject.activeSelf != show)
                spellBonusRoot.gameObject.SetActive(show);
        }

        /// <summary>
        /// Parks the spell card directly beneath the hover-only bonus boxes.
        ///
        /// Their stack is a fixed-height container whose children are laid out from its top, so the
        /// card cannot simply be the next sibling - it would be scaled away with them. Measuring the
        /// stack and offsetting by that much keeps the card in the same column and in the same
        /// reading order, while staying visible on its own.
        ///
        /// Called after the layout rebuild in <see cref="Load"/>, because the boxes have no resolved
        /// height before it.
        /// </summary>
        private void PositionSpellBonus()
        {
            if (spellBonusRoot == null || !spellBonusRoot.gameObject.activeSelf) return;
            if (unitAttributesUIContainer == null) return;

            // Not the parent's children: the container trims surplus boxes with Destroy(), which
            // does not take effect until end of frame, so a mage hovered right after a unit with
            // more attributes would measure the doomed boxes too and park the card too low.
            IReadOnlyList<TJ.Map.UnitBonusUI> bonuses = unitAttributesUIContainer.DisplayedBonusUIs;

            float stackHeight = 0f;
            int shown = 0;
            for (int i = 0; i < bonuses.Count; i++)
            {
                RectTransform box = bonuses[i].transform as RectTransform;
                if (box == null || !box.gameObject.activeSelf) continue;
                stackHeight += box.rect.height;
                shown++;
            }
            if (shown > 0) stackHeight += BONUS_STACK_SPACING * shown;

            spellBonusRoot.anchoredPosition = new Vector2(spellBonusRoot.anchoredPosition.x, -stackHeight);
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
            RefreshHuntersMark(entityManager);
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
        // The mark carries a countdown, so unlike the other badges it is re-read on the ammo tick
        // while the panel is up rather than only at hover time.
        private void RefreshHuntersMark(EntityManager entityManager)
        {
            if (huntersMarkAttribute == null) return;
            bool marked = entityManager.HasComponent<HuntersMarkTag>(squadEntity.SelfEntity);
            huntersMarkAttribute.gameObject.SetActive(marked);
            if (!marked) return;
            HuntersMarkTag mark = entityManager.GetComponentData<HuntersMarkTag>(squadEntity.SelfEntity);
            huntersMarkAttribute.LoadTimed(UnitCondition.IsMarked, mark.RemainingDuration,
                Mathf.RoundToInt((mark.DamageMultiplier - 1f) * 100f), Mathf.CeilToInt(mark.RemainingDuration));
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
            if (huntersMarkAttribute != null) huntersMarkAttribute.gameObject.SetActive(false);
            isOnFireAttribute.gameObject.SetActive(false);
            garrisonDefenderAttribute.gameObject.SetActive(false);
            defendersResolveAttribute.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        #region Editor preview
        // The spell card is built at runtime, which leaves nothing to look at while authoring the
        // prefab. These fill it from a real caster's assets without entering Play mode. Two things
        // are avoided on purpose: LocalizationManager.Instance (auto-creates a phantom manager
        // GameObject outside Play mode) and TabletopTavernData (its dictionaries only exist after
        // Awake), so the text comes straight off the en table asset and the stats off the SquadData.
        private const string PREVIEW_SQUAD_DATA = "SquadData/Iron Legion/Hexenjager Mage";

        [ContextMenu("Preview/Smite Spell Card")]
        private void EditorPreviewSmiteSpellCard() => EditorPreviewSpellCard(PREVIEW_SQUAD_DATA);

        [ContextMenu("Preview/Clear Spell Card")]
        private void EditorClearSpellCardPreview()
        {
            if (Application.isPlaying) { Debug.LogWarning("SquadBattleInfo: the preview is an Editor authoring tool, not for Play mode.", this); return; }
            spellStatRows.Clear();
            spellChargesRow = null;
            if (spellStatRowsParent != null)
                foreach (SpellStatRowUI row in spellStatRowsParent.GetComponentsInChildren<SpellStatRowUI>(true))
                    UnityEditor.Undo.DestroyObjectImmediate(row.gameObject);
            ShowSpellUI(false);
            EditorSetPanelAlpha(0f);
            UnityEditor.EditorUtility.SetDirty(this);
        }

        // MemoriCanvasGroup.Awake has not run in edit mode, so its cached group may be null; go to
        // the CanvasGroup directly. The panel is authored hidden, and Load() re-enables it at runtime.
        private void EditorSetPanelAlpha(float alpha)
        {
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null) group.alpha = alpha;
        }

        private void EditorPreviewSpellCard(string squadDataResourcePath)
        {
            if (Application.isPlaying) { Debug.LogWarning("SquadBattleInfo: the preview is an Editor authoring tool, not for Play mode.", this); return; }

            SquadData squadData = Resources.Load<SquadData>(squadDataResourcePath);
            if (squadData == null || squadData.assets.mageSpell == null)
            {
                Debug.LogError($"SquadBattleInfo: no caster SquadData with a mageSpell at Resources/{squadDataResourcePath}.", this);
                return;
            }

            EditorClearSpellCardPreview();

            squadStats = squadData.stats;
            prestige = 0;
            squadEntity = default;
            if (unitAttributesUIContainer == null) unitAttributesUIContainer = GetComponent<UnitAttributesUIContainer>();

            TJ.Spells.SpellData spell = squadData.assets.mageSpell;
            // Same placeholders GetLocalizedSpellDescription fills, minus the colour tags - the tag
            // applicator localizes through LocalizationManager too.
            string description = string.Format(EditorLocalizedText(spell.Spell + "_Desc"), spell.SpellType, spell.SpellModifierValue, spell.SpellDuration);

            ShowSpellUI(true);
            EditorSetPanelAlpha(1f);
            RenderSpellCard(spell, EditorLocalizedText, description);
            foreach (SpellStatRowUI row in spellStatRows)
                UnityEditor.Undo.RegisterCreatedObjectUndo(row.gameObject, "Preview Spell Card");

            LayoutRebuilder.ForceRebuildLayoutImmediate(transform as RectTransform);
            PositionSpellBonus();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"SquadBattleInfo: previewing {spell.name}. Use Preview/Clear Spell Card before saving.", this);
        }

        /// <summary>Reads the en value for a key off the table assets. Falls back to the key.</summary>
        private static string EditorLocalizedText(string key)
        {
            var shared = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Localization.Tables.SharedTableData>(
                "Assets/Data/Localization/MainLocalizationTable Shared Data.asset");
            var table = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Localization.Tables.StringTable>(
                "Assets/Data/Localization/MainLocalizationTable_en.asset");
            if (shared == null || table == null) return key;

            long id = shared.GetId(key);
            if (id == 0) return key;
            var entry = table.GetEntry(id);
            return entry != null && !string.IsNullOrEmpty(entry.Value) ? entry.Value : key;
        }
        #endregion
#endif
    }
}
