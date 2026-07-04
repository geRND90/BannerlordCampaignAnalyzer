using System.IO;
using TaleWorlds.Library;

namespace BannerlordCampaignAnalyzer
{
    internal static class AnalyzerPaths
    {
        public static string ModuleRoot { get; private set; } = "";
        public static string ModuleData { get; private set; } = "";
        public static string ConfigPath { get; private set; } = "";
        public static string LogDirectory { get; private set; } = "";
        public static string LogPath { get; private set; } = "";
        public static string PatchedMethodsPath { get; private set; } = "";

        public static void Initialize()
        {
            ModuleRoot = Path.Combine(BasePath.Name, "Modules", SubModule.ModuleId);
            ModuleData = Path.Combine(ModuleRoot, "ModuleData");
            ConfigPath = Path.Combine(ModuleData, "BCA_Settings.xml");
            LogDirectory = Path.Combine(ModuleData, "BCA_Logs");
            LogPath = Path.Combine(LogDirectory, "BCA_profile.csv");
            PatchedMethodsPath = Path.Combine(LogDirectory, "BCA_patched_methods.txt");

            Directory.CreateDirectory(ModuleData);
            Directory.CreateDirectory(LogDirectory);
        }
    }
}
