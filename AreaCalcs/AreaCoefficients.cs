using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace AreaCalculations
{
    [TransactionAttribute(TransactionMode.Manual)]
    internal class AreaCoefficients : IExternalCommand
    {        
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Resolve settings for current project
                string settingsError;
                ResolvedSettings resolved = SettingsManager.ResolveSettings(doc, out settingsError);
                if (resolved == null)
                {
                    TaskDialog.Show("Настройки", settingsError);
                    return Result.Failed;
                }

                // create and initiate the xaml window
                AreaCoefficientsWindow window = new AreaCoefficientsWindow();
                WindowInteropHelper helper = new WindowInteropHelper(window);
                helper.Owner = commandData.Application.MainWindowHandle;
                window.ShowDialog();

                // create area collect
                AreaCollection areaUpdater = new AreaCollection(doc);

                // check if A Coefficient Multiplied parameter exists on Areas
                Area sampleArea = areaUpdater.areasCollector.FirstOrDefault();
                if (sampleArea != null && sampleArea.LookupParameter("A Coefficient Multiplied") == null)
                {
                    TaskDialog paramError = new TaskDialog("Липсващ параметър");
                    paramError.MainInstruction = "Липсва параметър 'A Coefficient Multiplied' за Area категорията. Моля, заредете го като Instance параметър за Areas.";
                    paramError.Show();
                    return Result.Failed;
                }

                int count;

                // update area coefficients, based on the xaml windows' data
                if (window.overrideBool)
                    count = areaUpdater.updateAreaCoefficientsOverride(window.areaCoefficients);
                else
                    count = areaUpdater.updateAreaCoefficients(window.areaCoefficients);

                // output reports
                TaskDialog report = new TaskDialog("Report");
                if (count > 0)
                    report.MainInstruction = $"Успешно беше обновен параметър 'A Coefficient Multiplied' за {count} 'Area' обекта!";
                else
                    report.MainInstruction = "Не са открити 'Area' обекти с непопълнен параметър 'A Coefficient Multiplied'.";
                report.Show();

                return Result.Succeeded;
            }
            catch (Exception e)
            {
                TaskDialog exceptions = new TaskDialog("Съобщение за грешка");
                exceptions.MainInstruction = $"{e.Message}\n\n {e.ToString()}\n\n {e.InnerException} \n\n {e.GetBaseException()}";
                exceptions.Show();
                return Result.Failed;
            }
        }
    }
}
