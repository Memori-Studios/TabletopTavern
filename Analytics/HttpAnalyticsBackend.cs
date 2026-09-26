using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Memori.Analytics;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace TabletopTavern.Analytics
{
    // Posts queued events to Tools/AnalyticsServer in batches, retrying with backoff while it is unreachable.
    public class HttpAnalyticsBackend : IAnalyticsBackend
    {
        #region Tuning
        private const int BatchSize = 50;
        // Well under the server's 1 MB body limit, which refuses (and so drops) the whole batch.
        private const int MaxBatchBytes = 128 * 1024;
        // The server rejects props over 16 KB one event at a time, silently to the client; this keeps a margin.
        public const int MaxPropsBytes = 15 * 1024;
        private const int MaxQueuedEvents = 2000;
        private const float FirstRetrySeconds = 30f;
        private const float MaxRetrySeconds = 30f * 60f;
        private const int RequestTimeoutSeconds = 15;
        private const string QueueFileName = "queue.jsonl";
        private const string InstallIdFileName = "install_id";
        #endregion

        private readonly string _endpoint;
        private readonly string _writeKey;
        private readonly string _buildKind;
        private readonly string _installIdPath;
        private readonly AnalyticsQueue _queue;
        private readonly string _sessionId = Guid.NewGuid().ToString();

        private bool _consent = true;
        private string _installId;
        private bool _inFlight;
        private int _failures;
        private float _nextAttemptTime;

        public bool IsInitialized { get; private set; }

        public HttpAnalyticsBackend(string endpoint, string writeKey, string buildKind, string folder)
        {
            _endpoint = endpoint;
            _writeKey = writeKey;
            _buildKind = buildKind;
            _installIdPath = Path.Combine(folder, InstallIdFileName);
            _queue = new AnalyticsQueue(Path.Combine(folder, QueueFileName));
        }

        // Random per install, never the Steam id. Shown in bug reports so a reporter's runs can be found.
        public string InstallId => _consent ? GetOrCreateInstallId() : "None (analytics off)";

        public void Initialize()
        {
            if (IsInitialized) return;
            AnalyticsPump.Create(Tick);
            IsInitialized = true;
        }

        public void Record(string eventName, IReadOnlyDictionary<string, object> parameters)
        {
            if (!_consent) return;

            int propsBytes = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(parameters ?? new Dictionary<string, object>(), Formatting.None));
            if (propsBytes > MaxPropsBytes)
            {
                Debug.LogWarning($"[Analytics] {eventName} dropped: its props are {propsBytes} bytes, over the {MaxPropsBytes} byte limit.");
                return;
            }

            var envelope = new Dictionary<string, object>
            {
                { "id", Guid.NewGuid().ToString() },
                { "event", eventName },
                { "ts", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) },
                { "install_id", GetOrCreateInstallId() },
                { "session_id", _sessionId },
                { "app_version", Application.version },
                { "build_kind", _buildKind },
                { "props", parameters ?? new Dictionary<string, object>() },
            };
            _queue.Append(JsonConvert.SerializeObject(envelope, Formatting.None));
        }

        // Withdrawing consent deletes everything held locally, including the install id.
        public void SetConsent(bool granted)
        {
            _consent = granted;
            if (granted) return;

            _queue.Clear();
            if (File.Exists(_installIdPath)) File.Delete(_installIdPath);
            _installId = null;
        }

        public void Flush()
        {
            _failures = 0;
            _nextAttemptTime = 0f;
        }

        #region Sending
        private void Tick()
        {
            if (!_consent || _inFlight) return;
            if (_queue.Count > MaxQueuedEvents) _queue.RemoveFirst(_queue.Count - MaxQueuedEvents);
            if (_queue.Count == 0 || Time.unscaledTime < _nextAttemptTime) return;

            AnalyticsQueue.Batch batch = _queue.Peek(BatchSize, MaxBatchBytes);
            if (batch.Events.Count == 0)
            {
                _queue.RemoveFirst(batch.LinesRead);
                return;
            }
            Send(batch);
        }

        private void Send(AnalyticsQueue.Batch batch)
        {
            string body = "[" + string.Join(",", batch.Events) + "]";
            var request = new UnityWebRequest(_endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = RequestTimeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("X-Write-Key", _writeKey);

            _inFlight = true;
            int generation = _queue.Generation;
            request.SendWebRequest().completed += _ => OnSent(request, batch.LinesRead, generation);
        }

        private void OnSent(UnityWebRequest request, int linesSent, int generation)
        {
            long status = request.responseCode;
            bool accepted = request.result == UnityWebRequest.Result.Success;
            // A 400 or 413 means the batch itself is bad, so sending it again would fail forever.
            bool refused = status == 400 || status == 413;

            if (accepted || refused)
            {
                if (refused) Debug.LogWarning($"[Analytics] server refused a batch ({status}): {request.downloadHandler.text}");
                if (generation == _queue.Generation) _queue.RemoveFirst(linesSent);
                _failures = 0;
                _nextAttemptTime = 0f;
            }
            else
            {
                _failures++;
                float delay = Mathf.Min(MaxRetrySeconds, FirstRetrySeconds * Mathf.Pow(2f, _failures - 1));
                _nextAttemptTime = Time.unscaledTime + delay;
            }

            request.Dispose();
            _inFlight = false;
        }
        #endregion

        #region Identity
        private string GetOrCreateInstallId()
        {
            if (!string.IsNullOrEmpty(_installId)) return _installId;

            if (File.Exists(_installIdPath))
            {
                string stored = File.ReadAllText(_installIdPath).Trim();
                if (Guid.TryParse(stored, out _))
                {
                    _installId = stored;
                    return _installId;
                }
            }

            _installId = Guid.NewGuid().ToString();
            Directory.CreateDirectory(Path.GetDirectoryName(_installIdPath));
            File.WriteAllText(_installIdPath, _installId);
            return _installId;
        }
        #endregion
    }
}
