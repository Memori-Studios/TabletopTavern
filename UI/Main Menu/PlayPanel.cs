using Memori.SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Memori.Notifications;
using Memori.Localization;
using MoreMountains.Feedbacks;
using Memori.Audio;
using UnityEngine.EventSystems;
using Memori.Tooltip;
using Memori.UI;
using Memori.Metaprogression;
using System;
using Memori.Scenes;
using Memori.Utilities;
using System.Threading.Tasks;
using TJ.Spells;

namespace TJ.MainMenu
{
    /// <summary>
    /// Run setup, split across two screens.
    ///
    /// <b>Commander</b> - hero roster, 3D stage, dossier (hero effects, faction effects, signature
    /// unit, treasury) and the ten-rung difficulty ladder. Nothing here spends gold.
    /// <b>Warband</b> - army, gear and spells under one persistent purse, driven by
    /// <see cref="WarbandPanel"/>.
    ///
    /// This class owns the run's state (hero, difficulty, gear, army, spells) and is the only thing
    /// that calls CreateCampaign. The two screens are views over that state.
    /// </summary>
    public class PlayPanel : MainMenuPanel
    {
        [Header("Screens")]
        [SerializeField] private GameObject totalPanel;
        // Both screens stay active for the whole session and are shown/hidden by alpha and
        // raycasts rather than SetActive.
        [SerializeField] private MemoriCanvasGroup commanderScreen;
        [SerializeField] private MemoriCanvasGroup warbandScreen;
        [SerializeField] private WarbandPanel warbandPanel;
        [SerializeField] private Button toWarbandButton;
        [SerializeField] private Button startButton;

        [Header("Hero Roster")]
        [SerializeField] private HeroDifficultyButton[] heroSelectionButtons;
        [SerializeField] private MMF_Player heroPopInFeedback;
        [SerializeField] private DiscordUnlock discordUnlock;
        [SerializeField] private NewsletterUnlock newsletterUnlock;

        [Header("Hero Stage")]
        [SerializeField] private Transform heroParent;
        [SerializeField] private TMP_Text stageHeroNameText;
        [SerializeField] private TMP_Text stageFactionText;
        [SerializeField] private TMP_Text stageLoreText;

        [Header("Dossier - Hero")]
        [SerializeField] private TMP_Text heroBonusText1, heroBonusText2;
        [SerializeField] private MemoriTooltipTrigger heroTooltipTrigger;

        [Header("Dossier - Faction")]
        [SerializeField] private TMP_Text raceTitleText;
        [SerializeField] private TMP_Text raceCampaignBonusText, raceBattleBonusText;

        [Header("Dossier - Signature Unit")]
        [SerializeField] private TMP_Text signatureUnitNameText;
        [SerializeField] private SquadDisplayCardMenu uniqueUnitDisplayCardMenu;

        [Header("Dossier - Treasury")]
        [SerializeField] private TMP_Text treasuryTitleText;
        [SerializeField] private TMP_Text heroGoldText;
        [SerializeField] private TMP_Text treasuryNoteText;
        [SerializeField] private MemoriTooltipTrigger startingGoldTooltipTrigger;

        [Header("Difficulty")]
        [SerializeField] private TT_Difficulty _difficultySelected;
        [SerializeField] private GameObject extraInfo;
        [SerializeField] private TMP_Text _difficultyTitle, difficultyDescriptionText;
        [SerializeField] private Button increaseDifficultyButton, decreaseDifficultyButton;
        [SerializeField] private GameObject[] difficultyCrests;
        [SerializeField] private MMF_Player crestSpawnFeedback;
        [SerializeField] private MemoriTooltipTrigger additionalDifficultyInfoTooltipTrigger;
        // Optional: the collapsed "Difficulty: Level 7 Emperor" label. Its old home was the flyout
        // header button, which the redesign removes, so leave it unassigned if there is no longer
        // a summary label for it.
        [SerializeField] private TMP_Text difficultyButtonText;
        [SerializeField] private LockedButton lockedDifficultyStartButton;

        [Header("Army & Gear")]
        [SerializeField] private StartingArmyManager startingArmySection;
        [SerializeField] private Transform startingUnitsParent;
        [SerializeField] private MetaprogressionModel _startingArmyUnlockMetaprogressionModel;
        [SerializeField] private GameObject startingGearLockedNotice;
        [SerializeField] private TMP_Text armyCustomisationGateText;
        // Full-column overlay over the warband screen's army source list, and the only thing
        // that stops a unit being added while the hero is gated - RemoveTroop re-checks the
        // same flag for the loadout side. Left unassigned, army customisation is silently open.
        [SerializeField] private LockedButton startingArmyLockedBlocker;

        [Header("Camera Scene")]
        [SerializeField] private Camera _mainMenuCamera;
        [SerializeField] private Camera heroCamera;
        [SerializeField] private Transform cameraSceneParent;
        [SerializeField] private Vector3 commanderCameraRotation = Vector3.zero;
        [SerializeField] private Vector3 warbandCameraRotation = new Vector3(0f, -12.75f, 5f);
        [SerializeField] private float cameraTurnDuration = 0.35f;

        public StartingArmyManager StartingArmySection => startingArmySection;
        public event Action<Hero> OnActiveHeroChanged;

        public Hero hero;
        public SquadToLoad uniqueSquad;
        public GearID StartingGearID => startingGearID;
        public TT_Difficulty SelectedDifficulty => _difficultySelected;
        public bool HeroIsUnlocked => heroIsUnlocked;
        public UnlockCondition ActiveUnlockCondition => _unlockCondition;
        public bool StartingArmyLockedForHero => startingArmyLockedForHero;

        GearID startingGearID;
        GameObject heroObject;
        string _loadedHeroPrefabKey;
        bool heroIsUnlocked;
        bool startingArmyLockedForHero;
        bool warbandScreenShown;
        string startingArmyGateReason = string.Empty;
        int _heroPrefabLoadVersion;
        int _maxDifficultyCompletedOverall = 0;
        UnlockCondition _unlockCondition;
        Coroutine cameraTurnRoutine;
        // Added once to the signature-unit card, not once per hero hover.
        
        TroopHoverPlayPanel signatureUnitHover;

        public override void SetUp(MainMenu _mainMenu)
        {
            base.SetUp(_mainMenu);

            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartButtonClicked);
            toWarbandButton.onClick.RemoveAllListeners();
            toWarbandButton.onClick.AddListener(ShowWarbandScreen);

            increaseDifficultyButton.onClick.RemoveAllListeners();
            increaseDifficultyButton.onClick.AddListener(IncreaseDifficulty);
            decreaseDifficultyButton.onClick.RemoveAllListeners();
            decreaseDifficultyButton.onClick.AddListener(DecreaseDifficulty);

            startingArmySection.OnStartingArmyLengthChanged -= StartingArmyLengthChanged;
            startingArmySection.OnStartingArmyLengthChanged += StartingArmyLengthChanged;

            warbandPanel.SetUp(this, startingArmySection);

            totalPanel.SetActive(false);
            UnloadHeroes();
        }

        public override async void OpenPanel()
        {
            SceneHandler.Instance.TranstionCameras(_mainMenuCamera, heroCamera);
            await Task.Delay(500);
            cameraSceneParent.gameObject.SetActive(true);
            this.gameObject.SetActive(true);
            base.OpenPanel();
            totalPanel.SetActive(true);
            _maxDifficultyCompletedOverall = SaveDataHandler.LoadPlayerSaveData().MaxDifficultyOverall;

            // Guard the pairing: a mod can add or remove a hero, and the roster buttons are a fixed
            // authored array. Extra heroes are reported rather than silently dropped.
            Hero[] allHeroes = HeroData.Heroes;
            int rosterCount = Mathf.Min(heroSelectionButtons.Length, allHeroes.Length);
            if (allHeroes.Length != heroSelectionButtons.Length)
            {
                Debug.LogError($"[PlayPanel] {allHeroes.Length} heroes but {heroSelectionButtons.Length} roster buttons - showing {rosterCount}.");
            }
            for (int i = 0; i < rosterCount; i++)
            {
                heroSelectionButtons[i].LoadHeroSelectionPage(allHeroes[i], this);
            }

            Hero openingHero = GetHeroToOpenWith();
            int openingIndex = 0;
            for (int i = 0; i < rosterCount; i++)
            {
                if (allHeroes[i].HeroID != openingHero.HeroID) continue;
                openingIndex = i;
                break;
            }
            if (rosterCount > 0) EventSystem.current.SetSelectedGameObject(heroSelectionButtons[openingIndex].gameObject);

            LoadHeroes(openingHero);

            bool startingGearLocked = !SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingArmyUnlockMetaprogressionModel);
            startingGearLockedNotice.SetActive(startingGearLocked);

            // Closing the panel can leave the camera mid-turn, so start from the commander pose
            // rather than swinging into it as the panel opens.
            SnapHeroCameraTo(commanderCameraRotation);
            ShowCommanderScreen();
        }

        public override async void ClosePanel()
        {
            SceneHandler.Instance.TranstionCameras(heroCamera, _mainMenuCamera);
            await Task.Delay(500);
            UnloadHeroes();
            cameraSceneParent.gameObject.SetActive(false);
            base.ClosePanel();
            totalPanel.SetActive(false);
        }

        #region Screen switching
        public void ShowCommanderScreen()
        {
            warbandScreenShown = false;
            RefreshStartingArmyBlocker();

            commanderScreen.CGEnable();
            warbandScreen.CGDisable();
            TurnHeroCameraTo(commanderCameraRotation);
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        }

        public void ShowWarbandScreen()
        {
            // A locked hero can never start a run, so there is nothing to set up on the warband
            // screen. The start button was already gated by the validation strip, but the player
            // could still walk in and build an army and a spell loadout they could not play.
            if (!heroIsUnlocked)
            {
                NotificationManager.Instance.ErrorNotification(
                    HeroBonusManager.GetLocalizedHeroUnlockDescription(hero, _unlockCondition));
                return;
            }

            // Filled before the screen is shown, so it is never seen holding the last hero's loadout.
            warbandPanel.LoadForHero(hero);

            warbandScreenShown = true;
            RefreshStartingArmyBlocker();

            warbandScreen.CGEnable();
            commanderScreen.CGDisable();
            TurnHeroCameraTo(warbandCameraRotation);
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        }

        /// <summary>
        /// The blocker is a full-column overlay parented under the warband screen, and
        /// <c>LoadHeroes</c> raises it on every hero switch - which happens while the COMMANDER
        /// screen is up. That only looked safe because the warband screen is hidden by
        /// <c>CGDisable()</c>, which fades the CanvasGroup and never deactivates it. The scene
        /// instance carries its own Canvas, so the fade does not reach it and the blocker paints
        /// over the commander screen while the player is still picking a hero.
        ///
        /// So its visibility is gated on the warband screen actually being shown rather than on
        /// the fade. That holds whatever that Canvas is doing and needs no change to the
        /// authored hierarchy. It costs nothing on the gate itself: the army source list can
        /// only be reached from the warband screen, and <c>RemoveTroop</c> re-checks
        /// <c>StartingArmyLockedForHero</c> independently for the loadout side.
        /// </summary>
        private void RefreshStartingArmyBlocker()
        {
            startingArmyLockedBlocker.SetLockedState(
                warbandScreenShown && startingArmyLockedForHero, startingArmyGateReason);
        }
        #endregion

        #region Hero camera
        /// <summary>
        /// Turns the hero camera between the two run-setup screens. Rotation only - the camera never
        /// moves, so there is no position term to interpolate alongside it.
        ///
        /// <b>localRotation, not rotation.</b> The camera is parented and the parent carries its
        /// world placement, so local is both what the Transform inspector shows and what the
        /// authored values mean.
        /// </summary>
        private void TurnHeroCameraTo(Vector3 targetEuler)
        {
            if (cameraTurnRoutine != null) StopCoroutine(cameraTurnRoutine);
            cameraTurnRoutine = StartCoroutine(TurnHeroCameraRoutine(Quaternion.Euler(targetEuler)));
        }

        private IEnumerator TurnHeroCameraRoutine(Quaternion target)
        {
            Quaternion start = heroCamera.transform.localRotation;
            float elapsed = 0f;

            while (elapsed < cameraTurnDuration)
            {
                // Unscaled: Time.timeScale is owned by the battle scene, and the main menu must not
                // inherit whatever it was left at.
                elapsed += Time.unscaledDeltaTime;
                heroCamera.transform.localRotation =
                    Quaternion.Slerp(start, target, Mathf.Clamp01(elapsed / cameraTurnDuration));
                yield return null;
            }

            heroCamera.transform.localRotation = target;
            cameraTurnRoutine = null;
        }

        /// <summary>
        /// Jumps straight to a pose with no turn. Used when opening the panel: the camera can be
        /// left mid-turn by a close, and entering run setup should not replay the swing.
        /// </summary>
        private void SnapHeroCameraTo(Vector3 targetEuler)
        {
            if (cameraTurnRoutine != null)
            {
                StopCoroutine(cameraTurnRoutine);
                cameraTurnRoutine = null;
            }

            heroCamera.transform.localRotation = Quaternion.Euler(targetEuler);
        }
        #endregion

        #region Hero
        /// <summary>
        /// The panel opens on the hero the player last started a run with. lastHeroID is 0 on a
        /// fresh save and GetHeroByID falls back to the default hero, so first launch needs no
        /// special case. A hero that is no longer unlocked - a full-game save opened in the demo
        /// build - falls back too, rather than opening the panel already blocked.
        /// </summary>
        private Hero GetHeroToOpenWith()
        {
            Hero lastHero = HeroData.GetHeroByID(SaveDataHandler.LoadPlayerSaveData().lastHeroID);
#if DEMO
            UnlockCondition condition = lastHero.DemoUnlockCondition;
            if (SaveDataHandler.IsDevToolUser()) return lastHero;
#else
            UnlockCondition condition = lastHero.UnlockCondition;
#endif
            if (SaveDataHandler.IsUnlockConditionUnlocked(condition, lastHero.HeroID)) return lastHero;
            return HeroData.GetHeroByID(HeroData.DefaultHeroID);
        }

        public void LoadHeroes(Hero _hero, bool _resetGear = false)
        {
            UnloadHeroes();
            SetActiveHero(_hero);
#if DEMO
            _unlockCondition = _hero.DemoUnlockCondition;
#else
            _unlockCondition = _hero.UnlockCondition;
#endif
            heroIsUnlocked = SaveDataHandler.IsUnlockConditionUnlocked(_unlockCondition, _hero.HeroID);

#if DEMO
            if(SaveDataHandler.IsDevToolUser()) heroIsUnlocked = true;
#endif

            LoadHeroPrefab();

            IAudioRequester.Instance.PlaySFX(SFXData.SelectHero);

            List<int> maxDifficultyComletedOnHero = SaveDataHandler.GetHeroDifficultiesCompleted(hero.HeroID);

            toWarbandButton.interactable = heroIsUnlocked;

            // Army customisation needs one completed run with this hero. Stated up front on the
            // commander screen rather than surfacing as an error after a click on the next screen.
            startingArmyLockedForHero = !heroIsUnlocked || maxDifficultyComletedOnHero.Count == 0;
            string armyGateReason = LocalizationManager.Instance.GetText("OneCompletionRequired");
            armyCustomisationGateText.text = startingArmyLockedForHero
                ? armyGateReason
                : LocalizationManager.Instance.GetText("ArmyCustomisationUnlocked");
            startingArmyGateReason = armyGateReason;
            RefreshStartingArmyBlocker();

            if (_unlockCondition == UnlockCondition.DiscordExclusive && !heroIsUnlocked)
            {
                discordUnlock.ShowPanel();
            }
            else if (_unlockCondition == UnlockCondition.NewsletterExclusive && !heroIsUnlocked)
            {
                newsletterUnlock.ShowPanel();
            }

            if(_resetGear)
            {
                SetStartingGear(GearID.None);
            }
            else
            {
                if(!SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingArmyUnlockMetaprogressionModel))
                {
                    SetStartingGear(GearID.None);
                }
                else
                {
                    SetStartingGear(SaveDataHandler.LoadPlayerSaveData().lastStartingGearId);
                }
            }

            startingArmySection.SetUp(this);
            ShowHeroDetailsBox(hero);
            startingArmySection.LoadUnitsOfRace(hero.Race);

            LoadDifficulty(SaveDataHandler.GetHeroLastDifficulty(_hero.HeroID));

            // A different hero means a different signature spell, so the loadout is rebuilt rather
            // than carried over.
            warbandPanel.ResetLoadoutForHero(hero);
        }

        public async void LoadHeroPrefab()
        {
            int version = ++_heroPrefabLoadVersion;
            string key = TabletopTavernData.Instance.GetHeroPrefabKey(hero.HeroID);
            // Persistent: heroParent lives in the permanent Tavern backdrop, so this
            // instance must survive the ReleaseAll() calls on scene transitions.
            // Released explicitly in UnloadHeroes when the instance is destroyed.
            GameObject prefab = await TabletopTavernData.Instance.LoadHeroPrefabAsync(hero.HeroID, persistent: true);
            if (version != _heroPrefabLoadVersion) return;
            _loadedHeroPrefabKey = key;
            heroObject = Instantiate(prefab, heroParent);

            //get the animator from the new gameobject and play the pop in animation
            Animator animator = heroObject.GetComponent<Animator>();
            if (animator != null)
                animator.Play("HeroPopIn");

            heroPopInFeedback.PlayFeedbacks();
        }

        public void SetActiveHero(Hero _hero)
        {
            hero = _hero;
            OnActiveHeroChanged?.Invoke(hero);
            UpdateStartingGoldTooltip(_hero.StartingGold);
        }

        public void RevertToActiveHeroDetailsBox(Hero unhoveredHero)
        {
            if (unhoveredHero.HeroID != hero.HeroID)
            {
                ShowHeroDetailsBox(hero);
            }
        }

        /// <summary>
        /// Fills the stage and dossier. Called on hero change and on roster hover, so it must stay
        /// free of side effects that accumulate - see the signature-unit hover component below.
        /// </summary>
        public void ShowHeroDetailsBox(Hero _hero)
        {
            string heroNameLocalized = LocalizationManager.Instance.GetText(_hero.HeroName);
            stageHeroNameText.text = heroNameLocalized;
            stageFactionText.text = LocalizationManager.Instance.GetText(_hero.Race.ToString());
            stageLoreText.text = LocalizationManager.Instance.GetLoreString(_hero.HeroPrefabName);

            string heroBonusText1string = HeroBonusText.Get(_hero, 0);
            string heroBonusText2string = HeroBonusText.Get(_hero, 1);
            ColorData.XMLTagColorApplicator(ref heroBonusText1string);
            ColorData.XMLTagColorApplicator(ref heroBonusText2string);
            heroBonusText1.text = ApplyPrimaryColorToLabel(heroBonusText1string);
            heroBonusText2.text = ApplyPrimaryColorToLabel(heroBonusText2string);

            //faction
            string factionLocalized = LocalizationManager.Instance.GetText("Faction");
            string factionNameLocalized = LocalizationManager.Instance.GetText(_hero.Race.ToString());
            // TMP's <uppercase> is a render-time transform, so it cannot mangle a locale the way
            // string.ToUpper() can, and it leaves CJK untouched.
            raceTitleText.text = $"<color={ColorData.Gold}><uppercase>{factionLocalized} · {factionNameLocalized}</uppercase></color>";

            // Labelled by phase so it is unambiguous which effect fires on the map and which fires
            // in a battle - they read identically otherwise.
            string campaignBonusLocalized = LocalizationManager.Instance.GetText(_hero.Race + "BonusDescription");
            ColorData.XMLTagColorApplicator(ref campaignBonusLocalized);
            raceCampaignBonusText.text = BuildFactionEffectLine(
                LocalizationManager.Instance.GetText("CampaignEffectLabel"), campaignBonusLocalized);

            string battleBonusLocalized = $"{LocalizationManager.Instance.GetText(_hero.Race + "PassiveName")}: {RacePassiveInfo.GetDescription(_hero.Race)}";
            ColorData.XMLTagColorApplicator(ref battleBonusLocalized);
            raceBattleBonusText.text = BuildFactionEffectLine(
                LocalizationManager.Instance.GetText("BattleEffectLabel"), battleBonusLocalized);

            ShowTreasury(_hero);

            ShowSignatureUnit(_hero);

            string heroDescription = LocalizationManager.Instance.GetText(_hero.HeroDescription);
            heroTooltipTrigger.SetUpToolTip($"{heroNameLocalized}", $"{heroDescription}");
        }

        /// <summary>
        /// Title / total / note. The total is base + renown: the old readout printed the hero's raw
        /// StartingGold with the bonus in brackets after it, so the number the player read was never
        /// the number they had to spend.
        /// </summary>
        private void ShowTreasury(Hero _hero)
        {
            int renownBonus = startingArmySection.StartingGoldBonusFromMetaprogression;
            int total = _hero.StartingGold + renownBonus;

            treasuryTitleText.text = LocalizationManager.Instance.GetText("StartingTreasuryTitle");

            // The breakdown only earns its line when there is something to break down.
            if (renownBonus > 0)
            {
                string renownPart = $"<color={ColorData.Green}>+{renownBonus}</color>";
                heroGoldText.text = string.Format(
                    LocalizationManager.Instance.GetText("StartingTreasuryBreakdown"),
                    total, _hero.StartingGold, renownPart);
            }
            else
            {
                heroGoldText.text = $"{total} <sprite name=GoldSprite>";
            }

            treasuryNoteText.text = LocalizationManager.Instance.GetText("StartingTreasuryNote");
        }

        private void ShowSignatureUnit(Hero _hero)
        {
            uniqueSquad = new SquadToLoad(
                _hero.SignatureUnit,
                _prestige: 0,
                _unitIndex: 0
            );
            uniqueUnitDisplayCardMenu.SetUp(uniqueSquad, false, _isEnemy: true);
            uniqueUnitDisplayCardMenu.LockCard(true);
            uniqueUnitDisplayCardMenu.InheritCanvasSorting();

            // AddComponent used to run on every call, stacking one hover handler per roster hover.
            // GetComponent first so a handler already present on the card prefab is reused rather
            // than duplicated.
            if (signatureUnitHover == null)
            {
                signatureUnitHover = uniqueUnitDisplayCardMenu.GetComponent<TroopHoverPlayPanel>();
            }
            if (signatureUnitHover == null)
            {
                signatureUnitHover = uniqueUnitDisplayCardMenu.gameObject.AddComponent<TroopHoverPlayPanel>();
            }
            signatureUnitHover.SetUp(SIGNATURE_UNIT_HOVER_INDEX, this);

            signatureUnitNameText.text = LocalizationManager.Instance.GetText(uniqueSquad.UnitName.ToString());
        }

        /// <summary>Sentinel index meaning "the signature unit", not a slot in the starting army.</summary>
        public const int SIGNATURE_UNIT_HOVER_INDEX = 99;

        public void UnloadHeroes()
        {
            if(heroObject != null) {
                Destroy(heroObject);
                heroObject = null;
            }

            // Release the previous hero prefab's handle. It was loaded persistent
            // (pinned), so ReleaseAll() won't reclaim it - release it here, coupled
            // to destroying the instance it backs, or switching heroes leaks it.
            if (!string.IsNullOrEmpty(_loadedHeroPrefabKey))
            {
                AddressablesManager.Instance.Release(_loadedHeroPrefabKey);
                _loadedHeroPrefabKey = null;
            }
            SquadDisplayCardMenu[] squadDisplayCards = startingUnitsParent.GetComponentsInChildren<SquadDisplayCardMenu>();
            foreach (var squad in squadDisplayCards) {
                Destroy(squad.gameObject);
            }
        }

        public void ReloadHeroOnDiscordUnlock()
        {
            heroSelectionButtons[4].LoadHeroSelectionPage(HeroData.BjornIronskull, this);
            LoadHeroes(HeroData.BjornIronskull);
        }

        public void ReloadHeroOnNewsletterUnlock()
        {
            heroSelectionButtons[5].LoadHeroSelectionPage(HeroData.FreyjaStormweaver, this);
            LoadHeroes(HeroData.FreyjaStormweaver);
        }
        #endregion

        #region Difficulty
        public void IncreaseDifficulty()
        {
            if((int)_difficultySelected < (int)TT_Difficulty.Godking)
            {
                LoadDifficulty(_difficultySelected + 1);
            }
        }
        public void DecreaseDifficulty()
        {
            if((int)_difficultySelected > 1)
            {
                LoadDifficulty(_difficultySelected - 1);
            }
        }
        public void LoadDifficulty(TT_Difficulty _selectedDifficulty)
        {
            _difficultySelected = _selectedDifficulty;
            IAudioRequester.Instance.PlaySFX(SFXData.ChangeDifficulty);

            //get selected difficulty data
            DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(_difficultySelected);

            //set title
            string difficultyTitleLocalized = LocalizationManager.Instance.GetText("Difficulty");
            string levelLocalized = LocalizationManager.Instance.GetText("Level");
            string difficultyNamestring = LocalizationManager.Instance.GetText(difficultyData.difficultyName);
            _difficultyTitle.text = $"{levelLocalized} {(int)_difficultySelected}: {difficultyNamestring}";

            //set description
            if(_difficultySelected != TT_Difficulty.Peasant)
            {
                string difficultyDescriptionstring =
                LocalizationManager.Instance.GetText(difficultyData.difficultyModifiers[0]) +
                "\n" +
                LocalizationManager.Instance.GetText(difficultyData.difficultyModifiers[1]);

                difficultyDescriptionText.text = difficultyDescriptionstring;
            }
            else
            {
                //special case for peasant difficulty
                difficultyDescriptionText.text = "";
            }
            extraInfo.SetActive(_difficultySelected >= TT_Difficulty.Knight);

            //set button text on right side
            if(difficultyButtonText != null)
            {
                difficultyButtonText.text = $"<color {ColorData.Secondary}>{difficultyTitleLocalized}:</color> <color {ColorData.Tier4}>{levelLocalized} {(int)_difficultySelected} {difficultyNamestring}</color>";
            }

            //disable/enable increase decrease buttons
            increaseDifficultyButton.gameObject.SetActive(_difficultySelected != TT_Difficulty.Godking);
            decreaseDifficultyButton.gameObject.SetActive(_difficultySelected != TT_Difficulty.Peasant);

            //set locked state
            bool isLocked = (int)_difficultySelected > _maxDifficultyCompletedOverall +1;
            if(_difficultySelected == TT_Difficulty.Peasant)
            {
                isLocked = false; //peasant is always unlocked
            }

            lockedDifficultyStartButton.SetLockedState(isLocked, LocalizationManager.Instance.GetText("Difficulty Locked"));

            //display difficulty crests
            for (int i = 0; i < difficultyCrests.Length; i++)
            {
                difficultyCrests[i].SetActive(i == ((int)_difficultySelected - 1));
            }
            crestSpawnFeedback.StopFeedbacks();
            crestSpawnFeedback.PlayFeedbacks();

            string additionalModifiersDesc = "";
            List<string> allPreviousModifiers = DifficultyData.GetAllDifficultyModifiersBeforeLevel(_difficultySelected);

            foreach (string modifier in allPreviousModifiers)
            {
                additionalModifiersDesc += "- " + LocalizationManager.Instance.GetText(modifier) + "\n";
            }
            string additionalModifiersTitleLocalized = LocalizationManager.Instance.GetText("Additional Modifiers");
            additionalDifficultyInfoTooltipTrigger.SetUpToolTip(
                additionalModifiersTitleLocalized, additionalModifiersDesc);

            // The spinner can still land on a locked difficulty, so the validation strip has to
            // re-check it - it is the thing that gates the start button.
            warbandPanel.RefreshValidation();
        }
        #endregion

        #region Gear & army
        public void SetStartingGear(GearID _gearID)
        {
            startingGearID = _gearID;
            warbandPanel.RefreshValidation();
        }

        public void RemainingTreasuryChanged(int _remainingTreasury)
        {
            warbandPanel.RefreshValidation();
        }

        public void StartingArmyLengthChanged(int newLength)
        {
            warbandPanel.RefreshValidation();
        }
        #endregion

        #region Start
        public void OnStartButtonClicked()
        {
            // The validation strip already collects every blocker and drives startButton.interactable,
            // so a blocked run cannot reach here through the button. Re-checked anyway because
            // OnStartButtonClicked is public and the old flow relied on ordered activeSelf checks.
            warbandPanel.RefreshValidation();
            if (!warbandPanel.Validation.CanStart)
            {
                NotificationManager.Instance.ErrorNotification(warbandPanel.Validation.Blockers[0]);
                return;
            }

            startButton.interactable = false;

            Guid runUUID = Guid.NewGuid();
            SaveDataHandler.CreateCampaign(hero, startingArmySection.SelectedArmy, SelectedDifficulty,
                                           startingGearID, runUUID, startingArmySection.remainingTreasury.Value,
                                           warbandPanel.Loadout);
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.campaignsStarted++;
            SaveDataHandler.SavePlayerSaveData(saveData);

            mainMenu.LoadMapScene();
        }
        #endregion

        public void OnDestroy()
        {
            if(startingArmySection != null)
            {
                startingArmySection.OnStartingArmyLengthChanged -= StartingArmyLengthChanged;
            }
        }

        private string ApplyPrimaryColorToLabel(string text)
        {
            int colonIndex = text.IndexOf(':');
            if (colonIndex < 0) return text;
            return $"<color={ColorData.Primary}>{text[..(colonIndex + 1)]}</color>{text[(colonIndex + 1)..]}";
        }

        /// <summary>
        /// "Imperial Edict: First recruitment pack is free" becomes
        /// "Imperial Edict: campaign - First recruitment pack is free", effect name in gold and
        /// phase in the muted colour. The phase used to lead the line, which buried the name.
        /// Both source strings carry the effect's name ahead of the first colon, so that colon is
        /// the split rather than a second localization key.
        /// </summary>
        private string BuildFactionEffectLine(string phaseLabel, string nameAndBody)
        {
            int colonIndex = nameAndBody.IndexOf(':');
            string name = colonIndex < 0
                ? ""
                : $"<color={ColorData.Gold}>{nameAndBody[..(colonIndex + 1)]}</color> ";
            string body = colonIndex < 0 ? nameAndBody : nameAndBody[(colonIndex + 1)..].TrimStart();

            // Trimmed because several of the phase-label table entries carry a trailing space.
            return $"{name}<color={ColorData.Secondary}>{phaseLabel.Trim()}</color> - {body}";
        }

        private void UpdateStartingGoldTooltip(int startingGoldTreasury)
        {
            string titleLocalized = LocalizationManager.Instance.GetText("startingGold");
            string desc1Localized = LocalizationManager.Instance.GetText("startingGoldDesc1");
            string desc2Localized = LocalizationManager.Instance.GetText("startingGoldDesc2");
            string bonusString = startingArmySection.StartingGoldBonusFromMetaprogression > 0 ? $" <color={ColorData.Green}>(+{startingArmySection.StartingGoldBonusFromMetaprogression})</color>" : "";
            string fullDescription = $"{desc1Localized} <color={ColorData.Gold}>[{startingGoldTreasury}]</color>{bonusString} {desc2Localized}";

            // startingGoldTooltipTrigger.SetUpToolTip(titleLocalized, fullDescription);
        }
    }
}
