using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace BannerlordCampaignAnalyzer
{
    internal static class MethodProfiler
    {
        private sealed class Entry
        {
            public string MethodName = "";
            public string AssemblyName = "";
            public long Count;
            public long ExceptionCount;
            public long TotalTicks;
            public long MaxTicks;
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<MethodBase, Entry> Stats = new Dictionary<MethodBase, Entry>();
        private static AnalyzerConfig _config = new AnalyzerConfig();
        private static long _lastSummaryTimestamp = Stopwatch.GetTimestamp();

        public static void Configure(AnalyzerConfig config)
        {
            _config = config ?? new AnalyzerConfig();
            PerformanceDoctor.Configure(_config);
        }

        public static void Prefix(out long __state)
        {
            __state = Stopwatch.GetTimestamp();
        }

        public static Exception Finalizer(MethodBase __originalMethod, long __state, Exception __exception)
        {
            try
            {
                var elapsedTicks = Stopwatch.GetTimestamp() - __state;
                Record(__originalMethod, elapsedTicks, __exception, null, null);
            }
            catch
            {
                // Never alter the profiled method outcome.
            }

            return __exception;
        }

        public static Exception ContextFinalizer(MethodBase __originalMethod, long __state, Exception __exception, object __instance, object[] __args)
        {
            try
            {
                var elapsedTicks = Stopwatch.GetTimestamp() - __state;
                Record(__originalMethod, elapsedTicks, __exception, __instance, __args);
            }
            catch
            {
                // Never alter the profiled method outcome.
            }

            return __exception;
        }

        public static string Status()
        {
            lock (Gate)
            {
                return "BCA active | patched=" + ProfilerInstaller.PatchedCount
                    + " | tracked=" + Stats.Count
                    + " | log=" + AnalyzerPaths.LogPath;
            }
        }

        public static string Top(int take)
        {
            List<Entry> snapshot;
            lock (Gate)
            {
                snapshot = Stats.Values
                    .OrderByDescending(e => e.TotalTicks)
                    .Take(Math.Max(1, take))
                    .Select(Clone)
                    .ToList();
            }

            if (snapshot.Count == 0)
            {
                return "No profiled calls recorded yet.";
            }

            return string.Join("\n", snapshot.Select((e, i) =>
                (i + 1) + ". " + e.MethodName
                + " | calls=" + e.Count
                + " | avg=" + ToMs(e.TotalTicks / Math.Max(1, e.Count)).ToString("0.###") + "ms"
                + " | max=" + ToMs(e.MaxTicks).ToString("0.###") + "ms"
                + " | asm=" + e.AssemblyName));
        }

        public static void FlushSummary(bool force)
        {
            List<Entry> snapshot;
            lock (Gate)
            {
                if (!force && !ShouldWriteSummary())
                {
                    return;
                }

                _lastSummaryTimestamp = Stopwatch.GetTimestamp();
                snapshot = Stats.Values
                    .OrderByDescending(e => e.TotalTicks)
                    .Take(_config.TopMethodsInSummary)
                    .Select(Clone)
                    .ToList();
            }

            foreach (var entry in snapshot)
            {
                var avgTicks = entry.Count == 0 ? 0 : entry.TotalTicks / entry.Count;
                AnalyzerLog.Profile("summary", entry.MethodName, entry.AssemblyName, ToMs(entry.TotalTicks),
                    entry.Count, ToMs(avgTicks), ToMs(entry.MaxTicks), "exceptions=" + entry.ExceptionCount);
            }
        }

        private static void Record(MethodBase method, long elapsedTicks, Exception exception, object instance, object[] args)
        {
            Entry snapshot;
            double elapsedMs = ToMs(elapsedTicks);
            bool isSpike = elapsedMs >= _config.SpikeLogMilliseconds;
            bool writeSpike;
            bool writeException;

            lock (Gate)
            {
                if (!Stats.TryGetValue(method, out var entry))
                {
                    entry = new Entry
                    {
                        MethodName = FormatMethod(method),
                        AssemblyName = method.DeclaringType?.Assembly.GetName().Name ?? ""
                    };
                    Stats.Add(method, entry);
                }

                entry.Count++;
                entry.TotalTicks += elapsedTicks;
                if (elapsedTicks > entry.MaxTicks)
                {
                    entry.MaxTicks = elapsedTicks;
                }

                if (exception != null)
                {
                    entry.ExceptionCount++;
                }

                snapshot = Clone(entry);
                writeSpike = isSpike || elapsedMs >= _config.MinimumLogMilliseconds;
                writeException = exception != null && _config.LogExceptions;
            }

            var context = ShouldCaptureContext(elapsedMs, isSpike, exception)
                ? ContextFormatter.Build(method, instance, args, _config)
                : "";

            if (_config.DoctorEnabled && (isSpike || writeException))
            {
                PerformanceDoctor.Record(snapshot.MethodName, snapshot.AssemblyName, elapsedMs, isSpike, exception != null, context);
            }

            if (writeSpike)
            {
                var avgTicks = snapshot.Count == 0 ? 0 : snapshot.TotalTicks / snapshot.Count;
                AnalyzerLog.Profile(elapsedMs >= _config.SpikeLogMilliseconds ? "spike" : "slow",
                    snapshot.MethodName, snapshot.AssemblyName, elapsedMs, snapshot.Count,
                    ToMs(avgTicks), ToMs(snapshot.MaxTicks), context);
            }

            if (writeException)
            {
                AnalyzerLog.Profile("exception", snapshot.MethodName, snapshot.AssemblyName, elapsedMs,
                    snapshot.Count, 0, ToMs(snapshot.MaxTicks),
                    exception.GetType().Name + ": " + exception.Message + AppendContext(context));
            }

            FlushSummary(force: false);
        }

        private static bool ShouldWriteSummary()
        {
            var elapsed = ToMs(Stopwatch.GetTimestamp() - _lastSummaryTimestamp) / 1000.0;
            return elapsed >= _config.SummaryEverySeconds;
        }

        private static bool ShouldCaptureContext(double elapsedMs, bool isSpike, Exception exception)
        {
            if (exception != null && _config.IncludeContextOnExceptions)
            {
                return true;
            }

            if (isSpike && _config.IncludeContextOnSpikes)
            {
                return true;
            }

            return _config.IncludeContextOnSlow && elapsedMs >= _config.SlowContextMilliseconds;
        }

        private static string AppendContext(string context)
        {
            return string.IsNullOrWhiteSpace(context) ? "" : " | " + context;
        }

        private static Entry Clone(Entry entry)
        {
            return new Entry
            {
                MethodName = entry.MethodName,
                AssemblyName = entry.AssemblyName,
                Count = entry.Count,
                ExceptionCount = entry.ExceptionCount,
                TotalTicks = entry.TotalTicks,
                MaxTicks = entry.MaxTicks
            };
        }

        private static string FormatMethod(MethodBase method)
        {
            var typeName = method.DeclaringType?.FullName ?? "<no type>";
            return typeName + ":" + method.Name;
        }

        private static double ToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
