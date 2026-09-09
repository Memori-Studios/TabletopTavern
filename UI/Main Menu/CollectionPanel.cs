using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Memori.SaveData;
using Memori.UI;
using Memori.Utilities;
using Memori.Steamworks;
using Memori.Input;
using MoreMountains.Feedbacks;
using Memori.Localization;
using UnityEngine.Serialization;

namespace TJ.MainMenu
{
    [RequireComponent(typeof(MemoriCanvasGroup))]
    public class CollectionPanel : MonoBehaviour
    {
        private MemoriCanvasGroup panelCanvasGroup;
        [FormerlySerializedAs("returnToMainMenuButton")]
        [SerializeField] private Button closeButton;

        [Header("Gear")]
        [SerializeField] private Transform gearCardParent;
        [SerializeField] private TMP_Text gearCountText;
        [SerializeField] private CollectionGearCard[] gearCards;

        [Header("Potions")]
        [SerializeField] private Transform potionsCardParent;
        [SerializeField] private TMP_Text potionsCountText;
        [SerializeField] private CollectionConsumableCard[] potionsCards;
        [SerializeField] private SquadDisplayCardCollection squadDisplayCardCollectionPrefab;

        [Header("Races")]
        [SerializeField] private CollectionRaceButton ironLegionRaceButton;
        [SerializeField] private CollectionRaceParent ironLegionRaceParent;
        [SerializeField] private CollectionRaceButton greenTideButton;
        [SerializeField] private CollectionRaceParent greenTideCardParent;
        [SerializeField] private CollectionRaceButton ravenhostButton;
        [SerializeField] private CollectionRaceParent ravenhostCardParent;
        [SerializeField] private CollectionRaceButton taelindorButton;
        [SerializeField] private CollectionRaceParent taelindorCardParent;
        [SerializeField] private CollectionRaceButton sanguineCourtButton;
        [SerializeField] private CollectionRaceParent sanguineCourtCardParent;
        [SerializeField] private CollectionRaceButton sakuraDynastyButton;
        [SerializeField] private CollectionRaceParent sakuraDynastyCardParent;
        [SerializeField] private CollectionRaceButton deepstoneHoldButton;
        [SerializeField] private CollectionRaceParent deepstoneHoldCardParent;
        [SerializeField] private CollectionRaceButton drakosaurBroodButton;
        [SerializeField] private CollectionRaceParent drakosaurBroodCardParent;

        [Header("Buttons")]
        [SerializeField] private MemoriButtonV2 gearButton;
        [SerializeField] private MemoriButtonV2 potionsButton;
        [SerializeField] private MemoriCanvasGroup gearCanvasGroup, potionsCanvasGroup;
        [SerializeField] private GameObject gearUnacknowledgedIndicator, potionsUnacknowledgedIndicator;

        [Header("Unit Info Screen")]
        [SerializeField] private SquadBattleInfo squadBattleInfo;
        [SerializeField] private Transform prefabHolder;
        [SerializeField] private Transform prefabObject;
        [SerializeField] private RaceBasePrefab baseObject;
        private string _loadedRecruitmentPrefabKey;
        private System.Threading.CancellationTokenSource _hoverCts;
        [SerializeField] private MMF_Player dropInAnimation;
        [SerializeField] private Camera troopCamera;
        [SerializeField] private GameObject troopLights;
        [SerializeField] private Material undiscoveredTroopMaterial;
        [SerializeField] private RawImage troopImage;

        [Header("Rotation")]
        [SerializeField] private float rotationSpeed = 90f;
        private bool isRotatingRight = false;
        private bool isRotatingLeft = false;
        [SerializeField] private Button rotateLeftButton, rotateRightButton;

        [Header("Sections")]
        [SerializeField] private GameObject factionLoreSection;
        [SerializeField] private TMP_Text factionNameText, factionLoreText, factionBonusText;

        [SerializeField] private GameObject extraInfoSection;
        [SerializeField] private Button unitsButton, heroesButton, loreButton;
        RaceConfig activeRaceConfig;
        [SerializeField] private HeroDetailsPanel hero1, hero2;
        [SerializeField] private GameObject heroDetailsPanel;

        private string collectionType = "gear";
        private bool gearUnacknowledged, potionsUnacknowledged;
        private Dictionary<Race, bool> raceUnacknowledged = new();

        private RaceConfig[] raceConfigs;
        private Race selectedRace;

        [System.Serializable]
        private class RaceConfig
        {
            // Roster cached once. GetUnitsOfRace does unitNames.ToArray() on every call, so reading
            // it per use previously cost ~40 array allocations per build.
            public UnitName[] units;
            // Cards are built the first time this race's tab is opened, not during SetUp.
            public bool cardsBuilt;
            public CollectionRaceParent cardParent;
            public CollectionRaceButton button;
            public Race raceType;
            public string collectionTypeKey;
            public Hero hero1, hero2;

            public RaceConfig(UnitName[] units, CollectionRaceParent cardParent, CollectionRaceButton button, Race raceType, string collectionTypeKey, Hero hero1, Hero hero2)
            {
                this.units = units;
                this.cardParent = cardParent;
                this.button = button;
                this.raceType = raceType;
                this.collectionTypeKey = collectionTypeKey;
                this.hero1 = hero1;
                this.hero2 = hero2;
            }
        }

        private void Awake()
        {
            panelCanvasGroup = GetComponent<MemoriCanvasGroup>();
        }

        public void SetUp(Action onClose)
        {
            if (closeButton == null)
                Debug.LogError("[CollectionPanel] closeButton is not assigned - the panel cannot be closed.");
            else
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => onClose());
            }

            // Setup buttons
            SetupButtons();

            // Cache race configs
            CacheRaceConfigs();

            // Load gear
            LoadGear();

            // Load potions
            LoadPotions();

            // Load all races
            LoadAllRaces();

            CheckForAcknowledged();
            gearCanvasGroup.CGEnable();
            SetupRotationButtons();

            OpenCollectionType("gear");

            StartCoroutine(EvaluateCollectionAchievements());
        }

        private void SetupButtons()
        {
            gearButton.Button.onClick.RemoveAllListeners();
            potionsButton.Button.onClick.RemoveAllListeners();
            gearButton.Button.onClick.AddListener(() => OpenCollectionType("gear"));
            potionsButton.Button.onClick.AddListener(() => OpenCollectionType("potions"));

            ironLegionRaceButton.SetUp(() => OpenCollectionType("ironlegion"), Race.IronLegion);
            greenTideButton.SetUp(() => OpenCollectionType("Gruntkin"), Race.Gruntkin);
            ravenhostButton.SetUp(() => OpenCollectionType("ravenhost"), Race.RavenHost);
            taelindorButton.SetUp(() => OpenCollectionType("taelindor"), Race.TaelindorForest);
            sanguineCourtButton.SetUp(() => OpenCollectionType("sanguinecourt"), Race.SanguineCourt);
            sakuraDynastyButton.SetUp(() => OpenCollectionType("sakuradynasty"), Race.SakuraDynasty);
            deepstoneHoldButton.SetUp(() => OpenCollectionType("deepstonehold"), Race.DeepstoneHold);
            drakosaurBroodButton.SetUp(() => OpenCollectionType("drakosaurbrood"), Race.DrakosaurBrood);

            unitsButton.onClick.RemoveAllListeners();
            heroesButton.onClick.RemoveAllListeners();
            loreButton.onClick.RemoveAllListeners();
            unitsButton.onClick.AddListener(OnUnitsButtonClicked);
            heroesButton.onClick.AddListener(OnHeroesButtonClicked);
            loreButton.onClick.AddListener(OnLoreButtonClicked);
        }

        private void CacheRaceConfigs()
        {
            raceConfigs = new RaceConfig[]
            {
                // The arrays passed here are the only GetUnitsOfRace calls in the whole build now.
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.IronLegion), ironLegionRaceParent, ironLegionRaceButton, Race.IronLegion, "ironlegion", HeroData.EdricValeward, HeroData.RhydanGreythorne),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.Gruntkin), greenTideCardParent, greenTideButton, Race.Gruntkin, "Gruntkin", HeroData.BoblinTheGoblinKing, HeroData.KragmukGorethirster),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.RavenHost), ravenhostCardParent, ravenhostButton, Race.RavenHost, "ravenhost", HeroData.BjornIronskull, HeroData.FreyjaStormweaver),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.TaelindorForest), taelindorCardParent, taelindorButton, Race.TaelindorForest, "taelindor", HeroData.IltharionStarpire, HeroData.SerendaelOfNytherial),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.SanguineCourt), sanguineCourtCardParent, sanguineCourtButton, Race.SanguineCourt, "sanguinecourt", HeroData.SisterMorvayne, HeroData.LordDravenBloodreaver),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.SakuraDynasty), sakuraDynastyCardParent, sakuraDynastyButton, Race.SakuraDynasty, "sakuradynasty", HeroData.OdaNobukage, HeroData.TokugawaHarunobu),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.DeepstoneHold), deepstoneHoldCardParent, deepstoneHoldButton, Race.DeepstoneHold, "deepstonehold", HeroData.HrothgarGoblinslayer, HeroData.BerthaBarrelstorm),
                new(TabletopTavernData.Instance.GetUnitsOfRace(Race.DrakosaurBrood), drakosaurBroodCardParent, drakosaurBroodButton, Race.DrakosaurBrood, "drakosaurbrood", HeroData.SkrixTheSwarmcaller, HeroData.ValthrexPrimeclaw)
            };
        }

        private void LoadGear()
        {
            gearCards = gearCardParent.GetComponentsInChildren<CollectionGearCard>();
            // Rare first, then Uncommon, then Common. GearRarity ascends Common -> Rare, so sort descending.
            // OrderByDescending is stable, so gear keeps its GearID order within a rarity.
            GearID[] allGear = GearData.GetGearIDs()
                .OrderByDescending(g => GearData.GetGear(g).GearRarity)
                .ToArray();
            List<int> gearIdsAsInts = SaveDataHandler.GetGearIDsCollected();
            List<int> gearIdsAcknowledged = SaveDataHandler.GetGearIDsAcknowledged();

            int collectedCount = 0;
            for (int i = 0; i < allGear.Length; i++)
            {
                bool isCollected = gearIdsAsInts.Contains((int)allGear[i]);
                bool acknowledged = gearIdsAcknowledged.Contains((int)allGear[i]);
                gearCards[i].LoadGearCard(allGear[i], isCollected, acknowledged, this);
                if (isCollected) collectedCount++;
            }
            gearCountText.text = $"{collectedCount}/{allGear.Length}";
        }

        private void LoadPotions()
        {
            potionsCards = potionsCardParent.GetComponentsInChildren<CollectionConsumableCard>();
            ConsumableEnum[] consumables = ConsumableData.GetAllConsumableEnums();
            List<int> potionsIdsAsInts = SaveDataHandler.GetPotionsIDsCollected();
            List<int> potionsIdsAcknowledged = SaveDataHandler.GetPotionsIDsAcknowledged();

            int collectedCount = 0;
            for (int i = 0; i < consumables.Length; i++)
            {
                bool isCollected = potionsIdsAsInts.Contains((int)consumables[i]);
                bool acknowledged = potionsIdsAcknowledged.Contains((int)consumables[i]);
                potionsCards[i].LoadConsumableCard(consumables[i], isCollected, acknowledged, this);
                if (isCollected) collectedCount++;
            }
            potionsCountText.text = $"{collectedCount}/{consumables.Length}";
        }

        /// <summary>
        /// Only refreshes the eight race buttons. Building their cards used to happen here and cost
        /// ~446ms of a ~448ms SetUp - 112 instantiations of a 156-object prefab, every one of them
        /// hidden immediately because the panel opens on the gear tab. Cards are now built by
        /// EnsureRaceCardsBuilt the first time a race is actually opened.
        /// </summary>
        private void LoadAllRaces()
        {
            List<UnitName> troopsCollected = SaveDataHandler.GetTroopsIDsCollected();

            foreach (var config in raceConfigs)
            {
                int collectedCount = 0;
                foreach (UnitName unitId in config.units)
                    if (troopsCollected.Contains(unitId)) collectedCount++;

                config.button.UnitCountText.text = $"{collectedCount}/{config.units.Length}";

                // Nothing is visible yet, so keep every card container switched off. A CanvasGroup at
                // alpha 0 does NOT deactivate its children, so without this the hidden races' cards
                // would keep their Animators ticking for the whole session.
                config.cardParent.RaceUnitsParent.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Builds one race's unit cards, once. Costs roughly 4ms per card, so about 56ms for a
        /// 14-unit roster - a brief hitch on the first click of a tab, and free thereafter.
        /// </summary>
        private void EnsureRaceCardsBuilt(RaceConfig config)
        {
            if (config.cardsBuilt) return;
            config.cardsBuilt = true;

            // Callers must activate RaceUnitsParent first: Instantiate into an inactive parent
            // skips Awake, and SquadDisplayCard caches its presenter there.
            if (!config.cardParent.RaceUnitsParent.gameObject.activeInHierarchy)
                Debug.LogError("[CollectionPanel] Building cards for " + config.raceType + " while its container is inactive - their Awake will not run.");

            List<UnitName> unitsRecruited = SaveDataHandler.GetTroopsIDsCollected();
            List<UnitName> acknowledged = SaveDataHandler.GetTroopsIDsAcknowledged();

            config.cardParent.Clear();

            foreach (UnitName unitId in config.units)
            {
                bool isCollected = unitsRecruited.Contains(unitId);
                bool isAcknowledged = acknowledged.Contains(unitId);
                SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(unitId);

                SquadToLoad squadToLoad = new()
                {
                    UnitName = unitId,
                    maxUnitCount = squadStats.baseUnitCount,
                    HitPointsPerUnit = squadStats.HitPointsPerUnit,
                    SquadCurrentHealth = squadStats.baseUnitCount * squadStats.HitPointsPerUnit
                };

                SquadDisplayCardCollection card = Instantiate(squadDisplayCardCollectionPrefab, config.cardParent.RaceUnitsParent);
                card.SetUp(squadToLoad, isCollected, isAcknowledged, this, config.raceType);
                config.cardParent.RaceCardCollection.Add(card);
            }
        }

        /// <summary>
        /// Collection achievements are unrelated to drawing the panel, and each evaluator re-reads
        /// the save data and re-allocates its roster. Kept off the build frame so they cannot
        /// contribute to the open cost.
        /// </summary>
        private System.Collections.IEnumerator EvaluateCollectionAchievements()
        {
            yield return null;

            SaveDataHandler.EvaluateGearCollection();
            SaveDataHandler.EvaluateConsumableCollection();

            foreach (var config in raceConfigs)
            {
                SaveDataHandler.EvaluateRaceCollection(config.raceType);
                yield return null;
            }
        }

        private void SetupRotationButtons()
        {
            rotateRightButton.onClick.RemoveAllListeners();
            var rightPointer = rotateRightButton.gameObject.AddComponent<EventTrigger>();
            AddPointerEvent(rightPointer, EventTriggerType.PointerDown, () => isRotatingRight = true);
            AddPointerEvent(rightPointer, EventTriggerType.PointerUp, () => isRotatingRight = false);

            rotateLeftButton.onClick.RemoveAllListeners();
            var leftPointer = rotateLeftButton.gameObject.AddComponent<EventTrigger>();
            AddPointerEvent(leftPointer, EventTriggerType.PointerDown, () => isRotatingLeft = true);
            AddPointerEvent(leftPointer, EventTriggerType.PointerUp, () => isRotatingLeft = false);
        }

        void Update()
        {
            // baseObject is destroyed by ClearUnitPrefab, which can happen while a rotate button is held.
            // Unscaled time because the panel is usable while Settings has paused the battle.
            if (baseObject == null) return;

            if (isRotatingRight)
                baseObject.transform.Rotate(0, -rotationSpeed * Time.unscaledDeltaTime, 0);
            if (isRotatingLeft)
                baseObject.transform.Rotate(0, rotationSpeed * Time.unscaledDeltaTime, 0);
        }

        public void CheckForAcknowledged()
        {
            gearUnacknowledged = false;
            potionsUnacknowledged = false;
            raceUnacknowledged.Clear();

            List<int> gearCollected = SaveDataHandler.GetGearIDsCollected();
            List<int> gearAcknowledged = SaveDataHandler.GetGearIDsAcknowledged();
            List<int> potionsCollected = SaveDataHandler.GetPotionsIDsCollected();
            List<int> potionsAcknowledged = SaveDataHandler.GetPotionsIDsAcknowledged();
            List<UnitName> troopsCollected = SaveDataHandler.GetTroopsIDsCollected();
            List<UnitName> troopsAcknowledged = SaveDataHandler.GetTroopsIDsAcknowledged();

            // Check gear
            if (gearCollected.Any(id => !gearAcknowledged.Contains(id)))
                gearUnacknowledged = true;

            // Check potions
            if (potionsCollected.Any(id => !potionsAcknowledged.Contains(id)))
                potionsUnacknowledged = true;

            // Check races
            foreach (var config in raceConfigs)
            {
                bool hasUnacknowledged = false;
                foreach (UnitName unit in config.units)
                {
                    if (!troopsCollected.Contains(unit)) continue;
                    if (troopsAcknowledged.Contains(unit)) continue;
                    hasUnacknowledged = true;
                    break;
                }

                raceUnacknowledged[config.raceType] = hasUnacknowledged;

                config.button.UnacknowledgedIndicator.SetActive(hasUnacknowledged);
            }

            gearUnacknowledgedIndicator.SetActive(gearUnacknowledged);
            potionsUnacknowledgedIndicator.SetActive(potionsUnacknowledged);
        }

        public void OpenPanel()
        {
            if (closeButton != null) EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
            panelCanvasGroup.CGEnable();
            OpenCollectionType("gear");
            InputHandler.Instance.onUnitCardSelector += OnUnitCardSelector;
        }

        public void ClosePanel()
        {
            EventSystem.current.SetSelectedGameObject(null);
            panelCanvasGroup.CGDisable();
            HideUnitPrefab();
            CheckForAcknowledged();
            // Teardown path now, so guard the same way OnDestroy does.
            if (InputHandler.HasInstance)
                InputHandler.Instance.onUnitCardSelector -= OnUnitCardSelector;
        }

        private void OpenCollectionType(string type)
        {
            collectionType = type;
            
            // Hide all. Deactivating RaceUnitsParent matters as much as the CanvasGroup: alpha 0
            // hides a card but leaves it active, so without this every hidden faction's cards keep
            // their Animators ticking.
            gearCanvasGroup.CGDisable();
            potionsCanvasGroup.CGDisable();
            foreach (var config in raceConfigs)
            {
                config.cardParent.RaceCanvasGroup.CGDisable();
                config.cardParent.RaceUnitsParent.gameObject.SetActive(false);
            }

            HideUnitPrefab();

            // Show selected
            switch (collectionType)
            {
                case "gear":
                    gearCanvasGroup.CGEnable();
                    squadBattleInfo.Unhover();
                    HideRotationButtons();
                    extraInfoSection.SetActive(false);
                    factionLoreSection.SetActive(false);
                    heroDetailsPanel.SetActive(false);
                    break;
                case "potions":
                    potionsCanvasGroup.CGEnable();
                    squadBattleInfo.Unhover();
                    HideRotationButtons();
                    extraInfoSection.SetActive(false);
                    factionLoreSection.SetActive(false);
                    heroDetailsPanel.SetActive(false);
                    break;
                default:
                    activeRaceConfig = raceConfigs.FirstOrDefault(r => r.collectionTypeKey == collectionType);
                    if (activeRaceConfig != null)
                    {
                        // Activate BEFORE building. A GameObject instantiated into an inactive
                        // parent does not run Awake, so SquadDisplayCard._presenter would still be
                        // null when SetUp dereferences it.
                        activeRaceConfig.cardParent.RaceUnitsParent.gameObject.SetActive(true);
                        // First open of this tab pays for its own cards, roughly 4ms each.
                        EnsureRaceCardsBuilt(activeRaceConfig);
                        selectedRace = activeRaceConfig.raceType;
                        activeRaceConfig.cardParent.RaceCanvasGroup.CGEnable();
                        if (activeRaceConfig.cardParent.RaceCardCollection.Count > 0)
                            activeRaceConfig.cardParent.RaceCardCollection[0].OnPointerEnter(null);
                        ShowRotationButtons();
                        activeRaceConfig.cardParent.ResetLayout();
                        extraInfoSection.SetActive(true);
                        factionLoreSection.SetActive(false);
                        heroDetailsPanel.SetActive(false);
                        factionNameText.text = LocalizationManager.Instance.GetText(activeRaceConfig.raceType.ToString());
                        factionLoreText.text = LocalizationManager.Instance.GetLoreString(activeRaceConfig.raceType.ToString()+"Lore");
                        factionBonusText.text = LocalizationManager.Instance.GetText(activeRaceConfig.raceType.ToString() + "BonusDescription");
                    }
                    break;
            }
        }

        // Rest of methods unchanged...
        public void UpdateAcknowledged() => CheckForAcknowledged();

        public async void HoverSquad(SquadToLoad squad, bool isCollected)
        {
            _hoverCts?.Cancel();
            _hoverCts?.Dispose();
            _hoverCts = new System.Threading.CancellationTokenSource();
            var token = _hoverCts.Token;

            squadBattleInfo.SetUpCollection(squad, Team.Player);
            string key = TabletopTavernData.Instance.GetRecruitmentPrefabKey(squad.UnitName);
            GameObject prefab = await TabletopTavernData.Instance.LoadRecruitmentPrefabAsync(squad.UnitName);

            if (token.IsCancellationRequested)
            {
                AddressablesManager.Instance.Release(key);
                return;
            }

            if (_loadedRecruitmentPrefabKey == key)
                _loadedRecruitmentPrefabKey = null;
            LoadUnitPrefab(prefab, isCollected, TabletopTavernData.Instance.GetUnitSizeFromUnitName(squad.UnitName) != UnitSize.Infantry);
            _loadedRecruitmentPrefabKey = key;
        }

        public void LoadUnitPrefab(GameObject prefab, bool isCollected, bool bigBase)
        {
            ClearUnitPrefab();
#if UNITY_EDITOR
            isCollected = true;
#endif

            prefabObject = Instantiate(prefab, prefabHolder).GetComponent<Transform>();
            prefabObject.localPosition = new Vector3(0, 0, 0);
            baseObject = Instantiate(TabletopTavernData.Instance.GetRaceData(selectedRace).RaceBasePrefab, prefabHolder);
            baseObject.transform.localPosition = Vector3.zero;
            baseObject.SetUp(isCollected, undiscoveredTroopMaterial);
            dropInAnimation.PlayFeedbacks();
            if (bigBase)
                baseObject.transform.localScale = new Vector3(2f, 1f, 2f);
            else
                baseObject.transform.localScale = Vector3.one;

            prefabObject.SetParent(baseObject.transform);

            // The panel is usable while Settings has paused the battle, so the idle animation
            // must ignore timeScale or the unit stands in a frozen pose.
            foreach (Animator animator in prefabObject.GetComponentsInChildren<Animator>())
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                if (!isCollected)
                    animator.speed = 0;
            }

            if (!isCollected)
            {
                foreach (Renderer renderer in prefabObject.GetComponentsInChildren<Renderer>())
                {
                    Material[] materials = renderer.materials;
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = undiscoveredTroopMaterial;
                    renderer.materials = materials;
                }
            }

            troopCamera.enabled = true;
            troopImage.enabled = true;
            troopLights.SetActive(true);
        }
        public void ClearUnitPrefab()
        {
            if (prefabObject != null)
                Destroy(prefabObject.gameObject);
            if (baseObject != null)
                Destroy(baseObject.gameObject);
            ReleaseRecruitmentPrefab();
        }

        public void HideUnitPrefab()
        {
            if (prefabObject != null)
                Destroy(prefabObject.gameObject);
            ReleaseRecruitmentPrefab();
            troopCamera.enabled = false;
            troopLights.SetActive(false);
            troopImage.enabled = false;
        }

        private void ReleaseRecruitmentPrefab()
        {
            if (_loadedRecruitmentPrefabKey == null) return;
            AddressablesManager.Instance.Release(_loadedRecruitmentPrefabKey);
            _loadedRecruitmentPrefabKey = null;
        }

        public void ShowRotationButtons()
        {
            rotateLeftButton.gameObject.SetActive(true);
            rotateRightButton.gameObject.SetActive(true);
        }

        public void HideRotationButtons()
        {
            rotateLeftButton.gameObject.SetActive(false);
            rotateRightButton.gameObject.SetActive(false);
        }

        private void AddPointerEvent(EventTrigger trigger, EventTriggerType eventType, System.Action callback)
        {
            EventTrigger.Entry entry = new() { eventID = eventType };
            entry.callback.AddListener((eventData) => callback());
            trigger.triggers.Add(entry);
        }

        private void OnUnitCardSelector()
        {
            SquadDisplayCardCollection firstCard = null;
            var config = raceConfigs.FirstOrDefault(r => r.collectionTypeKey == collectionType);
            firstCard = config?.cardParent.RaceCardCollection.FirstOrDefault();

            if (firstCard != null)
                EventSystem.current.SetSelectedGameObject(firstCard.gameObject);
            else
                Debug.LogWarning("No active squad card found to select.");
        }
        private void OnDestroy()
        {
            if (InputHandler.HasInstance)
                InputHandler.Instance.onUnitCardSelector -= OnUnitCardSelector;
            _hoverCts?.Cancel();
            _hoverCts?.Dispose();
            // The panel is destroyed on every scene unload now, so this is the last chance to
            // release. AddressablesManager.Release is refcount-safe and no-ops on an unknown key.
            ReleaseRecruitmentPrefab();
        }
        private void OnUnitsButtonClicked()
        {
            if (activeRaceConfig == null) return;
            activeRaceConfig.cardParent.DisplayUnitsOfRace();
            ShowRotationButtons();
            factionLoreSection.SetActive(false);
            heroDetailsPanel.SetActive(false);
            if (activeRaceConfig.cardParent.RaceCardCollection.Count > 0)
                activeRaceConfig.cardParent.RaceCardCollection[0].OnPointerEnter(null);
        }
        private void OnHeroesButtonClicked()
        {
            if (activeRaceConfig == null) return;

            ClearUnitPrefab();
            factionLoreSection.SetActive(false);
            activeRaceConfig.cardParent.HideUnitsOfRace();
            squadBattleInfo.Unhover();
            HideRotationButtons();
            hero1.SetUp(activeRaceConfig.hero1);
            hero2.SetUp(activeRaceConfig.hero2);
            heroDetailsPanel.SetActive(true);
        }
        private void OnLoreButtonClicked()
        {
            if (activeRaceConfig == null) return;

            ClearUnitPrefab();
            factionLoreSection.SetActive(true);
            heroDetailsPanel.SetActive(false);
            activeRaceConfig.cardParent.HideUnitsOfRace();
            squadBattleInfo.Unhover();
            HideRotationButtons();
        }
    }
}