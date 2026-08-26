using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Memori.SaveData;
using Memori.Tooltip;
using TJ.Map;
using Memori.Audio;
using MoreMountains.Feedbacks;
using Memori.Localization;

namespace TJ
{
    [RequireComponent(typeof(GraphicRaycaster))]
    public class SquadDisplayCardMenu : SquadDisplayCard, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Projected Health Loss")]
        [SerializeField] private GameObject autoResolveProjectionObject;
        [SerializeField] private Slider redDifferenceInHealthSlider, greenFinalHealthSlider;

        [Header("Units Slain")]
        [SerializeField] private GameObject unitsSlainGroup;
        [SerializeField] private TMP_Text unitsSlainText;

        [Header("Units Lost")]
        [SerializeField] private GameObject unitsLostGroup;
        [SerializeField] private TMP_Text unitsLostText;

        [Header("Squad Options")]
        [SerializeField] private GameObject squadOptionsGroup;
        [SerializeField] private Button disbandButton, renameButton, mergeButton;
        [SerializeField] private PrestigeUnitButton prestigeUnitButton;
        [SerializeField] private GameObject prestigeGlow;
        [SerializeField] private MemoriTooltipTrigger renameUnitTooltipTrigger;
        [SerializeField] private MemoriTooltipTrigger disbandUnitTooltipTrigger;
        [SerializeField] private MemoriTooltipTrigger mergeUnitTooltipTrigger;

        [Header("Recovery")]
        [SerializeField] private TMP_Text healthChangedPopUpText;
        [SerializeField] private Slider healthRecoverySlider;
        [SerializeField] private TMP_Text healthRecoveryJuiceText;
        [SerializeField] private MMF_Player healthRecoveryJuiceMMF;

        [Header("Dead Squad")]
        [SerializeField] private GameObject deadObject;
        [SerializeField] private GameObject willDieObject;

        [Header("Juice")]
        [SerializeField] private MMF_Player spawnInJuice;
        [SerializeField] private MMF_Player prestigeJuice;
        [SerializeField] private Sprite tier2Sprite, tier3Sprite;
        [SerializeField] private Image prestigeImage;
        [SerializeField] private Transform displayParentScaleReset;

        [Header("Reordering Squad")]
        [SerializeField] private GameObject dummySquadCard;
        [SerializeField] private GameObject originSlotMarkerPrefab;
        [SerializeField] private Canvas healthBarTextCanvas;

        bool wasJustSelected;
        HUDPanel hudPanel;
        bool cachedShowOptions, cachedShowRenamePrestige, cachedShowMerge, cachedShowPrestige;
        bool isPrestigeAvailable;
        Canvas canvas;
        bool wantsInheritedCanvasSorting;
        Coroutine inheritCanvasSortingRoutine;
        const int INHERIT_CANVAS_SORTING_ATTEMPTS = 5;
        bool isEnemy;
        public Team CardTeam => isEnemy ? Team.Enemy : Team.Player;
        bool inReserve;
        public bool InReserve => inReserve;
        LayoutElement layoutElement;
        GraphicRaycaster graphicRaycaster;
        bool isSpawning;

        Transform ogParent;
        int ogSiblingIndex;
        SquadDisplayCardMenu hoveredCard;
        bool previewIsShift;
        Transform hoveredCardOgParent;
        int hoveredCardOgSiblingIndex;
        GameObject vacatedSlotPlaceholder;
        GameObject shiftPreviewPlaceholder;
        bool lastValidRegionIsDeployed;
        GameObject originSlotMarker;

        public void SetUp(SquadToLoad _squad, bool _inReserve, HUDPanel _hudPanel = null, bool _isEnemy = false)
        {
            isEnemy = _isEnemy;
            base.SetUp(_squad, _squad.UnitIndex);
            squad = _squad;
            hudPanel = _hudPanel;
            inReserve = _inReserve;
            unitType = TabletopTavernData.Instance.GetUnitTypeFromUnitName(squad.UnitName);
            _presenter.UnitCountText.text = TabletopTavernData.Instance.GetSquadCurrentUnitCount(squad);
            SetUpSliders();
            this.name = squad.UnitName.ToString() + " " + squad.UniqueID[..1];
            layoutElement = GetComponent<LayoutElement>();
            canvas = GetComponent<Canvas>();

            disbandButton.onClick.AddListener(() => hudPanel.AttemptDisbandSelectedSquads());
            renameButton.onClick.AddListener(RenameSquad);
            prestigeUnitButton.PrestigeButton.onClick.AddListener(PrestigeUnit);
            mergeButton.onClick.AddListener(() => hudPanel.MergeSelectedSquads());

            if (hudPanel != null && !isEnemy)
            {
                hudPanel.CheckForPrestigeAvailability(prestigeUnitButton, squad.UnitName, squad.UnitPrestige);
            }

            squadOptionsGroup.SetActive(false);
            autoResolveProjectionObject.SetActive(false);
            unitsSlainGroup.SetActive(false);
            unitsLostGroup.SetActive(false);
            willDieObject.SetActive(squad.SquadCurrentHealth == 0);
            graphicRaycaster = GetComponent<GraphicRaycaster>();

            SetUpToolTips();
        }
        private void OnEnable()
        {
            // Becoming active is the first moment a card built into a hidden column can stop being
            // a root canvas, so this is where that deferred request is honoured.
            if (wantsInheritedCanvasSorting) InheritCanvasSorting();
        }
        /// <summary>
        /// Drops this card's canvas overrides so it renders inside whatever canvas it was placed in.
        ///
        /// The prefab keeps Override Sorting on (card at 2, health bar text at 101) so the map HUD
        /// can lift a dragged card to 102 above its neighbours. That override also detaches the card
        /// from its parent chain, so anywhere the containing panel sorts above 2 the card is drawn
        /// behind that panel and the slot reads as empty - run setup's Total Play Panel is at 104.
        /// Cards there are locked and never dragged, so the override buys them nothing.
        ///
        /// Unity forces overrideSorting true while the card counts as a ROOT canvas and silently
        /// discards the write, so being parented at Instantiate is not enough on its own - an
        /// INACTIVE card is a root canvas too. Run setup builds the recruit tiers into a source
        /// column that stays inactive until its first SetFocus, which is why those cards came out
        /// stuck at order 2, behind a Total Play Panel at 104, only on a fresh panel open.
        /// So this latches the request, verifies the write, and re-applies it in OnEnable.
        /// </summary>
        public void InheritCanvasSorting()
        {
            wantsInheritedCanvasSorting = true;
            canvas = GetComponent<Canvas>();

            if (TryInheritCanvasSorting()) return;

            // An inactive card IS a root canvas, so the override cannot be released yet and no
            // coroutine can run to retry. Not an error - OnEnable picks it up when the column is
            // shown. Run setup builds the recruit tiers into a source column that is still inactive
            // until its first SetFocus, so this is the normal path there on a fresh panel open.
            if (!isActiveAndEnabled) return;

            if (inheritCanvasSortingRoutine != null) StopCoroutine(inheritCanvasSortingRoutine);
            inheritCanvasSortingRoutine = StartCoroutine(RetryInheritCanvasSorting());
        }
        /// <summary>Returns whether the override was actually released.</summary>
        private bool TryInheritCanvasSorting()
        {
            canvas.overrideSorting = false;
            healthBarTextCanvas.overrideSorting = false;
            return !canvas.overrideSorting;
        }
        private IEnumerator RetryInheritCanvasSorting()
        {
            // One frame is all the parent canvases need to register. The cap is a backstop so a card
            // that is genuinely a root canvas reports itself instead of retrying forever.
            for (int attempt = 0; attempt < INHERIT_CANVAS_SORTING_ATTEMPTS; attempt++)
            {
                yield return null;
                if (!wantsInheritedCanvasSorting) yield break;
                if (TryInheritCanvasSorting())
                {
                    inheritCanvasSortingRoutine = null;
                    yield break;
                }
            }
            inheritCanvasSortingRoutine = null;
            Debug.LogError($"[SquadDisplayCardMenu] {name} is still overriding canvas sorting after {INHERIT_CANVAS_SORTING_ATTEMPTS} frames - Unity sees no enabled parent Canvas. Parent chain: {GetHierarchyPath()}");
        }
        /// <summary>
        /// Diagnostic only. Names every ancestor and flags which of them Unity would accept as this
        /// card's parent canvas, which is the one thing the "still a root canvas" error cannot show.
        /// </summary>
        private string GetHierarchyPath()
        {
            System.Text.StringBuilder path = new();
            for (Transform t = transform.parent; t != null; t = t.parent)
            {
                path.Append(" < ").Append(t.name);
                if (!t.gameObject.activeInHierarchy) path.Append("(inactive)");
                Canvas parentCanvas = t.GetComponent<Canvas>();
                if (parentCanvas != null)
                {
                    path.Append(parentCanvas.enabled ? "[Canvas enabled]" : "[Canvas DISABLED]");
                }
            }
            return path.ToString();
        }
        public void LockCard(bool _lock)
        {
            if(_lock) {
                isLocked = true;
            } else {
                isLocked = false;
            }
        }
        private int GetHealthRecovery()
        {
            int healthRecovery = (int)(squad.SquadMaxHealth * TabletopTavernConstants.RESERVES_HEAL_AMOUNT);
            if(CampaignManager.HasInstance) healthRecovery *= CampaignManager.Instance.CampaignSaveManager.ReservesHealMultiplier;
            if(CampaignManager.HasInstance && CampaignManager.Instance.GearManager.CheckForGear(GearID.ChugJug)) healthRecovery*=2;
            return healthRecovery;
        }
        public override void SelectSquadButtonClicked()
        {
            if (wasJustSelected) {
                wasJustSelected = false;
                return;
            }

            if (isLocked) return;

            if (squad.SquadCurrentHealth == 0) {
                //check if player squad or enemy squad
                if (isEnemy) {
                    Debug.Log("Enemy squad is dead, cannot select.");
                    return;
                }
                AttemptDisbandSquad();
                return;
            }

            if (!CampaignManager.HasInstance) return;

            CampaignManager.Instance.MapSceneUIManager.HUDPanel.CloseNonSquadPopUps();

            bool modifierHeld = Input.GetKey(KeyCode.LeftControl)
                             || Input.GetKey(KeyCode.RightControl)
                             || Input.GetKey(KeyCode.LeftShift)
                             || Input.GetKey(KeyCode.RightShift);

            if (modifierHeld)
                CampaignManager.Instance.MapSceneUIManager.HUDPanel.ToggleCardInSelection(this);
            else
                CampaignManager.Instance.MapSceneUIManager.HUDPanel.SelectSingleCard(isSelected ? null : this);
        }
        public override void SelectSquad(bool _selected)
        {
            base.SelectSquad(_selected);
            canvas.sortingOrder = _selected ? 4 : 2;
        }
        public void SetOptionsVisibility(bool showOptions, bool showRenamePrestige, bool showMerge = false, bool showPrestige = false)
        {
            cachedShowOptions = showOptions;
            cachedShowRenamePrestige = showRenamePrestige;
            cachedShowMerge = showMerge;
            cachedShowPrestige = showPrestige;
            squadOptionsGroup.SetActive(showOptions && isPointerOver);
            if (!showOptions) return;
            disbandButton.gameObject.SetActive(true);
            renameButton.gameObject.SetActive(showRenamePrestige);
            prestigeUnitButton.gameObject.SetActive((showRenamePrestige && isPrestigeAvailable) || showPrestige);
            mergeButton.gameObject.SetActive(showMerge);
        }
        public override void OnPointerEnter(PointerEventData eventData)
        {
            isPointerOver = true;
            HoverHighlight(true);
            if (isLocked) return;

            if(isSpawning) return;

            base.OnPointerEnter(eventData);
            _presenter.OnHoverEnterFeedback.PlayFeedbacks();

            if (cachedShowOptions)
            {
                squadOptionsGroup.SetActive(true);
                disbandButton.gameObject.SetActive(true);
                renameButton.gameObject.SetActive(cachedShowRenamePrestige);
                prestigeUnitButton.gameObject.SetActive((cachedShowRenamePrestige && isPrestigeAvailable) || cachedShowPrestige);
                mergeButton.gameObject.SetActive(cachedShowMerge);
            }

            if (!CampaignManager.HasInstance) return;
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.HoverSquad(squad, true, this.transform);
        }
        public override void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            squadOptionsGroup.SetActive(false);
            HoverHighlight(false);
            if (isLocked) return;

            if(isSpawning) return;

            base.OnPointerExit(eventData);
            if (!CampaignManager.HasInstance) return;
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.HoverSquad(squad, false, this.transform);
        }
        
        #region healthShit
        private void SetUpSliders()
        {
            redDifferenceInHealthSlider.maxValue = squad.SquadMaxHealth;
            greenFinalHealthSlider.maxValue = squad.SquadMaxHealth;
            healthRecoverySlider.maxValue = squad.SquadMaxHealth;
        }
        public void ShowPotentialHealthLoss(SquadToLoad _squad)
        {
            _presenter.UnitCountText.text = TabletopTavernData.Instance.GetSquadCurrentUnitCount(_squad);
            redDifferenceInHealthSlider.value = squad.SquadCurrentHealth;
            greenFinalHealthSlider.value = _squad.SquadCurrentHealth;

            if(squad.SquadCurrentHealth !=0) {
                redDifferenceInHealthSlider.value = _presenter.HealthSlider.value;
                greenFinalHealthSlider.value = _squad.SquadCurrentHealth;
            }

            autoResolveProjectionObject.SetActive(true);
            willDieObject.SetActive(_squad.SquadCurrentHealth == 0);
        }
        public void ShowPotentialHealthRecovery()
        {
            int healtRecovery = squad.SquadCurrentHealth + GetHealthRecovery();
            healthRecoverySlider.value = Mathf.Min(healtRecovery, squad.SquadMaxHealth);
            int unitCount = (int)(healthRecoverySlider.value / squad.HitPointsPerUnit);
            if (healthRecoverySlider.value > 0 && unitCount == 0) unitCount = 1;
            _presenter.UnitCountText.text = unitCount.ToString();
        }
        public void HidePotentialHealthLoss()
        {
            _presenter.UnitCountText.text = TabletopTavernData.Instance.GetSquadCurrentUnitCount(squad);
            autoResolveProjectionObject.SetActive(false);
            willDieObject.SetActive(false);
        }
        public void HidePotentialHealthRecovery()
        {
            _presenter.UnitCountText.text = TabletopTavernData.Instance.GetSquadCurrentUnitCount(squad);
            healthRecoverySlider.value = 0;
        }
        public void ShowUnitsSlain(int _unitsSlain)
        {
            unitsSlainText.text = _unitsSlain.ToString();
            unitsSlainGroup.SetActive(true);
            hudPanel.CheckForPrestigeAvailability(prestigeUnitButton, squad.UnitName, squad.UnitPrestige);
        }
        public void ShowUnitsLost(int _unitsLost)
        {
            if (_unitsLost <= 0) return;
            if (isEnemy) return;

            unitsLostText.text = "-"+_unitsLost.ToString();
            unitsLostGroup.SetActive(true);
        }
        public void UpdateUnitCount(SquadToLoad _squad)
        {
            _presenter.HealthSlider.value = _squad.SquadCurrentHealth;
            int unitCountOfSquad = int.Parse(TabletopTavernData.Instance.GetSquadCurrentUnitCount(_squad));
            _presenter.UnitCountText.text = unitCountOfSquad.ToString();
            int changeInUnitCount = unitCountOfSquad - int.Parse(TabletopTavernData.Instance.GetSquadCurrentUnitCount(squad));
            // Debug.Log($"changeInUnitCount: {changeInUnitCount}");
            squad = _squad;
            if(changeInUnitCount == 0) return;

            ShowHealthRecoveryJuice(changeInUnitCount);
        }
        public void ShowHealthRecoveryJuice(int changeInUnitCount)
        {
            if(unitsLostGroup.activeSelf) return; // don't show health recovery juice if units were lost (i.e. negative change in unit count)

            // Debug.Log($"ShowHealthRecoveryJuice({changeInUnitCount})");
            if (inReserve)
            {
                healthChangedPopUpText.text = "+" + changeInUnitCount.ToString();
            }
            else
            {
                healthChangedPopUpText.text = "";
            }

            healthRecoveryJuiceText.text = (changeInUnitCount > 0 ? "+" : "" ) + changeInUnitCount.ToString();
            healthRecoveryJuiceText.color = changeInUnitCount > 0 ? Color.green : Color.red;

            healthRecoveryJuiceMMF.StopFeedbacks();
            healthRecoveryJuiceMMF.PlayFeedbacks();
        }
        #endregion
        public void ShowPrestigeJuice(int _prestigeLevel)
        {
            spawnInJuice.PlayFeedbacks();
            prestigeImage.sprite = _prestigeLevel == 1 ? tier2Sprite : tier3Sprite;
            prestigeJuice.PlayFeedbacks();
        }
        public void HideUnitsSlain()
        {
            if (unitsSlainGroup != null)
                unitsSlainGroup.SetActive(false);
        }
        public void HideUnitsLost()
        {
            if (unitsLostGroup != null)
                unitsLostGroup.SetActive(false);
        }
        public void AttemptDisbandSquad()
        {
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.DisbandUnit);
            OnPointerExit(null);
            TooltipManager.Instance.HideTooltip();

            if(PlayerPrefs.GetInt("DisbandSquadConfirmation", 0) == 1) {
                hudPanel.DisbandSquad(squad.UniqueID);
            } else {
                hudPanel.ShowDisbandSquadConfirmation(squad.UniqueID);
                IAudioRequester.Instance.PlaySFX(SFXData.DisbandSquad);
            }
        }
        public void RenameSquad()
        {
            hudPanel.GiveRenameSquadPrompt(squad.UniqueID);
        }
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isEnemy) return;

            Vector3 ogWorldPosition = transform.position;
            ogParent = transform.parent;
            ogSiblingIndex = transform.GetSiblingIndex();

            hoveredCard = null;
            previewIsShift = false;
            vacatedSlotPlaceholder = null;
            shiftPreviewPlaceholder = null;
            lastValidRegionIsDeployed = !inReserve;

            if (originSlotMarkerPrefab != null)
            {
                originSlotMarker = Instantiate(originSlotMarkerPrefab, ogParent);
                originSlotMarker.transform.position = ogWorldPosition;
                LayoutElement markerLayoutElement = originSlotMarker.GetComponent<LayoutElement>();
                if (markerLayoutElement == null) markerLayoutElement = originSlotMarker.AddComponent<LayoutElement>();
                markerLayoutElement.ignoreLayout = true; // marks the origin slot without consuming a layout slot itself
            }

            transform.SetSiblingIndex(transform.parent.transform.childCount - 1);

            layoutElement.ignoreLayout = true;
            canvas.sortingOrder = 102;
            healthBarTextCanvas.sortingOrder = 103;

            hudPanel.HighlightDeployedTroopsArea(lastValidRegionIsDeployed);
            hudPanel.HighlightReserveTroopsArea(!lastValidRegionIsDeployed);

            IAudioRequester.Instance.PlaySFX(SFXData.SquadHovered);

            hudPanel.LockCards(true);
            OnPointerEnter(null);
            hudPanel.DestroyEmptySquadCards();
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (isEnemy) return;

            transform.position = eventData.position;

            // Flicker guard: once a preview is active (swap or shift), the hovered card has physically
            // moved away from the slot the mouse is sitting over. Re-scanning every frame would find
            // nothing there, revert the preview, put the card back under the mouse, and re-trigger it
            // again next frame. As long as the pointer stays over the placeholder left behind, keep the
            // current preview instead of re-evaluating.
            if (hoveredCard != null)
            {
                GameObject stablePlaceholder = previewIsShift ? shiftPreviewPlaceholder : vacatedSlotPlaceholder;
                if (stablePlaceholder != null)
                {
                    RectTransform placeholderRect = stablePlaceholder.transform as RectTransform;
                    if (placeholderRect != null && RectTransformUtility.RectangleContainsScreenPoint(placeholderRect, eventData.position))
                        return;
                }
            }

            SquadDisplayCardMenu newHoveredCard = hudPanel.FindRealCardUnderScreenPoint(eventData.position, this);

            if (newHoveredCard != null)
            {
                if (newHoveredCard != hoveredCard)
                {
                    RevertHoverPreview();
                    // A card that started in deployed and is still hovering deployed inserts (shifting
                    // the cards in between); everything else (reserve involved on either end) swaps.
                    if (!inReserve && !newHoveredCard.InReserve)
                        BeginShiftPreview(newHoveredCard);
                    else
                        BeginSwapPreview(newHoveredCard);
                }
                return;
            }

            bool overDeployedRegion = RectTransformUtility.RectangleContainsScreenPoint(hudPanel.DeployedTroopsArea, eventData.position);
            bool overReserveRegion = RectTransformUtility.RectangleContainsScreenPoint(hudPanel.ReserveTroopsArea, eventData.position);

            if (overDeployedRegion || overReserveRegion)
            {
                bool targetIsDeployed = overDeployedRegion;
                if (hudPanel.RegionHasRoom(targetIsDeployed, this))
                {
                    RevertHoverPreview();
                    SetPreviewRegion(targetIsDeployed);
                }
            }
            // else: pointer is over neither a card nor a valid region - hold the last valid preview.
        }
        // Insert-and-shift preview: only used for deployed-to-deployed hovers. A single real (non-ignored)
        // placeholder is inserted at the hovered card's current slot - the layout group automatically
        // shifts that card (and everyone between it and A's now-vacant original slot) over by one, with
        // no manual per-card reparenting needed.
        void BeginShiftPreview(SquadDisplayCardMenu _card)
        {
            hoveredCard = _card;
            previewIsShift = true;

            shiftPreviewPlaceholder = Instantiate(dummySquadCard, hudPanel.DeployedUnitsParent);
            shiftPreviewPlaceholder.transform.SetSiblingIndex(_card.transform.GetSiblingIndex());

            SetPreviewRegion(true);
            IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
        }
        void BeginSwapPreview(SquadDisplayCardMenu _card)
        {
            hoveredCard = _card;
            previewIsShift = false;
            hoveredCardOgParent = _card.transform.parent;
            hoveredCardOgSiblingIndex = _card.transform.GetSiblingIndex();

            vacatedSlotPlaceholder = Instantiate(dummySquadCard, hoveredCardOgParent);
            vacatedSlotPlaceholder.transform.SetSiblingIndex(hoveredCardOgSiblingIndex);

            _card.transform.SetParent(ogParent);
            _card.transform.SetSiblingIndex(ogSiblingIndex);

            SetPreviewRegion(!_card.InReserve);
            IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
        }
        void RevertHoverPreview()
        {
            if (hoveredCard == null) return;

            if (previewIsShift)
            {
                if (shiftPreviewPlaceholder != null) Destroy(shiftPreviewPlaceholder);
                shiftPreviewPlaceholder = null;
            }
            else
            {
                hoveredCard.transform.SetParent(hoveredCardOgParent);
                hoveredCard.transform.SetSiblingIndex(hoveredCardOgSiblingIndex);

                if (vacatedSlotPlaceholder != null) Destroy(vacatedSlotPlaceholder);
                vacatedSlotPlaceholder = null;
            }

            hoveredCard = null;
            previewIsShift = false;
        }
        void SetPreviewRegion(bool _deployedRegion)
        {
            if (_deployedRegion != lastValidRegionIsDeployed)
            {
                hudPanel.HighlightDeployedTroopsArea(_deployedRegion);
                hudPanel.HighlightReserveTroopsArea(!_deployedRegion);
            }
            lastValidRegionIsDeployed = _deployedRegion;
        }
        public void OnEndDrag(PointerEventData eventData)
        {
            if (isEnemy) return;

            if (layoutElement != null)
                layoutElement.ignoreLayout = false;

            if (hoveredCard != null)
            {
                if (previewIsShift)
                    hudPanel.ShiftUnit(UniqueID, hoveredCard.SquadId);
                else
                    hudPanel.MoveUnit(UniqueID, hoveredCard.SquadId);
            }
            else
            {
                int targetIndex = hudPanel.GetFirstEmptySlotIndex(lastValidRegionIsDeployed, this);
                if (targetIndex >= 0)
                {
                    hudPanel.MoveUnit(UniqueID, targetIndex);
                    CampaignManager.Instance.CampaignSaveManager.ReorderUnits();
                }
            }

            OnPointerExit(null);
            hudPanel.LockCards(false);
            canvas.sortingOrder = 2;
            healthBarTextCanvas.sortingOrder = 101;
        }
        public void SpawnInJuice(bool makeInteractable)
        {
            if (spawnInJuice == null) return;

            isSpawning = true;

            // MakeInteractable(makeInteractable);
            spawnInJuice.PlayFeedbacks();
            IAudioRequester.Instance.PlaySFX(SFXData.SquadCardSpawn);
        }
        public void SpawnInComplete()
        {
            isSpawning = false;
            displayParentScaleReset.localScale = Vector3.one;
        }
        public void MakeInteractable(bool _isInteractable)
        {
            if(graphicRaycaster == null) return;
            graphicRaycaster.enabled = _isInteractable;
        }
        public void PrestigeUnit()
        {
            hudPanel.PrestigeUnit(squad.UniqueID);
            prestigeUnitButton.TooltipTrigger.OnPointerExit(null);
            TutorialManager.Instance.CompleteStepCheck(TutorialStepEnum.PrestigeUnit);
        }
        public void ShowPrestigeAvailability(bool _isAvailable)
        {
            isPrestigeAvailable = _isAvailable;
            prestigeGlow.SetActive(_isAvailable);
        }
        public void HideDeadSquads()
        {
            // Debug.Log($"squad.currentUnitCount: {squad.currentUnitCount}");
            if(squad.SquadCurrentHealth == 0) {
                deadObject.SetActive(true);
            }
        }
        private void SetUpToolTips()
        {
            string prestigeLocalised = LocalizationManager.Instance.GetText("Prestige");
            string prestigeTooltipLocalised = LocalizationManager.Instance.GetText("PrestigeTooltip");
            string unitTypeLocalised = LocalizationManager.Instance.GetText(unitType.ToString());
            string unitsGainLocalised = LocalizationManager.Instance.GetText("UnitsGain");
            string meleeAttackLocalised = LocalizationManager.Instance.GetText("MeleeAttack");
            string meleeDefenseLocalised = LocalizationManager.Instance.GetText("MeleeDefense");
            string leadershipLocalised = LocalizationManager.Instance.GetText("Leadership");
            string accuracyLocalised = LocalizationManager.Instance.GetText("Accuracy");
            string rangeLocalised = LocalizationManager.Instance.GetText("Range");
            string ammunitionLocalised = LocalizationManager.Instance.GetText("Ammunition");
            string andLocalised = LocalizationManager.Instance.GetText("and");
            string perLevelLocalised = LocalizationManager.Instance.GetText("PerLevel");
            string mergeUnitsDescriptionLocalised = LocalizationManager.Instance.GetText("MergeUnitsDescription");

            if(TabletopTavernConstants.FightsInMelee(unitType)) {
                prestigeTooltipLocalised += $"[{unitTypeLocalised}] {unitsGainLocalised} <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{meleeAttackLocalised}</color>, <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{meleeDefenseLocalised}</color> {andLocalised} <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{leadershipLocalised}</color> {perLevelLocalised}";
            } else if(unitType == UnitType.Ranged || unitType == UnitType.Artillery) {
                int ammoBonusPerLevel = unitType == UnitType.Artillery ? TabletopTavernConstants.PRESTIGE_AMMO_BONUS_ARTILLERY : TabletopTavernConstants.PRESTIGE_AMMO_BONUS_RANGED;
                prestigeTooltipLocalised += $"[{unitTypeLocalised}] {unitsGainLocalised} <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{accuracyLocalised}</color>, <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{rangeLocalised}</color> {andLocalised} <color={ColorData.Green}>+{ammoBonusPerLevel}</color> <color={ColorData.UnitStat}>{ammunitionLocalised}</color> {perLevelLocalised}";
            } else if(TabletopTavernConstants.Casts(unitType)) {
                // Range, Leadership and charges - no Accuracy. The branch above is an explicit
                // whitelist and this one is neither of the first two, so without it a mage's
                // prestige tooltip renders with no gains listed at all.
                prestigeTooltipLocalised += $"[{unitTypeLocalised}] {unitsGainLocalised} <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{rangeLocalised}</color>, <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_BONUS}</color> <color={ColorData.UnitStat}>{leadershipLocalised}</color> {andLocalised} <color={ColorData.Green}>+{TabletopTavernConstants.PRESTIGE_AMMO_BONUS_MAGE}</color> <color={ColorData.UnitStat}>{ammunitionLocalised}</color> {perLevelLocalised}";
            }

            string renameLocalised = LocalizationManager.Instance.GetText("Rename Unit");
            string disbandLocalised = LocalizationManager.Instance.GetText("Disband Unit");
            string mergeLocalised = LocalizationManager.Instance.GetText("Merge Units");

            prestigeUnitButton.TooltipTrigger.SetUpToolTip(prestigeLocalised, prestigeTooltipLocalised);
            renameUnitTooltipTrigger.SetUpToolTip(renameLocalised, "");
            disbandUnitTooltipTrigger.SetUpToolTip(disbandLocalised, "");
            mergeUnitTooltipTrigger.SetUpToolTip(mergeLocalised, mergeUnitsDescriptionLocalised);
        }
        public void UseConsumable()
        {
            wasJustSelected = true;
        }
        public SquadToLoad GetSquadToLoad()
        {
            return squad;
        }
        public override void OnDeselect(BaseEventData eventData)
        {
            // Your deselect logic
            base.OnDeselect(eventData);
            // squadOptionsGroup.SetActive(false);
        }
    }
}
