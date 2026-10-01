using System.Collections.Generic;
using System.Threading.Tasks;
using Memori.Audio;
using Memori.UI;
using Memori.Localization;
using Memori.Notifications;
using Memori.Utilities;
using TJ;
using TJ.Map;
using UnityEngine;
using UnityEngine.UI;
using Memori.SaveData;

namespace TJ.Campfire
{
    public class CampfirePanel : MapPanel
    {
        // Stored as NodeResume.choiceIndex, so values are append-only.
        public enum CampfireChoice { None, Rest, Train, Scavenge, Scout }

        [Header("View")]
        [SerializeField] private CampfirePanelView view;

        [Header("Train Cards")]
        [SerializeField] private SquadDisplayCardMenu squadCardPrefab;

        [Header("Map Overview")]
        [SerializeField] private MapOverviewPanel mapOverviewPanel;

        [Header("Continue")]
        [SerializeField] private Button continueButton;

        private const float RestHealAmount = 0.3f;
        private const int GearScavengeCount = 3;
        private const int MaxPrestige = 2;

        // Indexed by act - 1; endless acts keep the act 3 values.
        private static readonly int[] TrainCostToFirstLevelByAct = { 10, 20, 30 };
        private static readonly int[] TrainCostToSecondLevelByAct = { 20, 40, 60 };
        private static readonly int[] ScoutGoldByAct = { 10, 20, 35 };

        private int ActIndex => Mathf.Clamp(campaignSaveManager.SaveData.bookNumber, 1, ScoutGoldByAct.Length) - 1;
        private int TrainCostToFirstLevel => TrainCostToFirstLevelByAct[ActIndex];
        private int TrainCostToSecondLevel => TrainCostToSecondLevelByAct[ActIndex];
        private int ScoutGold => ScoutGoldByAct[ActIndex];
        private const int DeployedSlots = 10;

        private CampaignSaveManager campaignSaveManager;
        private MapSceneUIManager mapSceneUIManager;
        private MemoriCanvasGroup panelCanvasGroup;

        // Read by the nodeCompleted report when the layer completes.
        public CampfireChoice Chosen { get; private set; }
        public string TrainedUnit { get; private set; }
        public int TrainedPrestige { get; private set; }
        public string TrainedSquadId { get; private set; }

        private void Awake()
        {
            panelCanvasGroup = GetComponent<MemoriCanvasGroup>();
        }

        public void SetUp(CampaignSaveManager _csm, MapSceneUIManager _msui)
        {
            campaignSaveManager = _csm;
            mapSceneUIManager = _msui;

            view.SetUp(mapOverviewPanel, mapSceneUIManager);
            view.RestButton.onClick.AddListener(OnRest);
            view.TrainButton.onClick.AddListener(OpenTrainPicker);
            view.ScavengeButton.onClick.AddListener(OnScavenge);
            view.ScoutButton.onClick.AddListener(OnScoutAhead);
            view.BackButton.onClick.AddListener(CloseTrainPicker);
            if (continueButton != null) continueButton.onClick.AddListener(() =>
            {
                continueButton.interactable = false;
                mapSceneUIManager.TryDrainPendingPrestigeChoices(() => mapSceneUIManager.CompleteLayerAction());
            });
        }

        public void LoadCampfirePanel()
        {
            Chosen = CampfireChoice.None;
            TrainedUnit = null;
            TrainedPrestige = 0;
            TrainedSquadId = null;
            OpenFeedback.PlayFeedbacks();

            view.ClearSlots();
            view.SetChoicesInteractable(true);
            FillChoices();
            SetContinueVisible(false);
            if (continueButton != null) continueButton.interactable = true;

            panelCanvasGroup.CGEnable();
            StartCoroutine(UIJuice.Open(panelCanvasGroup.GetComponent<CanvasGroup>(), view.transform as RectTransform));
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);

            if (TryResumeLockedChoice()) return;
            ShowChoosing();
            view.StaggerChoices();
        }

        private void FillChoices()
        {
            int healPercent = RestHealPercent();
            view.SetRest($"{healPercent}%", string.Format(Text("campfireRestLine"), healPercent));

            int trainable = GetTrainableSquads().Count;
            view.SetTrain(
                $"{string.Format(Text("campfireCostRange"), TrainCostToFirstLevel, TrainCostToSecondLevel)} {TabletopTavernConstants.GOLD_SPRITE_STRING}",
                string.Format(Text("campfireTrainLine"), trainable, CountLiveSquads()),
                string.Format(Text("campfireTrainPrices"), TrainCostToFirstLevel, TrainCostToSecondLevel),
                trainable > 0);

            view.SetScavenge(string.Format(Text("campfireGearValue"), GearScavengeCount), string.Format(Text("campfireScavengeLine"), GearScavengeCount));

            bool slotsFull = !campaignSaveManager.HasRoomForConsumable();
            view.SetScout($"+{ScoutGold} {TabletopTavernConstants.GOLD_SPRITE_STRING}",
                Text(slotsFull ? "campfireScoutSlotsFull" : "campfireScoutConsumable"), slotsFull);
        }

        private void ShowChoosing()
        {
            view.SetHeader(Text("campfirePickOne"), Text("campfirePickOneSub"));
            view.ShowState(CampfirePanelView.State.Choosing);
        }

        // Scout and Scavenge show hidden things, so a quit reopens the locked choice instead of offering the options again.
        private bool TryResumeLockedChoice()
        {
            NodeResume resume = campaignSaveManager.SaveData.nodeResume;
            if (!resume.active || resume.nodeType != NodeType.Campfire || resume.nodeIndex != mapSceneUIManager.LayerNodeSelected) return false;

            Chosen = (CampfireChoice)resume.choiceIndex;
            view.SetChoicesInteractable(false);
            if (Chosen == CampfireChoice.Scavenge)
            {
                OpenScavengeTreasure();
                return true;
            }
            ShowResult(CampfireChoice.Scout,
                Text("CampfireScoutAhead"),
                string.Format(Text("CampfireScoutGoldDesc"), ScoutGold),
                new List<(string, string)>
                {
                    (Text("Gold"), $"+{ScoutGold} {TabletopTavernConstants.GOLD_SPRITE_STRING}"),
                    (Text("campfireCaptionReveals"), Text("campfireThisAct")),
                });
            return true;
        }

        private void LockChoice(CampfireChoice _choice)
        {
            campaignSaveManager.LockNodeResult(new NodeResume
            {
                nodeIndex = mapSceneUIManager.LayerNodeSelected,
                nodeType = NodeType.Campfire,
                choiceIndex = (int)_choice,
            });
        }

        private void OnRest()
        {
            Chosen = CampfireChoice.Rest;
            view.SetChoicesInteractable(false);
            if (mapOverviewPanel != null) mapOverviewPanel.Close();
            campaignSaveManager.ModifyTroopHealth(RestHealAmount);
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.ArmyStructureChanged();
            int healPercent = RestHealPercent();
            ShowResult(CampfireChoice.Rest,
                Text("CampfireRest"),
                string.Format(Text("CampfireRestDesc"), healPercent),
                new List<(string, string)>
                {
                    (Text("townHeal"), $"{healPercent}%"),
                    (Text("campfireCaptionSquads"), CountLiveSquads().ToString()),
                });
        }

        private int RestHealPercent() => Mathf.RoundToInt(CampaignSaveManager.ApplyHealingBonus(RestHealAmount) * 100f);

        private List<SquadToLoad> GetTrainableSquads()
        {
            List<SquadToLoad> eligible = new();
            foreach (var squad in campaignSaveManager.SaveData.playerArmy)
                if (squad.UnitIndex != -1 && !squad.isEmptySquad && squad.SquadCurrentHealth > 0 && squad.UnitPrestige < MaxPrestige)
                    eligible.Add(squad);
            return eligible;
        }

        private int CountLiveSquads()
        {
            int count = 0;
            foreach (var squad in campaignSaveManager.SaveData.playerArmy)
                if (squad.UnitIndex != -1 && !squad.isEmptySquad && squad.SquadCurrentHealth > 0) count++;
            return count;
        }

        private int TrainCost(SquadToLoad _squad) => _squad.UnitPrestige == 0 ? TrainCostToFirstLevel : TrainCostToSecondLevel;

        private string PriceText(SquadToLoad _squad)
        {
            int cost = TrainCost(_squad);
            string price = CampaignManager.Instance.GoldManager.CheckIfCanAfford(cost) ? cost.ToString() : $"<color={ColorData.Negative}>{cost}</color>";
            return $"{price} {TabletopTavernConstants.GOLD_SPRITE_STRING}";
        }

        private void OpenTrainPicker()
        {
            if (mapOverviewPanel != null) mapOverviewPanel.Close();
            view.ClearSlots();

            // Slot order matches the army bar: deployed first, then reserves. The array can be shorter than 10 + reserves.
            SquadToLoad[] army = campaignSaveManager.SaveData.playerArmy;
            int slotCount = Mathf.Min(army.Length, DeployedSlots + campaignSaveManager.MaxReserveSlots);
            bool hasReserve = false;
            for (int i = 0; i < slotCount; i++)
            {
                SquadToLoad squad = army[i];
                if (squad.UnitIndex == -1 || squad.isEmptySquad || squad.SquadCurrentHealth <= 0) continue;
                bool inReserve = i >= DeployedSlots;
                hasReserve |= inReserve;
                bool trainable = squad.UnitPrestige < MaxPrestige;
                view.AddSlot(inReserve).SetUp(squad, squadCardPrefab, inReserve,
                    trainable ? PriceText(squad) : Text("campfireMax"), trainable, OnTrainSquadPicked, OnTrainSquadHovered);
            }

            view.SetTrainCounts(string.Format(Text("campfireCanTrainCount"), GetTrainableSquads().Count, CountLiveSquads()), Text("campfireTrainHint"), hasReserve);
            view.ShowState(CampfirePanelView.State.Train);
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
        }

        private void OnTrainSquadHovered(SquadToLoad _squad, bool _hovered)
        {
            if (!_hovered)
            {
                view.SetTrainDetail(null);
                return;
            }
            string unitName = campaignSaveManager.GetUnitNameOrUnitNameOverride(_squad.UniqueID);
            view.SetTrainDetail(_squad.UnitPrestige >= MaxPrestige
                ? string.Format(Text("campfireTrainDetailMax"), unitName)
                : string.Format(Text("campfireTrainDetail"), unitName, _squad.UnitPrestige + 1, PriceText(_squad)));
        }

        private void CloseTrainPicker()
        {
            view.ClearSlots();
            ShowChoosing();
        }

        private void OnTrainSquadPicked(SquadToLoad _squad)
        {
            int cost = TrainCost(_squad);
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(cost))
            {
                NotificationManager.Instance.ErrorNotification(Text("NotEnoughGold"));
                return;
            }

            Chosen = CampfireChoice.Train;
            TrainedUnit = _squad.UnitName.ToString();
            TrainedPrestige = _squad.UnitPrestige + 1;
            TrainedSquadId = _squad.UniqueID;
            view.SetChoicesInteractable(false);
            string unitName = campaignSaveManager.GetUnitNameOrUnitNameOverride(_squad.UniqueID);

            // The prestige lands first so the one save ModifyGold writes holds both.
            campaignSaveManager.PrestigeSpecificUnit(_squad);
            campaignSaveManager.RegisterCampfireTraining();
            IAudioRequester.Instance.PlaySFX(SFXData.PrestigeUnit);
            CampaignManager.Instance.GoldManager.ModifyGold(-cost, Text("Train"));

            view.ClearSlots();
            ShowResult(CampfireChoice.Train,
                Text("CampfireTrainSuccess"),
                string.Format(Text("CampfireTrainSuccessDesc"), unitName),
                new List<(string, string)>
                {
                    (Text("campfireCaptionSquad"), unitName),
                    (Text("Prestige"), TrainedPrestige.ToString()),
                    (Text("Gold"), $"-{cost} {TabletopTavernConstants.GOLD_SPRITE_STRING}"),
                });
        }

        private void OnScavenge()
        {
            Chosen = CampfireChoice.Scavenge;
            view.SetChoicesInteractable(false);
            if (mapOverviewPanel != null) mapOverviewPanel.Close();
            LockChoice(CampfireChoice.Scavenge);
            OpenScavengeTreasure();
        }

        private void OpenScavengeTreasure()
        {
            panelCanvasGroup.FadeOutAsync();
            mapSceneUIManager.SetActivePanel(mapSceneUIManager.TreasurePanel);
            mapSceneUIManager.TreasurePanel.LoadTreasurePanelFromMapNode(GearScavengeCount);
        }

        private void OnScoutAhead()
        {
            Chosen = CampfireChoice.Scout;
            view.SetChoicesInteractable(false);
            if (mapOverviewPanel != null) mapOverviewPanel.Close();
            int activeLayer = mapSceneUIManager.MapSceneManager.GetActiveChapterIndex();
            mapSceneUIManager.MapSceneManager.RevealNodesInNextLayers(activeLayer, 100);
            if (mapOverviewPanel != null) mapOverviewPanel.Refresh();

            string description;
            string found;
            if (campaignSaveManager.HasRoomForConsumable())
            {
                int bookNumber = campaignSaveManager.SaveData.bookNumber;
                ConsumableEnum consumable = ConsumableData.GetWeightedConsumable(bookNumber, campaignSaveManager.GetSeededRandom());
                campaignSaveManager.AquireConsumable(consumable);
                found = Text(consumable.ToString() + "Name");
                description = string.Format(Text("CampfireScoutFoundDesc"), found, ScoutGold);
            }
            else
            {
                NotificationManager.Instance.ErrorNotification(Text("noRoomForConsumable"));
                found = Text("campfireNothing");
                description = string.Format(Text("CampfireScoutGoldDesc"), ScoutGold);
            }

            CampaignManager.Instance.GoldManager.ModifyGold(ScoutGold, Text("CampfireScoutAhead"));
            LockChoice(CampfireChoice.Scout);
            ShowResult(CampfireChoice.Scout, Text("CampfireScoutAhead"), description,
                new List<(string, string)>
                {
                    (Text("Gold"), $"+{ScoutGold} {TabletopTavernConstants.GOLD_SPRITE_STRING}"),
                    (Text("campfireCaptionFound"), found),
                    (Text("campfireCaptionReveals"), Text("campfireThisAct")),
                });
        }

        private void ShowResult(CampfireChoice choice, string title, string description, List<(string, string)> cells)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            view.SetHeader(Text("campfireReadyTitle"), Text("campfireReadySub"));
            view.ShowResult(choice, title, description, cells);
            SetContinueVisible(true);
        }

        private void SetContinueVisible(bool visible)
        {
            if (continueButton != null) continueButton.gameObject.SetActive(visible);
        }

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);

        public override async void ClosePanel()
        {
            CloseFeedback();
            view.ClearSlots();
            campaignSaveManager.RemoveZeroHealthSquads();
            await Task.Delay(200);
            panelCanvasGroup.FadeOutAsync(UIJuice.CloseTime);
        }
    }
}
