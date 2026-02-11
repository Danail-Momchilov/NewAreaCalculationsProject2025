# Area Calculations Plugin v2.1.1

A comprehensive Area Calculations plugin for Autodesk Revit, specifically tailored for Bulgarian building regulations and practices.

## Version 2.1.1

### New Features
- **Settings**: Area Scheme and Phase selection for filtering areas by scheme and site elements/rooms by phase
- **Common Area Groups**: New parameter `A Instance Area Common Group` for determining the main area of common parts, replacing the old primary-based logic
- **Migration Tool**: One-click migration button to switch from old to new common area logic
- **Special Common Area %**: Calculation of special common area percentages
- **Improved Error Handling**: Additional validation warnings across all commands, including cross-project settings detection

### Bug Fixes
- Fixed minor calculation inaccuracies
- Minor improvements to the Excel export
- Fixed settings not resolving correctly when switching between projects

## Features

The plugin provides comprehensive area calculation functionality including:
- Plot parameters calculations
- Area coefficient assignments
- Area parameter calculations
- Common area group management
- Excel export functionality with ClosedXML
- Bulgarian language UI messages and tooltips
- Smart rounding utilities
- Multiple plot type support (Standard, Corner, Two Zones, Two Plots)

## Technical Details

- **Target Framework**: .NET 8
- **Revit Version**: 2025 and 2026
- **Excel Library**: ClosedXML (no Excel installation required)
- **Language**: Bulgarian UI
- **Dependencies**: All ClosedXML dependencies automatically deployed
