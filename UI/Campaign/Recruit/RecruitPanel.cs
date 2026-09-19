using TJ.Map;
using UnityEngine;
using Memori.Utilities;
using UnityEngine.UI;
using TMPro;
using Memori.Audio;
using Memori.Notifications;
using Memori.Tooltip;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;
using Memori.Localization;
using Memori.Metaprogression;
using Memori.SaveData;

namespace TJ.Recruit
{
    [RequireComponent(typeof(MemoriCanvasGroup))]
    public class RecruitPanel : MapPanel
    {
        [Header("Recruitment")]
        [SerializeField] private RecruitCard recruitCardPrefab;
        [SerializeField] private Transform recruitCardsParent;

        [Header("Gear")]
        [SerializeField] private GearCard gearCardPrefab;
        [SerializeField] private Transform gearCardsParent;

        [Header("UI Elements")]
        [SerializeField] private Button skipButton;
        // [SerializeField] private Animator chooseACardPopupPrefab;

        [Header("Scene References")]
        [SerializeField] private RecruitmentScene recruitmentScene;

        [Header("Metaprogression")]
        [SerializeField] private MetaprogressionModel _postBattleRecruitMetaprogressionModel;

        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;
        MemoriCanvasGroup memoriCanvasGroup;
        SquadStats[] recruitmentOptions;

        List<GearCard> gearCards = new List<GearCard>();
        List<RecruitCard> recruitCards = new List<RecruitCard>();
        RecruitCard hoveredCard;
        bool hasSelectedRecruitCard = false;
        CancellationTokenSource _cardLoadCts;
        public enum RecruitmentType { Shop, Town, Battle, Conscription }
        RecruitmentType recruitmentType = RecruitmentType.Shop;
        
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            memoriCanvasGroup = GetComponent<MemoriCanvasGroup>();
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
        }
        public void LoadRecruitPanelFromShop(CardPackData _cardPackData)
        {
            recruitmentType = RecruitmentType.Shop;
            hasSelectedRecruitCard = false;
            // chooseACardPopupPrefab.SetBool("Active", true);
            recruitCards = new();
            gearCards = new();

            GenerateRecruitsFromCardPack(_cardPackData.packID);
            LoadRecruitCards();
            recruitmentScene.SetUp(campaignSaveManager.SaveData.recruitableUnits);
            campaignSaveManager.IncrementRerollCount(3);
            skipButton.gameObject.SetActive(true);
        }
        public void LoadRecruitPanelFromTown(Race _townRace, TownSize townsize)
        {
            recruitmentType = RecruitmentType.Town;
            hasSelectedRecruitCard = false;
            // chooseACardPopupPrefab.SetBool("Active", true);
            recruitCards = new();
            gearCards = new();

            GenerateRecruitsFromRaceTown(_townRace, townsize);
            LoadRecruitCards();
            recruitmentScene.SetUp(campaignSaveManager.SaveData.recruitableUnits);
            campaignSaveManager.IncrementRerollCount(3);
            skipButton.gameObject.SetActive(true);
        }
        public void LoadRecruitPanelFromBattle(Race _playerRace, UnitRarity unitRarity)
        {
            recruitmentType = RecruitmentType.Battle;
            hasSelectedRecruitCard = false;
            // chooseACardPopupPrefab.SetBool("Active", true);
            recruitCards = new();
            gearCards = new();

            GenerateRecruitsFromRaceBattle(_playerRace, unitRarity);
            LoadRecruitCards();
            recruitmentScene.SetUp(campaignSaveManager.SaveData.recruitableUnits);
            campaignSaveManager.IncrementRerollCount(3);
            skipButton.gameObject.SetActive(true);
        }
        public void LoadRecruitPanelForConscript(UnitName[] unitsToConscript)
        {
            recruitmentType = RecruitmentType.Conscription;
            hasSelectedRecruitCard = false;
            // chooseACardPopupPrefab.SetBool("Active", true);
            recruitCards = new();
            gearCards = new();

            campaignSaveManager.SaveRecruitableUnits(unitsToConscript);
            LoadRecruitCards();
            recruitmentScene.SetUp(campaignSaveManager.SaveData.recruitableUnits);
            campaignSaveManager.IncrementRerollCount(3);
            skipButton.gameObject.SetActive(true);
        }
        public void GenerateRecruitsFromCardPack(int _cardPackID)
        {
            UnitName[] unitNames = TabletopTavernData.Instance.GetSquadsToRecruitForPack(
                _cardPackID,
                _cardPackID == 4 ? 1 : 3,
                campaignSaveManager.GetSeededRandom(),
                CampaignManager.Instance.CampaignSaveManager.GetHeroID(),
                _cardPackID
            );
        
            campaignSaveManager.SaveRecruitableUnits(unitNames);
        }
        public void GenerateRecruitsFromRaceTown(Race _townRace, TownSize townsize)
        {
            campaignSaveManager.SaveRecruitableUnits(
                TabletopTavernData.Instance.GetSquadsToRecruitForRaceTown(_townRace, 3, campaignSaveManager.GetSeededRandom(), _townsize: townsize));
        }
        public void GenerateRecruitsFromRaceBattle(Race _playerRace, UnitRarity unitRarity)
        {
            campaignSaveManager.SaveRecruitableUnits(
                TabletopTavernData.Instance.GetSquadsToRecruitForRaceBattle(_playerRace, 3, campaignSaveManager.GetSeededRandom(), unitRarity));
        }
        public async void LoadGearCards()
        {
            _cardLoadCts?.Cancel();
            _cardLoadCts = new CancellationTokenSource();
            var token = _cardLoadCts.Token;

            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(1, 0.25f));

            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(CloseRecruitPanel);

            GearID[] recruitableGear = campaignSaveManager.SaveData.recruitableGear;
            if (recruitableGear == null) return;

            memoriCanvasGroup.CGEnable();

            for (int i = 0; i < recruitableGear.Length; i++) {
                if (token.IsCancellationRequested) break;
                GearCard gearCard = Instantiate(gearCardPrefab, gearCardsParent);
                gearCard.LoadGearCardShop(recruitableGear[i], this);
                gearCards.Add(gearCard);
                await Task.Delay(100);
            }
        }
        public async void LoadRecruitCards()
        {
            _cardLoadCts?.Cancel();
            _cardLoadCts = new CancellationTokenSource();
            var token = _cardLoadCts.Token;

            OpenFeedback.PlayFeedbacks();
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(1, 0.25f));

            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(CloseRecruitPanel);

            UnitName[] recruitableNames = campaignSaveManager.SaveData.recruitableUnits;
            // Debug.Log($"length of recruitable names in load recruit cards: {recruitableNames.Length}");
            recruitmentOptions = new SquadStats[recruitableNames.Length];

            for(int i = 0; i < recruitableNames.Length; i++) {
                SquadStats squadStats = TabletopTavernData.Instance.GetSquadStats(recruitableNames[i]);
                recruitmentOptions[i] = squadStats;
            }

            RenderTexture GetRawImage(int _index)
            {
                switch (_index)
                {
                    case 0: return recruitmentScene.RecruitmentRenderTexture1;
                    case 1: return recruitmentScene.RecruitmentRenderTexture2;
                    case 2: return recruitmentScene.RecruitmentRenderTexture3;
                    default: return null;
                }
            }

            memoriCanvasGroup.CGEnable();

            for (int i = 0; i < recruitmentOptions.Length; i++) {
                if (token.IsCancellationRequested) break;
                RecruitCard recruitCard = Instantiate(recruitCardPrefab, recruitCardsParent);
                // bool isPurchased = campaignSaveManager.SaveData.townData.recruitedIndices.Contains(i);
                recruitCard.SetUp(recruitmentOptions[i], this, i, GetRawImage(i), false);
                recruitCard.AddHoverToAttributes();
                recruitCards.Add(recruitCard);
                await Task.Delay(200);
            }
        }
        // One hovered card grows and lifts; the rest shrink. Null returns every card to its breath.
        public void SetHoveredCard(RecruitCard hovered)
        {
            hoveredCard = hovered;
            for (int i = 0; i < recruitCards.Count; i++)
            {
                RecruitCard.HoverMotion state = hovered == null ? RecruitCard.HoverMotion.Idle
                    : recruitCards[i] == hovered ? RecruitCard.HoverMotion.Hovered
                    : RecruitCard.HoverMotion.Neighbour;
                recruitCards[i].SetHoverMotion(state);
            }
        }
        // Cards flip in one at a time; a late card has to join a hover that is already underway.
        public void ReapplyHoveredCard()
        {
            SetHoveredCard(hoveredCard);
        }
        public async void AttemptToPurchaseRecruit(SquadStats _squadStats, RecruitCard _recruitCard)
        {
            if(hasSelectedRecruitCard) {
                Debug.Log("Already selected a recruit card, cannot select another.");
                return;
            }

            if(!campaignSaveManager.CheckForRoomToRecruit()) 
            {
                if(!_recruitCard.CanCombine) {
                    string errorLocalized = LocalizationManager.Instance.GetText("Max Units Recruited");
                    NotificationManager.Instance.ErrorNotification(errorLocalized);
                    return;
                }
                
                var selected = CampaignManager.Instance.MapSceneUIManager.HUDPanel.SelectedCards;
                SquadToLoad[] army = campaignSaveManager.SaveData.playerArmy;

                int minPrestige = int.MaxValue;
                for (int i = 0; i < army.Length; i++)
                {
                    if (army[i].UnitIndex == -1 || army[i].UnitName != _squadStats.unitName) continue;
                    if (army[i].UnitPrestige < minPrestige) minPrestige = army[i].UnitPrestige;
                }

                if (minPrestige != 0)
                {
                    string errorLocalized = LocalizationManager.Instance.GetText("Max Units Recruited");
                    NotificationManager.Instance.ErrorNotification(errorLocalized);
                    return;
                }

                bool selectionValid = selected.Count >= 2
                    && selected[0].GetSquadToLoad().UnitName == _squadStats.unitName
                    && selected[0].GetSquadToLoad().UnitPrestige == minPrestige
                    && selected[1].GetSquadToLoad().UnitName == _squadStats.unitName
                    && selected[1].GetSquadToLoad().UnitPrestige == minPrestige;

                if (selectionValid)
                {
                    campaignSaveManager.PrestigeAndCombineWithRecruit(
                        selected[0].GetSquadToLoad().UniqueID,
                        selected[1].GetSquadToLoad().UniqueID);
                }
                else
                {
                    string uid1 = string.Empty, uid2 = string.Empty;
                    for (int i = 0; i < army.Length; i++)
                    {
                        if (army[i].UnitIndex == -1 || army[i].UnitName != _squadStats.unitName || army[i].UnitPrestige != minPrestige) continue;
                        if (uid1 == string.Empty) uid1 = army[i].UniqueID;
                        else { uid2 = army[i].UniqueID; break; }
                    }

                    if (uid1 == string.Empty || uid2 == string.Empty)
                    {
                        string errorLocalized = LocalizationManager.Instance.GetText("Max Units Recruited");
                        NotificationManager.Instance.ErrorNotification(errorLocalized);
                        return;
                    }

                    campaignSaveManager.PrestigeAndCombineWithRecruit(uid1, uid2);
                }
                mapSceneUIManager.TryDrainPendingPrestigeChoices();
            }
            else
            {
                float conscriptedHealth = 1;
                if(recruitmentType == RecruitmentType.Conscription) 
                {
                    conscriptedHealth = TabletopTavernConstants.CONSCRIPT_SURVIVORS_HEALTH_PERCENTAGE;
                    if(SaveDataHandler.IsMetaprogressionNodeUnlocked(_postBattleRecruitMetaprogressionModel)) {
                        conscriptedHealth = 0.75f;
                    }
                    if(CampaignManager.Instance.CampaignSaveManager.CheckForGear(GearID.RiverTrout)) {
                        conscriptedHealth = 1;
                    }
                }

                campaignSaveManager.RecruitSquad(_squadStats, conscriptedHealth);
            }

            IAudioRequester.Instance.PlaySFX(SFXData.RecruitUnit);
            
            _recruitCard.CompletePurchase();
            skipButton.gameObject.SetActive(false);
            hasSelectedRecruitCard = true;
            TooltipManager.Instance.HideTooltip();

            for (int i = 0; i < recruitCards.Count; i++) {
                if(recruitCards[i].SquadStats.unitName != _squadStats.unitName) {
                    recruitCards[i].DarkenCard();
                }
            }

            await Task.Delay(750);
            CloseRecruitPanel();
        }
        public async void AttemptToPurchaseGear(GearCard _gearCard)
        {
            if (hasSelectedRecruitCard)
            {
                return;
            }

            if (!CampaignManager.Instance.CampaignSaveManager.CanAquireGear())
            {
                string errorLocalized = LocalizationManager.Instance.GetText("No space for gear");
                NotificationManager.Instance.ErrorNotification(errorLocalized);
                return;
            }

            IAudioRequester.Instance.PlaySFX(SFXData.MerchantPurchase);
            campaignSaveManager.AquireGear(_gearCard.GearID);
            // chooseACardPopupPrefab.SetBool("Active", false);
            _gearCard.PlayPurchaseFeedbacks();
            hasSelectedRecruitCard = true;
            TooltipManager.Instance.HideTooltip();

            for (int i = 0; i < gearCards.Count; i++)
            {
                if (gearCards[i].GearID != _gearCard.GearID)
                {
                    gearCards[i].DarkenCard();
                }
            }

            await Task.Delay(750);
            CloseRecruitPanel();
        }
        
        public void CloseRecruitPanel()
        {
            _cardLoadCts?.Cancel();
            foreach (Transform child in recruitCardsParent) Destroy(child.gameObject);
            foreach (Transform child in gearCardsParent) Destroy(child.gameObject);
            if(recruitmentScene != null) {
                recruitmentScene.ShutDown();
            }
            StartCoroutine(CampaignManager.Instance.MapCamera.LerpFocusedOnNodeVolume(0, 0.25f));
            memoriCanvasGroup.CGDisable();
            if(recruitmentType == RecruitmentType.Shop) {
                mapSceneUIManager.ShopPanel.RenableShopPanel();
            } else if(recruitmentType == RecruitmentType.Town) {
                mapSceneUIManager.TownPanel.CloseRecruitPanel();
            } else if(recruitmentType == RecruitmentType.Battle || recruitmentType == RecruitmentType.Conscription) {
                mapSceneUIManager.EngagementPanel.ReturnFromRecruitPanel();
            } 
            recruitCards.Clear();
            gearCards.Clear();
            hoveredCard = null;
            skipButton.gameObject.SetActive(false);
            CloseFeedback();
        }
        public override void ClosePanel()
        {
            //needed for title open
        }
    }
}