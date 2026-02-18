# Tasks

- [ ] Task 1: Verify and Fix File Discovery
  - [ ] SubTask 1.1: Modify `FindEncountFiles` in `EncounterViewModel.cs` to be case-insensitive.
  - [ ] SubTask 1.2: Update `LoadMaps` to check `_game.Files["map_encounter"]` (if it exists) to get the specific map folder, optimizing search.
  - [ ] SubTask 1.3: Add debug logs to `LoadMaps` to print found files count.

- [ ] Task 2: Verify and Fix Yokai Name Loading
  - [ ] SubTask 2.1: Update `LoadYokaiNames` to try `_game.Files["chara_text"]` first.
  - [ ] SubTask 2.2: Ensure `FindFileRecursive` is case-insensitive.
  - [ ] SubTask 2.3: Add debug logs for Yokai name loading.

- [ ] Task 3: Verify Encount Table Parsing
  - [ ] SubTask 3.1: Check `EncountConfig.cs` (read file first) to confirm Entry names (`ENCOUNT_CONFIG_INFO_BEGIN`, etc.).
  - [ ] SubTask 3.2: Ensure `LoadEncounter` uses correct entry names.

- [ ] Task 4: Verification
  - [ ] SubTask 4.1: User verification (since we can't run the UI).
