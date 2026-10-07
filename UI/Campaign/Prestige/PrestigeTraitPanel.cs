using TJ.Map;
using TJ;
using UnityEngine;
using Memori.Utilities;
using Memori.Audio;
using Memori.Localization;
using Memori.UI;
using System.Threading.Tasks;
using System.Collections.Generic;
using Memori.SaveData;
using System;
using MoreMountains.Feedbacks;
using TJ.Recruit;
using Memori.Tooltip;

namespace TJ.Prestige
{
    public class PrestigeTraitPanel : MapPanel
    {
        // The squad opens one level down, so the pick shows the step up to Prestige III as a reward.
        private const int SHOWN_PRESTIGE_BEFORE_PICK = 1;
        private const int MAX_PRESTIGE = 2;

        [SerializeField] private ChoicePanelView view;
        [SerializeField] private float pickedHoldSeconds = 0.6f;
        [SerializeField] private float fadeOutSeconds = 0.25f;
        [Tooltip("How long the old cards take to pop away before a Fateshine reroll deals the new ones.")]
        [SerializeField] private float rerollClearSeconds = 0.3f;

        [Header("Unit Prefab")]
        [SerializeField] private Transform prefabHolder;
        [SerializeField] private MMF_Player dropInAnimation;
        [SerializeField] private Camera troopCamera;
        [SerializeField] private GameObject troopLights;

        MemoriCanvasGroup memoriCanvasGroup;
        CampaignSaveManager campaignSaveManager;
        MapSceneUIManager mapSceneUIManager;

        SquadToLoad currentSquad;
        Action onResolved;
        RecruitCard recruitCard;
        bool picked;
        bool rerolling;
        List<UnitAttribute> shownTraits = new();

        Transform prefabObject;
        RaceBasePrefab baseObject;
        string loadedRecruitmentPrefabKey;
        System.Threading.CancellationTokenSource hoverCts;

        private void Awake()
        {
            memoriCanvasGroup = GetComponent<MemoriCanvasGroup>();
            memoriCanvasGroup.CGDisable();
            view.RerollClicked += Reroll;
        }
        private void OnDestroy()
        {
            if (view != null) view.RerollClicked -= Reroll;
        }
        public void SetUp(CampaignSaveManager _campaignSaveManager, MapSceneUIManager _mapSceneUIManager)
        {
            campaignSaveManager = _campaignSaveManager;
            mapSceneUIManager = _mapSceneUIManager;
        }
        public void LoadPrestigeTraitPanel(SquadToLoad squad, List<UnitAttribute> offer, Action _onResolved)
        {
            currentSquad = squad;
            onResolved = _onResolved;
            picked = false;
            rerolling = false;

            view.Clear();
            string title = string.Format(LocalizationManager.Instance.GetText("prestigePickTitle"), MemoriUI.ConvertNumberToRomanNumeral(MAX_PRESTIGE + 1));
            view.SetTitle(title, LocalizationManager.Instance.GetText("PrestigeDes"));

            recruitCard = view.ShowRecruitCard();
            recruitCard.SetUpDisplay(squad, troopCamera.targetTexture, SHOWN_PRESTIGE_BEFORE_PICK, UnitAttribute.None);
            LoadUnitPrefabAsync(squad.UnitName);

            AddTraitCards(offer);

            memoriCanvasGroup.CGEnable();
            ShowReroll(true);
            if (OpenFeedback != null) OpenFeedback.PlayFeedbacks();
            view.PlayOpen();
            IAudioRequester.Instance.PlaySFX(SFXData.OpenUI);
            view.FocusFirstCard();
        }
        private void AddTraitCards(List<UnitAttribute> offer)
        {
            shownTraits = new List<UnitAttribute>(offer);
            string footer = LocalizationManager.Instance.GetText("choiceClickToLearn");
            string done = LocalizationManager.Instance.GetText("choiceLearned");
            foreach (UnitAttribute trait in offer)
            {
                ChoiceCardView card = view.AddCard();
                card.Load(PrestigeTraitIcons.Get(trait), LocalizationManager.Instance.GetText(trait.ToString()), null, Color.clear,
                    LocalizationManager.Instance.GetText(trait.ToString() + "Desc"), footer, done);
                UnitAttribute chosenTrait = trait;
                card.Chosen += chosen => SelectTrait(chosenTrait, chosen);
            }
            view.WireNavigation();
        }

        #region Fateshine reroll
        // Hidden when the pool cannot change; locked with a reason when an Ordeal blocks it; struck out and greyed with no elixir.
        private void ShowReroll(bool pulse)
        {
            LocalizationManager text = LocalizationManager.Instance;
            int held = campaignSaveManager.FateshineElixirsHeld;
            OrdealId countering = OrdealRegistry.CounteringOrdeal(campaignSaveManager.SaveData.ActiveOrdeals, ConsumableEnum.FateshineElixir);
            bool usable = held > 0 && countering == OrdealId.None && !picked;
            string label = held > 0 ? string.Format(text.GetText("prestigeRerollCount"), held) : $"<s>{text.GetText("prestigeReroll")}</s>";
            string hint = usable ? text.GetText("prestigeRerollHint") : held == 0 ? text.GetText("prestigeRerollNeedsElixir") : null;
            var tooltip = new TooltipContent
            {
                Title = text.GetText("FateshineElixirName"),
                Body = text.GetText(held > 0 ? "prestigeRerollTooltip" : "prestigeRerollNeedsElixir"),
                Footer = countering != OrdealId.None ? OrdealRegistry.InactiveNote(countering) : "",
            };
            view.ShowReroll(campaignSaveManager.CanRerollPrestigeTraits(currentSquad), usable, held == 0, label, hint, tooltip, pulse && usable);
        }

        private async void Reroll()
        {
            if (picked || rerolling) return;
            List<UnitAttribute> offer = campaignSaveManager.RerollPrestigeTraitOffer(currentSquad, shownTraits);
            if (offer == null) return;
            rerolling = true;

            IAudioRequester.Instance.PlaySFX(SFXData.Drink);
            bool firstPop = true;
            foreach (ChoiceCardView card in view.Cards)
            {
                card.PopAway(firstPop);
                firstPop = false;
            }
            ShowReroll(false);

            await Task.Delay(Mathf.RoundToInt(rerollClearSeconds * 1000f));
            if (this == null) return;
            view.ClearCards();
            AddTraitCards(offer);
            view.PlayDeal();
            view.FocusFirstCard();
            rerolling = false;
        }
        #endregion
        private async void SelectTrait(UnitAttribute trait, ChoiceCardView chosen)
        {
            if (picked || rerolling) return;
            picked = true;
            ShowReroll(false);

            bool firstPop = true;
            foreach (ChoiceCardView card in view.Cards)
            {
                if (card == chosen) continue;
                card.PopAway(firstPop);
                firstPop = false;
            }
            chosen.ShowPicked();

            campaignSaveManager.ResolvePrestigeTraitChoice(currentSquad.UniqueID, trait);
            IAudioRequester.Instance.PlaySFX(SFXData.PrestigeUnit);
            if (recruitCard != null) recruitCard.ShowPrestige(MAX_PRESTIGE, trait);

            await Task.Delay(Mathf.RoundToInt(pickedHoldSeconds * 1000f));
            if (this == null) return;
            memoriCanvasGroup.FadeOutAsync(fadeOutSeconds);
            await Task.Delay(Mathf.RoundToInt(fadeOutSeconds * 1000f));
            if (this == null) return;

            Action resolved = onResolved;
            ClosePanel();
            resolved?.Invoke();
        }
        public override void ClosePanel()
        {
            memoriCanvasGroup.CGDisable();
            if (OpenFeedback != null) CloseFeedback();
            HideUnitPrefab();
            view.Clear();
            recruitCard = null;
        }

        private async void LoadUnitPrefabAsync(UnitName unitName)
        {
            hoverCts?.Cancel();
            hoverCts?.Dispose();
            hoverCts = new System.Threading.CancellationTokenSource();
            var token = hoverCts.Token;

            string key = TabletopTavernData.Instance.GetRecruitmentPrefabKey(unitName);
            GameObject prefab = await TabletopTavernData.Instance.LoadRecruitmentPrefabAsync(unitName);

            if (token.IsCancellationRequested)
            {
                AddressablesManager.Instance.Release(key);
                return;
            }

            if (loadedRecruitmentPrefabKey == key)
                loadedRecruitmentPrefabKey = null;
            LoadUnitPrefab(prefab, unitName);
            loadedRecruitmentPrefabKey = key;
        }
        private void LoadUnitPrefab(GameObject prefab, UnitName unitName)
        {
            ClearUnitPrefab();

            Race race = TabletopTavernData.Instance.GetRaceFromUnitName(unitName);
            bool bigBase = TabletopTavernData.Instance.GetUnitSizeFromUnitName(unitName) != UnitSize.Infantry;

            // Keeps the recruit prefab's own offset, as the town's recruit cards do.
            prefabObject = ModUnitPreview.Instantiate(unitName, prefab, prefabHolder).transform;
            baseObject = Instantiate(TabletopTavernData.Instance.GetRaceData(race).RaceBasePrefab, prefabHolder);
            baseObject.transform.localPosition = Vector3.zero;
            baseObject.SetUp(true, null);
            dropInAnimation.PlayFeedbacks();
            baseObject.transform.localScale = bigBase ? new Vector3(2f, 1f, 2f) : Vector3.one;

            prefabObject.SetParent(baseObject.transform);

            troopCamera.enabled = true;
            troopLights.SetActive(true);
        }
        private void ClearUnitPrefab()
        {
            if (prefabObject != null)
                Destroy(prefabObject.gameObject);
            if (baseObject != null)
                Destroy(baseObject.gameObject);
            ReleaseRecruitmentPrefab();
        }
        private void HideUnitPrefab()
        {
            hoverCts?.Cancel();
            if (prefabObject != null)
                Destroy(prefabObject.gameObject);
            if (baseObject != null)
                Destroy(baseObject.gameObject);
            ReleaseRecruitmentPrefab();
            troopCamera.enabled = false;
            troopLights.SetActive(false);
        }
        private void ReleaseRecruitmentPrefab()
        {
            if (loadedRecruitmentPrefabKey == null) return;
            AddressablesManager.Instance.Release(loadedRecruitmentPrefabKey);
            loadedRecruitmentPrefabKey = null;
        }
    }
}
