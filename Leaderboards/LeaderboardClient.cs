using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Memori.Analytics;
using Memori.Steamworks;
using Newtonsoft.Json;
using TabletopTavern.Analytics;
using UnityEngine;
using UnityEngine.Networking;

namespace TabletopTavern.Leaderboards
{
    /// <summary>One board the server lists for this season.</summary>
    public sealed class LeaderboardBoard
    {
        public string name;
        public string kind;     // godking (one hero), godking_all or endless
        public int? heroId;
        public string title;
    }

    public sealed class LeaderboardSeason
    {
        public string season;
        public bool open;
        public List<LeaderboardBoard> boards = new();

        public string HeroBoard(int heroId) => boards.FirstOrDefault(b => b.kind == "godking" && b.heroId == heroId)?.name;
        public string AllHeroesBoard => boards.FirstOrDefault(b => b.kind == "godking_all")?.name;
        public string MarchBoard => boards.FirstOrDefault(b => b.kind == "endless")?.name;
    }

    /// <summary>The server's answer to a submission. Rank and entries are for the hero's board (or the March board).</summary>
    public sealed class LeaderboardAnswer
    {
        public string status;   // posted, held, waiting or rejected
        public int? rank;
        public int? entries;
        public int? overallRank;
        public int? overallEntries;
        public string reason;

        public bool Final => status == "posted" || status == "held" || status == "rejected";

        public static int TopPercent(int rank, int entries) => entries > 0 ? Math.Max(1, (int)Math.Ceiling(rank * 100.0 / entries)) : 100;
    }

    /// <summary>
    /// The game's side of the server-written leaderboards. After a Godking win or a Godking March, the run is queued
    /// on disk and sent to the analytics server with a Steam ticket; the server rebuilds the score from the run's
    /// events and writes it to Steam. Nothing here sends a score. The board names come from the server each session.
    /// </summary>
    public static class LeaderboardClient
    {
        #region Server
        private const string BoardsUrl = "https://analytics.memoristudios.com/v1/leaderboards/boards";
        private const string SubmitUrl = "https://analytics.memoristudios.com/v1/leaderboards/submit";
        // Must match TICKET_IDENTITY in Tools/AnalyticsServer/ingest/leaderboards.js, or Steam refuses every ticket.
        public const string TicketIdentity = "tabletoptavern-leaderboards";
        public const string KindGodking = "godking";
        public const string KindEndless = "endless";
        #endregion

        #region Tuning
        // The run's last events are a few seconds behind the submission; this gives the analytics queue a head start.
        private const float FirstSendDelaySeconds = 10f;
        private const float FirstRetrySeconds = 120f;
        private const float MaxRetrySeconds = 60f * 60f;
        // The server stops waiting for a run's events after a day; this keeps a margin for a player who is offline.
        private const double GiveUpAfterHours = 72;
        private const int MaxPending = 20;
        // The server can wait up to 8 seconds for the run's last events before it answers.
        private const int RequestTimeoutSeconds = 30;
        private const string PendingFileName = "pending.json";
        #endregion

        [Serializable]
        private sealed class Pending
        {
            public string runId;
            public string kind;
            public string queuedUtc;
            public int attempts;
            public string nextUtc;
        }

        private static readonly Dictionary<string, LeaderboardAnswer> s_answers = new();
        private static List<Pending> s_pending;
        private static Task<LeaderboardSeason> s_boards;
        private static bool s_pumping;
        private static bool s_busy;
        private static float s_nextLook;

        /// <summary>Raised on the main thread with (runId, kind, answer) each time the server answers.</summary>
        public static event Action<string, string, LeaderboardAnswer> Answered;

        // Tests point this at a temp folder so they never touch the real save folder.
        internal static string Folder = Path.Combine(Application.persistentDataPath, "Leaderboards");
        private static string PendingPath => Path.Combine(Folder, PendingFileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            s_answers.Clear();
            s_pending = null;
            s_boards = null;
            s_busy = false;
            // Runs from an earlier session that the server has not answered yet go out once Steam is up.
            if (File.Exists(PendingPath)) StartPump();
        }

        #region Boards
        /// <summary>This season's boards, fetched once per session. Null when the server cannot be reached.</summary>
        public static Task<LeaderboardSeason> GetBoardsAsync()
        {
            if (s_boards == null || (s_boards.IsCompleted && s_boards.Result == null)) s_boards = FetchBoards();
            return s_boards;
        }

        private static async Task<LeaderboardSeason> FetchBoards()
        {
            using UnityWebRequest request = UnityWebRequest.Get(BoardsUrl);
            request.SetRequestHeader("X-Write-Key", GameEventTracker.WriteKey);
            request.timeout = RequestTimeoutSeconds;
            await Send(request);
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[Leaderboards] Could not read the board list: HTTP {request.responseCode} {request.error}");
                return null;
            }
            try
            {
                return JsonConvert.DeserializeObject<LeaderboardSeason>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboards] The board list was not readable: {e.Message}");
                return null;
            }
        }
        #endregion

        #region Submitting
        /// <summary>The newest answer for a run this session, if the server has answered.</summary>
        public static bool TryGetAnswer(string runId, string kind, out LeaderboardAnswer answer) =>
            s_answers.TryGetValue(Key(runId, kind), out answer);

        /// <summary>True when the run was queued this session or before, so an answer will come.</summary>
        public static bool Expecting(string runId, string kind) =>
            s_answers.ContainsKey(Key(runId, kind)) || Load().Any(p => p.runId == runId && p.kind == kind);

        /// <summary>Queues a finished run for the leaderboard. Skipped when this build sends no run events.</summary>
        public static void Submit(string runId, string kind)
        {
            try
            {
                if (!Guid.TryParse(runId, out _)) return;
#if UNITY_EDITOR
                // The Editor runs as Steam's test app, whose tickets the server cannot use, and sends editor-tagged events.
                Debug.Log($"[Leaderboards] Editor: would send run {runId} for the {kind} board.");
                return;
#else
                // The server checks the run's own events, so without them there is nothing it could check.
                if (!GameEventTracker.SendsRunEvents || !SteamStatic.IsSteamRunning()) return;
                List<Pending> pending = Load();
                if (pending.Any(p => p.runId == runId && p.kind == kind)) return;
                DateTime now = DateTime.UtcNow;
                pending.Add(new Pending { runId = runId, kind = kind, queuedUtc = Stamp(now), nextUtc = Stamp(now.AddSeconds(FirstSendDelaySeconds)) });
                while (pending.Count > MaxPending) pending.RemoveAt(0);
                Save();
                // Moves the run's events along now rather than after any analytics backoff.
                AnalyticsService.Flush();
                StartPump();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboards] Could not queue run {runId}: {e.Message}");
            }
        }

        private static void StartPump()
        {
            if (s_pumping) return;
            s_pumping = true;
            AnalyticsPump.Create(Tick);
        }

        private static void Tick()
        {
            if (s_busy || Time.unscaledTime < s_nextLook) return;
            s_nextLook = Time.unscaledTime + 1f;
            if (!SteamStatic.IsSteamRunning()) return;
            List<Pending> pending = Load();
            if (pending.Count == 0) return;

            DateTime now = DateTime.UtcNow;
            int stale = pending.RemoveAll(p => now - Parse(p.queuedUtc) > TimeSpan.FromHours(GiveUpAfterHours));
            if (stale > 0) Save();
            Pending due = pending.FirstOrDefault(p => Parse(p.nextUtc) <= now);
            if (due == null) return;
            s_busy = true;
            _ = SendOne(due);
        }

        private static async Task SendOne(Pending item)
        {
            try
            {
                using SteamStatic.WebApiTicket ticket = await SteamStatic.GetWebApiTicketAsync(TicketIdentity);
                if (ticket == null)
                {
                    Retry(item);
                    return;
                }

                string body = JsonConvert.SerializeObject(new Dictionary<string, string>
                {
                    { "runId", item.runId },
                    { "kind", item.kind },
                    { "installId", GameEventTracker.InstallId },
                    { "ticket", ticket.Hex },
                });
                using var request = new UnityWebRequest(SubmitUrl, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = RequestTimeoutSeconds,
                };
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("X-Write-Key", GameEventTracker.WriteKey);
                await Send(request);

                long code = request.responseCode;
                if (request.result == UnityWebRequest.Result.Success)
                {
                    LeaderboardAnswer answer = JsonConvert.DeserializeObject<LeaderboardAnswer>(request.downloadHandler.text);
                    if (answer?.status == null) throw new Exception("the answer had no status");
                    s_answers[Key(item.runId, item.kind)] = answer;
                    if (answer.Final) Remove(item);
                    else Retry(item);
                    Debug.Log($"[Leaderboards] Run {item.runId} ({item.kind}): {answer.status}{(answer.rank.HasValue ? $", #{answer.rank} of {answer.entries}" : "")}");
                    Notify(item, answer);
                }
                else if (code == 400 || code == 401)
                {
                    // The server will never take this request; keeping it would only retry forever.
                    Debug.LogWarning($"[Leaderboards] The server refused run {item.runId} (HTTP {code}): {request.downloadHandler.text}");
                    Remove(item);
                }
                else
                {
                    // Offline, a refused ticket (403), rate limited, or the server not open yet: it waits and tries again.
                    Debug.LogWarning($"[Leaderboards] Sending run {item.runId} failed (HTTP {code} {request.error}); it is tried again later.");
                    Retry(item);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboards] Sending run {item.runId} failed: {e.Message}");
                Retry(item);
            }
            finally
            {
                s_busy = false;
            }
        }

        private static void Notify(Pending item, LeaderboardAnswer answer)
        {
            try
            {
                Answered?.Invoke(item.runId, item.kind, answer);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboards] An Answered listener failed: {e.Message}");
            }
        }

        private static void Retry(Pending item)
        {
            item.attempts++;
            double wait = Math.Min(MaxRetrySeconds, FirstRetrySeconds * Math.Pow(2, item.attempts - 1));
            item.nextUtc = Stamp(DateTime.UtcNow.AddSeconds(wait));
            Save();
        }

        private static void Remove(Pending item)
        {
            Load().Remove(item);
            Save();
        }
        #endregion

        #region Pending file
        private static List<Pending> Load()
        {
            if (s_pending != null) return s_pending;
            s_pending = new List<Pending>();
            try
            {
                if (File.Exists(PendingPath))
                    s_pending = JsonConvert.DeserializeObject<List<Pending>>(File.ReadAllText(PendingPath)) ?? new List<Pending>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboards] The waiting runs could not be read and were dropped: {e.Message}");
            }
            return s_pending;
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                if (s_pending == null || s_pending.Count == 0)
                {
                    if (File.Exists(PendingPath)) File.Delete(PendingPath);
                    return;
                }
                // Written beside and moved into place, so a crash mid-write cannot leave half a file.
                string temp = PendingPath + ".tmp";
                File.WriteAllText(temp, JsonConvert.SerializeObject(s_pending));
                if (File.Exists(PendingPath)) File.Delete(PendingPath);
                File.Move(temp, PendingPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Leaderboards] Could not save the waiting runs: {e.Message}");
            }
        }
        #endregion

        #region Helpers
        private static string Key(string runId, string kind) => $"{runId}:{kind}";
        private static string Stamp(DateTime utc) => utc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);

        private static DateTime Parse(string stamp) =>
            DateTime.TryParse(stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime t)
                ? t.ToUniversalTime()
                : DateTime.MinValue;

        private static Task Send(UnityWebRequest request)
        {
            var done = new TaskCompletionSource<bool>();
            request.SendWebRequest().completed += _ => done.TrySetResult(true);
            return done.Task;
        }
        #endregion

        #region Tests
        internal static void ResetForTests(string folder)
        {
            Folder = folder;
            s_pending = null;
            s_answers.Clear();
        }

        internal static IReadOnlyList<(string RunId, string Kind, int Attempts)> PendingForTests() =>
            Load().Select(p => (p.runId, p.kind, p.attempts)).ToList();

        internal static void QueueForTests(string runId, string kind)
        {
            Load().Add(new Pending { runId = runId, kind = kind, queuedUtc = Stamp(DateTime.UtcNow), nextUtc = Stamp(DateTime.UtcNow) });
            Save();
        }

        internal static void RetryForTests(string runId)
        {
            Retry(Load().First(p => p.runId == runId));
        }
        #endregion
    }
}
