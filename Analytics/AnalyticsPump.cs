using System;
using UnityEngine;

namespace TabletopTavern.Analytics
{
    // Calls HttpAnalyticsBackend's send loop every frame from a hidden object that survives scene loads.
    public class AnalyticsPump : MonoBehaviour
    {
        private Action _tick;

        public static void Create(Action tick)
        {
            var host = new GameObject("[Analytics]") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(host);
            host.AddComponent<AnalyticsPump>()._tick = tick;
        }

        private void Update()
        {
            _tick();
        }
    }
}
