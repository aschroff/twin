# PlayMode Tests — Overview

> The **technical** inventory: every test, what it checks, and where it lives. The map that says
> which part of the app each test belongs to — and what is not covered at all — is
> `Assets/Tests/PROCESS_LANDSCAPE.md`, which is also the document the business department reads.
> It replaced `TESTUEBERSICHT_FACHABTEILUNG.md`.

All tests load the *Maker Main* scene and drive the app through its real UI. `PlayModeTestBase`
redirects the data path to a temp directory per test, so runs never touch your own twins.

Run them from the Unity Test Runner, or from the automation menu **Tools → Template PoC → …**
(results are written to `Temp/TemplatePoCResults.json`, see `Assets/Tests/Editor/TemplatePoCRunner.cs`).

## Base classes

| File | Purpose |
|------|---------|
| `PlayModeTestBase.cs` | Scene loading, temp data path, UI helpers (click by name/path, wait for mode, find list entries, `DragOnCanvas` for painting). |
| `NoAPICalls/TwinPaintTestBase.cs` | App flows shared by painting tests: load the LipEdema twin, select a view, choose the current group, paint with a marker, show/hide a group, assert parts are usable. |

## `NoAPICalls/` — app tests, no external services

| Test | What it checks |
|------|----------------|
| **SaveTwinPlayModeTests**<br>`SaveButton_OpensSaveMode` | Save screen opens, its buttons are present, a new twin can be created and appears in the twin list. |
| **SettingsUiPlayModeTests**<br>`SettingsButton_EnablesSettingsMode` | Settings screen opens. |
| **EditUiPlaymodeTests**<br>`EditButton_EnablesEditMode` | Core editing round trip: open a twin, enter Edit mode, all tool buttons present, select a view, paint with a marker, return to Main, open the group detail page and find the painted part in its group. |
| **GroupDetailPlayModeTests**<br>`PaintingPerGroup_ShowsOnePartPerSelectedGroup` | Paints one part into every group of the LipEdema twin (a different marker each) plus one into a newly created group; then on the group detail page selects one group at a time and verifies exactly that group's single part is listed. |
| **GroupPlayModeTests**<br>`HideAndShowGroup_KeepsItsParts` | Hiding and showing a group (overlay toggle) replays the visible groups; the hidden group keeps its parts, they stay bound to the paintable texture, and painting still works afterwards. |
| **GroupPlayModeTests**<br>`GroupList_ShowsPartCounts_AddsAndDeletesGroups` | Group list dialog: part count per group, adding a group by naming the empty entry (it becomes the current group and a fresh empty entry appears), deleting a group that is not the current one. |
| **GroupPlayModeTests**<br>`SaveAndReload_KeepsGroupsPartsAndTheirLinks` | Groups, their parts, part↔group links, visibility flags and the selected group survive leaving the twin and coming back. |
| **InfoDisplayPlayModeTests** (5 tests) | The status displays: the twin name in the header of every screen and the Twin/Version/Tool/Group block of the overview overlay — after a reset (the app falls back to `default.000`), after loading and after creating a twin, when a tool is picked and when the current group changes. |
| **ImportTwinPlayModeTests** (8 tests) | Export/import of a twin: the paint is visible as soon as an imported twin is opened (it travels as `Texture.png` in the twin folder, because the in-app texture cache never leaves the device); groups, parts and their links survive the round trip; an import never overwrites a twin that is already there (`V01`, `V02`, …, first free slot, and the suffix of a returning twin is kept); the twin is named after its config, not after the zip file (which anything may rename); importing while a twin of the same name is open keeps both apart; and a broken archive leaves the existing twins untouched. |
| **DocumentPromptPlayModeTests** (3 tests) | The dynamic prompt for Document → Twin: the two editable Settings rows arrive with their shipped default **in full** (a character limit that is too small truncates it), and the assembled prompt carries the language directive, both rows, every group of the twin, the tools split into "has a meaning" and "still free", and all 98 region keys. Also that the tool inventory lists every marker and filler — including the first row of each panel, which the older report prompt skips. The prompt is written to `Application.temporaryCachePath/DocumentPrompt/` for reading. The third test walks Upload → pick →
review screen (handing the pick over directly, since an OS dialog cannot be driven) and checks the
screen names the file and carries the whole prompt. |
| **DocumentApplyPlayModeTests** (6 tests) | Applying a document mapping to the twin — the only step of Document → Twin that changes anything. Nothing ticked changes nothing; what *is* ticked lands exactly where it should (group created, free tool given its meaning **before** anything is painted with it, a two-region finding as two parts carrying the document's text, the patient text appended to the report without losing what was there, the user's current group put back, all of it surviving the save); and what the twin does not allow is refused per item without costing the rest — a tool that already means something keeps its meaning, an unknown tool or region drops only its own item, the same region twice stays one part. The fourth test drives the **review screen**: a row per proposal plus a heading per block, every row unticked *and looking unticked* (the tick's alpha is asserted — the row prefab came from the group list, where the tick sat inside the toggled graphic and stayed visible), the region count on the row that costs the parts, a long treatment line that has to **wrap** rather than clip (its row must be taller than a short one and tall enough for its own text), then Apply through the real button of the prefab — after which the applied rows come back ticked, dimmed and locked, and a second Apply writes nothing. The last two tests cover the group chip (the picker offers the twin's groups and the proposed ones, writing the choice back into the proposal without touching the twin, and the text below follows it) and the way back to a proposal already in hand (leaving the screen writes nothing, returning needs no upload, and a mapping read for another twin is refused). Screens are written to `Application.temporaryCachePath/UploadReview/` for looking at. |
| **UploadPlayModeTests**<br>`UploadButton_OffersPhotoAndDocument` | The Upload button of the main screen opens the upload panel, which offers exactly two ways for a document to reach the twin — a photo and a file — each labelled from the localization table and wired to `DocumentUploadProcess` with its variant. The pick itself opens an OS dialog and is not driven. A second test covers the third entry, the way back to a proposal already in hand: it is configured but **not offered** before anything has been read, appears once a proposal exists, and disappears again for a twin the proposal was not read for. |
| **StickerPlayModeTests** (2 tests) | Sticker images per twin: the image behind a sticker slot — and the image its hash points at — follows the twin that is open, and an imported twin brings its own images for slots the open twin uses with different ones. |
| **UndoRedoPlayModeTests** (2 tests) | Undo/redo in the editing header, through the real buttons of the prefab: a click takes the last painted part out of its group, off the body (pixel comparison) and out of the save data, the next click brings it back at its place and repaints it, new paint afterwards ends the redo history. The second test pins the configuration that keeps the body texture from being copied per stroke — `undoRedo: None` on the body, `storeStates` off on every tool, no PaintIn3D undo button left — and that painting leaves no texture states behind (that copying got the app killed on the seventh stroke on an iPad Air). |
| **PartTemplateServiceTests** (11 tests) | The Text→Part service (`Assets/Code/Proc/Paint/`): painting a body region into the active group, tool override (colour, metadata, sticker rejection), current-tool resolution with marker fallback, the region catalog, region names per language (enmed/demed/demedlatin), and the save format (linear file growth, no texture references, part↔group and texture links restored on load). |
| **ProgrammaticPaintingTests**<br>`PaintTemplateParts_ThreeRegions` | Regression test for driving the CW paint pipeline from code (marker stroke, filler area): parts get the right tool metadata, a stored view, and survive the save round trip. Foundation of the template library generator. |
| **PartsScreenshotProcessPlayModeTests**<br>`ExecuteSync_WritesScreenshotForLinkedPart` | Paints a part on LipEdema, links it into the "Swell" group, runs `PartsScreenshotProcess.ExecuteSync` the way `VersionSequenceProcess` does (awaiting `ExecuteCompleted`), and asserts the `screenshot_<profile> - <group> - part <id>.png` file lands in the profile's data folder. |
| **SkinProcessPlayModeTests**<br>`ExportSkin_WritesSkinPngForSelectedProfile` | Opens a twin's Menu screen, clicks "Export skin", and asserts `skin_<profile>.png` is written to the profile's data folder. |
| **TourProcessPlayModeTests**<br>`ExportStandardViews_WritesScreenshotPerView` | Opens a twin's Menu screen, clicks "Export standard views" (which only starts a coroutine, so the test ticks a few frames), and asserts one `screenshot_<profile> - <view>.png` per configured `StandardViewManager` view. |
| **PartsDescriptionProcessTests**<br>`DescribeParts_SetsDescriptionFromRealScreenshot` | Lives here despite calling the real OpenAI API (skips itself via `Assert.Ignore` without a key in `testsecrets.json`). Paints a part, produces a real screenshot via `PartsScreenshotProcess` — `PartDescriptionProcess` only calls the AI once a screenshot exists on disk — then calls `PartsDescriptionProcess.Handle`, which fans out over every part, and waits for the description to move past the `"Part Number N :\n..."` placeholder `Execute()` stamps in before the per-part AI call overwrites it. |

## `APICalls/` — tests that call the real OpenAI API

> Since TWIN-455 this folder also holds `OpenAIClientTests` and `DocumentMappingApiTests` (which
> used to sit at the root of `PlayMode/`) and `PartsDescriptionProcessTests` (which used to sit in
> `NoAPICalls/` and really called the model whenever a key was configured). Every test here guards
> itself with `Assert.Ignore` when there is no key, so the folder is about **cost**, not failure.

All need a valid key in `Assets/Tests/Helper/testsecrets.json`; the Process tests skip themselves
(`Assert.Ignore`) without one, and `401`s otherwise.

| Test | What it checks |
|------|----------------|
| **PartDescriptionProcessTests**<br>`DescribePart_SetsDescriptionFromRealScreenshot` | Paints one part on LipEdema, produces a real screenshot via `PartsScreenshotProcess` (the same order `VersionSequenceProcess` uses — `PartDescriptionProcess` only calls the AI once a screenshot exists on disk), then calls `PartDescriptionProcess.Handle` for that single part and waits for a real, non-error description. |
| **VersionProcessTests**<br>`DescribeVersion_SetsPromptResultFromRealPart` | Sets a part's description directly — skipping a second real AI call — so `partManager.AllPartsDescribed()` is true, clears the Version-level `ItemPrompt.promptResult`, calls `VersionProcess.Handle`, and waits for a real, non-error prompt result. |
| **VersionSequenceProcessTests**<br>`RunSequence_DescribesPartAndVersionFromRealScreenshot` | Covers the "VersionSequenceProcess" GameObject's `SequenceProcess`, which chains `PartsScreenshotProcess` → `PartsDescriptionProcess` → `VersionProcess`. Paints a part with no screenshot and no description yet, calls `sequenceProcess.Handle`, and waits (60s, for two real AI calls in a row) for a non-error part description and version prompt result. |
| **OpenAIClientTests** (8 tests) | The `OpenAIClient` class directly — no scene, derives from `TestBase` rather than `PlayModeTestBase`: simple and structured requests, invalid-key handling, an empty-key constructor throw, parallel requests, model listing, file upload, and the AI component's image/PDF input path. |

## `Assets/Tests/EditMode/` — unit tests, no scene, no app

| Test | What it checks |
|------|----------------|
| **JsonSchemaBuilderTests** (6 tests) | The JSON schema requested for structured outputs: nested objects and lists are described to the bottom, injected value lists become `enum`s (on the items for an array member), the strict-mode invariants hold recursively over a whole schema, the two responses already in use stay flat, and a self-referencing type fails instead of hanging. |

| **PartHistoryTests** (13 tests) | The bookkeeping of part undo/redo on a bare `PartManager`: the last part goes, in painting order across groups; redo returns it to its old index with its group link; new paint or a stroke after undo starts a fresh part and ends the redo history; parts that arrive with the twin are not undoable, template parts are; a part deleted through the list or a deleted group leaves the history; loading drops it; an undone part is gone from the save data. **Tools → Template PoC → Run Part History Tests**. |

| **ApiKeysTests** (8 tests) | Reading the OpenAI key from a file outside version control: the member `testsecrets.json` uses plus the other spellings, whitespace trimmed, and a missing, empty or broken file yielding nothing instead of throwing at startup. |

EditMode on purpose. A PlayMode test that does **not** derive from `PlayModeTestBase` starts the
real app against the real data path — the sandboxing lives in that base class.

Run them from **Tools → Template PoC → Run Schema Tests**.

The Document → Twin tests are **Tools → Template PoC → Run Document Apply Tests** (6, offline),
**Run Document Prompt Tests** (3, offline), **Run Upload Tests** (1, offline) and
**Run Document Mapping API Test** (2, needs a key).

## `TemplateLibraryTools/` — not tests

`TemplateLibraryGenerator` + `TemplateRegionTable` generate the body-region template library.
They are `[UnityTest]` only because painting needs play mode; all methods are marked
`[Explicit]`, so **"Run All" skips them**. Start them from **Tools → Template Library → Batch …**.
See `TemplateLibrary/README.md` for the workflow.

## Outside `NoAPICalls/` and `APICalls/`

**DocumentMappingApiTests** (2 tests, still directly under `PlayMode/`) send an invented document
to the LLM. The first checks the answer can be applied: every body region is one of the 98 keys,
every tool is a tool of this app, every group is existing or proposed, a tool taken into use was
free, and what concerns the patient as a whole comes back as the patient text. The second drives
the same thing **through the app**, with the real two-page PDF
`Assets/Tests/Helper/lipoedema-report-sample.pdf` (a fictional lipoedema report): Upload, pick,
upload, call, proposal on the review screen. It also proves the app finds a key, that the PDF
upload path works — a text file takes a different one — and that a report full of symmetrical
findings yields at least one multi-region painting.
**Tools → Template PoC → Run Document Mapping API Test**.

## Notes for writing new tests

- **Painting needs a framed body.** The paint position is the screen centre; a twin's saved
  camera may point somewhere else (LipEdema's shows the lower body, where the centre falls
  between the legs and hits nothing). Select a view first — `TwinPaintTestBase.BodyView`.
- **A new part starts on a tool change**, not on a group change (known bug), so give each part
  its own marker when you need parts in different groups.
- **Painting details and gotchas** (single-frame strokes, fill tools, replaying commands):
  `NoAPICalls/CwPaintingTestGuide.md`.
