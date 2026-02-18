# Tasks

- [x] Task 1: Update file filtering logic in `EncounterViewModel.cs`
  - [x] Modify `FindEncountFiles` to check for `_enc` instead of `common_enc`.
  - [x] Ensure the file extension check `.cfg.bin` is case-insensitive.
- [x] Task 2: Verify and fix path construction
  - [x] Review the recursive path concatenation in `FindEncountFiles` to ensure correct slash handling.
  - [x] Ensure the root path passed to `FindEncountFiles` from `LoadMaps` is correct.
- [x] Task 3: Add debug logging
  - [x] Add `Console.WriteLine` or similar logging in `LoadMaps` to print the count of found files.
  - [x] Log the first few found paths to verify format.
