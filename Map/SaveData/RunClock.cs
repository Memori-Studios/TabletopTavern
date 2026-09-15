using Memori.Scenes;
using UnityEngine;

namespace Memori.SaveData
{
    /// <summary>
    /// Counts the seconds a campaign has actually been played, for the Godking leaderboard and the
    /// Run History board. It ticks unscaled time while the game is on the Map or in a campaign
    /// battle, so the pause menu counts and a custom battle does not.
    ///
    /// The count is process-local until a save flushes it: every write to campaignSaveData.json
    /// (and its snapshot) folds <see cref="TakeUnflushed"/> into
    /// <see cref="CampaignSaveData.playTimeSeconds"/>, and run end does the same before the record
    /// is written. <see cref="Reset"/> runs when a campaign is created so time spent on the main
    /// menu or in a custom battle between runs is never charged to the next one.
    /// </summary>
    public static class RunClock
    {
        private static double _unflushedSeconds;
        private static bool _stateCountsAsRun;
        private static bool _subscribed;

        public static double UnflushedSeconds => _unflushedSeconds;

        /// <summary>Returns the seconds counted since the last flush and zeroes the counter.</summary>
        public static double TakeUnflushed()
        {
            double seconds = _unflushedSeconds;
            _unflushedSeconds = 0;
            return seconds;
        }

        public static void Reset()
        {
            _unflushedSeconds = 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var host = new GameObject("Run Clock");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Ticker>();
        }

        /// <summary>
        /// Counting starts and stops on the state-changed event, not on a per-frame read of
        /// CurrentGameState. The serialized boot value of that field is whatever the Editor last
        /// saved (it read Map on 2026-09-14), so a per-frame read charged the whole boot to the
        /// clock before the first real transition. The event only fires once a scene is actually
        /// up, which is also when play starts.
        /// </summary>
        private static void OnGameStateChanged(GameStateEnum state)
        {
            // The custom-battle flag is a cached save read, checked once per transition.
            _stateCountsAsRun = state == GameStateEnum.Map
                || (state == GameStateEnum.Battle && !SaveDataHandler.LoadPlayerSaveData().customBattle);
        }

        private static void Tick(float unscaledDelta)
        {
            if (!_subscribed)
            {
                SceneHandler sceneHandler = SceneHandler.InstanceIfExists;
                if (sceneHandler == null) return;
                sceneHandler.OnGameStateChanged += OnGameStateChanged;
                _subscribed = true;
            }

            if (_stateCountsAsRun) _unflushedSeconds += unscaledDelta;
        }

        private class Ticker : MonoBehaviour
        {
            private void Update() => Tick(Time.unscaledDeltaTime);
        }
    }
}
