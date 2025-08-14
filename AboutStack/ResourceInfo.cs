using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaCalculations
{
    [TransactionAttribute(TransactionMode.Manual)]
    internal class ResourceInfo : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                TaskDialog dialog = new TaskDialog("Resources");
                dialog.MainInstruction = "Available resources for Area Calculations!";
                dialog.MainContent = "Choose your destiny!";
                dialog.CommonButtons = TaskDialogCommonButtons.Close;
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Open LMS", "Access the online Learning Management System");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Open Resources Folder", "Access local instructions and resources");
                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Open Sample File", "Open the Revit sample file as detached copy");

                TaskDialogResult result = dialog.Show();

                if (result == TaskDialogResult.CommandLink1)
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://moodle.ip-arch.com/course/view.php?id=6",
                        UseShellExecute = true
                    });
                }
                else if (result == TaskDialogResult.CommandLink2)
                {
                    System.Diagnostics.Process.Start(new ProcessStartInfo
                    {
                        FileName = @"T:\05.ISO\03.INSTRUCTIONS\05. AREA CALCULATIONS",
                        UseShellExecute = true
                    });
                }
                else if (result == TaskDialogResult.CommandLink3)
                {
                    ModelPath sampleFilePath = ModelPathUtils.ConvertUserVisiblePathToModelPath(@"T:\05.ISO\03.INSTRUCTIONS\05. AREA CALCULATIONS\02. REVIT SAMPLE FILE\AREACALC SAMPLE FILE_2026.rvt");
                    OpenOptions openOptions = new OpenOptions();
                    openOptions.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;
                    UIApplication uiApp = commandData.Application;
                    uiApp.OpenAndActivateDocument(sampleFilePath, openOptions, true);
                }

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
