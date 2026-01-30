using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace AreaCalculations
{
    [TransactionAttribute(TransactionMode.Manual)]
    internal class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                SettingsWindow window = new SettingsWindow(doc);
                window.ShowDialog();

                if (window.SettingsSaved)
                {
                    TaskDialog report = new TaskDialog("Settings");
                    report.MainInstruction = "Настройките бяха запазени успешно!";
                    report.Show();
                }

                return Result.Succeeded;
            }
            catch (Exception e)
            {
                TaskDialog exceptions = new TaskDialog("Съобщение за грешка");
                exceptions.MainInstruction = $"{e.Message}\n\n{e.ToString()}";
                exceptions.Show();
                return Result.Failed;
            }
        }
    }
}
