using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Office.Interop.Excel;
using Worksheet = Microsoft.Office.Interop.Excel.Worksheet;
using Borders = Microsoft.Office.Interop.Excel.Borders;
using Workbook = Microsoft.Office.Interop.Excel.Workbook;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.TextFormatting;
using static System.Net.Mime.MediaTypeNames;
using Document = Autodesk.Revit.DB.Document;
using Range = Microsoft.Office.Interop.Excel.Range;
using Regex = System.Text.RegularExpressions.Regex;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace AreaCalculations
{
    internal class AreaDictionary
    {
        private readonly double areaConvert = 10.7639104167096;
        private readonly double lengthConvert = 30.48;
        private SmartRound smartRounder { get; set; }
        private AritmeticAssistant aritAsist { get; set; }
        public string errorMessage { get; set; }
        public Dictionary<string, Dictionary<string, List<Area>>> AreasOrganizer { get; set; }
        public List<string> plotNames { get; set; }
        public Dictionary<string, Dictionary<string, double>> propertyCommonAreas { get; set; }
        public Dictionary<string, Dictionary<string, double>> propertyCommonAreasSpecial { get; set; }
        public Dictionary<string, Dictionary<string, double>> propertyCommonAreasAll { get; set; }
        public Dictionary<string, Dictionary<string, double>> propertyIndividualAreas { get; set; }
        public Dictionary<string, double> plotAreasImp { get; set; } // IMPORTANT!!! STORING DATA IN IMPERIAL
        public Dictionary<string, List<string>> plotProperties { get; set; }
        public Dictionary<string, double> plotBuildAreas { get; set; }
        public Dictionary<string, double> plotTotalBuild { get; set; }
        public Dictionary<string, double> plotUndergroundAreas { get; set; }
        public Dictionary<string, double> plotIndividualAreas { get; set; }
        public Dictionary<string, double> plotCommonAreas { get; set; }
        public Dictionary<string, double> plotCommonAreasSpecial { get; set; }
        public Dictionary<string, double> plotCommonAreasAll { get; set; }
        public Dictionary<string, double> plotLandAreas { get; set; }
        public Document doc { get; set; }
        public Transaction transaction { get; set; }
        public double areasCount { get; set; }
        public double missingAreasCount { get; set; }
        public string missingAreasData { get; set; }
        public AreaDictionary(Document activeDoc)
        {
            this.doc = activeDoc;
            this.AreasOrganizer = new Dictionary<string, Dictionary<string, List<Area>>>();
            this.propertyCommonAreas = new Dictionary<string, Dictionary<string, double>>();
            this.propertyCommonAreasSpecial = new Dictionary<string, Dictionary<string, double>>();
            this.propertyCommonAreasAll = new Dictionary<string, Dictionary<string, double>>();
            this.propertyIndividualAreas = new Dictionary<string, Dictionary<string, double>>();
            this.plotNames = new List<string>();
            this.plotProperties = new Dictionary<string, List<string>>();
            this.transaction = new Transaction(activeDoc, "Calculate and Update Area Parameters");
            this.plotAreasImp = new Dictionary<string, double>();
            this.plotBuildAreas = new Dictionary<string, double>();
            this.plotTotalBuild = new Dictionary<string, double>();
            this.plotUndergroundAreas = new Dictionary<string, double>();
            this.plotIndividualAreas = new Dictionary<string, double>();
            this.plotCommonAreas = new Dictionary<string, double>();
            this.plotCommonAreasSpecial = new Dictionary<string, double>();
            this.plotCommonAreasAll = new Dictionary<string, double>();
            this.plotLandAreas = new Dictionary<string, double>();
            this.areasCount = 0;
            this.missingAreasCount = 0;
            this.smartRounder = new SmartRound(doc);
            this.aritAsist = new AritmeticAssistant();

            ProjectInfo projectInfo = activeDoc.ProjectInformation;

            FilteredElementCollector areasCollector = new FilteredElementCollector(activeDoc).OfCategory(BuiltInCategory.OST_Areas).WhereElementIsNotElementType();

            // construct main AreaOrganizer Dictionary
            foreach (Element elem in areasCollector)
            {
               Area area = elem as Area;

                string plotName = area.LookupParameter("A Instance Area Plot").AsString();
                string groupName = area.LookupParameter("A Instance Area Group").AsString();

                if (!string.IsNullOrEmpty(plotName) && !string.IsNullOrEmpty(groupName) && area.Area!=0)
                {
                    if (!AreasOrganizer.ContainsKey(plotName))
                    {
                        this.AreasOrganizer.Add(plotName, new Dictionary<string, List<Area>>());
                        this.plotNames.Add(plotName);
                        this.plotProperties.Add(plotName, new List<string>());

                        if (projectInfo.LookupParameter("Plot Number").AsString() == plotName)
                            this.plotAreasImp.Add(plotName, projectInfo.LookupParameter("Plot Area").AsDouble());
                        else if (projectInfo.LookupParameter("Plot Number 1st").AsString() == plotName)
                            this.plotAreasImp.Add(plotName, projectInfo.LookupParameter("Plot Area 1st").AsDouble());
                        else if (projectInfo.LookupParameter("Plot Number 2nd").AsString() == plotName)
                            this.plotAreasImp.Add(plotName, projectInfo.LookupParameter("Plot Area 2nd").AsDouble());
                    }

                    if (!AreasOrganizer[plotName].ContainsKey(groupName))
                    {
                        this.AreasOrganizer[plotName].Add(groupName, new List<Area>());
                        this.plotProperties[plotName].Add(groupName);
                    }

                    this.AreasOrganizer[plotName][groupName].Add(area);
                    areasCount++;
                }

                else
                {
                    // TODO: check whether an Area has a plotName, that is not relative to the ones in the Project Info (is it actually needed?)
                    // TODO
                    // TODO
                    missingAreasCount++;
                    missingAreasData += $"{area.Id} {area.Number} {area.Name} {area.Area}\n";
                    // TODO
                    // TODO
                    // TODO
                }
            }

            // set gross area
            setGrossArea();

            // calculate primary area
            this.errorMessage = calculatePrimaryArea();

            // based on AreasOrganizer, construct the plotBuildAreas dictionary
            foreach (string plotName in plotNames)
            {
                plotBuildAreas.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Location").AsString() == "НАЗЕМНА")
                        {
                            plotBuildAreas[plotName] = aritAsist.addToArea(plotBuildAreas[plotName], smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotTotalBuild dictionary
            foreach (string plotName in plotNames)
            {
                plotTotalBuild.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Location").AsString().ToLower() == "надземна" 
                            || area.LookupParameter("A Instance Area Location").AsString().ToLower() == "наземна")
                        {
                            plotTotalBuild[plotName] = aritAsist.addToArea(plotTotalBuild[plotName], smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotUndergroundAreas dictionary
            foreach (string plotName in plotNames)
            {
                plotUndergroundAreas.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Location").AsString() == "ПОДЗЕМНА")
                        {
                            plotUndergroundAreas[plotName] += smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble());
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotIndividualAreas dictionary
            foreach (string plotName in plotNames)
            {
                plotIndividualAreas.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект" 
                            && !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            plotIndividualAreas[plotName] += smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area").AsDouble());
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotCommonAreas dictionary
            foreach (string plotName in plotNames)
            {
                plotCommonAreas.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част")
                        {
                            plotCommonAreas[plotName] += smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble());
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotCommonAreasSpecial dictionary
            foreach (string plotName in plotNames)
            {
                plotCommonAreasSpecial.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част"
                            && (area.LookupParameter("A Instance Area Primary").HasValue
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            plotCommonAreasSpecial[plotName] += smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble());
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotCommonAreasAll dictionary
            foreach (string plotName in plotNames)
            {
                plotCommonAreasAll.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част")
                        {
                            plotCommonAreasAll[plotName] += smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble());
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the propertyCommonAreas dictionary
            foreach (string plotName in plotNames)
            {
                propertyCommonAreas.Add(plotName, new Dictionary<string, double>());

                foreach (string plotProperty in plotProperties[plotName])
                {
                    propertyCommonAreas[plotName].Add(plotProperty, 0);

                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част"
                            && !(area.LookupParameter("A Instance Area Primary").HasValue
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            propertyCommonAreas[plotName][plotProperty] += smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble());
                        }
                    }
                }

                // check if there are "A + B + C" type of properties and if there are any, get the total sum of their common areas
                double plusPropertiesCommonSum = 0;
                bool wasFound = false;

                foreach (string plotProperty in plotProperties[plotName])
                {
                    if (plotProperty.Contains("+"))
                    {
                        plusPropertiesCommonSum += propertyCommonAreas[plotName][plotProperty];
                        wasFound = true;
                    }
                }

                // in case such property types were found, redistribute their areas across the rest of the properties
                if (wasFound)
                {
                    double remainingCommonArea = plotCommonAreas[plotName] - plusPropertiesCommonSum;

                    // search for properties of type "A + B"
                    foreach (string plotProperty in plotProperties[plotName])
                    {
                        if (plotProperty.Contains("+"))
                        {
                            // if such is found, redistribute their areas across the rest of the properties
                            string[] splitProperties = plotProperty.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                            foreach (string property in splitProperties)
                            {
                                double ratio = Math.Round(propertyCommonAreas[plotName][property] / remainingCommonArea, 3);

                                double areaToAdd = Math.Round(propertyCommonAreas[plotName][plotProperty] * ratio, 3);
                                propertyCommonAreas[plotName][property] += areaToAdd;
                            }
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the propertyCommonAreasSpecial dictionary
            foreach (string plotName in plotNames)
            {
                propertyCommonAreasSpecial.Add(plotName, new Dictionary<string, double>());

                foreach (string plotProperty in plotProperties[plotName])
                {
                    propertyCommonAreasSpecial[plotName].Add(plotProperty, 0);

                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част"
                            && (area.LookupParameter("A Instance Area Primary").HasValue
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            propertyCommonAreasSpecial[plotName][plotProperty] += smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble());
                        }
                    }
                }
            }

            // based on Areas Organizer, construct the propertyCommonAreasAll
            foreach (string plotName in plotNames)
            {
                propertyCommonAreasAll.Add(plotName, new Dictionary<string, double>());

                foreach (string plotProperty in plotProperties[plotName])
                {
                    double sum = propertyCommonAreas[plotName][plotProperty] + propertyCommonAreasSpecial[plotName][plotProperty];

                    propertyCommonAreasAll[plotName].Add(plotProperty, sum);
                }
            }

            // based on AreasOrganizer, construct the propertyIndividualAreas dictionary
            foreach (string plotName in plotNames)
            {
                propertyIndividualAreas.Add(plotName, new Dictionary<string, double>());

                foreach (string plotProperty in plotProperties[plotName])
                {
                    propertyIndividualAreas[plotName].Add(plotProperty, 0);

                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект"
                            && !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            propertyIndividualAreas[plotName][plotProperty] += smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area").AsDouble());
                        }
                    }
                }
            }

            // based on AreasOrganizer, construct the plotLandAreas dictionary
            foreach (string plotName in plotNames)
            {
                plotLandAreas.Add(plotName, 0);

                foreach (string plotProperty in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][plotProperty])
                    {
                        if (area.LookupParameter("A Instance Area Group").AsString().ToLower() == "земя")
                        {
                            plotLandAreas[plotName] += smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area").AsDouble());
                        }
                    }
                }
            }
        }
        private double ExtractLevelHeight(Level level)
        {
            return level.Elevation;
        }
        private string ReorderEntrance(string entranceName)
        {
            if (entranceName == "НЕПРИЛОЖИМО")
                return "A";
            else
                return entranceName;
        }
        private void calculateSurplusPercent(Dictionary<string, List<Area>> areaGroup, string parameterName)
        {
            if (areaGroup.Keys.ToList().Count == 0)
            {
                return;
            }

            transaction.Start();

            // calculate building permit surplus
            double buildingPermitTotal = 0;
            foreach (string group in areaGroup.Keys)
            {
                foreach (Area area in areaGroup[group])
                {
                    buildingPermitTotal += Math.Round(area.LookupParameter(parameterName).AsDouble(), 3, MidpointRounding.AwayFromZero);
                }
            }

            double surplus = Math.Round(100 - buildingPermitTotal, 3, MidpointRounding.AwayFromZero);
            int counter = 0;
            int loopCounter = 0;

            while (Math.Abs(surplus) >= 0.0005)
            {
                if (counter >= areaGroup.Keys.ToList().Count())
                {
                    counter = 0;
                    loopCounter++;
                }
                string group = areaGroup.Keys.ToList()[counter];

                if (Math.Round(Math.Abs(surplus) * 1000) >= areaGroup[group].Count())
                {
                    // calculate the deduction total, depending on whether the surplis is positive or negatibe
                    double coefficient = surplus / Math.Abs(surplus);
                    // so far, the result is either 1 or -1
                    double finalDeduction = 0.001 * coefficient;
                    // this is the final deduction calculated value, which would be either -0.001 or 0.001

                    foreach (Area area in areaGroup[group])
                    {
                        // redistribute final deduction towards the Building Permit %
                        double currentPercent = area.LookupParameter(parameterName).AsDouble();
                        area.LookupParameter(parameterName).Set(currentPercent + finalDeduction);
                        surplus -= finalDeduction;
                    }
                }

                counter++;

                if (loopCounter == 10)
                    surplus = 0;
            }

            transaction.Commit();
        }
        private void calculateSurplusPercentandArea(Dictionary<string, List<Area>> areaGroup, string parameterNamePercent, string parameterNameArea, double totalAreaToCalculateFrom)
        {
            if (areaGroup.Keys.ToList().Count == 0)
            {
                return;
            }

            transaction.Start();

            // calculate building permit surplus
            double buildingPermitTotal = 0;
            foreach (string group in areaGroup.Keys)
            {
                foreach (Area area in areaGroup[group])
                {
                    buildingPermitTotal += Math.Round(area.LookupParameter(parameterNamePercent).AsDouble(), 3);
                }
            }

            double surplus = Math.Round(100 - buildingPermitTotal, 3);
            int counter = 0;
            int numbOfCycles = 0;

            while (Math.Abs(surplus) >= 0.0005)
            {
                if (numbOfCycles == 5)
                {
                    string randomGroup = areaGroup.Keys.ToList()[0];
                    Area randomArea = areaGroup[randomGroup][0];
                    string areasInfo = $"plot: {randomArea.LookupParameter("A Instance Area Plot").AsString()} and area group: {randomArea.LookupParameter("A Instance Area Group").AsString()}";

                    TaskDialog.Show("Warning", $"Surplus could not be distributed for parameters {parameterNamePercent} and {parameterNameArea} for {areasInfo}");

                    transaction.Commit();
                    return;
                }
                if (counter >= areaGroup.Keys.ToList().Count())
                {
                    counter = 0;
                    numbOfCycles++;
                }

                string group = areaGroup.Keys.ToList()[counter];

                if (Math.Round(Math.Abs(surplus) * 1000) >= areaGroup[group].Count())
                {
                    // calculate the deduction total, depending on whether the surplis is positive or negatibe
                    double coefficient = surplus / Math.Abs(surplus);
                    // so far, the result is either 1 or -1
                    double finalDeduction = 0.001 * coefficient;
                    // this is the final deduction calculated value, which would be either -0.001 or 0.001

                    foreach (Area area in areaGroup[group])
                    {
                        // redistribute final deduction towards the Building Permit %
                        double calculatedPercent = area.LookupParameter(parameterNamePercent).AsDouble() + finalDeduction;
                        area.LookupParameter(parameterNamePercent).Set(calculatedPercent);
                        surplus -= finalDeduction;

                        // calculate the updated area 
                        double calculatedArea = Math.Round(calculatedPercent * totalAreaToCalculateFrom / 100, 2);

                        // redistribute square meters area accordingly
                        area.LookupParameter(parameterNameArea).Set(calculatedArea * areaConvert);
                    }
                }

                counter++;
            }

            // check if the area was calculated accordingly or if there is a newly formed surplus
            double totalAreaSum = 0;
            foreach (string group in areaGroup.Keys)
            {
                foreach (Area area in areaGroup[group])
                {
                    totalAreaSum += smartRounder.sqFeetToSqMeters(area.LookupParameter(parameterNameArea)?.AsDouble() ?? 0.0);
                }
            }

            double areaSurplus = Math.Round(totalAreaToCalculateFrom - totalAreaSum, 2);
            int areaCounter = 0;
            int numbOfAreaCycles = 0;

            // check if areasurplus is 0, and if yes, redistribute it accordingly as well
            while (Math.Abs(areaSurplus) >= 0.005)
            {
                if (numbOfAreaCycles == 5)
                {
                    string randomGroup = areaGroup.Keys.ToList()[0];
                    Area randomArea = areaGroup[randomGroup][0];
                    string areasInfo = $"plot: {randomArea.LookupParameter("A Instance Area Plot").AsString()} and area group: {randomArea.LookupParameter("A Instance Area Group").AsString()}";

                    TaskDialog.Show("Warning", $"Surplus area could not be distributed for parameters {parameterNameArea} for {areasInfo}. " +
                        $"// Remaining surplus is {areaSurplus} | Number of cycles: {numbOfAreaCycles}");

                    transaction.Commit();
                    return;
                }

                if (areaCounter >= areaGroup.Keys.ToList().Count())
                {
                    areaCounter = 0;
                    numbOfAreaCycles++;
                }

                string group = areaGroup.Keys.ToList()[areaCounter];

                if (Math.Round(Math.Abs(areaSurplus) * 100) >= areaGroup[group].Count())
                {
                    // calculate the deduction total, depending on whether the surplis is positive or negatibe
                    double coefficient = areaSurplus / Math.Abs(areaSurplus);
                    // so far, the result is either 1 or -1
                    double finalDeduction = 0.01 * coefficient;
                    // this is the final deduction calculated value, which would be either -0.01 or 0.01

                    foreach (Area area in areaGroup[group])
                    {
                        // calculate the updated area
                        double calculatedArea = Math.Round(smartRounder.sqFeetToSqMeters(area.LookupParameter(parameterNameArea)?.AsDouble() ?? 0.0) + finalDeduction, 2);
                        area.LookupParameter(parameterNameArea).Set(Math.Round(calculatedArea * areaConvert, 2));

                        areaSurplus -= finalDeduction;
                    }
                }

                areaCounter++;
            }
            
            transaction.Commit();
        }
        private void calculateSpecialCommonAreaSurplus(Dictionary<string, List<Area>> areaGroup, string parameterNameArea, double totalAreaToCalculateFrom)
        {
            if (areaGroup.Keys.ToList().Count == 0)
            {
                return;
            }

            transaction.Start();

            double totalCalculatedArea = 0;
            foreach (string group in areaGroup.Keys)
            {
                foreach (Area area in areaGroup[group])
                {
                    totalCalculatedArea += Math.Round(area.LookupParameter(parameterNameArea).AsDouble() / areaConvert, 2);
                }
            }

            double surplus = Math.Round(totalAreaToCalculateFrom - totalCalculatedArea, 2);
            int counter = 0;
            int numbOfCycles = 0;

            while (Math.Abs(surplus) >= 0.0005)
            {
                if (numbOfCycles == 5)
                {
                    string randomGroup = areaGroup.Keys.ToList()[0];
                    Area randomArea = areaGroup[randomGroup][0];
                    string areasInfo = $"plot: {randomArea.LookupParameter("A Instance Area Plot").AsString()} and area group: {randomArea.LookupParameter("A Instance Area Group").AsString()}";

                    TaskDialog.Show("Warning", $"Surplus could not be distributed for parameter {parameterNameArea}, {areasInfo}");

                    transaction.Commit();
                    return;
                }
                if (counter >= areaGroup.Keys.ToList().Count())
                {
                    counter = 0;
                    numbOfCycles++;
                }

                string group = areaGroup.Keys.ToList()[counter];

                // calculate the deduction total, depending on whether the surplis is positive or negatibe
                double coefficient = surplus / Math.Abs(surplus);
                // so far, the result is either 1 or -1
                double finalDeduction = 0.01 * coefficient;
                // this is the final deduction calculated value, which would be either -0.01 or 0.01

                foreach (Area area in areaGroup[group])
                {
                    if (area.LookupParameter("A Instance Common Area Special").HasValue &&
                        smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area Special")?.AsDouble() ?? 0.0) != 0 &&
                        area.LookupParameter("A Instance Common Area Special").AsString() != "")
                    {
                        // calculate the updated area
                        double calculatedArea = Math.Round(smartRounder.sqFeetToSqMeters(area.LookupParameter(parameterNameArea)?.AsDouble() ?? 0.0) + finalDeduction, 2);
                        area.LookupParameter(parameterNameArea).Set(Math.Round(calculatedArea * areaConvert, 2));

                        surplus -= finalDeduction;
                    }
                }

                counter++;
            }

            transaction.Commit();
        }
        private void calculateParkingPercentSurplus(Dictionary<List<double>, List<Room>> percentageDict, double totalPercentage, double totalPercentageAchieved, int index)
        {
            // calculate surplus
            double surplus = Math.Round(totalPercentage - totalPercentageAchieved, 3);
            int counter = 0;
            int loopCounter = 0;

            // redistribute surplus if any
            while (Math.Abs(surplus) >= 0.0005)
            {
                if (counter >= percentageDict.Keys.Count())
                {
                    counter = 0;
                    loopCounter++;
                }
                List<double> dictList = percentageDict.Keys.ToList()[counter];

                if (Math.Round(Math.Abs(surplus) * 1000) >= percentageDict[dictList].Count)
                {
                    // calculate the deduction total, depending on whether the surplis is positive or negatibe
                    double coefficient = surplus / Math.Abs(surplus);
                    // so far, the result is either 1 or -1
                    double finalDeduction = 0.001 * coefficient;
                    // this is the final deduction calculated value, which would be either -0.001 or 0.001

                    percentageDict.Keys.ToList()[counter][index] += finalDeduction;

                    foreach (Room room in percentageDict[dictList])
                    {
                        surplus -= finalDeduction;
                    }
                }

                counter++;

                if (loopCounter == 10)
                    surplus = 0;
            }
        }
        private void calculateParkingAreaSurplus(Dictionary<List<double>, List<Room>> percentageDict, double totalArea, double totalAreaAchieved, int index)
        {
            // calculate surplus
            double surplus = Math.Round(totalArea - totalAreaAchieved, 2);
            int counter = 0;
            int loopCounter = 0;

            // redistribute surplus if any
            while (Math.Abs(surplus) >= 0.0005)
            {
                if (counter >= percentageDict.Keys.Count())
                {
                    counter = 0;
                    loopCounter++;
                }
                List<double> dictList = percentageDict.Keys.ToList()[counter];

                if (Math.Round(Math.Abs(surplus) * 100) >= percentageDict[dictList].Count)
                {
                    // calculate the deduction total, depending on whether the surplis is positive or negatibe
                    double coefficient = surplus / Math.Abs(surplus);
                    // so far, the result is either 1 or -1
                    double finalDeduction = 0.01 * coefficient;
                    // this is the final deduction calculated value, which would be either -0.001 or 0.001

                    percentageDict.Keys.ToList()[counter][index] += finalDeduction;

                    foreach (Room room in percentageDict[dictList])
                    {
                        surplus -= finalDeduction;
                    }
                }

                counter++;

                if (loopCounter == 10)
                    surplus = 0;
            }
        }
        private bool doesHaveRoomsAdjascent(string areaNumber)
        {
            bool hasAdjRooms = false;

            List<Element> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType().ToList();

            foreach (Element element in rooms)
            {
                Room room = element as Room;

                if (room.LookupParameter("A Instance Area Primary").AsString() == areaNumber)
                {
                    hasAdjRooms = true;
                }
            }

            return hasAdjRooms;
        }
        private string areAdjascentRoomsAndAreasEqual(Dictionary<List<object>, Room> adjascentRooms, List<Area> adjascentRegular, List<Area> adjascentLand, Area mainArea)
        {
            string errormessage = "";

            double totalAdjascentAreas = smartRounder.sqFeetToSqMeters(mainArea.Area);
            double totalAdjascentRooms = 0;

            foreach (Area area in adjascentRegular)
                totalAdjascentAreas += smartRounder.sqFeetToSqMeters(area.Area);

            foreach (Area area in adjascentLand)
                totalAdjascentAreas += smartRounder.sqFeetToSqMeters(area.Area);

            foreach (List<object> calculations in adjascentRooms.Keys)
                totalAdjascentRooms += smartRounder.sqFeetToSqMeters(adjascentRooms[calculations].Area);

            if (totalAdjascentRooms != totalAdjascentAreas)
                errormessage = $"Area {mainArea.Name} | Id: {mainArea.Id} | Сумата от площите на нейните подчинени Rooms и Areas се разминава. Моля да я проверите\n";

            return errormessage;
        }
        private bool areAllLandAreasAdjascent(string plotName)
        {
            bool areAllLandAdjascent = true;

            foreach (Area area in AreasOrganizer[plotName]["ЗЕМЯ"])
            {
                if (!(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                    areAllLandAdjascent = false;
            }

            return areAllLandAdjascent;
        }
        private void setParkingShareParameters(Dictionary<List<double>, List<Room>> dict, Area area)
        {
            string areaNumber = area.LookupParameter("Number").AsString();
            double areaArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area")?.AsDouble() ?? 0.0);
            double commonAreaPercent = Math.Round(area.LookupParameter("A Instance Common Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
            double commonArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area")?.AsDouble() ?? 0.0);
            double specialCommonArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area Special")?.AsDouble() ?? 0.0);
            double totalCommonArea = commonArea + specialCommonArea;
            double totalArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Total Area")?.AsDouble() ?? 0.0);
            double buildingRight = Math.Round(area.LookupParameter("A Instance Building Permit %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
            double landPercentage = Math.Round(area.LookupParameter("A Instance RLP Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
            double landArea = Math.Round(area.LookupParameter("A Instance RLP Area")?.AsDouble() / areaConvert ?? 0.0, 3, MidpointRounding.AwayFromZero);

            foreach (List<double> listData in dict.Keys)
            {
                double percentage = listData[1];

                double commonAreaShare = Math.Round(percentage * commonArea / 100, 2, MidpointRounding.AwayFromZero);
                listData.Add(commonAreaShare);

                double commonAreaSpecialShare = Math.Round(percentage * specialCommonArea / 100, 2, MidpointRounding.AwayFromZero);
                listData.Add(commonAreaSpecialShare);

                double commonAreaTotalShare = Math.Round(percentage * totalCommonArea / 100, 2, MidpointRounding.AwayFromZero);
                listData.Add(commonAreaTotalShare);

                double totalAreaShare = Math.Round(percentage * totalArea / 100, 2, MidpointRounding.AwayFromZero);
                listData.Add(totalAreaShare);

                double buildingRightShare = Math.Round(percentage * buildingRight / 100, 3, MidpointRounding.AwayFromZero);
                listData.Add(buildingRightShare);

                double landPercentageShare = Math.Round(percentage * landPercentage / 100, 3, MidpointRounding.AwayFromZero);
                listData.Add(landPercentageShare);

                double landAreaShare = Math.Round(percentage * landArea / 100, 2, MidpointRounding.AwayFromZero);
                listData.Add(landAreaShare);
            }
        }
        private Dictionary<List<object>, Room> returnAdjascentRooms(Area area)
        {
            string areaNumber = area.LookupParameter("Number").AsString();
            double areaArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area")?.AsDouble() ?? 0.0);
            double commonAreaPercent = Math.Round(area.LookupParameter("A Instance Common Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
            double commonArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area")?.AsDouble() ?? 0.0);
            double specialCommonArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area Special")?.AsDouble() ?? 0.0);
            double totalCommonArea = commonArea + specialCommonArea;
            double totalArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Total Area")?.AsDouble() ?? 0.0);
            double buildingRight = Math.Round(area.LookupParameter("A Instance Building Permit %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
            double landPercentage = Math.Round(area.LookupParameter("A Instance RLP Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
            double landArea = Math.Round(area.LookupParameter("A Instance RLP Area")?.AsDouble() / areaConvert ?? 0.0, 3, MidpointRounding.AwayFromZero); // WHY 0.000 PRECISION FOR AREA???

            Dictionary<string, List<object>> keyValuePairs = new Dictionary<string, List<object>>();

            List<Room> rooms = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Rooms).WhereElementIsNotElementType()
            .Where(room => room.LookupParameter("A Instance Area Primary").AsString() == areaNumber).Cast<Room>().ToList();

            // group rooms, based on their Area
            var groupedRooms = rooms.GroupBy(room => Math.Round(room.LookupParameter("Area").AsDouble(), 3));

            // create a dictionary from these groups
            Dictionary<List<double>, List<Room>> percentageDict = new Dictionary<List<double>, List<Room>>();
            double totalPercentage = 0;
            double totalPercentageShare = 0;
            double totalBuildingRightShare = 0;
            double totalLandPercentageShare = 0;
            double totalCommonAreaShare = 0;
            double totalCommonAreaSpecialShare = 0;
            double totalCommonAreaTotalShare = 0;
            double totalAreaTotalShare = 0;
            double totalLandAreaShare = 0;

            foreach (var group in groupedRooms)
            {
                // generate a list with all the calculated data
                List<double> listData = new List<double>();
                listData.Add(group.Count());

                double percentage = Math.Round(
                    smartRounder.sqFeetToSqMeters(group.First().LookupParameter("Area").AsDouble()) * 100/areaArea, 
                    3, MidpointRounding.AwayFromZero);
                listData.Add(percentage);

                double percentageShare = Math.Round(percentage * commonAreaPercent / 100, 3, MidpointRounding.AwayFromZero);
                listData.Add(percentageShare);

                // add the list to the dictionary as a key
                percentageDict.Add(listData, new List<Room>());

                // add all the rooms in the group to the same key
                foreach (Room room in group)
                {
                    percentageDict[listData].Add(room);
                    totalPercentage += percentage;
                    totalPercentageShare += percentageShare;
                }
            }
            
            // redistribute surplus for percentage coefficients
            calculateParkingPercentSurplus(percentageDict, 100, totalPercentage, 1);
            setParkingShareParameters(percentageDict, area);

            foreach (List<double> listData in percentageDict.Keys)
            {
                foreach (Room room in percentageDict[listData])
                {
                    totalBuildingRightShare += listData[7];
                    totalLandPercentageShare += listData[8];
                    totalCommonAreaShare += listData[3];
                    totalCommonAreaSpecialShare += listData[4];
                    totalCommonAreaTotalShare += listData[5];
                    totalAreaTotalShare += listData[6];
                    totalLandAreaShare += listData[9];
                }
            }

            calculateParkingPercentSurplus(percentageDict, commonAreaPercent, totalPercentageShare, 2);
            calculateParkingPercentSurplus(percentageDict, buildingRight, totalBuildingRightShare, 7);
            calculateParkingPercentSurplus(percentageDict, landPercentage, totalLandPercentageShare, 8);

            // redistribute surplus for area coefficients
            calculateParkingAreaSurplus(percentageDict, commonArea, totalCommonAreaShare, 3);
            calculateParkingAreaSurplus(percentageDict, specialCommonArea, totalCommonAreaSpecialShare, 4);
            calculateParkingAreaSurplus(percentageDict, totalCommonArea, totalCommonAreaTotalShare, 5);
            calculateParkingAreaSurplus(percentageDict, landArea, totalLandAreaShare, 9);
            
            // fix eventual total area inaccuracy
            foreach (List<double> listData in percentageDict.Keys)
            {
                double roomArea = Math.Round(percentageDict[listData].First().Area / areaConvert, 2, MidpointRounding.AwayFromZero);

                listData[6] = roomArea + listData[5];
            }

            // construct new dictionary
            Dictionary<List<object>, Room> flattenedDict = new Dictionary<List<object>, Room>();

            // iterate through the percentageDict to populate the new dictionary
            foreach (var kvp in percentageDict)
            {
                List<double> keyList = kvp.Key;
                List<Room> roomsList = kvp.Value;

                double percentage = keyList[1];
                double percentageShare = keyList[2];
                double commonAreaShare = keyList[3];
                double commonAreaSpecialShare = keyList[4];
                double commonAreaTotalShare = keyList[5];
                double totalAreaShare = keyList[6];
                double buildingRightShare = keyList[7];
                double landPercentageShare = keyList[8];
                double landAreaShare = keyList[9];

                foreach (Room room in roomsList)
                {
                    // create the new key with the room number and percentage
                    List<object> newKey = new List<object>
                    {
                        room.LookupParameter("Number").AsString(),
                        percentage,
                        percentageShare,
                        commonAreaShare,
                        commonAreaSpecialShare,
                        commonAreaTotalShare,
                        totalAreaShare,
                        buildingRightShare,
                        landPercentageShare,
                        landAreaShare
                    };

                    // replace 0.0 values with nulls
                    for (int i = 0; i < newKey.Count; i++)
                    {
                        if (newKey[i] is double && (double)newKey[i] == 0.0)
                        {
                            newKey[i] = DBNull.Value;
                        }
                    }

                    // Add to the new dictionary
                    flattenedDict[newKey] = room;
                }
            }

            // sort the flattened dictionary based on room number
            var sortedFlattenedDict = flattenedDict.OrderBy(kvp => (string)kvp.Key[0]).ToDictionary(kvp => kvp.Key, kvp => kvp.Value); 

            return sortedFlattenedDict;
        }        
        private void setGrossArea()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        double calculatedValue = smartRounder.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()) * areaConvert;
                        area.LookupParameter("A Instance Gross Area").Set(calculatedValue);
                    }
                }
            }

            transaction.Commit();
        }
        private string calculatePrimaryArea()
        {
            string errorMessage = "";
            List<string> missingNumbers = new List<string>();

            transaction.Start();

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area secArea in AreasOrganizer[plotName][property])
                    {
                        if (secArea.LookupParameter("A Instance Area Primary").HasValue && 
                            secArea.LookupParameter("A Instance Area Primary").AsString() != "" && secArea.Area != 0)
                        {
                            bool wasFound = false;

                            string[] mainAreaNumbers = secArea.LookupParameter("A Instance Area Primary").AsString().Split(new char[] { '+' }, 
                                StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToArray();

                            foreach (string mainAreaNumber in mainAreaNumbers)
                            {
                                if (secArea.LookupParameter("A Instance Area Group").AsString().ToLower() != "земя")
                                {
                                    foreach (Area mainArea in AreasOrganizer[plotName][property])
                                    {
                                        if (mainArea.Number == mainAreaNumber)
                                        {
                                            wasFound = true;

                                            if (secArea.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект")
                                            {
                                                mainArea.LookupParameter("A Instance Gross Area").Set(
                                                    (smartRounder.sqFeetToSqMeters(mainArea.LookupParameter("A Instance Gross Area").AsDouble()) +
                                                    smartRounder.sqFeetToSqMeters(secArea.LookupParameter("Area").AsDouble())) * areaConvert
                                                    );
                                            }
                                        }
                                    }
                                }
                                
                                else
                                {
                                    foreach (string plotNameMain in plotNames)
                                    {
                                        foreach (string propertyMain in plotProperties[plotNameMain])
                                        {
                                            foreach (Area mainArea in AreasOrganizer[plotNameMain][propertyMain])
                                            {
                                                if (mainArea.Number == mainAreaNumber)
                                                {
                                                    wasFound = true;

                                                    if (secArea.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект")
                                                    {
                                                        mainArea.LookupParameter("A Instance Gross Area").Set(
                                                            mainArea.LookupParameter("A Instance Gross Area").AsDouble() +
                                                            secArea.LookupParameter("Area").AsDouble());
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (!wasFound && !missingNumbers.Contains(secArea.LookupParameter("Number").AsString()))
                            {
                                missingNumbers.Add(secArea.LookupParameter("Number").AsString());
                                errorMessage += $"Грешка: Area {secArea.LookupParameter("Number").AsString()} / id: {secArea.Id} " +
                                    $"/ Посочената Area е зададена като подчинена на такава с несъществуващ номер. Моля, проверете я и стартирайте апликацията отново\n";
                            }
                        }
                    }
                }
            }

            transaction.Commit();

            return errorMessage;
        }
        public void calculateC1C2()
        {
            transaction.Start();

            foreach (Element collectorElement in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Areas).WhereElementIsNotElementType().ToList())
            {
                Area area = collectorElement as Area;

                if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ"
                    && !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                {
                    area.LookupParameter("A Instance Price C1/C2").Set(
                            smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area")?.AsDouble() ?? 0.0)
                            * area.LookupParameter("A Coefficient Multiplied").AsDouble()
                            );
                }
            }

            transaction.Commit();
        }
        public void calculateCommonAreaPerc()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {
                    double totalC1C2 = 0;

                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ" && !(area.LookupParameter("A Instance Area Primary").HasValue 
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            double C1C2 = area.LookupParameter("A Instance Price C1/C2").AsDouble();
                            totalC1C2 += C1C2;
                        }
                    }

                    // calculate common area percentage parameter for each area
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ" && !(area.LookupParameter("A Instance Area Primary").HasValue 
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            double commonAreaPercent = Math.Round(area.LookupParameter("A Instance Price C1/C2").AsDouble() 
                                / totalC1C2 * 100, 3, MidpointRounding.AwayFromZero);
                            area.LookupParameter("A Instance Common Area %").Set(commonAreaPercent);
                        }
                    }
                }
            }

            transaction.Commit();
        }
        public void calculateCommonArea()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {
                    double propertyCommonArea = propertyCommonAreas[plotName][property];

                    // calculate common area percentage parameter for each area
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ"
                            && !(area.LookupParameter("A Instance Area Primary").HasValue 
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            double commonArea;

                            commonArea = 
                                Math.Round(
                                area.LookupParameter("A Instance Common Area %").AsDouble() * propertyCommonArea / 100, 2, MidpointRounding.AwayFromZero)
                                * areaConvert;

                            area.LookupParameter("A Instance Common Area").Set(commonArea);
                        }
                    }
                }
            }

            transaction.Commit();
        }
        public void calculateSpecialCommonAreas()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {
                    // first set all special common area values to 0
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        area.LookupParameter("A Instance Common Area Special").Set(0);
                    }

                    // check all areas from a given dictionary
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        // check if there is a common area that is set to adjascent to another one
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част" &&
                            area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != "")
                        {
                            // if such is found, find all of the areas, it is set to be adjascent to
                            string[] mainAreaNumbers = area.LookupParameter("A Instance Area Primary").AsString().Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(s => s.Trim())
                                .ToArray();

                            double sumC1C2 = 0;
                            List<Area> mainAreaElements = new List<Area>();

                            // find all the areas it is adjascent to and calculate their total C1C2 and add them to a list
                            foreach (string mainAreaNumber in mainAreaNumbers)
                            {
                                bool wafFound = false;

                                foreach (Area mainArea in AreasOrganizer[plotName][property])
                                {
                                    if (mainArea.LookupParameter("Number").AsString() == mainAreaNumber)
                                    {
                                        wafFound = true;
                                        sumC1C2 += mainArea.LookupParameter("A Instance Price C1/C2").AsDouble();
                                        mainAreaElements.Add(mainArea);
                                    }
                                }

                                if (!wafFound)
                                {
                                    errorMessage += $"Area {area.Name} | Id:{area.Id} " +
                                        $"е зададена като подчинена на такава с несъществуващ номер: {mainAreaNumber}\n";
                                }
                            }

                            // for each area of the list, calculate its Special Common Area
                            foreach (Area mainArea in mainAreaElements)
                            {
                                double percentage = Math.Round(mainArea.LookupParameter("A Instance Price C1/C2").AsDouble() * 100 / sumC1C2, 
                                    3, MidpointRounding.AwayFromZero);

                                double areaToAdd = smartRounder.sqFeetToSqMeters(area.Area) * percentage / 100 * areaConvert;

                                mainArea.LookupParameter("A Instance Common Area Special")
                                    .Set(mainArea.LookupParameter("A Instance Common Area Special").AsDouble() + areaToAdd);
                            }
                        }
                    }
                }
            }

            transaction.Commit();
        }
        public void calculateTotalArea()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {                 
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ" && 
                            !(area.LookupParameter("A Instance Area Primary").HasValue 
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            double gross = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area").AsDouble());
                            double common = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area").AsDouble());
                            double commonSpecial = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area Special").AsDouble());
                            double total = gross + common + commonSpecial;

                            area.LookupParameter("A Instance Total Area").Set(total * areaConvert);
                        }
                    }
                }
            }

            transaction.Commit();
        }
        public void calculateBuildingPercentPermit()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                double totalPlotC1C2IMP = 0;

                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ" 
                            && !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            totalPlotC1C2IMP += area.LookupParameter("A Instance Price C1/C2").AsDouble();
                        }
                    }
                }

                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ" 
                            && !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            double buildingPercentPermit = Math.Round(area.LookupParameter("A Instance Price C1/C2").AsDouble() 
                                                            / totalPlotC1C2IMP * 100, 3, MidpointRounding.AwayFromZero);
                            area.LookupParameter("A Instance Building Permit %").Set(buildingPercentPermit);
                        }
                    }
                }
            }

            transaction.Commit();
        }
        public void calculateRlpAreaPercent()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                double plotArea = plotAreasImp[plotName];

                // find all area objects of type 'ЗЕМЯ' and calculate their collective percentage as of the total plot area
                double reductionPercentage = 0;

                // also find the total C1C2 for all individual areas in the property
                double totalC1C2IMP = 0;

                // calculate the total C1C2 and reduction percentage for the whole plot
                foreach (string property in plotProperties[plotName])
                {                    
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Group").AsString().ToLower() == "земя")
                        {
                            double areaPercentage = Math.Round(100 * area.LookupParameter("Area").AsDouble() / plotArea, 3, MidpointRounding.AwayFromZero);
                            area.LookupParameter("A Instance RLP Area %").Set(areaPercentage);
                            reductionPercentage += areaPercentage;
                        }

                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект"
                            && !(area.LookupParameter("A Instance Area Primary").HasValue 
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            totalC1C2IMP += area.LookupParameter("A Instance Price C1/C2").AsDouble();
                        }
                    }
                }

                // calculate rlp area percent for each individual area
                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект"
                            && !(area.LookupParameter("A Instance Area Primary").HasValue
                            && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                        {
                            double rlpAreaPercent = area.LookupParameter("A Instance Price C1/C2").AsDouble() * 100 / totalC1C2IMP;

                            rlpAreaPercent = Math.Round(rlpAreaPercent * (100 - reductionPercentage) / 100, 3, MidpointRounding.AwayFromZero);

                            area.LookupParameter("A Instance RLP Area %").Set(rlpAreaPercent);
                        }
                    }
                }
            }

            transaction.Commit();
        }
        public void calculateRlpArea()
        {
            transaction.Start();

            foreach (string plotName in plotNames)
            {
                // reduce all the areas of 'ЗЕМЯ' objects from the total plot area
                double remainingPlotArea = plotAreasImp[plotName];

                // calculate the actual RLP area for each Area object
                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        double calculatedArea = area.LookupParameter("A Instance RLP Area %").AsDouble()
                            * remainingPlotArea / 100;

                        area.LookupParameter("A Instance RLP Area").Set(calculatedArea);
                    }
                }
            }

            transaction.Commit();
        }
        public string calculateInstancePropertyCommonAreaPercentage()
        {
            transaction.Start();

            string errorReport = "";

            foreach (string plotName in plotNames)
            {
                foreach (string property in plotProperties[plotName])
                {
                    foreach (Area area in AreasOrganizer[plotName][property])
                    {
                        if (area.LookupParameter("A Instance Area Category").AsString() == "САМОСТОЯТЕЛЕН ОБЕКТ" && 
                            !(area.LookupParameter("A Instance Area Primary").HasValue 
                            && area.LookupParameter("A Instance Area Primary").AsString() != "") && area.Area != 0)
                        {
                            try
                            {
                                double commonAreaImp = area.LookupParameter("A Instance Common Area").AsDouble();
                                double totalAreaImp = area.LookupParameter("A Instance Total Area").AsDouble();

                                double commonAreaPercent = Math.Round((commonAreaImp * 100) / totalAreaImp, 3, MidpointRounding.AwayFromZero);

                                area.LookupParameter("A Instance Property Common Area %").Set(commonAreaPercent);
                            }
                            catch 
                            {
                                errorReport += $"{area.Id} {area.Name} A Instance Common Area = {area.LookupParameter("A Instance Common Area").AsDouble()} " +
                                    $"/ A Instance Total Area = {area.LookupParameter("A Instance Total Area").AsDouble()}";
                            }
                        }
                    }
                }
            }

            transaction.Commit();

            return errorReport;
        }
        public void redistributeSurplus()
        {
            foreach (string plotName in plotNames)
            {
                Dictionary<string, List<Area>> areaGroupsAll = new Dictionary<string, List<Area>>();
                Dictionary<string, List<Area>> areaGroupsNoLand = new Dictionary<string, List<Area>>();
                Dictionary<string, Dictionary<string, List<Area>>> areaGroupsSeperateProperties = new Dictionary<string, Dictionary<string, List<Area>>>();

                List<Area> plotAreasNoLand = new List<Area>();
                List<Area> plotAreasAll = new List<Area>();

                // get all the areas in proper lists
                foreach (string property in plotProperties[plotName])
                {
                    if (property.ToLower() != "земя" && property.ToLower() != "траф" && !property.ToLower().Contains('+'))
                    {
                        plotAreasNoLand.AddRange(AreasOrganizer[plotName][property]);
                        plotAreasAll.AddRange(AreasOrganizer[plotName][property]);
                    }
                    else if (property.ToLower() == "земя")
                    {
                        plotAreasAll.AddRange(AreasOrganizer[plotName][property]);
                    }

                    // construct the dictionary for area surplus redistribution
                    List<Area> propertyGroupAreas = new List<Area>();

                    if (property.ToLower() != "земя" && property.ToLower() != "траф" && !property.ToLower().Contains('+'))
                    {
                        foreach (Area area in AreasOrganizer[plotName][property])
                        {
                            if (area.LookupParameter("A Instance Area Category").AsString().ToLower() != "обща част" && 
                                area.LookupParameter("A Instance Area Category").AsString().ToLower() != "изключена от оч" &&
                                !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                            {
                                propertyGroupAreas.Add(area);
                            }
                        }
                    }

                    List<Area> orderedPropertyGroup = propertyGroupAreas
                        .OrderBy(area => area.LookupParameter("A Instance Gross Area").AsDouble())
                        .Reverse()
                        .ToList();

                    var groupedAreasProperty = orderedPropertyGroup.GroupBy(area => area.LookupParameter("A Instance Gross Area").AsDouble());

                    areaGroupsSeperateProperties.Add(property, new Dictionary<string, List<Area>>());

                    int sequenceproperty = 1;

                    foreach (var group in groupedAreasProperty)
                    {
                        int areaCount = group.Count();
                        string key = $"{areaCount}N{sequenceproperty}";
                        areaGroupsSeperateProperties[property].Add(key, group.ToList());
                        sequenceproperty++;
                    }
                }

                // sort lists, based on objects' areas
                List<Area> sortedAreasNoLand = plotAreasNoLand
                    .OrderBy(area => area.LookupParameter("A Instance Gross Area").AsDouble())
                    .Where(area => area.LookupParameter("A Instance Area Category").AsString().ToLower() != "обща част")
                    .Where(area => area.LookupParameter("A Instance Area Category").AsString().ToLower() != "изключена от оч")
                    .Where(area => !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                    .Reverse()
                    .ToList();

                List<Area> sortedAreasAll = plotAreasAll
                    .OrderBy(area => area.LookupParameter("A Instance Gross Area").AsDouble())
                    .Where(area => area.LookupParameter("A Instance Area Category").AsString().ToLower() != "обща част")
                    .Where(area => area.LookupParameter("A Instance Area Category").AsString().ToLower() != "изключена от оч")
                    .Where(area => !(area.LookupParameter("A Instance Area Category").AsString().ToLower() == "самостоятелен обект" && 
                           area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                    .Reverse()
                    .ToList();

                // group areas by their "A Instance Gross Area" value
                var groupedAreasAll = sortedAreasAll.GroupBy(area => area.LookupParameter("A Instance Gross Area").AsDouble());
                var groupedAreasNoLand = sortedAreasNoLand.GroupBy(area => area.LookupParameter("A Instance Gross Area").AsDouble());

                // construct the dictionary for all area groups
                int sequenceAll = 1;

                foreach (var group in groupedAreasAll)
                {
                    int areaCount = group.Count();
                    string key = $"{areaCount}N{sequenceAll}";
                    areaGroupsAll[key] = group.ToList();
                    sequenceAll++;
                }

                // construct the dictionary for all area groups, except for the land
                int sequence = 1;

                foreach (var group in groupedAreasNoLand)
                {
                    int areaCount = group.Count();
                    string key = $"{areaCount}N{sequence}";
                    areaGroupsNoLand[key] = group.ToList();
                    sequence++;
                }
                
                // calculate building permit surplus
                calculateSurplusPercent(areaGroupsNoLand, "A Instance Building Permit %");
                // calculate RLP area percent and RLP Area
                calculateSurplusPercentandArea(areaGroupsAll, "A Instance RLP Area %", "A Instance RLP Area", Math.Round(plotAreasImp[plotName] / areaConvert, 2));
                // calculate common area percent and common area
                foreach (string property in areaGroupsSeperateProperties.Keys)
                {
                    calculateSurplusPercentandArea(areaGroupsSeperateProperties[property], "A Instance Common Area %", "A Instance Common Area", propertyCommonAreas[plotName][property]);
                    calculateSpecialCommonAreaSurplus(areaGroupsSeperateProperties[property], "A Instance Common Area Special", propertyCommonAreasSpecial[plotName][property]);
                }              
            }            
        }
        private void exportToExcelAdjascentRegular(Worksheet workSheet, int x, Area areaSub, bool isLand, string mainAreaNumber)
        {
            Range areaAdjRangeStr = workSheet.Range[$"A{x}", $"B{x}"];
            areaAdjRangeStr.set_Value(XlRangeValueDataType.xlRangeValueDefault, new[] { areaSub.LookupParameter("Number").AsString(), areaSub.LookupParameter("Custom Name").AsString() });

            areaAdjRangeStr.HorizontalAlignment = XlHAlign.xlHAlignRight;
            areaAdjRangeStr.Borders.LineStyle = XlLineStyle.xlContinuous;
            areaAdjRangeStr.Font.Italic = true;

            Range areaAdjRangeDouble = workSheet.Range[$"C{x}", $"O{x}"];
            areaAdjRangeDouble.NumberFormat = "0.00";

            if (isLand)
            {
                areaAdjRangeDouble.set_Value(XlRangeValueDataType.xlRangeValueDefault, new object[] { DBNull.Value, 
                                                        smartRounder.sqFeetToSqMeters(areaSub.LookupParameter("Area").AsDouble()),
                                                        DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 
                                                        DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
                                                        Math.Round(areaSub.LookupParameter("A Instance RLP Area %")?.AsDouble() ?? 0.0, 3),
                                                        smartRounder.sqFeetToSqMeters(areaSub.LookupParameter("A Instance RLP Area").AsDouble())});
            }
            else
            {
                areaAdjRangeDouble.set_Value(XlRangeValueDataType.xlRangeValueDefault, new object[] { smartRounder.sqFeetToSqMeters(areaSub.LookupParameter("Area").AsDouble()), 
                                                        DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 
                                                        DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value });
            }

            areaAdjRangeDouble.Font.Italic = true;

            Borders areaAdjRangeBorders = areaAdjRangeDouble.Borders;
            areaAdjRangeBorders[XlBordersIndex.xlEdgeLeft].LineStyle = XlLineStyle.xlContinuous;
            areaAdjRangeBorders[XlBordersIndex.xlEdgeTop].LineStyle = XlLineStyle.xlContinuous;
            areaAdjRangeBorders[XlBordersIndex.xlEdgeRight].LineStyle = XlLineStyle.xlContinuous;
            areaAdjRangeBorders[XlBordersIndex.xlEdgeBottom].LineStyle = XlLineStyle.xlContinuous;

            setBoldRange(workSheet, "N", "O", x);
        }
        private void setBoldRange(Worksheet workSheet, string startCell, string endCell, int row)
        {
            Range boldRange = workSheet.Range[$"{startCell}{row}", $"{endCell}{row}"];
            boldRange.Font.Bold = true;
        }
        private void setWrapRange(Worksheet worksheet, string startCell, string endCell, int row)
        {
            Range wrapRange = worksheet.Range[$"{startCell}{row}", $"{endCell}{row}"];
            wrapRange.WrapText = true;
        }
        private void setPlotBoundaries(Worksheet workSheet, string start, string end, int rangeStart, int rangeEnd)
        {
            Range cellsFive = workSheet.Range[$"{start}{rangeStart}", $"{end}{rangeEnd}"];
            Borders bordersFive = cellsFive.Borders;
            bordersFive[XlBordersIndex.xlEdgeLeft].LineStyle = XlLineStyle.xlContinuous;
            bordersFive[XlBordersIndex.xlEdgeTop].LineStyle = XlLineStyle.xlContinuous;
            bordersFive[XlBordersIndex.xlEdgeRight].LineStyle = XlLineStyle.xlContinuous;
            bordersFive[XlBordersIndex.xlEdgeBottom].LineStyle = XlLineStyle.xlContinuous;
            cellsFive.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.White);
        }
        private void setExcelDecimalsFormatting(Worksheet workSheet, int row)
        {
            workSheet.get_Range($"C{row}", $"D{row}").NumberFormat = "0.00";
            workSheet.get_Range($"E{row}", $"E{row}").NumberFormat = "0.0";
            workSheet.get_Range($"F{row}", $"F{row}").NumberFormat = "0.000";
            workSheet.get_Range($"G{row}", $"H{row}").NumberFormat = "0.000";
            workSheet.get_Range($"I{row}", $"L{row}").NumberFormat = "0.00";
            workSheet.get_Range($"M{row}", $"N{row}").NumberFormat = "0.000";
            workSheet.get_Range($"O{row}", $"O{row}").NumberFormat = "0.00";
        }
        private void setAllRangeBorders(Range range)
        {
            Borders blankBorders = range.Borders;
            blankBorders[XlBordersIndex.xlEdgeLeft].LineStyle = XlLineStyle.xlContinuous;
            blankBorders[XlBordersIndex.xlEdgeTop].LineStyle = XlLineStyle.xlContinuous;
            blankBorders[XlBordersIndex.xlEdgeRight].LineStyle = XlLineStyle.xlContinuous;
            blankBorders[XlBordersIndex.xlEdgeBottom].LineStyle = XlLineStyle.xlContinuous;
        }
        private void setSumFormulaExcludingRows(Worksheet workSheet, string column, int row, int startLine, int endLine, List<int> linesToExclude)
        {
            Range range = workSheet.Range[$"{column}{row}", $"{column}{row}"];
            string sumFormula = $"=SUM({column}{startLine}:{column}{endLine})";

            foreach (int line in linesToExclude)
            {
                sumFormula += $"-{column}{line}";
            }

            range.Formula = sumFormula;
        }
        private void setMergeBordersColorAndAlignment(Worksheet workSheet, string rangeStart, string rangeEnd, int row, bool setRowHeight, int rowHeight, 
            bool wrapText, bool setVerticalTop, bool colorWhite, bool colorGrey, bool setHorizontalCenter)
        {
            Range mergeRangeObject = workSheet.Range[$"{rangeStart}{row}", $"{rangeEnd}{row}"];
            mergeRangeObject.Merge();
            mergeRangeObject.Font.Bold = true;
            mergeRangeObject.Borders.LineStyle = XlLineStyle.xlContinuous;
            mergeRangeObject.HorizontalAlignment = XlHAlign.xlHAlignLeft;

            if (colorWhite) mergeRangeObject.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.White);

            if (colorGrey) mergeRangeObject.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);

            if (setRowHeight) mergeRangeObject.RowHeight = rowHeight;

            if (wrapText) mergeRangeObject.WrapText = true;

            if (setVerticalTop) mergeRangeObject.VerticalAlignment = XlVAlign.xlVAlignTop;

            if (setHorizontalCenter) mergeRangeObject.HorizontalAlignment = XlHAlign.xlHAlignCenter;
        }
        public string exportToExcel(string filePath, string sheetName)
        {
            string errormessage = "";

            Microsoft.Office.Interop.Excel.Application excelApplication = new Microsoft.Office.Interop.Excel.Application();
            Workbook workBook = excelApplication.Workbooks.Open(filePath, ReadOnly: false);

            // check if an excel sheet with the given name already exists
            Worksheet workSheet = null;

            bool doesSheetExist = false;
            foreach (Worksheet excelSheet in workBook.Sheets)
            {
                if (excelSheet.Name == sheetName)
                {
                    doesSheetExist = true;
                }
            }

            // if it does, get the existing one / if it does not, create a new one
            if (doesSheetExist)
            {
                workSheet = (Worksheet)workBook.Worksheets[sheetName];
            }
            else
            {
                TaskDialog.Show("Известие", "Ще бъде създаден нов sheet с посоченото име");

                workSheet = (Worksheet)workBook.Sheets.Add(After: workBook.Sheets[workBook.Sheets.Count]);
                workSheet.Name = sheetName;
            }

            // set columns' width
            workSheet.Range["A:A"].ColumnWidth = 15;
            workSheet.Range["B:B"].ColumnWidth = 25;
            workSheet.Range["C:C"].ColumnWidth = 10;
            workSheet.Range["D:D"].ColumnWidth = 10;
            workSheet.Range["E:F"].ColumnWidth = 10;
            workSheet.Range["G:O"].ColumnWidth = 10;

            workSheet.Range["Q:Q"].ColumnWidth = 15;
            workSheet.Range["R:R"].ColumnWidth = 10;

            int x = 1;

            // general formatting
            // main title : IPID and project number
            workSheet.Cells[x, 1] = "IPID";
            workSheet.Cells[x, 2] = doc.ProjectInformation.LookupParameter("Project Number").AsString();

            setMergeBordersColorAndAlignment(workSheet, "A", "A", x, false, 0, false, false, true, false, false);
            setMergeBordersColorAndAlignment(workSheet, "B", "O", x, true, 35, true, false, true, false, false);

            // main title : project name
            x += 2;
            workSheet.Cells[x, 1] = "ОБЕКТ";
            workSheet.Cells[x, 2] = doc.ProjectInformation.LookupParameter("Project Name").AsString();

            setMergeBordersColorAndAlignment(workSheet, "A", "A", x, false, 0, false, false, true, false, false);
            setMergeBordersColorAndAlignment(workSheet, "B", "O", x, true, 35, true, false, true, false, false);

            foreach (string plotName in plotNames)
            {
                x += 2;
                int rangeStart = x;

                // general plot data
                // plot row
                Range plotRange = workSheet.Range[$"A{x}", $"O{x}"];
                object[] plotStrings = new[] { "УПИ:", $"{smartRounder.sqFeetToSqMeters(plotAreasImp[plotName])}", "кв.м.", "", 
                    "Самостоятелни обекти и паркоместа:", "", "", "", "", "", "Забележки:", "", "", "", "", "" };                
                plotRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, plotStrings);
                workSheet.Cells[x, 2] = smartRounder.sqFeetToSqMeters(plotAreasImp[plotName]);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                plotRange.Font.Bold = true;

                // build up area row
                x++;
                Range baRange = workSheet.Range[$"A{x}", $"O{x}"];
                object[] baStrings = new[] { "ЗП:", $"{Math.Round(plotBuildAreas[plotName], 2)}", "кв.м.", "", "Ателиета:", "", "", "0", "бр", "", 
                    "", "", "", "", "", ""};                
                baRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, baStrings);
                workSheet.Cells[x, 2] = Math.Round(plotBuildAreas[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // total build area row
                x++;
                Range tbaRange = workSheet.Range[$"A{x}", $"O{x}"];
                string[] tbaStrings = new[] { "РЗП (надземна):", $"{Math.Round(plotTotalBuild[plotName], 2)}", "кв.м.", "",
                    "Апартаменти:", "", "", "0", "бр", "", "от площта на общите части са приспаднати XX.XX кв.м.", "", "", "", "", ""};                
                tbaRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, tbaStrings);
                workSheet.Cells[x, 2] = Math.Round(plotTotalBuild[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // underground row
                x++;
                Range uRange = workSheet.Range[$"A{x}", $"O{x}"];
                string[] uStrings = new[] { "РЗП (подземна):", $"{Math.Round(plotUndergroundAreas[plotName], 2)}", "кв.м.", "", 
                    "Магазини:", "", "", "0", "бр", "", "", "", "", "", "" };                
                uRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, uStrings);
                workSheet.Cells[x, 2] = Math.Round(plotUndergroundAreas[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // underground + tba row
                x++;
                Range utbaRange = workSheet.Range[$"A{x}", $"O{x}"];
                string[] utbaStrings = new[] { "РЗП общо:", $"{Math.Round(plotUndergroundAreas[plotName], 2) + Math.Round(plotTotalBuild[plotName], 2)}", 
                    "кв.м.", "", "Офиси", "", "", "0", "бр", "", "", "", "", "", "" };                
                utbaRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, utbaStrings);
                workSheet.Cells[x, 2] = Math.Round(plotUndergroundAreas[plotName], 2, MidpointRounding.AwayFromZero) 
                    + Math.Round(plotTotalBuild[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // CO row
                x++;
                Range coRange = workSheet.Range[$"A{x}", $"O{x}"];
                string[] coStrings = new[] { "Общо СО", $"{Math.Round(plotIndividualAreas[plotName], 2)}", "кв.м.", "", 
                    "Гаражи", "", "", "0", "бр", "", "", "", "", "", "", "" };                
                coRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, coStrings);
                workSheet.Cells[x, 2] = Math.Round(plotIndividualAreas[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // CA row
                x++;
                Range caRange = workSheet.Range[$"A{x}", $"O{x}"];
                string[] caStrings = new[] { "Общо ОЧ", $"{Math.Round(plotCommonAreas[plotName], 2)}", "кв.м.", "", 
                    "Складове", "", "", "0", "бр", "", "", "", "", "", "" };
                caRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, caStrings);
                workSheet.Cells[x, 2] = Math.Round(plotCommonAreas[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // land row
                x += 1;
                Range landRange = workSheet.Range[$"A{x}", $"O{x}"];
                string[] landStrings = new[] { "Земя към СО:", $"{Math.Round(plotAreasImp[plotName] / areaConvert, 2) - Math.Round(plotLandAreas[plotName], 2)}"
                    , "кв.м.", "", "Паркоместа", "", "", "0", "бр", "", "", "", "", "", "" };
                landRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, landStrings);
                workSheet.Cells[x, 2] = smartRounder.sqFeetToSqMeters(plotAreasImp[plotName]) - Math.Round(plotLandAreas[plotName], 2, MidpointRounding.AwayFromZero);
                workSheet.get_Range($"B{x}", $"B{x}").NumberFormat = "0.00";
                workSheet.get_Range($"B{x}", $"B{x}").HorizontalAlignment = XlHAlign.xlHAlignLeft;
                setBoldRange(workSheet, "A", "C", x);

                // set borders
                int rangeEnd = x;

                setPlotBoundaries(workSheet, "A", "C", rangeStart, rangeEnd);
                setPlotBoundaries(workSheet, "D", "D", rangeStart, rangeEnd);
                setPlotBoundaries(workSheet, "E", "I", rangeStart, rangeEnd);
                setPlotBoundaries(workSheet, "J", "J", rangeStart, rangeEnd);
                setPlotBoundaries(workSheet, "K", "O", rangeStart, rangeEnd);

                // a list, storing data about end lines of each separate proeprty group with respect to column M
                List<string> propertyEndLinesBuildingRigts = new List<string>();
                // a list, storing data about end lines of each separate property group with respect to column N
                List<string> propertyEndLinesLandSum = new List<string>();
                // a list, storing data about end lines of each separate proeprty group with respect to column O
                List<string> propertyEndLineslandSumArea = new List<string>();

                foreach (string property in plotProperties[plotName])
                {
                    if (!property.Contains("+") && !property.ToLower().Contains("траф") && !(property.ToLower().Equals("земя") && areAllLandAreasAdjascent(plotName)))
                    {
                        x += 2;
                        workSheet.Cells[x, 1] = $"ПЛОЩООБРАЗУВАНЕ САМОСТОЯТЕЛНИ ОБЕКТИ - {property}";

                        setMergeBordersColorAndAlignment(workSheet, "A", "O", x, true, 35, false, true, false, true, true);

                        x++;
                        Range indivdualRange = workSheet.Range[$"A{x}", $"O{x}"];
                        workSheet.Cells[x, 1] = $"ПЛОЩ СО: {Math.Round(propertyIndividualAreas[plotName][property], 2)} кв.м";
                        indivdualRange.Merge();
                        indivdualRange.HorizontalAlignment = XlHAlign.xlHAlignLeft;
                        indivdualRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        indivdualRange.Font.Bold = true;
                        indivdualRange.Borders.LineStyle = XlLineStyle.xlContinuous;
                        indivdualRange.RowHeight = 35;
                        indivdualRange.VerticalAlignment = XlVAlign.xlVAlignTop;

                        x++;
                        Range propertyDataRange = workSheet.Range[$"A{x}", $"O{x}"];
                        workSheet.Cells[x, 1] = $"ПЛОЩ ОЧ: {Math.Round(propertyCommonAreasAll[plotName][property], 2)} кв.м, от които " +
                            $"стандартни ОЧ: {Math.Round(propertyCommonAreas[plotName][property], 2)} кв.м. " +
                            $"и специални ОЧ: {Math.Round(propertyCommonAreasSpecial[plotName][property], 2)} кв.м.";
                        propertyDataRange.Merge();
                        propertyDataRange.Font.Bold = true;
                        propertyDataRange.Borders.LineStyle = XlLineStyle.xlContinuous;
                        propertyDataRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        propertyDataRange.RowHeight = 35;
                        propertyDataRange.VerticalAlignment = XlVAlign.xlVAlignTop;

                        x++;
                        workSheet.Cells[x, 8] = "ОБЩИ ЧАСТИ - F3";
                        Range commonLabelRange = workSheet.Range[$"H{x}", $"K{x}"];
                        commonLabelRange.Merge();
                        commonLabelRange.Font.Bold = true;
                        commonLabelRange.Borders.LineStyle = XlLineStyle.xlContinuous;
                        commonLabelRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        commonLabelRange.RowHeight = 35;
                        commonLabelRange.VerticalAlignment = XlVAlign.xlVAlignTop;
                        commonLabelRange.HorizontalAlignment = XlHAlign.xlHAlignCenter; 

                        x++;
                        Range parameterNamesRange = workSheet.Range[$"A{x}", $"O{x}"];
                        string[] parameterNamesData = new[] { "НОМЕР СО", "НАИМЕНОВАНИЕ СО", "ПЛОЩ\nF1(F2)", "ПРИЛ.\nПЛОЩ", "КОЕФ.", "C1(C2)",
                            "ДЯЛ ОТ\nПОДЗЕМЕН\nПАРКИНГ\nГАРАЖ", "О.Ч.", "", "СПЕЦ.\nО.Ч.", "ОБЩО\nО.Ч.", "ОБЩО\nF1(F2)+F3", "ПРАВО\nНА\nСТРОЕЖ", "ЗЕМЯ", ""};
                        parameterNamesRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, parameterNamesData);
                        parameterNamesRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        parameterNamesRange.Borders.LineStyle = XlLineStyle.xlContinuous;
                        parameterNamesRange.VerticalAlignment = XlVAlign.xlVAlignTop;
                        parameterNamesRange.Font.Bold = true;

                        Range numberRange = workSheet.Range[$"A{x-1}", $"A{x}"];
                        numberRange.Merge();
                        numberRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(numberRange);

                        Range nameRange = workSheet.Range[$"B{x - 1}", $"B{x}"];
                        nameRange.Merge();
                        nameRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(nameRange);

                        Range areaRange = workSheet.Range[$"C{x - 1}", $"C{x}"];
                        areaRange.Merge();
                        areaRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(areaRange);

                        Range adjAreaRange = workSheet.Range[$"D{x - 1}", $"D{x}"];
                        adjAreaRange.Merge();
                        adjAreaRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(adjAreaRange);

                        Range coefRange = workSheet.Range[$"E{x - 1}", $"E{x}"];
                        coefRange.Merge();
                        coefRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(coefRange);

                        Range cRange = workSheet.Range[$"F{x - 1}", $"F{x}"];
                        cRange.Merge();
                        cRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(cRange);

                        Range percentaParkingPlaces = workSheet.Range[$"G{x - 1}", $"G{x}"];
                        percentaParkingPlaces.Merge();
                        percentaParkingPlaces.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(percentaParkingPlaces);

                        Range commonMerge = workSheet.Range[$"H{x}", $"I{x}"];
                        commonMerge.Merge();
                        commonMerge.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(commonMerge);

                        Range specialCommonRange = workSheet.Range[$"J{x}", $"J{x}"];
                        specialCommonRange.Merge();
                        specialCommonRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(specialCommonRange);

                        Range totalCommonRange = workSheet.Range[$"K{x}", $"K{x}"];
                        totalCommonRange.Merge();
                        totalCommonRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(totalCommonRange);

                        Range totalSumRange = workSheet.Range[$"L{x - 1}", $"L{x}"];
                        totalSumRange.Merge();
                        totalSumRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(totalSumRange);

                        Range buildingRightsRange = workSheet.Range[$"M{x - 1}", $"M{x}"];
                        buildingRightsRange.Merge();
                        buildingRightsRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(buildingRightsRange);

                        Range landMerge = workSheet.Range[$"N{x-1}", $"O{x}"];
                        landMerge.Merge();
                        landMerge.HorizontalAlignment = XlHAlign.xlHAlignCenter;
                        setAllRangeBorders(landMerge);

                        x++;
                        Range parametersTypeRange = workSheet.Range[$"A{x}", $"O{x}"];
                        string[] parametersTypeData = new[] { "", "", "m2", "m2", "", "", "% и.ч.", "% и.ч.", "m2", "m2", "m2", "m2", "% и.ч.", "% и.ч.", "m2" };
                        parametersTypeRange.set_Value(XlRangeValueDataType.xlRangeValueDefault, parametersTypeData);
                        parametersTypeRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        parametersTypeRange.Font.Bold = true;
                        parametersTypeRange.Borders.LineStyle = XlLineStyle.xlContinuous;
                        parametersTypeRange.HorizontalAlignment = XlHAlign.xlHAlignCenter;

                        x++;
                        Range blankLineRange = workSheet.Range[$"A{x}", $"O{x}"];
                        blankLineRange.Merge();
                        blankLineRange.UnMerge();
                        blankLineRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                        Borders blankBorders = blankLineRange.Borders;
                        blankBorders[XlBordersIndex.xlEdgeLeft].LineStyle = XlLineStyle.xlContinuous;
                        blankBorders[XlBordersIndex.xlEdgeTop].LineStyle = XlLineStyle.xlContinuous;
                        blankBorders[XlBordersIndex.xlEdgeRight].LineStyle = XlLineStyle.xlContinuous;
                        blankBorders[XlBordersIndex.xlEdgeBottom].LineStyle = XlLineStyle.xlContinuous;

                        x++;

                        int startLine = x;

                        List<Area> sortedAreasNormal = AreasOrganizer[plotName][property]
                                .Where(area => area.LookupParameter("A Instance Area Category").AsString().ToLower().Equals("самостоятелен обект"))
                                //.Where(area => !(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                                .Where(area => !area.LookupParameter("A Instance Area Group").AsString().Equals("ЗЕМЯ"))
                                .OrderBy(area => ReorderEntrance(area.LookupParameter("A Instance Area Entrance").AsString()))
                                .ThenBy(area => ExtractLevelHeight(area.Level))
                                .ThenBy(area => area.LookupParameter("Number").AsString())
                                .ToList();

                        List<Area> areasToSort = new List<Area>();
                        if (AreasOrganizer[plotName].ContainsKey("ЗЕМЯ"))
                            areasToSort.AddRange(AreasOrganizer[plotName]["ЗЕМЯ"]);
                        if (AreasOrganizer[plotName].ContainsKey("ТРАФ"))
                            areasToSort.AddRange(AreasOrganizer[plotName]["ТРАФ"]);

                        List<Area> sortedAreasGround = areasToSort
                                .Where(area => area.LookupParameter("A Instance Area Group").AsString().ToLower().Equals("земя"))
                                .OrderBy(area => ReorderEntrance(area.LookupParameter("A Instance Area Entrance").AsString()))
                                .ThenBy(area => ExtractLevelHeight(area.Level))
                                .ThenBy(area => area.LookupParameter("Number").AsString())
                                .ToList();

                        List<Area> sortedAreas = new List<Area>();

                        if (property.ToLower() != "земя")
                        {
                            sortedAreas = sortedAreasNormal;
                        }
                        else
                        {
                            sortedAreas = sortedAreasGround;
                        }

                        List<string> levels = new List<string>();
                        List<string> entrances = new List<string>();

                        List<int> linesToExclude = new List<int>();
                        List<int> linesToExcludeLand = new List<int>();

                        string levelHeightStr = "";

                        foreach (Area area in sortedAreas)
                        {
                            if (!(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                            {
                                if (!entrances.Contains(area.LookupParameter("A Instance Area Entrance").AsString()))
                                {
                                    entrances.Add(area.LookupParameter("A Instance Area Entrance").AsString());

                                    if (area.LookupParameter("A Instance Area Entrance").AsString().ToLower() != "НЕПРИЛОЖИМО")
                                    {
                                        workSheet.Cells[x, 1] = area.LookupParameter("A Instance Area Entrance").AsString();
                                        Range entranceRangeString = workSheet.Range[$"A{x}", $"O{x}"];
                                        entranceRangeString.Merge();
                                        entranceRangeString.UnMerge();
                                        entranceRangeString.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                                        x++;
                                        levels.Clear();
                                    }
                                }

                                if (!levels.Contains(area.LookupParameter("Level").AsValueString()))
                                {
                                    levels.Add(area.LookupParameter("Level").AsValueString());
                                    double levelHeight = Math.Round(doc.GetElement(area.LookupParameter("Level").AsElementId()).LookupParameter("Elevation").AsDouble() * lengthConvert / 100, 3);

                                    if (levelHeight < 0)
                                    {
                                        string tempString = levelHeight.ToString("F3");
                                        levelHeightStr = $"{tempString}";
                                    }
                                    else if (levelHeight > 0)
                                    {
                                        string tempString = levelHeight.ToString("F3");
                                        levelHeightStr = $"+ {tempString}";
                                    }
                                    else
                                    {
                                        string tempString = levelHeight.ToString("F3");
                                        levelHeightStr = $"± {tempString}";
                                    }

                                    workSheet.Cells[x, 1] = $"КОТА {levelHeightStr}";
                                    Range levelsRangeString = workSheet.Range[$"A{x}", $"O{x}"];
                                    levelsRangeString.Merge();
                                    levelsRangeString.UnMerge();
                                    levelsRangeString.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);
                                    x++;
                                }

                                try
                                {
                                    Range cellRangeString = workSheet.Range[$"A{x}", $"B{x}"];
                                    Range cellRangeDouble = workSheet.Range[$"C{x}", $"O{x}"];

                                    string areaNumber = area.LookupParameter("Number").AsString() ?? "SOMETHING'S WRONG";
                                    string areaName = area.LookupParameter("Name")?.AsString() ?? "SOMETHING'S WRONG";
                                    double areaArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area")?.AsDouble() ?? 0.0);
                                    // TODO: rework properly for adjascent area
                                    object areaSubjected = DBNull.Value;
                                    // TODO: rework properly for adjascent area
                                    double ACCO = area.LookupParameter("A Coefficient Multiplied")?.AsDouble() ?? 0.0;
                                    double C1C2 = Math.Round(area.LookupParameter("A Instance Price C1/C2")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                    double areaCommonPercent = Math.Round(area.LookupParameter("A Instance Common Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                    double areaCommonArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area")?.AsDouble() ?? 0.0);
                                    double areaCommonAreaSpecial = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area Special")?.AsDouble() ?? 0.0);
                                    double areaTotalArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Total Area")?.AsDouble() ?? 0.0);
                                    double areaPermitPercent = Math.Round(area.LookupParameter("A Instance Building Permit %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                    double areaRLPPercentage = Math.Round(area.LookupParameter("A Instance RLP Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                    double areaRLP = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance RLP Area")?.AsDouble() ?? 0.0);

                                    string[] areaStringData = new[] { areaNumber, areaName };
                                    object[] areasDoubleData = new object[] { };

                                    if (property.ToLower() != "земя")
                                    {
                                        areasDoubleData = new object[] { areaArea, areaSubjected, ACCO, C1C2, DBNull.Value, areaCommonPercent, areaCommonArea, areaCommonAreaSpecial,
                                        areaCommonArea + areaCommonAreaSpecial, areaTotalArea, areaPermitPercent, areaRLPPercentage, areaRLP};
                                    }
                                    else 
                                    {
                                        areasDoubleData = new object[] { areaArea, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 
                                        DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, areaRLPPercentage, areaRLP};
                                    }
                                    
                                    for (int i = 0; i < areasDoubleData.Length; i++)
                                    {
                                        if (areasDoubleData[i] is double && (double)areasDoubleData[i] == 0.0)
                                        {
                                            areasDoubleData[i] = DBNull.Value;
                                        }
                                    }
                                    
                                    cellRangeString.set_Value(XlRangeValueDataType.xlRangeValueDefault, areaStringData);
                                    cellRangeString.Borders.LineStyle = XlLineStyle.xlContinuous;

                                    cellRangeDouble.set_Value(XlRangeValueDataType.xlRangeValueDefault, areasDoubleData);
                                    cellRangeDouble.Borders.LineStyle = XlLineStyle.xlContinuous;

                                    setBoldRange(workSheet, "C", "C", x);
                                    setBoldRange(workSheet, "H", "I", x);
                                    setBoldRange(workSheet, "L", "O", x);
                                    setWrapRange(workSheet, "B", "B", x);

                                    setExcelDecimalsFormatting(workSheet, x);

                                    // additional temporary fileds
                                    workSheet.Cells[x, 17] = $"КОТА {levelHeightStr}";
                                    workSheet.Cells[x, 18] = $"{Math.Round(100 * (areaCommonAreaSpecial + areaCommonArea) / areaTotalArea, 3, MidpointRounding.AwayFromZero)}";
                                }
                                catch
                                {
                                    Range cellRangeString = workSheet.Range[$"A{x}", $"B{x}"];
                                    string[] cellsStrings = new[] { "X", "Y" };
                                    cellRangeString.set_Value(XlRangeValueDataType.xlRangeValueDefault, cellsStrings);
                                }

                                x++;

                                // identify adjascent areas, if any
                                List<Area> adjascentAreasRegular = new List<Area>();

                                foreach (Area areaSub in sortedAreas)
                                {
                                    string primaryArea = areaSub.LookupParameter("A Instance Area Primary").AsString();

                                    if (primaryArea != null && primaryArea.Equals(area.LookupParameter("Number").AsString()))
                                    {
                                        adjascentAreasRegular.Add(areaSub);
                                    }
                                }

                                // also search for adjascent areas from within ground property group
                                List<Area> adjascentAreasLand = new List<Area>();

                                if (AreasOrganizer[plotName].ContainsKey("ЗЕМЯ"))
                                {
                                    foreach (Area areaGround in AreasOrganizer[plotName]["ЗЕМЯ"])
                                    {
                                        string primaryArea = areaGround.LookupParameter("A Instance Area Primary").AsString();

                                        if (primaryArea != null && primaryArea.Equals(area.LookupParameter("Number").AsString()))
                                        {
                                            adjascentAreasLand.Add(areaGround);
                                        }
                                    }
                                }

                                if (!doesHaveRoomsAdjascent(area.LookupParameter("Number").AsString()))
                                {
                                    if (adjascentAreasRegular.Count != 0)
                                    {
                                        adjascentAreasRegular.OrderBy(adjArea => adjArea.LookupParameter("Number").AsString()).ToList();
                                        adjascentAreasRegular.Insert(0, area);

                                        // write adjascent areas in excel sheet
                                        foreach (Area areaSub in adjascentAreasRegular)
                                        {
                                            exportToExcelAdjascentRegular(workSheet, x, areaSub, false, area.LookupParameter("Number").AsString());
                                            linesToExclude.Add(x);
                                            linesToExcludeLand.Add(x);
                                            x++;
                                        }
                                    }

                                    if (adjascentAreasLand.Count != 0)
                                    {
                                        adjascentAreasLand.OrderBy(adjArea => adjArea.LookupParameter("Number").AsString()).ToList();

                                        foreach (Area areaSub in adjascentAreasLand)
                                        {
                                            exportToExcelAdjascentRegular(workSheet, x, areaSub, true, area.LookupParameter("Number").AsString());
                                            x++;
                                        }
                                    }

                                    /*
                                    // adjascent areas loop for special common areas
                                    foreach (Area areaSub in AreasOrganizer[plotName][property])
                                    {
                                        if (areaSub.LookupParameter("A Instance Area Category").AsString().ToLower() == "обща част")
                                        {
                                            string primaryArea = areaSub.LookupParameter("A Instance Area Primary").AsString();

                                            if (primaryArea != null)
                                            {
                                                string[] result = primaryArea.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                                                    .Select(s => s.Trim())
                                                    .ToArray();

                                                if (result.Contains(area.LookupParameter("Number").AsString()))
                                                {
                                                    Range areaAdjRangeStr = workSheet.Range[$"A{x}", $"O{x}"];
                                                    areaAdjRangeStr.set_Value(XlRangeValueDataType.xlRangeValueDefault, new[] { areaSub.LookupParameter("Number").AsString(),
                                                                                                                                areaSub.LookupParameter("Name").AsString() });

                                                    areaAdjRangeStr.HorizontalAlignment = XlHAlign.xlHAlignRight;
                                                    areaAdjRangeStr.Borders.LineStyle = XlLineStyle.xlContinuous;

                                                    Range areaAdjRangeDouble = workSheet.Range[$"C{x}", $"W{x}"];
                                                    areaAdjRangeDouble.set_Value(XlRangeValueDataType.xlRangeValueDefault, new object[] {DBNull.Value, DBNull.Value, DBNull.Value, 
                                                                DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 
                                                                DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value });

                                                    Borders areaAdjRangeBorders = areaAdjRangeDouble.Borders;
                                                    areaAdjRangeBorders[XlBordersIndex.xlEdgeLeft].LineStyle = XlLineStyle.xlContinuous;
                                                    areaAdjRangeBorders[XlBordersIndex.xlEdgeTop].LineStyle = XlLineStyle.xlContinuous;
                                                    areaAdjRangeBorders[XlBordersIndex.xlEdgeRight].LineStyle = XlLineStyle.xlContinuous;
                                                    areaAdjRangeBorders[XlBordersIndex.xlEdgeBottom].LineStyle = XlLineStyle.xlContinuous;

                                                    x++;
                                                }
                                            }
                                        }
                                    }
                                    */
                                }

                                else
                                {
                                    Dictionary<List<object>, Room> adjascentRooms = returnAdjascentRooms(area);

                                    foreach (List<object> key in adjascentRooms.Keys)
                                    {
                                        Room room = adjascentRooms[key];

                                        Range areaAdjRangeStr = workSheet.Range[$"A{x}", $"B{x}"];
                                        areaAdjRangeStr.set_Value(XlRangeValueDataType.xlRangeValueDefault,
                                            new[] { key[0], room.LookupParameter("Name").AsString() });

                                        areaAdjRangeStr.Font.Italic = true;
                                        areaAdjRangeStr.HorizontalAlignment = XlHAlign.xlHAlignRight;
                                        areaAdjRangeStr.Borders.LineStyle = XlLineStyle.xlContinuous;

                                        Range areaAdjRangeDouble = workSheet.Range[$"C{x}", $"O{x}"];
                                        areaAdjRangeDouble.set_Value(XlRangeValueDataType.xlRangeValueDefault, new object[] {
                                                        smartRounder.sqFeetToSqMeters(room.LookupParameter("Area").AsDouble()),
                                                        DBNull.Value, DBNull.Value, DBNull.Value, 
                                                        key[1], key[2], key[3], key[4], key[5], key[6], key[7], key[8], key[9], DBNull.Value, DBNull.Value});

                                        Borders areaAdjRangeBorders = areaAdjRangeDouble.Borders;
                                        areaAdjRangeBorders[XlBordersIndex.xlEdgeLeft].LineStyle = XlLineStyle.xlContinuous;
                                        areaAdjRangeBorders[XlBordersIndex.xlEdgeTop].LineStyle = XlLineStyle.xlContinuous;
                                        areaAdjRangeBorders[XlBordersIndex.xlEdgeRight].LineStyle = XlLineStyle.xlContinuous;
                                        areaAdjRangeBorders[XlBordersIndex.xlEdgeBottom].LineStyle = XlLineStyle.xlContinuous;

                                        setExcelDecimalsFormatting(workSheet, x);

                                        linesToExclude.Add(x);
                                        linesToExcludeLand.Add(x);

                                        x++;
                                    }

                                    errormessage += areAdjascentRoomsAndAreasEqual(adjascentRooms, adjascentAreasRegular, adjascentAreasLand, area);
                                }
                            }
                        }

                        int endLine = x - 1;
                        propertyEndLinesBuildingRigts.Add($"M{x}");
                        propertyEndLinesLandSum.Add($"N{x}");
                        propertyEndLineslandSumArea.Add($"O{x}");

                        Range colorRange = workSheet.Range[$"C{startLine}", $"O{endLine}"];
                        //colorRange.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.AliceBlue);

                        // set a formula for the total area sum of F1/F2
                        setSumFormulaExcludingRows(workSheet, "C", x, startLine, endLine, linesToExclude);
                        setBoldRange(workSheet, "C", "C", x);

                        // set a formula for the total sum of adjascent areas
                        Range sumAdjascent = workSheet.Range[$"D{x}", $"D{x}"];
                        sumAdjascent.Formula = $"=SUM(D{startLine}:D{endLine})";

                        // set a formula for the total sum of C1/C2
                        Range sumC1C2 = workSheet.Range[$"F{x}", $"F{x}"];
                        sumC1C2.Formula = $"=SUM(F{startLine}:F{endLine})";

                        // set a formula for the total sum of Common Areas 
                        setSumFormulaExcludingRows(workSheet, "H", x, startLine, endLine, linesToExclude);
                        setBoldRange(workSheet, "H", "H", x);

                        // set a formula for the total sum of Special Common Areas Percentage
                        setSumFormulaExcludingRows(workSheet, "I", x, startLine, endLine, linesToExclude);
                        setBoldRange(workSheet, "I", "I", x);

                        // set a formula for the total sum of Special Common Areas Percentage
                        setSumFormulaExcludingRows(workSheet, "J", x, startLine, endLine, linesToExclude);

                        // set a formula for the total sum of all common areas
                        setSumFormulaExcludingRows(workSheet, "K", x, startLine, endLine, linesToExclude);

                        // set a formula for the total sum of Total Area
                        setSumFormulaExcludingRows(workSheet, "L", x, startLine, endLine, linesToExclude);
                        setBoldRange(workSheet, "L", "L", x);

                        // set a formula for the total sum of Building Right Percentage
                        setSumFormulaExcludingRows(workSheet, "M", x, startLine, endLine, linesToExclude);
                        setBoldRange(workSheet, "M", "M", x);

                        // set a formula for the total sum of Land Area Percentage
                        setSumFormulaExcludingRows(workSheet, "N", x, startLine, endLine, linesToExcludeLand);
                        setBoldRange(workSheet, "N", "N", x);

                        // set a formula for the total sum of Land Area
                        setSumFormulaExcludingRows(workSheet, "O", x, startLine, endLine, linesToExcludeLand);
                        setBoldRange(workSheet, "O", "O", x);

                        // set coloring for the summed up rows
                        Range colorRangePropertySum = workSheet.Range[$"A{endLine + 1}", $"O{endLine + 1}"];
                        colorRangePropertySum.Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.LightGray);

                        // set final decimal formatting
                        setExcelDecimalsFormatting(workSheet, x);

                        x++;
                    }
                }

                string propertyBuildingRightsFormula = $"=SUM({string.Join(",", propertyEndLinesBuildingRigts)})";
                workSheet.Range[$"M{x}"].Formula = propertyBuildingRightsFormula;
                workSheet.Range[$"M{x}"].Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.AntiqueWhite);

                string propertyLandSumFormula = $"=SUM({string.Join(",", propertyEndLinesLandSum)})";
                workSheet.Range[$"N{x}"].Formula = propertyLandSumFormula;
                workSheet.Range[$"N{x}"].Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.AntiqueWhite);

                string propertyLandSumAreaFormula = $"=SUM({string.Join(",", propertyEndLineslandSumArea)})";
                workSheet.Range[$"O{x}"].Formula = propertyLandSumAreaFormula;
                workSheet.Range[$"O{x}"].Interior.Color = ColorTranslator.ToOle(System.Drawing.Color.AntiqueWhite);
            }

            workBook.Save();
            workBook.Close();

            return errormessage;
        }

        // ClosedXML helper methods
        private void setMergeBordersColorAndAlignmentXL(IXLWorksheet workSheet, string rangeStart, string rangeEnd, int row, bool setRowHeight, int rowHeight,
            bool wrapText, bool setVerticalTop, bool colorWhite, bool colorGrey, bool setHorizontalCenter)
        {
            IXLRange mergeRange = workSheet.Range($"{rangeStart}{row}:{rangeEnd}{row}");
            mergeRange.Merge();
            mergeRange.Style.Font.Bold = true;
            mergeRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            mergeRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            mergeRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

            if (colorWhite) mergeRange.Style.Fill.BackgroundColor = XLColor.White;
            if (colorGrey) mergeRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            if (setRowHeight) workSheet.Row(row).Height = rowHeight;
            if (wrapText) mergeRange.Style.Alignment.WrapText = true;
            if (setVerticalTop) mergeRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            if (setHorizontalCenter) mergeRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        private void setBoldRangeXL(IXLWorksheet workSheet, string startCell, string endCell, int row)
        {
            IXLRange boldRange = workSheet.Range($"{startCell}{row}:{endCell}{row}");
            boldRange.Style.Font.Bold = true;
        }
        private void setPlotBoundariesXL(IXLWorksheet workSheet, string start, string end, int rangeStart, int rangeEnd)
        {
            IXLRange cellsRange = workSheet.Range($"{start}{rangeStart}:{end}{rangeEnd}");
            cellsRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cellsRange.Style.Fill.BackgroundColor = XLColor.White;
        }
        private void setExcelDecimalsFormattingXL(IXLWorksheet workSheet, int row)
        {
            workSheet.Range($"C{row}:D{row}").Style.NumberFormat.Format = "0.00";
            workSheet.Range($"E{row}:E{row}").Style.NumberFormat.Format = "0.0";
            workSheet.Range($"F{row}:F{row}").Style.NumberFormat.Format = "0.000";
            workSheet.Range($"G{row}:H{row}").Style.NumberFormat.Format = "0.000";
            workSheet.Range($"I{row}:L{row}").Style.NumberFormat.Format = "0.00";
            workSheet.Range($"M{row}:N{row}").Style.NumberFormat.Format = "0.000";
            workSheet.Range($"O{row}:O{row}").Style.NumberFormat.Format = "0.00";
        }
        private void setAllRangeBordersXL(IXLRange range)
        {
            range.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
            range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            range.Style.Border.RightBorder = XLBorderStyleValues.Thin;
            range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
        private void setWrapRangeXL(IXLWorksheet workSheet, string startCell, string endCell, int row)
        {
            IXLRange wrapRange = workSheet.Range($"{startCell}{row}:{endCell}{row}");
            wrapRange.Style.Alignment.WrapText = true;
        }
        private void exportToExcelAdjascentRegularXL(IXLWorksheet workSheet, int x, Area areaSub, bool isLand, string mainAreaNumber)
        {
            // Write area data to A and B columns
            workSheet.Cell(x, 1).Value = areaSub.LookupParameter("Number").AsString();
            workSheet.Cell(x, 2).Value = areaSub.LookupParameter("Custom Name").AsString();
            
            IXLRange areaAdjRangeStr = workSheet.Range($"A{x}:B{x}");
            areaAdjRangeStr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            areaAdjRangeStr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            areaAdjRangeStr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            // Apply borders to the rest of the row (C through O)
            IXLRange areaAdjRangeDouble = workSheet.Range($"C{x}:O{x}");
            areaAdjRangeDouble.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            areaAdjRangeDouble.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        private void setSumFormulaExcludingRowsXL(IXLWorksheet workSheet, string column, int row, int startLine, int endLine, List<int> linesToExclude)
        {
            IXLCell cell = workSheet.Cell(row, column);
            string sumFormula = $"=SUM({column}{startLine}:{column}{endLine})";

            foreach (int line in linesToExclude)
            {
                sumFormula += $"-{column}{line}";
            }

            cell.FormulaA1 = sumFormula;
        }
        // ClosedXML version of exportToExcel
        public string exportToExcelClosedXML(string filePath, string sheetName)
        {
            string errorMessage = "";

            try
            {
                using (var workBook = new XLWorkbook(filePath))
                {
                    IXLWorksheet workSheet;
                    if (workBook.Worksheets.Contains(sheetName))
                    {
                        workSheet = workBook.Worksheet(sheetName);
                    }
                    else
                    {
                        TaskDialog.Show("Известие", "Ще бъде създаден нов sheet с посоченото име");
                        workSheet = workBook.Worksheets.Add(sheetName);
                    }

                    // Set column widths (lines 1675-1684)
                    workSheet.Column(1).Width = 15;  // A
                    workSheet.Column(2).Width = 25;  // B
                    workSheet.Column(3).Width = 10;  // C
                    workSheet.Column(4).Width = 10;  // D
                    workSheet.Columns(5, 6).Width = 10;  // E:F
                    workSheet.Columns(7, 15).Width = 10;  // G:O
                    workSheet.Column(17).Width = 15;  // Q
                    workSheet.Column(18).Width = 10;  // R

                    int x = 1;

                    // General formatting
                    // Main title: IPID and project number (lines 1689-1693)
                    workSheet.Cell(x, 1).Value = "IPID";
                    workSheet.Cell(x, 2).Value = doc.ProjectInformation.LookupParameter("Project Number").AsString();

                    setMergeBordersColorAndAlignmentXL(workSheet, "A", "A", x, false, 0, false, false, true, false, false);
                    setMergeBordersColorAndAlignmentXL(workSheet, "B", "O", x, true, 35, true, false, true, false, false);

                    // Main title: project name (lines 1696-1701)
                    x += 2;
                    workSheet.Cell(x, 1).Value = "ОБЕКТ";
                    workSheet.Cell(x, 2).Value = doc.ProjectInformation.LookupParameter("Project Name").AsString();

                    setMergeBordersColorAndAlignmentXL(workSheet, "A", "A", x, false, 0, false, false, true, false, false);
                    setMergeBordersColorAndAlignmentXL(workSheet, "B", "O", x, true, 35, true, false, true, false, false);

                    foreach (string plotName in plotNames)
                    {
                        x += 2;
                        int rangeStart = x;

                        // general plot data
                        // plot row (lines 1709-1717)
                        workSheet.Cell(x, 1).Value = "УПИ:";
                        workSheet.Cell(x, 2).Value = smartRounder.sqFeetToSqMeters(plotAreasImp[plotName]);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Самостоятелни обекти и паркоместа:";
                        workSheet.Cell(x, 11).Value = "Забележки:";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        var plotRange = workSheet.Range($"A{x}:O{x}");
                        plotRange.Style.Font.Bold = true;

                        // build up area row (lines 1720-1728)
                        x++;
                        workSheet.Cell(x, 1).Value = "ЗП:";
                        workSheet.Cell(x, 2).Value = Math.Round(plotBuildAreas[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Ателиета:";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        var baRange = workSheet.Range($"A{x}:C{x}");
                        baRange.Style.Font.Bold = true;

                        // total build area row (lines 1730-1739)
                        x++;
                        workSheet.Cell(x, 1).Value = "РЗП (надземна):";
                        workSheet.Cell(x, 2).Value = Math.Round(plotTotalBuild[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Апартаменти:";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        workSheet.Cell(x, 11).Value = "от площта на общите части са приспаднати XX.XX кв.м.";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        setBoldRangeXL(workSheet, "A", "C", x);

                        // underground row (lines 1741-1750)
                        x++;
                        workSheet.Cell(x, 1).Value = "РЗП (подземна):";
                        workSheet.Cell(x, 2).Value = Math.Round(plotUndergroundAreas[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Магазини:";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        setBoldRangeXL(workSheet, "A", "C", x);

                        // underground + tba row (lines 1752-1762)
                        x++;
                        workSheet.Cell(x, 1).Value = "РЗП общо:";
                        workSheet.Cell(x, 2).Value = Math.Round(plotUndergroundAreas[plotName], 2, MidpointRounding.AwayFromZero) 
                            + Math.Round(plotTotalBuild[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Офиси";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        setBoldRangeXL(workSheet, "A", "C", x);

                        // CO row (lines 1764-1773)
                        x++;
                        workSheet.Cell(x, 1).Value = "Общо СО";
                        workSheet.Cell(x, 2).Value = Math.Round(plotIndividualAreas[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Гаражи";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        setBoldRangeXL(workSheet, "A", "C", x);

                        // CA row (lines 1775-1784)
                        x++;
                        workSheet.Cell(x, 1).Value = "Общо ОЧ";
                        workSheet.Cell(x, 2).Value = Math.Round(plotCommonAreas[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Складове";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        setBoldRangeXL(workSheet, "A", "C", x);

                        // land row (lines 1786-1795)
                        x++;
                        workSheet.Cell(x, 1).Value = "Земя към СО:";
                        workSheet.Cell(x, 2).Value = smartRounder.sqFeetToSqMeters(plotAreasImp[plotName]) - Math.Round(plotLandAreas[plotName], 2, MidpointRounding.AwayFromZero);
                        workSheet.Cell(x, 3).Value = "кв.м.";
                        workSheet.Cell(x, 5).Value = "Паркоместа";
                        workSheet.Cell(x, 8).Value = 0;
                        workSheet.Cell(x, 9).Value = "бр";
                        
                        workSheet.Cell(x, 2).Style.NumberFormat.Format = "0.00";
                        workSheet.Cell(x, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                        setBoldRangeXL(workSheet, "A", "C", x);

                        // set borders (lines 1797-1804)
                        int rangeEnd = x;
                        setPlotBoundariesXL(workSheet, "A", "C", rangeStart, rangeEnd);
                        setPlotBoundariesXL(workSheet, "D", "D", rangeStart, rangeEnd);
                        setPlotBoundariesXL(workSheet, "E", "I", rangeStart, rangeEnd);
                        setPlotBoundariesXL(workSheet, "J", "J", rangeStart, rangeEnd);
                        setPlotBoundariesXL(workSheet, "K", "O", rangeStart, rangeEnd);

                        // Property lists setup (lines 1806-1812)
                        List<string> propertyEndLinesBuildingRigts = new List<string>();
                        List<string> propertyEndLinesLandSum = new List<string>();
                        List<string> propertyEndLineslandSumArea = new List<string>();

                        // Implement foreach loop (lines 1813-1837)
                        foreach (string property in plotProperties[plotName])
                        {
                            if (!property.Contains("+") && !property.ToLower().Contains("траф") && !(property.ToLower().Equals("земя") && areAllLandAreasAdjascent(plotName)))
                            {
                                x += 2;
                                workSheet.Cell(x, 1).Value = $"ПЛОЩООБРАЗУВАНЕ САМОСТОЯТЕЛНИ ОБЕКТИ - {property}";

                                setMergeBordersColorAndAlignmentXL(workSheet, "A", "O", x, true, 35, false, true, false, true, true);

                                x++;
                                workSheet.Cell(x, 1).Value = $"ПЛОЩ СО: {Math.Round(propertyIndividualAreas[plotName][property], 2)} кв.м";
                                
                                IXLRange individualRange = workSheet.Range($"A{x}:O{x}");
                                individualRange.Merge();
                                individualRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                                individualRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                individualRange.Style.Font.Bold = true;
                                individualRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                individualRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                workSheet.Row(x).Height = 35;
                                individualRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                                x++;
                                // Property data range (lines 1838-1847)
                                workSheet.Cell(x, 1).Value = $"ПЛОЩ ОЧ: {Math.Round(propertyCommonAreasAll[plotName][property], 2)} кв.м, от които " +
                                    $"стандартни ОЧ: {Math.Round(propertyCommonAreas[plotName][property], 2)} кв.м. " +
                                    $"и специални ОЧ: {Math.Round(propertyCommonAreasSpecial[plotName][property], 2)} кв.м.";
                                
                                IXLRange propertyDataRange = workSheet.Range($"A{x}:O{x}");
                                propertyDataRange.Merge();
                                propertyDataRange.Style.Font.Bold = true;
                                propertyDataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                propertyDataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                propertyDataRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                workSheet.Row(x).Height = 35;
                                propertyDataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                                x++;
                                // Store the row number for the "ОБЩИ ЧАСТИ - F3" label
                                int headerRowTop = x;
                                
                                // Common parts label (lines 1850-1858)
                                workSheet.Cell(x, 8).Value = "ОБЩИ ЧАСТИ - F3";
                                IXLRange commonLabelRange = workSheet.Range($"H{x}:K{x}");
                                commonLabelRange.Merge();
                                commonLabelRange.Style.Font.Bold = true;
                                commonLabelRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                commonLabelRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                commonLabelRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                workSheet.Row(x).Height = 35;
                                commonLabelRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                                commonLabelRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                                // Apply formatting to other cells in this row (A-G, L-O)
                                IXLRange leftHeaderRange = workSheet.Range($"A{x}:G{x}");
                                leftHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                leftHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                leftHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                
                                IXLRange rightHeaderRange = workSheet.Range($"L{x}:O{x}");
                                rightHeaderRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                rightHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                rightHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                                x++;
                                // Parameter names range (lines 1861-1869)
                                string[] parameterNamesData = new[] { "НОМЕР СО", "НАИМЕНОВАНИЕ СО", "ПЛОЩ\nF1(F2)", "ПРИЛ.\nПЛОЩ", "КОЕФ.", "C1(C2)",
                                    "ДЯЛ ОТ\nПОДЗЕМЕН\nПАРКИНГ\nГАРАЖ", "О.Ч.", "", "СПЕЦ.\nО.Ч.", "ОБЩО\nО.Ч.", "ОБЩО\nF1(F2)+F3", "ПРАВО\nНА\nСТРОЕЖ", "ЗЕМЯ", ""};
                                
                                for (int col = 0; col < parameterNamesData.Length; col++)
                                {
                                    workSheet.Cell(x, col + 1).Value = parameterNamesData[col];
                                }
                                
                                IXLRange parameterNamesRange = workSheet.Range($"A{x}:O{x}");
                                parameterNamesRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                parameterNamesRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                parameterNamesRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                parameterNamesRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                                parameterNamesRange.Style.Font.Bold = true;
                                
                                // Set row height for the header rows
                                workSheet.Row(x-1).Height = 35;
                                workSheet.Row(x).Height = 75;

                                // Merge specific ranges (lines 1870-1933)
                                // Move text from row x to row x-1 for vertically merged cells
                                workSheet.Cell(x-1, 1).Value = workSheet.Cell(x, 1).Value;  // Move "НОМЕР СО"
                                workSheet.Cell(x, 1).Value = "";  // Clear the original
                                
                                workSheet.Cell(x-1, 2).Value = workSheet.Cell(x, 2).Value;  // Move "НАИМЕНОВАНИЕ СО"
                                workSheet.Cell(x, 2).Value = "";  // Clear the original
                                
                                IXLRange numberRange = workSheet.Range($"A{x-1}:A{x}");
                                numberRange.Merge();
                                numberRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                numberRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                numberRange.Style.Font.Bold = true;
                                numberRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(numberRange);

                                IXLRange nameRange = workSheet.Range($"B{x-1}:B{x}");
                                nameRange.Merge();
                                nameRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                nameRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                nameRange.Style.Font.Bold = true;
                                nameRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(nameRange);

                                // Move text from row x to row x-1 for columns C through G
                                for (int col = 3; col <= 7; col++)
                                {
                                    workSheet.Cell(x-1, col).Value = workSheet.Cell(x, col).Value;
                                    workSheet.Cell(x, col).Value = "";
                                }

                                IXLRange areaRange = workSheet.Range($"C{x-1}:C{x}");
                                areaRange.Merge();
                                areaRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                areaRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                areaRange.Style.Font.Bold = true;
                                areaRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(areaRange);

                                IXLRange adjAreaRange = workSheet.Range($"D{x-1}:D{x}");
                                adjAreaRange.Merge();
                                adjAreaRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                adjAreaRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                adjAreaRange.Style.Font.Bold = true;
                                adjAreaRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(adjAreaRange);

                                IXLRange coefRange = workSheet.Range($"E{x-1}:E{x}");
                                coefRange.Merge();
                                coefRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                coefRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                coefRange.Style.Font.Bold = true;
                                coefRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(coefRange);

                                IXLRange cRange = workSheet.Range($"F{x-1}:F{x}");
                                cRange.Merge();
                                cRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                cRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                cRange.Style.Font.Bold = true;
                                cRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(cRange);

                                IXLRange percentaParkingPlaces = workSheet.Range($"G{x-1}:G{x}");
                                percentaParkingPlaces.Merge();
                                percentaParkingPlaces.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                percentaParkingPlaces.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                percentaParkingPlaces.Style.Font.Bold = true;
                                percentaParkingPlaces.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(percentaParkingPlaces);

                                // H and I only merge on row x (they have "ОБЩИ ЧАСТИ - F3" spanning H-K on row x-1)
                                IXLRange commonMerge = workSheet.Range($"H{x}:I{x}");
                                commonMerge.Merge();
                                commonMerge.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                commonMerge.Style.Font.Bold = true;
                                commonMerge.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(commonMerge);

                                // J and K don't merge vertically (they stay separate on row x under "ОБЩИ ЧАСТИ - F3")
                                IXLRange specialCommonRange = workSheet.Range($"J{x}:J{x}");
                                specialCommonRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                specialCommonRange.Style.Font.Bold = true;
                                specialCommonRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(specialCommonRange);

                                IXLRange totalCommonRange = workSheet.Range($"K{x}:K{x}");
                                totalCommonRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                totalCommonRange.Style.Font.Bold = true;
                                totalCommonRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(totalCommonRange);

                                // Move text from row x to row x-1 for columns L through O
                                for (int col = 12; col <= 15; col++)
                                {
                                    workSheet.Cell(x-1, col).Value = workSheet.Cell(x, col).Value;
                                    workSheet.Cell(x, col).Value = "";
                                }

                                IXLRange totalSumRange = workSheet.Range($"L{x-1}:L{x}");
                                totalSumRange.Merge();
                                totalSumRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                totalSumRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                totalSumRange.Style.Font.Bold = true;
                                totalSumRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(totalSumRange);

                                IXLRange buildingRightsRange = workSheet.Range($"M{x-1}:M{x}");
                                buildingRightsRange.Merge();
                                buildingRightsRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                buildingRightsRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                buildingRightsRange.Style.Font.Bold = true;
                                buildingRightsRange.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(buildingRightsRange);

                                IXLRange landMerge = workSheet.Range($"N{x-1}:O{x}");
                                landMerge.Merge();
                                landMerge.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                landMerge.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                landMerge.Style.Font.Bold = true;
                                landMerge.Style.Alignment.WrapText = true;
                                setAllRangeBordersXL(landMerge);

                                x++;
                                // Parameters type range (lines 1936-1943)
                                string[] parametersTypeData = new[] { "", "", "m2", "m2", "", "", "% и.ч.", "% и.ч.", "m2", "m2", "m2", "m2", "% и.ч.", "% и.ч.", "m2" };
                                
                                for (int col = 0; col < parametersTypeData.Length; col++)
                                {
                                    workSheet.Cell(x, col + 1).Value = parametersTypeData[col];
                                }
                                
                                IXLRange parametersTypeRange = workSheet.Range($"A{x}:O{x}");
                                parametersTypeRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                parametersTypeRange.Style.Font.Bold = true;
                                parametersTypeRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                parametersTypeRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                parametersTypeRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                                x++;
                                // Blank line range (lines 1945-1953)
                                IXLRange blankLineRange = workSheet.Range($"A{x}:O{x}");
                                // Apply only outside borders (no inside borders)
                                blankLineRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                                blankLineRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                // Note: By using OutsideBorder and NOT setting InsideBorder, 
                                // we get the same effect as the original merge/unmerge trick

                                x++;
                                
                                // Start tracking line for formulas (line 1957)
                                int startLine = x;

                                // Sort areas for normal properties (lines 1959-1966)
                                List<Area> sortedAreasNormal = AreasOrganizer[plotName][property]
                                    .Where(area => area.LookupParameter("A Instance Area Category").AsString().ToLower().Equals("самостоятелен обект"))
                                    .Where(area => !area.LookupParameter("A Instance Area Group").AsString().Equals("ЗЕМЯ"))
                                    .OrderBy(area => ReorderEntrance(area.LookupParameter("A Instance Area Entrance").AsString()))
                                    .ThenBy(area => ExtractLevelHeight(area.Level))
                                    .ThenBy(area => area.LookupParameter("Number").AsString())
                                    .ToList();

                                // Prepare areas for ground properties (lines 1968-1973)
                                List<Area> areasToSort = new List<Area>();
                                if (AreasOrganizer[plotName].ContainsKey("ЗЕМЯ"))
                                    areasToSort.AddRange(AreasOrganizer[plotName]["ЗЕМЯ"]);
                                if (AreasOrganizer[plotName].ContainsKey("ТРАФ"))
                                    areasToSort.AddRange(AreasOrganizer[plotName]["ТРАФ"]);

                                // Sort ground areas (lines 1974-1979)
                                List<Area> sortedAreasGround = areasToSort
                                    .Where(area => area.LookupParameter("A Instance Area Group").AsString().ToLower().Equals("земя"))
                                    .OrderBy(area => ReorderEntrance(area.LookupParameter("A Instance Area Entrance").AsString()))
                                    .ThenBy(area => ExtractLevelHeight(area.Level))
                                    .ThenBy(area => area.LookupParameter("Number").AsString())
                                    .ToList();

                                // Determine which sorted list to use (lines 1981-1990)
                                List<Area> sortedAreas = new List<Area>();
                                if (property.ToLower() != "земя")
                                {
                                    sortedAreas = sortedAreasNormal;
                                }
                                else
                                {
                                    sortedAreas = sortedAreasGround;
                                }

                                // Initialize tracking lists (lines 1992-1999)
                                List<string> levels = new List<string>();
                                List<string> entrances = new List<string>();
                                List<int> linesToExclude = new List<int>();
                                List<int> linesToExcludeLand = new List<int>();
                                string levelHeightStr = "";

                                // Process each area (line 2000)
                                foreach (Area area in sortedAreas)
                                {
                                    // Check if area is not a primary area (line 2002)
                                    if (!(area.LookupParameter("A Instance Area Primary").HasValue && area.LookupParameter("A Instance Area Primary").AsString() != ""))
                                    {
                                        // Check and add entrance section header (line 2004)
                                        if (!entrances.Contains(area.LookupParameter("A Instance Area Entrance").AsString()))
                                        {
                                            entrances.Add(area.LookupParameter("A Instance Area Entrance").AsString());

                                            if (area.LookupParameter("A Instance Area Entrance").AsString().ToLower() != "неприложимо")
                                            {
                                                workSheet.Cell(x, 1).Value = area.LookupParameter("A Instance Area Entrance").AsString();
                                                IXLRange entranceRangeString = workSheet.Range($"A{x}:O{x}");
                                                // Apply gray background with only outside borders
                                                entranceRangeString.Style.Fill.BackgroundColor = XLColor.LightGray;
                                                entranceRangeString.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                                x++;
                                                levels.Clear();
                                            }
                                        }

                                        // Check and add level section header (lines 2020-2047)
                                        if (!levels.Contains(area.LookupParameter("Level").AsValueString()))
                                        {
                                            levels.Add(area.LookupParameter("Level").AsValueString());
                                            double levelHeight = Math.Round(doc.GetElement(area.LookupParameter("Level").AsElementId()).LookupParameter("Elevation").AsDouble() * lengthConvert / 100, 3);

                                            if (levelHeight < 0)
                                            {
                                                string tempString = levelHeight.ToString("F3");
                                                levelHeightStr = $"{tempString}";
                                            }
                                            else if (levelHeight > 0)
                                            {
                                                string tempString = levelHeight.ToString("F3");
                                                levelHeightStr = $"+ {tempString}";
                                            }
                                            else
                                            {
                                                string tempString = levelHeight.ToString("F3");
                                                levelHeightStr = $"± {tempString}";
                                            }

                                            workSheet.Cell(x, 1).Value = $"КОТА {levelHeightStr}";
                                            IXLRange levelsRangeString = workSheet.Range($"A{x}:O{x}");
                                            // Apply gray background with only outside borders
                                            levelsRangeString.Style.Fill.BackgroundColor = XLColor.LightGray;
                                            levelsRangeString.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                            x++;
                                        }

                                        // Try to process area data (lines 2049-2114)
                                        try
                                        {
                                            // Extract all area data (lines 2051-2068)
                                            string areaNumber = area.LookupParameter("Number").AsString() ?? "SOMETHING'S WRONG";
                                            string areaName = area.LookupParameter("Name")?.AsString() ?? "SOMETHING'S WRONG";
                                            double areaArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Gross Area")?.AsDouble() ?? 0.0);
                                            // TODO: rework properly for adjacent area
                                            object areaSubjected = DBNull.Value;
                                            // TODO: rework properly for adjacent area
                                            double ACCO = area.LookupParameter("A Coefficient Multiplied")?.AsDouble() ?? 0.0;
                                            double C1C2 = Math.Round(area.LookupParameter("A Instance Price C1/C2")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                            double areaCommonPercent = Math.Round(area.LookupParameter("A Instance Common Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                            double areaCommonArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area")?.AsDouble() ?? 0.0);
                                            double areaCommonAreaSpecial = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Common Area Special")?.AsDouble() ?? 0.0);
                                            double areaTotalArea = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance Total Area")?.AsDouble() ?? 0.0);
                                            double areaPermitPercent = Math.Round(area.LookupParameter("A Instance Building Permit %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                            double areaRLPPercentage = Math.Round(area.LookupParameter("A Instance RLP Area %")?.AsDouble() ?? 0.0, 3, MidpointRounding.AwayFromZero);
                                            double areaRLP = smartRounder.sqFeetToSqMeters(area.LookupParameter("A Instance RLP Area")?.AsDouble() ?? 0.0);

                                            // Prepare data arrays (lines 2070-2082)
                                            string[] areaStringData = new[] { areaNumber, areaName };
                                            object[] areasDoubleData = new object[] { };

                                            if (property.ToLower() != "земя")
                                            {
                                                areasDoubleData = new object[] { areaArea, areaSubjected, ACCO, C1C2, DBNull.Value, areaCommonPercent, areaCommonArea, areaCommonAreaSpecial,
                                                    areaCommonArea + areaCommonAreaSpecial, areaTotalArea, areaPermitPercent, areaRLPPercentage, areaRLP};
                                            }
                                            else 
                                            {
                                                areasDoubleData = new object[] { areaArea, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, 
                                                    DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, areaRLPPercentage, areaRLP};
                                            }
                                            
                                            // Replace 0.0 with DBNull (lines 2084-2090)
                                            for (int i = 0; i < areasDoubleData.Length; i++)
                                            {
                                                if (areasDoubleData[i] is double && (double)areasDoubleData[i] == 0.0)
                                                {
                                                    areasDoubleData[i] = DBNull.Value;
                                                }
                                            }

                                            // Write string data to A-B (lines 2092-2093)
                                            for (int col = 0; col < areaStringData.Length; col++)
                                            {
                                                workSheet.Cell(x, col + 1).Value = areaStringData[col];
                                            }
                                            IXLRange cellRangeString = workSheet.Range($"A{x}:B{x}");
                                            cellRangeString.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                            cellRangeString.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                                            // Write numeric data to C-O (lines 2095-2096)
                                            for (int col = 0; col < areasDoubleData.Length; col++)
                                            {
                                                if (areasDoubleData[col] != DBNull.Value)
                                                {
                                                    workSheet.Cell(x, col + 3).Value = (double)areasDoubleData[col];
                                                }
                                            }
                                            IXLRange cellRangeDouble = workSheet.Range($"C{x}:O{x}");
                                            cellRangeDouble.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                            cellRangeDouble.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                                            // Apply formatting (lines 2098-2103)
                                            setBoldRangeXL(workSheet, "C", "C", x);
                                            setBoldRangeXL(workSheet, "H", "I", x);
                                            setBoldRangeXL(workSheet, "L", "O", x);
                                            setWrapRangeXL(workSheet, "B", "B", x);

                                            setExcelDecimalsFormattingXL(workSheet, x);

                                            // Additional temporary fields (lines 2106-2107)
                                            workSheet.Cell(x, 17).Value = $"КОТА {levelHeightStr}";
                                            workSheet.Cell(x, 18).Value = $"{Math.Round(100 * (areaCommonAreaSpecial + areaCommonArea) / areaTotalArea, 3, MidpointRounding.AwayFromZero)}";
                                        }
                                        catch
                                        {
                                            // Error handling (lines 2109-2114)
                                            workSheet.Cell(x, 1).Value = "X";
                                            workSheet.Cell(x, 2).Value = "Y";
                                            IXLRange cellRangeString = workSheet.Range($"A{x}:B{x}");
                                            cellRangeString.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                            cellRangeString.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                        }

                                        x++;

                                        // Identify adjacent areas, if any (lines 2118-2145)
                                        List<Area> adjascentAreasRegular = new List<Area>();

                                        foreach (Area areaSub in sortedAreas)
                                        {
                                            string primaryArea = areaSub.LookupParameter("A Instance Area Primary").AsString();

                                            if (primaryArea != null && primaryArea.Equals(area.LookupParameter("Number").AsString()))
                                            {
                                                adjascentAreasRegular.Add(areaSub);
                                            }
                                        }

                                        // Also search for adjacent areas from within ground property group (lines 2131-2145)
                                        List<Area> adjascentAreasLand = new List<Area>();

                                        if (AreasOrganizer[plotName].ContainsKey("ЗЕМЯ"))
                                        {
                                            foreach (Area areaGround in AreasOrganizer[plotName]["ЗЕМЯ"])
                                            {
                                                string primaryArea = areaGround.LookupParameter("A Instance Area Primary").AsString();

                                                if (primaryArea != null && primaryArea.Equals(area.LookupParameter("Number").AsString()))
                                                {
                                                    adjascentAreasLand.Add(areaGround);
                                                }
                                            }
                                        }

                                        // Process adjacent areas if no rooms adjacent (lines 2147-2174)
                                        if (!doesHaveRoomsAdjascent(area.LookupParameter("Number").AsString()))
                                        {
                                            if (adjascentAreasRegular.Count != 0)
                                            {
                                                adjascentAreasRegular.OrderBy(adjArea => adjArea.LookupParameter("Number").AsString()).ToList();
                                                adjascentAreasRegular.Insert(0, area);

                                                // Write adjacent areas in excel sheet
                                                foreach (Area areaSub in adjascentAreasRegular)
                                                {
                                                    exportToExcelAdjascentRegularXL(workSheet, x, areaSub, false, area.LookupParameter("Number").AsString());
                                                    linesToExclude.Add(x);
                                                    linesToExcludeLand.Add(x);
                                                    x++;
                                                }
                                            }

                                            if (adjascentAreasLand.Count != 0)
                                            {
                                                adjascentAreasLand.OrderBy(adjArea => adjArea.LookupParameter("Number").AsString()).ToList();

                                                foreach (Area areaSub in adjascentAreasLand)
                                                {
                                                    exportToExcelAdjascentRegularXL(workSheet, x, areaSub, true, area.LookupParameter("Number").AsString());
                                                    x++;
                                                }
                                            }

                                            // Note: Lines 2175-2214 are commented out in original code (special common areas loop)
                                            // This commented section is not implemented as it's disabled in the original
                                        }
                                        else
                                        {
                                            // Handle adjacent rooms when doesHaveRoomsAdjascent returns true (lines 2217-2254)
                                            Dictionary<List<object>, Room> adjascentRooms = returnAdjascentRooms(area);

                                            foreach (List<object> key in adjascentRooms.Keys)
                                            {
                                                Room room = adjascentRooms[key];

                                                // Write room data to A-B columns (lines 2225-2227)
                                                workSheet.Cell(x, 1).Value = key[0].ToString();
                                                workSheet.Cell(x, 2).Value = room.LookupParameter("Name").AsString();
                                                
                                                IXLRange areaAdjRangeStr = workSheet.Range($"A{x}:B{x}");
                                                areaAdjRangeStr.Style.Font.Italic = true;
                                                areaAdjRangeStr.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                                                areaAdjRangeStr.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                                areaAdjRangeStr.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                                                // Write room numeric data to C-O columns (lines 2233-2237)
                                                object[] roomData = new object[] {
                                                    smartRounder.sqFeetToSqMeters(room.LookupParameter("Area").AsDouble()),
                                                    DBNull.Value, DBNull.Value, DBNull.Value,
                                                    key[1], key[2], key[3], key[4], key[5], key[6], key[7], key[8], key[9], DBNull.Value, DBNull.Value};

                                                for (int col = 0; col < roomData.Length; col++)
                                                {
                                                    if (roomData[col] != DBNull.Value)
                                                    {
                                                        // Handle type conversion for ClosedXML
                                                        if (roomData[col] is double)
                                                        {
                                                            workSheet.Cell(x, col + 3).Value = (double)roomData[col];
                                                        }
                                                        else
                                                        {
                                                            workSheet.Cell(x, col + 3).Value = roomData[col].ToString();
                                                        }
                                                    }
                                                }

                                                IXLRange areaAdjRangeDouble = workSheet.Range($"C{x}:O{x}");
                                                areaAdjRangeDouble.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                                areaAdjRangeDouble.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                                                // Apply decimal formatting (line 2245)
                                                setExcelDecimalsFormattingXL(workSheet, x);

                                                // Add to exclusion lists (lines 2247-2248)
                                                linesToExclude.Add(x);
                                                linesToExcludeLand.Add(x);

                                                x++;
                                            }

                                            // Check if adjacent rooms and areas are equal (line 2253)
                                            errorMessage += areAdjascentRoomsAndAreasEqual(adjascentRooms, adjascentAreasRegular, adjascentAreasLand, area);
                                        }
                                    }
                                }

                                // Property summary calculations (lines 2258-2315)
                                int endLine = x - 1;
                                propertyEndLinesBuildingRigts.Add($"M{x}");
                                propertyEndLinesLandSum.Add($"N{x}");
                                propertyEndLineslandSumArea.Add($"O{x}");

                                // Optional color range (commented out in original)
                                // IXLRange colorRange = workSheet.Range($"C{startLine}:O{endLine}");
                                // colorRange.Style.Fill.BackgroundColor = XLColor.AliceBlue;

                                // Set formulas for property totals (lines 2266-2306)
                                setSumFormulaExcludingRowsXL(workSheet, "C", x, startLine, endLine, linesToExclude);
                                setBoldRangeXL(workSheet, "C", "C", x);

                                // Total sum of adjacent areas
                                workSheet.Cell(x, 4).FormulaA1 = $"=SUM(D{startLine}:D{endLine})";

                                // Total sum of C1/C2  
                                workSheet.Cell(x, 6).FormulaA1 = $"=SUM(F{startLine}:F{endLine})";

                                // Common areas formulas
                                setSumFormulaExcludingRowsXL(workSheet, "H", x, startLine, endLine, linesToExclude);
                                setBoldRangeXL(workSheet, "H", "H", x);

                                setSumFormulaExcludingRowsXL(workSheet, "I", x, startLine, endLine, linesToExclude);
                                setBoldRangeXL(workSheet, "I", "I", x);

                                setSumFormulaExcludingRowsXL(workSheet, "J", x, startLine, endLine, linesToExclude);

                                setSumFormulaExcludingRowsXL(workSheet, "K", x, startLine, endLine, linesToExclude);

                                setSumFormulaExcludingRowsXL(workSheet, "L", x, startLine, endLine, linesToExclude);
                                setBoldRangeXL(workSheet, "L", "L", x);

                                setSumFormulaExcludingRowsXL(workSheet, "M", x, startLine, endLine, linesToExclude);
                                setBoldRangeXL(workSheet, "M", "M", x);

                                setSumFormulaExcludingRowsXL(workSheet, "N", x, startLine, endLine, linesToExcludeLand);
                                setBoldRangeXL(workSheet, "N", "N", x);

                                setSumFormulaExcludingRowsXL(workSheet, "O", x, startLine, endLine, linesToExcludeLand);
                                setBoldRangeXL(workSheet, "O", "O", x);

                                // Set coloring for the summed up rows (lines 2308-2310)
                                IXLRange colorRangePropertySum = workSheet.Range($"A{endLine + 1}:O{endLine + 1}");
                                colorRangePropertySum.Style.Fill.BackgroundColor = XLColor.LightGray;

                                // Set final decimal formatting (line 2313)
                                setExcelDecimalsFormattingXL(workSheet, x);

                                x++;
                            }
                        }

                        // Final plot totals (lines 2319-2329)
                        string propertyBuildingRightsFormula = $"=SUM({string.Join(",", propertyEndLinesBuildingRigts)})";
                        workSheet.Cell(x, 13).FormulaA1 = propertyBuildingRightsFormula;
                        workSheet.Cell(x, 13).Style.Fill.BackgroundColor = XLColor.AntiqueWhite;

                        string propertyLandSumFormula = $"=SUM({string.Join(",", propertyEndLinesLandSum)})";
                        workSheet.Cell(x, 14).FormulaA1 = propertyLandSumFormula;
                        workSheet.Cell(x, 14).Style.Fill.BackgroundColor = XLColor.AntiqueWhite;

                        string propertyLandSumAreaFormula = $"=SUM({string.Join(",", propertyEndLineslandSumArea)})";
                        workSheet.Cell(x, 15).FormulaA1 = propertyLandSumAreaFormula;
                        workSheet.Cell(x, 15).Style.Fill.BackgroundColor = XLColor.AntiqueWhite;
                    }

                    workBook.Save();
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Грешка при експорт в Excel: {ex.Message}";
            }

            return errorMessage;
        }        
    }
}
