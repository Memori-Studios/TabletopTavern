using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Memori.SaveData;
using TMPro;
using Memori.UI;
using Memori.Utilities;
using Memori.Scenes;
using Memori.Audio;
using Memori.Localization;
using System.Threading.Tasks;
using TabletopTavern.Analytics;
using TabletopTavern.Leaderboards;
using Memori.Notifications;
using MoreMountains.Feedbacks;
using Memori.Tooltip;

namespace TJ.Map
{
[RequireComponent(typeof(MemoriCanvasGroup))]
public class GameOverPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text demoCompletionText;
    [SerializeField] private TMP_Text heroNameText, difficultyNameText;
    [SerializeField] private GameObject[] difficultyCrests;

    [Header("Game Over Stats")]
    [SerializeField] private TMP_Text chaptersCompletedText;
    [SerializeField] private TMP_Text goldEarnedText, renownEarnedText, renownBreakdownText;
    [SerializeField] private TMP_Text enemiesSlainText;
    [SerializeField] private MemoriCanvasGroup completionMessageRow, heroNameRow, difficultyRow, backgroundRow;
    [SerializeField] private MemoriCanvasGroup chaptersCompletedRow, goldEarnedRow, renownEarnedRow, enemiesSlainRow;
    // Shown only when the run ended on the March; inactive in the scene so other endings keep their layout.
    [SerializeField] private MemoriCanvasGroup endlessChaptersRow;
    [SerializeField] private TMP_Text endlessChaptersLabel, endlessChaptersText;
    public MemoriButtonV2 mainMenuButton;
    [SerializeField] private MemoriCanvasGroup mainGameOverGroup, textGroup, fadeCanvasGroup;
    [SerializeField] private GameObject defeatObject, victoryObject;
    [SerializeField] private MMF_Player crestSpawnFeedback;

    [Header("Act Complete")]
    [SerializeField] private GameObject actCompleteObject;
    [SerializeField] private MemoriButtonV2 continueButton;
    // Replace Continue from the last story act on: keep marching, or end the run as a win.
    [SerializeField] private MemoriButtonV2 marchOnButton, claimVictoryButton;
    [SerializeField] private TMP_Text actCompleteTextPart1, actCompleteTextPart2,  actCompleteTextPart3;
    // The last story act ends on a choice: the map dims to black and the two roads stand in the middle. Null on an older scene.
    [SerializeField] private CanvasGroup marchChoiceDim;
    [SerializeField] private RectTransform marchChoiceRow;
    // Wrappers the stagger scales, because MemoriButtonV2 owns its own scale.
    [SerializeField] private RectTransform marchOnSlot, claimVictorySlot;
    [SerializeField] private TMP_Text marchOnLine, claimVictoryLine;
    [SerializeField] private float marchChoiceDimTime = 0.8f;
    // Each choice slams in with a burst of its colour behind it; March On also lights its fire.
    [SerializeField] private RectTransform claimVictoryBurst, marchOnBurst;
    [SerializeField] private GameObject marchOnFire;
    // The "?" beside March On: what the March is, and that the banked win and its Renown are kept.
    [SerializeField] private MemoriTooltipTrigger marchOnInfo;
    [SerializeField] private float choiceDelay = 2.5f;
    [SerializeField] private float choiceGap = 0.5f;
    [SerializeField] private float slamTime = 0.22f;
    [SerializeField] private float slamFromScale = 1.6f;
    [SerializeField] private float burstTime = 0.6f;
    // Each landing shakes the screen art and the choices; the dim stays put so no map edge shows.
    [SerializeField] private float shakeAmplitude = 14f;
    [SerializeField] private float shakeTime = 0.3f;
    bool _shaking;
    // Above the squad card counts (101) so the dim covers the HUD; below the Engagement pop-up (104), Settings and tooltips.
    const int MarchChoiceSortingOrder = 103;
    Canvas _canvas;
    int _restingSortingOrder;

    [Header("Hero Unlock")]
    [SerializeField] private MemoriCanvasGroup heroUnlockRow;
    [SerializeField] private TMP_Text unlockedHeroNameText;
    [SerializeField] private Image unlockedHeroImage;
    [SerializeField] private MemoriButtonV2 heroUnlockContinueButton;

    [Header("Leaderboard")]
    // A results row for where a Godking win or March placed. Optional, like the hero unlock row.
    [SerializeField] private MemoriCanvasGroup leaderboardRow;
    [SerializeField] private TMP_Text leaderboardText;

    TT_Difficulty _difficulty;
    bool _unlocksNewHero;
    int _unlockedHeroID;
    // A run that ended on the March: its win was banked at act III, so the end screen is a victory that counts battles.
    bool _endedOnMarch;
    int _marchBattlesWon;
    // The submission this screen reports on, or null when the run goes on no board.
    string _boardRunId, _boardKind;
    // Counted up when its row arrives; 0 leaves the row's text as written.
    int _renownTotal;

    public void Start()
    {
        mainGameOverGroup.CGDisable();
        textGroup.CGDisable();
        fadeCanvasGroup.CGDisable();
        victoryObject.SetActive(false);
        defeatObject.SetActive(false);
        actCompleteObject.SetActive(false);
        continueButton.gameObject.SetActive(false);
        marchOnButton.gameObject.SetActive(false);
        claimVictoryButton.gameObject.SetActive(false);
        HideMarchChoice();
        if (heroUnlockRow != null) heroUnlockRow.CGDisable();
        if (heroUnlockContinueButton != null) heroUnlockContinueButton.gameObject.SetActive(false);
        if (leaderboardRow != null) leaderboardRow.CGDisable();
    }

    private void OnDestroy()
    {
        LeaderboardClient.Answered -= OnLeaderboardAnswer;
    }

    public void RecordGameOver(bool _beatDemo)
    {
        CampaignSaveData saveData = CampaignManager.Instance.CampaignSaveManager.SaveData;
        RunStats runStats = saveData.RunStats;
        _endedOnMarch = saveData.InMarch && saveData.victoryBanked;
        _marchBattlesWon = saveData.marchBattlesWon;
        // A Godking win was sent when act III fell; a Godking March is sent as it ends.
        _boardRunId = saveData.RunId;
        _boardKind = saveData.difficultyLevel != TT_Difficulty.Godking ? null
            : _endedOnMarch ? (_marchBattlesWon > 0 ? LeaderboardClient.KindEndless : null)
            : _beatDemo ? LeaderboardClient.KindGodking : null;

        _unlocksNewHero = false;
        if (_beatDemo)
        {
            int currentHeroID = saveData.heroID;
            // A banked win already wrote the completion, so the save remembers whether it was the first.
            bool isFirstCompletion = saveData.victoryBanked
                ? saveData.victoryWasFirstHeroCompletion
                : SaveDataHandler.GetHeroDifficultiesCompleted(currentHeroID).Count == 0;
            if (isFirstCompletion)
            {
                Hero nextHero = HeroData.GetHeroByID(currentHeroID + 1);
                if (nextHero.HeroID == currentHeroID + 1 && nextHero.UnlockCondition == UnlockCondition.HeroCompletion)
                {
                    _unlocksNewHero = true;
                    _unlockedHeroID = nextHero.HeroID;
                    string unlockedLocalized = LocalizationManager.Instance.GetText("New Hero Unlocked");
                    string heroLocalized = LocalizationManager.Instance.GetText(nextHero.HeroName);
                    unlockedHeroNameText.text = $"<color={ColorData.Secondary}>{unlockedLocalized}</color> <color={ColorData.Primary}>{heroLocalized}</color>";
                }
            }
        }

        RenownAward renownAward = SaveDataHandler.RecordGameOver(_beatDemo);

        string heroNameLocalized = LocalizationManager.Instance.GetText(HeroData.GetHeroByID(saveData.heroID).HeroName);
        heroNameText.text = heroNameLocalized;
        _difficulty = saveData.difficultyLevel;

        //get selected difficulty data
        DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(_difficulty);
        string difficultyNamestring = LocalizationManager.Instance.GetText(difficultyData.difficultyName);
        string chaptersLocalized = LocalizationManager.Instance.GetText("Chapters");
        string actsLocalized = LocalizationManager.Instance.GetText("Acts");

        difficultyNameText.text = difficultyNamestring;

        chaptersCompletedText.text = runStats.chaptersCompleted.ToString();
        goldEarnedText.text = runStats.goldEarned.ToString();
        enemiesSlainText.text = runStats.enemiesSlain.ToString();
        if (endlessChaptersRow != null)
        {
            endlessChaptersRow.gameObject.SetActive(_endedOnMarch);
            endlessChaptersLabel.text = $"<color={ColorData.Gold}>{LocalizationManager.Instance.GetText("endlessModeChapters")}</color>";
            endlessChaptersText.text = $"<color={ColorData.Gold}>{_marchBattlesWon}</color>";
        }

        renownEarnedText.text = $"<color={ColorData.Tier4}>{renownAward.total}</color>";
        _renownTotal = renownAward.total;
        renownBreakdownText.text = $"{renownAward.chaptersCompleted} {chaptersLocalized}  |  {renownAward.actsCompleted} {actsLocalized} (+{renownAward.actRenown})  |  {difficultyNamestring} (x{renownAward.difficultyMultiplier:0.00})";
        if (renownAward.marchBattles > 0)
            renownBreakdownText.text += $"  |  {string.Format(LocalizationManager.Instance.GetText("RunHistoryMarchBattles"), renownAward.marchBattles)} (+{renownAward.marchRenown})";
        if (renownAward.ordealCount > 0)
            renownBreakdownText.text += $"  |  {renownAward.ordealCount} {LocalizationManager.Instance.GetText("Ordeals")} (x{renownAward.ordealMultiplier:0.00})";

        // A banked victory already reported its win when act 3 fell; closing it reports the endless march, if any.
        string endReason = _beatDemo
            ? (saveData.victoryBanked ? "claim" : "win")
            : saveData.selectedNodeType == NodeType.Town ? "garrisonLoss" : "fieldLoss";
        GameEventTracker.RunClosed(saveData, _beatDemo ? RunResult.Win : RunResult.Loss, endReason, renownAward.total);

        CampaignManager.Instance.CampaignSaveManager.DeleteCampaignSave();

        // The save is gone but the map HUD underneath this panel is still live and interactive, and every
        // troop interaction reads it. This is the single funnel for run end (defeat via EngagementPanel,
        // victory via MapSceneManager), so locking here covers both.
        CampaignManager.Instance.MapSceneUIManager.HUDPanel.LockForRunEnd();
    }
    public async void DisplayGameOver(bool beatDemo = false)
    {
        string demoCompletedLocalized = LocalizationManager.Instance.GetText("Demo Completed");
        string defeatedLocalized = LocalizationManager.Instance.GetText("Defeated");
        demoCompletionText.text = beatDemo ? demoCompletedLocalized : defeatedLocalized;
        // Falling on the March is how the March ends: the screen is the victory it banked, with its chapters as a stat row.
        if (_endedOnMarch) beatDemo = true;
        IAudioRequester.Instance.SwitchToGameOverMusic(beatDemo);
        // The Act Complete buttons sit beside the results card; a claimed victory arrives from that screen.
        continueButton.gameObject.SetActive(false);
        marchOnButton.gameObject.SetActive(false);
        claimVictoryButton.gameObject.SetActive(false);
        HideMarchChoice();
        mainGameOverGroup.CGEnable();
        defeatObject.SetActive(!beatDemo);
        victoryObject.SetActive(beatDemo);

        mainMenuButton.Button.ClearClickListeners();
        mainMenuButton.Button.onClick.AddListener(() => ExitAfterFadeOut());
        
        for (int i = 0; i < difficultyCrests.Length; i++)
        {
            difficultyCrests[i].SetActive(i == DifficultyData.GetDifficultyLevelData(_difficulty).crestIndex);
        }
        
        await Task.Delay(500);
        if (_unlocksNewHero && heroUnlockRow != null)
            await ShowHeroUnlockScreen();
        bool showBoard = PrepareLeaderboardLine();
        FitMarchRow(showBoard);
        await FadeInStatsSequentially(showBoard);
        // Main Menu works during the fade-in; once the map unloads, the tip would open over the menu.
        if (this == null) return;
        TutorialManager.Instance.LoadStepsFromRandomSpot(new TutorialStep[1] { TutorialData.RenownCarriesOver });
    }

    // The stat stack fits five rows above the thank-you message: the March row takes the empty leaderboard slot, or all rows close up.
    private void FitMarchRow(bool showBoard)
    {
        if (!_endedOnMarch || endlessChaptersRow == null || leaderboardRow == null) return;
        if (!showBoard)
        {
            leaderboardRow.gameObject.SetActive(false);
            return;
        }
        if (endlessChaptersRow.transform.parent.TryGetComponent(out VerticalLayoutGroup stack)) stack.spacing = MarchRowSpacing;
    }
    const float MarchRowSpacing = -8f;

    private async Task FadeInStatsSequentially(bool showBoard)
    {
        textGroup.CGEnable();

        var rows = new System.Collections.Generic.List<MemoriCanvasGroup> { backgroundRow, difficultyRow, heroNameRow, chaptersCompletedRow, enemiesSlainRow, goldEarnedRow };
        if (_endedOnMarch && endlessChaptersRow != null) rows.Add(endlessChaptersRow);
        rows.Add(renownEarnedRow);
        rows.Add(completionMessageRow);
        if (showBoard) rows.Add(leaderboardRow);
        foreach (var row in rows) row.CGDisable();

        const float rowFade = 0.3f;
        const int rowGapMs = 150;

        // One sound for the whole deal-in; the rows overlap instead of waiting for each other.
        IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
        foreach (var row in rows)
        {
            if(row == difficultyRow)
                crestSpawnFeedback.PlayFeedbacks();

            _ = row.FadeIn(rowFade);
            if (row == renownEarnedRow && _renownTotal > 0) StartCoroutine(CountRenown());
            await Task.Delay(rowGapMs);
            if (this == null) return;
        }
        await Task.Delay(Mathf.RoundToInt(rowFade * 1000f));
    }

    // The run's reward counts up with ticks, then punches as it lands.
    private IEnumerator CountRenown()
    {
        yield return UIJuice.CountUp(renownEarnedText, _renownTotal, 0.6f, v => $"<color={ColorData.Tier4}>{v}</color>",
            () => IAudioRequester.Instance.PlaySFX(SFXData.TinyClick), 6);
        yield return UIJuice.Punch(renownEarnedText.transform, 1.15f);

    }

    #region Leaderboard line
    // True when this run went to a board and the server's answer is coming or already here.
    private bool PrepareLeaderboardLine()
    {
        if (leaderboardRow == null || _boardKind == null || !LeaderboardClient.Expecting(_boardRunId, _boardKind)) return false;
        LeaderboardClient.Answered -= OnLeaderboardAnswer;
        LeaderboardClient.Answered += OnLeaderboardAnswer;
        LeaderboardClient.TryGetAnswer(_boardRunId, _boardKind, out LeaderboardAnswer answer);
        leaderboardText.text = LeaderboardLine(answer);
        return true;
    }

    private void OnLeaderboardAnswer(string runId, string kind, LeaderboardAnswer answer)
    {
        if (this == null || runId != _boardRunId || kind != _boardKind) return;
        leaderboardText.text = LeaderboardLine(answer);
    }

    // Nothing for a run the leaderboard turned away; the player was not promised a place.
    private string LeaderboardLine(LeaderboardAnswer answer)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        if (answer == null || answer.status == "waiting") return loc.GetText("LeaderboardChecking");
        if (answer.status == "held") return loc.GetText("LeaderboardHeld");
        if (answer.status != "posted" || !answer.rank.HasValue || !answer.entries.HasValue) return string.Empty;
        int rank = answer.rank.Value;
        int entries = answer.entries.Value;
        return string.Format(loc.GetText("LeaderboardPlaced"), rank.ToString("N0"), entries.ToString("N0"), LeaderboardAnswer.TopPercent(rank, entries));
    }
    #endregion

    private async Task ShowHeroUnlockScreen()
    {
        if (unlockedHeroImage != null)
        {
            Sprite sprite = await TabletopTavernData.Instance.LoadHeroSpriteAsync(_unlockedHeroID);
            if (sprite != null) unlockedHeroImage.sprite = sprite;
        }

        IAudioRequester.Instance.PlaySFX(SFXData.LightMouseOver);
        await heroUnlockRow.FadeIn(0.4f);

        await Task.Delay(300);
        heroUnlockContinueButton.gameObject.SetActive(true);

        var tcs = new TaskCompletionSource<bool>();
        heroUnlockContinueButton.Button.ClearClickListeners();
        heroUnlockContinueButton.Button.onClick.AddListener(() => tcs.TrySetResult(true));
        await tcs.Task;

        heroUnlockContinueButton.gameObject.SetActive(false);
        await heroUnlockRow.FadeOut(0.3f);
    }

    public async void DisplayActComplete()
    {
        mainGameOverGroup.CGEnable();
        actCompleteObject.SetActive(true);

        IAudioRequester.Instance.PlaySFX(SFXData.Victory);
        IAudioRequester.Instance.PlaySFX(SFXData.Cheer);

        string actLocalized = LocalizationManager.Instance.GetText("Act");
        string completeLocalized = LocalizationManager.Instance.GetText("Complete");
        int actsCompleted = CampaignManager.Instance.CampaignSaveManager.SaveData.bookNumber;
        string totalText = $"<size=300><cspace=-20>{actLocalized[0]}<size=240><cspace=-4>{actLocalized[1..]} {MemoriUI.ConvertNumberToRomanNumeral(actsCompleted)} {completeLocalized}";
        actCompleteTextPart1.text = totalText;
        actCompleteTextPart2.text = totalText;
        actCompleteTextPart3.text = totalText;

        bool offerMarchOn = actsCompleted >= TabletopTavernConstants.FINAL_STORY_ACT
            && DifficultyRules.EndlessOffered(CampaignManager.Instance.CampaignSaveManager.SaveData.difficultyLevel);
#if DEMO
        offerMarchOn = false;
#endif
        continueButton.Button.ClearClickListeners();
        continueButton.Button.onClick.AddListener(CampaignManager.Instance.MapSceneUIManager.CompleteLayer);
        marchOnButton.Button.ClearClickListeners();
        marchOnButton.Button.onClick.AddListener(CampaignManager.Instance.MapSceneUIManager.CompleteLayer);
        // The Spell Update blocker is a child of the button, so a click on it would bubble up to March On.
        marchOnButton.Button.interactable = DifficultyRules.EndlessUnlocked;
        claimVictoryButton.Button.ClearClickListeners();
        claimVictoryButton.Button.onClick.AddListener(CampaignManager.Instance.MapSceneUIManager.ClaimVictory);

        bool centredChoice = offerMarchOn && marchChoiceDim != null;
        if (centredChoice)
        {
            _canvas.sortingOrder = MarchChoiceSortingOrder;
            marchChoiceDim.gameObject.SetActive(true);
            StartCoroutine(UIJuice.Open(marchChoiceDim, null, marchChoiceDimTime, 0f));
        }

        await Task.Delay(Mathf.RoundToInt((centredChoice ? choiceDelay : 1f) * 1000f));
        if (this == null) return;
        continueButton.gameObject.SetActive(!offerMarchOn);
        marchOnButton.gameObject.SetActive(offerMarchOn);
        claimVictoryButton.gameObject.SetActive(offerMarchOn);
        if (centredChoice) StartCoroutine(RevealMarchChoice());
    }

    // Claim Victory lands first, then March On after a beat, each with its own sting.
    private IEnumerator RevealMarchChoice()
    {
        marchOnLine.text = LocalizationManager.Instance.GetText("marchIntroLaws");
        claimVictoryLine.text = LocalizationManager.Instance.GetText("claimVictoryLine");
        if (marchOnInfo != null)
            marchOnInfo.SetUpToolTip(new TooltipContent
            {
                Title = LocalizationManager.Instance.GetText("marchIntroEndless"),
                Body = LocalizationManager.Instance.GetText("marchOnInfoBody"),
                Footer = $"<color={ColorData.Green}>{LocalizationManager.Instance.GetText("marchOnInfoFooter")}</color>",
            });
        CanvasGroup claimGroup = SlotGroup(claimVictorySlot);
        CanvasGroup marchGroup = SlotGroup(marchOnSlot);
        marchChoiceRow.gameObject.SetActive(true);

        yield return SlamIn(claimVictorySlot, claimGroup, claimVictoryBurst, SFXData.ChoiceMade);
        yield return new WaitForSecondsRealtime(choiceGap);
        if (marchOnFire != null) marchOnFire.SetActive(true);
        // The sting the Engagement panel opens with: March On is the road back into battle.
        yield return SlamIn(marchOnSlot, marchGroup, marchOnBurst, SFXData.SelectToBattle);
    }

    private static CanvasGroup SlotGroup(RectTransform slot)
    {
        CanvasGroup group = slot.GetComponent<CanvasGroup>();
        if (group == null) group = slot.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        return group;
    }

    // Drops in from large and lands hard: the sting, a burst behind it, then a punch. Clicks wait for the landing.
    private IEnumerator SlamIn(RectTransform slot, CanvasGroup group, RectTransform burst, string sting)
    {
        IAudioRequester.Instance.PlaySFX(sting);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / slamTime)
        {
            float e = t * t;
            slot.localScale = Vector3.one * Mathf.Lerp(slamFromScale, 1f, e);
            group.alpha = Mathf.Clamp01(t * 2.5f);
            yield return null;
        }
        slot.localScale = Vector3.one;
        group.alpha = 1f;
        group.interactable = true;
        group.blocksRaycasts = true;
        if (burst != null) StartCoroutine(Burst(burst));
        StartCoroutine(ScreenShake());
        yield return UIJuice.Punch(slot);
    }

    private IEnumerator ScreenShake()
    {
        if (_shaking) yield break;
        _shaking = true;
        RectTransform[] targets = { (RectTransform)actCompleteObject.transform, marchChoiceRow };
        Vector2[] rests = new Vector2[targets.Length];
        for (int i = 0; i < targets.Length; i++) rests[i] = targets[i].anchoredPosition;
        for (float t = 0f; t < shakeTime; t += Time.unscaledDeltaTime)
        {
            float fall = 1f - t / shakeTime;
            Vector2 offset = UnityEngine.Random.insideUnitCircle * shakeAmplitude * fall * fall;
            for (int i = 0; i < targets.Length; i++) targets[i].anchoredPosition = rests[i] + offset;
            yield return null;
        }
        for (int i = 0; i < targets.Length; i++) targets[i].anchoredPosition = rests[i];
        _shaking = false;
    }

    private IEnumerator Burst(RectTransform burst)
    {
        Graphic glow = burst.GetComponent<Graphic>();
        Color colour = glow.color;
        burst.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / burstTime)
        {
            float e = 1f - (1f - t) * (1f - t);
            burst.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.5f, e);
            glow.color = new Color(colour.r, colour.g, colour.b, Mathf.Lerp(colour.a, 0f, e));
            yield return null;
        }
        burst.gameObject.SetActive(false);
        glow.color = colour;
    }

    private void HideMarchChoice()
    {
        if (marchChoiceDim == null) return;
        _shaking = false;
        if (marchOnFire != null) marchOnFire.SetActive(false);
        if (claimVictoryBurst != null) claimVictoryBurst.gameObject.SetActive(false);
        if (marchOnBurst != null) marchOnBurst.gameObject.SetActive(false);
        if (_canvas == null)
        {
            _canvas = GetComponent<Canvas>();
            _restingSortingOrder = _canvas.sortingOrder;
        }
        _canvas.sortingOrder = _restingSortingOrder;
        marchChoiceDim.alpha = 0f;
        marchChoiceDim.gameObject.SetActive(false);
        marchChoiceRow.gameObject.SetActive(false);
    }
    public async void ExitAfterFadeOut()
    {
        await fadeCanvasGroup.FadeIn(1f);
        SceneHandler.Instance.SwitchGameState(GameStateEnum.MainMenu);
    }
}
}
