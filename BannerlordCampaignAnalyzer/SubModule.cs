using System;
using HarmonyLib;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BannerlordCampaignAnalyzer
{
    public sealed class SubModule : MBSubModuleBase
    {
        public const string ModuleId = "BannerlordCampaignAnalyzer";
        public const string HarmonyId = "roger.bannerlord.campaignanalyzer";

        private bool _loaded;

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            AnalyzerPaths.Initialize();
            AnalyzerLog.Initialize(AnalyzerPaths.LogPath, AnalyzerConfig.Load());
            AnalyzerLog.Info("submodule_load", "Campaign Performance Analyzer v0.3.0 loaded.");
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            try
            {
                var config = AnalyzerConfig.Load();
                AnalyzerLog.Configure(config);

                if (config.Enabled)
                {
                    ProfilerInstaller.Install(new Harmony(HarmonyId), config);
                    InformationManager.DisplayMessage(new InformationMessage(
                        "Campaign Performance Analyzer loaded. Logs: Modules/BannerlordCampaignAnalyzer/ModuleData/BCA_Logs",
                        Color.FromUint(0x00B4E197)));
                }
                else
                {
                    AnalyzerLog.Info("disabled", "Analyzer disabled by BCA_Settings.xml.");
                }
            }
            catch (Exception ex)
            {
                AnalyzerLog.Error("install_failed", ex);
            }
        }

        protected override void OnSubModuleUnloaded()
        {
            base.OnSubModuleUnloaded();
            try
            {
                ProfilerInstaller.Uninstall();
                MethodProfiler.FlushSummary(force: true);
                AnalyzerLog.Info("submodule_unload", "Campaign Performance Analyzer unloaded.");
                AnalyzerLog.Close();
            }
            catch
            {
                // Avoid throwing during game shutdown.
            }
        }
    }
}
