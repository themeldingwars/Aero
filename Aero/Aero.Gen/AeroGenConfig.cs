using System.Data.SqlTypes;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aero.Gen
{
    public class AeroGenConfig
    {
        public static string Prefix         = "Aero_";
        public        bool   Enabled        = true;
        public        bool   BoundsCheck    = true;
        public        bool   DiagLogging    = true;
        public        bool   LogReadsWrites = true; // Needs DiagLogging to be enabled too

        public static AeroGenConfig Load(AnalyzerConfigOptions configOptions)
        {
            var config = new AeroGenConfig();
            LoadConfigBool(configOptions, "Enabled", ref config.Enabled);
            LoadConfigBool(configOptions, "BoundsCheck", ref config.BoundsCheck);
            LoadConfigBool(configOptions, "DiagLogging", ref config.DiagLogging);
            LoadConfigBool(configOptions, "LogReadsWrites", ref config.LogReadsWrites);

            return config;
        }

        private static void LoadConfigBool(AnalyzerConfigOptions configOptions, string name, ref bool value)
        {
            if (configOptions.TryGetValue($"{Prefix}{name}".ToLower(), out string str)) {
                if (bool.TryParse(str, out bool val)) {
                    value = val;
                }
            }
        }
    }
}