using System.Collections.Generic;
using UnityEngine;

namespace TJ
{

public enum EventRollOutcome { CriticalFailure, Failure, Success, CriticalSuccess }
public enum EventOutcomeModifierEnum { None, Gold, GearDrop, UnitHealth, PrestigeUnit, NewUnit, ConsumableDrop, NextBattleLeadership, NextBattleEnemyLeadership, NextBattleMana, NextBattleFixture, RevealMap, LoseGear, LoseSquad, LosePrestige } //Reputation
// Only Roll throws the d20; every other kind resolves straight to successOutcome.
public enum EventChoiceKind { Roll, Pay, Sacrifice, WalkAway }

[System.Serializable] public struct EventChoice
{
    public EventChoiceKind Kind;
    public int minimumRollNeeded; // 1-20
    public int ArmySizeRequired;
    public int GoldRequired;
    [Tooltip("Paid when the choice is clicked: negative UnitHealth, LoseGear, LoseSquad or LosePrestige. The choice is refused if it cannot be paid.")]
    public List<EventOutcomeModifier> Cost;
    [Tooltip("The choice is hidden unless every non-empty list below matches: the hero's race, the hero id, a unit type in the army, owned working gear.")]
    public List<Race> RequiredRaces;
    public List<int> RequiredHeroes;
    public List<UnitType> RequiredUnitTypes;
    public List<GearID> RequiredGear;
    public EventOutcome successOutcome;
    public EventOutcome failureOutcome;
    public EventOutcome criticalFailureOutcome;
    public EventOutcome criticalSuccessOutcome;

}
[System.Serializable] public struct EventOutcome
{
    public List<EventOutcomeModifier> EventOutcomeModifiers;
}
[System.Serializable] public struct EventOutcomeModifier
{
    public float Value;
    public EventOutcomeModifierEnum EventOutcomeModifierEnum;
    [Tooltip("NextBattleFixture only: the fixture to place on the next battlefield. Its prefab decides which side it helps.")]
    public BattlefieldBonusEnum Fixture;
}
[System.Serializable] public struct EventReward
{
    public EventOutcome EventOutcome;
}
// Event outcomes waiting for the next battle, which clears them; auto-resolve keeps the mana for a fought one.
[System.Serializable] public class EventBattleEffects
{
    public int playerLeadership;
    public int enemyLeadership;
    public int mana;
    public List<BattlefieldBonusEnum> fixtures = new();
}
public struct EventDrawContext
{
    public int Act;
    public bool Endless;
    public int HeroId;
    public Race HeroRace;
    public IReadOnlyList<string> History;
}

public static class EventData
{
    public const string RegistryResourcePath = "EventData/EventRegistry";

    public static EventDefinitionSO[] GetAllEvents()
    {
        EventRegistrySO registry = Resources.Load<EventRegistrySO>(RegistryResourcePath);
        if (registry == null || registry.AllEvents == null)
        {
            Debug.LogError($"[EventData] No EventRegistrySO at Resources/{RegistryResourcePath}. No map event can load.");
            return System.Array.Empty<EventDefinitionSO>();
        }
        return registry.AllEvents;
    }
    public static string HistoryEntry(EventDefinitionSO _event, int _choice, EventRollOutcome _outcome) => $"{_event.TableKey}/{_choice}/{_outcome}";

    // Weighted draw among eligible events not yet in _seen; _seen empties once every eligible event has been drawn.
    public static EventDefinitionSO PickEvent(IReadOnlyList<EventDefinitionSO> _all, EventDrawContext _context, List<string> _seen, System.Random _random)
    {
        List<EventDefinitionSO> eligible = new();
        foreach (EventDefinitionSO e in _all)
            if (e != null && e.IsEligible(_context)) eligible.Add(e);
        if (eligible.Count == 0)
        {
            Debug.LogError("[EventData] No event is eligible for this run; drawing from every event.");
            foreach (EventDefinitionSO e in _all) if (e != null) eligible.Add(e);
            if (eligible.Count == 0) return null;
        }

        List<EventDefinitionSO> fresh = eligible.FindAll(e => !_seen.Contains(e.TableKey));
        if (fresh.Count == 0)
        {
            foreach (EventDefinitionSO e in eligible) _seen.Remove(e.TableKey);
            fresh = eligible;
        }

        float total = 0f;
        foreach (EventDefinitionSO e in fresh) total += Mathf.Max(0.01f, e.Weight);
        double pick = _random.NextDouble() * total;
        EventDefinitionSO chosen = fresh[fresh.Count - 1];
        foreach (EventDefinitionSO e in fresh)
        {
            pick -= Mathf.Max(0.01f, e.Weight);
            if (pick < 0) { chosen = e; break; }
        }
        _seen.Add(chosen.TableKey);
        return chosen;
    }
}
}
