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
        [SerializeField] private TownPanelView view;
        // Lives in the map's shared bottom-right Continue slot, outside the view, where players expect it on every panel.
        [SerializeField] private Button continueButton;
        [SerializeField] private SquadDisplayCardMenu squadDisplayCardMenuPrefab;
        public Transform GarrisonTroopTransform => view.GarrisonGrid;

        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;
        TownSaveData townSaveData;
        MemoriCanvasGroup townPanelCanvasGroup;
        TreasurePanel treasurePanel;
        RecruitPanel recruitPanel;
        ShopPanel shopPanel;
        GoldManager goldManager;
        int recruitmentCost;
        int selectedNodeIndex;
        bool hasRecruitedMaxUnits = false;
        bool imperialEdictActive = false;
        // True once the sacked town's spoil rows are on screen; gear left behind only pays from there.
        bool spoilsShown;

        private void Awake()
        {
            townPanelCanvasGroup = GetComponent<MemoriCanvasGroup>();

            view.FightButton.onClick.AddListener(OnSackTown);
            view.GearRow.Button.onClick.AddListener(OnLootGearButtonClicked);
            view.GoldRow.Button.onClick.AddListener(OnLootGoldButtonClicked);

            view.EnterButton.onClick.AddListener(OnEnterTown);
            view.RecruitButton.onClick.AddListener(OnRecruitUnitsButtonClicked);
            view.ConscriptRow.Button.onClick.AddListener(OnConscriptUnitsButtonClicked);

            continueButton.onClick.AddListener(CompleteTown);
        }
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
            recruitPanel = mapSceneUIManager.RecruitPanel;
            shopPanel = mapSceneUIManager.ShopPanel;
            treasurePanel = mapSceneUIManager.TreasurePanel;

            goldManager = CampaignManager.Instance.GoldManager;
        }
        public void LoadTownPanel(int _selectedNodeIndex, int level)
        {
            goldManager.OnGoldAmountChanged -= UpdateAffordability;
            goldManager.OnGoldAmountChanged += UpdateAffordability;
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0.5f, 0.25f));
            selectedNodeIndex = _selectedNodeIndex;
            spoilsShown = false;
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
                    LoadEnemyCompany();
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

            // A garrison fight hands the screen to the engagement panel, which brings this panel back after the battle.
            if (townSaveData.townInteractionStatus == TownInteractionStatus.None || townSaveData.townInteractionStatus == TownInteractionStatus.Entered)
                OpenPanel();
            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.TownExplanation });

        }
        private void SetUpTownInfo()
        {
            townSaveData = campaignSaveManager.SaveData.townData;
            // Debug.Log($"Setting up town info: {townSaveData.hasLootedGear} bountyAmount: {townSaveData.bountyAmount} gear IDs: {string.Join(", ", townSaveData.townLootGearIDs)}");
            recruitmentCost = TownSaveData.GetTownRecruitCost(townSaveData.townSize);
            string townSizeLocalized = LocalizationManager.Instance.GetText(townSaveData.townSize.ToString());
            string raceLocalized = LocalizationManager.Instance.GetText(townSaveData.townRace.ToString());
            view.SetHeader(LocalizationManager.Instance.GetText(townSaveData.townName), raceLocalized + " " + townSizeLocalized,
                ColorData.GetRaceDisplayColor(townSaveData.townRace), townSaveData.townSize);
            view.SetTownInfo(TownInfoContent());
            hasRecruitedMaxUnits = false;
            imperialEdictActive = false;
            SetRecruitmentAvailable(true);

            view.SetEnterStrip(LocalizationManager.Instance.GetText("townHeal"), HealPercentText(), RecruitRarityText());
            view.SetEnterLines(string.Format(LocalizationManager.Instance.GetText("townHealLine"), HealPercentText()),
                LocalizationManager.Instance.GetText("townRecruitLine"), RecruitLimitNote());
            UpdateAffordability(campaignSaveManager.SaveData.goldAmount);
            view.SetFightSubtitle(LocalizationManager.Instance.GetText("sackTownFlavor"));
            view.SetBounty(BountyRangeText());
            SetUpGearPreview();
            SetUpPrediction();
            view.SetEliteRavagers(null, null, null);

            SetUpBattlefieldInfo();
        }
        // Mirrors the engagement panel's weather/biome pair so the garrison fight is legible before the
        // player commits.
        private void SetUpBattlefieldInfo()
        {
            SetUpWeatherInfo();
            SetUpBiomeInfo();
        }
        // The garrison battle is fought on this node, so this is the same seeded roll the map node and
        // EngagementPanel.GenerateBattlefield read.
        private void SetUpWeatherInfo()
        {
            MapRegion mapRegion = MapThemeManager.Instance.GetMapRegion(mapSceneUIManager.MapSceneManager.MapRace);
            Weather weather = CampaignSaveManager.GenerateNodeWeather(
                selectedNodeIndex,
                campaignSaveManager.SaveData.seed,
                campaignSaveManager.SaveData.bookNumber,
                mapRegion,
                campaignSaveManager.SaveData.ordealWeather);

            string weatherNameLocalized = LocalizationManager.Instance.GetText(weather.ToString());
            string weatherDescriptionLocalized = WeatherInfo.GetDescription(weather);
            view.SetWeather(weather, weatherNameLocalized, weatherDescriptionLocalized);
        }
        // Deliberately not the node's biome roll. Sacking a town is always a garrison fight, and both
        // EngagementPanel.GenerateBattlefield and GreyCompanyBattlefield force Biome.Plains for one, so
        // what the player actually gets is the walled garrison rather than any of the four biomes.
        private void SetUpBiomeInfo()
        {
            view.SetBattlefield(LocalizationManager.Instance.GetText("Garrison"));
        }
        // Shown the way HealTroopsOnTownEntry applies it, hero healing bonus included.
        private string HealPercentText()
        {
            float heal = Mathf.Min(1f, CampaignSaveManager.ApplyHealingBonus(campaignSaveManager.TownEntryHealAmount()));
            return $"{Mathf.RoundToInt(heal * 100f)}%";
        }
        private string RecruitRarityText()
        {
            switch (townSaveData.townSize)
            {
                case TownSize.Castle:
                    return string.Format(LocalizationManager.Instance.GetText("townRecruitUpTo"), $"<color={ColorData.Tier2}>{LocalizationManager.Instance.GetText("Uncommon")}</color>");
                case TownSize.City:
                    return string.Format(LocalizationManager.Instance.GetText("townRecruitUpTo"), $"<color={ColorData.Tier3}>{LocalizationManager.Instance.GetText("Rare")}</color>");
                default:
                    return $"<color={ColorData.Tier1}>{LocalizationManager.Instance.GetText("Common")}</color>";
            }
        }
        private string RecruitLimitNote()
        {
            string note = TownIsSameRace()
                ? $"<color={ColorData.Positive}>{LocalizationManager.Instance.GetText("UncappedRecruitment")}</color>"
                : LocalizationManager.Instance.GetText("townRecruitLimit");
            if (HeroBonusManager.Instance.ActiveHeroID == 1 || HeroBonusManager.Instance.ActiveHeroID == 2)
                note += $"\n<color={ColorData.Gold}>{LocalizationManager.Instance.GetText("IronLegionBonusDescription")}</color>";
            return note;
        }
        private int ActBonus() => 10 * (campaignSaveManager.SaveData.bookNumber - 1); //add 10 gold per book number to the bounty amount
        // The loot is rolled when the town is generated, so the player can weigh it before choosing a road.
        private void SetUpGearPreview()
        {
            List<GearID> loot = townSaveData.townLootGearIDs;
            GearTooltip(loot, out string title, out string body);
            if (title == null)
            {
                view.SetGearPreview(null, null, null);
                return;
            }
            List<string> names = new();
            foreach (GearID gearID in loot)
                names.Add($"<color=#{ColorUtility.ToHtmlStringRGB(ColorData.GetGearRarityColor(GearData.GetGear(gearID).GearRarity))}>{LocalizationManager.Instance.GetText(gearID + "Name")}</color>");
            string line = string.Format(LocalizationManager.Instance.GetText(loot.Count == 1 ? "townGearOne" : "townGearPick"), string.Join(", ", names));
            view.SetGearPreview(line, title, body);
        }
        private void SetUpPrediction()
        {
            CampaignSaveData saveData = campaignSaveManager.SaveData;
            bool hidden = townSaveData.townInteractionStatus != TownInteractionStatus.None
                || DifficultyRules.AutoResolveDisabled(saveData.difficultyLevel)
                || saveData.HasOrdeal(OrdealId.BlindMarch)
                || townSaveData.townGarrisonUnits == null || townSaveData.townGarrisonUnits.Length == 0;
            if (hidden)
            {
                view.SetPrediction("");
                return;
            }
            MapRegion mapRegion = MapThemeManager.Instance.GetMapRegion(mapSceneUIManager.MapSceneManager.MapRace);
            Weather weather = CampaignSaveManager.GenerateNodeWeather(selectedNodeIndex, saveData.seed, saveData.bookNumber, mapRegion, saveData.ordealWeather);
            bool win = mapSceneUIManager.EngagementPanel.PredictGarrisonAssault(townSaveData.townGarrisonUnits, weather);
            string result = $"<color={(win ? ColorData.Positive : ColorData.Negative)}>{LocalizationManager.Instance.GetText(win ? "Victory" : "Defeat")}</color>";
            view.SetPrediction(string.Format(LocalizationManager.Instance.GetText("engagementPredicts"), result));
        }
        private string BountyRangeText()
        {
            (int min, int max) = TownSaveData.GetEffectiveBountyRange(townSaveData.townSize);
            int bonus = ActBonus();
            return $"{min + bonus}-{Mathf.Max(min, max - 1) + bonus}<sprite name=GoldSprite>";
        }
        private void DisplayTownOptions()
        {
            ShowRoad(TownPanelView.Road.Undecided, false);
            LoadEnemyCompany();
        }
        private void ShowRoad(TownPanelView.Road road, bool animate)
        {
            view.ShowRoad(road, animate);
            continueButton.gameObject.SetActive(road != TownPanelView.Road.Undecided);
        }
        public async void LoadEnemyCompany()
        {
            // Rebuild from scratch. Nothing guarantees HideEnemyCompany ran first, so without this a second
            // DisplayTownOptions appends a whole extra set of garrison cards on top of the existing ones.
            foreach (Transform child in view.GarrisonGrid) {
                Destroy(child.gameObject);
            }
            SquadToLoad[] garrison = campaignSaveManager.SaveData.townData.townGarrisonUnits;
            view.SetGarrisonCount(garrison.Length);
            List<SquadDisplayCardMenu> enemySquadsCards = new ();
            foreach (SquadToLoad squad in garrison)
            {
                SquadDisplayCardMenu squadDisplayCardMenu = Instantiate(squadDisplayCardMenuPrefab, view.GarrisonGrid);
                enemySquadsCards.Add(squadDisplayCardMenu);
                squadDisplayCardMenu.SetUp(squad, false, mapSceneUIManager.HUDPanel, true);
                squadDisplayCardMenu.SpawnInJuice(false);
                await Task.Delay(100);
                // Leaving the Map mid-loop destroys this panel; a card made after that has no parent and leaks into the active scene.
                if (this == null) return;
            }
            foreach (SquadDisplayCardMenu squad in enemySquadsCards)
            {
                squad.MakeInteractable(true);
            }

        }
        public void HideEnemyCompany()
        {
            foreach (Transform child in view.GarrisonGrid) {
                Destroy(child.gameObject);
            }
        }
        public void ReloadTownPanel()
        {
            OpenPanel();
        }
        private void OpenPanel()
        {
            townPanelCanvasGroup.CGEnable();
            StartCoroutine(UIJuice.Open(townPanelCanvasGroup.GetComponent<CanvasGroup>(), view.transform as RectTransform));
        }
        private void LogTownChoice(string choice)
        {
            TabletopTavern.Analytics.NodeLog.Try("town", () => TabletopTavern.Analytics.NodeLog.Set("town", new Dictionary<string, object>
            {
                { "size", townSaveData.townSize.ToString() },
                { "race", townSaveData.townRace.ToString() },
                { "act", choice },
            }));
        }
        private void OnSackTown()
        {
            LogTownChoice("sack");
            CampaignManager.Instance.MapSceneUIManager.EngagementPanel.LoadEngagementPanelFromTown();
            townPanelCanvasGroup.FadeOutAsync(0.25f);
            HideEnemyCompany();
        }
        //Patch me
        public void LoadTownPostGarrisonEngagement()
        {
            campaignSaveManager.SetTownData(townSaveData);
            campaignSaveManager.AddCampaignTraitGoldToTownLoot();
            SetUpTownInfo();
            ShowRoad(TownPanelView.Road.Sacked, false);
            view.SetFightSubtitle($"{LocalizationManager.Instance.GetText("townGarrisonDefeated")} <color={ColorData.Negative}>{LocalizationManager.Instance.GetText("townReservesDidNotHeal")}</color>");
            OpenPanel();
            LootTown();
            view.PlaySacked();

            if (HeroBonusManager.Instance.ActiveHeroID == 5 || HeroBonusManager.Instance.ActiveHeroID == 6)
            {
                string[] unitNames = CampaignManager.Instance.CampaignSaveManager.PrestigeRandomUnits2();
                string displayText;
                if (unitNames != null)
                {
                    IAudioRequester.Instance.PlaySFX(SFXData.PrestigeUnit);
                    displayText = LocalizationManager.Instance.GetText(unitNames[0]);
                    if (unitNames[1] != null)
                        displayText += ", " + LocalizationManager.Instance.GetText(unitNames[1]);
                }
                else
                {
                    displayText = LocalizationManager.Instance.GetText("None");
                }
                view.SetEliteRavagers($"<color={ColorData.Gold}>{LocalizationManager.Instance.GetText("Elite Ravagers")}:</color> {displayText}",
                    LocalizationManager.Instance.GetText("Campaign Bonus"), LocalizationManager.Instance.GetText("RavenHostBonusDescription"));
            }
            else
            {
                view.SetEliteRavagers(null, null, null);
            }
        }
        public void LootTown()
        {
            // Debug.Log($"Setting up town info: {townSaveData.hasLootedGear} bountyAmount: {townSaveData.bountyAmount} gear IDs: {string.Join(", ", townSaveData.townLootGearIDs)}");
            IAudioRequester.Instance.PlaySFX(SFXData.SackTown);
            campaignSaveManager.RemoveZeroHealthSquads();

            int actBonus = ActBonus();
            string goldDetail;
            if (actBonus > 0)
            {
                string actInRomanNumerals = MemoriUI.ConvertNumberToRomanNumeral(campaignSaveManager.SaveData.bookNumber);
                goldDetail = string.Format(LocalizationManager.Instance.GetText("townSpoilGoldAct"), townSaveData.bountyAmount, actBonus, actInRomanNumerals);
            }
            else
            {
                goldDetail = LocalizationManager.Instance.GetText("townSpoilGold");
            }
            view.GoldRow.Set(LocalizationManager.Instance.GetText("Loot Gold"), goldDetail, $"{townSaveData.bountyAmount + actBonus}<sprite name=GoldSprite>");
            view.GoldRow.SetTaken(townSaveData.bountyAmount <= 0);
            spoilsShown = true;
            SetUpGearRow();
            view.GearRow.SetTaken(townSaveData.hasLootedGear);
            view.ConscriptRow.Set(LocalizationManager.Instance.GetText("Recruit Units"),
                LocalizationManager.Instance.GetText(TownSaveData.ConscriptPrestige(townSaveData.townSize) > 0 ? "townConscriptPrestige" : "townRecruitLine"),
                $"<color={ColorData.Positive}>{LocalizationManager.Instance.GetText("townSpoilFree")}</color>");
            view.ConscriptRow.SetTaken(false);

            SteamAchievements.Unlock(AchievementId.SackCity);
            SteamAchievements.AddStat(SteamStatId.CitiesSacked, 1);
            campaignSaveManager.SaveData.townsSacked++;
            switch (townSaveData.townSize)
            {
                case TownSize.Village: campaignSaveManager.SaveData.RunStats.villagesSacked++; break;
                case TownSize.Castle: campaignSaveManager.SaveData.RunStats.castlesSacked++; break;
                case TownSize.City: campaignSaveManager.SaveData.RunStats.citiesSacked++; break;
            }
            if(campaignSaveManager.SaveData.townsSacked >= 3) {
                SteamAchievements.Unlock(AchievementId.ThreeTownsSackedRun);
            }
            if (campaignSaveManager.SaveData.townsSacked >= 9 &&
                HeroData.GetRaceFromHero(campaignSaveManager.SaveData.heroID) == Race.RavenHost)
            {
                SteamAchievements.Unlock(AchievementId.NineRealms);
            }
        }
        private void OnEnterTown()
        {
            LogTownChoice("enter");
            townSaveData.townInteractionStatus = TownInteractionStatus.Entered;
            IAudioRequester.Instance.PlaySFX(SFXData.EnterTown);

            campaignSaveManager.HealTroopsOnTownEntry();
            campaignSaveManager.SetTownData(townSaveData);

            ShowRoad(TownPanelView.Road.Entered, true);
            view.PlayEntered();
            view.SetEnterStrip(LocalizationManager.Instance.GetText("townHealed"), HealPercentText(), RecruitRarityText());
            view.SetEnterLines($"<color={ColorData.Positive}>{string.Format(LocalizationManager.Instance.GetText("townHealedLine"), HealPercentText())}</color>",
                LocalizationManager.Instance.GetText("townRecruitLine"), RecruitLimitNote());

            imperialEdictActive = (HeroBonusManager.Instance.ActiveHeroID == 1 || HeroBonusManager.Instance.ActiveHeroID == 2)
                && !campaignSaveManager.SaveData.IsFactionPassiveBlocked(Race.IronLegion);
            UpdateAffordability(campaignSaveManager.SaveData.goldAmount);

            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.TownExplanation);
        }
        public void OnLootGearButtonClicked()
        {
            if (townSaveData.townLootGearIDs == null || townSaveData.townLootGearIDs.Count == 0)
            {
                Debug.LogError("[TownPanel] OnLootGearButtonClicked: townLootGearIDs is empty.");
                return;
            }
            // A single item was shown on the row already, so it is taken on the click with no chest to open.
            if (townSaveData.townLootGearIDs.Count == 1)
            {
                if (!campaignSaveManager.CanAquireGear())
                {
                    NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("No space for gear"));
                    return;
                }
                GearID gearID = townSaveData.townLootGearIDs[0];
                TabletopTavern.Analytics.NodeLog.Try("town loot", () => TabletopTavern.Analytics.NodeLog.Add("loot",
                    new Dictionary<string, object> { { "k", "gear" }, { "v", gearID.ToString() } }));
                IAudioRequester.Instance.PlaySFX(SFXData.SelectCard);
                campaignSaveManager.AquireGear(gearID);
                TooltipManager.Instance.HideTooltip();
                view.GearRow.Button.OnPointerExit(null);
                view.GearRow.SetTaken(true, true);
                townSaveData.hasLootedGear = true;
                campaignSaveManager.SetTownData(townSaveData);
                return;
            }
            treasurePanel.LoadTreasurePanelFromTown(townSaveData.townLootGearIDs, OnLootGearClosed);
            view.GearRow.SetTaken(true, true);
            townSaveData.hasLootedGear = true;
        }
        // One item shows itself on the row: its art, name, rarity and a hover with what it does. A pick keeps the chest.
        private void SetUpGearRow()
        {
            List<GearID> loot = townSaveData.townLootGearIDs;
            string leave = string.Format(LocalizationManager.Instance.GetText("townSpoilGearLeave"), $"{TownSaveData.GearLeftGold(townSaveData.townSize)}<sprite name=GoldSprite>");
            if (loot == null || loot.Count != 1)
            {
                int choices = loot == null ? 0 : loot.Count;
                view.GearRow.Set(LocalizationManager.Instance.GetText("Loot Gear"), leave,
                    choices > 1 ? string.Format(LocalizationManager.Instance.GetText("townSpoilGearPick"), choices) : LocalizationManager.Instance.GetText("townSpoilGearValue"));
                view.GearRow.SetIcon(null);
                GearTooltip(loot, out string title, out string body);
                view.GearRow.SetTooltip(title, body);
                return;
            }
            Gear gear = GearData.GetGear(loot[0]);
            string rarity = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorData.GetGearRarityColor(gear.GearRarity))}>{LocalizationManager.Instance.GetText(gear.GearRarity.ToString())}</color>";
            string name = LocalizationManager.Instance.GetText(loot[0] + "Name");
            view.GearRow.Set(name, leave, rarity);
            view.GearRow.SetIcon(SpriteData.GetSprite(gear.GearName));
            string description = string.Format(LocalizationManager.Instance.GetText(loot[0] + "Desc"), gear.GearModifierValue);
            view.GearRow.SetTooltip(name, $"{rarity}\n{KeywordText.Render(description, false)}");
        }
        // Every item on offer, named in its rarity colour with what it does.
        private void GearTooltip(List<GearID> loot, out string title, out string body)
        {
            title = body = null;
            if (loot == null || loot.Count == 0) return;
            List<string> details = new();
            foreach (GearID gearID in loot)
            {
                Gear gear = GearData.GetGear(gearID);
                string name = $"<color=#{ColorUtility.ToHtmlStringRGB(ColorData.GetGearRarityColor(gear.GearRarity))}>{LocalizationManager.Instance.GetText(gearID + "Name")}</color>";
                string description = string.Format(LocalizationManager.Instance.GetText(gearID + "Desc"), gear.GearModifierValue);
                details.Add($"{name}\n{KeywordText.Render(description, false)}");
            }
            title = LocalizationManager.Instance.GetText("Loot Gear");
            body = string.Join("\n\n", details);
        }
        // Gear still in the vault when the player walks on pays its gold, the same as leaving it on the pick screen.
        private void PayForGearLeftBehind()
        {
            if (!spoilsShown || townSaveData.hasLootedGear) return;
            if (townSaveData.townLootGearIDs == null || townSaveData.townLootGearIDs.Count == 0) return;
            townSaveData.hasLootedGear = true;
            view.GearRow.SetTaken(true);
            OnLootGearClosed(false);
        }
        // Gear left in the vault pays gold, so a full gear bar never makes the reward worth nothing.
        private void OnLootGearClosed(bool gearTaken)
        {
            if (gearTaken) return;
            int gold = TownSaveData.GearLeftGold(townSaveData.townSize);
            TabletopTavern.Analytics.NodeLog.Try("town loot", () => TabletopTavern.Analytics.NodeLog.Add("loot",
                new Dictionary<string, object> { { "k", "gearLeft" }, { "v", gold } }));
            campaignSaveManager.AddSpoilsGoldToLastBattle(gold);
            goldManager.ModifyGold(gold, LocalizationManager.Instance.GetText("townGearLeft"));
            campaignSaveManager.SetTownData(townSaveData);
        }
        public void OnLootGearCardSelected()
        {
            townSaveData.hasLootedGear = true;
            campaignSaveManager.SetTownData(townSaveData);

            view.GearRow.Button.OnPointerExit(null);
            view.GearRow.SetTaken(true);
        }
        public void OnLootGoldButtonClicked()
        {
            string localizedString = LocalizationManager.Instance.GetText("Loot Gold");
            int looted = townSaveData.bountyAmount + ActBonus();
            TabletopTavern.Analytics.NodeLog.Try("town loot", () => TabletopTavern.Analytics.NodeLog.Add("loot",
                new Dictionary<string, object> { { "k", "gold" }, { "v", looted } }));
            campaignSaveManager.AddSpoilsGoldToLastBattle(looted);
            goldManager.ModifyGold(looted, localizedString);
            townSaveData.bountyAmount = 0;
            campaignSaveManager.SetTownData(townSaveData);
            view.GoldRow.Button.OnPointerExit(null);

            view.GoldRow.SetTaken(true, true);
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
            if (DifficultyRules.RecruitCostIncreased(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel)) modifiedRecruitmentCost += 2;
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.HasOrdeal(OrdealId.PressGanged)) modifiedRecruitmentCost += OrdealRegistry.PRESS_GANGED_RECRUIT_RISE;
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
            recruitPanel.LoadRecruitPanelFromTown(townSaveData.townRace, townSaveData.townSize, TownSaveData.ConscriptPrestige(townSaveData.townSize));
            view.ConscriptRow.Button.OnPointerExit(null);
            view.ConscriptRow.SetTaken(true, true);
        }
        public void CompleteTown()
        {
            PayForGearLeftBehind();
            mapSceneUIManager.TryDrainPendingPrestigeChoices(() => mapSceneUIManager.CompleteLayerAction());
        }
        public override void ClosePanel()
        {
            Debug.Log("[Map] Closing TownPanel");
            goldManager.OnGoldAmountChanged -= UpdateAffordability;
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0f, 0.25f));
            HideEnemyCompany();

            IAudioRequester.Instance.PlaySFX(SFXData.CloseUI);
            townPanelCanvasGroup.FadeOutAsync(UIJuice.CloseTime);
        }
        public void DisableTownCanvasesOnLoss()
        {
            goldManager.OnGoldAmountChanged -= UpdateAffordability;
            HideEnemyCompany();

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
            if(DifficultyRules.RecruitCostIncreased(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel)) {
                modifiedRecruitmentCost += 2;
            }
            if (CampaignManager.Instance.CampaignSaveManager.SaveData.HasOrdeal(OrdealId.PressGanged))
                modifiedRecruitmentCost += OrdealRegistry.PRESS_GANGED_RECRUIT_RISE;


            string colorString = _goldAmount >= modifiedRecruitmentCost ? ColorData.Primary : ColorData.Negative;

            if (imperialEdictActive)
            {
                modifiedRecruitmentCost = 0;
                colorString = ColorData.Positive;
            }

            view.SetCost($"<color={colorString}>{modifiedRecruitmentCost}</color><sprite name=GoldSprite>");
        }
        public void OnDestroy()
        {
            if (goldManager == null) return;

            goldManager.OnGoldAmountChanged -= UpdateAffordability;
        }
        private void SetRecruitmentAvailable(bool available)
        {
            view.SetRecruitAvailable(available);
        }
        private bool TownIsSameRace()
        {
            return townSaveData.townRace == HeroData.GetRaceFromHero(CampaignManager.Instance.CampaignSaveManager.GetHeroID());
        }
        // Built when the town loads because garrison sizes depend on the current act and difficulty.
        private TooltipContent TownInfoContent()
        {
            string caption = $"<line-height=135%><size=11><color=#8C9AA2><uppercase><pos=46%>{LocalizationManager.Instance.GetText("Garrison")}"
                + $"<pos=72%>{LocalizationManager.Instance.GetText("Bounty")}</uppercase></color></size>";
            return new TooltipContent
            {
                Title = LocalizationManager.Instance.GetText("Town Info"),
                Body = LocalizationManager.Instance.GetText("townDescription"),
                Footer = caption
                    + TownInfoRow(TownSize.Village, ColorData.Tier1)
                    + TownInfoRow(TownSize.Castle, ColorData.Tier2)
                    + TownInfoRow(TownSize.City, ColorData.Tier3),
                Width = 420f,
            };
        }
        // One table line: the size in its tier colour, garrison squads, bounty. A diamond marks the town being visited.
        private string TownInfoRow(TownSize townSize, string colour)
        {
            string marker = townSize == townSaveData.townSize ? $"<color={ColorData.Gold}>\u25C6</color>" : "";
            return $"\n<size=15>{marker}<pos=6%><color={colour}>{LocalizationManager.Instance.GetText(townSize.ToString())}</color>"
                + $"<pos=46%>{GarrisonSize(townSize)}<pos=72%>{BountyRange(townSize)}<sprite name=GoldSprite></size>";
        }
        private string BountyRange(TownSize townSize)
        {
            (int min, int max) = TownSaveData.GetEffectiveBountyRange(townSize);
            int bonus = ActBonus();
            return $"{min + bonus}-{Mathf.Max(min, max - 1) + bonus}";
        }
        private int GarrisonSize(TownSize townSize)
        {
            CampaignSaveData saveData = campaignSaveManager.SaveData;
            bool strongerGarrisons = DifficultyRules.StrongerGarrisons(saveData.difficultyLevel);
            int count = 0;
            foreach (TierCount entry in ArmyGenerationRuleData.ResolveTownGarrisonTierCounts(townSize, saveData.bookNumber, strongerGarrisons))
                count += entry.Count;
            // Must match the squad ArmyCreator.GenerateTownGarrison drops on Easy in act 1.
            if (DifficultyRules.SmallerGarrison(saveData.difficultyLevel, saveData.bookNumber) && count > 1) count--;
            // Must match ArmyCreator.AddEliteSquad, which stops at the deployment cap.
            if (saveData.HasOrdeal(OrdealId.EliteGuard) && count < TabletopTavernConstants.ENDLESS_ENEMY_SQUAD_CAP) count++;
            // Must match the squad CampaignSaveManager.GenerateTown drops for Aura Farming.
            if (CampaignManager.Instance.GearManager.CheckForGear(GearID.AuraFarming)) count--;
            return Mathf.Max(count, 0);
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
        TownSize.Village => (8, 13),
        TownSize.Castle => (16, 21),
        TownSize.City => (26, 31),
        _ => (0, 0),
    };
    // How many gear items a sacked town offers; the player keeps one.
    public static int LootGearChoices(TownSize townSize) => townSize switch
    {
        TownSize.Castle => 2,
        TownSize.City => 3,
        _ => 1,
    };
    public static int GearLeftGold(TownSize townSize) => townSize switch
    {
        TownSize.Castle => 15,
        TownSize.City => 20,
        _ => 10,
    };
    // A City sits one step before the act's final battle, so its free recruit arrives ready for it.
    public static int ConscriptPrestige(TownSize townSize) => townSize == TownSize.City ? 1 : 0;
    public static (int Min, int Max) GetEffectiveBountyRange(TownSize townSize) =>
        BountyRangeOverrides.TryGetValue(townSize, out var range) ? range : GetDefaultBountyRange(townSize);

    public static int GenerateBountyAmount(TownSize _townWealth, int _seed)
    {
        System.Random random = new(Seed: _seed);
        var range = GetEffectiveBountyRange(_townWealth);
        return random.Next(range.Min, range.Max);
    }
}
 