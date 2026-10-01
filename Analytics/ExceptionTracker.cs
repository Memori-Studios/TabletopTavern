using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Memori.Scenes;
using UnityEngine;

namespace TabletopTavern.Analytics
{
    /// <summary>One exception type at one place in the game's code, the first time a session sees it.</summary>
    public class ExceptionSighting
    {
        public string Signature;
        public string Type;
        public string Where;
        public List<string> Frames = new List<string>();
        public string Message;
        public int SessionSec;
    }

    // Reports each distinct exception once per session, grouped by a signature that survives line moves and path changes.
    public static class ExceptionTracker
    {
        #region Limits
        public const int MaxSignaturesPerSession = 20;
        public const int MaxFramesSent = 5;
        public const int MaxMessageLength = 200;
        public const int RecentKept = 5;
        #endregion

        // Frames from these namespaces are engine or library code; the first frames outside them are the game's.
        private static readonly string[] LibraryPrefixes =
        {
            "UnityEngine.", "UnityEditor.", "Unity.", "System.", "Mono.", "Microsoft.", "Steamworks.", "Newtonsoft.",
            "TMPro.", "Cysharp.", "DG.", "MoreMountains.", "ProjectDawn.", "MagicaCloth2.", "Shapes.", "Gilzoide.",
            "SQLite", "Lofelt.", "Polyperfect.", "MalbersAnimations.", "UnityUIExtensions.",
        };

        private static readonly Regex GenericArity = new Regex(@"`\d+", RegexOptions.Compiled);
        private static readonly Regex GenericArgs = new Regex(@"\[[^\]]*\]", RegexOptions.Compiled);
        private static readonly Regex Lambda = new Regex(@"^(?<cls>.+?)[+.]<>c(?:__DisplayClass[\d_]+)?\.<(?<m>[^>]*)>b__\w+$", RegexOptions.Compiled);
        private static readonly Regex StateMachine = new Regex(@"^(?<cls>.+?)[+.]<(?<m>[^>]+)>d__\d+\.MoveNext$", RegexOptions.Compiled);
        private static readonly Regex LocalFunction = new Regex(@"^(?<cls>.+?)\.<(?<m>[^>]+)>g__\w+\|[\d_]+$", RegexOptions.Compiled);
        private static readonly Regex AngleParts = new Regex(@"<[^>]*>\w*", RegexOptions.Compiled);
        private static readonly Regex WindowsPath = new Regex(@"[A-Za-z]:[\\/][^\s'""]*", RegexOptions.Compiled);
        private static readonly Regex SlashPath = new Regex(@"(?:\.{0,2}/)?(?:[\w.\-]+/){1,}[\w.\-]*", RegexOptions.Compiled);
        private static readonly Regex BackslashPath = new Regex(@"(?:[\w.\- ]+\\){1,}[\w.\-]*", RegexOptions.Compiled);
        private static readonly Regex Quoted = new Regex(@"'[^']*'|""[^""]*""", RegexOptions.Compiled);
        private static readonly Regex Digits = new Regex(@"\d+", RegexOptions.Compiled);

        private static readonly object s_lock = new object();
        private static readonly Dictionary<string, int> s_counts = new Dictionary<string, int>();
        private static readonly List<string> s_recent = new List<string>();
        private static readonly ConcurrentQueue<ExceptionSighting> s_pending = new ConcurrentQueue<ExceptionSighting>();
        private static int s_overflow;
        private static readonly Stopwatch s_clock = new Stopwatch();
        [ThreadStatic] private static bool t_inHandler;

        #region Session state
        /// <summary>The newest distinct signatures this session, oldest first, for the bug report event.</summary>
        public static List<string> RecentSignatures
        {
            get { lock (s_lock) return new List<string>(s_recent); }
        }

        /// <summary>How often each signature fired this session.</summary>
        public static Dictionary<string, int> Counts
        {
            get { lock (s_lock) return new Dictionary<string, int>(s_counts); }
        }

        /// <summary>Sightings dropped because the session already had its limit of distinct signatures.</summary>
        public static int Overflow
        {
            get { lock (s_lock) return s_overflow; }
        }
        #endregion

        #region Install
        /// <summary>Starts listening for this session. Only called where events are really sent, so the Editor console stays quiet by default.</summary>
        public static void Install()
        {
            // Bootstrap calls this once per launch or Play; statics can outlive a Play when domain reload is off.
            ResetSession();
            s_clock.Restart();
            Application.logMessageReceivedThreaded -= OnLogMessage;
            Application.logMessageReceivedThreaded += OnLogMessage;
            AnalyticsPump.Create(SendPending);
        }

        // Unity can call this from any thread and from inside another log call, so it only records and queues.
        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Assert) return;
            if (t_inHandler) return;
            t_inHandler = true;
            try
            {
                Observe(condition, stackTrace, type);
            }
            catch
            {
                // Reporting must never throw back into Unity's logger.
            }
            finally
            {
                t_inHandler = false;
            }
        }

        /// <summary>Counts one exception and queues it for sending if its signature is new this session.</summary>
        public static void Observe(string condition, string stackTrace, LogType type)
        {
            ExceptionSighting sighting = Describe(condition, stackTrace, type);
            lock (s_lock)
            {
                if (s_counts.TryGetValue(sighting.Signature, out int seen))
                {
                    s_counts[sighting.Signature] = seen + 1;
                    return;
                }
                if (s_counts.Count >= MaxSignaturesPerSession)
                {
                    s_overflow++;
                    return;
                }
                s_counts[sighting.Signature] = 1;
                s_recent.Add(sighting.Signature);
                if (s_recent.Count > RecentKept) s_recent.RemoveAt(0);
            }
            // Stopwatch, not Time: Unity's clock throws off the main thread.
            sighting.SessionSec = (int)(s_clock.ElapsedMilliseconds / 1000);
            s_pending.Enqueue(sighting);
        }

        // Runs on the main thread, where the save and the analytics queue are safe to touch.
        private static void SendPending()
        {
            if (s_pending.IsEmpty) return;
            SceneHandler scenes = SceneHandler.InstanceIfExists;
            string state = scenes != null ? scenes.CurrentGameState.ToString() : "Unknown";
            while (s_pending.TryDequeue(out ExceptionSighting sighting))
                GameEventTracker.ExceptionSeen(sighting, state);
        }

        /// <summary>Clears the session's counts. For tests.</summary>
        public static void ResetSession()
        {
            lock (s_lock)
            {
                s_counts.Clear();
                s_recent.Clear();
                s_overflow = 0;
            }
            while (s_pending.TryDequeue(out _)) { }
        }
        #endregion

        #region Signature
        /// <summary>Builds the signature, frames and cleaned message for one logged exception.</summary>
        public static ExceptionSighting Describe(string condition, string stackTrace, LogType type)
        {
            string firstLine = FirstLine(condition);
            string exceptionType = type == LogType.Assert ? "Assert" : ExceptionType(firstLine);
            string message = CleanMessage(MessageAfterType(firstLine));

            var frames = new List<string>();
            foreach (string raw in (stackTrace ?? "").Split('\n'))
            {
                string frame = NormalizeFrame(raw);
                if (frame == null || IsLibraryFrame(frame)) continue;
                if (frames.Count == 0 || frames[frames.Count - 1] != frame) frames.Add(frame);
                if (frames.Count >= MaxFramesSent) break;
            }

            string key = frames.Count > 0
                ? exceptionType + "|" + frames[0] + "|" + (frames.Count > 1 ? frames[1] : "")
                : exceptionType + "|" + message;

            return new ExceptionSighting
            {
                Signature = Fnv1a(key),
                Type = exceptionType,
                Where = frames.Count > 0 ? frames[0] : null,
                Frames = frames,
                Message = message,
            };
        }

        /// <summary>"Namespace.Class.Method" from one stack line in Unity's or IL2CPP's format, or null for a line that is not a frame.</summary>
        public static string NormalizeFrame(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            string s = line.Trim();
            if (s.StartsWith("at ")) s = s.Substring(3);
            if (s.StartsWith("(wrapper") || s.StartsWith("---")) return null;

            int args = s.IndexOf(" (", StringComparison.Ordinal);
            if (args < 0) args = s.IndexOf('(');
            if (args > 0) s = s.Substring(0, args);
            s = s.Trim().Replace(':', '.').Replace('/', '+');
            if (s.Length == 0) return null;

            s = GenericArity.Replace(s, "");
            s = GenericArgs.Replace(s, "");

            Match m = Lambda.Match(s);
            if (!m.Success) m = StateMachine.Match(s);
            if (!m.Success) m = LocalFunction.Match(s);
            if (m.Success) s = m.Groups["cls"].Value + "." + m.Groups["m"].Value;

            s = AngleParts.Replace(s, "").Replace('+', '.');
            while (s.Contains("..")) s = s.Replace("..", ".");
            return s.Trim('.');
        }

        private static bool IsLibraryFrame(string frame)
        {
            foreach (string prefix in LibraryPrefixes)
                if (frame.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int end = text.IndexOf('\n');
            return (end < 0 ? text : text.Substring(0, end)).Trim();
        }

        // "NullReferenceException: Object reference..." gives NullReferenceException; a namespace is dropped.
        private static string ExceptionType(string firstLine)
        {
            int colon = firstLine.IndexOf(':');
            string head = colon > 0 ? firstLine.Substring(0, colon) : firstLine;
            if (head.Length == 0 || head.Contains(" ")) return "Exception";
            int dot = head.LastIndexOf('.');
            return dot >= 0 ? head.Substring(dot + 1) : head;
        }

        private static string MessageAfterType(string firstLine)
        {
            int colon = firstLine.IndexOf(": ", StringComparison.Ordinal);
            return colon > 0 ? firstLine.Substring(colon + 2) : firstLine;
        }

        /// <summary>The message with paths, quoted values and numbers blanked, so it names no player and groups cleanly.</summary>
        public static string CleanMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            string s = WindowsPath.Replace(message, "#");
            s = SlashPath.Replace(s, "#");
            s = BackslashPath.Replace(s, "#");
            s = Quoted.Replace(s, "#");
            s = Digits.Replace(s, "#");
            return s.Length > MaxMessageLength ? s.Substring(0, MaxMessageLength) : s;
        }

        // 32-bit FNV-1a as 8 hex characters: stable across machines and runtimes, unlike string.GetHashCode.
        private static string Fnv1a(string text)
        {
            uint hash = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash *= 16777619;
            }
            return hash.ToString("x8");
        }
        #endregion
    }
}
