using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Memori.Audio;
using Memori.Localization;
using Memori.SaveData;
using Memori.Tooltip;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Collection codex: a left rail (Armoury, Factions), a page header, and one layout on every page with
    /// the items or the 3D stage in the middle and a fixed detail panel on the right. Hovering previews an
    /// entry; clicking keeps it. Built by CollectionCodexBuilder; lives in the Collection overlay scene.
    /// </summary>
    [RequireComponent(typeof(MemoriCanvasGroup))]
    public class CollectionPanel : MonoBehaviour
    {
        private enum View { Gear, Potions, Faction }
        private enum FactionTab { Units, Heroes, Lore }

        [Header("Frame")]
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text closeLabel;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private RectTransform progressFill;

        [Header("Rail")]
        [SerializeField] private Transform railContainer;
        [SerializeField] private TMP_Text railSectionTemplate;
        [SerializeField] private CollectionRailRow railRowTemplate;
        [SerializeField] private Sprite gearRailIcon;
        [SerializeField] private Sprite potionRailIcon;

        [Header("Page header")]
        [SerializeField] private Image headerMarker;
        [SerializeField] private TMP_Text headerTitle;
        [SerializeField] private TMP_Text headerSubtitle;
        [SerializeField] private GameObject tabsRoot;
        [SerializeField] private CollectionTab unitsTab;
        [SerializeField] private CollectionTab heroesTab;
        [SerializeField] private CollectionTab loreTab;
        [SerializeField] private GameObject effectsRoot;
        [SerializeField] private TMP_Text battleEffectText;
        [SerializeField] private MemoriTooltipTrigger battleEffectTooltip;
        [SerializeField] private TMP_Text campaignEffectText;
        [SerializeField] private MemoriTooltipTrigger campaignEffectTooltip;

        [Header("Grid")]
        [SerializeField] private GameObject gridRoot;
        [SerializeField] private ScrollRect gridScroll;
        [SerializeField] private Transform gearGroups;
        [SerializeField] private Transform potionGroups;
        [SerializeField] private CollectionGroupHeader groupHeaderTemplate;
        [SerializeField] private RectTransform groupGridTemplate;
        [SerializeField] private CollectionTile itemTileTemplate;

        [Header("Stage")]
        [SerializeField] private GameObject stageRoot;
        [SerializeField] private CollectionStageInput stageInput;
        [SerializeField] private TMP_Text stageHint;
        [SerializeField] private GameObject rosterRoot;
        [SerializeField] private Transform rosterGrid;
        [SerializeField] private CollectionTile unitTileTemplate;
        [SerializeField] private GameObject heroTabsRoot;
        [SerializeField] private CollectionTab[] heroTabs;
        [SerializeField] private GameObject heroLoreRoot;
        [SerializeField] private ScrollRect heroLoreScroll;
        [SerializeField] private TMP_Text heroLoreText;

        [Header("Lore")]
        [SerializeField] private GameObject loreRoot;
        [SerializeField] private ScrollRect loreScroll;
        [SerializeField] private TMP_Text loreText;

        [Header("Detail")]
        [SerializeField] private CollectionDetailPanel detail;
        [SerializeField] private CanvasGroup contentGroup;
        [SerializeField] private float fadeTime = 0.15f;
        [SerializeField] private float previewDelay = 0.12f;
        [SerializeField] private float revertDelay = 0.2f;

        private class Faction
        {
            public Race Race;
            public UnitName[] Units;
            public Hero[] Heroes;
            public CollectionRailRow Row;
            public readonly List<CollectionTile> Tiles = new();
            public bool Built;
            public int Pinned = -1;
            public int HeroIndex;
        }

        private struct ItemInfo
        {
            public int Rarity;
            public string RarityName;
            public Color Colour;
            public Sprite Icon;
            public bool Found;
            public bool Seen;
        }

        private MemoriCanvasGroup _canvasGroup;
        private CollectionPreviewRig _rig;
        private Faction[] _factions;
        private GearID[] _gear;
        private ConsumableEnum[] _potions;
        private readonly List<CollectionTile> _gearTiles = new();
        private readonly List<CollectionTile> _potionTiles = new();
        private CollectionRailRow _gearRow, _potionRow;
        // -1 until the page is first opened, which then picks the first entry the player owns.
        private int _gearPinned = -1, _potionPinned = -1;

        private HashSet<int> _gearFound, _gearSeen, _potionFound, _potionSeen;
        private HashSet<UnitName> _unitFound, _unitSeen;

        private View _view;
        private FactionTab _tab;
        private Faction _faction;
        private CollectionTile _hovered;
        private Coroutine _pending;
        private Coroutine _fade;

        private static string T(string key) => LocalizationManager.Instance.GetText(key);

        private void Awake()
        {
            _canvasGroup = GetComponent<MemoriCanvasGroup>();
        }

        #region Lifecycle

        public void SetUp(Action onClose, CollectionPreviewRig rig)
        {
            _rig = rig;
            stageInput.Rig = rig;

            if (closeButton == null)
                Debug.LogError("[CollectionPanel] closeButton is not assigned - the panel cannot be closed.");
            else
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(() => onClose());
            }

            ReadSave();
            BuildData();
            BuildRail();
            WireTabs();
            Localize();
            detail.Localize();
            RefreshCounts();

            StartCoroutine(EvaluateCollectionAchievements());
        }

        public void OpenPanel()
        {
            _canvasGroup.CGEnable();
            // Selection stays empty: automatic UI navigation would wander onto the menu behind the overlay.
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            ShowGear();
        }

        public void ClosePanel()
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            _canvasGroup.CGDisable();
            StopPending();
            if (_rig != null) _rig.Clear();
        }

        /// <summary>Kept for CollectionGearCard, which the Warband gear list shares.</summary>
        public void UpdateAcknowledged() => RefreshNewDots();

        /// <summary>
        /// Collection achievements are unrelated to drawing the panel, and each evaluator re-reads the save
        /// and re-allocates its roster, so they run after the first frame, one faction per frame.
        /// </summary>
        private IEnumerator EvaluateCollectionAchievements()
        {
            yield return null;

            SaveDataHandler.EvaluateGearCollection();
            SaveDataHandler.EvaluateConsumableCollection();

            foreach (Faction faction in _factions)
            {
                SaveDataHandler.EvaluateRaceCollection(faction.Race);
                yield return null;
            }
        }

        #endregion

        #region Build

        private void ReadSave()
        {
            _gearFound = new HashSet<int>(SaveDataHandler.GetGearIDsCollected());
            _gearSeen = new HashSet<int>(SaveDataHandler.GetGearIDsAcknowledged());
            _potionFound = new HashSet<int>(SaveDataHandler.GetPotionsIDsCollected());
            _potionSeen = new HashSet<int>(SaveDataHandler.GetPotionsIDsAcknowledged());
            _unitFound = new HashSet<UnitName>(SaveDataHandler.GetTroopsIDsCollected());
            _unitSeen = new HashSet<UnitName>(SaveDataHandler.GetTroopsIDsAcknowledged());
        }

        private void BuildData()
        {
            // Rarest first. OrderByDescending is stable, so each rarity keeps its authored order.
            _gear = GearData.GetGearIDs().OrderByDescending(g => GearData.GetGear(g).GearRarity).ToArray();
            _potions = ConsumableData.GetAllConsumableEnums().OrderByDescending(p => ConsumableData.GetConsumable(p).ConsumableRarity).ToArray();

            Hero[] heroes = HeroData.Heroes;
            _factions = Enum.GetValues(typeof(Race)).Cast<Race>()
                .Where(race => race != Race.Special)
                .Select(race => new Faction
                {
                    Race = race,
                    Units = TabletopTavernData.Instance.GetUnitsOfRace(race),
                    Heroes = heroes.Where(hero => hero.Race == race).OrderBy(hero => hero.HeroID).ToArray(),
                })
                .ToArray();
        }

        private void BuildRail()
        {
            Section("CollectionArmory");
            _gearRow = Row(T("CollectionGear"), gearRailIcon, Color.white, false, ShowGear);
            _potionRow = Row(T("CollectionPotions"), potionRailIcon, Color.white, false, ShowPotions);
            Section("CollectionFactions");
            foreach (Faction faction in _factions)
            {
                Faction target = faction;
                faction.Row = Row(T(faction.Race.ToString()), null, ColorData.GetRaceDisplayColor(faction.Race), true,
                    () => ShowFaction(target, FactionTab.Units));
            }
        }

        private void Section(string key)
        {
            TMP_Text section = Instantiate(railSectionTemplate, railContainer);
            section.gameObject.SetActive(true);
            section.text = T(key);
        }

        private CollectionRailRow Row(string label, Sprite icon, Color colour, bool diamond, Action onClick)
        {
            CollectionRailRow row = Instantiate(railRowTemplate, railContainer);
            row.gameObject.SetActive(true);
            row.SetUp(label, icon, colour, diamond);
            row.Button.onClick.AddListener(() =>
            {
                IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
                onClick();
            });
            return row;
        }

        private void WireTabs()
        {
            unitsTab.Button.onClick.AddListener(() => SwitchTab(FactionTab.Units));
            heroesTab.Button.onClick.AddListener(() => SwitchTab(FactionTab.Heroes));
            loreTab.Button.onClick.AddListener(() => SwitchTab(FactionTab.Lore));
            for (int i = 0; i < heroTabs.Length; i++)
            {
                int index = i;
                heroTabs[i].Button.onClick.AddListener(() =>
                {
                    IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
                    ShowHero(index);
                });
            }
        }

        private void SwitchTab(FactionTab tab)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            if (_faction != null) ShowFaction(_faction, tab);
        }

        private void FadeContent()
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Fade());
        }

        private IEnumerator Fade()
        {
            // Unscaled: the Collection opens over a battle that Settings may have paused.
            for (float t = 0f; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                contentGroup.alpha = Mathf.SmoothStep(0.3f, 1f, t / fadeTime);
                yield return null;
            }
            contentGroup.alpha = 1f;
            _fade = null;
        }

        private void Localize()
        {
            titleText.text = T("collectionButton");
            closeLabel.text = T("Close");
            unitsTab.SetLabel(T("Units"));
            heroesTab.SetLabel(T("Heroes"));
            loreTab.SetLabel(T("Lore"));
            stageHint.text = T("CollectionDragToTurn");
        }

        private void BuildItemGroups(Transform container, List<ItemInfo> items, List<CollectionTile> tiles)
        {
            int i = 0;
            while (i < items.Count)
            {
                int start = i;
                int rarity = items[i].Rarity;
                while (i < items.Count && items[i].Rarity == rarity) i++;

                int found = 0;
                for (int j = start; j < i; j++) if (items[j].Found) found++;

                CollectionGroupHeader header = Instantiate(groupHeaderTemplate, container);
                header.gameObject.SetActive(true);
                header.Set(items[start].Colour, items[start].RarityName, FoundCount(found, i - start));

                RectTransform grid = Instantiate(groupGridTemplate, container);
                grid.gameObject.SetActive(true);
                for (int j = start; j < i; j++)
                {
                    CollectionTile tile = Instantiate(itemTileTemplate, grid);
                    tile.gameObject.SetActive(true);
                    tile.Index = j;
                    tile.SetItem(items[j].Icon, items[j].Colour, items[j].Found, items[j].Found && !items[j].Seen);
                    Hook(tile);
                    tiles.Add(tile);
                }
            }
        }

        private void EnsureGearBuilt()
        {
            if (_gearTiles.Count > 0) return;
            var items = new List<ItemInfo>();
            foreach (GearID id in _gear)
            {
                Gear gear = GearData.GetGear(id);
                items.Add(new ItemInfo
                {
                    Rarity = (int)gear.GearRarity,
                    RarityName = T(gear.GearRarity.ToString()),
                    Colour = (Color)ColorData.GetGearRarityColor(gear.GearRarity),
                    Icon = SpriteData.GetSprite(gear.GearName),
                    Found = _gearFound.Contains((int)id),
                    Seen = _gearSeen.Contains((int)id),
                });
            }
            BuildItemGroups(gearGroups, items, _gearTiles);
        }

        private void EnsurePotionsBuilt()
        {
            if (_potionTiles.Count > 0) return;
            var items = new List<ItemInfo>();
            foreach (ConsumableEnum id in _potions)
            {
                ConsumableRarity rarity = ConsumableData.GetConsumable(id).ConsumableRarity;
                items.Add(new ItemInfo
                {
                    Rarity = (int)rarity,
                    RarityName = T(rarity.ToString()),
                    Colour = (Color)ColorData.GetRarityTierColor((UnitRarity)(int)rarity),
                    Icon = SpriteData.GetSprite(id.ToString()),
                    Found = _potionFound.Contains((int)id),
                    Seen = _potionSeen.Contains((int)id),
                });
            }
            BuildItemGroups(potionGroups, items, _potionTiles);
        }

        private void EnsureUnitsBuilt(Faction faction)
        {
            if (faction.Built) return;
            faction.Built = true;
            for (int i = 0; i < faction.Units.Length; i++)
            {
                UnitName unit = faction.Units[i];
                CollectionTile tile = Instantiate(unitTileTemplate, rosterGrid);
                tile.gameObject.SetActive(true);
                tile.Index = i;
                bool found = _unitFound.Contains(unit);
                tile.SetUnit(TabletopTavernData.Instance.GetUnitIcon(unit), TabletopTavernData.Instance.GetSquadTypeIcon(unit),
                    CollectionDetailPanel.TierColour(unit), found, found && !_unitSeen.Contains(unit));
                Hook(tile);
                faction.Tiles.Add(tile);
            }
        }

        private void Hook(CollectionTile tile)
        {
            tile.Hovered += OnTileHovered;
            tile.Unhovered += OnTileUnhovered;
            tile.Clicked += OnTileClicked;
        }

        #endregion

        #region Views

        private void SetView(View view, Faction faction)
        {
            StopPending();
            FadeContent();
            _hovered = null;
            _view = view;
            _faction = faction;

            _gearRow.SetActive(view == View.Gear);
            _potionRow.SetActive(view == View.Potions);
            foreach (Faction f in _factions) f.Row.SetActive(f == faction);

            bool isFaction = view == View.Faction;
            gridRoot.SetActive(!isFaction);
            gearGroups.gameObject.SetActive(view == View.Gear);
            potionGroups.gameObject.SetActive(view == View.Potions);
            tabsRoot.SetActive(isFaction);
            effectsRoot.SetActive(isFaction);
            // The marker's slot holds its place in the header row, so the slot is what hides.
            headerMarker.transform.parent.gameObject.SetActive(isFaction);
            if (!isFaction)
            {
                stageRoot.SetActive(false);
                loreRoot.SetActive(false);
                _rig.Clear();
                gridScroll.verticalNormalizedPosition = 1f;
            }
        }

        private void ShowGear()
        {
            SetView(View.Gear, null);
            EnsureGearBuilt();
            headerTitle.text = T("CollectionGear");
            headerSubtitle.text = FoundCount(_gear.Count(g => _gearFound.Contains((int)g)), _gear.Length);
            Pin(_gearPinned);
        }

        private void ShowPotions()
        {
            SetView(View.Potions, null);
            EnsurePotionsBuilt();
            headerTitle.text = T("CollectionPotions");
            headerSubtitle.text = FoundCount(_potions.Count(p => _potionFound.Contains((int)p)), _potions.Length);
            Pin(_potionPinned);
        }

        private void ShowFaction(Faction faction, FactionTab tab)
        {
            SetView(View.Faction, faction);
            _tab = tab;

            Color colour = ColorData.GetRaceDisplayColor(faction.Race);
            headerMarker.color = colour;
            headerTitle.text = T(faction.Race.ToString());
            headerSubtitle.text = FoundCount(faction.Units.Count(u => _unitFound.Contains(u)), faction.Units.Length);
            unitsTab.SetActive(tab == FactionTab.Units);
            heroesTab.SetActive(tab == FactionTab.Heroes);
            loreTab.SetActive(tab == FactionTab.Lore);

            string passive = T(faction.Race + "PassiveName");
            battleEffectText.text = $"<size=80%><uppercase>{T("BattleEffectLabel")}</uppercase></size>  <color=#E9C06A>{passive}</color>";
            battleEffectTooltip.SetUpToolTip(passive, KeywordText.ForTooltip(RacePassiveInfo.GetDescription(faction.Race)));
            string campaign = T(faction.Race + "BonusDescription");
            CollectionEffectBlock.Split(campaign, out string campaignName, out string campaignBody);
            campaignEffectText.text = $"<size=80%><uppercase>{T("CampaignEffectLabel")}</uppercase></size>  <color=#E9C06A>{campaignName}</color>";
            campaignEffectTooltip.SetUpToolTip(campaignName, KeywordText.ForTooltip(campaignBody));

            stageRoot.SetActive(tab != FactionTab.Lore);
            loreRoot.SetActive(tab == FactionTab.Lore);
            rosterRoot.SetActive(tab == FactionTab.Units);
            heroTabsRoot.SetActive(tab == FactionTab.Heroes);
            heroLoreRoot.SetActive(tab == FactionTab.Heroes);

            switch (tab)
            {
                case FactionTab.Units:
                    // The roster must be active before its tiles are built, or their Awake never runs.
                    EnsureUnitsBuilt(faction);
                    foreach (Faction f in _factions)
                        foreach (CollectionTile tile in f.Tiles)
                            tile.gameObject.SetActive(f == faction);
                    Pin(faction.Pinned);
                    break;
                case FactionTab.Heroes:
                    for (int i = 0; i < heroTabs.Length; i++)
                    {
                        bool used = i < faction.Heroes.Length;
                        heroTabs[i].gameObject.SetActive(used);
                        if (!used) continue;
                        heroTabs[i].SetLabel(T(faction.Heroes[i].HeroName));
                        CollectionDetailPanel.SetHeroPortrait(heroTabs[i].Portrait, faction.Heroes[i].HeroID);
                    }
                    ShowHero(Mathf.Clamp(faction.HeroIndex, 0, Mathf.Max(0, faction.Heroes.Length - 1)));
                    break;
                case FactionTab.Lore:
                    _rig.Clear();
                    loreText.text = WithInitial(LocalizationManager.Instance.GetLoreString(faction.Race + "Lore"));
                    loreScroll.verticalNormalizedPosition = 1f;
                    detail.ShowFaction(faction.Race, faction.Heroes, faction.Units.Count(u => _unitFound.Contains(u)), faction.Units.Length, OnCommander);
                    break;
            }
        }

        private void ShowHero(int index)
        {
            if (_faction == null || index >= _faction.Heroes.Length) return;
            _faction.HeroIndex = index;
            for (int i = 0; i < heroTabs.Length; i++) heroTabs[i].SetActive(i == index);
            Hero hero = _faction.Heroes[index];
            detail.ShowHero(hero, OnSignatureUnit);
            _rig.ShowHero(hero);
            heroLoreText.text = WithInitial(LocalizationManager.Instance.GetLoreString(hero.HeroPrefabName));
            heroLoreScroll.verticalNormalizedPosition = 1f;
        }

        private void OnSignatureUnit(UnitName unit)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            int index = Array.IndexOf(_faction.Units, unit);
            if (index >= 0) _faction.Pinned = index;
            ShowFaction(_faction, FactionTab.Units);
        }

        private void OnCommander(int index)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            _faction.HeroIndex = index;
            ShowFaction(_faction, FactionTab.Heroes);
        }

        #endregion

        #region Selection

        private List<CollectionTile> CurrentTiles() => _view switch
        {
            View.Gear => _gearTiles,
            View.Potions => _potionTiles,
            _ => _faction != null && _tab == FactionTab.Units ? _faction.Tiles : null,
        };

        private int PinnedIndex() => _view switch
        {
            View.Gear => _gearPinned,
            View.Potions => _potionPinned,
            _ => _faction != null ? Mathf.Max(0, _faction.Pinned) : 0,
        };

        private void Pin(int index)
        {
            List<CollectionTile> tiles = CurrentTiles();
            if (tiles == null || tiles.Count == 0) return;
            if (index < 0) index = Mathf.Max(0, tiles.FindIndex(tile => tile.Found));
            index = Mathf.Clamp(index, 0, tiles.Count - 1);
            switch (_view)
            {
                case View.Gear: _gearPinned = index; break;
                case View.Potions: _potionPinned = index; break;
                default: _faction.Pinned = index; break;
            }
            for (int i = 0; i < tiles.Count; i++) tiles[i].SetSelected(i == index);
            Present(index, true);
        }

        private void Present(int index, bool loadModel)
        {
            switch (_view)
            {
                case View.Gear:
                {
                    GearID id = _gear[index];
                    bool found = _gearFound.Contains((int)id);
                    detail.ShowGear(id, found);
                    if (found && _gearSeen.Add((int)id))
                    {
                        SaveDataHandler.AcknowledgedGear(id);
                        _gearTiles[index].SetNew(false);
                        RefreshNewDots();
                    }
                    break;
                }
                case View.Potions:
                {
                    ConsumableEnum id = _potions[index];
                    bool found = _potionFound.Contains((int)id);
                    detail.ShowPotion(id, found);
                    if (found && _potionSeen.Add((int)id))
                    {
                        SaveDataHandler.AcknowledgedPotion(id);
                        _potionTiles[index].SetNew(false);
                        RefreshNewDots();
                    }
                    break;
                }
                default:
                {
                    if (_faction == null || _tab != FactionTab.Units) return;
                    UnitName unit = _faction.Units[index];
                    bool found = _unitFound.Contains(unit);
                    detail.ShowUnit(unit, _faction.Race, found);
                    if (loadModel) _rig.ShowUnit(unit, _faction.Race, found);
                    if (found && _unitSeen.Add(unit))
                    {
                        SaveDataHandler.AcknowledgedTroop(unit);
                        _faction.Tiles[index].SetNew(false);
                        RefreshNewDots();
                    }
                    break;
                }
            }
        }

        private void OnTileHovered(CollectionTile tile)
        {
            _hovered = tile;
            StopPending();
            Present(tile.Index, false);
            if (_view == View.Faction)
                _pending = StartCoroutine(After(previewDelay, () =>
                {
                    if (_hovered == tile) _rig.ShowUnit(_faction.Units[tile.Index], _faction.Race, tile.Found);
                }));
        }

        private void OnTileUnhovered(CollectionTile tile)
        {
            if (_hovered != tile) return;
            _hovered = null;
            StopPending();
            // A short grace period, so sliding across the grid does not flash the kept entry between tiles.
            _pending = StartCoroutine(After(revertDelay, () =>
            {
                if (_hovered == null) Present(PinnedIndex(), true);
            }));
        }

        private void OnTileClicked(CollectionTile tile)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            StopPending();
            Pin(tile.Index);
        }

        private IEnumerator After(float seconds, Action action)
        {
            // Realtime: the Collection opens over a battle that Settings may have paused.
            yield return new WaitForSecondsRealtime(seconds);
            _pending = null;
            action();
        }

        private void StopPending()
        {
            if (_pending == null) return;
            StopCoroutine(_pending);
            _pending = null;
        }

        #endregion

        #region Keyboard and controller

        private void Update()
        {
            if (_canvasGroup.alpha < 0.99f || _factions == null) return;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;

            int page = 0;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) page = -1;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) page = 1;
            if (pad != null && pad.leftShoulder.wasPressedThisFrame) page = -1;
            if (pad != null && pad.rightShoulder.wasPressedThisFrame) page = 1;
            if (page != 0)
            {
                StepPage(page);
                return;
            }

            if (_view == View.Faction)
            {
                int tab = -1;
                if (keyboard != null && keyboard.digit1Key.wasPressedThisFrame) tab = 0;
                if (keyboard != null && keyboard.digit2Key.wasPressedThisFrame) tab = 1;
                if (keyboard != null && keyboard.digit3Key.wasPressedThisFrame) tab = 2;
                if (pad != null && pad.leftTrigger.wasPressedThisFrame) tab = ((int)_tab + 2) % 3;
                if (pad != null && pad.rightTrigger.wasPressedThisFrame) tab = ((int)_tab + 1) % 3;
                if (tab >= 0 && tab != (int)_tab)
                {
                    SwitchTab((FactionTab)tab);
                    return;
                }
            }

            Vector2 move = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) move = Vector2.left;
                if (keyboard.rightArrowKey.wasPressedThisFrame) move = Vector2.right;
                if (keyboard.upArrowKey.wasPressedThisFrame) move = Vector2.up;
                if (keyboard.downArrowKey.wasPressedThisFrame) move = Vector2.down;
            }
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) move = Vector2.left;
                if (pad.dpad.right.wasPressedThisFrame) move = Vector2.right;
                if (pad.dpad.up.wasPressedThisFrame) move = Vector2.up;
                if (pad.dpad.down.wasPressedThisFrame) move = Vector2.down;
                if (pad.buttonEast.wasPressedThisFrame) closeButton.onClick.Invoke();
            }
            if (move != Vector2.zero) Move(move);
        }

        private void StepPage(int direction)
        {
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
            int count = _factions.Length + 2;
            int current = _view == View.Gear ? 0 : _view == View.Potions ? 1 : 2 + Array.IndexOf(_factions, _faction);
            int next = (current + direction + count) % count;
            if (next == 0) ShowGear();
            else if (next == 1) ShowPotions();
            else ShowFaction(_factions[next - 2], FactionTab.Units);
        }

        // Moves the kept entry to the nearest tile in the pressed direction, measured on screen, so it works
        // across the rarity groups as well as inside one grid.
        private void Move(Vector2 direction)
        {
            if (_view == View.Faction && _tab == FactionTab.Heroes && direction.x != 0f && _faction.Heroes.Length > 1)
            {
                IAudioRequester.Instance.PlaySFX(SFXData.ButtonClick);
                int count = _faction.Heroes.Length;
                ShowHero((_faction.HeroIndex + (direction.x > 0f ? 1 : -1) + count) % count);
                return;
            }

            List<CollectionTile> tiles = CurrentTiles();
            int from = PinnedIndex();
            if (tiles == null || from < 0 || from >= tiles.Count) return;
            Vector2 origin = tiles[from].transform.position;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (i == from || !tiles[i].gameObject.activeInHierarchy) continue;
                Vector2 delta = (Vector2)tiles[i].transform.position - origin;
                float along = Vector2.Dot(delta, direction);
                if (along <= 1f) continue;
                float across = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
                float score = along + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0) return;
            IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
            StopPending();
            _hovered = null;
            Pin(best);
            Reveal((RectTransform)tiles[best].transform);
        }

        private void Reveal(RectTransform target)
        {
            if (!gridRoot.activeInHierarchy) return;
            RectTransform viewport = gridScroll.viewport;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, target);
            Vector2 position = gridScroll.content.anchoredPosition;
            const float margin = 16f;
            if (bounds.max.y > viewport.rect.yMax) position.y -= bounds.max.y - viewport.rect.yMax + margin;
            else if (bounds.min.y < viewport.rect.yMin) position.y += viewport.rect.yMin - bounds.min.y + margin;
            gridScroll.content.anchoredPosition = position;
        }

        #endregion

        #region Counts

        private void RefreshCounts()
        {
            int gearFound = _gear.Count(g => _gearFound.Contains((int)g));
            int potionFound = _potions.Count(p => _potionFound.Contains((int)p));
            _gearRow.SetProgress(gearFound, _gear.Length);
            _potionRow.SetProgress(potionFound, _potions.Length);

            int found = gearFound + potionFound;
            int total = _gear.Length + _potions.Length;
            foreach (Faction faction in _factions)
            {
                int unitsFound = faction.Units.Count(u => _unitFound.Contains(u));
                faction.Row.SetProgress(unitsFound, faction.Units.Length);
                found += unitsFound;
                total += faction.Units.Length;
            }

            progressText.text = string.Format(T("CollectionFoundCount"), $"<color=#ECE6D8>{found}</color>", total);
            progressFill.anchorMax = new Vector2(total > 0 ? (float)found / total : 0f, 1f);
            RefreshNewDots();
        }

        private void RefreshNewDots()
        {
            if (_gearRow == null) return;
            _gearRow.SetNew(_gearFound.Any(id => !_gearSeen.Contains(id)));
            _potionRow.SetNew(_potionFound.Any(id => !_potionSeen.Contains(id)));
            foreach (Faction faction in _factions)
                faction.Row.SetNew(faction.Units.Any(u => _unitFound.Contains(u) && !_unitSeen.Contains(u)));
        }

        private static string FoundCount(int found, int total) => string.Format(T("CollectionFoundCount"), found, total);

        // Opens a lore passage with a gold initial, as a codex page would.
        private static string WithInitial(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.TrimStart();
            if (text.Length == 0 || text[0] == '<') return text;
            return $"<size=160%><color=#E9C06A>{text[0]}</color></size>{text.Substring(1)}";
        }

        #endregion
    }
}
