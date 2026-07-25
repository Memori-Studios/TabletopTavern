using Memori.SaveData;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

namespace TJ.MainMenu
{
    public class PlayPanel : MainMenuPanel
    {
        [Header("Main Buttons")]
        [SerializeField] private PlayPanelButton changeHeroButton;
        [SerializeField] private PlayPanelButton difficultyButton, startingGearButton, startingArmyButton, startButton;

        [Header("Button Text")]
        [SerializeField] private TMP_Text difficultyButtonText;
        [SerializeField] private TMP_Text startingGearButtonText, heroButtonText;

        [Header("Main Panels")]
        [SerializeField] private GameObject totalPanel;
        [SerializeField] private GameObject difficultyPanel, startingGearPanel, startingArmyPanel, heroDetailsPanel;
        [SerializeField] private PlayPanelExitArea playPanelExitArea;

        [Header("Hero Info")]
        [SerializeField] private Transform heroParent;
        [SerializeField] private TMP_Text heroNameText, heroGoldText, heroBonusText1, heroBonusText2;        

        [Header("Locked Buttons")]
        [SerializeField] private LockedButton lockedForHeroReasonButton;
        [SerializeField] private LockedButton lockedDifficultyStartButton;
        [SerializeField] private LockedButton lockedInsufficientFundsButton;
        [SerializeField] private LockedButton lockStartingArmyButton;
        [SerializeField] private LockedButton oneUnitRequiredButton;
        [SerializeField] private LockedButton startingGearLockedButton;

        [Header("Camera Scene")]
        [SerializeField] private Camera _mainMenuCamera;
        [SerializeField] private Camera heroCamera;
        [SerializeField] private Transform cameraSceneParent;

        [Header("Hero Selection")]
        [SerializeField] private HeroDifficultyButton[] heroSelectionButtons;
        [SerializeField] private MMF_Player heroPopInFeedback;
        [SerializeField] private DiscordUnlock discordUnlock;
        [SerializeField] private NewsletterUnlock newsletterUnlock;
        [SerializeField] private MemoriTooltipTrigger heroTooltipTrigger;
        [SerializeField] private TMP_Text signatureUnitNameText;
        [SerializeField] private SquadDisplayCardMenu uniqueUnitDisplayCardMenu;
        public event Action<Hero> OnActiveHeroChanged;
        [SerializeField] private MemoriTooltipTrigger startingGoldTooltipTrigger;

        [Header("Difficulty")]
        [SerializeField] private TT_Difficulty _difficultySelected;
        [SerializeField] private GameObject extraInfo;
        [SerializeField] private TMP_Text _difficultyTitle, difficultyDescriptionText;
        [SerializeField] private Button increaseDifficultyButton, decreaseDifficultyButton;
        [SerializeField] private GameObject[] difficultyCrests;
        [SerializeField] private MMF_Player crestSpawnFeedback;
        [SerializeField] private MemoriTooltipTrigger additionalDifficultyInfoTooltipTrigger;

        [Header("Starting Gear")]
        [SerializeField] private StartingArmyManager startingArmySection;
        public StartingArmyManager StartingArmySection => startingArmySection;
        [SerializeField] private Transform startingUnitsParent;
        [SerializeField] private MetaprogressionModel _startingArmyUnlockMetaprogressionModel;

        [Header("Faction Section")]
        [SerializeField] private TMP_Text raceTitleText;        
        [SerializeField] private TMP_Text raceCampaignBonusText, raceBattleBonusText;        
        
        private PlayPanelButton _activePlayPanelButton;
        public Hero hero;
        public SquadToLoad uniqueSquad;
        public GearID StartingGearID => startingGearID;
        private bool startingArmyLockedForHero;
        public bool StartingArmyLockedForHero => startingArmyLockedForHero;

        GearID startingGearID;
        GameObject heroObject;
        string _loadedHeroPrefabKey;
        bool heroIsUnlocked;
        int _heroPrefabLoadVersion;
        int _maxDifficultyCompletedOverall = 0;
        private UnlockCondition _unlockCondition;

        public override void SetUp(MainMenu _mainMenu)
        {
            base.SetUp(_mainMenu);

            startButton.Button.onClick.RemoveAllListeners();
            startButton.Button.onClick.AddListener(OnStartButtonClicked);
            startingGearButton.SetUp(this, startingGearPanel);
            startingArmyButton.SetUp(this, startingArmyPanel);
            startButton.SetUp(this, null);
            totalPanel.SetActive(false);

            increaseDifficultyButton.onClick.AddListener(IncreaseDifficulty);
            decreaseDifficultyButton.onClick.AddListener(DecreaseDifficulty);

            startingArmySection.OnStartingArmyLengthChanged += StartingArmyLengthChanged;

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

            for (int i = 0; i < heroSelectionButtons.Length; i++)
            {
                heroSelectionButtons[i].LoadHeroSelectionPage(HeroData.Heroes[i], this);
            }

            EventSystem.current.SetSelectedGameObject(heroSelectionButtons[0].gameObject);

            LoadHeroes(HeroData.EdricValeward);

            bool startingGearLocked = !SaveDataHandler.IsMetaprogressionNodeUnlocked(_startingArmyUnlockMetaprogressionModel);
            startingGearLockedButton.SetLockedState(startingGearLocked, LocalizationManager.Instance.GetText("Unlocked in Metaprogression Tree"));

            startingGearPanel.SetActive(false);
            startingArmyPanel.SetActive(false);
            heroDetailsPanel.SetActive(true);
            difficultyPanel.SetActive(true);
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

            lockedForHeroReasonButton.SetLockedState(!heroIsUnlocked, HeroBonusManager.GetLocalizedHeroUnlockDescription(_hero, _unlockCondition));

            LoadHeroPrefab();
            
            IAudioRequester.Instance.PlaySFX(SFXData.SelectHero);

            List<int> maxDifficultyComletedOnHero = SaveDataHandler.GetHeroDifficultiesCompleted(hero.HeroID);

            //UnitNotDiscovered
            startingArmyLockedForHero = !heroIsUnlocked || maxDifficultyComletedOnHero.Count == 0;
            lockStartingArmyButton.SetLockedState(startingArmyLockedForHero, LocalizationManager.Instance.GetText("OneCompletionRequired"));

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
            string leaderLocalized = LocalizationManager.Instance.GetText("Leader");
            string heroNameLocalized = LocalizationManager.Instance.GetText(_hero.HeroName);
            heroButtonText.text = $"<color {ColorData.Secondary}>{leaderLocalized}:</color> <color {ColorData.Legendary}>{heroNameLocalized}</color>";
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
        public void ShowHeroDetailsBox(Hero _hero)
        {
            string leaderLocalized = LocalizationManager.Instance.GetText("Leader");
            string heroNameLocalized = LocalizationManager.Instance.GetText(_hero.HeroName);
            heroNameText.text = $"<color {ColorData.Secondary}>{leaderLocalized}:</color> <color {ColorData.Primary}>{heroNameLocalized}</color>";
            
            string heroBonusText1string = LocalizationManager.Instance.GetText(_hero.HeroBonusDescription[0].Replace("heroBonusDescription", "heroBonusTitle")) + ": " + LocalizationManager.Instance.GetText(_hero.HeroBonusDescription[0]);
            string heroBonusText2string = LocalizationManager.Instance.GetText(_hero.HeroBonusDescription[1].Replace("heroBonusDescription", "heroBonusTitle")) + ": " + LocalizationManager.Instance.GetText(_hero.HeroBonusDescription[1]);
            ColorData.XMLTagColorApplicator(ref heroBonusText1string);
            ColorData.XMLTagColorApplicator(ref heroBonusText2string);
            heroBonusText1string = ApplyPrimaryColorToLabel(heroBonusText1string);
            heroBonusText2string = ApplyPrimaryColorToLabel(heroBonusText2string);
            heroBonusText1.text = heroBonusText1string;
            heroBonusText2.text = heroBonusText2string;
            
            //faction
            string factionLocalized = LocalizationManager.Instance.GetText("Faction");
            string factionNameLocalized = LocalizationManager.Instance.GetText(_hero.Race.ToString());
            raceTitleText.text = $"{factionLocalized}: {factionNameLocalized}";

            string campaignBonusLocalized = LocalizationManager.Instance.GetText(_hero.Race + "BonusDescription");
            ColorData.XMLTagColorApplicator(ref campaignBonusLocalized);
            campaignBonusLocalized = ApplyPrimaryColorToLabel(campaignBonusLocalized);
            raceCampaignBonusText.text = campaignBonusLocalized;

            string battleBonusLocalized = $"{LocalizationManager.Instance.GetText(_hero.Race + "PassiveName")}: {LocalizationManager.Instance.GetText(_hero.Race + "PassiveDescription")}";
            ColorData.XMLTagColorApplicator(ref battleBonusLocalized);
            battleBonusLocalized = ApplyPrimaryColorToLabel(battleBonusLocalized);
            raceBattleBonusText.text = battleBonusLocalized;

            string treasuryLocalized = LocalizationManager.Instance.GetText("Treasury");
            string bonusString = startingArmySection.StartingGoldBonusFromMetaprogression > 0 ? $" <color={ColorData.Green}>(+{startingArmySection.StartingGoldBonusFromMetaprogression})</color>" : "";
            heroGoldText.text = $"{treasuryLocalized}:    {_hero.StartingGold}{bonusString} <sprite name=GoldSprite>";

            uniqueSquad = new SquadToLoad(
                _hero.SignatureUnit, 
                _prestige: 0, 
                _unitIndex: 0
            );
            uniqueUnitDisplayCardMenu.SetUp(uniqueSquad, false, _isEnemy: true);
            uniqueUnitDisplayCardMenu.LockCard(true);
            uniqueUnitDisplayCardMenu.gameObject.AddComponent<TroopHoverPlayPanel>().SetUp(99, this);
            string titleLocalized = LocalizationManager.Instance.GetText("SignatureUnit");
            signatureUnitNameText.text = titleLocalized + ":\n" +            
             LocalizationManager.Instance.GetText(uniqueSquad.UnitName.ToString());

            string heroDescription = LocalizationManager.Instance.GetText(_hero.HeroDescription);
            
            heroTooltipTrigger.SetUpToolTip($"{heroNameLocalized}", $"{heroDescription}");
            heroDetailsPanel.SetActive(true);
        }
        public void UnloadHeroes()
        {
            if(heroObject != null) {
                Destroy(heroObject);
                heroObject = null;
            }

            // Release the previous hero prefab's handle. It was loaded persistent
            // (pinned), so ReleaseAll() won't reclaim it — release it here, coupled
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
        public void OnStartButtonClicked()
        {
            if (!startButton.Button.interactable) return;
            startButton.Button.interactable = false;

            if(!heroIsUnlocked) {
                NotificationManager.Instance.ErrorNotification(HeroBonusManager.GetLocalizedHeroUnlockDescription(hero, _unlockCondition));
                startButton.Button.interactable = true;
                return;
            }

            //check if any locked buttons are active
            if(lockedInsufficientFundsButton.gameObject.activeSelf)
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("InsufficientGoldError"));
                startButton.Button.interactable = true;
                return;
            }
            else if(lockedDifficultyStartButton.gameObject.activeSelf)
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("Difficulty Locked"));
                startButton.Button.interactable = true;
                return;
            }
            else if(oneUnitRequiredButton.gameObject.activeSelf)
            {
                NotificationManager.Instance.ErrorNotification(LocalizationManager.Instance.GetText("OneUnitRequiredError"));
                startButton.Button.interactable = true;
                return;
            }

            Guid runUUID = Guid.NewGuid();
            SaveDataHandler.CreateCampaign(hero, startingArmySection.SelectedArmy, _difficultySelected, startingGearID, runUUID, startingArmySection.remainingTreasury.Value);
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.campaignsStarted++;
            SaveDataHandler.SavePlayerSaveData(saveData);

            mainMenu.LoadMapScene();
        }
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
            // Debug.Log($"Loading difficulty: {_difficultySelected}");
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
            difficultyButtonText.text = $"<color {ColorData.Secondary}>{difficultyTitleLocalized}:</color> <color {ColorData.Legendary}>{levelLocalized} {(int)_difficultySelected} {difficultyNamestring}</color>";

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

            //update hero buttons
            // for (int i = 0; i < heroDifficultyButtons.Length; i++)
            // {
            //     heroDifficultyButtons[i].CheckDifficultyStatus((int)_difficultySelected);
            // }

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
        }
        public void ReloadHeroOnDiscordUnlock()
        {
            // heroDifficultyButtons[4].LoadHeroDifficultyPage(HeroData.BjornIronskull, this);
            heroSelectionButtons[4].LoadHeroSelectionPage(HeroData.BjornIronskull, this);
            LoadHeroes(HeroData.BjornIronskull);
        }
        public void ReloadHeroOnNewsletterUnlock()
        {
            // heroDifficultyButtons[5].LoadHeroDifficultyPage(HeroData.FreyjaStormweaver, this);
            heroSelectionButtons[5].LoadHeroSelectionPage(HeroData.FreyjaStormweaver, this);
            LoadHeroes(HeroData.FreyjaStormweaver);
        }
        public void SetStartingGear(GearID _gearID)
        {
            startingGearID = _gearID;
            // Debug.Log($"Starting gear set to: {startingGearID}");
            string startingGearLocalized = LocalizationManager.Instance.GetText("Starting Gear");
            string gearNameLocalized = LocalizationManager.Instance.GetText(startingGearID+"Name");
            startingGearButtonText.text = $"<color {ColorData.Secondary}>{startingGearLocalized}:</color> <color {ColorData.Legendary}>{gearNameLocalized}</color>";
        }
        public void RemainingTreasuryChanged(int _remainingTreasury)
        {
            lockedInsufficientFundsButton.SetLockedState(_remainingTreasury < 0, LocalizationManager.Instance.GetText("InsufficientGoldError"));
        }
        public void OnDestroy()
        {
            if(startingArmySection != null)            {
            startingArmySection.OnStartingArmyLengthChanged -= StartingArmyLengthChanged;
            }
        }
        public void StartingArmyLengthChanged(int newLength)
        {
            oneUnitRequiredButton.SetLockedState(newLength == 0, LocalizationManager.Instance.GetText("OneUnitRequiredError"));
        }
        public void OnPlayPanelButtonHover(PlayPanelButton playPanelButton)
        {
            if(_activePlayPanelButton == playPanelButton) return;

            if(_activePlayPanelButton != null)
            {
                _activePlayPanelButton.ShutDown();
            }

            _activePlayPanelButton = playPanelButton;
            playPanelExitArea.gameObject.SetActive(_activePlayPanelButton != null);
        }
        public void CloseActiveButton()
        {
            if(_activePlayPanelButton == null) return;
            _activePlayPanelButton.ShutDown();
            _activePlayPanelButton = null;
            playPanelExitArea.gameObject.SetActive(false);
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        }
        private string ApplyPrimaryColorToLabel(string text)
        {
            int colonIndex = text.IndexOf(':');
            if (colonIndex < 0) return text;
            return $"<color {ColorData.Primary}>{text[..(colonIndex + 1)]}</color>{text[(colonIndex + 1)..]}";
        }
        private void UpdateStartingGoldTooltip(int startingGoldTreasury)
        {
            string titleLocalized = LocalizationManager.Instance.GetText("startingGold");
            string desc1Localized = LocalizationManager.Instance.GetText("startingGoldDesc1");
            string desc2Localized = LocalizationManager.Instance.GetText("startingGoldDesc2");
            string bonusString = startingArmySection.StartingGoldBonusFromMetaprogression > 0 ? $" <color={ColorData.Green}>(+{startingArmySection.StartingGoldBonusFromMetaprogression})</color>" : "";
            string fullDescription = $"{desc1Localized} <color={ColorData.Gold}>[{startingGoldTreasury}]</color>{bonusString} {desc2Localized}";

            startingGoldTooltipTrigger.SetUpToolTip(titleLocalized, fullDescription);
        }
    }
}
