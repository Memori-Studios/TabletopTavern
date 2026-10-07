using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Memori.Scenes;
using TabletopTavern.Analytics;
using UnityEngine;
using UnityEngine.Networking;

namespace TJ
{
    /// <summary>What the server sends and what is saved on disk: a revision number and every unit's stats.</summary>
    [Serializable]
    public class RemoteBalanceFile
    {
        public int rev;
        // True when the server's newest revision is the one this game already holds; no units are sent then.
        public bool unchanged;
        public SquadStatsOverrideFile units = new();
    }

    [Serializable]
    public class RemoteBalanceBaked
    {
        public int rev;
    }

    /// <summary>
    /// Unit stats pushed from the Editor to the analytics server (Tabletop Tavern > Balance > Remote Balance) and
    /// read here once per launch, so a balance change needs no build. The request starts at launch and the first
    /// scene load waits a few seconds for it; the answer is saved on disk and applied over the build's stats through
    /// the mod override loader. With no answer the saved copy is used, and with no copy the build's own stats.
    /// The stats are chosen once and never change during a session.
    /// </summary>
    public static class RemoteBalance
    {
        #region Server
        private const string Url = "https://analytics.memoristudios.com/v1/balance";
        // Written by the Editor window on each push, so a build knows which revision its own stats already are.
        public const string BakedResourcePath = "RemoteBalance/baked";
        public const string ApplyInEditorPref = "TabletopTavern.RemoteBalance.ApplyInEditor";
        #endregion

        #region Tuning
        // How long the first scene load waits for the answer before falling back to the saved copy.
        private const float WaitSeconds = 3f;
        private const int RequestTimeoutSeconds = 10;
        private const string CacheFileName = "balance.json";
        #endregion

        private static RemoteBalanceFile s_saved;
        private static RemoteBalanceFile s_active;
        private static Task s_fetch;
        private static int s_baked = -1;

        // Tests point this at a temp folder so they never touch the real save folder.
        internal static string Folder = Path.Combine(Application.persistentDataPath, "RemoteBalance");
        private static string CachePath => Path.Combine(Folder, CacheFileName);

        /// <summary>The revision this build's own stats match, 0 when nothing was ever pushed.</summary>
        public static int BakedRevision
        {
            get
            {
                if (s_baked >= 0) return s_baked;
                s_baked = 0;
                TextAsset baked = Resources.Load<TextAsset>(BakedResourcePath);
                if (baked != null)
                {
                    try
                    {
                        s_baked = Mathf.Max(0, JsonUtility.FromJson<RemoteBalanceBaked>(baked.text).rev);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[RemoteBalance] The baked revision could not be read: {e.Message}");
                    }
                }
                return s_baked;
            }
        }

        /// <summary>The stat revision in effect this session. Analytics sends it with each run and battle.</summary>
        public static int Revision => s_active != null ? s_active.rev : BakedRevision;

        private static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                // The Editor's assets are the source of the server's stats, so it only reads them back to test this path.
                return UnityEditor.EditorPrefs.GetBool(ApplyInEditorPref, false);
#elif RELEASE
                return true;
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            s_saved = null;
            s_active = null;
            s_fetch = null;
            s_baked = -1;
            SceneHandler.OnBeforeFirstLoad -= Settle;
            if (!Enabled) return;
            s_saved = ReadSaved();
            s_fetch = Fetch();
            SceneHandler.OnBeforeFirstLoad += Settle;
        }

        #region Choosing
        // Awaited before the first scene load. TabletopTavernData reloads its catalogue once every hook has run.
        private static async Task Settle()
        {
            if (s_fetch != null) await Task.WhenAny(s_fetch, Task.Delay(TimeSpan.FromSeconds(WaitSeconds)));
            Choose();
        }

        // An answer that arrives after this is saved for the next launch and changes nothing now.
        internal static void Choose()
        {
            // A saved copy no newer than the build is older tuning; the build's stats win.
            s_active = s_saved != null && s_saved.rev > BakedRevision ? s_saved : null;
            Debug.Log(s_active != null
                ? $"[RemoteBalance] Using revision {s_active.rev} ({s_active.units.overrides.Count} units) over the build's revision {BakedRevision}."
                : $"[RemoteBalance] Using the build's own stats (revision {BakedRevision}).");
        }

        /// <summary>Applies this session's remote stats over the catalogue. Runs before the mods, so a mod still wins.</summary>
        public static void Apply(Dictionary<UnitName, SquadStats> squadStatsDictionary, Dictionary<UnitName, SquadAssets> squadAssetsDictionary,
            Dictionary<Race, List<UnitName>> unitsOfRaceDictionary)
        {
            if (s_active == null) return;
            SquadStatsOverrideLoader.ApplyFile(s_active.units, $"remote balance rev {s_active.rev}", squadStatsDictionary, squadAssetsDictionary, unitsOfRaceDictionary);
        }
        #endregion

        #region Fetching
        private static async Task Fetch()
        {
            try
            {
                string url = $"{Url}?v={UnityWebRequest.EscapeURL(Application.version)}&have={(s_saved != null ? s_saved.rev : 0)}";
                using UnityWebRequest request = UnityWebRequest.Get(url);
                request.SetRequestHeader("X-Write-Key", GameEventTracker.WriteKey);
                request.timeout = RequestTimeoutSeconds;
                var done = new TaskCompletionSource<bool>();
                request.SendWebRequest().completed += _ => done.TrySetResult(true);
                await done.Task;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[RemoteBalance] Could not read the stats: HTTP {request.responseCode} {request.error}");
                    return;
                }
                Store(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteBalance] Could not read the stats: {e.Message}");
            }
        }

        // Takes the server's answer: a new revision replaces the saved copy, revision 0 removes it.
        internal static void Store(string json)
        {
            RemoteBalanceFile file = Parse(json);
            if (file == null || file.unchanged) return;
            try
            {
                if (file.rev <= 0)
                {
                    s_saved = null;
                    if (File.Exists(CachePath)) File.Delete(CachePath);
                    return;
                }
                if (file.units.overrides.Count == 0) return;
                s_saved = file;
                Directory.CreateDirectory(Folder);
                // Written beside and moved into place, so a crash mid-write cannot leave half a file.
                string temp = CachePath + ".tmp";
                File.WriteAllText(temp, json);
                if (File.Exists(CachePath)) File.Delete(CachePath);
                File.Move(temp, CachePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteBalance] Could not save the stats: {e.Message}");
            }
        }

        private static RemoteBalanceFile ReadSaved()
        {
            try
            {
                if (!File.Exists(CachePath)) return null;
                RemoteBalanceFile file = Parse(File.ReadAllText(CachePath));
                if (file != null && file.rev > 0 && file.units.overrides.Count > 0) return file;
                File.Delete(CachePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteBalance] The saved stats could not be read: {e.Message}");
            }
            return null;
        }

        private static RemoteBalanceFile Parse(string json)
        {
            RemoteBalanceFile file;
            try
            {
                file = JsonUtility.FromJson<RemoteBalanceFile>(json);
            }
            catch (Exception)
            {
                return null;
            }
            if (file == null) return null;
            file.units ??= new SquadStatsOverrideFile();
            file.units.overrides ??= new List<SquadStatsOverrideEntry>();
            // Type, size, race and rarity change rosters and prefabs, so they only ever come from the build.
            for (int i = 0; i < file.units.overrides.Count; i++)
            {
                SquadStatsOverrideEntry entry = file.units.overrides[i];
                entry.heroID = entry.unitType = entry.unitSize = entry.race = entry.rarityTier = null;
                file.units.overrides[i] = entry;
            }
            return file;
        }
        #endregion

        #region Tests
        internal static void ResetForTests(string folder, int bakedRevision)
        {
            Folder = folder;
            s_baked = bakedRevision;
            s_saved = null;
            s_active = null;
            s_fetch = null;
        }

        internal static void ReadSavedForTests() => s_saved = ReadSaved();

        internal static void RestoreAfterTests() => ResetForTests(Path.Combine(Application.persistentDataPath, "RemoteBalance"), -1);
        #endregion
    }
}
