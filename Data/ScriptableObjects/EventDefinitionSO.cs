using System.Collections.Generic;
using UnityEngine;

namespace TJ
{
    /// <summary>One map event; all of its text lives in the EventTable under keys built from TableKey.</summary>
    [CreateAssetMenu(fileName = "Event", menuName = "GameData/Event", order = 1)]
    public class EventDefinitionSO : ScriptableObject
    {
        [Tooltip("Prefix of every EventTable key this event reads, e.g. ThePlaguedVillage.")]
        public string TableKey;

        [Tooltip("At most 10: EventChoiceDisplay reads the choice index from the key's last digit.")]
        public EventChoice[] Choices;

        [Header("Who meets this event")]
        [Min(1)] public int MinAct = 1;
        [Tooltip("0 means no upper limit.")]
        [Min(0)] public int MaxAct;
        public bool EndlessOnly;
        [Tooltip("Hero races that can meet it. Empty means every race.")]
        public List<Race> Races;
        [Tooltip("Hero ids that can meet it. Empty means every hero.")]
        public List<int> Heroes;
        [Tooltip("History entries (TableKey/choice/outcome, or a prefix such as TableKey/0/). Empty means always; otherwise the run must hold one of them.")]
        public List<string> RequiresHistory;
        [Tooltip("Relative chance among the eligible events.")]
        [Min(0.01f)] public float Weight = 1f;

        public bool IsEligible(EventDrawContext _context)
        {
            if (_context.Act < MinAct) return false;
            if (MaxAct > 0 && _context.Act > MaxAct) return false;
            if (EndlessOnly && !_context.Endless) return false;
            if (Races != null && Races.Count > 0 && !Races.Contains(_context.HeroRace)) return false;
            if (Heroes != null && Heroes.Count > 0 && !Heroes.Contains(_context.HeroId)) return false;
            if (RequiresHistory == null || RequiresHistory.Count == 0) return true;
            if (_context.History == null) return false;
            foreach (string needed in RequiresHistory)
                foreach (string entry in _context.History)
                    if (entry.StartsWith(needed, System.StringComparison.Ordinal)) return true;
            return false;
        }

        public string NameKey => TableKey + "Name";
        public string DescriptionKey => TableKey + "Desc";
        public string ChoiceKey(int _choice) => TableKey + _choice;
        public string OutcomeKey(int _choice, EventRollOutcome _outcome) => ChoiceKey(_choice) + _outcome + "OutcomeDesc";

        public IEnumerable<string> AllTableKeys()
        {
            yield return NameKey;
            yield return DescriptionKey;
            for (int i = 0; i < Choices.Length; i++)
            {
                yield return ChoiceKey(i) + "Title";
                yield return ChoiceKey(i) + "Desc";
                foreach (EventRollOutcome outcome in System.Enum.GetValues(typeof(EventRollOutcome)))
                    yield return OutcomeKey(i, outcome);
            }
        }
    }
}
