# Encounter Editor Loading Fix V2 Spec

## Why
The Encounter Editor's map list remains empty despite previous fixes. Analysis reveals that the file filtering logic is too strict, searching only for "common_enc" files and ignoring standard map encounter files (e.g., `t101g00_enc_01.cfg.bin`). Additionally, there may be issues with path construction in the recursive search.

## What Changes
- **Relax File Filtering**: Update `FindEncountFiles` in `EncounterViewModel.cs` to match any file containing `_enc` (or just ending in `.cfg.bin` within the `map_encounter` folder) instead of requiring "common_enc".
- **Robust Path Construction**: Ensure the recursive file search builds valid absolute paths that can be resolved by the Virtual File System (VFS).
- **Debug Logging**: Add temporary logging to trace the number of files found and any path parsing errors.

## Impact
- **Affected File**: `ICN_T2/UI/WPF/ViewModels/EncounterViewModel.cs`
- **User Experience**: The "Map" dropdown should now populate with a list of encounter files.

## ADDED Requirements
### Requirement: Correct File Discovery
The system SHALL scan the `map_encounter` directory and its subdirectories for all valid encounter files.
- **Criteria**: Files must end with `.cfg.bin`.
- **Criteria**: Files should likely contain `_enc` in their name (to be verified against legacy logic).

### Requirement: Valid Path Resolution
The system SHALL construct file paths that match the VFS keys exactly, ensuring `GetFileFromFullPath` returns a valid file object.

## MODIFIED Requirements
### Requirement: `LoadMaps` Logic
- **Old**: Filtered for `common_enc`.
- **New**: Filter for `_enc` or all `.cfg.bin` files relevant to encounters.
