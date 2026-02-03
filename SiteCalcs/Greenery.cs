using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Floor = Autodesk.Revit.DB.Floor;
using Toposolid = Autodesk.Revit.DB.Toposolid;

namespace AreaCalculations
{
    internal class Greenery
    {
        public double greenArea { get; set; }
        public double greenArea1 { get; set; }
        public double greenArea2 { get; set; }
        public double achievedPercentage { get; set; }
        public double achievedPercentage1 { get; set; }
        public double achievedPercentage2 { get; set; }
        public List<double> greenAreas { get; set; } = new List<double>();
        public List<double> achievedPercentages { get; set; } = new List<double>();
        private SmartRound smartRounder { get; set; }
        private AritmeticAssistant aritAsist { get; set; }
        public string errorReport = "";
        double areaConvert = 10.7639104167096;
        double lengthConvert = 30.48;

        private AreaCalculationsSettings settings { get; set; }
        private ElementId phaseId { get; set; }
        private Dictionary<ElementId, int> phaseOrder { get; set; }

        private void BuildPhaseOrder(Document doc)
        {
            phaseOrder = new Dictionary<ElementId, int>();
            PhaseArray phases = doc.Phases;
            for (int i = 0; i < phases.Size; i++)
            {
                phaseOrder[phases.get_Item(i).Id] = i;
            }
        }

        private bool ElementExistsInPhase(Element element)
        {
            if (phaseId == null) return true;

            ElementId createdPhase = element.CreatedPhaseId;
            ElementId demolishedPhase = element.DemolishedPhaseId;

            // Element must be created in the selected phase or an earlier one
            if (!phaseOrder.ContainsKey(createdPhase) || !phaseOrder.ContainsKey(phaseId))
                return false;

            if (phaseOrder[createdPhase] > phaseOrder[phaseId])
                return false;

            // If demolished, it must be demolished in a phase AFTER the selected one
            if (demolishedPhase != ElementId.InvalidElementId)
            {
                if (!phaseOrder.ContainsKey(demolishedPhase))
                    return false;

                if (phaseOrder[demolishedPhase] <= phaseOrder[phaseId])
                    return false;
            }

            return true;
        }

        private string CheckGreeneryParameters(Document doc, int plotCount)
        {
            string missingParams = "";

            // Check "Green Area" type parameter on Floor types
            FilteredElementCollector floorTypes = new FilteredElementCollector(doc).OfClass(typeof(FloorType));
            bool floorTypeHasGreenArea = floorTypes.Cast<FloorType>().Any(ft => ft.LookupParameter("Green Area") != null);
            if (!floorTypeHasGreenArea)
                missingParams += "Липсва параметър 'Green Area' (Type) за Floor категорията. Моля, заредете го като Type параметър за Floors.\n";

            // Check "Green Area" type parameter on Wall types
            FilteredElementCollector wallTypes = new FilteredElementCollector(doc).OfClass(typeof(WallType));
            bool wallTypeHasGreenArea = wallTypes.Cast<WallType>().Any(wt => wt.LookupParameter("Green Area") != null);
            if (!wallTypeHasGreenArea)
                missingParams += "Липсва параметър 'Green Area' (Type) за Wall категорията. Моля, заредете го като Type параметър за Walls.\n";

            // Check "Green Area" type parameter on Railing types
            FilteredElementCollector railingTypes = new FilteredElementCollector(doc).OfClass(typeof(ElementType)).OfCategory(BuiltInCategory.OST_StairsRailing);
            bool railingTypeHasGreenArea = railingTypes.Cast<ElementType>().Any(rt => rt.LookupParameter("Green Area") != null);
            if (!railingTypeHasGreenArea)
                missingParams += "Липсва параметър 'Green Area' (Type) за Railing категорията. Моля, заредете го като Type параметър за Railings.\n";

            // Check "Green Area" type parameter on Toposolid types
            FilteredElementCollector toposolidTypes = new FilteredElementCollector(doc).OfClass(typeof(ElementType)).OfCategory(BuiltInCategory.OST_Toposolid);
            bool toposolidTypeHasGreenArea = toposolidTypes.Cast<ElementType>().Any(tt => tt.LookupParameter("Green Area") != null);
            if (!toposolidTypeHasGreenArea)
                missingParams += "Липсва параметър 'Green Area' (Type) за Toposolid категорията. Моля, заредете го като Type параметър за Toposolids.\n";

            // For two plots scenario, check "A Instance Area Plot" instance parameter
            if (plotCount == 2)
            {
                // Check on Floors
                FilteredElementCollector floors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType();
                Floor sampleFloor = floors.Cast<Floor>().FirstOrDefault();
                if (sampleFloor != null && sampleFloor.LookupParameter("A Instance Area Plot") == null)
                    missingParams += "Липсва параметър 'A Instance Area Plot' (Instance) за Floor категорията. Моля, заредете го като Instance параметър за Floors.\n";

                // Check on Walls
                FilteredElementCollector walls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType();
                Wall sampleWall = walls.Cast<Wall>().FirstOrDefault();
                if (sampleWall != null && sampleWall.LookupParameter("A Instance Area Plot") == null)
                    missingParams += "Липсва параметър 'A Instance Area Plot' (Instance) за Wall категорията. Моля, заредете го като Instance параметър за Walls.\n";

                // Check on Railings
                FilteredElementCollector railings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing).WhereElementIsNotElementType();
                Railing sampleRailing = railings.Cast<Railing>().FirstOrDefault();
                if (sampleRailing != null && sampleRailing.LookupParameter("A Instance Area Plot") == null)
                    missingParams += "Липсва параметър 'A Instance Area Plot' (Instance) за Railing категорията. Моля, заредете го като Instance параметър за Railings.\n";

                // Check on Toposolids
                FilteredElementCollector toposolids = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Toposolid).WhereElementIsNotElementType();
                Toposolid sampleToposolid = toposolids.Cast<Toposolid>().FirstOrDefault();
                if (sampleToposolid != null && sampleToposolid.LookupParameter("A Instance Area Plot") == null)
                    missingParams += "Липсва параметър 'A Instance Area Plot' (Instance) за Toposolid категорията. Моля, заредете го като Instance параметър за Toposolids.\n";
            }

            return missingParams;
        }

        public Greenery(Document doc, List<string> plotNames, List<double> plotAreas)
        {
            try
            {
                this.smartRounder = new SmartRound(doc);
                this.aritAsist = new AritmeticAssistant();

                // Load settings and build phase order for filtering
                this.settings = SettingsManager.LoadSettings();
                this.phaseId = !string.IsNullOrEmpty(settings.PhaseId) ? new ElementId(long.Parse(settings.PhaseId)) : null;
                BuildPhaseOrder(doc);

                // Validate parameters first
                errorReport = CheckGreeneryParameters(doc, plotNames.Count);
                if (!string.IsNullOrEmpty(errorReport))
                    return;

                // Collect elements and filter by phase
                List<Floor> allFloors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType()
                    .Cast<Floor>().Where(f => ElementExistsInPhase(f)).ToList();
                List<Toposolid> allToposolids = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Toposolid).WhereElementIsNotElementType()
                    .Cast<Toposolid>().Where(t => ElementExistsInPhase(t)).ToList();
                List<Wall> allWalls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType()
                    .Cast<Wall>().Where(w => ElementExistsInPhase(w)).ToList();
                List<Railing> allRailings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing).WhereElementIsNotElementType()
                    .Cast<Railing>().Where(r => ElementExistsInPhase(r)).ToList();

                if (plotNames.Count == 1)
                {
                    foreach (Floor floor in allFloors)
                        if (floor.FloorType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            greenArea = aritAsist.addToArea(greenArea, smartRounder.sqFeetToSqMeters(floor.LookupParameter("Area").AsDouble()));
                        }

                    foreach (Toposolid toposolid in allToposolids)
                    {
                        ElementType toposolidType = doc.GetElement(toposolid.GetTypeId()) as ElementType;
                        if (toposolidType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            greenArea = aritAsist.addToArea(greenArea, smartRounder.sqFeetToSqMeters(toposolid.LookupParameter("Area").AsDouble()));
                        }
                    }

                    foreach (Wall wall in allWalls)
                    {
                        if (wall.WallType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            if ((wall.LookupParameter("Unconnected Height").AsDouble() * lengthConvert) <= 200)
                            {
                                double wallArea = smartRounder.sqFeetToSqMeters(wall.LookupParameter("Area").AsDouble());
                                greenArea = aritAsist.addToArea(greenArea, wallArea);
                            }
                            else
                            {
                                double lengthCm = smartRounder.feetToCentimeters(wall.LookupParameter("Length").AsDouble());
                                double lengthDiv = aritAsist.divideValue(lengthCm, 100);
                                double multiplied = aritAsist.multiplyValues(lengthDiv, 2);
                                
                                greenArea = aritAsist.addToArea(greenArea, multiplied);
                            }
                        }
                    }

                    foreach (Railing railing in allRailings)
                    {
                        ElementId railingTypeId = railing.GetTypeId();
                        ElementType railingType = doc.GetElement(railingTypeId) as ElementType;

                        if (railingType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            if (smartRounder.feetToCentimeters(railingType.LookupParameter("Railing Height").AsDouble()) <= 200)
                            {
                                double lengthCm = smartRounder.feetToCentimeters(railing.LookupParameter("Length").AsDouble());
                                double heightCm = smartRounder.feetToCentimeters(railingType.LookupParameter("Railing Height").AsDouble());
                                double lengthDiv = aritAsist.divideValue(lengthCm, 100);
                                double heightDiv = aritAsist.divideValue(heightCm, 100);
                                double multiplied = aritAsist.multiplyValues(lengthDiv, heightDiv);
                                
                                greenArea = aritAsist.addToArea(greenArea, multiplied);
                            }
                            else
                            {
                                double lengthCm = smartRounder.feetToCentimeters(railing.LookupParameter("Length").AsDouble());
                                double lengthDiv = aritAsist.divideValue(lengthCm, 100);
                                double multiplied = aritAsist.multiplyValues(lengthDiv, 2);
                                
                                greenArea = aritAsist.addToArea(greenArea, multiplied);
                            }
                        }
                    }

                    achievedPercentage = Math.Round(greenArea * 100 / plotAreas[0], 2);
                    greenAreas.Add(greenArea);
                    achievedPercentages.Add(achievedPercentage);
                }

                else if (plotNames.Count == 2)
                {
                    foreach (Floor floor in allFloors)
                    {
                        if (floor.FloorType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            double floorArea = smartRounder.sqFeetToSqMeters(floor.LookupParameter("Area").AsDouble());

                            if (floor.LookupParameter("A Instance Area Plot").AsString() == plotNames[0])
                                greenArea1 = aritAsist.addToArea(greenArea1, floorArea);
                            else if (floor.LookupParameter("A Instance Area Plot").AsString() == plotNames[1])
                                greenArea2 = aritAsist.addToArea(greenArea2, floorArea);
                            else
                                errorReport += $"Плоча с id: {floor.Id} има попълнен параметър A Instance Area Plot, " +
                                    $"чиято стойност не отговаря на нито едно от двете въведени имена за УПИ\n";
                        }
                    }

                    foreach (Toposolid toposolid in allToposolids)
                    {
                        ElementType toposolidType = doc.GetElement(toposolid.GetTypeId()) as ElementType;
                        if (toposolidType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            double toposolidArea = smartRounder.sqFeetToSqMeters(toposolid.LookupParameter("Area").AsDouble());

                            if (toposolid.LookupParameter("A Instance Area Plot").AsString() == plotNames[0])
                                greenArea1 = aritAsist.addToArea(greenArea1, toposolidArea);
                            else if (toposolid.LookupParameter("A Instance Area Plot").AsString() == plotNames[1])
                                greenArea2 = aritAsist.addToArea(greenArea2, toposolidArea);
                            else
                                errorReport += $"Toposolid с id: {toposolid.Id} има попълнен параметър A Instance Area Plot, " +
                                    $"чиято стойност не отговаря на нито едно от двете въведени имена за УПИ\n";
                        }
                    }

                    foreach (Wall wall in allWalls)
                    {
                        if (wall.WallType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            double wallArea = 0;
                            
                            if (wall.LookupParameter("Unconnected Height").AsDouble() * lengthConvert <= 200)
                            {
                                wallArea = smartRounder.sqFeetToSqMeters(wall.LookupParameter("Area").AsDouble());
                            }
                            else
                            {
                                double lengthCm = smartRounder.feetToCentimeters(wall.LookupParameter("Length").AsDouble());
                                double lengthDiv = aritAsist.divideValue(lengthCm, 100);
                                wallArea = aritAsist.multiplyValues(lengthDiv, 2);
                            }
                            
                            if (wall.LookupParameter("A Instance Area Plot").AsString() == plotNames[0])
                                greenArea1 = aritAsist.addToArea(greenArea1, wallArea);
                            else if (wall.LookupParameter("A Instance Area Plot").AsString() == plotNames[1])
                                greenArea2 = aritAsist.addToArea(greenArea2, wallArea);
                            else
                                errorReport += $"Стена с id: {wall.Id} има попълнен параметър A Instance Area Plot, чиято стойност не отговаря на нито едно от двете въведени имена за УПИ\n";
                        }
                    }

                    foreach (Railing railing in allRailings)
                    {
                        ElementId railingTypeId = railing.GetTypeId();
                        ElementType railingType = doc.GetElement(railingTypeId) as ElementType;

                        if (railingType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            double railingArea = 0;
                            
                            if (smartRounder.feetToCentimeters(railingType.LookupParameter("Railing Height").AsDouble()) <= 200)
                            {
                                double lengthCm = smartRounder.feetToCentimeters(railing.LookupParameter("Length").AsDouble());
                                double heightCm = smartRounder.feetToCentimeters(railingType.LookupParameter("Railing Height").AsDouble());
                                double lengthDiv = aritAsist.divideValue(lengthCm, 100);
                                double heightDiv = aritAsist.divideValue(heightCm, 100);
                                railingArea = aritAsist.multiplyValues(lengthDiv, heightDiv);
                            }
                            else
                            {
                                double lengthCm = smartRounder.feetToCentimeters(railing.LookupParameter("Length").AsDouble());
                                double lengthDiv = aritAsist.divideValue(lengthCm, 100);
                                railingArea = aritAsist.multiplyValues(lengthDiv, 2);
                            }
                            
                            if (railing.LookupParameter("A Instance Area Plot").AsString() == plotNames[0])
                                greenArea1 = aritAsist.addToArea(greenArea1, railingArea);
                            else if (railing.LookupParameter("A Instance Area Plot").AsString() == plotNames[1])
                                greenArea2 = aritAsist.addToArea(greenArea2, railingArea);
                            else
                                errorReport += $"Парапет с id: {railing.Id} има попълнен параметър A Instance Area Plot, чиято стойност не отговаря на нито едно от двете въведени имена за УПИ\n";
                        }
                    }

                    achievedPercentage1 = Math.Round(((greenArea1 * 100) / plotAreas[0]), 2);
                    achievedPercentage2 = Math.Round(((greenArea2 * 100) / plotAreas[1]), 2);
                    achievedPercentages.Add(achievedPercentage1);
                    achievedPercentages.Add(achievedPercentage2);
                    greenAreas.Add(greenArea1);
                    greenAreas.Add(greenArea2);
                }
            }
            catch (Exception e)
            {
                TaskDialog.Show("Warning", e.Message);
            }
        }
    }
}
