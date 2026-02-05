using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;

namespace AreaCalculations
{
    [TransactionAttribute(TransactionMode.Manual)]
    public class MigrateCommonGroups : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Check if settings are configured
                if (!SettingsManager.SettingsExist())
                {
                    TaskDialog.Show("Настройки", "Моля, първо конфигурирайте настройките (Area Scheme и Phase) чрез бутона 'Settings'.");
                    return Result.Failed;
                }

                // Load settings for filtering
                AreaCalculationsSettings settings = SettingsManager.LoadSettings();
                ElementId areaSchemeId = !string.IsNullOrEmpty(settings.AreaSchemeId) ? new ElementId(long.Parse(settings.AreaSchemeId)) : null;
                ElementId phaseId = !string.IsNullOrEmpty(settings.PhaseId) ? new ElementId(long.Parse(settings.PhaseId)) : null;

                // Get all areas filtered by Area Scheme
                List<Area> allAreas = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Areas)
                    .WhereElementIsNotElementType()
                    .Cast<Area>()
                    .Where(a => a.Area > 0)
                    .Where(a => areaSchemeId == null || a.AreaScheme.Id == areaSchemeId)
                    .ToList();

                // Build lookup: AreaNumber -> Area (for САМОСТОЯТЕЛЕН ОБЕКТ)
                Dictionary<string, Area> areaNumberLookup = new Dictionary<string, Area>();
                foreach (Area area in allAreas)
                {
                    string category = area.LookupParameter("A Instance Area Category")?.AsString() ?? "";
                    if (category == "САМОСТОЯТЕЛЕН ОБЕКТ")
                    {
                        string number = area.LookupParameter("Number")?.AsString() ?? "";
                        if (!string.IsNullOrEmpty(number) && !areaNumberLookup.ContainsKey(number))
                        {
                            areaNumberLookup[number] = area;
                        }
                    }
                }

                // Get all ОБЩА ЧАСТ areas with "A Instance Area Primary" set
                List<Area> commonAreasWithPrimary = allAreas
                    .Where(a => (a.LookupParameter("A Instance Area Category")?.AsString() ?? "") == "ОБЩА ЧАСТ")
                    .Where(a => !string.IsNullOrEmpty(a.LookupParameter("A Instance Area Primary")?.AsString()))
                    .ToList();

                if (commonAreasWithPrimary.Count == 0)
                {
                    TaskDialog.Show("Информация", "Не са открити ОБЩА ЧАСТ области с попълнен параметър 'A Instance Area Primary'.");
                    return Result.Succeeded;
                }

                // Parse primaries and validate references
                string warnings = "";
                Dictionary<Area, HashSet<string>> commonAreaToPrimaries = new Dictionary<Area, HashSet<string>>();

                foreach (Area commonArea in commonAreasWithPrimary)
                {
                    string primaryString = commonArea.LookupParameter("A Instance Area Primary").AsString();
                    string[] primaryNumbers = primaryString.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToArray();

                    HashSet<string> validPrimaries = new HashSet<string>();

                    foreach (string number in primaryNumbers)
                    {
                        if (!areaNumberLookup.ContainsKey(number))
                        {
                            warnings += $"ОБЩА ЧАСТ '{commonArea.LookupParameter("Number")?.AsString()}' (Id: {commonArea.Id}) " +
                                $"съдържа референция към несъществуваща област: '{number}'\n";
                        }
                        else
                        {
                            validPrimaries.Add(number);
                        }
                    }

                    if (validPrimaries.Count > 0)
                    {
                        commonAreaToPrimaries[commonArea] = validPrimaries;
                    }
                }

                // Detect groupings and conflicts
                // Group common areas that share identical or overlapping primary sets
                List<HashSet<Area>> groups = new List<HashSet<Area>>();
                HashSet<Area> processedAreas = new HashSet<Area>();

                foreach (Area commonArea in commonAreaToPrimaries.Keys)
                {
                    if (processedAreas.Contains(commonArea))
                        continue;

                    HashSet<Area> currentGroup = new HashSet<Area> { commonArea };
                    HashSet<string> currentPrimaries = new HashSet<string>(commonAreaToPrimaries[commonArea]);

                    // Find all common areas that share any primary with the current group
                    bool foundNew = true;
                    while (foundNew)
                    {
                        foundNew = false;
                        foreach (Area otherArea in commonAreaToPrimaries.Keys)
                        {
                            if (currentGroup.Contains(otherArea))
                                continue;

                            HashSet<string> otherPrimaries = commonAreaToPrimaries[otherArea];

                            // Check if there's any overlap
                            if (currentPrimaries.Overlaps(otherPrimaries))
                            {
                                currentGroup.Add(otherArea);
                                currentPrimaries.UnionWith(otherPrimaries);
                                foundNew = true;
                            }
                        }
                    }

                    processedAreas.UnionWith(currentGroup);
                    groups.Add(currentGroup);
                }

                // Validate groups - all areas in a group must have identical primaries
                List<HashSet<Area>> validGroups = new List<HashSet<Area>>();
                Dictionary<HashSet<Area>, HashSet<string>> groupToPrimaries = new Dictionary<HashSet<Area>, HashSet<string>>();

                foreach (HashSet<Area> group in groups)
                {
                    // Get the union of all primaries in this group
                    HashSet<string> expectedPrimaries = new HashSet<string>();
                    foreach (Area area in group)
                    {
                        expectedPrimaries.UnionWith(commonAreaToPrimaries[area]);
                    }

                    // Check if all areas in the group have the same primaries
                    bool isValid = true;
                    foreach (Area area in group)
                    {
                        HashSet<string> areaPrimaries = commonAreaToPrimaries[area];
                        if (!areaPrimaries.SetEquals(expectedPrimaries))
                        {
                            isValid = false;
                            string areaNumber = area.LookupParameter("Number")?.AsString() ?? area.Id.ToString();
                            string hasPrimaries = string.Join(", ", areaPrimaries);
                            string expectedStr = string.Join(", ", expectedPrimaries);
                            warnings += $"КОНФЛИКТ: ОБЩА ЧАСТ '{areaNumber}' (Id: {area.Id}) има примари [{hasPrimaries}], " +
                                $"но групата изисква [{expectedStr}]. Групата няма да бъде присвоена.\n";
                        }
                    }

                    if (isValid)
                    {
                        validGroups.Add(group);
                        groupToPrimaries[group] = expectedPrimaries;
                    }
                }

                // Show warnings if any
                if (!string.IsNullOrEmpty(warnings))
                {
                    string warningsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "warnings.txt");
                    File.WriteAllText(warningsPath, warnings);

                    TaskDialogResult continueResult = TaskDialog.Show("Предупреждения",
                        $"Открити са следните проблеми:\n\n{warnings}\n\nЖелаете ли да продължите с валидните групи?",
                        TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                    if (continueResult == TaskDialogResult.No)
                    {
                        return Result.Cancelled;
                    }
                }

                if (validGroups.Count == 0)
                {
                    TaskDialog.Show("Информация", "Няма валидни групи за присвояване.");
                    return Result.Succeeded;
                }

                // Build preview message
                string preview = "Ще бъдат създадени следните групи:\n\n";
                char groupLabel = 'A';
                Dictionary<HashSet<Area>, char> groupLabels = new Dictionary<HashSet<Area>, char>();

                foreach (HashSet<Area> group in validGroups)
                {
                    groupLabels[group] = groupLabel;
                    HashSet<string> primaries = groupToPrimaries[group];

                    string commonAreaNumbers = string.Join(", ", group.Select(a => a.LookupParameter("Number")?.AsString() ?? a.Id.ToString()));
                    string primaryNumbers = string.Join(", ", primaries);

                    preview += $"Група {groupLabel}:\n";
                    preview += $"  ОБЩИ ЧАСТИ: {commonAreaNumbers}\n";
                    preview += $"  САМОСТОЯТЕЛНИ ОБЕКТИ: {primaryNumbers}\n\n";

                    groupLabel++;
                }

                // Show confirmation dialog
                TaskDialogResult confirmResult = TaskDialog.Show("Потвърждение",
                    preview + "Желаете ли да продължите?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (confirmResult == TaskDialogResult.No)
                {
                    return Result.Cancelled;
                }

                // Apply changes in transaction
                using (Transaction trans = new Transaction(doc, "Migrate Common Groups"))
                {
                    trans.Start();

                    int updatedCommonAreas = 0;
                    int updatedIndividualAreas = 0;

                    foreach (HashSet<Area> group in validGroups)
                    {
                        string label = groupLabels[group].ToString();
                        HashSet<string> primaries = groupToPrimaries[group];

                        // Set group on ОБЩА ЧАСТ areas
                        foreach (Area commonArea in group)
                        {
                            Parameter param = commonArea.LookupParameter("A Instance Area Common Group");
                            if (param != null && !param.IsReadOnly)
                            {
                                param.Set(label);
                                updatedCommonAreas++;
                            }
                        }

                        // Set group on САМОСТОЯТЕЛЕН ОБЕКТ areas
                        foreach (string primaryNumber in primaries)
                        {
                            if (areaNumberLookup.ContainsKey(primaryNumber))
                            {
                                Area individualArea = areaNumberLookup[primaryNumber];
                                Parameter param = individualArea.LookupParameter("A Instance Area Common Group");
                                if (param != null && !param.IsReadOnly)
                                {
                                    param.Set(label);
                                    updatedIndividualAreas++;
                                }
                            }
                        }
                    }

                    trans.Commit();

                    TaskDialog.Show("Успех",
                        $"Миграцията приключи успешно!\n\n" +
                        $"Създадени групи: {validGroups.Count}\n" +
                        $"Обновени ОБЩИ ЧАСТИ: {updatedCommonAreas}\n" +
                        $"Обновени САМОСТОЯТЕЛНИ ОБЕКТИ: {updatedIndividualAreas}");
                }

                return Result.Succeeded;
            }
            catch (Exception e)
            {
                TaskDialog.Show("Грешка", $"{e.Message}\n\n{e.ToString()}");
                return Result.Failed;
            }
        }
    }
}
