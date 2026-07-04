using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BannerlordCampaignAnalyzer
{
    internal static class ContextFormatter
    {
        public static string Build(MethodBase method, object instance, object[] args, AnalyzerConfig config)
        {
            try
            {
                var chunks = new List<string>();

                AddObjectContext(chunks, "instance", instance, config);

                if (args != null)
                {
                    for (int i = 0; i < args.Length && chunks.Count < config.MaxContextObjects; i++)
                    {
                        AddObjectContext(chunks, "arg" + i, args[i], config);
                    }
                }

                if (chunks.Count == 0)
                {
                    return "";
                }

                var context = "context=" + string.Join(" ; ", chunks);
                return Trim(context, config.MaxContextCharacters);
            }
            catch (Exception ex)
            {
                return "context_error=" + ex.GetType().Name + ":" + ex.Message;
            }
        }

        private static void AddObjectContext(List<string> chunks, string label, object value, AnalyzerConfig config)
        {
            if (value == null || chunks.Count >= config.MaxContextObjects)
            {
                return;
            }

            var direct = DescribeKnownObject(label, value);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                chunks.Add(direct);
                return;
            }

            foreach (var nestedName in new[]
            {
                "MobileParty", "Party", "LeaderParty", "OwnerParty", "TargetParty",
                "Hero", "LeaderHero", "Owner", "OwnerHero", "Clan", "OwnerClan",
                "Kingdom", "Settlement", "CurrentSettlement", "TargetSettlement"
            })
            {
                if (chunks.Count >= config.MaxContextObjects)
                {
                    break;
                }

                var nested = TryGetProperty(value, nestedName);
                if (nested == null || ReferenceEquals(nested, value))
                {
                    continue;
                }

                var nestedDescription = DescribeKnownObject(label + "." + nestedName, nested);
                if (!string.IsNullOrWhiteSpace(nestedDescription))
                {
                    chunks.Add(nestedDescription);
                }
            }
        }

        private static string DescribeKnownObject(string label, object value)
        {
            var type = value.GetType();
            var fullName = type.FullName ?? type.Name;

            if (IsType(fullName, "MobileParty"))
            {
                return DescribeMobileParty(label, value);
            }

            if (IsType(fullName, "PartyBase"))
            {
                return DescribePartyBase(label, value);
            }

            if (IsType(fullName, "Hero"))
            {
                return DescribeHero(label, value);
            }

            if (IsType(fullName, "Clan"))
            {
                return DescribeClan(label, value);
            }

            if (IsType(fullName, "Kingdom"))
            {
                return DescribeKingdom(label, value);
            }

            if (IsType(fullName, "Settlement"))
            {
                return DescribeSettlement(label, value);
            }

            if (IsType(fullName, "Town"))
            {
                return DescribeTown(label, value);
            }

            if (IsType(fullName, "Village"))
            {
                return DescribeVillage(label, value);
            }

            if (IsType(fullName, "Army"))
            {
                return DescribeArmy(label, value);
            }

            return "";
        }

        private static bool IsType(string fullName, string typeName)
        {
            return fullName.EndsWith("." + typeName, StringComparison.Ordinal)
                || fullName.EndsWith("+" + typeName, StringComparison.Ordinal)
                || string.Equals(fullName, typeName, StringComparison.Ordinal);
        }

        private static string DescribeMobileParty(string label, object party)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(party),
                "id=" + SafeString(TryGetProperty(party, "StringId")),
                "leader=" + SafeName(TryGetProperty(party, "LeaderHero")),
                "clan=" + SafeName(TryGetProperty(party, "ActualClan") ?? TryGetProperty(party, "OwnerClan")),
                "faction=" + SafeName(TryGetProperty(party, "MapFaction") ?? TryGetProperty(party, "Owner")),
                "troops=" + RosterCount(TryGetProperty(party, "MemberRoster"), "TotalManCount"),
                "wounded=" + RosterCount(TryGetProperty(party, "MemberRoster"), "TotalWounded"),
                "prisoners=" + RosterCount(TryGetProperty(party, "PrisonRoster"), "TotalManCount"),
                "settlement=" + SafeName(TryGetProperty(party, "CurrentSettlement")),
                "target=" + SafeName(TryGetProperty(party, "TargetSettlement")),
                "army=" + SafeName(TryGetProperty(party, "Army")),
                "pos=" + SafeString(TryGetProperty(party, "Position2D"))
            };

            AddBool(parts, party, "IsActive");
            AddBool(parts, party, "IsLordParty");
            AddBool(parts, party, "IsCaravan");
            AddBool(parts, party, "IsMilitia");
            AddBool(parts, party, "IsBandit");

            return label + ".MobileParty{" + Compact(parts) + "}";
        }

        private static string DescribePartyBase(string label, object partyBase)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(partyBase),
                "owner=" + SafeName(TryGetProperty(partyBase, "Owner")),
                "leader=" + SafeName(TryGetProperty(partyBase, "LeaderHero")),
                "mobile=" + SafeName(TryGetProperty(partyBase, "MobileParty")),
                "troops=" + RosterCount(TryGetProperty(partyBase, "MemberRoster"), "TotalManCount"),
                "prisoners=" + RosterCount(TryGetProperty(partyBase, "PrisonRoster"), "TotalManCount")
            };

            return label + ".PartyBase{" + Compact(parts) + "}";
        }

        private static string DescribeHero(string label, object hero)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(hero),
                "id=" + SafeString(TryGetProperty(hero, "StringId")),
                "clan=" + SafeName(TryGetProperty(hero, "Clan")),
                "party=" + SafeName(TryGetProperty(hero, "PartyBelongedTo")),
                "faction=" + SafeName(TryGetProperty(hero, "MapFaction") ?? TryGetProperty(hero, "SupporterOf"))
            };

            AddBool(parts, hero, "IsAlive");
            AddBool(parts, hero, "IsPrisoner");

            return label + ".Hero{" + Compact(parts) + "}";
        }

        private static string DescribeClan(string label, object clan)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(clan),
                "id=" + SafeString(TryGetProperty(clan, "StringId")),
                "kingdom=" + SafeName(TryGetProperty(clan, "Kingdom")),
                "tier=" + SafeString(TryGetProperty(clan, "Tier")),
                "gold=" + SafeString(TryGetProperty(clan, "Gold"))
            };

            AddBool(parts, clan, "IsEliminated");
            AddBool(parts, clan, "IsBanditFaction");

            return label + ".Clan{" + Compact(parts) + "}";
        }

        private static string DescribeKingdom(string label, object kingdom)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(kingdom),
                "id=" + SafeString(TryGetProperty(kingdom, "StringId")),
                "ruler=" + SafeName(TryGetProperty(kingdom, "Ruler")),
                "clans=" + CountOf(TryGetProperty(kingdom, "Clans")),
                "armies=" + CountOf(TryGetProperty(kingdom, "Armies"))
            };

            return label + ".Kingdom{" + Compact(parts) + "}";
        }

        private static string DescribeSettlement(string label, object settlement)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(settlement),
                "id=" + SafeString(TryGetProperty(settlement, "StringId")),
                "ownerClan=" + SafeName(TryGetProperty(settlement, "OwnerClan")),
                "faction=" + SafeName(TryGetProperty(settlement, "MapFaction") ?? TryGetProperty(settlement, "Owner")),
                "town=" + SafeName(TryGetProperty(settlement, "Town")),
                "village=" + SafeName(TryGetProperty(settlement, "Village")),
                "parties=" + CountOf(TryGetProperty(settlement, "Parties"))
            };

            AddBool(parts, settlement, "IsTown");
            AddBool(parts, settlement, "IsCastle");
            AddBool(parts, settlement, "IsVillage");

            return label + ".Settlement{" + Compact(parts) + "}";
        }

        private static string DescribeTown(string label, object town)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(TryGetProperty(town, "Settlement") ?? town),
                "ownerClan=" + SafeName(TryGetProperty(town, "OwnerClan")),
                "prosperity=" + SafeString(TryGetProperty(town, "Prosperity")),
                "security=" + SafeString(TryGetProperty(town, "Security")),
                "foodStocks=" + SafeString(TryGetProperty(town, "FoodStocks")),
                "garrison=" + RosterCount(TryGetProperty(TryGetProperty(town, "GarrisonParty"), "MemberRoster"), "TotalManCount")
            };

            return label + ".Town{" + Compact(parts) + "}";
        }

        private static string DescribeVillage(string label, object village)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(TryGetProperty(village, "Settlement") ?? village),
                "bound=" + SafeName(TryGetProperty(village, "Bound")),
                "hearth=" + SafeString(TryGetProperty(village, "Hearth")),
                "tradeBound=" + SafeName(TryGetProperty(village, "TradeBound"))
            };

            return label + ".Village{" + Compact(parts) + "}";
        }

        private static string DescribeArmy(string label, object army)
        {
            var parts = new List<string>
            {
                "name=" + SafeName(army),
                "leaderParty=" + SafeName(TryGetProperty(army, "LeaderParty")),
                "kingdom=" + SafeName(TryGetProperty(army, "Kingdom")),
                "parties=" + CountOf(TryGetProperty(army, "Parties"))
            };

            return label + ".Army{" + Compact(parts) + "}";
        }

        private static object TryGetProperty(object value, string name)
        {
            try
            {
                if (value == null) return null;
                var property = value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return property != null && property.GetIndexParameters().Length == 0 ? property.GetValue(value, null) : null;
            }
            catch
            {
                return null;
            }
        }

        private static string SafeName(object value)
        {
            if (value == null) return "";
            var name = TryGetProperty(value, "Name");
            if (name != null)
            {
                return SafeString(name);
            }

            return SafeString(value);
        }

        private static string SafeString(object value)
        {
            if (value == null) return "";
            try
            {
                if (value is float f) return f.ToString("0.##", CultureInfo.InvariantCulture);
                if (value is double d) return d.ToString("0.##", CultureInfo.InvariantCulture);
                if (value is decimal m) return m.ToString("0.##", CultureInfo.InvariantCulture);
                return value.ToString();
            }
            catch
            {
                return "<unreadable>";
            }
        }

        private static string RosterCount(object roster, string propertyName)
        {
            return SafeString(TryGetProperty(roster, propertyName));
        }

        private static string CountOf(object value)
        {
            if (value == null) return "";
            try
            {
                var count = TryGetProperty(value, "Count");
                return SafeString(count);
            }
            catch
            {
                return "";
            }
        }

        private static void AddBool(List<string> parts, object value, string propertyName)
        {
            var prop = TryGetProperty(value, propertyName);
            if (prop is bool b)
            {
                parts.Add(propertyName + "=" + (b ? "true" : "false"));
            }
        }

        private static string Compact(IEnumerable<string> parts)
        {
            return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p) && !p.EndsWith("=", StringComparison.Ordinal)));
        }

        private static string Trim(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max)
            {
                return value;
            }

            return value.Substring(0, Math.Max(0, max - 3)) + "...";
        }
    }
}
