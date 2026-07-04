using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BannerlordCampaignAnalyzer
{
    internal static class AnalyzerLog
    {
        private static readonly object Gate = new object();
        private static string _path = "";
        private static AnalyzerConfig _config = new AnalyzerConfig();

        public static void Initialize(string path, AnalyzerConfig config)
        {
            _path = path;
            Configure(config);
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            EnsureHeader();
        }

        public static void Configure(AnalyzerConfig config)
        {
            _config = config ?? new AnalyzerConfig();
        }

        public static void Profile(string kind, string method, string assembly, double elapsedMs, long count, double avgMs, double maxMs, string note)
        {
            Write(kind, method, assembly, elapsedMs.ToString("0.###", CultureInfo.InvariantCulture),
                count.ToString(CultureInfo.InvariantCulture),
                avgMs.ToString("0.###", CultureInfo.InvariantCulture),
                maxMs.ToString("0.###", CultureInfo.InvariantCulture),
                note);
        }

        public static void Info(string kind, string note)
        {
            Write(kind, "", "", "", "", "", "", note);
        }

        public static void Error(string kind, Exception ex)
        {
            Write(kind, "", "", "", "", "", "", ex.GetType().Name + ": " + ex.Message);
        }

        public static void Close()
        {
            lock (Gate)
            {
                // File.AppendAllText opens and closes per write; nothing persistent to close.
            }
        }

        private static void Write(string kind, string method, string assembly, string elapsedMs, string count, string avgMs, string maxMs, string note)
        {
            try
            {
                lock (Gate)
                {
                    RotateIfNeeded();
                    EnsureHeader();
                    var line = string.Join(",",
                        Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)),
                        Csv(kind),
                        Csv(method),
                        Csv(assembly),
                        Csv(elapsedMs),
                        Csv(count),
                        Csv(avgMs),
                        Csv(maxMs),
                        Csv(note));
                    File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // Diagnostics must never affect gameplay.
            }
        }

        private static void EnsureHeader()
        {
            if (!File.Exists(_path) || new FileInfo(_path).Length == 0)
            {
                File.AppendAllText(_path, "time,kind,method,assembly,elapsed_ms,count,avg_ms,max_ms,note" + Environment.NewLine, Encoding.UTF8);
            }
        }

        private static void RotateIfNeeded()
        {
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                return;
            }

            var info = new FileInfo(_path);
            if (info.Length < _config.RotateAfterBytes)
            {
                return;
            }

            for (int i = _config.RotatedFileCount; i >= 1; i--)
            {
                var older = _path + "." + i;
                var newer = _path + "." + (i + 1);
                if (i == _config.RotatedFileCount && File.Exists(older))
                {
                    File.Delete(older);
                }
                else if (File.Exists(older))
                {
                    File.Move(older, newer);
                }
            }

            File.Move(_path, _path + ".1");
        }

        private static string Csv(string value)
        {
            if (value == null) return "";
            return "\"" + value.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";
        }
    }
}
