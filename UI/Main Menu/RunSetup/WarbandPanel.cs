using System;
using System.Collections.Generic;
using Memori.Audio;
using Memori.Localization;
using Memori.Notifications;
using Memori.Tooltip;
using TJ.Spells;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TJ.MainMenu
{
    /// <summary>Which loadout block the source column is currently showing.</summary>
    public enum WarbandSection { Army, Gear, Spells }

    /// <summary>
    /// Screen two of run setup: everything that fills a slot or spends the treasury, under one
    /// persistent purse.
    ///
    /// Layout is source | loadout. The source column switches with its tabs, or by hovering a
    /// loadout block, so nothing is hidden behind a hover flyout the way the old Starting Gear /
    /// Modify Starting Army buttons were. Details show in tooltips and the unit hover panel.
    ///
    /// Army and gear remain owned by <see cref="StartingArmyManager"/>; this panel only drives
    /// section focus, the purse readout, the spell loadout, and validation.
    /// </summary>
    public class WarbandPanel : MonoBehaviour
    {
        [Header("Commander Summary")]
        [SerializeField] private TMP_Text commanderSummaryText;
        [SerializeField] private Button backToCommanderButton;

        // Label, amount and breakdown are one TMP object; the size and colour steps between them
        // are inline tags rather than separate objects.
        [Header("Purse")]
        [SerializeField] private TMP_Text remainingTreasuryText;
        [SerializeField] private MemoriTooltipTrigger treasuryTooltipTrigger;

        [Header("Source Column")]
        [SerializeField] private GameObject armySourceRoot;
        [SerializeField] private GameObject gearSourceRoot;
        [SerializeField] private GameObject spellSourceRoot;

        [Header("Source Tabs")]
        [SerializeField] private CollectionTab armyTab;
        [SerializeField] private CollectionTab gearTab;
        [SerializeField] private CollectionTab spellTab;

        [Header("Army Heading")]
        [SerializeField] private TMP_Text armyHeadingText;
        [SerializeField] private TMP_Text armyHintText;

        // One per loadout block, each authored with its own WarbandSection. Focus follows the
        // pointer rather than needing a click.
        [Header("Section Hover Areas")]
        [SerializeField] private WarbandSectionHoverArea[] sectionHoverAreas;
        [SerializeField] private GameObject armySectionHighlight;
        [SerializeField] private GameObject gearSectionHighlight;
        [SerializeField] private GameObject spellSectionHighlight;

        [Header("Section Counters")]
        [SerializeField] private TMP_Text armyCountText;
        [SerializeField] private TMP_Text gearCountText;
        [SerializeField] private TMP_Text spellCountText;

        [Header("Spells")]
        [SerializeField] private SpellLoadoutSlot[] spellSlots;
        // Shares SpellBrowseSlot and SpellBrowseGroup with the in-battle picker rather than having its
        // own row type, so a spell reads identically in run setup and in battle. Different prefabs,
        // same components.
        [SerializeField] private SpellBrowseSlot grimoireTilePrefab;
        [SerializeField] private SpellBrowseGroup grimoireGroupPrefab;
        [SerializeField] private Transform grimoireContentParent;
        // Optional, same role as SpellBrowseMenu.specialGroupParent: the four Lesser spells are a band
        // of four rather than a pair, so they can be given their own full-width line.
        [SerializeField] private Transform grimoireSpecialGroupParent;
        // Optional. Lives inside grimoireContentParent and is kept as its last cell across rebuilds.
        [SerializeField] private Transform grimoireLegend;
        [SerializeField] private TMP_Text legendEquippedText;
        [SerializeField] private TMP_Text legendLockedText;

        [Header("Grimoire Header")]
        [SerializeField] private TMP_Text grimoireTitleText;
        [SerializeField] private TMP_Text grimoireHintText;

        [Header("Validation")]
        [SerializeField] private RunSetupValidation validation;

        private PlayPanel playPanel;
        private StartingArmyManager startingArmySection;
        private WarbandSection focusedSection = WarbandSection.Army;
        private bool focusApplied;

        private readonly List<SpellBrowseSlot> grimoireTiles = new();
        private readonly List<MemoriTooltipTrigger> grimoireTooltips = new();
        private Spell[] loadout = Array.Empty<Spell>();
        // Slot the grimoire will fill on the next pick. Never the signature slot.
        private int targetSpellSlot = 1;

        public Spell[] Loadout => loadout;
        public RunSetupValidation Validation => validation;

        public void SetUp(PlayPanel _playPanel, StartingArmyManager _startingArmySection)
        {
            playPanel = _playPanel;
            startingArmySection = _startingArmySection;

            foreach (WarbandSectionHoverArea hoverArea in sectionHoverAreas)
            {
                if (hoverArea == null) continue;
                hoverArea.SetUp(SetFocus);
            }

            backToCommanderButton.onClick.RemoveAllListeners();
            backToCommanderButton.onClick.AddListener(playPanel.ShowCommanderScreen);

            WireTab(armyTab, WarbandSection.Army);
            WireTab(gearTab, WarbandSection.Gear);
            WireTab(spellTab, WarbandSection.Spells);

            // -= before += so a second SetUp (returning to the panel) does not double-subscribe.
            startingArmySection.remainingTreasury.OnValueChanged -= RefreshPurse;
            startingArmySection.remainingTreasury.OnValueChanged += RefreshPurse;
            startingArmySection.OnStartingArmyLengthChanged -= OnArmyLengthChanged;
            startingArmySection.OnStartingArmyLengthChanged += OnArmyLengthChanged;
        }

        /// <summary>
        /// Rebuilds the view for the active hero. Called from ShowWarbandScreen, so this is the one
        /// place that pays for destroying and re-instantiating the grimoire.
        /// </summary>
        public void LoadForHero(Hero hero)
        {
            loadout = SpellLoadout.Sanitize(loadout, hero.HeroID);
            // The armed slot is per-visit, not per-hero: leaving it wherever the last pick landed
            // means re-opening the screen arms an arbitrary slot the player never chose.
            targetSpellSlot = SpellLoadout.SignatureSlotIndex + 1;
            SetGrimoireLegendText();
            SetSourceText(hero);
            BuildGrimoire();
            RefreshSpellSlots();
            RefreshCommanderSummary(hero);
            RefreshPurse(startingArmySection.remainingTreasury.Value);
            // Re-applied even if Army was already focused, so every visit opens on the recruit list.
            focusApplied = false;
            SetFocus(WarbandSection.Army);
        }

        private void SetSourceText(Hero hero)
        {
            armyTab.SetLabel(LocalizationManager.Instance.GetText("WarbandTabRecruit"));
            gearTab.SetLabel(LocalizationManager.Instance.GetText("WarbandTabArmory"));
            spellTab.SetLabel(LocalizationManager.Instance.GetText("WarbandTabGrimoire"));
            armyHeadingText.text = string.Format(LocalizationManager.Instance.GetText("WarbandRecruitHeading"),
                                                 LocalizationManager.Instance.GetText(hero.Race.ToString()));
            armyHintText.text = LocalizationManager.Instance.GetText("WarbandRecruitHint");
        }

        /// <summary>
        /// Resets the loadout to the hero's default. Data only: this runs on every hero change from
        /// PlayPanel.LoadHeroes while the commander screen is up and the warband screen is not even
        /// visible, so rebuilding the grimoire here would be work nobody sees. LoadForHero does the
        /// rebuild when the screen is actually shown.
        /// </summary>
        public void ResetLoadoutForHero(Hero hero)
        {
            loadout = SpellLoadout.GetDefaultLoadout(hero.HeroID);
            targetSpellSlot = SpellLoadout.SignatureSlotIndex + 1;
            // LoadHeroes evaluates validation before it gets here (via LoadDifficulty), so without
            // this the strip would be judging the previous hero's loadout.
            RefreshValidation();
        }

        private void RefreshCommanderSummary(Hero hero)
        {
            string heroName = LocalizationManager.Instance.GetText(hero.HeroName);
            string factionName = LocalizationManager.Instance.GetText(hero.Race.ToString());
            DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(playPanel.SelectedDifficulty);
            string difficultyName = LocalizationManager.Instance.GetText(difficultyData.difficultyName);

            // The hero carries the line; faction and difficulty are context, so they drop to the
            // muted colour and a smaller size rather than competing at equal weight.
            commanderSummaryText.text =
                $"<b><color={ColorData.Primary}>{heroName}</color></b>" +
                $"<color={ColorData.Secondary}><size=85%> · {factionName} · {difficultyName}</size></color>";
        }

        #region Focus
        public void SetFocus(WarbandSection section)
        {
            // Focus now follows the pointer, so this fires on every block the mouse crosses.
            // Re-entering the block you are already on is a no-op, otherwise sweeping across the
            // column would replay the hover sound and re-toggle the source roots each frame-ish.
            // focusApplied forces the first call through, since Army is also the default value.
            if (focusApplied && focusedSection == section) return;

            focusApplied = true;
            focusedSection = section;

            armySourceRoot.SetActive(section == WarbandSection.Army);
            gearSourceRoot.SetActive(section == WarbandSection.Gear);
            spellSourceRoot.SetActive(section == WarbandSection.Spells);

            armySectionHighlight.SetActive(section == WarbandSection.Army);
            gearSectionHighlight.SetActive(section == WarbandSection.Gear);
            spellSectionHighlight.SetActive(section == WarbandSection.Spells);

            armyTab.SetActive(section == WarbandSection.Army);
            gearTab.SetActive(section == WarbandSection.Gear);
            spellTab.SetActive(section == WarbandSection.Spells);

            IAudioRequester.Instance.PlaySFX(SFXData.ButtonHover);
        }

        private void WireTab(CollectionTab tab, WarbandSection section)
        {
            tab.Button.onClick.RemoveAllListeners();
            tab.Button.onClick.AddListener(() => SetFocus(section));
        }
        #endregion

        #region Purse
        private void RefreshPurse(int remaining)
        {
            // Colour lives inline rather than on TMP_Text.color, because the label and breakdown
            // either side of the amount stay muted while the amount alone turns red on an overspend.
            string amountColor = remaining < 0 ? ColorData.Error : ColorData.Gold;
            string treasuryLabel = LocalizationManager.Instance.GetText("Treasury");

            int startingGold = startingArmySection.StartingGold;
            int armySpend = startingArmySection.ArmyGoldSpend;
            int gearSpend = startingArmySection.GearGoldSpend;

            remainingTreasuryText.text =
                $"<color={ColorData.Secondary}><size=75%><uppercase>{treasuryLabel}</uppercase></size></color>  " +
                $"<b><color={amountColor}><size=135%>{remaining}</size></color></b>  " +
                $"<color={ColorData.Secondary}><size=85%>" +
                string.Format(LocalizationManager.Instance.GetText("TreasuryBreakdown"), startingGold, armySpend, gearSpend) +
                "</size></color>";

            string bonus = startingArmySection.StartingGoldBonusFromMetaprogression > 0
                ? $" <color={ColorData.Green}>(+{startingArmySection.StartingGoldBonusFromMetaprogression})</color>"
                : "";
            treasuryTooltipTrigger.SetUpToolTip(
                LocalizationManager.Instance.GetText("Treasury"),
                string.Format(LocalizationManager.Instance.GetText("TreasuryTooltipDesc"), startingGold) + bonus);

            RefreshCounters();
            RefreshValidation();
        }

        private void OnArmyLengthChanged(int newLength)
        {
            RefreshCounters();
            RefreshValidation();
        }

        private void RefreshCounters()
        {
            armyCountText.text = $"{startingArmySection.SelectedArmy.Length} / {StartingArmyManager.MaxStartingArmySize}";
            gearCountText.text = playPanel.StartingGearID == GearID.None ? "0 / 1" : "1 / 1";
            // Counts against UNLOCKED slots, not the array length, so the readout is not permanently
            // short by however many slots the player has yet to buy.
            spellCountText.text = $"{CountFilledSpellSlots()} / {SpellLoadout.GetUnlockedSlotCount()}";
        }

        private int CountFilledSpellSlots()
        {
            int filled = 0;
            for (int i = 0; i < loadout.Length; i++)
            {
                if (loadout[i] != Spell.None) filled++;
            }
            return filled;
        }
        #endregion

        #region Spells
        // Band order, matching SpellBrowseMenu: the Lesser spells lead, then the eight factions in
        // Race enum order. Exhaustive over Race, so nothing in the pool can be silently dropped.
        private static readonly Race[] GRIMOIRE_GROUP_ORDER =
        {
            Race.Special, Race.IronLegion, Race.Gruntkin, Race.RavenHost, Race.TaelindorForest,
            Race.SanguineCourt, Race.SakuraDynasty, Race.DeepstoneHold, Race.DrakosaurBrood,
        };

        /// <summary>
        /// The pool itself is hero-independent now - every obtainable spell is listed for every hero.
        /// This still runs per hero because the STATES move: the locked set and which spell sits in
        /// the signature slot both change.
        /// </summary>
        private void BuildGrimoire()
        {
            // The whole obtainable pool, locked spells AND this hero's own signature included, so every
            // faction band is complete and the player can see what beating other heroes earns them.
            List<SpellData> pool = new();
            foreach (Spell spell in SpellLoadout.GetGrimoireSpells())
            {
                SpellData spellData = SpellRegistry.Get(spell);
                if (spellData != null) pool.Add(spellData);
            }

            BuildGrimoireBands(pool, immediateClear: false);
        }

        /// <summary>
        /// Instantiates the faction bands and their tiles. Shared by the runtime build and the Editor
        /// preview so the two cannot drift - what you author is the same code path that ships.
        /// </summary>
        /// <param name="immediateClear">Editor preview has no frame to wait for, so it needs DestroyImmediate.</param>
        private void BuildGrimoireBands(List<SpellData> pool, bool immediateClear)
        {
            // Whole bands are cleared, not just tiles - the group headers live under the same parents
            // and would otherwise accumulate on every hero change. The grimoire rebuilds per hero
            // (the locked set and the signature both move), unlike the battle picker's build-once.
            ClearGrimoireBands(grimoireContentParent, immediateClear);
            ClearGrimoireBands(grimoireSpecialGroupParent, immediateClear);
            grimoireTiles.Clear();
            grimoireTooltips.Clear();

            foreach (Race race in GRIMOIRE_GROUP_ORDER)
            {
                Transform bandParent = (race == Race.Special && grimoireSpecialGroupParent != null)
                    ? grimoireSpecialGroupParent : grimoireContentParent;

                SpellBrowseGroup group = null;

                foreach (SpellData spellData in pool)
                {
                    if (spellData.Race != race) continue;

                    // Built lazily so a faction with nothing obtainable leaves no empty header.
                    if (group == null)
                    {
                        group = Instantiate(grimoireGroupPrefab, bandParent);
                        group.SetUp(race);
                    }

                    // Parented at Instantiate, never afterwards - a Canvas on the tile would refuse to
                    // release overrideSorting while it was briefly a root canvas, and fail silently.
                    SpellBrowseSlot tile = Instantiate(grimoireTilePrefab, group.TilesParent);
                    SpellData captured = spellData;
                    tile.SetUp(captured, () => PickSpell(captured), null, NotifySpellAlreadyEquipped);
                    grimoireTiles.Add(tile);
                    if (Application.isPlaying) AddGrimoireTooltip(tile, captured);
                }
            }

            if (grimoireLegend != null) grimoireLegend.SetAsLastSibling();
        }

        private void ClearGrimoireBands(Transform parent, bool immediate)
        {
            if (parent == null) return;

            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child == grimoireLegend) continue;
                if (immediate) DestroyImmediate(child.gameObject);
                else Destroy(child.gameObject);
            }
        }

        private void SetGrimoireLegendText()
        {
            if (legendEquippedText != null) legendEquippedText.text = LocalizationManager.Instance.GetText("GrimoireLegendEquipped");
            if (legendLockedText != null) legendLockedText.text = LocalizationManager.Instance.GetText("Locked");
        }

        /// <summary>
        /// Names the slot the next pick fills, or says there is none to fill until one is bought.
        /// </summary>
        private void RefreshGrimoireHeader()
        {
            if (grimoireTitleText == null) return;

            bool hasFreeSlot = !SpellLoadout.IsSlotLocked(targetSpellSlot);
            grimoireTitleText.text = hasFreeSlot
                ? string.Format(LocalizationManager.Instance.GetText("SpellBrowseTitle"), targetSpellSlot + 1)
                : LocalizationManager.Instance.GetText("GrimoireNoFreeSlotTitle");
            if (grimoireHintText != null)
                grimoireHintText.text = LocalizationManager.Instance.GetText(hasFreeSlot ? "GrimoireSlotHint" : "GrimoireNoFreeSlotHint");
        }

        private void RefreshSpellSlots()
        {
            for (int i = 0; i < spellSlots.Length; i++)
            {
                SpellData spellData = i < loadout.Length ? SpellRegistry.Get(loadout[i]) : null;
                bool locked = SpellLoadout.IsSlotLocked(i);
                spellSlots[i].LoadSlot(i, spellData, FocusSpellSlot, null, locked);
                spellSlots[i].SetFocused(!locked && i == targetSpellSlot);
            }
            RefreshGrimoireStates();
            RefreshGrimoireHeader();
            RefreshCounters();
        }

        /// <summary>
        /// Resolves every grimoire tile to one of the four browse states. Same order of checks as
        /// SpellBrowseMenu.RefreshEquippedState, so a spell reads the same in run setup as in battle:
        /// unavailable wins over everything, then the armed slot, then equipped elsewhere.
        /// </summary>
        private void RefreshGrimoireStates()
        {
            foreach (SpellBrowseSlot tile in grimoireTiles)
            {
                Spell spell = tile.SpellData.Spell;
                int equippedSlot = Array.IndexOf(loadout, spell);

                // Equipped is resolved BEFORE locked, and that order is load-bearing. IsUnlocked is
                // false for a hero's own signature until that hero has been beaten on Godking, yet the
                // signature always occupies slot 1 - checking locked first rendered it hatched and
                // un-takeable while it was sitting equipped two panels away.
                //
                // Armed slot is checked first of all: that spell is equipped too, but the brackets are
                // the more useful reading, since it is the one about to be replaced. The signature can
                // never land here - FocusSpellSlot refuses to arm the signature slot.
                if (equippedSlot == targetSpellSlot)
                {
                    tile.SetState(SpellBrowseState.InArmedSlot);
                    continue;
                }
                if (equippedSlot >= 0)
                {
                    tile.SetState(SpellBrowseState.Equipped, equippedSlot);
                    continue;
                }

                // Locked maps to Unavailable: still visible, still coloured under its faction, but not
                // takeable - it reads as something to earn rather than being hidden.
                tile.SetState(SpellLoadout.IsUnlocked(spell)
                    ? SpellBrowseState.Available : SpellBrowseState.Unavailable);
            }

            // The footer names the armed slot and what it holds, so a showing tooltip must repaint.
            foreach (MemoriTooltipTrigger tooltip in grimoireTooltips) tooltip.RefreshContent();
        }

        /// <summary>Clicking a spell slot arms it as the destination for the next grimoire pick.</summary>
        private void FocusSpellSlot(int slotIndex)
        {
            if (slotIndex == SpellLoadout.SignatureSlotIndex) return;
            if (SpellLoadout.IsSlotLocked(slotIndex)) return;

            targetSpellSlot = slotIndex;
            for (int i = 0; i < spellSlots.Length; i++)
            {
                spellSlots[i].SetFocused(i == slotIndex);
            }
            RefreshGrimoireStates();
            RefreshGrimoireHeader();
            SetFocus(WarbandSection.Spells);
        }

        private static void NotifySpellAlreadyEquipped()
        {
            NotificationManager.Instance.ErrorNotification(
                LocalizationManager.Instance.GetText("SpellAlreadyEquipped"));
        }

        private void PickSpell(SpellData spellData)
        {
            if (spellData == null) return;
            if (targetSpellSlot <= SpellLoadout.SignatureSlotIndex || targetSpellSlot >= loadout.Length) return;
            if (SpellLoadout.IsSlotLocked(targetSpellSlot)) return;
            // The row's button is already non-interactable when locked; this guards the unlock gate
            // itself rather than trusting a UI state to be the only thing enforcing it.
            if (!SpellLoadout.IsUnlocked(spellData.Spell)) return;

            // Normally unreachable: an equipped tile rejects its own click with a flash and raises
            // NotifySpellAlreadyEquipped itself. Kept as the guard on the data path.
            if (Array.IndexOf(loadout, spellData.Spell) >= 0)
            {
                NotifySpellAlreadyEquipped();
                return;
            }

            loadout[targetSpellSlot] = spellData.Spell;
            IAudioRequester.Instance.PlaySFX(SFXData.AddGear);

            // The armed slot deliberately stays put. Picking is nearly always "I want to change
            // THIS slot", so auto-advancing meant a second look at the same slot silently landed
            // on the next one instead.
            RefreshSpellSlots();
            RefreshValidation();
        }
        #endregion

        #region Grimoire tooltip
        private void AddGrimoireTooltip(SpellBrowseSlot tile, SpellData spellData)
        {
            MemoriTooltipTrigger trigger = tile.GetComponent<MemoriTooltipTrigger>();
            if (trigger == null) trigger = tile.gameObject.AddComponent<MemoriTooltipTrigger>();
            trigger.SetContentProvider(() => BuildGrimoireTooltip(spellData));
            grimoireTooltips.Add(trigger);
        }

        /// <summary>The shared spell tooltip, with a footer that says what a click does, like the armory tooltip.</summary>
        private TooltipContent BuildGrimoireTooltip(SpellData spellData)
        {
            int equippedSlot = Array.IndexOf(loadout, spellData.Spell);
            bool pinned = equippedSlot == SpellLoadout.SignatureSlotIndex;
            TooltipContent content = SpellTooltip.Build(spellData, new SpellTooltip.Context { Pinned = pinned });
            if (!pinned) content.Footer = BuildGrimoireFooter(spellData, equippedSlot);
            return content;
        }

        // Equipped is checked before locked: the active hero's signature sits in slot 1 before that hero is beaten.
        private string BuildGrimoireFooter(SpellData spellData, int equippedSlot)
        {
            LocalizationManager loc = LocalizationManager.Instance;
            if (equippedSlot >= 0) return string.Format(loc.GetText("GrimoireInSlot"), equippedSlot + 1);
            if (!SpellLoadout.IsUnlocked(spellData.Spell)) return $"<color={ColorData.Error}>{BuildLockedDescription(spellData)}</color>";
            if (SpellLoadout.IsSlotLocked(targetSpellSlot)) return loc.GetText("GrimoireNoFreeSlotTitle");

            string footer = string.Format(loc.GetText("GrimoireClickToEquip"), targetSpellSlot + 1);
            SpellData replaced = targetSpellSlot < loadout.Length ? SpellRegistry.Get(loadout[targetSpellSlot]) : null;
            if (replaced != null) footer += "\n" + string.Format(loc.GetText("WarbandGearReplaces"), loc.GetText(replaced.Spell.ToString()));
            return footer;
        }

        /// <summary>"Complete a run as {hero} to unlock." Falls back to a heroless line if no hero
        /// claims this spell, which GetGrimoireSpells should already have filtered out.</summary>
        private string BuildLockedDescription(SpellData spellData)
        {
            if (!SpellLoadout.TryGetSignatureHero(spellData.Spell, out Hero owner))
            {
                return LocalizationManager.Instance.GetText("SpellLockedUnknownDesc");
            }

            return string.Format(LocalizationManager.Instance.GetText("SpellLockedDesc"),
                                 LocalizationManager.Instance.GetText(owner.HeroName));
        }
        #endregion

        public void RefreshValidation()
        {
            validation.Evaluate(playPanel, startingArmySection, loadout);
        }

#if UNITY_EDITOR
        #region Editor preview

        // The grimoire is built entirely at runtime, so the warband screen is blind to author without
        // this - empty band parents tell you nothing about spacing, band widths, or how any of the four
        // tile states read. Mirrors SpellBrowseMenu's preview, and goes through the same
        // BuildGrimoireBands the game uses so the two cannot drift.
        //
        // Preview objects are ordinary scene objects: CLEAR BEFORE SAVING.

        [ContextMenu("Preview/Build")]
        private void EditorBuildPreview()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("WarbandPanel: the preview is an Editor authoring tool, not for Play mode.", this);
                return;
            }
            if (grimoireGroupPrefab == null || grimoireTilePrefab == null || grimoireContentParent == null)
            {
                Debug.LogError("WarbandPanel: assign grimoireGroupPrefab, grimoireTilePrefab and grimoireContentParent before previewing.", this);
                return;
            }

            List<SpellData> pool = EditorLoadAllSpells();
            if (pool.Count == 0)
            {
                Debug.LogWarning("WarbandPanel: no SpellData assets found to preview.", this);
                return;
            }

            BuildGrimoireBands(pool, immediateClear: true);

            // Registered after the fact rather than inside BuildGrimoireBands, which stays free of
            // editor code. Registering each band root is enough - its tiles go with it.
            EditorRegisterUndo(grimoireContentParent);
            EditorRegisterUndo(grimoireSpecialGroupParent);

            EditorApplyPreviewStates();

            // The bands cannot size to their labels until those labels have been measured once.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

            Debug.Log($"WarbandPanel: grimoire preview built with {grimoireTiles.Count} tiles. Clear it before saving the scene.", this);
        }

        [ContextMenu("Preview/Clear")]
        private void EditorClearPreview()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("WarbandPanel: the preview is an Editor authoring tool, not for Play mode.", this);
                return;
            }

            ClearGrimoireBands(grimoireContentParent, immediate: true);
            ClearGrimoireBands(grimoireSpecialGroupParent, immediate: true);
            grimoireTiles.Clear();
        }

        /// <summary>
        /// Spreads the four states across the preview so each can be styled in one pass, rather than
        /// reading real save data - IsUnlocked depends on which heroes you have beaten, so a real read
        /// would show whatever this machine's save happens to contain.
        /// </summary>
        private void EditorApplyPreviewStates()
        {
            for (int i = 0; i < grimoireTiles.Count; i++)
            {
                switch (i)
                {
                    case 0:  grimoireTiles[i].SetState(SpellBrowseState.Equipped, 0);  break;
                    case 1:  grimoireTiles[i].SetState(SpellBrowseState.InArmedSlot);  break;
                    case 5:  grimoireTiles[i].SetState(SpellBrowseState.Equipped, 2);  break;
                    case 11: grimoireTiles[i].SetState(SpellBrowseState.Equipped, 3);  break;
                    case 3:
                    case 8:
                    case 14: grimoireTiles[i].SetState(SpellBrowseState.Unavailable);  break;
                    default: grimoireTiles[i].SetState(SpellBrowseState.Available);    break;
                }
            }
        }

        private void EditorRegisterUndo(Transform parent)
        {
            if (parent == null) return;

            for (int i = 0; i < parent.childCount; i++)
            {
                UnityEditor.Undo.RegisterCreatedObjectUndo(parent.GetChild(i).gameObject, "Build Grimoire Preview");
            }
        }

        private List<SpellData> EditorLoadAllSpells()
        {
            List<SpellData> found = new();

            foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:SpellData"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                SpellData asset = UnityEditor.AssetDatabase.LoadAssetAtPath<SpellData>(path);
                if (asset != null) found.Add(asset);
            }
            return found;
        }

        #endregion
#endif
    }
}
