using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Autodesk.Revit.Creation;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Document = Autodesk.Revit.DB.Document;

namespace AreaCalculations
{
    internal class AreaCollection
    {
        public List<double> build { get; set; }
        public List<double> totalBuild { get; set; }
        public List<Area> areasCollector { get; set; }
        public Document doc { get; set; }
        Transaction transaction { get; set; }
        private double areaConvert = 10.7639104167096;
        private SmartRound smartRound { get; set; }
        private AritmeticAssistant aritAsist { get; set; }
        private AreaCalculationsSettings settings { get; set; }
        private ElementId areaSchemeId { get; set; }
        private bool updateIfNoValue(Parameter param, double value)
        {
            if (param.HasValue && param.AsValueString() != "" && param.AsDouble() != 0)
                return false;
            else
            {
                param.Set(value);
                return true;
            }
        }     
        public AreaCollection(Document document)
        {
            this.doc = document;
            this.smartRound = new SmartRound(document);
            this.aritAsist = new AritmeticAssistant();

            // Load settings for filtering
            this.settings = SettingsManager.LoadSettings();
            this.areaSchemeId = !string.IsNullOrEmpty(settings.AreaSchemeId) ? new ElementId(long.Parse(settings.AreaSchemeId)) : null;

            ParameterValueProvider provider = new ParameterValueProvider(new ElementId(BuiltInParameter.ROOM_AREA));
            FilterNumericRuleEvaluator evaluator = new FilterNumericGreater();
            double epsilon = 0.0001;
            ElementParameterFilter filter = new ElementParameterFilter(new FilterDoubleRule(provider, evaluator, epsilon, 1E-6));

            this.areasCollector = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Areas).WhereElementIsNotElementType().WherePasses(filter)
                .Cast<Area>().Where(a => areaSchemeId == null || a.AreaScheme.Id == areaSchemeId).ToList();

            this.transaction = new Transaction(doc, "Update Areas");
        }
        public AreaCollection(Document document, List<string> plotNames)
        {
            this.doc = document;
            this.smartRound = new SmartRound(document);
            this.aritAsist = new AritmeticAssistant();

            // Load settings for filtering
            this.settings = SettingsManager.LoadSettings();
            this.areaSchemeId = !string.IsNullOrEmpty(settings.AreaSchemeId) ? new ElementId(long.Parse(settings.AreaSchemeId)) : null;

            ParameterValueProvider provider = new ParameterValueProvider(new ElementId(BuiltInParameter.ROOM_AREA));
            FilterNumericRuleEvaluator evaluator = new FilterNumericGreater();
            double epsilon = 0.0001;
            ElementParameterFilter filter = new ElementParameterFilter(new FilterDoubleRule(provider, evaluator, epsilon, 1E-6));

            this.areasCollector = new FilteredElementCollector(document).OfCategory(BuiltInCategory.OST_Areas).WhereElementIsNotElementType().WherePasses(filter)
                .Cast<Area>().Where(a => areaSchemeId == null || a.AreaScheme.Id == areaSchemeId).ToList();

            this.transaction = new Transaction(document, "Update Areas");

            this.build = new List<double>();
            this.totalBuild = new List<double>();

            this.build.Add(0);
            this.build.Add(0);

            this.totalBuild.Add(0);
            this.totalBuild.Add(0);

            foreach (Area area in areasCollector)
            {
                if (area.LookupParameter("Area").AsString() != "Not Placed")
                {
                    if (plotNames.Count == 1)
                    {
                        if (area.LookupParameter("A Instance Area Location").AsString() == "НАЗЕМНА" ||
                            area.LookupParameter("A Instance Area Location").AsString() == "ПОЛУПОДЗЕМНА")
                            this.build[0] = aritAsist.addToArea(this.build[0], smartRound.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                        if (area.LookupParameter("A Instance Area Location").AsString() == "НАДЗЕМНА" ||
                            area.LookupParameter("A Instance Area Location").AsString() == "НАЗЕМНА")
                            this.totalBuild[0] = aritAsist.addToArea(this.totalBuild[0], smartRound.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                    }
                    else if (plotNames.Count == 2)
                    {
                        if (area.LookupParameter("A Instance Area Location").AsString() == "НАЗЕМНА" || 
                            area.LookupParameter("A Instance Area Location").AsString() == "ПОЛУПОДЗЕМНА")
                        {
                            if (area.LookupParameter("A Instance Area Plot").AsString() == plotNames[0])
                                this.build[0] = aritAsist.addToArea(this.build[0], smartRound.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                            else if (area.LookupParameter("A Instance Area Plot").AsString() == plotNames[1])
                                this.build[1] = aritAsist.addToArea(this.build[1], smartRound.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                        }
                        else if (area.LookupParameter("A Instance Area Location").AsString() == "НАДЗЕМНА")
                        {
                            if (area.LookupParameter("A Instance Area Plot").AsString() == plotNames[0])
                                this.totalBuild[0] = aritAsist.addToArea(this.totalBuild[0], smartRound.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                            else if (area.LookupParameter("A Instance Area Plot").AsString() == plotNames[1])
                                this.totalBuild[1] = aritAsist.addToArea(this.totalBuild[1], smartRound.sqFeetToSqMeters(area.LookupParameter("Area").AsDouble()));
                        }
                    }
                    else
                    {
                        this.build[0] = plotNames.Count;
                        this.totalBuild[0] = 1;
                    }
                }
            }
        }
        public string CheckAreasParameters(List<string> plotNames, ProjectInfo projInfo)
        {
            string errorMessage = "";

            List<string> AreaCategoryValues = new List<string> { "ИЗКЛЮЧЕНА ОТ ОЧ", "НЕПРИЛОЖИМО", "ОБЩА ЧАСТ", "САМОСТОЯТЕЛЕН ОБЕКТ" };
            List<string> AreaLocationValues = new List<string> { "НАДЗЕМНА", "НАЗЕМНА", "НЕПРИЛОЖИМО", "ПОДЗЕМНА", "ПОЛУПОДЗЕМНА" };

            List<string> plotTypesValues = new List<string>();

            if (new List<string> { "СТАНДАРТНО УПИ", "ЪГЛОВО УПИ", "УПИ В ДВЕ ЗОНИ" }.Contains(projInfo.LookupParameter("Plot Type").AsString()))
            {
                plotTypesValues.Add(projInfo.LookupParameter("Plot Number").AsString());
            }
            else if (projInfo.LookupParameter("Plot Type").AsString() == "ДВЕ УПИ")
            {
                plotTypesValues.Add(projInfo.LookupParameter("Plot Number 1st").AsString());
                plotTypesValues.Add(projInfo.LookupParameter("Plot Number 2nd").AsString());
            }

            foreach (Area area in areasCollector)
            {
                if (area.Area != 0)
                {
                    if (area.LookupParameter("A Instance Area Group").AsString() == "" || !area.LookupParameter("A Instance Area Group").HasValue)
                    { 
                        errorMessage += $"Грешка: " +
                            $"Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} / Непопълнен параметър: A Instance Area Group \n"; 
                    }

                    else if ((!new List<string> { "ТРАФ", "ЗЕМЯ" }.Contains(area.LookupParameter("A Instance Area Group").AsString())) && 
                        (area.LookupParameter("A Instance Area Category").AsString() == "НЕПРИЛОЖИМО"))
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} " +
                            $"/ Параметър: A Instance Area Category. Индивидуален обект със зададена стойност за 'A Instance Area Group', " +
                            $"различна от 'ТРАФ' и 'ЗЕМЯ', не може да приеме стойност 'НЕПРИЛОЖИМО' за 'A Instance Area Category'\n"; 
                    }

                    if (area.LookupParameter("A Instance Area Category").AsString() == "" || !area.LookupParameter("A Instance Area Category").HasValue)
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                            $"/ id: {area.Id.ToString()} / Непопълнен параметър: A Instance Area Category\n"; 
                    }

                    else if (!AreaCategoryValues.Contains(area.LookupParameter("A Instance Area Category").AsString()))
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} " +
                            $"/ Параметър: A Instance Area Category. Допустими стойности: ИЗКЛЮЧЕНА ОТ ОЧ, НЕПРИЛОЖИМО, ОБЩА ЧАСТ, САМОСТОЯТЕЛЕН ОБЕКТ\n"; 
                    }

                    if (area.LookupParameter("A Instance Area Location").AsString() == "" || !area.LookupParameter("A Instance Area Location").HasValue)
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                            $"/ id: {area.Id.ToString()} / Непопълнен параметър: A Instance Area Location\n";
                    }

                    else if (!AreaLocationValues.Contains(area.LookupParameter("A Instance Area Location").AsString()))
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} " +
                            $"/ Параметър: A Instance Area Location. Допустими стойности: НАДЗЕМНА, НАЗЕМНА, НЕПРИЛОЖИМО, ПОДЗЕМНА, ПОЛУПОДЗЕМНА\n"; 
                    }

                    if (area.LookupParameter("A Instance Area Category").AsString() == "НЕПРИЛОЖИМО" && 
                        area.LookupParameter("A Instance Area Location").AsString() != "НЕПРИЛОЖИМО" && 
                        area.LookupParameter("A Instance Area Location").AsString() != "")
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} " +
                            $"/ Параметър: A Instance Area Location. Когато за параметър 'A Instance Area Category' е попълнена стойност 'НЕПРИЛОЖИМО', " +
                            $"то за 'A Instance Area Location' трябва да бъде зададена същата стойност\n"; 
                    }

                    if (area.LookupParameter("A Instance Area Entrance").AsString() == "" || !area.LookupParameter("A Instance Area Entrance").HasValue) 
                    { 
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                            $"/ id: {area.Id.ToString()} / Непопълнен параметър: A Instance Area Entrance\n"; 
                    }

                    if (plotNames.Count == 2)
                    {
                        if (!plotNames.Contains(area.LookupParameter("A Instance Area Plot").AsString()))
                        { 
                            errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} " +
                                $"/ Параметър: A Instance Area Plot. Допустими стойности: {plotNames[0]} и {plotNames[1]}\n"; 
                        }
                    }
                    
                    if (!plotTypesValues.Contains(area.LookupParameter("A Instance Area Plot").AsString()))
                    {
                        string allPlotsStr = "";

                        foreach (string plot in plotTypesValues)
                            allPlotsStr += $"| {plot} | ";

                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} / id: {area.Id.ToString()} " +
                            $"/ Параметър: A Instance Area Plot. Допустими стойности: {allPlotsStr}\n";
                    }

                    if (area.LookupParameter("Number").AsString() == "" || !area.LookupParameter("Number").HasValue)
                    {
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                            $"/ id: {area.Id.ToString()} / Непопълнен параметър: Number\n";
                    }

                    // Validate that common areas don't have A Instance Area Primary set
                    if (area.LookupParameter("A Instance Area Category").AsString() == "ОБЩА ЧАСТ"
                        && area.LookupParameter("A Instance Area Primary").HasValue
                        && area.LookupParameter("A Instance Area Primary").AsString() != "")
                    {
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                            $"/ id: {area.Id.ToString()} / Обща част не може да има стойност за 'A Instance Area Primary'. " +
                            $"Използвайте 'A Instance Area Common Group' за специални общи части.\n";
                    }

                    if (area.LookupParameter("A Instance Area Primary").HasValue
                        && area.LookupParameter("A Instance Area Primary").AsString() != ""
                        && area.LookupParameter("A Instance Area Primary").AsString() == area.LookupParameter("Number").AsString())
                    {
                        errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                            $"/ id: {area.Id.ToString()} / Една Area не може да бъде подчинена сама на себе си\n";
                    }

                    // THE WHOLE THING UNDERNEATH IS DISGUISTING... TO BE PROPERLY REVISED
                    if (area.LookupParameter("A Instance Area Primary").HasValue
                        && area.LookupParameter("A Instance Area Primary").AsString() != ""
                        && area.LookupParameter("A Instance Area Primary").AsString() != area.LookupParameter("Number").AsString())
                    {
                        if (!area.LookupParameter("A Instance Area Primary").AsString().Contains("+"))
                        {
                            bool wasFound = false;

                            ParameterValueProvider provider = new ParameterValueProvider(new ElementId(BuiltInParameter.ROOM_AREA));
                            FilterNumericRuleEvaluator evaluator = new FilterNumericGreater();
                            double epsilon = 0.0001;
                            ElementParameterFilter filter = new ElementParameterFilter(new FilterDoubleRule(provider, evaluator, epsilon, 1E-6));

                            List<Area> mainAreasCollector = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_Areas).WhereElementIsNotElementType().WherePasses(filter)
                                .Cast<Area>().Where(a => areaSchemeId == null || a.AreaScheme.Id == areaSchemeId).ToList();

                            foreach (Area mainArea in mainAreasCollector)
                            {
                                if (area.LookupParameter("A Instance Area Primary").AsString() == mainArea.LookupParameter("Number").AsString())
                                    wasFound = true;
                            }

                            if (!wasFound)
                                errorMessage += $"Грешка: Area {area.LookupParameter("Number").AsString()} " +
                                $"/ id: {area.Id.ToString()} / Дадената Area е подчинена на несъществуваща такава\n";
                        }
                    }
                }
            }

            return errorMessage;
        }
        public int updateAreaCoefficients(Dictionary<string, double> areaCoefficientsDict)
        {
            int i = 0;

            double ACM = areaCoefficientsDict["ACM"];
            double ACMGP = areaCoefficientsDict["ACMGP"];
            double ACMSM = areaCoefficientsDict["ACMSM"];

            transaction.Start();

            foreach (Area area in areasCollector)
            {
                if (area.LookupParameter("Area").AsString() != "Not Placed")
                {
                    Parameter coeffParam = area.LookupParameter("A Coefficient Multiplied");

                    // Only update if no value exists
                    if (!coeffParam.HasValue || coeffParam.AsDouble() == 0)
                    {
                        string areaName = area.LookupParameter("Name").AsString().ToLower();

                        if (new List<string> { "склад", "мазе" }.Any(keyword => areaName.Contains(keyword)))
                            coeffParam.Set(ACMSM);
                        else if (new List<string> { "гараж", "паркинг" }.Any(keyword => areaName.Contains(keyword)))
                            coeffParam.Set(ACMGP);
                        else
                            coeffParam.Set(ACM);

                        i++;
                    }
                }
            }

            transaction.Commit();
            return i;
        }

        public int updateAreaCoefficientsOverride(Dictionary<string, double> areaCoefficientsDict)
        {
            int i = 0;

            double ACM = areaCoefficientsDict["ACM"];
            double ACMGP = areaCoefficientsDict["ACMGP"];
            double ACMSM = areaCoefficientsDict["ACMSM"];

            transaction.Start();

            foreach (Area area in areasCollector)
            {
                if (area.LookupParameter("Area").AsString() != "Not Placed")
                {
                    Parameter coeffParam = area.LookupParameter("A Coefficient Multiplied");
                    string areaName = area.LookupParameter("Name").AsString().ToLower();

                    if (new List<string> { "склад", "мазе" }.Any(keyword => areaName.Contains(keyword)))
                        coeffParam.Set(ACMSM);
                    else if (new List<string> { "гараж", "паркинг" }.Any(keyword => areaName.Contains(keyword)))
                        coeffParam.Set(ACMGP);
                    else
                        coeffParam.Set(ACM);

                    i++;
                }
            }

            transaction.Commit();
            return i;
        }
    }
}
