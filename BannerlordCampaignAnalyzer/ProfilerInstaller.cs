using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace BannerlordCampaignAnalyzer
{
    internal static class ProfilerInstaller
    {
        private static Harmony? _harmony;
        private static readonly HashSet<MethodBase> PatchedMethods = new HashSet<MethodBase>();

        public static int PatchedCount => PatchedMethods.Count;

        public static void Install(Harmony harmony, AnalyzerConfig config)
        {
            _harmony = harmony;
            MethodProfiler.Configure(config);

            var prefix = new HarmonyMethod(typeof(MethodProfiler).GetMethod(nameof(MethodProfiler.Prefix),
                BindingFlags.Public | BindingFlags.Static));
            var finalizer = new HarmonyMethod(typeof(MethodProfiler).GetMethod(nameof(MethodProfiler.Finalizer),
                BindingFlags.Public | BindingFlags.Static));
            var contextFinalizer = new HarmonyMethod(typeof(MethodProfiler).GetMethod(nameof(MethodProfiler.ContextFinalizer),
                BindingFlags.Public | BindingFlags.Static));

            if (config.AutoPatchCampaignBehaviorTicks)
            {
                PatchCampaignBehaviorTicks(config, prefix, finalizer, contextFinalizer);
            }

            if (config.PatchManualMethods)
            {
                PatchManualMethods(config, prefix, finalizer, contextFinalizer);
            }

            if (config.WriteLoadedMethodList)
            {
                WritePatchedMethods();
            }

            AnalyzerLog.Info("installed", "Patched methods: " + PatchedMethods.Count);
        }

        public static void Uninstall()
        {
            if (_harmony != null)
            {
                _harmony.UnpatchAll(SubModule.HarmonyId);
            }

            PatchedMethods.Clear();
        }

        private static void PatchCampaignBehaviorTicks(AnalyzerConfig config, HarmonyMethod prefix, HarmonyMethod finalizer, HarmonyMethod contextFinalizer)
        {
            var behaviorBase = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviorBase");
            if (behaviorBase == null)
            {
                AnalyzerLog.Info("campaign_behavior_base_missing", "Could not find CampaignBehaviorBase.");
                return;
            }

            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(GetLoadableTypes))
            {
                if (type == null || type.Assembly == typeof(SubModule).Assembly)
                {
                    continue;
                }

                if (!behaviorBase.IsAssignableFrom(type) || type.IsAbstract)
                {
                    continue;
                }

                var assemblyName = type.Assembly.GetName().Name ?? "";
                if (IsDeniedAssembly(config, assemblyName))
                {
                    continue;
                }

                if (!config.IncludeTaleWorldsBehaviors && assemblyName.StartsWith("TaleWorlds.", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var method in methods)
                {
                    if (LooksLikeCampaignTickHandler(config, method))
                    {
                        TryPatch(method, config, prefix, finalizer, contextFinalizer);
                    }
                }
            }
        }

        private static void PatchManualMethods(AnalyzerConfig config, HarmonyMethod prefix, HarmonyMethod finalizer, HarmonyMethod contextFinalizer)
        {
            foreach (var spec in config.ManualMethods.Where(s => !string.IsNullOrWhiteSpace(s)))
            {
                var parts = spec.Split(new[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    AnalyzerLog.Info("manual_method_bad_spec", spec);
                    continue;
                }

                var type = AccessTools.TypeByName(parts[0].Trim());
                if (type == null)
                {
                    AnalyzerLog.Info("manual_type_missing", spec);
                    continue;
                }

                var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(m => string.Equals(m.Name, parts[1].Trim(), StringComparison.Ordinal));

                var patchedAny = false;
                foreach (var method in methods)
                {
                    patchedAny |= TryPatch(method, config, prefix, finalizer, contextFinalizer);
                }

                if (!patchedAny)
                {
                    AnalyzerLog.Info("manual_method_missing", spec);
                }
            }
        }

        private static bool TryPatch(MethodInfo method, AnalyzerConfig config, HarmonyMethod prefix, HarmonyMethod finalizer, HarmonyMethod contextFinalizer)
        {
            try
            {
                if (!CanPatch(method) || PatchedMethods.Contains(method))
                {
                    return false;
                }

                var selectedFinalizer = ShouldUseContextFinalizer(method, config) ? contextFinalizer : finalizer;
                _harmony?.Patch(method, prefix: prefix, finalizer: selectedFinalizer);
                PatchedMethods.Add(method);
                return true;
            }
            catch (Exception ex)
            {
                AnalyzerLog.Info("patch_failed", FormatMethod(method) + " | " + ex.GetType().Name + ": " + ex.Message);
                return false;
            }
        }

        private static bool CanPatch(MethodInfo method)
        {
            if (method == null) return false;
            if (method.IsAbstract) return false;
            if (method.IsGenericMethodDefinition || method.ContainsGenericParameters) return false;
            if (method.IsSpecialName) return false;
            if (method.DeclaringType == null) return false;
            if (method.DeclaringType.Assembly == typeof(SubModule).Assembly) return false;
            if (method.GetMethodBody() == null) return false;
            return true;
        }

        private static bool LooksLikeCampaignTickHandler(AnalyzerConfig config, MethodInfo method)
        {
            if (!CanPatch(method))
            {
                return false;
            }

            return config.MethodNameContains.Any(token =>
                !string.IsNullOrWhiteSpace(token)
                && method.Name.IndexOf(token.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool ShouldUseContextFinalizer(MethodInfo method, AnalyzerConfig config)
        {
            if (!config.IncludeContextOnSpikes && !config.IncludeContextOnSlow && !config.IncludeContextOnExceptions)
            {
                return false;
            }

            foreach (var parameter in method.GetParameters())
            {
                var typeName = parameter.ParameterType.FullName ?? parameter.ParameterType.Name;
                var parameterName = parameter.Name ?? "";

                if (LooksLikeContextType(typeName) || LooksLikeContextParameterName(parameterName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeContextType(string typeName)
        {
            return typeName.IndexOf("MobileParty", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.IndexOf("PartyBase", StringComparison.OrdinalIgnoreCase) >= 0
                || typeName.EndsWith(".Hero", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith(".Clan", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith(".Kingdom", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith(".Settlement", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith(".Town", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith(".Village", StringComparison.OrdinalIgnoreCase)
                || typeName.EndsWith(".Army", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeContextParameterName(string parameterName)
        {
            return parameterName.IndexOf("party", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("hero", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("clan", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("kingdom", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("settlement", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("town", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("village", StringComparison.OrdinalIgnoreCase) >= 0
                || parameterName.IndexOf("army", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDeniedAssembly(AnalyzerConfig config, string assemblyName)
        {
            return config.AssemblyNameDenyList.Any(denied =>
                !string.IsNullOrWhiteSpace(denied)
                && assemblyName.Equals(denied.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null).Cast<Type>();
            }
            catch
            {
                return Enumerable.Empty<Type>();
            }
        }

        private static void WritePatchedMethods()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AnalyzerPaths.PatchedMethodsPath));
                File.WriteAllLines(AnalyzerPaths.PatchedMethodsPath,
                    PatchedMethods
                        .OrderBy(FormatMethod)
                        .Select(m => FormatMethod(m) + " | " + (m.DeclaringType?.Assembly.GetName().Name ?? "")));
            }
            catch (Exception ex)
            {
                AnalyzerLog.Error("write_patched_methods_failed", ex);
            }
        }

        private static string FormatMethod(MethodBase method)
        {
            return (method.DeclaringType?.FullName ?? "<no type>") + ":" + method.Name;
        }
    }
}
