using System;
using System.Collections.Generic;
using TaleWorlds.Library;

namespace BannerlordCampaignAnalyzer
{
    public static class AnalyzerCommands
    {
        [CommandLineFunctionality.CommandLineArgumentFunction("status", "bca")]
        public static string Status(List<string> args)
        {
            return MethodProfiler.Status();
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("top", "bca")]
        public static string Top(List<string> args)
        {
            var take = 10;
            if (args != null && args.Count > 0)
            {
                int.TryParse(args[0], out take);
            }

            return MethodProfiler.Top(Math.Max(1, take));
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("flush", "bca")]
        public static string Flush(List<string> args)
        {
            MethodProfiler.FlushSummary(force: true);
            return "BCA summary flushed to " + AnalyzerPaths.LogPath;
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("doctor", "bca")]
        public static string Doctor(List<string> args)
        {
            var take = 0;
            if (args != null && args.Count > 0)
            {
                int.TryParse(args[0], out take);
            }

            return PerformanceDoctor.Report(take);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("parties", "bca")]
        public static string Parties(List<string> args)
        {
            var take = 0;
            if (args != null && args.Count > 0)
            {
                int.TryParse(args[0], out take);
            }

            return PerformanceDoctor.ReportParties(take);
        }

        [CommandLineFunctionality.CommandLineArgumentFunction("doctor_clear", "bca")]
        public static string DoctorClear(List<string> args)
        {
            PerformanceDoctor.Clear();
            return "BCA Doctor counters cleared.";
        }
    }
}
