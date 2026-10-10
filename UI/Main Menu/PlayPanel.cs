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
using VolumetricLights;
using TabletopTavern.Analytics;

namespace TJ.MainMenu
{
    /// <summary>
    /// Run setup, split across two screens.
    ///
    /// <b>Commander</b> - hero roster, 3D stage and the hero panel (effects, signature unit and spell,
    /// record, treasury), shown through <see cref="CommanderScreenView"/>. Nothing here spends gold.
    /// <b>Warband</b> - army, gear, spells and the difficulty ladder under one persistent purse,
    /// driven by <see cref="WarbandPanel"/>.
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

        [Header("Hero Screen")]
        [SerializeField] private CommanderScreenView commanderView;
        [SerializeField] private MMF_Player heroPopInFeedback;
        [SerializeField] private DiscordUnlock discordUnlock;
        [SerializeField] private NewsletterUnlock newsletterUnlock;

        [Header("Hero Stage")]
        [SerializeField] private Transform heroParent;
        [SerializeField] private MemoriTooltipTrigger startingGoldTooltipTrigger;
        // World-space FX on the hero: a small one when a hero is picked, a big one for the campaign send-off.
        [SerializeField] private GameObject heroPickFx;
        [SerializeField] private GameObject sendOffFx;
        [SerializeField] private float stageFxLifetime = 4f;

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

        public StartingArmyManager StartingArmySection => startingArmySection;
        public event Action<Hero> OnActiveHeroChanged;

        public Hero hero;
        public SquadToLoad uniqueSquad;
        public GearID StartingGearID => startingGearID;
        public bool StartingGearLocked => startingGearLocked;
        public TT_Difficulty SelectedDifficulty => _difficultySelected;
        public bool HeroIsUnlocked => heroIsUnlocked;
        public UnlockCondition ActiveUnlockCondition => _unlockCondition;
        public bool StartingArmyLockedForHero => startingArmyLockedForHero;

        GearID startingGearID;
        GameObject heroObject;
        string _loadedHeroPrefabKey;
        bool heroIsUnlocked;
        bool startingArmyLockedForHero;
        bool startingGearLocked;
        bool warbandScreenShown;
        string startingArmyGateReason = string.Empty;
        int _heroPrefabLoadVersion;
        UnlockCondition _unlockCondition;
        // Found at runtime: the light lives in the persistent Tavern scene, which a serialized field cannot reach.
        VolumetricLight fireplaceLight;

        public override void SetUp(MainMenu _mainMenu)
        {
            base.SetUp(_mainMenu);

            // Only this panel's own handler is removed: RemoveAllListeners also took the button's click sound.
            startButton.onClick.RemoveListener(OnStartButtonClicked);
            startButton.onClick.AddListener(OnStartButtonClicked);
            toWarbandButton.onClick.RemoveListener(ShowWarbandScreen);
            toWarbandButton.onClick.AddListener(ShowWarbandScreen);

            increaseDifficultyButton.onClick.RemoveListener(IncreaseDifficulty);
            increaseDifficultyButton.onClick.AddListener(IncreaseDifficulty);
            decreaseDifficultyButton.onClick.RemoveListener(DecreaseDifficulty);
            decreaseDifficultyButton.onClick.AddListener(DecreaseDifficulty);

            // A locked hero switches Build Army off; a click on it still says why (the toast plays the fail sound).
            UIDenyFeedback buildArmyDeny = UIDenyFeedback.Attach(toWarbandButton, sound: false);
            buildArmyDeny.Denied -= ShowWarbandScreen;
            buildArmyDeny.Denied += ShowWarbandScreen;

            startingArmySection.OnStartingArmyLengthChanged -= StartingArmyLengthChanged;
            startingArmySection.OnStartingArmyLengthChanged += StartingArmyLengthChanged;

            warbandPanel.SetUp(this, startingArmySection);

            totalPanel.SetActive(false);
            UnloadHeroes();
        }

        public override async void OpenPanel()
        {
            // Real time, so the deal-in meets the door however long the hero load below holds the frame.
            float openedAt = Time.realtimeSinceStartup;
            SceneHandler.Instance.TranstionCameras(_mainMenuCamera, heroCamera);
            await Task.Delay(500);
            cameraSceneParent.gameObject.SetActive(true);
            this.gameObject.SetActive(true);
            base.OpenPanel();
            totalPanel.SetActive(true);

            // Guard the pairing: a mod can add or remove a hero, and the roster tiles are a fixed
            // generated set. Extra heroes are reported rather than silently dropped.
            Hero[] allHeroes = HeroData.Heroes;
            HeroRosterTile[] tiles = commanderView.Tiles;
            int rosterCount = commanderView.LoadRoster(allHeroes, this);
            if (allHeroes.Length != tiles.Length)
            {
                Debug.LogError($"[PlayPanel] {allHeroes.Length} heroes but {tiles.Length} roster tiles - showing {rosterCount}.");
            }

            Hero openingHero = GetHeroToOpenWith();
            int openingIndex = 0;
            for (int i = 0; i < rosterCount; i++)
            {
                if (allHeroes[i].HeroID != openingHero.HeroID) continue;
                openingIndex = i;
                break;
            }
            if (rosterCount > 0) EventSystem.current.SetSelectedGameObject(tiles[openingIndex].gameObject);

            startingGearLocked = !SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingArmyUnlockMetaprogressionModel);
            // The camera door is the arrival sound; the hero and screen this panel picks for itself stay silent.
            silentSetUp = true;
            LoadHeroes(openingHero);
            ShowCommanderScreen();
            silentSetUp = false;
            startingArmySection.WarmUnitInfo();
            if (arrival != null) StopCoroutine(arrival);
            arrival = StartCoroutine(ArriveWithDoor(openedAt));
        }

        // The camera door (MainTransition) starts to open two thirds of the way through.
        private const float DoorOpensAt = 0.67f;
        private const float DoorWaitLimit = 2.5f;
        private Coroutine arrival;

        // The roster and hero panel deal in as the camera door opens; a load hitch holds the door, so it is waited on, not timed.
        private IEnumerator ArriveWithDoor(float openedAt)
        {
            while (SceneHandler.Instance.CameraDoorProgress < DoorOpensAt && Time.realtimeSinceStartup - openedAt < DoorWaitLimit)
                yield return null;
            commanderView.PlayArrival(0f);
            arrival = null;
        }

        private bool silentSetUp;
        private Coroutine screenSwap;

        // The incoming screen fades in and rises a little; the outgoing one is already hidden by CGDisable.
        private void PlayScreenSwap(MemoriCanvasGroup incoming)
        {
            if (screenSwap != null) StopCoroutine(screenSwap);
            screenSwap = StartCoroutine(UIJuice.Open(incoming.GetComponent<CanvasGroup>(), incoming.transform as RectTransform, UIJuice.SwapTime, 16f));
            if (!silentSetUp) IAudioRequester.Instance.PlaySFX(SFXData.CardFlip);
        }

        public override async void ClosePanel()
        {
            SceneHandler.Instance.TranstionCameras(heroCamera, _mainMenuCamera);
            await Task.Delay(500);
            UnloadHeroes();
            SetFireplaceShadowsLive(false);
            cameraSceneParent.gameObject.SetActive(false);
            base.ClosePanel();
            totalPanel.SetActive(false);
        }

        #region Screen switching
        public void ShowCommanderScreen()
        {
            // The campaign is already on its way; going back now would start it from the hero screen.
            if (sendOff != null) return;
            warbandScreenShown = false;
            warbandPanel.SetShown(false);
            RefreshWarbandBlockers();

            commanderScreen.CGEnable();
            warbandScreen.CGDisable();
            SetFireplaceShadowsLive(true);
            PlayScreenSwap(commanderScreen);
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
            RefreshWarbandBlockers();

            warbandScreen.CGEnable();
            commanderScreen.CGDisable();
            // Keys and a controller stay on the warband screen instead of wandering onto the hero roster behind it,
            // and start on the recruit list rather than the bottom corner.
            ContainedNavigation.Attach(warbandScreen.gameObject).PreferFirst(startingArmySection.FirstRecruitControl);
            // A selection left from the last visit would keep the first key press on Start Campaign.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PlayScreenSwap(warbandScreen);
            warbandPanel.SetShown(true);
            warbandPanel.PlayArrival();
            startingArmySection.PlayRecruitArrival();
        }

        /// <summary>
        /// Right-click steps the two screens back one at a time: Warband -> Commander first, and
        /// only from the Commander screen does the menu take over and return to the main menu.
        /// </summary>
        public override bool TryStepBack()
        {
            if (sendOff != null) return true;
            if (!warbandScreenShown) return false;

            ShowCommanderScreen();
            return true;
        }

        /// <summary>
        /// Both locked blockers sit under the warband screen with their own override-sorting Canvas,
        /// where uGUI stops honouring the parent CanvasGroup's blocksRaycasts, so <c>CGDisable()</c>
        /// leaves them catching clicks over the commander screen's hero roster. They are gated on the
        /// screen being shown instead; both lists are only reachable from the warband screen, and
        /// <c>RemoveTroop</c> re-checks <c>StartingArmyLockedForHero</c> for the loadout side.
        /// </summary>
        private void RefreshWarbandBlockers()
        {
            startingArmyLockedBlocker.SetLockedState(
                warbandScreenShown && startingArmyLockedForHero, startingArmyGateReason);
            startingGearLockedNotice.SetActive(warbandScreenShown && startingGearLocked);
        }
        #endregion

        #region Fireplace shadows
        /// <summary>
        /// Live shadows let the staged hero shade the fireplace light shaft; everywhere else the shadow stays baked.
        /// </summary>
        private void SetFireplaceShadowsLive(bool live)
        {
            if (fireplaceLight == null)
            {
                if (!live) return;
                foreach (VolumetricLight light in FindObjectsByType<VolumetricLight>(FindObjectsSortMode.None))
                {
                    if (light.gameObject.scene.buildIndex == (int)SceneIndexes.Tavern) fireplaceLight = light;
                }
                if (fireplaceLight == null) return;
            }

            fireplaceLight.shadowBakeInterval = live ? ShadowBakeInterval.EveryFrame : ShadowBakeInterval.OnStart;
            // One fresh capture on the way out, so the baked shadow never keeps a hero who has left the stage.
            if (!live) fireplaceLight.ScheduleShadowCapture();
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
            // A pick the player made gets the full answer; the hero the panel opens on arrives with the door instead.
            bool announce = !silentSetUp;
            // The burst lands on the click, sized from the hero still on stage, and covers the swap.
            if (announce) SpawnStageFx(heroPickFx, PickFxScale);
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

            if (announce) IAudioRequester.Instance.PlaySFX(SFXData.SelectHero);

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
            RefreshWarbandBlockers();

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
            if (announce)
            {
                commanderView.RevealHero();
                commanderView.CountTreasury();
            }
            startingArmySection.LoadUnitsOfRace(hero.Race);

            // The hero pick has its own sound; the difficulty that comes with it stays quiet.
            LoadDifficulty(SaveDataHandler.GetHeroLastDifficulty(_hero.HeroID), true);

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
            if (version != _heroPrefabLoadVersion)
            {
                // A newer hero took over during the load, so UnloadHeroes will never release this claim.
                if (prefab != null) AddressablesManager.Instance.Release(key);
                return;
            }
            _loadedHeroPrefabKey = key;
            heroObject = Instantiate(prefab, heroParent);

            //get the animator from the new gameobject and play the pop in animation
            Animator animator = heroObject.GetComponent<Animator>();
            if (animator != null)
                animator.Play("HeroPopIn");

            heroPopInFeedback.PlayFeedbacks();
        }

        // One-shot FX on the hero in the 3D tavern; it removes itself, so nothing needs to track it.
        private void SpawnStageFx(GameObject fxPrefab, float scale)
        {
            if (fxPrefab == null || heroParent == null) return;
            // The hero camera frames the hero from the knees up, so the FX sits at the body's centre, sized to the hero.
            // Not parented to the stage: its pop-in scale would shrink the FX with it.
            Bounds body = new Bounds(heroParent.position, Vector3.one * FxReferenceHeight);
            bool measured = false;
            int layer = heroParent.gameObject.layer;
            if (heroObject != null)
            {
                foreach (Renderer part in heroObject.GetComponentsInChildren<Renderer>())
                {
                    if (!measured) { body = part.bounds; measured = true; layer = part.gameObject.layer; }
                    else body.Encapsulate(part.bounds);
                }
            }
            GameObject fx = Instantiate(fxPrefab, body.center, Quaternion.identity);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fx, heroParent.gameObject.scene);
            // The hero camera renders only the tavern layers; the FX prefabs sit on Default and would never be drawn.
            foreach (Transform part in fx.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
            // Clamped, so a stray prop in the hero prefab cannot blow the FX up or shrink it away.
            fx.transform.localScale = Vector3.one * (scale * Mathf.Clamp(body.size.y / FxReferenceHeight, 0.25f, 3f));
            Destroy(fx, stageFxLifetime);
        }

        // The Epic Toon FX are sized for a figure about this tall, in world units.
        private const float FxReferenceHeight = 2f;
        // A pick is a medium moment, so its FX plays at a third of the send-off's size.
        private const float PickFxScale = 0.35f;

        // The hero answers the send-off; the tavern controllers hold a masculine and a feminine cheer.
        private void PlayHeroCheer()
        {
            if (heroObject == null) return;
            Animator animator = heroObject.GetComponent<Animator>();
            if (animator == null) return;
            bool masculine = HasParameter(animator, "isMasculine") && animator.GetBool("isMasculine");
            foreach (string state in new[] { masculine ? "Masc Cheer" : "Fem Cheer", "Cheer" })
            {
                if (!animator.HasState(0, Animator.StringToHash(state))) continue;
                animator.CrossFadeInFixedTime(state, 0.15f, 0);
                return;
            }
        }

        private static bool HasParameter(Animator animator, string name)
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.name == name) return true;
            return false;
        }

        public void SetActiveHero(Hero _hero)
        {
            hero = _hero;
            // The warband screen and the new-run save read the catalogue, so the hero's overrides go in here.
            TabletopTavernData.Instance.ApplyHeroConditionalOverrides(_hero.HeroID);
            OnActiveHeroChanged?.Invoke(hero);
            UpdateStartingGoldTooltip(_hero.StartingGold);
        }

        /// <summary>
        /// Fills the hero panel for the picked hero. It must stay free of side effects that accumulate.
        /// </summary>
        public void ShowHeroDetailsBox(Hero _hero)
        {
            // The signature unit's hover panel reads this squad.
            uniqueSquad = new SquadToLoad(_hero.SignatureUnit, _prestige: 0, _unitIndex: 0);
            commanderView.ShowHero(_hero, this);
            ShowTreasury(_hero);
        }

        /// <summary>
        /// The total is base + renown: an old readout printed the raw StartingGold with the bonus in
        /// brackets after it, so the number the player read was never the number they had to spend.
        /// </summary>
        private void ShowTreasury(Hero _hero)
        {
            int renownBonus = startingArmySection.StartingGoldBonusFromMetaprogression;
            commanderView.ShowTreasury(_hero.StartingGold + renownBonus, _hero.StartingGold, renownBonus);
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
            foreach (WarbandArmyTile tile in startingUnitsParent.GetComponentsInChildren<WarbandArmyTile>()) {
                Destroy(tile.gameObject);
            }
        }

        public void ReloadHeroOnDiscordUnlock()
        {
            commanderView.Tiles[4].Load(HeroData.BjornIronskull, this);
            LoadHeroes(HeroData.BjornIronskull);
        }

        public void ReloadHeroOnNewsletterUnlock()
        {
            commanderView.Tiles[5].Load(HeroData.FreyjaStormweaver, this);
            LoadHeroes(HeroData.FreyjaStormweaver);
        }
        #endregion

        #region Difficulty
        public void IncreaseDifficulty()
        {
            if(!DifficultyRules.IsHardest(_difficultySelected))
            {
                LoadDifficulty(DifficultyRules.Next(_difficultySelected));
            }
        }
        public void DecreaseDifficulty()
        {
            if(DifficultyRules.Rank(_difficultySelected) > 0)
            {
                LoadDifficulty(DifficultyRules.Previous(_difficultySelected));
            }
        }
        public void LoadDifficulty(TT_Difficulty _selectedDifficulty, bool silent = false)
        {
            // A saved last difficulty can come from the other ladder.
            _difficultySelected = DifficultyRules.Normalize(_selectedDifficulty);
            if (!silent) IAudioRequester.Instance.PlaySFX(SFXData.ChangeDifficulty);

            //get selected difficulty data
            DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(_difficultySelected);

            //set title
            string difficultyTitleLocalized = LocalizationManager.Instance.GetText("Difficulty");
            string difficultyNamestring = LocalizationManager.Instance.GetText(difficultyData.difficultyName);
            _difficultyTitle.text = difficultyNamestring;

            //set description, one line per modifier this level adds
            List<string> levelModifierLines = new List<string>();
            foreach (int modifier in difficultyData.modifiers)
            {
                levelModifierLines.Add(LocalizationManager.Instance.GetText(DifficultyData.ModifierKey(modifier)));
            }
            // Easy adds no modifiers, so it names itself the base difficulty instead of leaving the box empty.
            difficultyDescriptionText.text = levelModifierLines.Count > 0
                ? string.Join("\n", levelModifierLines)
                : LocalizationManager.Instance.GetText("difficultyModifier0");

            List<string> allPreviousModifiers = DifficultyData.GetAllDifficultyModifiersBeforeLevel(_difficultySelected);
            extraInfo.SetActive(allPreviousModifiers.Count > 0);

            //set button text on right side
            if(difficultyButtonText != null)
            {
                difficultyButtonText.text = $"<color {ColorData.Secondary}>{difficultyTitleLocalized}:</color> <color {ColorData.Tier4}>{difficultyNamestring}</color>";
            }

            //disable/enable increase decrease buttons
            increaseDifficultyButton.gameObject.SetActive(!DifficultyRules.IsHardest(_difficultySelected));
            decreaseDifficultyButton.gameObject.SetActive(DifficultyRules.Rank(_difficultySelected) > 0);

            //display difficulty crests
            for (int i = 0; i < difficultyCrests.Length; i++)
                difficultyCrests[i].SetActive(i == difficultyData.crestIndex);
            crestSpawnFeedback.StopFeedbacks();
            crestSpawnFeedback.PlayFeedbacks();
            ShowDifficultyFeel(levelModifierLines.Count, silent);

            string additionalModifiersDesc = "";

            foreach (string modifier in allPreviousModifiers)
            {
                additionalModifiersDesc += "- " + LocalizationManager.Instance.GetText(modifier) + "\n";
            }
            string additionalModifiersTitleLocalized = LocalizationManager.Instance.GetText("Additional Modifiers");
            additionalDifficultyInfoTooltipTrigger.SetUpToolTip(
                additionalModifiersTitleLocalized, additionalModifiersDesc);

            // A locked level blocks Start, so the validation strip has to re-check on every change.
            warbandPanel.RefreshValidation();
        }

        private Coroutine difficultyFeel;
        private CanvasGroup crestGroup;
        private const float LockedCrestAlpha = 0.45f;

        // A locked level dims its crest; a chosen level deals its modifier lines in.
        private void ShowDifficultyFeel(int lineCount, bool silent)
        {
            if (crestGroup == null)
            {
                Transform crests = crestSpawnFeedback.transform.parent;
                crestGroup = crests.GetComponent<CanvasGroup>();
                if (crestGroup == null) crestGroup = crests.gameObject.AddComponent<CanvasGroup>();
            }
            bool locked = DifficultyRules.IsLocked(_difficultySelected, SaveDataHandler.LoadPlayerSaveData().MaxDifficultyOverall);
            crestGroup.alpha = locked ? LockedCrestAlpha : 1f;

            if (difficultyFeel != null) StopCoroutine(difficultyFeel);
            difficultyDescriptionText.maxVisibleLines = 99;
            if (silent || !isActiveAndEnabled) return;
            difficultyFeel = StartCoroutine(DifficultyFeelRoutine(lineCount));
        }

        private IEnumerator DifficultyFeelRoutine(int lineCount)
        {
            for (int line = 1; line <= lineCount; line++)
            {
                difficultyDescriptionText.maxVisibleLines = line;
                yield return new WaitForSecondsRealtime(0.04f);
            }
            difficultyDescriptionText.maxVisibleLines = 99;
            difficultyFeel = null;
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
        // The flare is seen on the button first, then the panels fade and leave the hero alone before the door closes.
        private const float SendOffFlareTime = 0.2f;
        private const float SendOffFadeTime = 0.35f;
        private const float SendOffHoldTime = 0.25f;
        private Coroutine sendOff;

        public void OnStartButtonClicked()
        {
            // A second click during the send-off skips the rest of it.
            if (sendOff != null)
            {
                StopCoroutine(sendOff);
                sendOff = null;
                BeginCampaign();
                return;
            }

            // The validation strip already collects every blocker and drives startButton.interactable,
            // so a blocked run cannot reach here through the button. Re-checked anyway because
            // OnStartButtonClicked is public and the old flow relied on ordered activeSelf checks.
            warbandPanel.RefreshValidation();
            if (!warbandPanel.Validation.CanStart)
            {
                NotificationManager.Instance.ErrorNotification(warbandPanel.Validation.Blockers[0]);
                return;
            }

            sendOff = StartCoroutine(SendOff());
        }

        // The run's one big moment: the war horn and the hero's battle theme, gold flare, the hero cheers, the panels fade, then the door closes.
        private IEnumerator SendOff()
        {
            yield return SendOffFeel();
            sendOff = null;
            BeginCampaign();
        }

        private IEnumerator SendOffFeel()
        {
            IAudioRequester.Instance.PlaySFX(SFXData.BattleHorn);
            IAudioRequester.Instance.PlayBattleTheme((int)hero.Race);
            warbandPanel.PlaySendOff();
            PlayHeroCheer();
            SpawnStageFx(sendOffFx, 1f);
            yield return new WaitForSecondsRealtime(SendOffFlareTime);
            // Raycasts stay on, so a second click on Start can still skip the rest.
            yield return UIJuice.Close(warbandScreen.GetComponent<CanvasGroup>(), SendOffFadeTime);
            yield return new WaitForSecondsRealtime(SendOffHoldTime);
        }

        private void BeginCampaign()
        {
            startButton.interactable = false;

            Guid runUUID = Guid.NewGuid();
            SaveDataHandler.CreateCampaign(hero, startingArmySection.SelectedArmy, SelectedDifficulty,
                                           startingGearID, runUUID, startingArmySection.remainingTreasury.Value,
                                           warbandPanel.Loadout, GameEventTracker.TryBuild("runStarted", () => BuildRunSetup()));
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.campaignsStarted++;
            SaveDataHandler.SavePlayerSaveData(saveData);

            mainMenu.LoadMapScene();
        }
        // What the setup screen offered and what the player changed, for runStarted.
        private AnalyticsRunSetup BuildRunSetup()
        {
            var setup = new AnalyticsRunSetup
            {
                Source = "menu",
                FromMenu = true,
                ArmyLocked = startingArmyLockedForHero,
                ArmyCustomized = !SameUnits(startingArmySection.SelectedArmy, hero.StartingArmyUnits),
                TreasuryBase = startingArmySection.StartingGold - startingArmySection.StartingGoldBonusFromMetaprogression,
                TreasuryRenownBonus = startingArmySection.StartingGoldBonusFromMetaprogression,
                ArmySpend = startingArmySection.ArmyGoldSpend,
                GearSpend = startingArmySection.GearGoldSpend,
            };
            foreach (UnitName unit in startingArmySection.OfferedUnits) setup.ArmyOptions.Add(unit.ToString());
            return setup;
        }
        private static bool SameUnits(SquadToLoad[] army, UnitName[] defaults)
        {
            List<UnitName> chosen = new();
            foreach (SquadToLoad squad in army) chosen.Add(squad.UnitName);
            List<UnitName> original = new(defaults ?? Array.Empty<UnitName>());
            if (chosen.Count != original.Count) return false;
            chosen.Sort();
            original.Sort();
            for (int i = 0; i < chosen.Count; i++)
                if (chosen[i] != original[i]) return false;
            return true;
        }
        #endregion

        public void OnDestroy()
        {
            if(startingArmySection != null)
            {
                startingArmySection.OnStartingArmyLengthChanged -= StartingArmyLengthChanged;
            }
            SetFireplaceShadowsLive(false);
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
