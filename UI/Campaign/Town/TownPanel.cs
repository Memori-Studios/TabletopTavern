using UnityEngine;
using TJ.Map;
using Memori.Utilities;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using TJ.Recruit;
using TJ.Shop;
using Memori.SaveData;
using Memori.Audio;
using Memori.Localization;
using Memori.Tooltip;
using Memori.UI;
using System.Threading.Tasks;
using TJ;
using Memori.Notifications;
using Memori.Steamworks;
using TJ.Treasure;

namespace TJ.Town
{
    public class TownPanel : MapPanel
    {
        [Header("Town Config")]
        [SerializeField] MemoriCanvasGroup townOptionsCanvasGroup;
        [SerializeField] TMP_Text townNameText, townDescriptionText;
        [SerializeField] private Image villageImage, castleImage, cityImage;
        [SerializeField] MemoriCanvasGroup garrisonCanvasGroup;
        [SerializeField] Transform garrisonTroopTransform;
        public Transform GarrisonTroopTransform => garrisonTroopTransform;
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardMenuPrefab;

        [Header("Sack Town")]
        [SerializeField] MemoriCanvasGroup sackTownCanvasGroup;
        [SerializeField] TMP_Text bountyAmountText;
        [SerializeField] Button sackTownButton, lootGoldButton, lootGearButton;
        [SerializeField] Button continueAfterSackTownButton;

        [Header("Enter Town")]
        [SerializeField] MemoriCanvasGroup enterTownCanvasGroup;
        [SerializeField] Button enterTownButton, recruitUnitsButton;//, shopButton;
        [SerializeField] Button continueAfterEnterTownButton;
        [SerializeField] MemoriTooltipTrigger enterTownTooltipTrigger, sackTownTooltipTrigger, recruitUnitsTooltipTrigger, recruitmentDetailsNumberTooltip;
        [SerializeField] TMP_Text recruitUnitsCostText;
        [SerializeField] MemoriCanvasGroup recruitmentButtonCanvasGroup;
        [SerializeField] GameObject recruitmentAvailableObject, recruitmentUnavailableObject;

        [Header("Faction Effects")]
        [SerializeField] TMP_Text imperialEdictText;
        [SerializeField] GameObject sackAndSlaughterText;
        [SerializeField] MemoriTooltipTrigger sackAndSlaughterTooltipTrigger;
        [SerializeField] TMP_Text sackAndSlaughterUnit1Text;

        [Header("Sack Town")]
        [SerializeField] Button conscriptUnitsButton;

        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;
        TownSaveData townSaveData;
        MemoriCanvasGroup townPanelCanvasGroup;
        TreasurePanel treasurePanel;
        RecruitPanel recruitPanel;
        ShopPanel shopPanel;
        GoldManager goldManager;
        int recruitmentCost;
        bool hasRecruitedMaxUnits = false;
        bool imperialEdictActive = false;

        private void Awake()
        {
            townPanelCanvasGroup = GetComponent<MemoriCanvasGroup>();

            sackTownButton.onClick.AddListener(OnSackTown);
            lootGearButton.onClick.AddListener(OnLootGearButtonClicked);
            lootGoldButton.onClick.AddListener(OnLootGoldButtonClicked);

            enterTownButton.onClick.AddListener(OnEnterTown);
            recruitUnitsButton.onClick.AddListener(OnRecruitUnitsButtonClicked);
            conscriptUnitsButton.onClick.AddListener(OnConscriptUnitsButtonClicked);

            continueAfterSackTownButton.onClick.AddListener(CompleteTown);
            continueAfterEnterTownButton.onClick.AddListener(CompleteTown);

        }
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
            recruitPanel = mapSceneUIManager.RecruitPanel;
            shopPanel = mapSceneUIManager.ShopPanel;
            treasurePanel = mapSceneUIManager.TreasurePanel;

            lootGoldButton.gameObject.SetActive(false);
            goldManager = CampaignManager.Instance.GoldManager;
        }
        public void LoadTownPanel(int _selectedNodeIndex, int level)
        {
            goldManager.OnGoldAmountChanged -= UpdateAffordability;
            goldManager.OnGoldAmountChanged += UpdateAffordability;
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0.5f, 0.25f));
            if (!campaignSaveManager.SaveData.nodeGenerated) {
                Debug.Log($"generating town for node {_selectedNodeIndex}");
                campaignSaveManager.GenerateTown(_selectedNodeIndex, level);
            }

            SetUpTownInfo();
            // Debug.Log($"townSaveData.townInteractionStatus: {townSaveData.townInteractionStatus}");
            switch (townSaveData.townInteractionStatus)
            {
                case TownInteractionStatus.None:
                    DisplayTownOptions();
                    break;
                case TownInteractionStatus.Entered:
                    OnEnterTown();
                    break;
                case TownInteractionStatus.Sacked:
                    OnSackTown();
                    break;
                case TownInteractionStatus.GarrisonBattleStarted:
                    OnSackTown();
                    break;
                default:
                    Debug.LogError($"Wrong TownInteractionStatus: {townSaveData.townInteractionStatus}");
                    break;
            }

            townPanelCanvasGroup.FadeInAsync(0.25f);
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.TownExplanation });
            
        }
        private void SetUpTownInfo()
        {
            townSaveData = campaignSaveManager.SaveData.townData;
            // Debug.Log($"Setting up town info: {townSaveData.hasLootedGear} bountyAmount: {townSaveData.bountyAmount} gear IDs: {string.Join(", ", townSaveData.townLootGearIDs)}");
            recruitmentCost = TownSaveData.GetTownRecruitCost(townSaveData.townSize);
            string townSizeLocalized = LocalizationManager.Instance.GetText(townSaveData.townSize.ToString());
            townNameText.text = LocalizationManager.Instance.GetText(townSaveData.townName);
            string raceLocalized = LocalizationManager.Instance.GetText(townSaveData.townRace.ToString());
            townDescriptionText.text = raceLocalized + " " + townSizeLocalized;
            recruitmentButtonCanvasGroup.CGEnable();
            hasRecruitedMaxUnits = false;
            SetRecruitmentAvailable(true);

            if (townSaveData.townSize == TownSize.Village)
            {
                villageImage.enabled = true;
                castleImage.enabled = false;
                cityImage.enabled = false;
            }
            else if (townSaveData.townSize == TownSize.Castle)
            {
                villageImage.enabled = true;
                castleImage.enabled = true;
                cityImage.enabled = false;
            }
            else if (townSaveData.townSize == TownSize.City)
            {
                villageImage.enabled = true;
                castleImage.enabled = true;
                cityImage.enabled = true;
            }

            string enterTownTitleLocalized = LocalizationManager.Instance.GetText("EnterTown");
            string enterTownDescriptionLocalized = LocalizationManager.Instance.GetText("enterTownDesc");
            if (TownIsSameRace())
            {
                string unlimitedRecruits = LocalizationManager.Instance.GetText("UncappedRecruitment");
                enterTownDescriptionLocalized += $"\n<color={ColorData.Positive}>" + unlimitedRecruits + "</color>";
            }
            string sackTownTitleLocalized = LocalizationManager.Instance.GetText("FightGarrison");
            string enterTownFlavorLocalized = LocalizationManager.Instance.GetText("enterTownFlavor");
            string sackTownDescriptionLocalized = LocalizationManager.Instance.GetText("sackTownDesc");
            string sackTownFlavorLocalized = LocalizationManager.Instance.GetText("sackTownFlavor");
            string recruitmentButtonTooltipTitleLocalized = LocalizationManager.Instance.GetText("Recruit Units");
            string recruitmentButtonTooltipDescLocalized = LocalizationManager.Instance.GetText("townRecruitmentDesc");

            enterTownTooltipTrigger.SetUpToolTip(enterTownTitleLocalized, enterTownDescriptionLocalized, enterTownFlavorLocalized);
            sackTownTooltipTrigger.SetUpToolTip(sackTownTitleLocalized, sackTownDescriptionLocalized, sackTownFlavorLocalized);
            recruitmentDetailsNumberTooltip.SetUpToolTip(recruitmentButtonTooltipTitleLocalized, recruitmentButtonTooltipDescLocalized);
        }
        private void DisplayTownOptions()
        {
            townOptionsCanvasGroup.CGEnable();
            LoadEnemyCompany();
        }
        public async void LoadEnemyCompany()
        {
            garrisonCanvasGroup.CGEnable();
            List<SquadDisplayCardMenu> enemySquadsCards = new ();
            foreach (SquadToLoad squad in campaignSaveManager.SaveData.townData.townGarrisonUnits)
            {
                SquadDisplayCardMenu squadDisplayCardMenu = Instantiate(squadDisplayCardMenuPrefab, garrisonTroopTransform);
                enemySquadsCards.Add(squadDisplayCardMenu);
                squadDisplayCardMenu.SetUp(squad, false, mapSceneUIManager.HUDPanel, true);
                squadDisplayCardMenu.SpawnInJuice(false);
                await Task.Delay(100);
            }
            foreach (SquadDisplayCardMenu squad in enemySquadsCards)
            {
                squad.MakeInteractable(true);
            }

        }
        public void HideEnemyCompany()
        {
            foreach (Transform child in garrisonTroopTransform) {
                Destroy(child.gameObject);
            }
            garrisonCanvasGroup.CGDisable();
        }
        public void ReloadTownPanel()
        {
            townPanelCanvasGroup.FadeInAsync(0.25f);
        }
        private void OnSackTown()
        {
            CampaignManager.Instance.MapSceneUIManager.EngagementPanel.LoadEngagementPanelFromTown();
            townOptionsCanvasGroup.FadeOutAsync(0.25f);
            HideEnemyCompany();
        }
        //Patch me
        public void LoadTownPostGarrisonEngagement()
        {
            campaignSaveManager.SetTownData(townSaveData);
            SetUpTownInfo();
            townPanelCanvasGroup.FadeInAsync(0.25f);
            LootTown();

            if (HeroBonusManager.Instance.ActiveHeroID == 5 || HeroBonusManager.Instance.ActiveHeroID == 6)
            {
                sackAndSlaughterText.SetActive(true);
                string[] unitNames = CampaignManager.Instance.CampaignSaveManager.PrestigeRandomUnits2();
                if (unitNames != null)
                {
                    IAudioRequester.Instance.PlaySFX(SFXData.PrestigeUnit);
                    string displayText = LocalizationManager.Instance.GetText(unitNames[0]);
                    if (unitNames[1] != null)
                        displayText += "\n" + LocalizationManager.Instance.GetText(unitNames[1]);
                    sackAndSlaughterUnit1Text.text = displayText;
                    sackAndSlaughterTooltipTrigger.SetUpToolTip(LocalizationManager.Instance.GetText("Campaign Bonus"), LocalizationManager.Instance.GetText("RavenHostBonusDescription"));
                }
                else
                {
                    string noneLocalized = LocalizationManager.Instance.GetText("None");
                    sackAndSlaughterUnit1Text.text = noneLocalized;
                }
            }
            else
            {
                sackAndSlaughterText.SetActive(false);
            }
        }
        public void LootTown()
        {
            // Debug.Log($"Setting up town info: {townSaveData.hasLootedGear} bountyAmount: {townSaveData.bountyAmount} gear IDs: {string.Join(", ", townSaveData.townLootGearIDs)}");
            IAudioRequester.Instance.PlaySFX(SFXData.SackTown);
            campaignSaveManager.RemoveZeroHealthSquads();

            if (townSaveData.bountyAmount > 0) lootGoldButton.gameObject.SetActive(true);

            lootGearButton.gameObject.SetActive(!townSaveData.hasLootedGear);
            conscriptUnitsButton.gameObject.SetActive(true);
            int actBonus = 5 * (campaignSaveManager.SaveData.bookNumber - 1); //add 5 gold per book number to the bounty amount
            if (actBonus > 0)
            {
                string ActLocalized = LocalizationManager.Instance.GetText("Act");
                string actInRomanNumerals = MemoriUI.ConvertNumberToRomanNumeral(campaignSaveManager.SaveData.bookNumber);
                bountyAmountText.text = $"{townSaveData.bountyAmount} <color={ColorData.Green}>+{actBonus} {ActLocalized} {actInRomanNumerals}</color>";
            }
            else
            {
                bountyAmountText.text = $"{townSaveData.bountyAmount}";
            }
            sackTownCanvasGroup.FadeInAsync(0.25f);

            SteamAchievements.Unlock(AchievementId.SackCity);
            SteamAchievements.AddStat(SteamStatId.CitiesSacked, 1);
            campaignSaveManager.SaveData.townsSacked++;
            if(campaignSaveManager.SaveData.townsSacked >= 3) {
                SteamAchievements.Unlock(AchievementId.ThreeTownsSackedRun);
            }
            if (campaignSaveManager.SaveData.townsSacked >= 9 &&
                HeroData.GetRaceFromHero(campaignSaveManager.SaveData.heroID) == Race.RavenHost)
            {
                SteamAchievements.Unlock(AchievementId.NineRealms);
            }

            //Thirst for Blood: Sacking a city heals all units to full health
            if (HeroBonusManager.Instance.ActiveHeroID == 10)
            {
                campaignSaveManager.ModifyTroopHealth(1);
            }
        }
        private async void OnEnterTown()
        {
            townSaveData.townInteractionStatus = TownInteractionStatus.Entered;
            IAudioRequester.Instance.PlaySFX(SFXData.EnterTown);

            campaignSaveManager.HealTroopsOnTownEntry();
            campaignSaveManager.SetTownData(townSaveData);
            await townOptionsCanvasGroup.FadeOut(0.25f);

            string townSizeLocalized = LocalizationManager.Instance.GetText(townSaveData.townSize.ToString());
            string recruitTownTitleLocalized = LocalizationManager.Instance.GetText("RecruitUnitsFromTownTitle");
            string recruitTownDescriptionLocalized = LocalizationManager.Instance.GetText("RecruitUnitsFromTownDesc") + $" {LocalizationManager.Instance.GetText(townSaveData.townRace.ToString())}";

            recruitTownTitleLocalized += $" {townSizeLocalized}: <color={ColorData.Tier1}>[{LocalizationManager.Instance.GetText("Tier I")}]</color>"; 
            if(townSaveData.townSize == TownSize.Castle || townSaveData.townSize == TownSize.City) {
                recruitTownTitleLocalized += $" + <color={ColorData.Tier2}>[{LocalizationManager.Instance.GetText("Tier II")}]</color>"; 
            }
            if(townSaveData.townSize == TownSize.City) {
                recruitTownTitleLocalized += $" + <color={ColorData.Tier3}>[{LocalizationManager.Instance.GetText("Tier III")}]</color>"; 
            }

            recruitUnitsTooltipTrigger.SetUpToolTip(recruitTownTitleLocalized, recruitTownDescriptionLocalized);

            if (HeroBonusManager.Instance.ActiveHeroID == 1 || HeroBonusManager.Instance.ActiveHeroID == 2)
            {
                imperialEdictActive = true;
                imperialEdictText.enabled = true;
                recruitUnitsCostText.text = $"<color={ColorData.Positive}>{0}</color><sprite name=GoldSprite>";
            }
            else
            {
                imperialEdictActive = false;
                imperialEdictText.enabled = false;
                UpdateAffordability(campaignSaveManager.SaveData.goldAmount);
            }

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.TownExplanation);
            enterTownCanvasGroup.FadeInAsync(0.25f);
            HideEnemyCompany();
        }
        public void OnLootGearButtonClicked()
        {
            if (townSaveData.townLootGearIDs == null || townSaveData.townLootGearIDs.Count == 0)
            {
                Debug.LogError("[TownPanel] OnLootGearButtonClicked: townLootGearIDs is empty.");
                return;
            }
            treasurePanel.LoadTreasurePanelFromShop(townSaveData.townLootGearIDs[0]);
            lootGearButton.gameObject.SetActive(false);
            townSaveData.hasLootedGear = true;
        }
        public void OnLootGearCardSelected()
        {
            townSaveData.hasLootedGear = true;
            campaignSaveManager.SetTownData(townSaveData);

            lootGearButton.OnPointerExit(null);
            lootGearButton.gameObject.SetActive(false);
        }
        public void OnLootGoldButtonClicked()
        {
            string localizedString = LocalizationManager.Instance.GetText("Loot Gold");
            int actBonus = 5 * (campaignSaveManager.SaveData.bookNumber - 1); //add 5 gold per book number to the bounty amount
            goldManager.ModifyGold(townSaveData.bountyAmount + actBonus, localizedString);
            townSaveData.bountyAmount = 0;
            campaignSaveManager.SetTownData(townSaveData);
            lootGoldButton.OnPointerExit(null);

            lootGoldButton.gameObject.SetActive(false);
        }
        public void OnRecruitUnitsButtonClicked()
        {
            if (hasRecruitedMaxUnits)
            {
                string errorLocalized = LocalizationManager.Instance.GetText("You have recruited the max amount of units from this town.");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }
            int modifiedRecruitmentCost = recruitmentCost;
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Duke) modifiedRecruitmentCost += 2;
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.JailersKey))
            {
                modifiedRecruitmentCost -= townSaveData.townSize switch
                {
                    TownSize.Village => 5,
                    TownSize.Castle => 10,
                    _ => 15,
                };
            }

            if (imperialEdictActive)
            {
                modifiedRecruitmentCost = 0;
                imperialEdictActive = false;
                UpdateAffordability(campaignSaveManager.SaveData.goldAmount);
            }
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(modifiedRecruitmentCost))
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("notEnoughGold"));
                shopPanel.RenableShopPanel();
                return;
            }
            string localizedString = LocalizationManager.Instance.GetText("Recruit Units");
            CampaignManager.Instance.GoldManager.ModifyGold(-modifiedRecruitmentCost, localizedString);

            townPanelCanvasGroup.FadeOutAsync(0.25f);
            IAudioRequester.Instance.PlaySFX(SFXData.FocusNode);
            recruitPanel.LoadRecruitPanelFromTown(townSaveData.townRace, townSaveData.townSize);

            if (!TownIsSameRace())
            {
                hasRecruitedMaxUnits = true;
                SetRecruitmentAvailable(false);
            }
        }
        public void OnConscriptUnitsButtonClicked()
        {
            townPanelCanvasGroup.FadeOutAsync(0.25f);
            IAudioRequester.Instance.PlaySFX(SFXData.FocusNode);
            recruitPanel.LoadRecruitPanelFromTown(townSaveData.townRace, townSaveData.townSize);
            conscriptUnitsButton.gameObject.SetActive(false);
        }
        public void CompleteTown()
        {
            mapSceneUIManager.TryDrainPendingPrestigeChoices(() => mapSceneUIManager.CompleteLayerAction());
        }
        public override void ClosePanel()
        {
            Debug.Log("[Map] Closing TownPanel");
            goldManager.OnGoldAmountChanged -= UpdateAffordability;
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0f, 0.25f));
            lootGearButton.gameObject.SetActive(false);
            lootGoldButton.gameObject.SetActive(false);
            HideEnemyCompany();

            townPanelCanvasGroup.FadeOutAsync(0.25f);
            sackTownCanvasGroup.CGDisable();
            enterTownCanvasGroup.CGDisable();
            townOptionsCanvasGroup.CGDisable();
        }
        public void DisableTownCanvasesOnLoss()
        {
            goldManager.OnGoldAmountChanged -= UpdateAffordability;
            lootGearButton.gameObject.SetActive(false);
            lootGoldButton.gameObject.SetActive(false);
            HideEnemyCompany();

            sackTownCanvasGroup.CGDisable();
            enterTownCanvasGroup.CGDisable();
            townOptionsCanvasGroup.CGDisable();
            townPanelCanvasGroup.CGDisable();
        }
        public void CloseRecruitPanel()
        {
            ReloadTownPanel();
        }
        public void UpdateAffordability(int _goldAmount)
        {
            int modifiedRecruitmentCost = recruitmentCost;
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.JailersKey))
            {
                modifiedRecruitmentCost -= townSaveData.townSize switch
                {
                    TownSize.Village => 5,
                    TownSize.Castle => 10,
                    _ => 15,
                };
            }

            //DifficultyMod 9
            if(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel >= TT_Difficulty.Duke) {
                modifiedRecruitmentCost += 2;
            }


            string colorString = _goldAmount >= modifiedRecruitmentCost ? ColorData.Primary : ColorData.Negative;

            if (imperialEdictActive)
            {
                modifiedRecruitmentCost = 0;
                imperialEdictText.enabled = true;
                colorString = ColorData.Positive;
            }
            else
            {
                imperialEdictText.enabled = false;
            }

            recruitUnitsCostText.text = $"<color={colorString}>{modifiedRecruitmentCost}</color><sprite name=GoldSprite>";
        }
        public void OnDestroy()
        {
            if (goldManager == null) return;

            goldManager.OnGoldAmountChanged -= UpdateAffordability;
        }
        private void SetRecruitmentAvailable(bool available)
        {
            if (recruitmentAvailableObject != null) recruitmentAvailableObject.SetActive(available);
            if (recruitmentUnavailableObject != null) recruitmentUnavailableObject.SetActive(!available);
            recruitmentButtonCanvasGroup.canvasGroup.alpha = available ? 1f : 0.25f;
        }
        private bool TownIsSameRace()
        {
            return townSaveData.townRace == HeroData.GetRaceFromHero(CampaignManager.Instance.CampaignSaveManager.GetHeroID());
        }
    }
}

[System.Serializable] public enum TownInteractionStatus { None, Entered, GarrisonBattleStarted, Sacked };
[System.Serializable] public enum TownSize { Village, Castle, City };
[System.Serializable] public class TownSaveData
{
    public string townName;
    public TownSize townSize;
    public Race townRace;
    public int bountyAmount;
    public List<GearID> townLootGearIDs;
    public TownInteractionStatus townInteractionStatus;
    public bool hasLootedGear;
    public SquadToLoad[] townGarrisonUnits;

    public static TownSize GenerateTownSize(int level)
    {
        switch (level)
        {
            case 3:
                return TownSize.Village;
            case 7:
                return TownSize.Castle;
            case 10:
                return TownSize.City;
            default:
                Debug.LogError($"You put a town at the wrong level ({level}) Expected 3, 7, or 10");
                return TownSize.Village;
        }
    }
    private static readonly Dictionary<TownSize, (int Min, int Max)> BountyRangeOverrides = new();
    // Lives here rather than on TabletopTavernConstants (where VILLAGE/CASTLE/CITY_RECRUIT_COST
    // are declared) because TownSize is part of the root TabletopTavern.Core assembly, and
    // TabletopTavernConstants compiles into the separate Components assembly, which cannot
    // reference back to root. Root -> Components is fine, so this reads the consts directly below.
    private static readonly Dictionary<TownSize, int> RecruitCostOverrides = new();

    public static void ClearEconomyOverrides()
    {
        BountyRangeOverrides.Clear();
        RecruitCostOverrides.Clear();
    }
    // Validated by the caller (EconomyOverrideLoader) before this is invoked - System.Random.Next
    // throws if max < min, and that would surface as a live crash during town generation rather
    // than at boot-time mod load, so a bad pair must never reach this dictionary.
    public static void SetBountyRangeOverride(TownSize size, int min, int max) => BountyRangeOverrides[size] = (min, max);
    public static void SetRecruitCostOverride(TownSize size, int cost) => RecruitCostOverrides[size] = cost;

    public static int GetTownRecruitCost(TownSize townSize)
    {
        if (RecruitCostOverrides.TryGetValue(townSize, out int overrideCost)) return overrideCost;
        return townSize switch
        {
            TownSize.Castle => TabletopTavernConstants.CASTLE_RECRUIT_COST,
            TownSize.City => TabletopTavernConstants.CITY_RECRUIT_COST,
            _ => TabletopTavernConstants.VILLAGE_RECRUIT_COST,
        };
    }

    public static (int Min, int Max) GetDefaultBountyRange(TownSize townSize) => townSize switch
    {
        TownSize.Village => (4, 7),
        TownSize.Castle => (9, 12),
        TownSize.City => (14, 17),
        _ => (0, 0),
    };
    public static (int Min, int Max) GetEffectiveBountyRange(TownSize townSize) =>
        BountyRangeOverrides.TryGetValue(townSize, out var range) ? range : GetDefaultBountyRange(townSize);

    public static int GenerateBountyAmount(TownSize _townWealth, int _seed)
    {
        System.Random random = new(Seed: _seed);
        var range = GetEffectiveBountyRange(_townWealth);
        return random.Next(range.Min, range.Max);
    }
}
 