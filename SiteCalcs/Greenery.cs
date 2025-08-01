using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Microsoft.Office.Interop.Excel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Floor = Autodesk.Revit.DB.Floor;

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
        public Greenery(Document doc, List<string> plotNames, List<double> plotAreas)
        {
            try
            {
                this.smartRounder = new SmartRound(doc);
                this.aritAsist = new AritmeticAssistant();

                FilteredElementCollector allFloors = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Floors).WhereElementIsNotElementType();
                FilteredElementCollector allWalls = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType();
                FilteredElementCollector allRailings = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StairsRailing).WhereElementIsNotElementType();

                if (plotNames.Count == 1)
                {
                    foreach (Floor floor in allFloors)
                        if (floor.FloorType.LookupParameter("Green Area").AsInteger() == 1)
                        {
                            greenArea = aritAsist.addToArea(greenArea, smartRounder.sqFeetToSqMeters(floor.LookupParameter("Area").AsDouble()));
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
