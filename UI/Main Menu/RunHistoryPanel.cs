using System.Collections.Generic;
using System.Globalization;
using Memori.Localization;
using Memori.SaveData;
using Memori.Tooltip;
using TJ.Spells;
using TMPro;
using UnityEngine;

namespace TJ.MainMenu
{
    /// <summary>
    /// The Run History board: every finished campaign in <see cref="PlayerSaveData.runHistory"/>,
    /// newest first, and for the selected run the warband it ended with - army and reserves as
    /// squad cards, gear and spells as their usual tiles - plus the run's gold and outcome.
    ///
    /// Everything here is a read of the <see cref="RunRecord"/>; nothing on this panel writes.
    /// The squad and gear cards are the same prefabs the map and run setup use, so their tooltips
    /// come for free.
    /// </summary>
    public class RunHistoryPanel : MainMenuPanel
    {
        [Header("Run list")]
        [SerializeField] private RunHistoryCard cardPrefab;
        [SerializeField] private Transform cardsParent;
        [SerializeField] private TMP_Text runCountText;
        [SerializeField] private TMP_Text emptyText;

        [Header("Selected run")]
        [SerializeField] private GameObject detailGroup;
        [SerializeField] private TMP_Text detailTitleText;
        [SerializeField] private TMP_Text detailStatsText;
        [SerializeField] private TMP_Text armyLabelText;
        [SerializeField] private TMP_Text reserveLabelText;
        [SerializeField] private TMP_Text gearLabelText;
        [SerializeField] private TMP_Text spellsLabelText;
        [SerializeField] private Transform armyParent;
        [SerializeField] private Transform reserveParent;
        [SerializeField] private Transform gearParent;
        [SerializeField] private Transform spellsParent;
        [SerializeField] private SquadDisplayCardMenu squadCardPrefab;
        [SerializeField] private CollectionGearCard gearCardPrefab;
        [SerializeField] private SpellBrowseSlot spellTilePrefab;

        /// <summary>Slots from this index up are the reserve row, matching HUDPanel's layout.</summary>
        private const int MAIN_ARMY_SLOTS = 10;

        private readonly List<RunHistoryCard> _spawnedCards = new();
        private readonly List<GameObject> _spawnedDetail = new();
        private RunRecord _selected;

        public override void OpenPanel()
        {
            base.OpenPanel();
            Refresh();
        }

        public override void ClosePanel()
        {
            base.ClosePanel();
            // Cards are cheap to rebuild and the list can change between visits, so nothing is kept.
            ClearList();
            ClearDetail();
        }

        private void Refresh()
        {
            ClearList();
            ClearDetail();

            LocalizationManager loc = LocalizationManager.Instance;
            List<RunRecord> runs = SaveDataHandler.GetRunHistory();

            int wins = 0;
            foreach (RunRecord run in runs)
            {
                if (run.outcome == RunOutcome.Win) wins++;

                // Parent passed to Instantiate on purpose: a card that starts life unparented is a root
                // canvas for one frame and can lose its sorting settings (see the run-setup card notes).
                RunHistoryCard card = Instantiate(cardPrefab, cardsParent);
                card.Load(run, ShowRun);
                _spawnedCards.Add(card);
            }

            runCountText.text = string.Format(loc.GetText("RunHistoryCount"), runs.Count, wins);
            emptyText.text = loc.GetText("RunHistoryEmpty");
            emptyText.gameObject.SetActive(runs.Count == 0);

            armyLabelText.text = loc.GetText("RunHistoryArmy");
            reserveLabelText.text = loc.GetText("Reserve");
            gearLabelText.text = loc.GetText("RunHistoryGear");
            spellsLabelText.text = loc.GetText("Spells");

            if (runs.Count > 0) ShowRun(runs[0]);
            else detailGroup.SetActive(false);
        }

        private void ShowRun(RunRecord run)
        {
            ClearDetail();
            _selected = run;
            foreach (RunHistoryCard card in _spawnedCards)
                card.SetSelected(card.Record == run);

            detailGroup.SetActive(true);

            LocalizationManager loc = LocalizationManager.Instance;
            Hero hero = HeroData.GetHeroByID(run.heroID);
            DifficultyLevel difficultyData = DifficultyData.GetDifficultyLevelData(run.difficulty);
            string date = run.EndedAtUtc.ToLocalTime().ToString("D", CultureInfo.CurrentCulture);

            detailTitleText.text = $"{loc.GetText(hero.HeroName)}  <color={ColorData.Secondary}>|</color>  " +
                                   $"{loc.GetText("Level")} {(int)run.difficulty}: {loc.GetText(difficultyData.difficultyName)}  " +
                                   $"<color={ColorData.Secondary}>|</color>  {RunHistoryCard.FormatOutcome(run.outcome, loc)}";

            detailStatsText.text = string.Join("    ",
                Stat(loc.GetText("Gold"), run.goldAtEnd, ColorData.Gold),
                Stat(loc.GetText("RunHistoryGoldEarned"), run.goldEarned, ColorData.Gold),
                Stat(loc.GetText("RunHistoryActReached"), run.actReached),
                Stat(loc.GetText("Chapters"), run.chaptersCompleted),
                Stat(loc.GetText("RunHistoryBattles"), run.battlesFought),
                Stat(loc.GetText("enemiesSlain"), run.enemiesSlain),
                Stat(loc.GetText("RunHistoryRenownEarned"), run.renownEarned, ColorData.Tier4),
                $"<color={ColorData.Secondary}>{loc.GetText("RunHistoryTime")}</color> {LeaderboardRow.FormatTime(run.playTimeSeconds)}",
                $"<color={ColorData.Secondary}>{date}</color>");

            BuildArmy(run, loc);
            BuildGear(run, loc);
            BuildSpells(run, loc);
        }

        private static string Stat(string label, int value, string valueColor = null)
        {
            string colored = valueColor != null ? $"<color={valueColor}>{value}</color>" : value.ToString();
            // "Act {0}" style labels carry their own placeholder; the rest read "Label value".
            return label.Contains("{0}") ? string.Format(label, colored) : $"<color={ColorData.Secondary}>{label}</color> {colored}";
        }

        private void BuildArmy(RunRecord run, LocalizationManager loc)
        {
            int reserves = 0;
            for (int i = 0; i < run.army.Length; i++)
            {
                SquadToLoad squad = run.army[i];
                if (squad.isEmptySquad || squad.maxUnitCount <= 0) continue;

                bool inReserve = i >= MAIN_ARMY_SLOTS;
                if (inReserve) reserves++;

                SquadDisplayCardMenu card = Instantiate(squadCardPrefab, inReserve ? reserveParent : armyParent);
                card.SetUp(squad, inReserve, _isEnemy: true);
                card.LockCard(true);
                card.InheritCanvasSorting();

                string title = loc.GetText(squad.UnitName.ToString());
                string description = squad.PrestigeTrait != UnitAttribute.None
                    ? loc.GetText(squad.PrestigeTrait.ToString())
                    : loc.GetText(TabletopTavernData.Instance.GetUnitTypeFromUnitName(squad.UnitName).ToString());
                card.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(title, description);

                _spawnedDetail.Add(card.gameObject);
            }

            reserveLabelText.gameObject.SetActive(reserves > 0);
            reserveParent.gameObject.SetActive(reserves > 0);
        }

        private void BuildGear(RunRecord run, LocalizationManager loc)
        {
            int shown = 0;
            foreach (GearID gearID in run.gear)
            {
                if (gearID == GearID.None) continue;
                CollectionGearCard card = Instantiate(gearCardPrefab, gearParent);
                // Collected and acknowledged: the card is a record of what was carried, never a
                // discovery, so its hover must not touch the collection's acknowledged list.
                card.LoadGearCard(gearID, true, true);
                _spawnedDetail.Add(card.gameObject);
                shown++;
            }
            gearLabelText.gameObject.SetActive(shown > 0);
            gearParent.gameObject.SetActive(shown > 0);
        }

        private void BuildSpells(RunRecord run, LocalizationManager loc)
        {
            int shown = 0;
            if (spellTilePrefab != null)
            {
                foreach (Spell spell in run.spells)
                {
                    SpellData spellData = SpellRegistry.Get(spell);
                    if (spellData == null) continue;

                    SpellBrowseSlot tile = Instantiate(spellTilePrefab, spellsParent);
                    tile.SetUp(spellData, null, null);
                    tile.SetState(SpellBrowseState.Available);
                    tile.gameObject.AddComponent<MemoriTooltipTrigger>().SetUpToolTip(
                        loc.GetText(spellData.Spell.ToString()),
                        spellData.GetLocalizedSpellDescription());
                    _spawnedDetail.Add(tile.gameObject);
                    shown++;
                }
            }
            spellsLabelText.gameObject.SetActive(shown > 0);
            spellsParent.gameObject.SetActive(shown > 0);
        }

        private void ClearList()
        {
            foreach (RunHistoryCard card in _spawnedCards)
            {
                if (card != null) Destroy(card.gameObject);
            }
            _spawnedCards.Clear();
        }

        private void ClearDetail()
        {
            _selected = null;
            foreach (GameObject go in _spawnedDetail)
            {
                if (go != null) Destroy(go);
            }
            _spawnedDetail.Clear();
        }

        /// <summary>How many runs are recorded, for the main-menu button counter.</summary>
        public static int CountRuns() => SaveDataHandler.LoadPlayerSaveData().runHistory?.Count ?? 0;
    }
}
