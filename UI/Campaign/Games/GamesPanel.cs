using System.Threading.Tasks;
using Memori.Audio;
using Memori.Notifications;
using Memori.Utilities;
using TMPro;
using TJ.Map;
using UnityEngine;
using UnityEngine.UI;
using Memori.Scenes;
using Memori.Localization;

namespace TJ.Games
{
    public class GamesPanel : MapPanel
    {
        [Header("Main Items")]
        [SerializeField] private MemoriCanvasGroup diceTablePanel;
        [SerializeField] private MemoriCanvasGroup buttonsPanel;
        [SerializeField] private MemoriCanvasGroup resultsPanel;
        [SerializeField] private GamesButton[] buttons;
        [SerializeField] private Dice _playerDice;
        [SerializeField] private Dice _houseDice;
        private GamesButton BetSmallButton  => buttons[0];
        private GamesButton BetMediumButton => buttons[1];
        private GamesButton BetLargeButton  => buttons[2];
        private GamesButton BuyARoundButton => buttons[3];
        private GamesButton SkipButton      => buttons[4];

        [Header("Results")]
        [SerializeField] private TMP_Text diceTableResultText;
        [SerializeField] private TMP_Text resultDescriptionText;
        [SerializeField] private Button continueButton;

        // [Header("Camera")]
        // [SerializeField] private float _parallaxStrength = 0.025f;
        // [SerializeField] private float _parallaxLerpSpeed = 3f;
        [SerializeField] private Light _spotlight;
        private TavernThemeHideMe objectToHide;
        private TavernCheer[] _tavernCheers;
        private bool _isOpen;

        private CampaignSaveManager campaignSaveManager;
        private MapSceneUIManager mapSceneUIManager;
        private MemoriCanvasGroup panelCanvasGroup;

        const int SmallBetBase = 5;
        const int MediumBetBase = 10;
        const int LargeBetBase = 20;
        const int BuyARoundCost = 5;
        const float BuyARoundHealAmount = 0.2f;

        private int smallBet;
        private int mediumBet;
        private int largeBet;

        private bool diceRolled = false;
        private int lastBet;
        private int lastGoldChange;
        public bool CanRewind => diceRolled;

        private void Awake()
        {
            panelCanvasGroup = GetComponent<MemoriCanvasGroup>();
            if (_spotlight != null) _spotlight.enabled = false;
        }

        private void Start() 
        {
            CampaignManager.Instance.GoldManager.OnGoldAmountChanged += UpdateAffordability;
        }

        public void SetUp(CampaignSaveManager _csm, MapSceneUIManager _msui)
        {
            campaignSaveManager = _csm;
            mapSceneUIManager = _msui;

            int bookNumber = Mathf.Clamp(campaignSaveManager.SaveData.bookNumber, 1, 3);
            smallBet  = SmallBetBase  * bookNumber;
            mediumBet = MediumBetBase * bookNumber;
            largeBet  = LargeBetBase  * bookNumber;

            string wagerLocalized = LocalizationManager.Instance.GetText("Wager");
            BetSmallButton.LoadGamesButton(LocalizationManager.Instance.GetText("BetSmall"), $"{wagerLocalized} {smallBet}  {TabletopTavernConstants.GOLD_SPRITE_STRING}", () => OnDiceTableBet(smallBet));
            BetMediumButton.LoadGamesButton(LocalizationManager.Instance.GetText("BetMedium"), $"{wagerLocalized} {mediumBet}  {TabletopTavernConstants.GOLD_SPRITE_STRING}", () => OnDiceTableBet(mediumBet));
            BetLargeButton.LoadGamesButton(LocalizationManager.Instance.GetText("BetLarge"), $"{wagerLocalized} {largeBet}  {TabletopTavernConstants.GOLD_SPRITE_STRING}", () => OnDiceTableBet(largeBet));
            BuyARoundButton.LoadGamesButton(LocalizationManager.Instance.GetText("BuyARound") + $" -{BuyARoundCost}  {TabletopTavernConstants.GOLD_SPRITE_STRING}", LocalizationManager.Instance.GetText("BuyARoundDesc"), () => OnBuyARound());
            SkipButton.LoadGamesButton(LocalizationManager.Instance.GetText("Skip"), LocalizationManager.Instance.GetText("SkipTableDesc"), () => OnSkip());

            continueButton.onClick.AddListener(() => mapSceneUIManager.CompleteLayerAction());
        }

        public async void LoadGamesPanel()
        {
            _tavernCheers = FindObjectsByType<TavernCheer>(FindObjectsSortMode.None);
            objectToHide = FindFirstObjectByType<TavernThemeHideMe>();
            if (objectToHide != null) objectToHide.gameObject.SetActive(false);

            _playerDice.ResetScale();
            _houseDice.ResetScale();

            foreach (GamesButton button in buttons)
                button.DeactivateButton();

            _isOpen = true;
            diceRolled = false;
            if (_spotlight != null) _spotlight.enabled = true;
            await CampaignManager.Instance.MapCamera.EnterGamesScene();
            diceTableResultText.text = "";
            buttonsPanel.CGEnable();
            resultsPanel.CGDisable();
            SetGamesPanelVisible(true);
            GamePlayerLoader.Instance.MoveToGames();
            panelCanvasGroup.CGEnable();
            OpenFeedback.PlayFeedbacks();

            await Task.Delay(400);

            foreach (GamesButton button in buttons)
            {
                button.ActivateButton();
                IAudioRequester.Instance.PlaySFX(SFXData.EventOptionLoad);
                await Task.Delay(100);
            }
        }
        private void UpdateAffordability(int _goldAmount)
        {
            BetSmallButton.SetButtonAffordability(CampaignManager.Instance.GoldManager.CheckIfCanAfford(smallBet));
            BetMediumButton.SetButtonAffordability(CampaignManager.Instance.GoldManager.CheckIfCanAfford(mediumBet));
            BetLargeButton.SetButtonAffordability(CampaignManager.Instance.GoldManager.CheckIfCanAfford(largeBet));
            BuyARoundButton.SetButtonAffordability(CampaignManager.Instance.GoldManager.CheckIfCanAfford(BuyARoundCost));
            SkipButton.SetButtonAffordability(true);
        }

        private void OnBuyARound()
        {
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(BuyARoundCost))
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("NotEnoughGold"));
                return;
            }
            string localizedString = LocalizationManager.Instance.GetText("BuyARound");
            CampaignManager.Instance.GoldManager.ModifyGold(-BuyARoundCost, localizedString);
            campaignSaveManager.ModifyTroopHealth(BuyARoundHealAmount);
            IAudioRequester.Instance.PlaySFX(SFXData.Purchase);

            CheerAll();
            buttonsPanel.FadeOutAsync();
            resultsPanel.FadeInAsync();
            ShowResult(
                string.Format(LocalizationManager.Instance.GetText("RoundBoughtDesc"), (int)(BuyARoundHealAmount * 100)));
        }

        private void OnDiceTableBet(int bet)
        {
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(bet))
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("NotEnoughGoldBet"));
                return;
            }

            lastBet = bet;
            CampaignManager.Instance.CampaignSaveManager.RegisterGoldWagered(bet);
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            buttonsPanel.FadeOutAsync();
            RollDice(bet);
        }

        private async void RollDice(int bet)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            _playerDice.PlayLoadFeedback();
            _houseDice.PlayLoadFeedback();

            System.Random random = campaignSaveManager.GetCampaignRandom();
            int playerRoll = random.Next(1, 7);
            int houseRoll  = random.Next(1, 7);
            int margin = playerRoll - houseRoll;

            // reroll once in the player's favor if they lose
            if (margin < 0)
            {
                int rerollPlayer = random.Next(1, 7);
                int rerollHouse  = random.Next(1, 7);
                if (rerollPlayer - rerollHouse > margin)
                {
                    playerRoll = rerollPlayer;
                    houseRoll  = rerollHouse;
                    margin     = playerRoll - houseRoll;
                }
            }

            if (campaignSaveManager.FateshineElixirArmed)
            {
                playerRoll = 6;
                houseRoll  = 1;
                margin     = 5;
                campaignSaveManager.ConsumeFateshineElixir();
            }

            string resultMessage;
            int goldChange;

            if (margin >= 5)
            {
                goldChange = bet * 2;
                resultMessage = string.Format(LocalizationManager.Instance.GetText("DiceTableBigWin"), playerRoll, houseRoll, goldChange) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}";
            }
            else if (margin > 0)
            {
                goldChange = bet;
                resultMessage = string.Format(LocalizationManager.Instance.GetText("DiceTableWin"), playerRoll, houseRoll, goldChange) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}";
            }
            else if (margin == 0)
            {
                goldChange = 0;
                resultMessage = string.Format(LocalizationManager.Instance.GetText("DiceTablePush"), playerRoll, houseRoll);
            }
            else if (margin <= -5)
            {
                goldChange = -(bet * 2);
                resultMessage = string.Format(LocalizationManager.Instance.GetText("DiceTableBigLoss"), playerRoll, houseRoll, Mathf.Abs(goldChange)) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}";
            }
            else
            {
                goldChange = -bet;
                resultMessage = string.Format(LocalizationManager.Instance.GetText("DiceTableLoss"), playerRoll, houseRoll, Mathf.Abs(goldChange)) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}";
            }

            await Task.WhenAll(
                _playerDice != null ? _playerDice.AnimateToFace(playerRoll) : Task.CompletedTask,
                _houseDice  != null ? _houseDice.AnimateToFace(houseRoll)   : Task.CompletedTask
            );

            lastGoldChange = goldChange;
            string gamesDescriptionLocalized = LocalizationManager.Instance.GetText("gamesDesc");
            if (goldChange > 0) CampaignManager.Instance.GoldManager.ModifyGold(goldChange, gamesDescriptionLocalized);
            else if (goldChange < 0) CampaignManager.Instance.GoldManager.ModifyGold(-Mathf.Abs(goldChange), gamesDescriptionLocalized);

            IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);

            string resultSFX = margin switch
            {
                >= 5  => SFXData.CriticalSuccess,
                <= -5 => SFXData.CriticalFailure,
                > 0   => SFXData.Success,
                < 0   => SFXData.Failure,
                _     => SFXData.Success
            };
            IAudioRequester.Instance.PlaySFX(resultSFX);

            if (margin > 0 && _playerDice != null) _playerDice.PulseOutline();
            else if (margin < 0 && _houseDice != null) _houseDice.PulseOutline();

            if (margin > 0)
            {
                GamePlayerLoader.Instance.PlayPlayerAnimation("Cheer");
                GamePlayerLoader.Instance.PlayEnemyAnimation("Sad");
                IAudioRequester.Instance.PlaySFX(SFXData.Cheer);
            }
            else if (margin < 0)
            {
                GamePlayerLoader.Instance.PlayEnemyAnimation("Cheer");
                GamePlayerLoader.Instance.PlayPlayerAnimation("Sad");
                IAudioRequester.Instance.PlaySFX(SFXData.Boo);
            }

            diceTableResultText.text = resultMessage;
            resultsPanel.FadeInAsync();
            diceRolled = true;
        }

        public void Rewind()
        {
            diceRolled = false;
            // undo the previous outcome's gold change before rerolling
            string rewindLocalized = LocalizationManager.Instance.GetText("RewindName");
            CampaignManager.Instance.GoldManager.ModifyGold(-lastGoldChange, rewindLocalized);
            resultsPanel.FadeOutAsync();
            RollDice(lastBet);
        }

        private void ShowResult(string description)
        {
            resultDescriptionText.text = description;
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.ArmyStructureChanged();
        }

        private async void CheerAll()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.Cheer);
            foreach (TavernCheer cheer in _tavernCheers)
            {
                cheer.Cheer();
                await Task.Delay(100);
            }
        }

        private void SetGamesPanelVisible(bool visible)
        {
            if (visible) diceTablePanel.CGEnable();
            else diceTablePanel.FadeOutAsync();
        }

        // private void Update()
        // {
        //     if (_isOpen && CampaignManager.Instance.MapCamera.GamesCamera != null)
        //     {
        //         Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        //         Vector2 mouseOffset = Vector2.ClampMagnitude(((Vector2)Input.mousePosition - screenCenter) / screenCenter, 1f);
        //         Vector3 targetLocalPos = new Vector3(mouseOffset.x, mouseOffset.y, 0f) * _parallaxStrength;
        //         CampaignManager.Instance.MapCamera.GamesCamera.transform.localPosition = Vector3.Lerp(
        //             CampaignManager.Instance.MapCamera.GamesCamera.transform.localPosition, targetLocalPos, Time.deltaTime * _parallaxLerpSpeed);
        //     }
        // }

        public override async void ClosePanel()
        {
            CloseFeedback();
            _isOpen = false;

            SceneHandler.Instance.TranstionCameras(
                CampaignManager.Instance.MapCamera.GamesCamera,
                CampaignManager.Instance.MapCamera.MapCameraInstance
            );

            await Task.Delay(500);

            if (_spotlight != null) _spotlight.enabled = false;
            CampaignManager.Instance.MapCamera.GamesCamera.transform.localPosition = Vector3.zero;
            GamePlayerLoader.Instance.MoveToMap();
            panelCanvasGroup.CGDisable();

            if (objectToHide != null) objectToHide.gameObject.SetActive(true);
        }
        private void OnDestroy() {
            if(CampaignManager.HasInstance && CampaignManager.Instance.GoldManager != null)
                CampaignManager.Instance.GoldManager.OnGoldAmountChanged -= UpdateAffordability;
        }
        private void OnSkip() 
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            IAudioRequester.Instance.PlaySFX(SFXData.Boo);
            mapSceneUIManager.CompleteLayerAction();
        }
    }
}
