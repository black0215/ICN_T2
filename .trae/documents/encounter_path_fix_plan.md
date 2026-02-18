# Encounter Editor Path & PCK Support Plan

## Why
The user has specified the correct location and format for the map files, which differs from the previous implementation. The files are located at `/data/map/(subdirectory)/mapenv.pck` and contain files ending in `_mapenv.bin`. Additionally, `.pck` files must be unpacked using `XPCK` logic.

## What Changes
- **Search Path**: Change the root search directory from `map_encounter` to `/data/map`.
- **PCK Handling**: Implement logic to detect `.pck` files (specifically `mapenv.pck`), open them using `Level5.Archives.XPCK.XPCK`, and search inside them.
- **File Pattern**: Look for files matching the pattern `*_mapenv.bin` inside the unpacked `.pck`.
- **Path Separators**: Use system-agnostic path handling (e.g., `Path.Combine` or `Path.DirectorySeparatorChar`) where applicable, while respecting the VFS structure.

## Impact
- **Affected File**: `ICN_T2/UI/WPF/ViewModels/EncounterViewModel.cs`
- **Dependencies**: Requires `ICN_T2.Logic.Level5.Archives.XPCK` namespace.
- **Functionality**: The Map list will now populate with `_mapenv.bin` files found within `mapenv.pck` archives.

## Implementation Steps
1.  **Update Imports**: Add `using ICN_T2.Logic.Level5.Archives.XPCK;` to `EncounterViewModel.cs`.
2.  **Modify `FindEncountFiles`**:
    -   Change entry point to scan `/data/map`.
    -   Iterate through subdirectories.
    -   Check for existence of `mapenv.pck`.
    -   If found, load the PCK.
    -   Iterate files inside the PCK and filter for `_mapenv.bin`.
3.  **Path Handling**: Ensure the stored path indicates it's inside a PCK (e.g., `/data/map/t100/mapenv.pck/t100_mapenv.bin`) so `LoadEncounter` can resolve it later.
    -   *Note*: The current VFS might not support "path inside file" natively. I may need to adjust `LoadEncounter` to handle this "virtual path" by opening the PCK again.
