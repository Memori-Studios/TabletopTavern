using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Memori.Steamworks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TJ
{
    /// <summary>
    /// The Editor's side of Remote Balance: builds the stat list from the SquadData assets, compares it with the
    /// server's newest revision and pushes it. The assets are the only source; nothing here writes to them.
    /// </summary>
    public static class RemoteBalanceEditor
    {
        #region Server
        private const string AdminUrl = "https://analytics.memoristudios.com/admin/balance";
        private const string AdminKeyPref = "TabletopTavern.RemoteBalance.AdminKey";
        private const string BakedAssetPath = "Assets/Resources/" + RemoteBalance.BakedResourcePath + ".json";
        private const int RequestTimeoutSeconds = 15;
        // The Steam Uploader refreshes its readout every few seconds; this keeps that from calling the server each time.
        private const double UploadCheckSeconds = 60;
        #endregion

        [Serializable]
        public class ServerRevision
        {
            public int rev;
            public string pushedAt;
            public string note;
            public string minBuild;
            public bool enabled = true;
            public SquadStatsOverrideFile units = new();
        }

        [Serializable]
        private class PushBody
        {
            public string note;
            public string minBuild;
            public SquadStatsOverrideFile units;
        }

        [Serializable]
        private class PushAnswer
        {
            public int rev;
            public int changes;
            public string error;
        }

        public struct Difference
        {
            public string Unit, Stat, Server, Local;
        }

        private static readonly FieldInfo[] StatFields = typeof(SquadStatsOverrideEntry).GetFields()
            .Where(f => f.FieldType == typeof(string) && f.Name != nameof(SquadStatsOverrideEntry.unitName)).ToArray();

        private static double s_uploadCheckedAt = -1;
        private static string s_uploadConcern;

        /// <summary>The push key from balance-setup.sh on the droplet. Kept in EditorPrefs, never in the project or a build.</summary>
        public static string AdminKey
        {
            get => EditorPrefs.GetString(AdminKeyPref, "");
            set => EditorPrefs.SetString(AdminKeyPref, value ?? "");
        }

        public static int BakedRevision
        {
            get
            {
                if (!File.Exists(BakedAssetPath)) return 0;
                try
                {
                    return JsonUtility.FromJson<RemoteBalanceBaked>(File.ReadAllText(BakedAssetPath)).rev;
                }
                catch (Exception)
                {
                    return 0;
                }
            }
        }

        #region Local stats
        /// <summary>Every unit's stats straight from the assets, with no mod applied and the build-only fields left out.</summary>
        public static SquadStatsOverrideFile BuildLocal()
        {
            var stats = new Dictionary<UnitName, SquadStats>();
            var assets = new Dictionary<UnitName, SquadAssets>();
            foreach (SquadData so in Resources.LoadAll<SquadData>("SquadData"))
            {
                stats[so.stats.unitName] = so.stats;
                assets[so.stats.unitName] = so.assets;
            }
            var file = JsonUtility.FromJson<SquadStatsOverrideFile>(SquadStatsOverrideLoader.ExportTemplate(stats, assets));
            for (int i = 0; i < file.overrides.Count; i++)
            {
                SquadStatsOverrideEntry entry = file.overrides[i];
                entry.heroID = entry.unitType = entry.unitSize = entry.race = entry.rarityTier = null;
                file.overrides[i] = entry;
            }
            return file;
        }

        public static List<Difference> Compare(SquadStatsOverrideFile server, SquadStatsOverrideFile local)
        {
            var differences = new List<Difference>();
            Dictionary<string, SquadStatsOverrideEntry> onServer = (server?.overrides ?? new List<SquadStatsOverrideEntry>())
                .Where(e => !string.IsNullOrEmpty(e.unitName)).GroupBy(e => e.unitName).ToDictionary(g => g.Key, g => g.First());
            var seen = new HashSet<string>();
            foreach (SquadStatsOverrideEntry mine in local.overrides)
            {
                seen.Add(mine.unitName);
                if (!onServer.TryGetValue(mine.unitName, out SquadStatsOverrideEntry theirs))
                {
                    differences.Add(new Difference { Unit = mine.unitName, Stat = "(unit)", Server = "", Local = "new" });
                    continue;
                }
                foreach (FieldInfo field in StatFields)
                {
                    string a = (string)field.GetValue(theirs) ?? "";
                    string b = (string)field.GetValue(mine) ?? "";
                    if (a != b) differences.Add(new Difference { Unit = mine.unitName, Stat = field.Name, Server = a, Local = b });
                }
            }
            foreach (string name in onServer.Keys.Where(n => !seen.Contains(n)))
                differences.Add(new Difference { Unit = name, Stat = "(unit)", Server = "present", Local = "removed" });
            return differences;
        }
        #endregion

        #region Server calls
        /// <summary>The server's newest revision, or null with a reason. A server with no revision yet answers rev 0.</summary>
        public static ServerRevision FetchLatest(out string error)
        {
            using UnityWebRequest request = UnityWebRequest.Get(AdminUrl);
            if (!Send(request, out error)) return null;
            try
            {
                ServerRevision latest = JsonUtility.FromJson<ServerRevision>(request.downloadHandler.text);
                latest.units ??= new SquadStatsOverrideFile();
                return latest;
            }
            catch (Exception e)
            {
                error = $"The server's answer was not readable: {e.Message}";
                return null;
            }
        }

        /// <summary>Pushes the stats as a new revision and stamps its number into the project. Returns 0 with a reason on failure.</summary>
        public static int Push(SquadStatsOverrideFile units, string note, string minBuild, out int changes, out string error)
        {
            changes = 0;
            string body = JsonUtility.ToJson(new PushBody { note = note ?? "", minBuild = minBuild ?? "", units = units });
            using var request = new UnityWebRequest(AdminUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body)),
                downloadHandler = new DownloadHandlerBuffer(),
            };
            request.SetRequestHeader("Content-Type", "application/json");
            if (!Send(request, out error)) return 0;
            PushAnswer answer = JsonUtility.FromJson<PushAnswer>(request.downloadHandler.text);
            if (answer == null || answer.rev <= 0)
            {
                error = "The server did not return a revision number.";
                return 0;
            }
            changes = answer.changes;
            WriteBaked(answer.rev);
            s_uploadCheckedAt = -1;
            return answer.rev;
        }

        // A build made after this carries the revision, so it never takes the same stats from the server again.
        public static void WriteBaked(int rev)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BakedAssetPath));
            File.WriteAllText(BakedAssetPath, JsonUtility.ToJson(new RemoteBalanceBaked { rev = rev }));
            AssetDatabase.ImportAsset(BakedAssetPath);
        }

        // Blocks the Editor until the server answers; these calls are small and only run on a button or the upload check.
        private static bool Send(UnityWebRequest request, out string error)
        {
            error = null;
            string key = AdminKey;
            if (string.IsNullOrEmpty(key))
            {
                error = "No push key. Paste the key that balance-setup.sh printed on the droplet.";
                return false;
            }
            request.SetRequestHeader("X-Admin-Key", key);
            request.timeout = RequestTimeoutSeconds;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) Thread.Sleep(10);
            if (request.result == UnityWebRequest.Result.Success) return true;

            string detail = request.downloadHandler != null ? request.downloadHandler.text : "";
            try
            {
                string serverError = JsonUtility.FromJson<PushAnswer>(detail)?.error;
                if (!string.IsNullOrEmpty(serverError)) detail = serverError;
            }
            catch (Exception)
            {
                // Not JSON: show what came back as it is.
            }
            error = request.responseCode switch
            {
                401 => "The server refused the push key.",
                404 => "Remote Balance is not set up on the server yet. Run ./balance-setup.sh on the droplet.",
                0 => $"Could not reach the server: {request.error}",
                _ => $"HTTP {request.responseCode}: {detail}",
            };
            return false;
        }
        #endregion

        #region Upload check
        [InitializeOnLoadMethod]
        private static void RegisterUploadCheck()
        {
            PreShipGate.ExtraConcerns.Remove(UploadConcern);
            PreShipGate.ExtraConcerns.Add(UploadConcern);
        }

        // A build whose stats differ from the server's newest revision would ship numbers the server then overrides
        // or never learns about, so the upload asks for a push first.
        private static string UploadConcern()
        {
            if (s_uploadCheckedAt >= 0 && EditorApplication.timeSinceStartup - s_uploadCheckedAt < UploadCheckSeconds) return s_uploadConcern;
            s_uploadCheckedAt = EditorApplication.timeSinceStartup;
            s_uploadConcern = null;
            if (string.IsNullOrEmpty(AdminKey))
            {
                s_uploadConcern = "Remote Balance: this Editor has no push key, so unit stats were not compared with the server.";
                return s_uploadConcern;
            }
            ServerRevision latest = FetchLatest(out string error);
            if (latest == null)
            {
                s_uploadConcern = $"Remote Balance: could not compare unit stats with the server. {error}";
                return s_uploadConcern;
            }
            int differences = Compare(latest.units, BuildLocal()).Count;
            if (differences > 0)
            {
                s_uploadConcern = $"Remote Balance: {differences} unit stat value(s) differ from the server's revision {latest.rev}. " +
                                  $"Open Tabletop Tavern > Balance > Remote Balance and push with Minimum build {Application.version}, " +
                                  "or players on this build will not match the server.";
            }
            else if (BakedRevision != latest.rev)
            {
                WriteBaked(latest.rev);
            }
            return s_uploadConcern;
        }
        #endregion
    }
}
