using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Autodesk.Revit.DB;

namespace AreaCalculations
{
    public class AreaCalculationsSettings
    {
        public string AreaSchemeName { get; set; }
        public string PhaseName { get; set; }
    }

    public class ResolvedSettings
    {
        public ElementId AreaSchemeId { get; set; }
        public ElementId PhaseId { get; set; }
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

        public static ResolvedSettings ResolveSettings(Document doc, out string error)
        {
            error = null;

            if (!File.Exists(SettingsFile))
            {
                error = "Моля, първо конфигурирайте настройките (Area Scheme и Phase) чрез бутона 'Settings'.";
                return null;
            }

            AreaCalculationsSettings settings = LoadSettings();

            if (string.IsNullOrEmpty(settings.AreaSchemeName) || string.IsNullOrEmpty(settings.PhaseName))
            {
                error = "Моля, първо конфигурирайте настройките (Area Scheme и Phase) чрез бутона 'Settings'.";
                return null;
            }

            // Resolve AreaScheme by name
            AreaScheme areaScheme = new FilteredElementCollector(doc)
                .OfClass(typeof(AreaScheme))
                .Cast<AreaScheme>()
                .FirstOrDefault(s => s.Name == settings.AreaSchemeName);

            if (areaScheme == null)
            {
                error = $"Area Scheme '{settings.AreaSchemeName}' не е намерен в текущия проект. Моля, преконфигурирайте настройките чрез бутона 'Settings'.";
                return null;
            }

            // Resolve Phase by name
            Phase phase = new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .FirstOrDefault(p => p.Name == settings.PhaseName);

            if (phase == null)
            {
                error = $"Phase '{settings.PhaseName}' не е намерена в текущия проект. Моля, преконфигурирайте настройките чрез бутона 'Settings'.";
                return null;
            }

            return new ResolvedSettings
            {
                AreaSchemeId = areaScheme.Id,
                PhaseId = phase.Id
            };
        }
    }
}
