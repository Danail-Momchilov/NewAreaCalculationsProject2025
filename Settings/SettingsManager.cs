using System;
using System.IO;
using System.Text.Json;

namespace AreaCalculations
{
    public class AreaCalculationsSettings
    {
        public string AreaSchemeId { get; set; }
        public string AreaSchemeName { get; set; }
        public string PhaseId { get; set; }
        public string PhaseName { get; set; }
    }

    public static class SettingsManager
    {
        private static readonly string SettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "AreaCalculations");

        private static readonly string SettingsFile = Path.Combine(SettingsFolder, "settings.json");

        public static AreaCalculationsSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<AreaCalculationsSettings>(json);
                }
            }
            catch { }

            return new AreaCalculationsSettings();
        }

        public static void SaveSettings(AreaCalculationsSettings settings)
        {
            try
            {
                if (!Directory.Exists(SettingsFolder))
                {
                    Directory.CreateDirectory(SettingsFolder);
                }

                JsonSerializerOptions options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(SettingsFile, json);
            }
            catch { }
        }

        public static bool SettingsExist()
        {
            return File.Exists(SettingsFile);
        }
    }
}
