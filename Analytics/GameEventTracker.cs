using System.Collections.Generic;
using Memori.Analytics;
using UnityEngine;

namespace TabletopTavern.Analytics
{
    public enum RunResult { Win, Loss, Abandon }

    // Game-side analytics layer. Owns two things:
    //
    //   1. Bootstrap - picks the backend once, before any scene loads. Editor -> console logger
    //      (verify without sending); RELEASE build -> Unity Analytics; DEMO and any other build
    //      -> no backend, so nothing is captured. No scene object or wiring required.
    //
    //   2. The game's event vocabulary. For now that is a single run-summary event, fired
    //      exactly once per run at its end (win / loss / abandon). Battle-level detail is a
    //      later pass and deliberately not wired yet.
    public static class GameEventTracker
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
#if UNITY_EDITOR
            // Editor: log to console for verification, never send.
            AnalyticsService.SetBackend(new DebugLogAnalyticsBackend());
#elif RELEASE
            // Real telemetry, RELEASE build only.
            AnalyticsService.SetBackend(new UnityAnalyticsBackend());
#else
            // DEMO (or any non-RELEASE build): analytics off - leaving the backend unset makes
            // every AnalyticsService call a no-op, so nothing is captured.
#endif
        }

        // Call whenever the player makes or changes their privacy choice.
        public static void SetConsent(bool granted) => AnalyticsService.SetConsent(granted);

        // The one upload per run: a single custom event at run end, carrying hero, difficulty,
        // outcome, and how far the run got, as typed parameters. All must be pre-registered in
        // the Unity dashboard (Event Manager) with names matching EXACTLY (case-sensitive):
        // event "runEnded" with params heroId (int), difficulty (int), runResult (string),
        // turnNumber (int). Any unregistered or misnamed param is silently dropped.
        public static void RunEnded(int heroId, int difficulty, RunResult result, int turnNumber)
        {
            AnalyticsService.Record("runEnded", new Dictionary<string, object>
            {
                { "heroId", heroId },
                { "difficulty", difficulty },
                { "runResult", result.ToString() },
                { "turnNumber", turnNumber },
            });
        }
    }
}
