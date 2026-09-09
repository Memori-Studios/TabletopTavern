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
using System.Threading.Tasks;

namespace TJ.Recruit
{
    [RequireComponent(
        typeof(Button),
        typeof(UnitAttributesUIContainer),
        typeof(UnitStatsUIContainer)
    )]//, typeof(MemoriTooltipTrigger))]
    public class RecruitCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconHighlight, tierGradient, tierGradientBack1, tierGradientBack2;
        [SerializeField] private ParticleSystem _tierParticleSystem1, _tierParticleSystem2, _tierParticleSystem3, _tierParticleSystem4;
        [SerializeField] private TMP_Text recruitNameText, goldCostText, unitCountText, maxHealthText;
        [SerializeField] private MemoriTooltipTrigger unitCountTooltip, maxHealthTooltip;
        [SerializeField] private GameObject purceasedGO, cardBackGO, canCombineGO;//costGO
        [SerializeField] private Transform cardParentTransform;
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

        public void SetUp(SquadStats _squadData, RecruitPanel _recruitPanel, int _index, RenderTexture _recruitImage, bool _isPurchased)
        {
            squadStats = _squadData;
            recruitPanel = _recruitPanel;
            index = _index;
            iconHighlight.enabled = false;
            canvas = GetComponent<Canvas>();
            graphicRaycaster = GetComponent<GraphicRaycaster>();
            graphicRaycaster.enabled = true;

            SetUpTierVisuals();
            goldCostText.text = cost.ToString();
            goldCostText.color = CampaignManager.Instance.GoldManager.CheckIfCanAfford(cost) ? Color.white : Color.red;

            recruitNameText.text = LocalizationManager.Instance.GetText(squadStats.unitName.ToString());
            unitCountText.text = squadStats.baseUnitCount.ToString();
            unitCountTooltip.SetUpToolTip(_description: LocalizationManager.Instance.GetText("Unit Count"));
            maxHealthText.text = (squadStats.baseUnitCount * squadStats.HitPointsPerUnit).ToString();
            maxHealthTooltip.SetUpToolTip(_description: LocalizationManager.Instance.GetText("HitPoints"));
            recruitImageRaw.texture = _recruitImage;

            unitAttributesUIContainer = GetComponent<UnitAttributesUIContainer>();
            unitAttributesUIContainer.Load(squadStats.unitName);

            unitStatsUIContainer = GetComponent<UnitStatsUIContainer>();
            unitStatsUIContainer.Load(squadStats.unitName, true, 0);

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
            

            GetComponent<Image>().raycastTarget = false;

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
            purchaseMMF.PlayFeedbacks();
            graphicRaycaster.enabled = false;
            OnPointerExit(null);
            GetComponent<Button>().onClick.RemoveAllListeners();
            TooltipManager.Instance.HideTooltip();

            TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[2] { TutorialData.ReorderUnits, TutorialData.DisbandUnit });
        }
        Task bloomTask;
        public void OnPointerEnter(PointerEventData eventData)
        {
            if(!canInteract) return;

            iconHighlight.enabled = true;
            bloomTask = MemoriUI.BloomItemScale(transform, 1.25f, 0.15f);
            IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
            canvas.sortingOrder = 2;
        }
        public async void OnPointerExit(PointerEventData eventData)
        {
            iconHighlight.enabled = false;
            if (bloomTask != null && !bloomTask.IsCompleted)
            {
                await bloomTask; // Wait for the bloom task to complete if it's still running
            }
            if (this == null) return; // Check if the object has been destroyed
            MemoriUI.BloomItemScale(transform, 1f, 0.1f);
            canvas.sortingOrder = 1;
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
        }
        public void DarkenCard()
        {
            OnPointerExit(null);
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
                iconHighlight.enabled = false;
                MemoriUI.BloomItemScale(transform, 1f, 0.1f);
                canvas.sortingOrder = 1;
            }
        }
}
}
