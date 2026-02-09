using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace AreaCalculations
{
    public partial class SettingsWindow : Window
    {
        private Document doc;
        private List<AreaScheme> areaSchemes;
        private List<Phase> phases;

        public bool SettingsSaved { get; private set; } = false;

        public SettingsWindow(Document document)
        {
            InitializeComponent();
            doc = document;
            LoadAreaSchemes();
            LoadPhases();
            LoadSavedSettings();
        }

        private void LoadAreaSchemes()
        {
            areaSchemes = new FilteredElementCollector(doc)
                .OfClass(typeof(AreaScheme))
                .Cast<AreaScheme>()
                .OrderBy(scheme => scheme.Name)
                .ToList();

            foreach (AreaScheme scheme in areaSchemes)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = scheme.Name;
                item.Tag = scheme.Id.Value.ToString();
                AreaSchemeComboBox.Items.Add(item);
            }

            if (AreaSchemeComboBox.Items.Count > 0)
                AreaSchemeComboBox.SelectedIndex = 0;
        }

        private void LoadPhases()
        {
            phases = new FilteredElementCollector(doc)
                .OfClass(typeof(Phase))
                .Cast<Phase>()
                .OrderBy(phase => phase.Name)
                .ToList();

            foreach (Phase phase in phases)
            {
                ComboBoxItem item = new ComboBoxItem();
                item.Content = phase.Name;
                item.Tag = phase.Id.Value.ToString();
                PhaseComboBox.Items.Add(item);
            }

            if (PhaseComboBox.Items.Count > 0)
                PhaseComboBox.SelectedIndex = 0;
        }

        private void LoadSavedSettings()
        {
            AreaCalculationsSettings settings = SettingsManager.LoadSettings();

            if (!string.IsNullOrEmpty(settings.AreaSchemeName))
            {
                for (int i = 0; i < AreaSchemeComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = AreaSchemeComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == settings.AreaSchemeName)
                    {
                        AreaSchemeComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (!string.IsNullOrEmpty(settings.PhaseName))
            {
                for (int i = 0; i < PhaseComboBox.Items.Count; i++)
                {
                    ComboBoxItem item = PhaseComboBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == settings.PhaseName)
                    {
                        PhaseComboBox.SelectedIndex = i;
                        break;
                    }
                }
            }
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            ComboBoxItem selectedScheme = AreaSchemeComboBox.SelectedItem as ComboBoxItem;
            ComboBoxItem selectedPhase = PhaseComboBox.SelectedItem as ComboBoxItem;

            if (selectedScheme == null || selectedPhase == null)
            {
                MessageBox.Show("Моля, изберете Area Scheme и Phase.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AreaCalculationsSettings settings = new AreaCalculationsSettings
            {
                AreaSchemeName = selectedScheme.Content.ToString(),
                PhaseName = selectedPhase.Content.ToString()
            };

            SettingsManager.SaveSettings(settings);
            SettingsSaved = true;
            this.Close();
        }
    }
}
