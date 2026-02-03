
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Windows;

namespace AreaCalculations
{
    /// <summary>
    /// Interaction logic for AreaCoefficientsWindow.xaml
    /// </summary>
    public partial class AreaCoefficientsWindow : Window
    {
        public Dictionary<string, double> areaCoefficients = new Dictionary<string, double>
        {
            {"ACM", 1 },
            {"ACMGP", 0.8 },
            {"ACMSM", 0.3 }
        };

        public bool overrideBool = false;

        public AreaCoefficientsWindow()
        {
            InitializeComponent();
        }

        private void SetAreaCoefficients(object sender, EventArgs e)
        {
            try
            {
                this.areaCoefficients["ACM"] = Convert.ToDouble(ACM.Text);
                this.areaCoefficients["ACMGP"] = Convert.ToDouble(ACMGP.Text);
                this.areaCoefficients["ACMSM"] = Convert.ToDouble(ACMSM.Text);

                if (overrideCoefficients.IsChecked != null)
                    this.overrideBool = (bool)overrideCoefficients.IsChecked;
            }
            catch { }

            this.Close();
        }
    }
}
