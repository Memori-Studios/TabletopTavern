using System.Threading.Tasks;
using Memori.Audio;
using Memori.Notifications;
using Memori.UI;
using Memori.Utilities;
using TJ.Map;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Memori.Scenes;
using Memori.Localization;
using Memori.SaveData;
using Memori.Steamworks;

namespace TJ.Games
{
    public class GamesPanel : MapPanel
    {
        [Header("Main Items")]
        [SerializeField] private MemoriCanvasGroup diceTablePanel;
        [SerializeField] private GamesPanelView view;
        [SerializeField] private Dice _playerDice;
        [SerializeField] private Dice _houseDice;

        // [Header("Camera")]
        // [SerializeField] private float _parallaxStrength = 0.025f;
        // [SerializeField] private float _parallaxLerpSpeed = 3f;
        [SerializeField] private Light _spotlight;
        // Dice Holder local height on a short canvas (UI Scale 125%), so the dice clear the strip above the army bar.
        [SerializeField] private float _raisedDiceHolderY = 0.5f;
        private Transform _diceHolder;
        private float _authoredDiceHolderY;
        private bool _diceRaised;
        private TavernThemeHideMe objectToHide;
        private TavernCheer[] _tavernCheers;

        private CampaignSaveManager campaignSaveManager;
        private MapSceneUIManager mapSceneUIManager;
        private MemoriCanvasGroup panelCanvasGroup;

        const int SmallBetBase = 5;
        const int MediumBetBase = 10;
        const int LargeBetBase = 20;
        const int BuyARoundCost = 5;
        const float BuyARoundHealAmount = 0.2f;
        const int HigherLowerWinPauseMs = 900;
        const int ColumnCount = 3;

        // Stored in NodeResume.tableGame and NodeResume.phase; saves hold these numbers, so never renumber them.
        const int TableDice = 1, TableHigherLower = 2;
        const int DicePhaseRolling = 0, DicePhaseResolved = 1;
        const int PhaseAwaitingCall = 0, PhaseCalled = 1, PhaseBusted = 2, PhaseCashedOut = 3, PhaseCleared = 4;

        private int smallBet;
        private int mediumBet;
        private int largeBet;

        private enum TableGame { None, Dice, HigherLower, Round }
        private TableGame activeGame;

        private bool diceRolled = false;
        private int lastBet;
        private int lastGoldChange;

        private int higherLowerStake;
        private int higherLowerWins;
        private int houseFace;
        private bool lastCallHigher;
        private bool awaitingCall;
        private bool higherLowerBusted;

        public bool CanRewind => activeGame switch
        {
            TableGame.Dice        => diceRolled,
            TableGame.HigherLower => higherLowerBusted,
            _                     => false,
        };

        private void Awake()
        {
            panelCanvasGroup = GetComponent<MemoriCanvasGroup>();
            if (_spotlight != null) _spotlight.enabled = false;
            _diceHolder = _playerDice.transform.parent;
            _authoredDiceHolderY = _diceHolder.localPosition.y;
        }

        private void LateUpdate()
        {
            bool raise = view.ShortCanvas;
            if (raise == _diceRaised) return;
            _diceRaised = raise;
            Vector3 position = _diceHolder.localPosition;
            position.y = raise ? _raisedDiceHolderY : _authoredDiceHolderY;
            _diceHolder.localPosition = position;
        }

        private void Start()
        {
            CampaignManager.Instance.GoldManager.OnGoldAmountChanged += UpdateAffordability;
        }

        public void SetUp(CampaignSaveManager _csm, MapSceneUIManager _msui)
        {
            campaignSaveManager = _csm;
            mapSceneUIManager = _msui;

            Bind(view.ContinueButton, () => mapSceneUIManager.CompleteLayerAction());
            Bind(view.SkipButton, OnSkip);
            Bind(view.StakeButtons[0].Button, () => OnDiceTableBet(smallBet));
            Bind(view.StakeButtons[1].Button, () => OnDiceTableBet(mediumBet));
            Bind(view.StakeButtons[2].Button, () => OnDiceTableBet(largeBet));
            Bind(view.PlayButton, OnHigherLowerStart);
            Bind(view.BuyButton, OnBuyARound);
            Bind(view.HigherButton.Button, () => OnCall(true));
            Bind(view.LowerButton.Button, () => OnCall(false));
            Bind(view.CashOutButton, OnCashOut);
        }

        // SetUp can run twice, so each button drops its old listener first.
        private static void Bind(Button button, UnityAction action)
        {
            button.ClearClickListeners();
            button.onClick.AddListener(action);
        }

        public async void LoadGamesPanel()
        {
            objectToHide = FindFirstObjectByType<TavernThemeHideMe>();
            if (objectToHide != null) objectToHide.gameObject.SetActive(false);
            // Gathered after the hide so the switched-off NPC is not asked to cheer.
            _tavernCheers = FindObjectsByType<TavernCheer>(FindObjectsSortMode.None);

            _playerDice.ResetScale();
            _houseDice.ResetScale();

            activeGame = TableGame.None;
            diceRolled = false;
            awaitingCall = false;
            higherLowerBusted = false;
            LoadMenu();
            view.ShowChoosing(false);

            NodeResume resume = campaignSaveManager.SaveData.nodeResume;
            bool resuming = resume.active && resume.nodeType == NodeType.Games;

            if (_spotlight != null) _spotlight.enabled = true;
            await CampaignManager.Instance.MapCamera.EnterGamesScene();
            if (this == null) return;
            SetGamesPanelVisible(true);
            GamePlayerLoader.Instance.MoveToGames();
            panelCanvasGroup.CGEnable();
            StartCoroutine(UIJuice.Open(panelCanvasGroup.GetComponent<CanvasGroup>(), view.transform as RectTransform));
            OpenFeedback.PlayFeedbacks();

            if (resuming)
            {
                ResumeTable(resume);
                return;
            }

            UpdateAffordability(0); // argument unused; buttons read gold live
            view.AttachDenyFeedback();
            await Task.Delay(400);
            if (this == null) return;

            for (int i = 0; i < ColumnCount; i++)
            {
                view.RevealColumn(i);
                IAudioRequester.Instance.PlaySFX(SFXData.EventOptionLoad);
                await Task.Delay(100);
                if (this == null) return;
            }
            view.SetCardInteractable(true);
        }

        private void LoadMenu()
        {
            // Bets keep growing into the first endless acts, then stop; the bank is bigger by then, not bottomless.
            int bookNumber = Mathf.Clamp(campaignSaveManager.SaveData.bookNumber, 1, 5);
            smallBet  = SmallBetBase  * bookNumber;
            mediumBet = MediumBetBase * bookNumber;
            largeBet  = LargeBetBase  * bookNumber;
            higherLowerStake = TavernGameRules.HigherLowerStakeBase * bookNumber;

            view.SetStakes($"{Text("Act")} {MemoriUI.ConvertNumberToRomanNumeral(Mathf.Max(campaignSaveManager.SaveData.bookNumber, 1))}");
            view.SetDiceColumn($"{smallBet} - {GoldText(largeBet)}", Text("gamesWinDouble"), $"{TavernGameRules.DiceTableWinChancePercent()}%",
                new[] { GoldText(smallBet), GoldText(mediumBet), GoldText(largeBet) });
            int best = TavernGameRules.HigherLowerPot(higherLowerStake, TavernGameRules.HigherLowerMaxCalls);
            view.SetHigherLowerColumn(GoldText(higherLowerStake), GoldText(best),
                string.Format(Text("gamesUpTo"), TavernGameRules.HigherLowerMaxCalls), GoldText(higherLowerStake));
            view.SetRoundColumn(GoldText(BuyARoundCost), $"{HealPercent}%", string.Format(Text("gamesRoundLine1"), HealPercent), GoldText(BuyARoundCost));
        }

        private static int HealPercent => Mathf.RoundToInt(BuyARoundHealAmount * 100f);

        private void UpdateAffordability(int _goldAmount)
        {
            // Only the card's buttons cost gold; once a game runs, its own buttons are set by the game.
            if (activeGame != TableGame.None) return;
            GoldManager gold = CampaignManager.Instance.GoldManager;
            view.StakeButtons[0].SetAffordable(gold.CheckIfCanAfford(smallBet));
            view.StakeButtons[1].SetAffordable(gold.CheckIfCanAfford(mediumBet));
            view.StakeButtons[2].SetAffordable(gold.CheckIfCanAfford(largeBet));
            view.SetPlayAffordable(gold.CheckIfCanAfford(higherLowerStake));
            view.SetBuyAffordable(gold.CheckIfCanAfford(BuyARoundCost));
        }

        private void OnBuyARound()
        {
            if (activeGame != TableGame.None) return;
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(BuyARoundCost))
            {
                NotificationManager.Instance.ErrorNotification(Text("NotEnoughGold"));
                return;
            }
            activeGame = TableGame.Round;
            CampaignManager.Instance.GoldManager.ModifyGold(-BuyARoundCost, Text("BuyARound"));
            TabletopTavern.Analytics.NodeLog.Try("games round", () => TabletopTavern.Analytics.NodeLog.Set("game",
                new System.Collections.Generic.Dictionary<string, object> { { "t", "Round" }, { "net", -BuyARoundCost } }));
            campaignSaveManager.ModifyTroopHealth(BuyARoundHealAmount);
            IAudioRequester.Instance.PlaySFX(SFXData.Purchase);

            CheerAll();
            view.ShowTable(GamesPanelView.Table.Round, Text("BuyARound"), true);
            view.SetStripText(Text("gamesRoundFlavor"), Text("gamesGold"), GoldChangeText(-BuyARoundCost));
            view.ShowRoundResult(string.Format(Text("RoundBoughtDesc"), HealPercent));
            CampaignManager.Instance.MapSceneUIManager.HUDPanel.ArmyStructureChanged();
        }

        #region Resume
        // Writes the table's state to the snapshot; Continue then reopens it here instead of offering the bet again.
        private void LockTable(int _tableGame, int _stake, int _playerFace, int _houseFace, int _phase, int _goldChange)
        {
            // Every table state passes through here, so the node log keeps the latest one: how the game ended.
            TabletopTavern.Analytics.NodeLog.Try("games table", () => TabletopTavern.Analytics.NodeLog.Set("game",
                new System.Collections.Generic.Dictionary<string, object>
                {
                    { "t", _tableGame == TableDice ? "Dice" : "HigherLower" },
                    { "stake", _stake },
                    { "p", _playerFace },
                    { "h", _houseFace },
                    { "wins", higherLowerWins },
                    { "phase", _phase },
                    { "net", _goldChange },
                }));
            campaignSaveManager.LockNodeResult(new NodeResume
            {
                nodeIndex  = mapSceneUIManager.LayerNodeSelected,
                nodeType   = NodeType.Games,
                tableGame  = _tableGame,
                stake      = _stake,
                playerFace = _playerFace,
                houseFace  = _houseFace,
                wins       = higherLowerWins,
                phase      = _phase,
                callHigher = lastCallHigher,
                goldChange = _goldChange,
            });
        }

        private async void ResumeTable(NodeResume _resume)
        {
            if (_resume.tableGame == TableDice)
            {
                activeGame = TableGame.Dice;
                lastBet = _resume.stake;
                lastGoldChange = _resume.goldChange;
                view.ShowTable(GamesPanelView.Table.Dice, Text("gamesDiceTable"), false);
                ShowDiceRolling(_resume.stake);
                if (_resume.phase == DicePhaseRolling) RollDice(_resume.stake, _resume.playerFace, _resume.houseFace);
                else ShowDiceResult(_resume.stake, _resume.playerFace, _resume.houseFace);
                return;
            }

            activeGame = TableGame.HigherLower;
            higherLowerStake = _resume.stake;
            higherLowerWins = _resume.wins;
            houseFace = _resume.houseFace;
            lastCallHigher = _resume.callHigher;
            ShowHigherLowerTable(false);

            switch (_resume.phase)
            {
                case PhaseAwaitingCall:
                    await ShowHouseDie();
                    if (this == null) return;
                    LoadCallButtons();
                    break;
                case PhaseCalled:
                    await ShowHouseDie();
                    if (this == null) return;
                    RollPlayerDie(_resume.playerFace);
                    break;
                case PhaseBusted:
                    await ShowBothDice(_resume.playerFace);
                    if (this == null) return;
                    ShowBust(_resume.playerFace);
                    break;
                case PhaseCashedOut:
                    await ShowHouseDie();
                    if (this == null) return;
                    ShowCashedOut(_resume.goldChange);
                    break;
                case PhaseCleared:
                    await ShowBothDice(_resume.playerFace);
                    if (this == null) return;
                    ShowCleared(_resume.playerFace, _resume.goldChange);
                    break;
            }
        }
        #endregion

        #region Dice Table
        private void OnDiceTableBet(int bet)
        {
            if (activeGame != TableGame.None) return;
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(bet))
            {
                NotificationManager.Instance.ErrorNotification(Text("NotEnoughGoldBet"));
                return;
            }

            activeGame = TableGame.Dice;
            CampaignManager.Instance.CampaignSaveManager.RegisterGoldWagered(bet);
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            view.ShowTable(GamesPanelView.Table.Dice, Text("gamesDiceTable"), true);
            ShowDiceRolling(bet);
            RollDice(bet);
        }

        private void ShowDiceRolling(int bet)
        {
            view.SetStripText(Text("gamesDiceFlavor"), Text("gamesStake"), GoldText(bet));
            view.ShowPayout(-1);
            view.HideResult();
        }

        // Preset faces replay a roll that was locked before a quit; otherwise the dice are drawn and locked before they land.
        private async void RollDice(int bet, int presetPlayer = 0, int presetHouse = 0)
        {
            lastBet = bet;
            int playerRoll = presetPlayer;
            int houseRoll  = presetHouse;
            if (playerRoll == 0)
            {
                System.Random random = campaignSaveManager.GetCampaignRandom();
                playerRoll = random.Next(1, 7);
                houseRoll  = random.Next(1, 7);

                if (campaignSaveManager.FateshineElixirArmed)
                {
                    playerRoll = 6;
                    houseRoll  = 1;
                    campaignSaveManager.ConsumeFateshineElixir();
                }
                LockTable(TableDice, bet, playerRoll, houseRoll, DicePhaseRolling, 0);
            }

            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            _playerDice.PlayLoadFeedback();
            _houseDice.PlayLoadFeedback();

            await Task.WhenAll(
                _playerDice != null ? _playerDice.AnimateToFace(playerRoll) : Task.CompletedTask,
                _houseDice  != null ? _houseDice.AnimateToFace(houseRoll)   : Task.CompletedTask
            );
            // A quit mid-roll unloads this panel; the resumed one applies the locked roll, so this one must not.
            if (this == null) return;

            // Rewind refunds what actually moved, not the rule's amount.
            int goldChange = bet * TavernGameRules.DiceTablePayout(playerRoll, houseRoll);
            int goldBefore = CampaignManager.Instance.GoldManager.CurrentGoldAmount;
            CampaignManager.Instance.GoldManager.ModifyGold(goldChange, Text("gamesDesc"));
            lastGoldChange = CampaignManager.Instance.GoldManager.CurrentGoldAmount - goldBefore;
            LockTable(TableDice, bet, playerRoll, houseRoll, DicePhaseResolved, lastGoldChange);

            PresentDiceResult(bet, playerRoll, houseRoll);
        }

        // A resumed result: the dice land on the locked faces and no gold moves, it already did.
        private async void ShowDiceResult(int bet, int playerRoll, int houseRoll)
        {
            _playerDice.PlayLoadFeedback();
            _houseDice.PlayLoadFeedback();
            await Task.WhenAll(_playerDice.AnimateToFace(playerRoll), _houseDice.AnimateToFace(houseRoll));
            if (this == null) return;
            PresentDiceResult(bet, playerRoll, houseRoll);
        }

        private void PresentDiceResult(int bet, int playerRoll, int houseRoll)
        {
            int payout = TavernGameRules.DiceTablePayout(playerRoll, houseRoll);
            int goldChange = bet * payout;
            string resultMessage = payout switch
            {
                >= 2 => string.Format(Text("DiceTableBigWin"), playerRoll, houseRoll, goldChange) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}",
                1    => string.Format(Text("DiceTableWin"), playerRoll, houseRoll, goldChange) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}",
                0    => string.Format(Text("DiceTablePush"), playerRoll, houseRoll),
                _    => string.Format(Text("DiceTableLoss"), playerRoll, houseRoll, Mathf.Abs(goldChange)) + $" {TabletopTavernConstants.GOLD_SPRITE_STRING}",
            };

            IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);
            string resultSFX = payout switch
            {
                >= 2 => SFXData.CriticalSuccess,
                < 0  => SFXData.Failure,
                _    => SFXData.Success
            };
            IAudioRequester.Instance.PlaySFX(resultSFX);

            if (payout > 0 && _playerDice != null) _playerDice.PulseOutline();
            else if (payout < 0 && _houseDice != null) _houseDice.PulseOutline();

            if (payout > 0) PlayTableWin();
            else if (payout < 0) PlayTableLoss();

            // Payout rules in the strip's order: higher, six against one or double sixes, tie, lower.
            int paidRule = payout >= 2 ? 1 : payout == 1 ? 0 : payout == 0 ? 2 : 3;
            int side = System.Math.Sign(payout);
            view.SetStripText(resultMessage, Text("gamesGold"), GoldChangeText(lastGoldChange));
            view.ShowPayout(paidRule);
            view.ShowResult(playerRoll, houseRoll, side, -side, payout < 0 && HasUsableRewind());
            diceRolled = true;
        }
        #endregion

        #region Higher or Lower
        private void OnHigherLowerStart()
        {
            if (activeGame != TableGame.None) return;
            if (!CampaignManager.Instance.GoldManager.CheckIfCanAfford(higherLowerStake))
            {
                NotificationManager.Instance.ErrorNotification(Text("NotEnoughGoldBet"));
                return;
            }

            activeGame = TableGame.HigherLower;
            higherLowerWins = 0;
            higherLowerBusted = false;
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);

            CampaignManager.Instance.GoldManager.ModifyGold(-higherLowerStake, Text("gamesDesc"));
            campaignSaveManager.RegisterGoldWagered(higherLowerStake);

            ShowHigherLowerTable(true);
            RollHouseDie();
        }

        private void ShowHigherLowerTable(bool animate)
        {
            view.ShowTable(GamesPanelView.Table.HigherLower, Text("HigherOrLower"), animate);
            var pots = new string[TavernGameRules.HigherLowerMaxCalls + 1];
            for (int i = 0; i < pots.Length; i++) pots[i] = GoldText(TavernGameRules.HigherLowerPot(higherLowerStake, i));
            view.SetLadderValues(pots);
            view.ShowLadder(higherLowerWins, false);
            view.SetStripText("", Text("gamesPot"), GoldText(TavernGameRules.HigherLowerPot(higherLowerStake, higherLowerWins)));
        }

        // Each die moves the roll counter, so no two dice share a seed and the locked snapshot keeps the count.
        private int DrawFace()
        {
            campaignSaveManager.IncrementRerollCount();
            return campaignSaveManager.GetCampaignRandom().Next(1, 7);
        }

        private async void RollHouseDie()
        {
            awaitingCall = false;
            view.LockCalls();

            houseFace = DrawFace();
            LockTable(TableHigherLower, higherLowerStake, 0, houseFace, PhaseAwaitingCall, 0);
            await ShowHouseDie();
            if (this == null) return;
            LoadCallButtons();
        }

        private async Task ShowHouseDie()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            _houseDice.PlayLoadFeedback();
            await _houseDice.AnimateToFace(houseFace);
            IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);
        }

        private async Task ShowBothDice(int _playerFace)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            _houseDice.PlayLoadFeedback();
            _playerDice.PlayLoadFeedback();
            await Task.WhenAll(_houseDice.AnimateToFace(houseFace), _playerDice.AnimateToFace(_playerFace));
            IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);
        }

        private void LoadCallButtons()
        {
            int pot = TavernGameRules.HigherLowerPot(higherLowerStake, higherLowerWins);
            view.SetStripText(string.Format(Text("gamesHouseRolled"), houseFace), Text("gamesPot"), GoldText(pot));
            view.ShowLadder(higherLowerWins, false);
            string cashOut = higherLowerWins > 0
                ? string.Format(Text("CashOutDesc"), GoldText(pot))
                : Text("CashOutLocked");
            // A call with no winning face is greyed out; it would also leave Fateshine nothing to land on.
            view.ShowCalls(CallChanceText(true), TavernGameRules.WinChancePercent(true, houseFace), WinningFaces(true),
                CallChanceText(false), TavernGameRules.WinChancePercent(false, houseFace), WinningFaces(false),
                cashOut, higherLowerWins > 0);
            awaitingCall = true;
        }

        private string CallChanceText(bool callHigher) =>
            string.Format(Text("HigherLowerChance"), TavernGameRules.WinChancePercent(callHigher, houseFace));

        private bool[] WinningFaces(bool callHigher)
        {
            var faces = new bool[6];
            for (int face = 1; face <= 6; face++) faces[face - 1] = TavernGameRules.CallWins(callHigher, houseFace, face);
            return faces;
        }

        private void OnCall(bool callHigher)
        {
            if (!awaitingCall || TavernGameRules.WinningFaces(callHigher, houseFace) == 0) return;
            awaitingCall = false;
            lastCallHigher = callHigher;
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            RollPlayerDie(0);
        }

        // A preset face replays a call that was locked before a quit.
        private async void RollPlayerDie(int presetFace)
        {
            view.LockCalls();

            int playerFace = presetFace;
            if (playerFace == 0)
            {
                playerFace = DrawFace();
                if (campaignSaveManager.FateshineElixirArmed)
                {
                    playerFace = TavernGameRules.BestFace(lastCallHigher);
                    campaignSaveManager.ConsumeFateshineElixir();
                }
                LockTable(TableHigherLower, higherLowerStake, playerFace, houseFace, PhaseCalled, 0);
            }

            IAudioRequester.Instance.PlaySFX(SFXData.ShakeDice);
            _playerDice.PlayLoadFeedback();
            await _playerDice.AnimateToFace(playerFace);
            if (this == null) return;
            IAudioRequester.Instance.PlaySFX(SFXData.DiceRoll);

            if (!TavernGameRules.CallWins(lastCallHigher, houseFace, playerFace))
            {
                higherLowerBusted = true;
                LockTable(TableHigherLower, higherLowerStake, playerFace, houseFace, PhaseBusted, 0);
                ShowBust(playerFace);
                return;
            }

            higherLowerWins++;
            int pot = TavernGameRules.HigherLowerPot(higherLowerStake, higherLowerWins);
            _playerDice.PulseOutline();

            if (higherLowerWins >= TavernGameRules.HigherLowerMaxCalls)
            {
                CampaignManager.Instance.GoldManager.ModifyGold(pot, Text("gamesDesc"));
                LockTable(TableHigherLower, higherLowerStake, playerFace, houseFace, PhaseCleared, pot);
                IAudioRequester.Instance.PlaySFX(SFXData.CriticalSuccess);
                PlayTableWin();
                CheerAll();
                ShowCleared(playerFace, pot);
                SteamAchievements.Unlock(AchievementId.BeatTheHouse);
                return;
            }

            IAudioRequester.Instance.PlaySFX(SFXData.Success);
            GamePlayerLoader.Instance.PlayPlayerAnimation("Cheer");
            view.ShowLadder(higherLowerWins, false);
            view.SetStripText(string.Format(Text("HigherLowerWin"), playerFace, GoldText(pot)), Text("gamesPot"), GoldText(pot));
            await Task.Delay(HigherLowerWinPauseMs);
            if (this == null) return;
            RollHouseDie();
        }

        private void ShowBust(int playerFace)
        {
            _houseDice.PulseOutline();
            PlayTableLoss();
            IAudioRequester.Instance.PlaySFX(SFXData.Failure);
            int lostPot = TavernGameRules.HigherLowerPot(higherLowerStake, higherLowerWins);
            view.ShowLadder(higherLowerWins, true);
            view.SetStripText(string.Format(Text("HigherLowerBust"), playerFace, houseFace), Text("gamesPot"),
                $"<color={ColorData.Negative}>{lostPot}</color> {TabletopTavernConstants.GOLD_SPRITE_STRING}");
            view.ShowResult(playerFace, houseFace, -1, 1, HasUsableRewind());
            higherLowerBusted = true;
        }

        private void ShowCleared(int playerFace, int pot)
        {
            view.ShowLadder(TavernGameRules.HigherLowerMaxCalls, false);
            view.SetStripText(string.Format(Text("HigherLowerCleared"), playerFace, GoldText(pot)), Text("gamesGold"), GoldChangeText(pot));
            view.ShowResult(playerFace, houseFace, 1, -1, false);
        }

        private void ShowCashedOut(int pot)
        {
            view.ShowLadder(higherLowerWins, false);
            view.SetStripText(string.Format(Text("HigherLowerCashOut"), GoldText(pot)), Text("gamesGold"), GoldChangeText(pot));
            view.ShowResult(0, 0, 0, 0, false);
        }

        private void OnCashOut()
        {
            if (!awaitingCall || higherLowerWins == 0) return;
            awaitingCall = false;
            view.LockCalls();

            int pot = TavernGameRules.HigherLowerPot(higherLowerStake, higherLowerWins);
            CampaignManager.Instance.GoldManager.ModifyGold(pot, Text("gamesDesc"));
            LockTable(TableHigherLower, higherLowerStake, 0, houseFace, PhaseCashedOut, pot);
            IAudioRequester.Instance.PlaySFX(SFXData.Success);
            PlayTableWin();
            ShowCashedOut(pot);
        }

        // Rewind replays the losing call against the same house die; the stake is already paid, so no gold moves.
        private void RewindHigherLowerCall()
        {
            higherLowerBusted = false;
            view.HideResult();
            view.ShowLadder(higherLowerWins, false);
            view.SetStripText("", Text("gamesPot"), GoldText(TavernGameRules.HigherLowerPot(higherLowerStake, higherLowerWins)));
            RollPlayerDie(0);
        }
        #endregion

        public void Rewind()
        {
            TabletopTavern.Analytics.NodeLog.Count("rewinds");
            if (activeGame == TableGame.HigherLower)
            {
                RewindHigherLowerCall();
                return;
            }

            diceRolled = false;
            // undo the previous outcome's gold change before rerolling
            CampaignManager.Instance.GoldManager.ModifyGold(-lastGoldChange, Text("RewindName"));
            ShowDiceRolling(lastBet);
            RollDice(lastBet);
        }

        // The hint only shows when drinking a Rewind would work: one in the bag and no Ordeal blocking it.
        private bool HasUsableRewind()
        {
            CampaignSaveData data = campaignSaveManager.SaveData;
            return data.consumables.Contains(ConsumableEnum.Rewind) && !data.IsConsumableBlocked(ConsumableEnum.Rewind);
        }

        private void PlayTableWin()
        {
            GamePlayerLoader.Instance.PlayPlayerAnimation("Cheer");
            GamePlayerLoader.Instance.PlayEnemyAnimation("Sad");
            IAudioRequester.Instance.PlaySFX(SFXData.Cheer);
        }

        private void PlayTableLoss()
        {
            GamePlayerLoader.Instance.PlayEnemyAnimation("Cheer");
            GamePlayerLoader.Instance.PlayPlayerAnimation("Sad");
            IAudioRequester.Instance.PlaySFX(SFXData.Boo);
        }

        private static string Text(string key) => LocalizationManager.Instance.GetText(key);

        private static string GoldText(int amount) => $"{amount} {TabletopTavernConstants.GOLD_SPRITE_STRING}";

        private static string GoldChangeText(int amount)
        {
            string number = amount > 0 ? $"<color={ColorData.Positive}>+{amount}</color>"
                : amount < 0 ? $"<color={ColorData.Negative}>-{-amount}</color>"
                : "0";
            return $"{number} {TabletopTavernConstants.GOLD_SPRITE_STRING}";
        }

        private async void CheerAll()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.Cheer);
            foreach (TavernCheer cheer in _tavernCheers)
            {
                if (cheer == null) continue;
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
            // A Rewind drunk on the map after leaving must not reach back into this table.
            activeGame = TableGame.None;
            diceRolled = false;
            higherLowerBusted = false;
            awaitingCall = false;

            CloseFeedback();

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
            if (activeGame != TableGame.None) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ChoiceMade);
            IAudioRequester.Instance.PlaySFX(SFXData.Boo);
            mapSceneUIManager.CompleteLayerAction();
        }
    }
}
