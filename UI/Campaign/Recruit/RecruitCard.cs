using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Memori.UI;
using UnityEngine.EventSystems;
using Memori.Tooltip;
using TJ.Map;
using Memori.Audio;
using Memori.Utilities;
using MoreMountains.Feedbacks;
using Memori.Localization;

namespace TJ.Recruit
{
    [RequireComponent(
        typeof(Button),
        typeof(UnitAttributesUIContainer),
        typeof(UnitStatsUIContainer)
    )]//, typeof(MemoriTooltipTrigger))]
    public class RecruitCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image iconHighlight, tierGradient, tierGradientBack1, tierGradientBack2;
        [SerializeField] private ParticleSystem _tierParticleSystem1, _tierParticleSystem2, _tierParticleSystem3, _tierParticleSystem4;
        [SerializeField] private TMP_Text recruitNameText, goldCostText, unitCountText, maxHealthText;
        [SerializeField] private MemoriTooltipTrigger unitCountTooltip, maxHealthTooltip;
        [SerializeField] private GameObject purceasedGO, cardBackGO, canCombineGO;//costGO
        [SerializeField] private Transform cardParentTransform;
        [Tooltip("Scaled, lifted and breathed on hover. Nothing else may animate its scale or position.")]
        [SerializeField] private RectTransform cardContentRect;
        [SerializeField] private Image recruitUnitTypeImage1, recruitUnitTypeImage2;
        [SerializeField] private ImageHighlighter imageHighlighter;
        [SerializeField] private RawImage recruitImageRaw;

        [Header("Combine")]
        [SerializeField] private MemoriTooltipTrigger canCombineTooltip;

        [Header("MMF")]
        [SerializeField] private MMF_Player purchaseMMF, loadInMMF;

        [Header("Unit Rarity")]
        [SerializeField] private Image unitRarityImage;
        [SerializeField] private TMP_Text unitRarityText;
        [Tooltip("Optional. Only casters show it; it hides itself for everything else.")]
        [SerializeField] private SpellInfoBlock spellInfoBlock;

        RecruitPanel recruitPanel;
        SquadStats squadStats;
        public SquadStats SquadStats => squadStats;
        private int cost;
        public int Cost => cost;
        private int index;
        public int Index => index;
        UnitStatsUIContainer unitStatsUIContainer;
        UnitAttributesUIContainer unitAttributesUIContainer;
        Canvas canvas;
        GraphicRaycaster graphicRaycaster;
        bool canInteract = false;
        bool canCombine = false;
        public bool CanCombine => canCombine;
        bool isPurchased = false;
        bool isPointerOver = false;

        #region Hover motion fields
        public enum HoverMotion { Idle, Hovered, Neighbour, Pressed }
        const float HoverScale = 1.15f, NeighbourScale = 0.92f, PressedScale = 0.9f;
        const float HoverLift = 20f;
        const float HoverInDuration = 0.1f, HoverOutDuration = 0.08f, PressDuration = 0.03f;
        const float BreathAmplitude = 4f, BreathPeriod = 1.8f, BreathStagger = 0.3f;
        // Inset of the hit box from the card edge, so a hover needs the cursor well onto the card.
        const float HitBoxInset = 20f;
        HoverMotion hoverMotion = HoverMotion.Idle;
        bool motionActive = false;
        bool motionSettled = true;
        float motionStartTime, motionDuration, startScale, targetScale, startY, targetY;
        float breathClock;
        #endregion

        public void SetUp(SquadStats _squadData, RecruitPanel _recruitPanel, int _index, RenderTexture _recruitImage, bool _isPurchased)
        {
            squadStats = _squadData;
            recruitPanel = _recruitPanel;
            index = _index;
            if (cardContentRect == null) Debug.LogError("RecruitCard: cardContentRect is not assigned", this);
            iconHighlight.enabled = false;
            canvas = GetComponent<Canvas>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            graphicRaycaster.enabled = true;

            SetUpTierVisuals();
            goldCostText.text = cost.ToString();
            goldCostText.color = CampaignManager.Instance.GoldManager.CheckIfCanAfford(cost) ? Color.white : Color.red;

            recruitNameText.text = LocalizationManager.Instance.GetText(squadStats.unitName.ToString());
            // The count this recruit will actually arrive with, hero rule included.
            int unitCount = HeroBonusManager.GetPlayerBaseUnitCount(squadStats.unitName, HeroBonusManager.Instance.ActiveHeroID);
            unitCountText.text = unitCount.ToString();
            unitCountTooltip.SetUpToolTip(_description: LocalizationManager.Instance.GetText("Unit Count"));
            maxHealthText.text = (unitCount * squadStats.HitPointsPerUnit).ToString();
            maxHealthTooltip.SetUpToolTip(_description: LocalizationManager.Instance.GetText("HitPoints"));
            recruitImageRaw.texture = _recruitImage;

            unitAttributesUIContainer = GetComponent<UnitAttributesUIContainer>();
            unitAttributesUIContainer.Load(squadStats.unitName);

            unitStatsUIContainer = GetComponent<UnitStatsUIContainer>();
            unitStatsUIContainer.Load(squadStats.unitName, true, 0);
            unitStatsUIContainer.DisableTooltips();

            // Before the refreshes below: the block sizes itself to its description, and the card's
            // ContentSizeFitter has to sum a settled height.
            if (spellInfoBlock != null && spellInfoBlock.Load(squadStats.unitName, squadStats.unitType))
                CollapseEmptyAttributesRow();

            unitStatsUIContainer.Refresh();
            unitAttributesUIContainer.Refresh();

            purceasedGO.SetActive(_isPurchased);
            // costGO.SetActive(!_isPurchased);
            if (!_isPurchased)
            {
                GetComponent<Button>().onClick.AddListener(SelectCard);
                loadInMMF.PlayFeedbacks();
            }
            else
            {
                cardBackGO.SetActive(false);
            }
            cardParentTransform.rotation = Quaternion.Euler(0, -90, 0);
            
            Sprite sprite = TabletopTavernData.Instance.GetSquadTypeIcon(squadStats.unitName);
            recruitUnitTypeImage1.sprite = sprite;
            recruitUnitTypeImage2.sprite = sprite;
            

            // The root is the hit box: static and identical on every card, while the children move and tilt.
            Image rootImage = GetComponent<Image>();
            rootImage.enabled = true;
            rootImage.raycastTarget = true;
            rootImage.raycastPadding = Vector4.one * HitBoxInset;

            // Only the root's inset hit box may start a hover; graphics that carry a tooltip keep their own raycast.
            foreach (Graphic graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.gameObject == gameObject || graphic.GetComponent<MemoriTooltipTrigger>() != null) continue;
                graphic.raycastTarget = false;
            }

            isPurchased = _isPurchased;
            RefreshCombineState();
            CampaignManager.Instance.CampaignSaveManager.OnArmyStructureChanged += RefreshCombineState;

            OnPointerExit(null); // Ensure the card is not highlighted on setup
        }
        public void AddHoverToAttributes()
        {
            unitAttributesUIContainer.EnableHoverBonuses();
        }

        /// <summary>
        /// The attribute row reserves a flat 65px via its LayoutElement's minHeight, whether or not
        /// it has any entries. On a caster that is dead space sitting directly under the spell block,
        /// and it is what pushes the card past its fixed 580px frame.
        ///
        /// Only collapsed when the row is genuinely empty, so a prestige-granted trait on a mage
        /// still gets its space. Cards are instantiated per recruit, so there is no authored value
        /// to restore afterwards.
        /// </summary>
        private void CollapseEmptyAttributesRow()
        {
            if (unitAttributesUIContainer.DisplayedAttributeCount > 0) return;

            Transform attributesParent = unitAttributesUIContainer.UnitAttributesParent;
            if (attributesParent == null) return;

            LayoutElement layoutElement = attributesParent.GetComponent<LayoutElement>();
            if (layoutElement != null) layoutElement.minHeight = 0f;
        }
        private void SetUpTierVisuals()
        {
            Color tierColor = ColorData.GetRarityTierColor(squadStats.RarityTier);
            tierGradient.color = tierColor;
            
            if(squadStats.RarityTier != UnitRarity.Common) 
            {
                var main1 = _tierParticleSystem1.main;
                main1.startColor = tierColor;
                var main2 = _tierParticleSystem2.main;
                main2.startColor = tierColor;
                var main3 = _tierParticleSystem3.main;
                main3.startColor = tierColor;
                var main4 = _tierParticleSystem4.main;
                main4.startColor = tierColor;
            }

            _tierParticleSystem1.gameObject.SetActive(false);
            _tierParticleSystem2.gameObject.SetActive(false);
            _tierParticleSystem3.gameObject.SetActive(false);
            _tierParticleSystem4.gameObject.SetActive(false);

            unitRarityImage.color = tierColor;
            unitRarityText.text = LocalizationManager.Instance.GetText(squadStats.RarityTier.ToString());
            tierGradient.color = new Color(tierGradient.color.r, tierGradient.color.g, tierGradient.color.b, 25 / 255f);
            tierGradientBack1.color = new Color(tierColor.r, tierColor.g, tierColor.b, 25 / 255f);
            tierGradientBack2.color = new Color(tierColor.r, tierColor.g, tierColor.b, 25 / 255f);

            imageHighlighter.gameObject.SetActive(squadStats.RarityTier != UnitRarity.Common);
            imageHighlighter.SetColors(
                new Color(tierGradient.color.r, tierGradient.color.g, tierGradient.color.b, 0 / 255f),
                new Color(tierGradient.color.r, tierGradient.color.g, tierGradient.color.b, 255 / 255f));
        }
        private void OnDestroy()
        {
            if (CampaignManager.HasInstance && CampaignManager.Instance.CampaignSaveManager != null)
                CampaignManager.Instance.CampaignSaveManager.OnArmyStructureChanged -= RefreshCombineState;
        }
        private void RefreshCombineState()
        {
            // The pick itself changes the army; the badges keep showing what the player saw when choosing.
            if (recruitPanel.HasSelectedRecruitCard) return;
            canCombineGO.SetActive(false);
            canCombine = false;
            if (!isPurchased && !CampaignManager.Instance.CampaignSaveManager.CheckForRoomToRecruit())
            {
                var army = CampaignManager.Instance.CampaignSaveManager.SaveData.playerArmy;
                int minPrestige = int.MaxValue;
                int matchCount = 0;
                for (int i = 0; i < army.Length; i++)
                {
                    if (army[i].UnitIndex == -1 || army[i].UnitName != squadStats.unitName) continue;
                    if (army[i].UnitPrestige < minPrestige)
                    {
                        minPrestige = army[i].UnitPrestige;
                        matchCount = 1;
                    }
                    else if (army[i].UnitPrestige == minPrestige)
                    {
                        matchCount++;
                    }
                }
                if (matchCount >= 2 && minPrestige == 0)
                {
                    canCombine = true;
                    canCombineGO.SetActive(true);
                    string combineTitle = LocalizationManager.Instance.GetText("Prestige");
                    string combineDesc = LocalizationManager.Instance.GetText("PrestigeTooltip");
                    canCombineTooltip.SetUpToolTip(combineTitle, combineDesc);
                }
            }
        }
        public void CompletePurchase()
        {
            StopHoverMotion();
            purchaseMMF.PlayFeedbacks();
            graphicRaycaster.enabled = false;
            OnPointerExit(null);
            GetComponent<Button>().onClick.RemoveAllListeners();
            TooltipManager.Instance.HideTooltip();

            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[2] { TutorialData.ReorderUnits, TutorialData.DisbandUnit });
        }
        public void OnPointerEnter(PointerEventData eventData)
        {
            // Remembered through the flip so the card can start hovered the moment it becomes interactive.
            isPointerOver = true;
            if(!canInteract) return;
            BeginHover();
        }
        void BeginHover()
        {
            iconHighlight.enabled = true;
            IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
            canvas.sortingOrder = 2;
            recruitPanel.SetHoveredCard(this);
        }
        public void OnPointerExit(PointerEventData eventData)
        {
            isPointerOver = false;
            iconHighlight.enabled = false;
            canvas.sortingOrder = 1;
            if (recruitPanel != null) recruitPanel.SetHoveredCard(null);
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (!canInteract) return;
            SetHoverMotion(HoverMotion.Pressed);
        }
        public void OnPointerUp(PointerEventData eventData)
        {
            // Leaving the card while pressed already fires OnPointerExit, so Pressed here means still over it.
            if (hoverMotion != HoverMotion.Pressed) return;
            recruitPanel.SetHoveredCard(this);
        }
        public void SelectCard()
        {
            if(!canInteract) return;
            Debug.Log($"Selected recruit card: {squadStats.unitName}");

            recruitPanel.AttemptToPurchaseRecruit(squadStats, this);
        }
        public void PlayCardDrawSFX()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.CardDraw);
        }
        public void PlayCardFlipSFX()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.CardFlip);
            if(squadStats.RarityTier != UnitRarity.Common) 
            {
                _tierParticleSystem1.gameObject.SetActive(true);
                _tierParticleSystem2.gameObject.SetActive(true);
                _tierParticleSystem3.gameObject.SetActive(true);
                _tierParticleSystem4.gameObject.SetActive(true);
            }
            canInteract = true;
            StartHoverMotion();
            if (isPointerOver) BeginHover();
            else recruitPanel.ReapplyHoveredCard();
        }
        public void DarkenCard()
        {
            canInteract = false;
            OnPointerExit(null);
            FreezeHoverMotion();
            //this is triggered on all cards that are not selected, should get every text and image and set it to it's current color but slightly darker
            Color darkenColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Image[] images = GetComponentsInChildren<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                //if color is black, skip it
                if (images[i].color == Color.black) continue;
                images[i].color = images[i].color * darkenColor;
            }
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>();
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].color = texts[i].color * darkenColor;
            }
       
            _tierParticleSystem1.gameObject.SetActive(false);
            _tierParticleSystem2.gameObject.SetActive(false);
            _tierParticleSystem3.gameObject.SetActive(false);
            _tierParticleSystem4.gameObject.SetActive(false);
        }
        //if mouse leaves screen, disable the highlight
        public void OnApplicationFocus(bool focus)
        {
            if (!focus)
            {
                OnPointerExit(null);
            }
        }

        #region Hover motion
        // All motion goes on Card Content so the root's hit box never scales, lifts or tilts.
        void StartHoverMotion()
        {
            motionActive = true;
            motionSettled = true;
            hoverMotion = HoverMotion.Idle;
            breathClock = -index * BreathStagger;
        }
        void StopHoverMotion()
        {
            if (!motionActive) return;
            motionActive = false;
            cardContentRect.localScale = Vector3.one;
            SetCardY(0f);
        }
        // Holds the card where its hover left it, so a shrunk neighbour stays shrunk after a purchase.
        void FreezeHoverMotion()
        {
            if (!motionActive) return;
            motionActive = false;
            if (motionSettled) return;
            cardContentRect.localScale = Vector3.one * targetScale;
            SetCardY(targetY);
        }
        public void SetHoverMotion(HoverMotion state)
        {
            if (!motionActive || hoverMotion == state) return;
            hoverMotion = state;
            startScale = cardContentRect.localScale.x;
            startY = cardContentRect.anchoredPosition.y;
            switch (state)
            {
                case HoverMotion.Hovered:
                    targetScale = HoverScale; targetY = HoverLift; motionDuration = HoverInDuration; break;
                case HoverMotion.Neighbour:
                    targetScale = NeighbourScale; targetY = 0f; motionDuration = HoverInDuration; break;
                case HoverMotion.Pressed:
                    targetScale = PressedScale; targetY = 0f; motionDuration = PressDuration; break;
                default:
                    // Breath restarts from the bottom, each card waiting its stagger before rising.
                    targetScale = 1f; targetY = -BreathAmplitude; motionDuration = HoverOutDuration;
                    breathClock = -index * BreathStagger; break;
            }
            motionStartTime = Time.unscaledTime;
            motionSettled = false;
        }
        void Update()
        {
            if (!motionActive) return;
            if (!motionSettled)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - motionStartTime) / motionDuration);
                float e = hoverMotion == HoverMotion.Hovered ? BackOut(p) : p;
                cardContentRect.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, targetScale, e);
                SetCardY(Mathf.LerpUnclamped(startY, targetY, e));
                if (p >= 1f) motionSettled = true;
                return;
            }
            if (hoverMotion != HoverMotion.Idle) return;
            breathClock += Time.unscaledDeltaTime;
            float y = breathClock < 0f
                ? -BreathAmplitude
                : -BreathAmplitude * Mathf.Cos(breathClock / BreathPeriod * 2f * Mathf.PI);
            SetCardY(y);
        }
        void SetCardY(float y)
        {
            Vector2 pos = cardContentRect.anchoredPosition;
            pos.y = y;
            cardContentRect.anchoredPosition = pos;
        }
        // Overshoots the target by about a tenth of the travel before settling.
        static float BackOut(float p)
        {
            const float s = 1.70158f;
            p -= 1f;
            return 1f + p * p * ((s + 1f) * p + s);
        }
        #endregion
    }
}
