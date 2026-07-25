using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using UnityAnalytics = Unity.Services.Analytics;

namespace TabletopTavern.Analytics
{
    // Sends events to Unity Gaming Services (UGS) Analytics. Notes:
    //   - Every event name + parameter MUST be pre-registered in the Unity dashboard
    //     (Event Manager) or it is dropped server-side.
    //   - Initialization is async; events recorded before it finishes are dropped. Our only
    //     event (RunEnded) fires at the end of a run, long after startup, so this is fine.
    //   - Consent maps onto Start/StopDataCollection, UGS's GDPR gate.
    //
    // Uses the CustomEvent + RecordEvent path (com.unity.services.analytics 6.x) - the same API
    // the project's earlier (now-removed) commented-out analytics used.
    public class UnityAnalyticsBackend : Memori.Analytics.IAnalyticsBackend
    {
        private bool m_consent = true;

        public bool IsInitialized { get; private set; }

        public async void Initialize()
        {
            try
            {
                await UnityServices.InitializeAsync();
                IsInitialized = true;
                if (m_consent) UnityAnalytics.AnalyticsService.Instance.StartDataCollection();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Analytics] Unity Services init failed: " + e.Message);
            }
        }

        public void Record(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (!IsInitialized) return;

            UnityAnalytics.CustomEvent evt = new(eventName);
            if (parameters != null)
            {
                foreach (KeyValuePair<string, object> kv in parameters) evt.Add(kv.Key, kv.Value);
            }
            UnityAnalytics.AnalyticsService.Instance.RecordEvent(evt);
        }

        public void SetConsent(bool granted)
        {
            m_consent = granted;
            if (!IsInitialized) return;

            if (granted) UnityAnalytics.AnalyticsService.Instance.StartDataCollection();
            else UnityAnalytics.AnalyticsService.Instance.StopDataCollection();
        }

        public void Flush()
        {
            if (IsInitialized) UnityAnalytics.AnalyticsService.Instance.Flush();
        }
    }
}
