# Encounter Editor Re-implementation Spec

## Why

The current Encounter Editor implementation is failing to load and display table data correctly, specifically missing the `Tables` and `Slots` population. The user has requested to discard the existing complex logic (including image handling if it causes issues) and strictly follow the legacy implementation from `Albatross-legacy/Forms/Encounters/EncounterWindow.cs`.

## What Changes

* **Data Loading Logic**:

  * Re-implement `LoadEncounter` to use `GetMapWhoContainsEncounter` (or equivalent available in current `IGame`) to populate the map list.

  * Implement `GetMapEncounter` to fetch `EncountTables` and `EncountCharas` when a map is selected.

  * **Crucial**: Populate the "Table" dropdown with indices (Table 1, Table 2...) based on `EncountTables.Count`.

  * **Crucial**: Populate the "Slots" list based on `SelectedEncountTable.EncountOffsets` (or `Charas` for Blasters).

* **UI Logic**:

  * Remove complex image loading for now (or simplify it to avoid crashes).

  * Bind `Tables` dropdown to a list of strings ("Table 1", "Table 2"...).

  * Bind `Slots` to a collection of ViewModels representing the rows in the legacy DataGrid.

* **Removed Features**:

  * **BREAKING**: Remove the `common_enc` pre-loading logic if it conflicts with the legacy approach of loading per-map.

  * Remove direct `.pck` parsing logic inside the ViewModel if `IGame` or helper classes already handle it (Legacy uses `GameOpened.GetMapEncounter`).

## Impact

* **Affected File**: `ICN_T2/UI/WPF/ViewModels/EncounterViewModel.cs`

* **Dependency**: Requires `ICN_T2.Logic.Game` (or equivalent) to implement `GetMapEncounter` and `GetMapWhoContainsEncounter` if they don't exist, or use existing `VirtualDirectory` traversal that mimics them.

## ADDED Requirements

### Requirement: Legacy-style Data Population

The system SHALL populate the `Tables` list immediately upon selecting a map.
The system SHALL populate the `Slots` list immediately upon selecting a table, using the `EncountOffsets` to lookup `EncountCharas`.

## MODIFIED Requirements

### Requirement: `LoadEncounter`

* **Old**: Complex file reading, PCK parsing, `common_enc` lookup.

* **New**: Call `GetMapEncounter` (to be implemented or adapted), store `Tables` and `Charas`, update UI collections.

## REMOVED Requirements

### Requirement: Image Loading (Temporarily)

* Disable `IMGC` loading if it continues to cause crashes, or wrap it in a safe try-catch that returns null on ANY error, as per legacy `try { ... } catch { yokaiPicture = null; }`.

