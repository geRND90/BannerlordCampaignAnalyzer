using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.Library;

namespace BannerlordCampaignAnalyzer
{
    internal static class PerformanceDoctor
    {
        private sealed class Suspect
        {
            public string Key = "";
            public string Type = "";
            public string Name = "";
            public string System = "";
            public string Assembly = "";
            public long Events;
            public long Spikes;
            public long Exceptions;
            public double TotalMs;
            public double MaxMs;
            public bool Alerted;
            public readonly Dictionary<string, int> Methods = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Targets = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Settlements = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Leaders = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Clans = new Dictionary<string, int>();
            public readonly Dictionary<string, int> Factions = new Dictionary<string, int>();
        }

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, Suspect> Suspects = new Dictionary<string, Suspect>();
        private static AnalyzerConfig _config = new AnalyzerConfig();

        public static void Configure(AnalyzerConfig config)
        {
            _config = config ?? new AnalyzerConfig();
        }

        public static void Record(string method, string assembly, double elapsedMs, bool isSpike, bool isException, string context)
        {
            if (!_config.DoctorEnabled)
            {
                return;
            }

            Suspect snapshot;
            lock (Gate)
            {
                var info = SuspectInfo.From(method, assembly, context);
                if (!Suspects.TryGetValue(info.Key, out var suspect))
                {
                    suspect = new Suspect
                    {
                        Key = info.Key,
                        Type = info.Type,
                        Name = info.Name,
                        System = info.System,
                        Assembly = assembly
                    };
                    Suspects.Add(info.Key, suspect);
                }

                suspect.Events++;
                suspect.TotalMs += elapsedMs;
                if (elapsedMs > suspect.MaxMs)
                {
                    suspect.MaxMs = elapsedMs;
                }

                if (isSpike)
                {
                    suspect.Spikes++;
                }

                if (isException)
                {
                    suspect.Exceptions++;
                }

                AddCount(suspect.Methods, ShortMethod(method));
                AddCount(suspect.Targets, info.Target);
                AddCount(suspect.Settlements, info.Settlement);
                AddCount(suspect.Leaders, info.Leader);
                AddCount(suspect.Clans, info.Clan);
                AddCount(suspect.Factions, info.Faction);

                snapshot = Clone(suspect);
                if (ShouldAlert(suspect, elapsedMs))
                {
                    suspect.Alerted = true;
                }
                else
                {
                    snapshot = null;
                }
            }

            if (snapshot != null)
            {
                ShowAlert(snapshot);
            }
        }

        public static string Report(int take)
        {
            take = NormalizeTake(take);
            List<Suspect> snapshot;
            lock (Gate)
            {
                snapshot = Suspects.Values
                    .OrderByDescending(Score)
                    .ThenByDescending(s => s.MaxMs)
                    .Take(take)
                    .Select(Clone)
                    .ToList();
            }

            if (snapshot.Count == 0)
            {
                return "BCA Doctor: no suspects recorded yet.";
            }

            var lines = new List<string> { "BCA Doctor - top suspects:" };
            for (int i = 0; i < snapshot.Count; i++)
            {
                lines.Add(FormatSuspect(i + 1, snapshot[i], includeSuggestion: i == 0));
            }

            return string.Join("\n", lines);
        }

        public static string ReportParties(int take)
        {
            take = NormalizeTake(take);
            List<Suspect> snapshot;
            lock (Gate)
            {
                snapshot = Suspects.Values
                    .Where(s => s.Type == "Party")
                    .OrderByDescending(Score)
                    .ThenByDescending(s => s.MaxMs)
                    .Take(take)
                    .Select(Clone)
                    .ToList();
            }

            if (snapshot.Count == 0)
            {
                return "BCA Doctor: no party suspects recorded yet.";
            }

            var lines = new List<string> { "BCA Doctor - party suspects:" };
            for (int i = 0; i < snapshot.Count; i++)
            {
                lines.Add(FormatSuspect(i + 1, snapshot[i], includeSuggestion: true));
            }

            return string.Join("\n", lines);
        }

        public static void Clear()
        {
            lock (Gate)
            {
                Suspects.Clear();
            }
        }

        private static bool ShouldAlert(Suspect suspect, double elapsedMs)
        {
            return _config.DoctorAutoAlerts
                && !suspect.Alerted
                && suspect.Spikes >= _config.DoctorAlertSpikeCount
                && suspect.MaxMs >= _config.DoctorAlertMinimumMilliseconds
                && elapsedMs >= _config.DoctorAlertMinimumMilliseconds;
        }

        private static void ShowAlert(Suspect suspect)
        {
            try
            {
                var message = "[BCA] " + suspect.Type + " suspeito: " + suspect.Name
                    + " | " + suspect.Spikes + " spikes"
                    + " | max " + suspect.MaxMs.ToString("0") + "ms"
                    + " | " + suspect.System;

                var target = TopKey(suspect.Targets);
                if (!string.IsNullOrWhiteSpace(target))
                {
                    message += " | alvo: " + target;
                }

                InformationManager.DisplayMessage(new InformationMessage(message, Color.FromUint(0x00F0C36D)));
                AnalyzerLog.Info("doctor_alert", message + " | suggestion=" + Suggestion(suspect));
            }
            catch
            {
                // Alerts are optional; diagnostics must never affect gameplay.
            }
        }

        private static string FormatSuspect(int index, Suspect suspect, bool includeSuggestion)
        {
            var line = index + ". " + suspect.Type + ": " + suspect.Name
                + " | system=" + suspect.System
                + " | events=" + suspect.Events
                + " | spikes=" + suspect.Spikes
                + " | total=" + suspect.TotalMs.ToString("0.0") + "ms"
                + " | max=" + suspect.MaxMs.ToString("0.0") + "ms";

            line = AppendTop(line, suspect.Methods, "method");
            line = AppendTop(line, suspect.Leaders, "leader");
            line = AppendTop(line, suspect.Clans, "clan");
            line = AppendTop(line, suspect.Factions, "faction");
            line = AppendTop(line, suspect.Targets, "target");
            line = AppendTop(line, suspect.Settlements, "settlement");

            if (includeSuggestion)
            {
                line += "\n   Sugestao: " + Suggestion(suspect);
            }

            return line;
        }

        private static string AppendTop(string current, Dictionary<string, int> values, string label)
        {
            var top = TopKey(values);
            return string.IsNullOrWhiteSpace(top) ? current : current + " | " + label + "=" + top;
        }

        private static string Suggestion(Suspect suspect)
        {
            if (suspect.Type == "Party")
            {
                return "se repetir, chame/redirecione/dissolva e recrie a party; alvo repetido pode indicar rota ou decisao presa.";
            }

            if (suspect.Type == "Settlement")
            {
                return "verifique rebeliao, issues, workshops ou parties presas nesse settlement.";
            }

            if (suspect.Type == "Clan" && suspect.System.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "provavel custo diario de spawn/reposicao de heroes; observar se sempre afeta os mesmos clans.";
            }

            if (suspect.Type == "Clan")
            {
                return "verifique se o cla aparece repetidamente em decisoes, barters, rebelioes ou ticks diarios.";
            }

            return "use bca.top e o CSV para analise detalhada; se o mesmo sistema repetir, pode virar patch especifico.";
        }

        private static double Score(Suspect suspect)
        {
            return suspect.TotalMs + suspect.Spikes * _config.SpikeLogMilliseconds + suspect.Exceptions * 100.0;
        }

        private static int NormalizeTake(int take)
        {
            return take < 1 ? Math.Max(1, _config.DoctorTopSuspects) : take;
        }

        private static Suspect Clone(Suspect source)
        {
            var clone = new Suspect
            {
                Key = source.Key,
                Type = source.Type,
                Name = source.Name,
                System = source.System,
                Assembly = source.Assembly,
                Events = source.Events,
                Spikes = source.Spikes,
                Exceptions = source.Exceptions,
                TotalMs = source.TotalMs,
                MaxMs = source.MaxMs,
                Alerted = source.Alerted
            };

            Copy(source.Methods, clone.Methods);
            Copy(source.Targets, clone.Targets);
            Copy(source.Settlements, clone.Settlements);
            Copy(source.Leaders, clone.Leaders);
            Copy(source.Clans, clone.Clans);
            Copy(source.Factions, clone.Factions);
            return clone;
        }

        private static void Copy(Dictionary<string, int> source, Dictionary<string, int> target)
        {
            foreach (var pair in source)
            {
                target[pair.Key] = pair.Value;
            }
        }

        private static void AddCount(Dictionary<string, int> counts, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (!counts.ContainsKey(value))
            {
                counts[value] = 0;
            }

            counts[value]++;
        }

        private static string TopKey(Dictionary<string, int> values)
        {
            return values.Count == 0
                ? ""
                : values.OrderByDescending(p => p.Value).ThenBy(p => p.Key).First().Key;
        }

        private static string ShortMethod(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                return "";
            }

            var colon = method.LastIndexOf(':');
            return colon >= 0 && colon + 1 < method.Length ? method.Substring(colon + 1) : method;
        }

        private sealed class SuspectInfo
        {
            public string Key = "";
            public string Type = "";
            public string Name = "";
            public string System = "";
            public string Target = "";
            public string Settlement = "";
            public string Leader = "";
            public string Clan = "";
            public string Faction = "";

            public static SuspectInfo From(string method, string assembly, string context)
            {
                var system = ClassifySystem(method, assembly);
                var partyName = Extract(context, "MobileParty", "name");
                if (!string.IsNullOrWhiteSpace(partyName))
                {
                    return new SuspectInfo
                    {
                        Type = "Party",
                        Name = partyName,
                        Key = "Party:" + partyName,
                        System = system,
                        Target = Extract(context, "MobileParty", "target"),
                        Settlement = Extract(context, "MobileParty", "settlement"),
                        Leader = Extract(context, "MobileParty", "leader"),
                        Clan = Extract(context, "MobileParty", "clan"),
                        Faction = Extract(context, "MobileParty", "faction")
                    };
                }

                var settlementName = Extract(context, "Settlement", "name");
                if (!string.IsNullOrWhiteSpace(settlementName))
                {
                    return new SuspectInfo
                    {
                        Type = "Settlement",
                        Name = settlementName,
                        Key = "Settlement:" + settlementName,
                        System = system,
                        Settlement = settlementName,
                        Clan = Extract(context, "Settlement", "ownerClan"),
                        Faction = Extract(context, "Settlement", "faction")
                    };
                }

                var clanName = Extract(context, "Clan", "name");
                if (!string.IsNullOrWhiteSpace(clanName))
                {
                    return new SuspectInfo
                    {
                        Type = "Clan",
                        Name = clanName,
                        Key = "Clan:" + clanName,
                        System = system,
                        Clan = clanName,
                        Faction = Extract(context, "Clan", "kingdom")
                    };
                }

                var heroName = Extract(context, "Hero", "name");
                if (!string.IsNullOrWhiteSpace(heroName))
                {
                    return new SuspectInfo
                    {
                        Type = "Hero",
                        Name = heroName,
                        Key = "Hero:" + heroName,
                        System = system,
                        Leader = heroName,
                        Clan = Extract(context, "Hero", "clan"),
                        Faction = Extract(context, "Hero", "faction")
                    };
                }

                var kingdomName = Extract(context, "Kingdom", "name");
                if (!string.IsNullOrWhiteSpace(kingdomName))
                {
                    return new SuspectInfo
                    {
                        Type = "Kingdom",
                        Name = kingdomName,
                        Key = "Kingdom:" + kingdomName,
                        System = system,
                        Faction = kingdomName
                    };
                }

                var name = ShortMethodName(method);
                return new SuspectInfo
                {
                    Type = "System",
                    Name = name,
                    Key = "System:" + assembly + ":" + method,
                    System = system
                };
            }

            private static string Extract(string context, string objectType, string field)
            {
                if (string.IsNullOrWhiteSpace(context))
                {
                    return "";
                }

                var pattern = "\\." + Regex.Escape(objectType) + "\\{[^}]*\\b" + Regex.Escape(field) + "=([^,;}]+)";
                var match = Regex.Match(context, pattern);
                return match.Success ? match.Groups[1].Value.Trim() : "";
            }

            private static string ClassifySystem(string method, string assembly)
            {
                var value = (method ?? "") + " " + (assembly ?? "");
                if (Contains(value, "RecruitmentBehavior") || Contains(value, "PartyHourlyAiTick") || Contains(value, "PartyAI"))
                {
                    return "Party AI / Recruitment";
                }

                if (Contains(value, "HeroSpawn"))
                {
                    return "Hero / Clan spawn";
                }

                if (Contains(value, "Issues"))
                {
                    return "Issues";
                }

                if (Contains(value, "Rebellions"))
                {
                    return "Rebellions";
                }

                if (Contains(value, "KingdomDecision"))
                {
                    return "Kingdom decisions";
                }

                if (Contains(value, "Barter") || Contains(value, "Diplomacy"))
                {
                    return "Diplomacy / Barter";
                }

                if (Contains(value, "Workshop") || Contains(value, "Town") || Contains(value, "Settlement"))
                {
                    return "Settlement / Economy";
                }

                if (Contains(value, "Clan"))
                {
                    return "Clan daily";
                }

                if (Contains(value, "Title"))
                {
                    return "Titles";
                }

                return ShortMethodName(method);
            }

            private static bool Contains(string value, string token)
            {
                return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private static string ShortMethodName(string method)
            {
                if (string.IsNullOrWhiteSpace(method))
                {
                    return "Unknown";
                }

                var colon = method.LastIndexOf(':');
                return colon >= 0 && colon + 1 < method.Length ? method.Substring(colon + 1) : method;
            }
        }
    }
}
