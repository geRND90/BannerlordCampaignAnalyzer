using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace BannerlordCampaignAnalyzer
{
    [XmlRoot("AnalyzerConfig")]
    public sealed class AnalyzerConfig
    {
        public bool Enabled { get; set; } = true;
        public bool AutoPatchCampaignBehaviorTicks { get; set; } = true;
        public bool IncludeTaleWorldsBehaviors { get; set; } = true;
        public bool PatchManualMethods { get; set; } = true;
        public double MinimumLogMilliseconds { get; set; } = 5.0;
        public double SpikeLogMilliseconds { get; set; } = 50.0;
        public int SummaryEverySeconds { get; set; } = 60;
        public int TopMethodsInSummary { get; set; } = 25;
        public long RotateAfterBytes { get; set; } = 5L * 1024L * 1024L;
        public int RotatedFileCount { get; set; } = 5;
        public bool LogExceptions { get; set; } = true;
        public bool WriteLoadedMethodList { get; set; } = true;
        public bool IncludeContextOnSpikes { get; set; } = true;
        public bool IncludeContextOnExceptions { get; set; } = true;
        public bool IncludeContextOnSlow { get; set; } = false;
        public double SlowContextMilliseconds { get; set; } = 25.0;
        public int MaxContextCharacters { get; set; } = 900;
        public int MaxContextObjects { get; set; } = 5;
        public bool DoctorEnabled { get; set; } = true;
        public bool DoctorAutoAlerts { get; set; } = true;
        public int DoctorAlertSpikeCount { get; set; } = 5;
        public double DoctorAlertMinimumMilliseconds { get; set; } = 50.0;
        public int DoctorTopSuspects { get; set; } = 5;

        [XmlArrayItem("string")]
        public List<string> MethodNameContains { get; set; } = new List<string>
        {
            "Tick",
            "Hourly",
            "Daily",
            "Weekly"
        };

        [XmlArrayItem("string")]
        public List<string> AssemblyNameDenyList { get; set; } = new List<string>
        {
            "BannerlordCampaignAnalyzer",
            "0Harmony",
            "Harmony",
            "mscorlib",
            "System",
            "System.Core"
        };

        [XmlArrayItem("string")]
        public List<string> ManualMethods { get; set; } = new List<string>
        {
            "TaleWorlds.CampaignSystem.Campaign:DailyTick",
            "TaleWorlds.CampaignSystem.Campaign:HourlyTick",
            "TaleWorlds.CampaignSystem.Campaign:WeeklyTick"
        };

        public static AnalyzerConfig Load()
        {
            try
            {
                if (File.Exists(AnalyzerPaths.ConfigPath))
                {
                    using (var stream = File.OpenRead(AnalyzerPaths.ConfigPath))
                    {
                        var serializer = new XmlSerializer(typeof(AnalyzerConfig));
                        if (serializer.Deserialize(stream) is AnalyzerConfig config)
                        {
                            return config.Normalize();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AnalyzerLog.Error("config_load_failed", ex);
            }

            var fresh = new AnalyzerConfig().Normalize();
            fresh.Save();
            return fresh;
        }

        public void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AnalyzerPaths.ConfigPath));
            using (var stream = File.Create(AnalyzerPaths.ConfigPath))
            {
                var serializer = new XmlSerializer(typeof(AnalyzerConfig));
                serializer.Serialize(stream, this);
            }
        }

        private AnalyzerConfig Normalize()
        {
            if (MethodNameContains == null) MethodNameContains = new List<string>();
            if (AssemblyNameDenyList == null) AssemblyNameDenyList = new List<string>();
            if (ManualMethods == null) ManualMethods = new List<string>();
            if (MinimumLogMilliseconds < 0) MinimumLogMilliseconds = 0;
            if (SpikeLogMilliseconds < MinimumLogMilliseconds) SpikeLogMilliseconds = MinimumLogMilliseconds;
            if (SummaryEverySeconds < 10) SummaryEverySeconds = 10;
            if (TopMethodsInSummary < 1) TopMethodsInSummary = 1;
            if (RotateAfterBytes < 128 * 1024) RotateAfterBytes = 128 * 1024;
            if (RotatedFileCount < 1) RotatedFileCount = 1;
            if (SlowContextMilliseconds < 0) SlowContextMilliseconds = 0;
            if (MaxContextCharacters < 120) MaxContextCharacters = 120;
            if (MaxContextObjects < 1) MaxContextObjects = 1;
            if (DoctorAlertSpikeCount < 1) DoctorAlertSpikeCount = 1;
            if (DoctorAlertMinimumMilliseconds < 0) DoctorAlertMinimumMilliseconds = 0;
            if (DoctorTopSuspects < 1) DoctorTopSuspects = 1;
            return this;
        }
    }
}
