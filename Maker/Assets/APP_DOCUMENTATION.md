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

---

## 3. Architecture & Code Structure

```
Assets/
├── Code/
│   ├── AI/                        # LLM integration
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
│   ├── Proc/                      # External processing & async jobs
│   │   ├── Process.cs / ProcessManager.cs / ProcessSync.cs
│   │   ├── AI/                    # AI-specific processes
│   │   ├── Meshcapade/            # Meshcapade avatar API client
│   │   └── Paint/                 # Text→Part: PartTemplateService + feature spec
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

---

## 8. Notes for AI Assistants

- Most game logic is in `Assembly-CSharp` (no explicit asmdef) or `Maker.Runtime`.
- `SerializableDictionary` comes from the **Rotary Heart** plugin, not a Unity built-in.
- API keys / credentials should be stored in environment variables or a git-ignored `credentials.json` – **never** committed to the repository.
- Unity version: check `ProjectSettings/ProjectVersion.txt` for the exact editor version.
- When editing data models (`ConfigData`, etc.), ensure backwards compatibility with existing saved files.
- **Twin names are limited to 11 characters** (`TwinNameValidator`: `^[a-zA-Z0-9_()-]{1,11}$`; the code comment claims 14 but the regex enforces 11). Invalid names fail silently apart from a toast — the New/Save-as buttons then simply don't switch modes.
- `PartData.description` is the free-text field the user edits in the Part detail UI; `meaning`/`nameTool`/`colorTool` are stamped from the active tool by `PartManager.StoreCurrentPartInformation()`.
- `PartManager.SaveData` serialises via `JsonUtility.ToJson(this)`; the `PartData.group` ↔ `GroupData.groupParts` cycle triggers "Serialization depth limit 10 exceeded" warnings — known/pre-existing behaviour, the saved format relies on it.
- PlayMode tests can be launched from automation via **Tools → Template PoC → Run PlayMode Test** (`Assets/Tests/Editor/TemplatePoCRunner.cs`); results are written to `Temp/TemplatePoCResults.json`. The body-region template library is generated via **Tools → Template Library → Batch …** (output in `TemplateLibrary/`, see its README).
- A **new part** is started by a tool change, a paintable-texture change, leaving an Edit mode to Main/Shape/Move, or selecting a view — **not** by switching the current group (known bug, ticket pending: `PartManager.SetCurrentGroup` never sets `startNewPart`, and the `startNewPart = true` in `StartNewGroup` is unreachable dead code). Consequence: after switching groups without changing the tool, the next stroke is appended to the previous part and stays in the old group.
- For programmatic painting in tests, read `Assets/Tests/PlayMode/NoAPICalls/CwPaintingTestGuide.md` first — especially the single-frame stroke gotcha.
- Paint commands are recorded by `PaintCommandSerialization` (`Assets/Code/DataPersistence/`), the app-owned base class of `PartManager`. It is adopted from the PaintIn3D example script `CwCommandSerialization`, which is therefore unused and can be overwritten freely on CW updates. The target texture is a runtime binding (bound in `PartManager.LoadData`), not saved data; `PartData.group` is likewise re-linked on load instead of serialized (it would inline a cycle and bloat the file). The app saves on quit (`OnApplicationQuit → SaveConfig`) — never edit config files while the app runs. App Reset deletes all profiles.
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
- **Text → Part feature** (`Assets/Code/Proc/Paint/`): `PartTemplateService.PaintRegion(twin, region)` paints a pre-painted body-region template (bundled twins under `Resources/templates/`, 98 regions — catalog in `Assets/Resources/BODY_REGIONS.md`) into the twin's active group, optionally as a chosen marker/filler tool. The part carries `regionKey` plus the localized region name as its description. Region names live in `TwinLocalTables` under `region.<key>` for `enmed`/`demed`/`demedlatin` and are imported from `Assets/Resources/region_names.tsv` via **Tools → Localization → Import Region Names**. Manual selection UI: `RegionManager`. Spec and findings: `Assets/Code/Proc/Paint/FEATURE_TEXT_TO_PART.md`. Generation tooling: `Assets/Tests/PlayMode/TemplateLibraryTools/` (marked `[Explicit]` — not part of the app test suite).
