# Fix Encounter Editor Loading Spec

## Why
The user reports that the Encounter Editor is empty. This is caused by a bug in the file discovery logic:
1. `FindFileRecursive` and `FindEncountFiles` were not correctly reconstructing full file paths, causing `GetFileFromFullPath` to fail.
2. `LoadYokaiNames` failed to find `chara_text` due to the same path issue, resulting in an empty Yokai list.
3. The logic for `GetMapWhoContainsEncounter` in the legacy code was referenced, but for YW2 (which had empty legacy support), we must implement a robust search for `common_enc` files as requested.

## What Changes
- **EncounterViewModel.cs**:
  - **Fix File Discovery**: Rewrite `FindEncountFiles` and `FindFileRecursive` to correctly propagate directory paths during recursion.
  - **Case Insensitivity**: Ensure all file name checks are case-insensitive.
  - **Optimization**: Use `_game.Files["map_encounter"]` (mapped to `data/res/map`) as the search root for maps.
  - **Yokai Names**: Fix `LoadYokaiNames` to correctly locate `chara_text` using the fixed recursive search.
  - **Debug Logging**: Add explicit debug output for every step of the loading process to aid future troubleshooting.

## Impact
- **Affected Specs**: `implement-encounter-editor` (fixes it).
- **Affected Code**: `ICN_T2/UI/WPF/ViewModels/EncounterViewModel.cs`.

## MODIFIED Requirements
### Requirement: Robust File Loading
The system SHALL correctly identify and load `common_enc` files from the `map` directory.
The system SHALL correctly load `chara_text` files to populate Yokai names.

#### Scenario: Fix Verification
- **WHEN** user opens Encounter Editor
- **THEN** "Map" dropdown is populated with `*common_enc*.cfg.bin` files.
- **WHEN** user selects a map
- **THEN** "Table" dropdown is populated.
- **WHEN** user selects a table
- **THEN** Grid is populated with Yokai names (not just "Unknown").
