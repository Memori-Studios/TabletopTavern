using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TabletopTavern.Analytics
{
    // Events waiting to send, one JSON object per line; a line leaves only after the server accepts it.
    // The file is read once and mirrored in memory, so a send never re-reads a large offline backlog.
    public class AnalyticsQueue
    {
        public struct Batch
        {
            public List<string> Events;
            public int LinesRead;
        }

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _path;
        private readonly List<string> _lines;

        public int Count => _lines.Count;

        // Bumped by Clear so a send that started before the clear cannot remove newer events.
        public int Generation { get; private set; }

        public AnalyticsQueue(string path)
        {
            _path = path;
            _lines = ReadLines();
        }

        public void Append(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            using (var stream = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                // A crash mid-write leaves a line with no newline; end it so the next event stays separate.
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    if (stream.ReadByte() != '\n') stream.WriteByte((byte)'\n');
                }
                stream.Seek(0, SeekOrigin.End);
                byte[] bytes = Utf8NoBom.GetBytes(json + "\n");
                stream.Write(bytes, 0, bytes.Length);
            }
            _lines.Add(json);
        }

        // Bad lines count toward LinesRead but are not returned, so removing LinesRead after a send clears them.
        // The first event is always taken, so one event larger than maxBytes cannot block the queue.
        public Batch Peek(int maxEvents, int maxBytes = int.MaxValue)
        {
            var events = new List<string>();
            int linesRead = 0;
            long bytes = 0;
            foreach (string line in _lines)
            {
                if (events.Count >= maxEvents) break;
                bool valid = IsValidJsonObject(line);
                if (valid)
                {
                    long size = Utf8NoBom.GetByteCount(line) + 1;
                    if (events.Count > 0 && bytes + size > maxBytes) break;
                    bytes += size;
                    events.Add(line);
                }
                linesRead++;
            }
            return new Batch { Events = events, LinesRead = linesRead };
        }

        public void RemoveFirst(int lines)
        {
            int removed = Math.Min(Math.Max(lines, 0), _lines.Count);
            _lines.RemoveRange(0, removed);
            if (_lines.Count == 0)
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            else
            {
                File.WriteAllText(_path, string.Join("\n", _lines) + "\n", Utf8NoBom);
            }
        }

        public void Clear()
        {
            if (File.Exists(_path)) File.Delete(_path);
            _lines.Clear();
            Generation++;
        }

        private List<string> ReadLines()
        {
            if (!File.Exists(_path)) return new List<string>();
            return File.ReadAllLines(_path, Utf8NoBom).Where(line => line.Length > 0).ToList();
        }

        private static bool IsValidJsonObject(string line)
        {
            try
            {
                return JToken.Parse(line).Type == JTokenType.Object;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
