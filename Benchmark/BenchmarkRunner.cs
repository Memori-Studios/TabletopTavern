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
    /// Headless-ish benchmark: launch the player with -benchmark, it boots the saved custom battle,
    /// parks the camera, records deployment then the fight, writes CSV + marker tables, and quits.
    /// Args: -benchmark -benchmark-out &lt;dir&gt; -benchmark-label &lt;text&gt; -benchmark-scale &lt;renderScale&gt;
    ///       -benchmark-deploy &lt;seconds&gt; -benchmark-battle &lt;seconds&gt;
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

        private string _outDir, _label;
        private float _deploySeconds, _battleSeconds, _renderScale;
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

        private void Start()
        {
            _label = ArgValue("-benchmark-label", "run");
            _outDir = ArgValue("-benchmark-out", Path.Combine(Application.persistentDataPath, "Benchmarks"));
            _deploySeconds = float.Parse(ArgValue("-benchmark-deploy", "10"), System.Globalization.CultureInfo.InvariantCulture);
            _battleSeconds = float.Parse(ArgValue("-benchmark-battle", "45"), System.Globalization.CultureInfo.InvariantCulture);
            _renderScale = float.Parse(ArgValue("-benchmark-scale", "0"), System.Globalization.CultureInfo.InvariantCulture);
            _hover = HasArg("-benchmark-hover");
            _profileSeconds = float.Parse(ArgValue("-benchmark-profile", "0"), System.Globalization.CultureInfo.InvariantCulture);
            Directory.CreateDirectory(_outDir);
            Log($"benchmark start label={_label} out={_outDir} deploy={_deploySeconds}s battle={_battleSeconds}s scale={_renderScale} hover={_hover}");
            StartCoroutine(Run());
        }

        private void Log(string line)
        {
            Debug.Log("[Benchmark] " + line);
            _log.AppendLine(line);
        }

        private IEnumerator Run()
        {
            // Let the menu finish booting before we replace it with the battle.
            while (!SceneHandler.HasInstance || SceneHandler.Instance.CurrentGameState != GameStateEnum.MainMenu)
                yield return null;
            yield return new WaitForSecondsRealtime(4f);

            // Same path as the Custom Battle button on the main menu.
            PlayerSaveData saveData = SaveDataHandler.LoadPlayerSaveData();
            saveData.customBattle = true;
            SaveDataHandler.SavePlayerSaveData(saveData);
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
            yield return new WaitForSecondsRealtime(2f);

            // Same path as the Load Formation button: pulls both armies from customBattleSaveData.json.
            var load = bm.ArmySpawnManager.LoadBothArmies();
            while (!load.IsCompleted) yield return null;
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

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_renderScale > 0f && urp != null) urp.renderScale = _renderScale;
            Log($"settings: res={Screen.width}x{Screen.height} renderScale={(urp != null ? urp.renderScale : -1f)} msaa={(urp != null ? urp.msaaSampleCount : -1)} shadowDist={(urp != null ? urp.shadowDistance : -1f)} cascades={(urp != null ? urp.shadowCascadeCount : -1)} shadowRes={(urp != null ? urp.mainLightShadowmapResolution : -1)} opaqueTex={(urp != null && urp.supportsCameraOpaqueTexture)} gpu={SystemInfo.graphicsDeviceName} cpu={SystemInfo.processorType}");

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
            File.WriteAllText(Path.Combine(_outDir, _label + "_log.txt"), _log.ToString());
            yield return null;
            Application.Quit();
        }

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
