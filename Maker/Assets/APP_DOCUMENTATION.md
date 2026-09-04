# Twin Maker – App Documentation

> **Purpose:** Living reference document for AI assistants and developers working on this project.

---

## 1. Overview

**Twin Maker** is a Unity-based cross-platform application (iOS, Android, desktop) that allows patients and doctors to create a **digital twin** of a patient. The digital twin serves as a medical reference for:

- Clinical documentation and reports
- Querying an LLM about the patient's condition, treatments, and progression over time
- Visual annotation of the patient's body

The core interaction revolves around an **interactive 3D human mesh** that users can paint, annotate, and modify.

---

## 2. Domain Model & Key Concepts

| Term | Description |
|------|-------------|
| **Version** | A snapshot of the twin at a specific point in time (e.g. a consultation date). Stored as `ConfigData`. |
| **Part** | A single annotation painted/placed onto the mesh – e.g. a drawn area, a sticker, a marking. Each part has a tool, a group, and optional AI-generated text. |
| **Group** | A flexible category for organising parts (e.g. *Treatments*, *Injuries*, *Pain areas*). Stored as `groupList` in `ConfigData`. |
| **Tool** | Defines what a part *means*. Examples: burning sensation brush, lymphatic drainage brush, injury sticker. Tools carry semantic metadata used in prompts. |
| **View** | A saved camera angle / body pose for quick navigation. |

### Data persistence

- `ConfigData` – serialised per-version data (camera, groups, parts, prompts, shape params, etc.)
- `AttributesData` – extensible sample attributes (currently placeholder integers)
- `DataPersistenceManager` / `FileDataHandler` – load/save pipeline via `IDataPersistence`
- Serialisation uses `SerializableDictionary` and Newtonsoft JSON (`com.unity.nuget.newtonsoft-json`)

Every twin version is one directory under the persistent data path, named `<name>.<version>`,
and that directory name is what identifies the twin in the app (`selectedProfileId`):

```
LipEdema.twin/
  ConfigTwin              # the ConfigData json (+ ConfigTwin.bak)
  Texture.png             # the painted body texture, written on export
  <sticker slot id>.png   # one image per sticker slot the twin uses
```

`ConfigData.name` / `.version` must match the directory name: the twin list shows them from the
config while selecting a twin looks up the directory, so a mismatch gives a row that cannot be
opened.

The painted body texture is also cached per twin in **PlayerPrefs** (`CwPaintableTexture.Save`
via `CwCommon.SaveBytes`) so switching twins does not replay every paint command. That cache is
local to the device, which is why `Texture.png` exists: on load, `Body.handleChange` takes the
cache when there is one and otherwise reads the file and fills the cache from it.

### Export / import of a twin

- **Export** (`DataPersistenceManager.ExportConfig`) saves the twin, writes `Texture.png` into
  its directory, zips the directory and hands the zip to the OS. `ExportConfigZip()` is the same
  without the OS step.
- **Import** (`ImportConfig`) unpacks into a temporary directory, reads the identity from the
  **config** – never from the zip filename, which any transport may rename – and moves the twin
  to `<name>.<version>`. If that exists, the version gets a `V01`, `V02`, … suffix, so an import
  never overwrites or deletes a twin on the device. The moved twin's config is rewritten to match
  its directory and stamped as the newest version of that name.
- Picking the file is asynchronous: `ImportConfig` reports the imported profile id through a
  callback, and `ConfigManager.ImportTwin` refreshes the twin list from there.
- Import/export is the groundwork for exchanging twin versions between users over a server, so
  everything a twin needs has to live in its directory.

**Known limits, none of them decided yet:**

- Export zips are written **into the twin data directory**. Unzipping one by hand there creates a
  directory that looks like a twin (`LipEdema.twin 2`) and the versions screen then throws. Moving
  exports to their own folder — `Exports/` inside the data dir, or `Application.temporaryCachePath`
  — is an open call.
- Version labels grow on repeated exchange: a twin imported, exported and imported again becomes
  `000V01V01`. To be revisited once versions become timestamps.
- The app cannot delete the twin that is currently selected, so the twin list can never be emptied.
- The `V99` ceiling fails an import with a log line and nothing on screen.

### Twin versions on the server

Since TWIN-437 a twin version can be uploaded to the backend and listed back. The screen is
reached from the versions screen (the cloud button, `ConfigManager.OpenVersionSync`) and shows one
row per version of the current twin: which are on the server, which exist only on this device.

- The archive that is uploaded is **the same zip the export produces** — that is why a twin
  directory has to be self-contained.
- A version already on the server cannot be ticked. The server refuses a second upload of the same
  twin name and version permanently (`TWIN_VERSION_ALREADY_EXISTS`), so offering the tick would be
  offering a guaranteed failure.
- Uploading a version that is *not* the one currently open packs its directory untouched. Going
  through the normal export would call `Save` first, which moves the directory's modification time
  — and `GetMostRecentlyUpdatedProfileId` picks the twin to open at the next start from exactly
  that. See `DataPersistenceManager.ExportZipForVersion`.
- Reading a version back down from the server does not exist yet.

Details: `Assets/Code/Net/Twins/README.md`.

---

## 3. Architecture & Code Structure

```
Assets/
├── Code/
│   ├── AI/                        # LLM integration
│   │   ├── AI_INTEGRATION.md      # reference for the OpenAI layer + prompt building
│   │   ├── AI.cs                  # Core AI controller
│   │   ├── AIService.cs           # Service abstraction
│   │   ├── MedicalAI.cs           # Medical-domain AI logic
│   │   ├── OpenAIClient.cs        # OpenAI API client
│   │   ├── StructuredOutputs.cs   # Structured response parsing
│   │   └── PromptGeneration/      # Building prompts from parts/tools
│   │       ├── Part.cs / PromptPart.cs
│   │       ├── Tools.cs / Markers.cs / Marker.cs / Fillers.cs
│   │       ├── PromptContributor.cs
│   │       ├── IPromptContributingGameObject.cs / IRoot.cs
│   │
│   ├── DataPersistence/           # Save / load system
│   │   ├── Data/                  # Data classes (ConfigData, AttributesData)
│   │   ├── DataPersistenceManager.cs
│   │   ├── FileDataHandler.cs     # files, zip export/import
│   │   ├── TwinTextureFile.cs     # the painted texture as a file in the twin directory
│   │   ├── IDataPersistence.cs
│   │   └── SerializableTypes/
│
│   ├── Interface/                 # Domain interfaces & navigation
│   │   ├── Model.cs / Lib.cs
│   │   ├── ToolTracker.cs
│   │   └── TwinNavigation.cs
│   │
│   ├── Net/                       # The Twin Maker backend
│   │   ├── Auth/                  # Sign-in, tokens, session — README.md in the folder
│   │   └── Twins/                 # Twin versions on the server — README.md in the folder
│   │
│   ├── Proc/                      # External processing & async jobs
│   │   ├── PROCESSES.md           # reference for the process layer
│   │   ├── Process.cs / ProcessManager.cs / ProcessSync.cs
│   │   ├── AI/                    # AI-specific processes
│   │   ├── Meshcapade/            # Meshcapade avatar API client
│   │   ├── Paint/                 # Text→Part: PartTemplateService + feature spec
│   │   └── Document/              # Document→Twin: upload process + feature spec
│   │
│   └── View/                      # UI layer (MVC-ish)
│       ├── Item/                  # UI item components (Body, Group, Part, Sticker, …)
│       ├── Manager/               # Screen/panel managers (PartManager, GroupManager, VersionManager, …)
│       ├── Mode/                  # Interaction modes (EditMode, MainMode, ShapeMode, …)
│       ├── Singleton.cs
│       ├── UIController.cs
│       └── InteractionController.cs
│
├── Plugins/                       # 3rd-party native plugins
│   ├── CW/                       # CodeWriter (PaintIn3D core)
│   ├── NativeFilePicker/
│   ├── NativeGallery/
│   └── SimpleFileBrowser/
│
├── SMPLX/                         # SMPL-X body model (parametric mesh)
├── SMPLX-Validation/
├── Prefabs/
├── Scenes/
├── Resources/
├── Localization/ & Tables/        # Unity Localization (en, enmed, de, demed, demedlatin)
└── Rotary Heart/                  # SerializableDictionary package
```

### Assembly definitions

| Assembly | Scope |
|----------|-------|
| `Assembly-CSharp` | Default – most game code lives here |
| `Maker.Runtime` | `Assets/Code/` (main app logic) |
| Various plugin asmdefs | LeanTouch, PaintIn3D, CW, NativeFilePicker, etc. |

---

## 4. Third-Party Packages & Plugins

### Unity packages (via Package Manager / `manifest.json`)

| Package | Purpose |
|---------|---------|
| `com.unity.localization` 1.5.11 | Multi-language UI |
| `com.unity.nuget.newtonsoft-json` 3.0.2 | JSON serialisation |
| `com.unity.test-framework` 1.3.9 | Edit/Play mode tests |
| `com.unity.ai.navigation` 2.0.0 | NavMesh (if needed) |
| `com.unity.timeline` 1.8.6 | Timeline animations |

### Asset Store / vendored plugins

| Plugin | Purpose |
|--------|---------|
| **PaintIn3D** (CW) | Painting on 3D mesh surfaces – core painting engine |
| **LeanTouch / LeanTouchPlus** | Touch input handling (pinch, rotate, swipe) |
| **LeanGUI / LeanGUIShapes** | UI helpers |
| **LeanTransition** | Tweening / transitions |
| **LeanCommon / LeanCommonPlus** | Shared Lean utilities |
| **SMPL-X** | Parametric human body model – shape/pose from parameters, images, or prompts |
| **Meshcapade API** | Cloud service to generate SMPL-X avatars from images/video |
| **Rotary Heart – SerializableDictionary** | Inspector-friendly dictionaries |
| **NativeFilePicker** | Platform-native file picker |
| **NativeGallery** | Platform-native gallery/photo access |
| **SimpleFileBrowser** | In-app file browser fallback |
| **AI Toolbox** (`AiToolbox`) | AI/LLM helper utilities |

---

## 5. AI Integration (current state)

> **Reference documents:** `Assets/Code/AI/AI_INTEGRATION.md` (OpenAI client, structured
> outputs, where prompt text comes from) and `Assets/Code/Proc/PROCESSES.md` (the process
> layer that triggers the calls). Read those before touching either layer.

### Part → Text (already working)

1. User paints/places a **Part** on the mesh.
2. A screenshot of the part on the mesh is captured.
3. `PromptGeneration/` builds a prompt including:
   - The screenshot (as base64 image)
   - The tool's semantic meaning (e.g. "burning sensation")
   - Contextual markers and fillers
4. `OpenAIClient` sends the prompt to the LLM.
5. The LLM returns a structured medical description of the part.
6. Descriptions can later be aggregated per **Version** to produce an overall patient report, or across **multiple Versions** to analyse progression over time.

### Document → Twin (in progress)

A photo or a PDF of a document (referral letter, body chart, hand drawing) is to be analysed and
its findings mapped onto the twin as groups, tools and painted body regions. Step one — the way
in — exists: the **Upload** button in the bottom row of the main screen opens the `Upload` panel,
which offers a photo (gallery picker, as in the sticker upload) or a document (OS file picker, as
in the twin import); `DocumentUploadProcess` (`Assets/Code/Proc/Document/`) does the picking.
Spec, target structure and the remaining steps:
`Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md`.

---

## 6. Platforms

- **iOS** (primary – `Build iOS/` contains Xcode project)
- **Android** (supported)
- **Desktop / Editor** (development & testing)

---

## 7. Testing

- Tests live in `Assets/Tests/` (assemblies: `PlayModeTests`, `Tests`, `EditModeTests`).
- **Overview of every PlayMode test: `Assets/Tests/PlayMode/TESTS_OVERVIEW.md`** — start there.
- PlayMode tests drive the app through its real UI in the *Maker Main* scene; the data path is
  redirected to a temp directory per test, so runs never touch your own twins.
- `NoAPICalls/` needs no external services. `OpenAIClientTests` call the OpenAI API and need a
  key in `Assets/Tests/Helper/testsecrets.json`.
- Automation: **Tools → Template PoC → …** runs tests and writes results to
  `Temp/TemplatePoCResults.json` (`Assets/Tests/Editor/TemplatePoCRunner.cs`) — useful for
  running tests from outside the editor UI.
- Writing painting tests: read `Assets/Tests/PlayMode/NoAPICalls/CwPaintingTestGuide.md` first.
- The backend clients are covered by **EditMode** tests that run against a real HTTP server on
  loopback (`Assets/Tests/EditMode/FakeTwinApiServer.cs`), not against a mock: the ways an HTTP
  client can be wrong — a form-encoded body, a doubled `/v1`, a renamed JSON key — are all
  invisible from inside Unity and only show up as a 422 against a live server.

---

## 8. Notes for AI Assistants

- Most game logic is in `Assembly-CSharp` (no explicit asmdef) or `Maker.Runtime`.
- `SerializableDictionary` comes from the **Rotary Heart** plugin, not a Unity built-in.
- API keys / credentials **never** go onto a component in the scene — that serializes them into `Maker Main.unity` and commits them. The OpenAI key is resolved by `Code.AI.ApiKeys` from `OPENAI_API_KEY`, from `secrets.json` in the persistent data path, or (editor only) from the git-ignored `Assets/Tests/Helper/testsecrets.json`. See `Assets/Code/AI/AI_INTEGRATION.md`.
- Unity version: check `ProjectSettings/ProjectVersion.txt` for the exact editor version.
- When editing data models (`ConfigData`, etc.), ensure backwards compatibility with existing saved files.
- **Twin names are limited to 11 characters** (`TwinNameValidator`: `^[a-zA-Z0-9_()-]{1,11}$`; the code comment claims 14 but the regex enforces 11). Invalid names fail silently apart from a toast — the New/Save-as buttons then simply don't switch modes.
- `PartData.description` is the free-text field the user edits in the Part detail UI; `meaning`/`nameTool`/`colorTool` are stamped from the active tool by `PartManager.StoreCurrentPartInformation()`.
- `PartManager.SaveData` serialises via `JsonUtility.ToJson(this)`; the `PartData.group` ↔ `GroupData.groupParts` cycle triggers "Serialization depth limit 10 exceeded" warnings — known/pre-existing behaviour, the saved format relies on it.
- PlayMode tests can be launched from automation via **Tools → Template PoC → Run PlayMode Test** (`Assets/Tests/Editor/TemplatePoCRunner.cs`); results are written to `Temp/TemplatePoCResults.json`. The body-region template library is generated via **Tools → Template Library → Batch …** (output in `TemplateLibrary/`, see its README).
- A **new part** is started by a tool change, a paintable-texture change, leaving an Edit mode to Main/Shape/Move, or selecting a view — **not** by switching the current group (known bug, ticket pending: `PartManager.SetCurrentGroup` never sets `startNewPart`, and the `startNewPart = true` in `StartNewGroup` is unreachable dead code). Consequence: after switching groups without changing the tool, the next stroke is appended to the previous part and stays in the old group.
- For programmatic painting in tests, read `Assets/Tests/PlayMode/NoAPICalls/CwPaintingTestGuide.md` first — especially the single-frame stroke gotcha.
- Paint commands are recorded by `PaintCommandSerialization` (`Assets/Code/DataPersistence/`), the app-owned base class of `PartManager`. It is adopted from the PaintIn3D example script `CwCommandSerialization`, which is therefore unused and can be overwritten freely on CW updates. The target texture is a runtime binding (bound in `PartManager.LoadData`), not saved data; `PartData.group` is likewise re-linked on load instead of serialized (it would inline a cycle and bloat the file). The app saves on quit (`OnApplicationQuit → SaveConfig`) — never edit config files while the app runs. App Reset deletes all profiles.
- **Undo/Redo works on parts, not on texture states.** `PartManager.Undo()` takes the last part
  painted in this session out of its group and replays the visible groups (`ClearRefreshAll`);
  `Redo()` puts it back at its old index. Parts that arrive with the twin are not undoable — they
  go through the part list — while parts painted from a region template are (`RecordPaintedPart`).
  New paint ends what could be redone; loading a twin drops the history. The header buttons
  (`Undo All Button` / `Redo All Button` in `GUI top.prefab`, and the copy in `Upload UI.prefab`)
  carry `PartHistoryButton`, which dims through the CanvasGroup while there is nothing to do.
  PaintIn3D's own undo is **switched off everywhere and has to stay off**: `undoRedo: None` on the
  body's `CwPaintableTexture`, `storeStates: false` on every `CwHitScreen` (tool prefabs and the
  scene's `Delete` tool), no `CwButtonUndoAll`/`CwButtonRedoAll`. In `FullTextureCopy` mode every
  stroke copied the 8192² body texture (256 MiB), so an iPad Air was killed by iOS on the seventh
  stroke; it also left the texture and the saved parts disagreeing, so undone strokes came back on
  the next load. Pinned by `UndoRedoPlayModeTests.Painting_StoresNoTextureCopies`.
- **Sticker images and their hashes**: a sticker *slot* (`Sticker` + `Item` in the EditSticker UI,
  10 of them) has a fixed `Item.id`, its image is `<Item.id>.png` in the twin directory, and its
  hash is `sum of the id characters % 100` (`Item.getHash`). Paint commands store that hash, so the
  hash belongs to the slot and **all twins share it** while the image behind it is per twin. Loading
  a twin therefore re-points the registration (`Sticker.Register` → `CwTextureHash` →
  `CwSerialization.HashToTexture`) at the image of that twin, and frees the hash when the twin has
  no image for the slot — otherwise replayed sticker commands draw the previous twin's image. Two
  consequences: only one twin's stickers can be registered at a time (a side-by-side view would
  need the twin in the hash), and `Item.getHash` must not be changed — the hashes are stored inside
  saved paint commands. A slot whose id hashed to `0` would count as "no hash"; none of the current
  ids do.
- **Document → Twin feature** (`Assets/Code/Proc/Document/`): the Upload button of the main
  screen offers a photo or a PDF, to be analysed and mapped onto the twin. Only the way in is
  built so far — spec: `Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md`. Adding the fourth
  bottom button meant tightening the bottom row's grid spacing from 95 to 70; the row is
  ~593 units wide on a phone in portrait, so a fourth 80-unit button does not fit otherwise.
- **UI belongs in the prefab, not in the scene instance.** Every panel under `Canvas` is a prefab
  instance, so new buttons and panels are added to the prefab asset (`Assets/Prefabs/GUI/…`).
  The scene keeps only what cannot live in a prefab: references to scene objects, above all the
  `Maker` object (`InteractionController`) that button clicks target. `Canvas` itself is not a
  prefab, so panels are children of it in the scene.
- **Text → Part feature** (`Assets/Code/Proc/Paint/`): `PartTemplateService.PaintRegion(twin, region)` paints a pre-painted body-region template (bundled twins under `Resources/templates/`, 98 regions — catalog in `Assets/Resources/BODY_REGIONS.md`) into the twin's active group, optionally as a chosen marker/filler tool. The part carries `regionKey` plus the localized region name as its description. Region names live in `TwinLocalTables` under `region.<key>` for `enmed`/`demed`/`demedlatin` and are imported from `Assets/Resources/region_names.tsv` via **Tools → Localization → Import Region Names**. Manual selection UI: `RegionManager`. Spec and findings: `Assets/Code/Proc/Paint/FEATURE_TEXT_TO_PART.md`. Generation tooling: `Assets/Tests/PlayMode/TemplateLibraryTools/` (marked `[Explicit]` — not part of the app test suite).

---

## 9. Driving the editor from outside

The project carries `com.unity.pipeline` in its committed `manifest.json`, so a **running** Unity
editor can be driven from a terminal by any tool that can shell out — not only by one assistant.
`unity status` shows whether an editor is connected; it takes ~15 s after an editor start. The
package offers, among others: run and list tests, execute C# in the editor, read the console,
capture the game or scene view, and the typed asset commands (`set_serialized_field`,
`save_prefab_contents`, `find_gameobjects`, …).

Asset edits from a script go through the normal editor API — `PrefabUtility.LoadPrefabContents` →
change → `SaveAsPrefabAsset` for prefabs, `SerializedObject` for private `[SerializeField]`
fields. Do not hand-edit `.unity` or `.prefab` YAML: the wiring lives in nested-prefab overrides
with fileID/GUID cross-references, and Unity re-serializes anyway.

**Traps, each of which has cost time here:**

- **`SerializableDictionary` (Rotary Heart) cannot be filled field by field.** Each write of
  `_keys`/`_values` is its own apply, and entries that momentarily share an empty key collapse into
  one on the next deserialize. Resize the arrays *and* set the distinct keys in a **single** apply,
  then verify by reopening the scene and counting. `UIController.uiPanels` and
  `InteractionController.interactionModes` are both of this kind.
- **Running EditMode tests requires play mode to be stopped.** Started during play mode, the run
  does not fail cleanly: it dies in the test framework's `SaveModifiedSceneTask` with
  `This cannot be used during play mode` and leaves the status stuck on "running".
- **Do not activate a UI prefab instance in the scene just to look at it.** The layout rebuild
  writes driven `RectTransform` values into `Maker Main.unity` as new prefab overrides — around
  200 lines of noise on top of the intended change. If it happened, `git checkout --` the scene and
  redo the edit without activating.
- **Saving the scene from the editor writes ~200 lines of noise** — driven `RectTransform` values
  of the UI prefab instances land in `Maker Main.unity` as new overrides on every
  `EditorSceneManager.SaveScene`, whether or not anything was activated. For a *scalar* change
  (`undoRedo: 1` → `0`, a bool on a scene component) flip the value in the YAML by hand instead,
  then let the editor pick it up (`AssetDatabase.Refresh()` + `OpenScene`; the scene reloads with
  `isDirty == false`). Prefabs saved via `SaveAsPrefabAsset` do not have this problem. The
  hand-edit warning above is about wiring — fileIDs, GUIDs, nested-prefab overrides — not about a
  number on a line that already exists.
- **A copied prefab brings its scale.** The list in `GroupDetailUI` has `localScale (2,2,1)`, so a
  rect sized for scale 1 renders twice as large. Render the result and look before believing a
  layout is right.
- **Screenshotting a Screen Space - Overlay canvas outside play mode** needs a detour: overlay
  canvases bypass every camera, so a camera capture shows nothing and a scene-view capture shows
  the world. Build a throwaway additive scene with a **WorldSpace** canvas sized to the runtime
  canvas, instantiate the panel into it, add an orthographic camera and capture that. In play mode,
  capturing the composited screen works directly.
- **Adding localization keys makes Unity re-register the string tables with Addressables.** Check
  `git diff Assets/AddressableAssetsData/` afterwards: a `Preload` label went missing that way once,
  which would have left that locale's strings unloaded in a build.

