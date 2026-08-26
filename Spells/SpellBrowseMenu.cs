using System;
using System.Collections.Generic;
using Memori.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TJ.Spells
{
    /// <summary>
    /// Pre-battle spell picker. Opened by hovering a <see cref="SpellCastButton"/> (custom battle,
    /// Deployment phase only). Clicking a row asks the <see cref="SpellManager"/> to swap that spell
    /// into the slot the menu was opened from. Hovering a row renders its name and description in one
    /// fixed info panel rather than a floating tooltip, so the text is always in the same place.
    ///
    /// Rows are grouped into faction bands rather than listed flat. Every spell in the pool is shown,
    /// including the ones already equipped - an equipped row dims in place and displays which slot holds
    /// it, so the list never reorders under the cursor mid-swap.
    ///
    /// The root object should carry a raycast-target background Image so the whole panel (not just the
    /// rows) counts as "hovered" - this drives the open/close retention handled by SpellManager.
    /// </summary>
    public class SpellBrowseMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private SpellBrowseSlot rowPrefab;
        [SerializeField] private SpellBrowseGroup groupPrefab;
        [SerializeField] private Transform contentParent;
        // Optional. The four Lesser spells are one band of four rather than a pair, so they do not fit
        // the two-bands-per-line grid the eight factions use. Assign a full-width parent above the grid
        // to give them their own line; leave it null and they fall in with everything else.
        [SerializeField] private Transform specialGroupParent;

        // Fixed info panel for the hovered row. Authored OUTSIDE this menu's root, because Open()
        // re-centers the root on whichever cast button was hovered and the panel must not move.
        // Its GameObject must stay ACTIVE in the scene - MemoriCanvasGroup.Awake never runs on an
        // inactive object, leaving canvasGroup null and CGEnable throwing. It hides via alpha.
        [Header("Spell Info Panel")]
        [SerializeField] private MemoriCanvasGroup spellInfoPanel;
        [SerializeField] private TMP_Text spellInfoNameText;
        [SerializeField] private TMP_Text spellInfoDescriptionText;
        // Both optional and purely additive - the panel still names and describes the spell without
        // them. Tinted from the same display ramp the tile interiors use, so the panel picks up the
        // colour of whatever is being hovered.
        [SerializeField] private TMP_Text spellInfoRaceText;
        [SerializeField] private Image spellInfoAccentImage;

        private SpellData[] pool;
        private Action<int, SpellData> onSpellPicked;
        private Action onHoverEnter, onHoverExit;

        private readonly List<SpellBrowseSlot> rows = new();
        private int targetSlotIndex = -1;

        /// <summary>
        /// Wires the fixed data once (pool + callbacks) and hides the menu. Called from
        /// SpellManager.LoadSpellManager after the loadout is known.
        /// </summary>
        public void Initialize(SpellData[] _pool, Action<int, SpellData> _onSpellPicked, Action _onHoverEnter, Action _onHoverExit)
        {
            pool = _pool;
            onSpellPicked = _onSpellPicked;
            onHoverEnter = _onHoverEnter;
            onHoverExit = _onHoverExit;

            // Reported rather than thrown: SpellManager.LoadSpellManager runs inside an async Task, so
            // an exception here can be swallowed instead of reaching the console. The picker still
            // works without the panel, it just cannot describe anything.
            if(spellInfoPanel == null)
                Debug.LogError($"SpellBrowseMenu: spellInfoPanel is not assigned on {name}. Spell descriptions will not render.");

            Close();
        }

        public void Open(int _targetSlotIndex, SpellData[] equippedSpells, RectTransform anchor)
        {
            targetSlotIndex = _targetSlotIndex;
            BuildRowsIfNeeded();
            RefreshEquippedState(equippedSpells);
            gameObject.SetActive(true);

            // Rows are built above while this root is still inactive, and layout does not run on an
            // inactive object - a TMP label reports a preferred width of zero until it has been laid
            // out once. Without this the faction bands solve against stale sizes and nothing marks them
            // dirty again, so the rule ends up drawn through the label instead of after it. It also
            // makes CenterHorizontallyOn read a settled rect rather than the pre-layout one.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

            if(anchor != null) CenterHorizontallyOn(anchor);

            // Opening describes the slot it was opened from. That is the content the cast button's
            // floating tooltip used to carry - SpellManager suppresses that tooltip for the whole
            // browsing window, so this panel is the only place the hovered slot is described.
            SpellData spellInArmedSlot = SpellInArmedSlot(equippedSpells);
            if(spellInArmedSlot != null) ShowSpellInfo(spellInArmedSlot);
            else HideSpellInfo();
        }

        /// <summary>The spell currently occupying the slot the menu was opened for, or null.</summary>
        private SpellData SpellInArmedSlot(SpellData[] equippedSpells)
        {
            return (targetSlotIndex >= 0 && equippedSpells != null && targetSlotIndex < equippedSpells.Length)
                ? equippedSpells[targetSlotIndex] : null;
        }

        // Aligns the menu's horizontal center with the hovered button's, keeping its authored y/z.
        // Center-based (not pivot-based) so it stays correct regardless of either rect's pivot.
        private void CenterHorizontallyOn(RectTransform anchor)
        {
            RectTransform rect = (RectTransform)transform;
            Vector3 anchorCenter = anchor.TransformPoint(anchor.rect.center);
            Vector3 selfCenter = rect.TransformPoint(rect.rect.center);
            Vector3 pivotOffsetX = rect.position - selfCenter;

            Vector3 pos = rect.position;
            pos.x = anchorCenter.x + pivotOffsetX.x;
            rect.position = pos;
        }

        public void Close()
        {
            targetSlotIndex = -1;
            HideSpellInfo();
            if(gameObject.activeSelf) gameObject.SetActive(false);
        }

        #region Spell Info Panel
        /// <summary>
        /// Renders the hovered row's spell in the fixed info panel. Called on hover only - nothing
        /// clears it while the menu is open, so the last hovered spell stays readable.
        /// </summary>
        private void ShowSpellInfo(SpellData spell)
        {
            if(spell == null || spellInfoPanel == null) return;

            spellInfoNameText.text = LocalizationManager.Instance.GetText(spell.Spell.ToString());
            spellInfoDescriptionText.text = spell.GetLocalizedSpellDescription();

            Color factionColour = ColorData.GetRaceDisplayColor(spell.Race);
            if(spellInfoRaceText != null)
            {
                spellInfoRaceText.text = SpellRaceLabel.Get(spell.Race);
                spellInfoRaceText.color = factionColour;
            }
            if(spellInfoAccentImage != null) spellInfoAccentImage.color = factionColour;

            spellInfoPanel.CGEnable();
            // CGEnable turns raycast blocking on. This panel is display only, and swallowing pointer
            // events near the menu would read as "pointer left" and close it mid-browse.
            spellInfoPanel.canvasGroup.blocksRaycasts = false;
        }

        private void HideSpellInfo()
        {
            if(spellInfoPanel == null) return;
            spellInfoPanel.CGDisable();
        }
        #endregion

        // Band order: the Lesser spells lead, then the eight factions in Race enum order. Exhaustive
        // over Race, so no pool entry can be silently dropped.
        private static readonly Race[] GROUP_ORDER =
        {
            Race.Special, Race.IronLegion, Race.Gruntkin, Race.RavenHost, Race.TaelindorForest,
            Race.SanguineCourt, Race.SakuraDynasty, Race.DeepstoneHold, Race.DrakosaurBrood,
        };

        // Rows are built once from the full pool, in a fixed order, and never rebuilt - so swapping a
        // spell never reorders the list. Equipped spells are dimmed in place instead of removed.
        private void BuildRowsIfNeeded()
        {
            if(rows.Count > 0 || pool == null) return;

            // Any children already here are leftovers from an Editor preview that was not cleared
            // before the scene was saved. Removing them makes a forgotten preview harmless instead of
            // silently doubling every row at runtime.
            ClearBands(false);

            foreach(Race race in GROUP_ORDER)
            {
                Transform bandParent = (race == Race.Special && specialGroupParent != null)
                    ? specialGroupParent : contentParent;

                SpellBrowseGroup group = null;

                foreach(SpellData spell in pool)
                {
                    if(spell == null || spell.Race != race) continue;

                    // Built lazily so a faction with nothing in the pool leaves no empty header behind.
                    if(group == null)
                    {
                        group = Instantiate(groupPrefab, bandParent);
                        group.SetUp(race);
                    }

                    // Parented at Instantiate, never afterwards - a Canvas on the row would refuse to
                    // give up overrideSorting while it was briefly a root canvas, and fail silently.
                    SpellBrowseSlot row = Instantiate(rowPrefab, group.TilesParent);
                    SpellData capturedSpell = spell;
                    row.SetUp(capturedSpell, () => Pick(capturedSpell), ShowSpellInfo);
                    rows.Add(row);
                }
            }
        }

        private void RefreshEquippedState(SpellData[] equippedSpells)
        {
            SpellData spellInArmedSlot = SpellInArmedSlot(equippedSpells);

            for(int i = 0; i < rows.Count; i++)
            {
                SpellData rowSpell = rows[i].SpellData;

                // An unauthored asset has no prefab and would throw at Instantiate on first cast, so it
                // is shown as un-takeable rather than allowed into a slot.
                if(rowSpell.SpellPrefab == null)
                {
                    rows[i].SetState(SpellBrowseState.Unavailable);
                    continue;
                }

                // Checked before Equipped, because the spell in the armed slot is equipped too and the
                // brackets are the more useful reading of it.
                if(spellInArmedSlot != null && rowSpell == spellInArmedSlot)
                {
                    rows[i].SetState(SpellBrowseState.InArmedSlot);
                    continue;
                }

                int equippedSlot = IndexOfEquipped(rowSpell, equippedSpells);
                rows[i].SetState(equippedSlot >= 0 ? SpellBrowseState.Equipped : SpellBrowseState.Available,
                                 equippedSlot);
            }
        }

        /// <summary>Slot holding this spell, or -1. The index is what lets a row show WHICH slot.</summary>
        private int IndexOfEquipped(SpellData spell, SpellData[] equippedSpells)
        {
            if(equippedSpells == null) return -1;
            for(int i = 0; i < equippedSpells.Length; i++)
            {
                if(equippedSpells[i] == spell) return i;
            }
            return -1;
        }

        private void Pick(SpellData spell)
        {
            if(targetSlotIndex < 0) return;
            onSpellPicked?.Invoke(targetSlotIndex, spell);
        }

        public void OnPointerEnter(PointerEventData eventData) => onHoverEnter?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => onHoverExit?.Invoke();

        /// <summary>
        /// Empties both band parents. <paramref name="immediate"/> is for the Editor preview, which has
        /// no frame to wait for; the runtime path uses ordinary Destroy.
        /// </summary>
        private void ClearBands(bool immediate)
        {
            rows.Clear();
            ClearChildren(contentParent, immediate);
            ClearChildren(specialGroupParent, immediate);
        }

        private void ClearChildren(Transform parent, bool immediate)
        {
            if(parent == null) return;

            for(int i = parent.childCount - 1; i >= 0; i--)
            {
                GameObject child = parent.GetChild(i).gameObject;
                if(immediate) DestroyImmediate(child);
                else Destroy(child);
            }
        }

#if UNITY_EDITOR
        #region Editor preview

        // Every row, group header and colour in this menu is built at runtime, which leaves the prefab
        // impossible to lay out in the Editor - an empty content parent tells you nothing about
        // spacing, band widths or how any of the four row states read.
        //
        // These build a throwaway copy from the real spell assets, deliberately spreading the states
        // across the rows so all four can be styled in one pass. Preview objects are ordinary scene
        // objects, so CLEAR BEFORE SAVING - though BuildRowsIfNeeded now clears leftovers at runtime
        // too, so forgetting is not fatal.

        // Editor-only placeholder, sized like a real description so wrapping can be judged. Not
        // player-facing, so it is exempt from the no-hardcoded-strings rule.
        private const string PREVIEW_DESCRIPTION =
            "Preview text. This line exists to show how a two or three line spell description wraps inside the panel.";

        [ContextMenu("Preview/Build")]
        private void EditorBuildPreview()
        {
            if(Application.isPlaying)
            {
                Debug.LogWarning("SpellBrowseMenu: the preview is an Editor authoring tool, not for Play mode.", this);
                return;
            }
            if(groupPrefab == null || rowPrefab == null || contentParent == null)
            {
                Debug.LogError("SpellBrowseMenu: assign groupPrefab, rowPrefab and contentParent before previewing.", this);
                return;
            }

            EditorClearPreview();

            SpellData[] previewPool = EditorLoadAllSpells();
            if(previewPool.Length == 0)
            {
                Debug.LogWarning("SpellBrowseMenu: no SpellData assets found to preview.", this);
                return;
            }

            // Slot 2, matching the header copy, so the armed-slot row has somewhere to belong.
            targetSlotIndex = 1;

            int index = 0;
            SpellData infoSample = null;

            foreach(Race race in GROUP_ORDER)
            {
                Transform bandParent = (race == Race.Special && specialGroupParent != null)
                    ? specialGroupParent : contentParent;

                SpellBrowseGroup group = null;

                foreach(SpellData spell in previewPool)
                {
                    if(spell == null || spell.Race != race) continue;

                    if(group == null)
                    {
                        group = Instantiate(groupPrefab, bandParent);
                        UnityEditor.Undo.RegisterCreatedObjectUndo(group.gameObject, "Build Spell Picker Preview");
                        group.SetUp(race);
                    }

                    SpellBrowseSlot row = Instantiate(rowPrefab, group.TilesParent);
                    UnityEditor.Undo.RegisterCreatedObjectUndo(row.gameObject, "Build Spell Picker Preview");
                    row.SetUp(spell, null, null);
                    EditorApplyPreviewState(row, spell, index);

                    if(infoSample == null && race != Race.Special) infoSample = spell;
                    index++;
                }
            }

            // Same reason as Open(): the labels have to be measured once before the bands can size to
            // them, or the preview shows the rule cutting through the text.
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);

            EditorPreviewInfoPanel(infoSample != null ? infoSample : previewPool[0]);
            Debug.Log($"SpellBrowseMenu: preview built with {index} rows. Clear it before saving the scene.", this);
        }

        [ContextMenu("Preview/Clear")]
        private void EditorClearPreview()
        {
            if(Application.isPlaying)
            {
                Debug.LogWarning("SpellBrowseMenu: the preview is an Editor authoring tool, not for Play mode.", this);
                return;
            }

            ClearBands(true);
        }

        /// <summary>
        /// Spreads the four states across the preview so each one is visible at once. Row 0 is equipped
        /// elsewhere, row 1 occupies the armed slot, rows 5 and 11 are equipped in later slots, and any
        /// asset with no prefab resolves to Unavailable exactly as it does at runtime.
        /// </summary>
        private void EditorApplyPreviewState(SpellBrowseSlot row, SpellData spell, int index)
        {
            if(spell.SpellPrefab == null) { row.SetState(SpellBrowseState.Unavailable); return; }

            switch(index)
            {
                case 0:  row.SetState(SpellBrowseState.Equipped, 0);   break;
                case 1:  row.SetState(SpellBrowseState.InArmedSlot);   break;
                case 5:  row.SetState(SpellBrowseState.Equipped, 2);   break;
                case 11: row.SetState(SpellBrowseState.Equipped, 3);   break;
                default: row.SetState(SpellBrowseState.Available);     break;
            }
        }

        /// <summary>
        /// Fills the info panel without touching LocalizationManager or MemoriCanvasGroup - the first
        /// would fabricate a phantom singleton GameObject in the open scene, and the second has not run
        /// Awake in the Editor, so its cached canvasGroup may be null.
        /// </summary>
        private void EditorPreviewInfoPanel(SpellData sample)
        {
            if(sample == null) return;

            if(spellInfoNameText != null) spellInfoNameText.text = sample.Spell.ToString();
            if(spellInfoDescriptionText != null) spellInfoDescriptionText.text = PREVIEW_DESCRIPTION;

            Color factionColour = ColorData.GetRaceDisplayColor(sample.Race);
            if(spellInfoRaceText != null)
            {
                spellInfoRaceText.text = SpellRaceLabel.Get(sample.Race);
                spellInfoRaceText.color = factionColour;
            }
            if(spellInfoAccentImage != null) spellInfoAccentImage.color = factionColour;

            if(spellInfoPanel != null)
            {
                CanvasGroup group = spellInfoPanel.GetComponent<CanvasGroup>();
                if(group != null) group.alpha = 1f;
            }
        }

        private SpellData[] EditorLoadAllSpells()
        {
            List<SpellData> found = new();

            foreach(string guid in UnityEditor.AssetDatabase.FindAssets("t:SpellData"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                SpellData asset = UnityEditor.AssetDatabase.LoadAssetAtPath<SpellData>(path);
                if(asset != null) found.Add(asset);
            }
            return found.ToArray();
        }

        #endregion
#endif
    }
}
