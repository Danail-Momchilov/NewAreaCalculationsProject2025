# Area Calculations Plugin - Project Analysis

## Project Overview

This is an Autodesk Revit plugin designed for calculating building areas, plot parameters, and exporting data to Excel. The plugin is specifically tailored for Bulgarian building regulations and practices, as evidenced by the Bulgarian language in UI messages and tooltips.

## Project Structure and Organization

### Directory Structure
```
NewAreaCalculationsProject/
├── AboutStack/              # About dialog and information windows
│   ├── AboutInfo.cs
│   ├── ResourceInfo.cs
│   └── VersionInfo.cs
├── AreaCalcs/              # Area calculation functionality
│   ├── AreaCoefficients.cs
│   ├── AreaCoefficientsWindow.xaml
│   ├── AreaCoefficientsWindow.xaml.cs
│   ├── AreaDictionary.cs
│   └── CalculateAreaParameters.cs
├── Excel/                  # Excel export functionality
│   ├── ExportToExcel.cs
│   ├── SheetNameWindow.xaml
│   └── SheetNameWindow.xaml.cs
├── SiteCalcs/             # Site/Plot calculations
│   ├── AreaCollection.cs
│   ├── Greenery.cs
│   ├── OutputReport.cs
│   ├── ProjInfoUpdater.cs
│   └── SiteCalcs.cs
├── SmartRound/            # Rounding utilities
│   └── SmartRound.cs
├── Properties/            # Assembly information
├── Releases/              # Release executables
├── img/                   # UI icons and images
├── bin/                   # Build output
├── obj/                   # Intermediate build files
└── App.cs                 # Main application entry point
```

### Class Organization

1. **Entry Point**
   - `App.cs` - Implements `IExternalApplication` interface, creates ribbon UI

2. **Command Classes** (implement `IExternalCommand`)
   - `SiteCalcs` - Plot parameters calculations
   - `AreaCoefficients` - Area coefficient assignments
   - `CalculateAreaParameters` - Area parameter calculations
   - `ExportToExcel` - Excel export functionality
   - `AboutInfo`, `VersionInfo`, `ResourceInfo` - Information dialogs

3. **Support Classes**
   - `AreaCollection` - Manages collections of Area elements
   - `AreaDictionary` - Dictionary structure for area data
   - `ProjInfoUpdater` - Updates project information parameters
   - `Greenery` - Greenery calculations
   - `OutputReport` - Report generation
   - `SmartRound` - Smart rounding utilities

4. **UI Classes**
   - `AreaCoefficientsWindow` - WPF window for coefficient input
   - `SheetNameWindow` - WPF window for Excel sheet naming

## Naming Conventions and Coding Style

### Naming Conventions
- **Namespaces**: Single namespace `AreaCalculations` for all classes
- **Classes**: PascalCase (e.g., `AreaCollection`, `ProjInfoUpdater`)
- **Methods**: camelCase (e.g., `updateAreaCoefficients`, `checkProjectInfoParameters`)
- **Properties**: PascalCase (e.g., `PlotType`, `AreaScheme`)
- **Private fields**: camelCase (e.g., `areaConvert`, `smartRound`)
- **Parameters**: camelCase (e.g., `commandData`, `plotNames`)
- **Constants**: Not consistently defined, but conversion factors are hardcoded

### Coding Style Characteristics
- **Transaction Attribute**: All command classes use `[TransactionAttribute(TransactionMode.Manual)]`
- **Error Handling**: Try-catch blocks with TaskDialog for user feedback
- **UI Messages**: Bulgarian language for all user-facing messages
- **Comments**: Minimal comments, mostly TODO markers
- **Code Organization**: Logical grouping by functionality
- **LINQ Usage**: Modern C# features including LINQ queries
- **Magic Numbers**: Some hardcoded values (e.g., `10.7639104167096` for area conversion)

### Code Patterns
- Consistent use of Revit API patterns
- Manual transaction management
- FilteredElementCollector for element queries
- Parameter manipulation through Revit API
- WPF for custom dialogs

## External References and Dependencies

### Core Dependencies
1. **.NET 8** - Target framework (UPGRADED from .NET Framework 4.8)
   - **CRITICAL**: DO NOT change .NET framework version - Revit 2026 runs on .NET 8
   - **CRITICAL**: DO NOT modify general project settings without explicit approval
2. **Autodesk Revit API 2026** - (UPGRADED from Revit API 2023)
   - RevitAPI.dll
   - RevitAPIUI.dll
3. **Microsoft Office Interop Excel 15.0.4795.1001**
   - Used for Excel export functionality
   - Includes COM references for Excel automation
4. **WPF Framework**
   - PresentationCore
   - PresentationFramework
   - System.Xaml
   - WindowsBase
5. **Standard .NET Libraries**
   - System.Core
   - System.Drawing
   - System.Windows.Forms
   - System.Xml.Linq
   - System.Data
   - Microsoft.CSharp

### COM References
- Microsoft.Office.Core
- Microsoft.Office.Interop.Excel
- VBIDE

## Further Development Recommendations

### 1. Replace Excel Interop Library
The current implementation uses Microsoft Office Interop, which has several limitations:
- Requires Microsoft Excel to be installed on the client machine
- Can cause memory leaks if not properly disposed
- Performance issues with large datasets
- COM interop complexity

**Recommended alternatives:**
- **EPPlus** - Modern, fast, and doesn't require Excel installation
- **ClosedXML** - User-friendly API built on top of OpenXML
- **NPOI** - Cross-platform solution that works with both .xls and .xlsx

### Implementation Strategy for Excel Library Migration:
1. Add NuGet package for chosen library (e.g., EPPlus)
2. Create abstraction layer for Excel operations
3. Gradually replace Interop calls with new library methods
4. Remove COM references after migration
5. Update error handling for new library exceptions

### Benefits of Migration:
- No Excel installation requirement
- Better performance
- Simplified deployment
- Cross-platform compatibility potential
- Modern async/await support
- Better memory management

## Build Configuration

- **Post-Build Events**: Automatically copies .addin files and DLLs to Revit Addins folder
- **Target**: Revit 2021 (based on post-build paths)
- **Configuration**: Debug and Release configurations available
- **Output**: Library (.dll) for Revit to load

## Development Workflow

**Hot Reload**: During development, Visual Studio runs in debug mode with Revit as an external application. Use **hot reload** to apply code changes instead of rebuilding. Rebuilding restarts Revit, so only rebuild when absolutely necessary (e.g., changes to project structure, adding new files, or changes that hot reload cannot handle).

- **Prefer hot reload** for code logic changes within existing methods
- **Rebuild only when required** for structural changes (new classes, new files, signature changes)

## Known Issues (Post .NET 8 / Revit 2026 Upgrade)

### Floating-Point Precision in Area Display
- **Symptoms**: Area values showing excessive decimal precision (e.g., 273.78999999999996 instead of 273.79)
- **Affected Components**: 
  - SmartRound.sqFeetToSqMeters() method in SmartRound.cs:30
  - Values displayed in OutputReport dialog (OutputReport.cs:26-31, 35-46)
- **Root Cause**: Changes in .NET 8 and/or Revit 2026 API affecting the UnitFormatUtils.Format method's precision handling
- **Context**: This issue did not exist in the previous .NET Framework 4.8 + Revit API 2023 version
- **Constraint**: Must use Revit's internal API rounding (`UnitFormatUtils.Format`) to ensure values match exactly what users see in Revit UI, including Revit's own rounding inaccuracies

## Notes

- The project uses Bulgarian language throughout the UI, indicating regional specificity
- Parameter names suggest compliance with Bulgarian building regulations
- The plugin handles multiple plot types and area schemes
- Includes smart rounding functionality for calculations
- Project appears to be version 1.06 based on release files
- **IMPORTANT**: Recent upgrade to .NET 8 and Revit 2026 API has introduced floating-point precision display issues

## Excel Export Migration (Interop to ClosedXML)

### Naming Conventions for ClosedXML Migration
When converting Excel Interop methods to ClosedXML equivalents:

1. **Variable Names**: 
   - Keep existing naming: `workSheet` (capital S) - NOT `worksheet` (lowercase s)
   - Follow original Interop code variable naming exactly

2. **Method Names**: 
   - Original Interop helpers: `setMergeBordersColorAndAlignment`, `setBoldRange`, `setPlotBoundaries`
   - ClosedXML equivalents: Add `XL` suffix: `setMergeBordersColorAndAlignmentXL`, `setBoldRangeXL`, `setPlotBoundariesXL`

3. **Parameter Consistency**:
   - All helper method parameters must use `workSheet` (capital S) to match calling code
   - Method signatures should maintain the same parameter order and types as Interop versions

4. **Type Declarations**:
   - **NEVER use `var`** unless absolutely necessary
   - Always specify the correct explicit type (e.g., `IXLRange`, `IXLWorksheet`, `string`, `int`)
   - Example: Use `IXLRange mergeRange = workSheet.Range(...)` NOT `var mergeRange = workSheet.Range(...)`

### Migration Progress
- ✅ **Precision Issues**: Fixed using AritmeticAssistant class
- ✅ **ClosedXML Integration**: NuGet package added with proper dependency deployment
- 🔄 **Excel Export**: Partial migration complete (lines 1675-1813)
- ❌ **Formatting Issues**: Header merging and borders need refinement
- ❌ **Complete Migration**: Need to finish property loops and area calculations