# Fix Encounter Editor Path Logic Plan

## Problem
The user reports that the Encounter Editor is still empty.
The investigation reveals that `LoadMaps` in `EncounterViewModel.cs` searches for maps starting from the `map_encounter` sub-directory but passes an empty `currentPath`. This results in **relative paths** (e.g., `t1/file.bin`) being stored in the `Maps` list.
However, `LoadEncounter` uses `_game.Game.Directory.GetFileFromFullPath(mapPath)`, which expects a **full path** from the root (e.g., `data/res/map/t1/file.bin`).
This mismatch causes `FileNotFoundException`, leading to an empty map list (or failing to load selected maps).

## Solution
1.  **Modify `LoadMaps`**:
    *   When using the optimized search (starting from `map_encounter` folder), retrieve the full path of that folder (e.g., `data/res/map`).
    *   Pass this full path as the `currentPath` argument to `FindEncountFiles`.
    *   This ensures `Maps` contains full paths that are valid for `GetFileFromFullPath`.

2.  **Verify `LoadYokaiNames`**:
    *   Ensure `FindFileRecursive` is working as expected (it seems correct for root search, but worth double-checking if `chara_text` is not found).
    *   Add more aggressive logging to pinpoint if Yokai loading is also failing.

3.  **Refine `FindEncountFiles` and `FindFileRecursive`**:
    *   Ensure they handle path separators consistently (using `/`).

## Steps
1.  Edit `ICN_T2/UI/WPF/ViewModels/EncounterViewModel.cs`.
2.  Update `LoadMaps` to pass `mapInfo.Path` as `currentPath`.
3.  Update `LoadYokaiNames` to ensure robust fallback.
4.  Verify logic.

## Verification
- The user should see the map list populated.
- Selecting a map should populate the table list.
- Selecting a table should show Yokai names.
