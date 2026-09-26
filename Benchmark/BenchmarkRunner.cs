using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Memori.SaveData;
using Memori.Scenes;
using Unity.Entities;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TJ.Benchmark
{
    /// <summary>
    /// Headless-ish benchmark: launch the player with -benchmark, it runs one mode, records it, writes CSV + marker
    /// tables, and quits. Modes: battle (saved custom battle, deployment then the fight), menu (main menu at rest),
    /// map (continues the saved campaign, times the load, records the default view and a closer one).
    /// Args: -benchmark -benchmark-saveroot &lt;dir&gt; -benchmark-mode battle|menu|map -benchmark-out &lt;dir&gt;
    ///       -benchmark-label &lt;text&gt; -benchmark-scale &lt;renderScale&gt; -benchmark-deploy &lt;seconds&gt;
    ///       -benchmark-battle &lt;seconds&gt; -benchmark-record &lt;seconds&gt; -benchmark-settle &lt;seconds&gt;
    /// </summary>
    public class BenchmarkRunner : MonoBehaviour
    {
        #region Boot
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!HasArg("-benchmark")) return;
            var go = new GameObject("BenchmarkRunner");
            DontDestroyOnLoad(go);
            go.AddComponent<BenchmarkRunner>();
        }

        // Runs before any scene loads, so a benchmark never reads or writes the player's real saves.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RedirectSaves()
        {
            if (!HasArg("-benchmark")) return;
            string root = ArgValue("-benchmark-saveroot", "");
            if (!string.IsNullOrEmpty(root)) SaveDataHandler.SetSaveRoot(root);
        }

        private static bool HasArg(string name)
        {
            foreach (string a in System.Environment.GetCommandLineArgs())
                if (a == name) return true;
            return false;
        }

        private static string ArgValue(string name, string fallback)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return fallback;
        }
        #endregion

        // How far the map camera moves along its view for the close-up phase, in metres.
        private const float MapZoomDistance = 0.35f;

        private string _outDir, _label, _mode;
        private float _deploySeconds, _battleSeconds, _renderScale, _recordSeconds, _settleSeconds;
        private bool _hover;
        // Seconds of binary profiler log (.raw) to write mid-battle; 0 = off. Load it in the Editor Profiler.
        private float _profileSeconds;
        private readonly StringBuilder _log = new();

        private IEnumerator CaptureProfilerLog(string phase)
        {
            string path = Path.Combine(_outDir, _label + "_" + phase);
            UnityEngine.Profiling.Profiler.logFile = path;
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;
            UnityEngine.Profiling.Profiler.enabled = true;
            Log($"profiler log started -> {path}.raw");
            yield return new WaitForSecondsRealtime(_profileSeconds);
            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = "";
            Log("profiler log stopped");
        }

#if UNITY_STANDALONE_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);
#endif

        // Sweeps the real OS cursor over the middle of the screen so hover, tooltips and UI raycasts run as in play.
        private void MoveCursorAlongSweep(double t)
        {
#if UNITY_STANDALONE_WIN
            float x = Screen.width * (0.5f + 0.32f * Mathf.Sin((float)t * 0.7f));
            float y = Screen.height * (0.5f + 0.22f * Mathf.Sin((float)t * 1.1f + 1f));
            SetCursorPos(Mathf.RoundToInt(x), Mathf.RoundToInt(Screen.height - y));
#endif
        }

        // Same cursor spot in every run, so whatever it hovers is hovered in every build.
        private static void ParkCursor()
        {
#if UNITY_STANDALONE_WIN
            SetCursorPos(Screen.width / 2, Screen.height / 2);
#endif
        }

        private void Start()
        {
            _label = ArgValue("-benchmark-label", "run");
            _outDir = ArgValue("-benchmark-out", Path.Combine(Application.persistentDataPath, "Benchmarks"));
            _mode = ArgValue("-benchmark-mode", "battle");
            _deploySeconds = float.Parse(ArgValue("-benchmark-deploy", "10"), System.Globalization.CultureInfo.InvariantCulture);
            _battleSeconds = float.Parse(ArgValue("-benchmark-battle", "45"), System.Globalization.CultureInfo.InvariantCulture);
            _recordSeconds = float.Parse(ArgValue("-benchmark-record", "45"), System.Globalization.CultureInfo.InvariantCulture);
            _settleSeconds = float.Parse(ArgValue("-benchmark-settle", "10"), System.Globalization.CultureInfo.InvariantCulture);
            _renderScale = float.Parse(ArgValue("-benchmark-scale", "0"), System.Globalization.CultureInfo.InvariantCulture);
            _hover = HasArg("-benchmark-hover");
            _profileSeconds = float.Parse(ArgValue("-benchmark-profile", "0"), System.Globalization.CultureInfo.InvariantCulture);
            Directory.CreateDirectory(_outDir);
            Log($"benchmark start label={_label} mode={_mode} out={_outDir} saveRoot={SaveDataHandler.SaveRoot} deploy={_deploySeconds}s battle={_battleSeconds}s record={_recordSeconds}s settle={_settleSeconds}s scale={_renderScale} hover={_hover}");
            if (string.IsNullOrEmpty(ArgValue("-benchmark-saveroot", "")))
            {
                // Every mode writes playerSaveData, so without a redirect it would overwrite the player's own saves.
                Log("refusing to run: pass -benchmark-saveroot <folder with a copy of the saves>");
                Finish();
                return;
            }
            StartCoroutine(Run());
        }

        private void Log(string line)
        {
            Debug.Log("[Benchmark] " + line);
            _log.AppendLine(line);
        }

        private void Finish()
        {
            File.WriteAllText(Path.Combine(_outDir, _label + "_log.txt"), _log.ToString());
            Application.Quit();
        }

        private IEnumerator Run()
        {
            // Let the menu finish booting before a mode replaces it.
            while (!SceneHandler.HasInstance || SceneHandler.Instance.CurrentGameState != GameStateEnum.MainMenu)
                yield return null;

            switch (_mode)
            {
                case "menu": yield return RunMenu(); break;
                case "map": yield return RunMap(); break;
                default: yield return RunBattle(); break;
            }

            yield return null;
            Finish();
        }

        #region Modes
        private IEnumerator RunBattle()
        {
            yield return new WaitForSecondsRealtime(4f);

            // Same path as the Custom Battle button on the main menu.
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.customBattle = true;
            SaveDataHandler.SavePlayerSaveData(saveData);
            double battleLoadStart = Time.realtimeSinceStartupAsDouble;
            SceneHandler.Instance.SwitchGameState(GameStateEnum.Battle);
            Log("switched to battle");

            // SquadManager.SetUp runs at the end of battlefield generation, which is after the
            // Deployment phase begins; loading armies before it NREs on a null ECB system.
            BattleManager bm = null;
            while (bm == null || bm.GamePhase != GamePhase.Deployment || !SceneHandler.Instance.SceneSetUpComplete)
            {
                bm = FindFirstObjectByType<BattleManager>();
                yield return null;
            }
            Log($"battle loaded in {Time.realtimeSinceStartupAsDouble - battleLoadStart:F2}s");
            yield return new WaitForSecondsRealtime(2f);

            // Same path as the Load Formation button: pulls both armies from customBattleSaveData.json.
            double armyLoadStart = Time.realtimeSinceStartupAsDouble;
            var load = bm.ArmySpawnManager.LoadBothArmies();
            while (!load.IsCompleted) yield return null;
            Log($"army load task finished in {Time.realtimeSinceStartupAsDouble - armyLoadStart:F2}s");
            if (load.IsFaulted) Log("LoadBothArmies faulted: " + load.Exception);
            int units = -1, stable = 0;
            while (stable < 120)
            {
                int now = CountUnits();
                stable = now == units ? stable + 1 : 0;
                units = now;
                yield return null;
            }
            Log($"armies loaded: units={units} entities={CountAll()}");

            ApplyFrameSettings();

            // Fixed camera pose: look at the battlefield centre, let the lerp settle, then freeze the camera script.
            bm.BattleCameraScript.FocusOnPosition(Vector3.zero);
            yield return new WaitForSecondsRealtime(2.5f);
            bm.BattleCameraScript.enabled = false;
            Log($"camera parked at {bm.BattleCamera.transform.position} rot {bm.BattleCamera.transform.eulerAngles}");

            yield return Record("deploy", _deploySeconds, bm);

            bm.StartBattle();
            Log("battle started");
            yield return Record("battle", _battleSeconds, bm);

            Log($"end: units={CountUnits()} phase={bm.GamePhase}");
        }

        private IEnumerator RunMenu()
        {
            yield return WaitForSetUp(GameStateEnum.MainMenu);
            Log($"menu ready {Time.realtimeSinceStartup:F2}s after startup");
            yield return new WaitForSecondsRealtime(_settleSeconds);
            ApplyFrameSettings();
            ParkCursor();
            LogScene("menu");
            yield return Record("menu", _recordSeconds, null);
        }

        private IEnumerator RunMap()
        {
            yield return WaitForSetUp(GameStateEnum.MainMenu);
            Log($"menu ready {Time.realtimeSinceStartup:F2}s after startup");
            yield return new WaitForSecondsRealtime(2f);

            // Same path as the Continue button, which only exists while a campaign is in progress.
            var menu = FindFirstObjectByType<TJ.MainMenu.MainMenu>();
            if (!SaveDataHandler.CampaignSaveExists() || menu == null)
            {
                Log($"map: cannot continue (campaign save={SaveDataHandler.CampaignSaveExists()}, menu found={menu != null})");
                yield break;
            }
            double loadStart = Time.realtimeSinceStartupAsDouble;
            menu.LoadMapScene();
            yield return WaitForSetUp(GameStateEnum.Map);
            Log($"map loaded in {Time.realtimeSinceStartupAsDouble - loadStart:F2}s");

            // Covers the four-second intro fly-in; afterwards the camera only moves on player input.
            yield return new WaitForSecondsRealtime(_settleSeconds);
            ApplyFrameSettings();
            ParkCursor();
            LogScene("map");
            yield return Record("map", _recordSeconds, null);

            var mapCamera = FindFirstObjectByType<TJ.Map.MapCamera>();
            if (mapCamera == null || mapCamera.target == null)
            {
                Log("mapzoom: no map camera target");
                yield break;
            }
            mapCamera.target.position += mapCamera.target.forward * MapZoomDistance;
            yield return new WaitForSecondsRealtime(3f);
            LogScene("mapzoom");
            yield return Record("mapzoom", _recordSeconds, null);
        }

        // The state flips before its scene exists, so wait for the set-up flag as well.
        private static IEnumerator WaitForSetUp(GameStateEnum state)
        {
            while (!SceneHandler.HasInstance || SceneHandler.Instance.CurrentGameState != state || !SceneHandler.Instance.SceneSetUpComplete)
                yield return null;
        }

        private void ApplyFrameSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_renderScale > 0f && urp != null) urp.renderScale = _renderScale;
            var ao = new StringBuilder();
            if (urp != null)
                foreach (ScriptableRendererData data in urp.rendererDataList)
                    if (data != null)
                        foreach (var feature in data.rendererFeatures)
                            if (feature is ScreenSpaceAmbientOcclusion) ao.Append(data.name).Append('=').Append(feature.isActive).Append(' ');
            Log($"settings: res={Screen.width}x{Screen.height} renderScale={(urp != null ? urp.renderScale : -1f)} msaa={(urp != null ? urp.msaaSampleCount : -1)} shadowDist={(urp != null ? urp.shadowDistance : -1f)} cascades={(urp != null ? urp.shadowCascadeCount : -1)} shadowRes={(urp != null ? urp.mainLightShadowmapResolution : -1)} opaqueTex={(urp != null && urp.supportsCameraOpaqueTexture)} ao=[{ao.ToString().Trim()}] gpu={SystemInfo.graphicsDeviceName} cpu={SystemInfo.processorType}");
        }

        private void LogScene(string phase)
        {
            var cameras = new StringBuilder();
            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (cam.isActiveAndEnabled) cameras.Append(cam.name).Append(cam.targetTexture != null ? "(RT)" : "").Append(' ');
            int renderers = 0;
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.enabled && r.gameObject.activeInHierarchy) renderers++;
            Log($"{phase}: cameras=[{cameras.ToString().Trim()}] liveRenderers={renderers}");
        }
        #endregion

        private static int CountUnits()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return 0;
            using var q = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<Unit>());
            return q.CalculateEntityCount();
        }

        private static int CountAll()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            return world == null ? 0 : world.EntityManager.UniversalQuery.CalculateEntityCount();
        }

        #region Recording
        private IEnumerator Record(string phase, float seconds, BattleManager bm)
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var names = new List<string>();
            var recAll = new List<ProfilerRecorder>();
            var recMain = new List<ProfilerRecorder>();
            foreach (var h in handles)
            {
                var d = ProfilerRecorderHandle.GetDescription(h);
                if (d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds) continue;
                try
                {
                    recAll.Add(new ProfilerRecorder(h, 1, ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.StartImmediately));
                    recMain.Add(new ProfilerRecorder(h, 1, ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.StartImmediately));
                    names.Add("[" + d.Category.Name + "] " + d.Name);
                }
                catch { }
            }
            var counters = new Dictionary<string, ProfilerRecorder>
            {
                { "drawCalls", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count") },
                { "batches", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count") },
                { "setPass", ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count") },
                { "triangles", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count") },
                { "shadowCasters", ProfilerRecorder.StartNew(ProfilerCategory.Render, "Shadow Casters Count") },
                { "gcAlloc", ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame") },
            };
            int n = names.Count;
            var sumAll = new double[n]; var sumMain = new double[n]; var maxMain = new double[n];
            string csvPath = Path.Combine(_outDir, _label + "_frames.csv");
            var csv = new StringBuilder(File.Exists(csvPath) ? "" : "phase,frame,t,unscaledDt,cpuFrame,cpuMain,cpuRender,gpu,units,drawCalls,batches,setPass,triangles,shadowCasters,gcAllocBytes\n");
            var timings = new FrameTiming[1];
            var dts = new List<double>(); var gpus = new List<double>(); var mains = new List<double>();
            double start = Time.realtimeSinceStartupAsDouble;
            bool profileStarted = false;
            int frames = 0;
            yield return null; yield return null;
            while (Time.realtimeSinceStartupAsDouble - start < seconds)
            {
                yield return null;
                frames++;
                if (_hover) MoveCursorAlongSweep(Time.realtimeSinceStartupAsDouble - start);
                // 20 s in, both armies are in melee in every benchmark battle.
                if (_profileSeconds > 0 && phase == "battle" && !profileStarted && Time.realtimeSinceStartupAsDouble - start > 20)
                {
                    profileStarted = true;
                    StartCoroutine(CaptureProfilerLog(phase));
                }
                FrameTimingManager.CaptureFrameTimings();
                uint got = FrameTimingManager.GetLatestTimings(1, timings);
                double cpuFrame = got > 0 ? timings[0].cpuFrameTime : -1, cpuMain = got > 0 ? timings[0].cpuMainThreadFrameTime : -1;
                double cpuRender = got > 0 ? timings[0].cpuRenderThreadFrameTime : -1, gpu = got > 0 ? timings[0].gpuFrameTime : -1;
                double dt = Time.unscaledDeltaTime * 1000.0;
                dts.Add(dt); if (gpu >= 0) gpus.Add(gpu); if (cpuMain >= 0) mains.Add(cpuMain);
                for (int i = 0; i < n; i++)
                {
                    double a = recAll[i].LastValue / 1e6, m = recMain[i].LastValue / 1e6;
                    sumAll[i] += a; sumMain[i] += m; if (m > maxMain[i]) maxMain[i] = m;
                }
                csv.Append(phase).Append(',').Append(frames).Append(',').Append((Time.realtimeSinceStartupAsDouble - start).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(dt.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(cpuFrame.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(cpuMain.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(cpuRender.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(gpu.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                   .Append(CountUnits()).Append(',')
                   .Append(counters["drawCalls"].LastValue).Append(',').Append(counters["batches"].LastValue).Append(',').Append(counters["setPass"].LastValue).Append(',')
                   .Append(counters["triangles"].LastValue).Append(',').Append(counters["shadowCasters"].LastValue).Append(',').Append(counters["gcAlloc"].LastValue).Append('\n');
            }
            foreach (var r in recAll) r.Dispose();
            foreach (var r in recMain) r.Dispose();
            foreach (var r in counters.Values) r.Dispose();

            File.AppendAllText(csvPath, csv.ToString());

            var sb = new StringBuilder();
            sb.AppendLine($"label={_label} phase={phase} frames={frames} seconds={seconds} res={Screen.width}x{Screen.height} hover={_hover} units={CountUnits()}");
            sb.AppendLine("unscaledDt " + Stats(dts) + "\ncpuMain    " + Stats(mains) + "\ngpu        " + Stats(gpus));
            var idx = new List<int>(); for (int i = 0; i < n; i++) if (sumMain[i] > 0 || sumAll[i] > 0) idx.Add(i);
            sb.AppendLine("== MAIN THREAD marker total time, top 200 (avg ms/frame, max ms) ==");
            idx.Sort((a, b) => sumMain[b].CompareTo(sumMain[a]));
            int c = 0; foreach (int i in idx) { if (c++ >= 200 || sumMain[i] <= 0) break; sb.AppendLine((sumMain[i] / frames).ToString("F3").PadLeft(9) + " " + maxMain[i].ToString("F2").PadLeft(8) + "  " + names[i]); }
            sb.AppendLine("== ALL THREADS marker total time, top 150 (avg ms/frame summed over threads) ==");
            idx.Sort((a, b) => sumAll[b].CompareTo(sumAll[a]));
            c = 0; foreach (int i in idx) { if (c++ >= 150 || sumAll[i] <= 0) break; sb.AppendLine((sumAll[i] / frames).ToString("F3").PadLeft(9) + "  " + names[i] + "  (main " + (sumMain[i] / frames).ToString("F3") + ")"); }
            File.WriteAllText(Path.Combine(_outDir, _label + "_" + phase + "_markers.txt"), sb.ToString());
            Log($"{phase}: frames={frames} " + Stats(dts));
        }

        private static string Stats(List<double> v)
        {
            if (v.Count == 0) return "n/a";
            var s = new List<double>(v); s.Sort();
            double avg = 0; foreach (double x in s) avg += x; avg /= s.Count;
            return $"avg={avg:F2} p50={s[s.Count / 2]:F2} p90={s[(int)(s.Count * 0.9)]:F2} p99={s[(int)(s.Count * 0.99)]:F2} max={s[s.Count - 1]:F2} ({1000.0 / avg:F1} fps)";
        }
        #endregion
    }
}
