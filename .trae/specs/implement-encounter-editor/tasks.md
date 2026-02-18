# Tasks

- [x] Task 1: Clean up `EncounterViewModel.cs`
  - [x] Remove complex `common_enc` pre-loading logic.
  - [x] Remove broken `LoadMapImage` logic or strictly wrap it.
  - [x] Define `EncountTable` and `EncountChara` lists in ViewModel to match legacy.
- [x] Task 2: Implement Map Loading
  - [x] Ensure `LoadMaps` populates `Maps` collection.
  - [x] When Map is selected, trigger `LoadEncounter(mapName)`.
- [x] Task 3: Implement Table & Slot Loading (Legacy Logic)
  - [x] Implement/Port `GetMapEncounter` logic (reading `.cfg.bin` from map file).
  - [x] Populate `Tables` (ObservableCollection of strings: "Table 1", etc.).
  - [x] On Table selection, populate `Slots`.
    - [x] Iterate `SelectedTable.EncountOffsets`.
    - [x] If offset != -1, find corresponding `EncountChara`.
    - [x] Resolve Name using `Charaparams` and `Charanames`.
    - [x] Create `EncountSlotViewModel` with Name, Level, and (Optional) Image.
