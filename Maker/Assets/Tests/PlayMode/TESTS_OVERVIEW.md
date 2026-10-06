# Tests — Overview

> The **technical** inventory: every test, what it checks, and where it lives. The map that says
> which part of the app each test belongs to — and what is not covered at all — is
> `Assets/Tests/PROCESS_LANDSCAPE.md`, which is also the document the business department reads.
> It replaced `TESTUEBERSICHT_FACHABTEILUNG.md` (TWIN-454).

213 tests in three folders, sorted by what they cost (counted 6 October 2026, see the landscape):

| Folder | Tests | What it needs |
|---|---|---|
| `Assets/Tests/EditMode/` | 109 | nothing — no scene, no app, no network |
| `Assets/Tests/PlayMode/NoAPICalls/` | 89 | the real app, no external service |
| `Assets/Tests/PlayMode/APICalls/` | 15 | a key in `Assets/Tests/Helper/testsecrets.json`; costs tokens |

Every test carries exactly one category from `Assets/Tests/Helper/TestCategories.cs` (P01–P07,
K01–K05, `T00_technical`); `TestCategoriesGuardTests` fails when one carries none or more than
one. The **Category** column below gives it without its suffix.

The PlayMode tests that derive from `PlayModeTestBase` load the *Maker Main* scene and drive the app
through its real UI. `PlayModeTestBase` redirects the data path to a temp directory per test, so
runs never touch your own twins — and since TWIN-460 the painted texture's PlayerPrefs key too
(`PaintIsolationPlayModeTests`).

Run them from the Unity Test Runner, with `unity cmd run_tests` (see `Maker/CLAUDE.md`), or from
the automation menu **Tools → Template PoC → …** (results are written to
`Temp/TemplatePoCResults.json`, see `Assets/Tests/Editor/TemplatePoCRunner.cs`).

## Base classes

| File | Purpose |
|------|---------|
| `PlayModeTestBase.cs` | Scene loading, temp data path, UI helpers (click by name/path, wait for mode, find list entries, `DragOnCanvas` for painting). |
| `NoAPICalls/TwinPaintTestBase.cs` | App flows shared by painting tests: load the LipEdema twin, select a view, choose the current group, paint with a marker, show/hide a group, assert parts are usable. |

## `NoAPICalls/` — app tests, no external services

| Test | Category | What it checks |
|------|----------|----------------|
| **SaveTwinPlayModeTests**<br>`SaveButton_OpensSaveMode` | P01 | Save screen opens, its buttons are present, a new twin can be created and appears in the twin list. |
| **InfoDisplayPlayModeTests** (5 tests) | P01 (2) · P02 · P03 · P07 | The status displays: the twin name in the header of every screen and the Twin/Version/Tool/Group block of the overview overlay — after a reset (the app falls back to `default.000`), after loading and after creating a twin, when a tool is picked and when the current group changes. The only class whose category sits on each test rather than on the class. |
| **LoadEveryTwinTests**<br>`EveryTwinInTheList_LoadsWithoutError` | P01 | Loads every twin in the list, one after the other, twice round — written for a `NullReferenceException` in `Model.LoadData` that only a switch through the whole list showed. |
| **TwinLoadingMemoryTests**<br>`LoadingTheSameTwinAgainAfterOthers_CostsNoMoreMemory` | P01 | Puts the app into the same state twice, with four other twins opened in between, and compares the memory retained — weighing every live texture, because the 8192×8192 body paint does not show up in `Texture.currentTextureMemory`. Catches growth per twin switch, which is what killed the app on an iPad Air; the absolute numbers belong to the editor. |
| **TwinActivityMemoryTests**<br>`WorkingInSeveralTwins_DoesNotKeepGrowingMemory` | P01 | The same with a stroke painted in each twin, over three rounds: noise wobbles, a leak rises, so each step is checked against the noise band and the whole climb against twice that. |
| **TextureSaveCostTests** (2 tests) | P01 | Breaks the cost of saving the painted texture down into its steps, and measures the same picture without the two steps that look unnecessary. **Asserts nothing** — the output is the point. |
| **TextureSaveReadbackTests**<br>`ReadingBackDirectly_IsCheaperAndGivesTheSameImage` | P01 | That reading the paint back directly (`PaintTextureSaver`, TWIN-459) costs less than the route through a `Texture2D` and gives the same image, compared pixel by pixel at sample points. |
| **UndoRedoPlayModeTests** (2 tests) | P02 | Undo/redo in the editing header, through the real buttons of the prefab: a click takes the last painted part out of its group, off the body (pixel comparison) and out of the save data, the next click brings it back at its place and repaints it, new paint afterwards ends the redo history. The second test pins the configuration that keeps the body texture from being copied per stroke — `undoRedo: None` on the body, `storeStates` off on every tool, no PaintIn3D undo button left — and that painting leaves no texture states behind (that copying got the app killed on the seventh stroke on an iPad Air). |
| **StickerPlayModeTests** (2 tests) | P02 | Sticker images per twin: the image behind a sticker slot — and the image its hash points at — follows the twin that is open, and an imported twin brings its own images for slots the open twin uses with different ones. |
| **PartTemplateServiceTests** (11 tests) | P02 | The Text→Part service (`Assets/Code/Proc/Paint/`): painting a body region into the active group, tool override (colour, metadata, sticker rejection), current-tool resolution with marker fallback, the region catalog, region names per language (enmed/demed/demedlatin), and the save format (linear file growth, no texture references, part↔group and texture links restored on load). |
| **ProgrammaticPaintingTests**<br>`PaintTemplateParts_ThreeRegions` | P02 | Regression test for driving the CW paint pipeline from code (marker stroke, filler area): parts get the right tool metadata, a stored view, and survive the save round trip. Foundation of the template library generator. |
| **GroupDetailPlayModeTests**<br>`PaintingPerGroup_ShowsOnePartPerSelectedGroup` | P03 | Paints one part into every group of the LipEdema twin (a different marker each) plus one into a newly created group; then on the group detail page selects one group at a time and verifies exactly that group's single part is listed. |
| **GroupPlayModeTests**<br>`HideAndShowGroup_KeepsItsParts` | P03 | Hiding and showing a group (overlay toggle) replays the visible groups; the hidden group keeps its parts, they stay bound to the paintable texture, and painting still works afterwards. |
| **GroupPlayModeTests**<br>`GroupList_ShowsPartCounts_AddsAndDeletesGroups` | P03 | Group list dialog: part count per group, adding a group by naming the empty entry (it becomes the current group and a fresh empty entry appears), deleting a group that is not the current one. |
| **GroupPlayModeTests**<br>`SaveAndReload_KeepsGroupsPartsAndTheirLinks` | P03 | Groups, their parts, part↔group links, visibility flags and the selected group survive leaving the twin and coming back. |
| **ViewPlayModeTests** (3 tests) | P04 | Stored views: a view is six numbers (the body's yaw and pitch, the camera's position, its zoom), so the tests compare numbers rather than pictures. A stored view comes back when it is activated, comes back after the twin was turned, and survives leaving and reopening the twin. Each test first asserts that the pose really changed, so a `select` that does nothing cannot pass. Screenshots go to `Application.temporaryCachePath/ViewShots/` for looking at. |
| **ShapePlayModeTests** (8 tests) | P04 | The Shape screen — the seventh tool of the Edit bar, and until TWIN-473 the only one with no test at all. That the screen opens with its six buttons; that each of the five still calls `Model` (the call is wired in the prefab's inspector, where a rename or a re-parent can drop it without a word — the button would still look right and do nothing, which is what this test is here to catch); that the four functions do what they say — the figure changes and, despite `overall_random`'s name, repeats itself, the face stays inside ±2, arms and hands toggle back and forth, Reset undoes all four; and that a changed figure survives save and reload. The face is not part of that last one: `ConfigData` has no field for the expressions, so it is never saved (see the open points in `PROCESS_LANDSCAPE.md`). |
| **TourProcessPlayModeTests**<br>`ExportStandardViews_WritesScreenshotPerView` | P04 | Opens a twin's Menu screen, clicks "Export standard views" (which only starts a coroutine, so the test ticks a few frames), and asserts one `screenshot_<profile> - <view>.png` per configured `StandardViewManager` view. |
| **DocumentPromptPlayModeTests** (3 tests) | P05 | The dynamic prompt for Document → Twin: the two editable Settings rows arrive with their shipped default **in full** (a character limit that is too small truncates it), and the assembled prompt carries the language directive, both rows, every group of the twin, the tools split into "has a meaning" and "still free", and all 98 region keys. Also that the tool inventory lists every marker and filler — including the first row of each panel, which the older report prompt skips. The prompt is written to `Application.temporaryCachePath/DocumentPrompt/` for reading. The third test walks Upload → pick → review screen (handing the pick over directly, since an OS dialog cannot be driven) and checks the screen names the file and carries the whole prompt. |
| **DocumentApplyPlayModeTests** (7 tests) | P05 | Applying a document mapping to the twin — the only step of Document → Twin that changes anything. Nothing ticked changes nothing; what *is* ticked lands exactly where it should (group created, free tool given its meaning **before** anything is painted with it, a two-region finding as two parts carrying the document's text, the patient text appended to the report without losing what was there, the user's current group put back, all of it surviving the save); and what the twin does not allow is refused per item without costing the rest — a tool that already means something keeps its meaning, an unknown tool or region drops only its own item, the same region twice stays one part. The fourth test drives the **review screen**: a row per proposal plus a heading per block, every row unticked *and looking unticked* (the tick's alpha is asserted — the row prefab came from the group list, where the tick sat inside the toggled graphic and stayed visible), the region count on the row that costs the parts, a long treatment line that has to **wrap** rather than clip (its row must be taller than a short one and tall enough for its own text), then Apply through the real button of the prefab — after which the applied rows come back ticked, dimmed and locked, and a second Apply writes nothing. Ticking a finding also ticks the group and the tool it needs, so the screen shows everything that would be written. The last two tests cover the group chip (the picker offers the twin's groups and the proposed ones, writing the choice back into the proposal without touching the twin, and the text below follows it) and the way back to a proposal already in hand (leaving the screen writes nothing, returning needs no upload, and a mapping read for another twin is refused). Screens are written to `Application.temporaryCachePath/UploadReview/` for looking at. |
| **UploadPlayModeTests** (2 tests) | P05 | The Upload button of the main screen opens the upload panel, which offers exactly two ways for a document to reach the twin — a photo and a file — each labelled from the localization table and wired to `DocumentUploadProcess` with its variant. The pick itself opens an OS dialog and is not driven. The second test covers the third entry, the way back to a proposal already in hand: it is configured but **not offered** before anything has been read, appears once a proposal exists, and disappears again for a twin the proposal was not read for. |
| **PartsScreenshotProcessPlayModeTests**<br>`ExecuteSync_WritesScreenshotForLinkedPart` | P05 | Paints a part on LipEdema, links it into the "Swell" group, runs `PartsScreenshotProcess.ExecuteSync` the way `VersionSequenceProcess` does (awaiting `ExecuteCompleted`), and asserts the `screenshot_<profile> - <group> - part <id>.png` file lands in the profile's data folder. |
| **MissingScreenshotsButtonPlayModeTests**<br>`CreateMissing_ShootsTheUnshotPartAndShowsItInTheList` | P05 | The "create missing images" button of the group detail panel (TWIN-466): it knows how many parts have no screenshot, shoots exactly those, and the list shows the picture afterwards. |
| **PartsDescriptionButtonPlayModeTests**<br>`Buttons_OfferOnlyThePartsThatCanActuallyBeDescribed` | P05 | The two description buttons of the group detail panel (TWIN-468) say what a press would do — the number on the label and whether the button is alive — for one part in three states: no image (nothing to offer), image but no text (the plain button offers it), image and text (only the forced button still offers it). Spends no token. |
| **PartsDescriptionPlayModeTests** (4 tests) | P05 | What "describe every part" may and may not touch, without spending a token: text that is already there is left alone, in the plain and in the forced variant; a part without a screenshot is never a candidate (the model is asked about the picture); described parts are skipped unless forced. The run used to stamp a placeholder into every description *before* asking, and that placeholder was what stayed when the asking did not happen. |
| **PartMenuPlayModeTests** (3 tests) | P05 | The three entries of the part detail page (TWIN-474) each have an action and a localized label — the Delete entry used to sit there with an empty `onClick`, and two labels were keys no table has. Delete really takes the part away and goes back to the body; taking the picture writes the screenshot **and** gives the screen back (the recorder hides the whole canvas, which used to strand the app on the bare body). |
| **SkinProcessPlayModeTests**<br>`ExportSkin_WritesSkinPngForSelectedProfile` | P05 | Opens a twin's Menu screen, clicks "Export skin", and asserts `skin_<profile>.png` is written to the profile's data folder. |
| **ImportTwinPlayModeTests** (10 tests) | P06 | Export/import of a twin: the paint is visible as soon as an imported twin is opened (it travels as `Texture.png` in the twin folder, because the in-app texture cache never leaves the device); groups, parts and their links survive the round trip; an import never overwrites a twin that is already there — it takes the id from its config when that is free and otherwise the first free `V01`, `V02`, … of exactly that id, and a returning twin keeps its suffix (`000V01` becomes `000V01V01`); the twin is named after its config, not after the zip file (which anything may rename); importing while a twin of the same name is open keeps both apart and leaves the open version marked in the list; a twin directory the app did not write (a copy, a second download) does not break the version list; and a broken archive leaves the existing twins untouched. |
| **SettingsUiPlayModeTests**<br>`SettingsButton_EnablesSettingsMode` | P07 | Settings screen opens. |
| **AutoSavePlayModeTests** (2 tests) | P07 | The twin is on disk before the app is taken away (TWIN-467): sending the app to the background writes the painted part to the file — iOS does not run `OnApplicationQuit` when it reclaims a backgrounded app — and the periodic save holds off while the user is still painting, since a save between two strokes would split one annotation into two parts. Both look at the file, not at a reload, because every route back into a twin saves on the way in. |
| **BusyOverlayPlayModeTests** (2 tests) | P07 | While a twin loads (about two seconds of blocked main thread) the user is told so, in a localized message, and taps are swallowed so an impatient second tap does not start the switch again (TWIN-461); when the panel goes, the twin really is loaded. |
| **ThinkingOverlayPlayModeTests** (4 tests) | P07 | The animated logo while the app asks the language model (TWIN-475): it sits where the loading message goes, plays its frames there and back again, is counted so a series of questions does not blink between them, and ends even when the request fails — a leaked count would leave the logo on screen for the rest of the session. |
| **PaintIsolationPlayModeTests** (2 tests) | T00 | A test run must not read or write the user's own paintings. The data directory is redirected, but the painted body texture lives in PlayerPrefs, one store per application; a twin switch used to drop the test prefix from the save name. Checks that the prefix survives a switch and that a whole round leaves the real keys untouched. |

`EditUiPlaymodeTests.cs` (class `EditUiPlayModeTests`) is the chain **K01** and listed under
[Chains](#chains).

### Explicit — for looking at, not for the suite

These carry `[Explicit]`, so "Run All" skips them; start them by name. They assert nothing.

| Test | Category | What it produces |
|------|----------|------------------|
| **BusyOverlayLookTests**<br>`ShowMeTheBusyPanel` | P07 | `Temp/busy-overlay-before.png` and `Temp/busy-overlay.png` — the loading panel, and the screen without it so the dimming can be judged. |
| **ThinkingOverlayLookTests**<br>`ShowMeTheThinkingLogo` | P07 | `Temp/thinking-logo.png`. |
| **GroupDetailButtonsLookTests**<br>`ShowMeTheGroupDetailButtons` | P05 | `Temp/group-detail-buttons.png` — whether the three stacked buttons of the group detail panel fit. |
| **SwitchDurationTests**<br>`HowLongDoesATwinSwitchTake` | T00 | A log line with the duration of five twin switches — whether loading is still slow enough to need feedback on screen. |
| **UndoResidueDiagnosticTests**<br>`WhatIsLeftOnTheBodyAfterUndo` | P02 | A log of what is left on the body after an undo, and whether waiting longer changes it. Marked temporary. |

## `APICalls/` — tests that call the real OpenAI API

> Since TWIN-455 this folder also holds `OpenAIClientTests` and `DocumentMappingApiTests` (which
> used to sit at the root of `PlayMode/`) and `PartsDescriptionProcessTests` (which used to sit in
> `NoAPICalls/` and really called the model whenever a key was configured). Every test here guards
> itself with `Assert.Ignore` when there is no key, so the folder is about **cost**, not failure.

All need a valid key in `Assets/Tests/Helper/testsecrets.json`; without one they skip themselves
(`Assert.Ignore`), with an invalid one they fail with a `401`.

| Test | Category | What it checks |
|------|----------|----------------|
| **PartDescriptionProcessTests**<br>`DescribePart_SetsDescriptionFromRealScreenshot` | P05 | Paints one part on LipEdema, produces a real screenshot via `PartsScreenshotProcess` (the same order `VersionSequenceProcess` uses — `PartDescriptionProcess` only calls the AI once a screenshot exists on disk), then calls `PartDescriptionProcess.Handle` for that single part and waits for a real, non-error description. |
| **PartsDescriptionProcessTests**<br>`DescribeParts_SetsDescriptionFromRealScreenshot` | P05 | Paints a part, produces a real screenshot via `PartsScreenshotProcess`, then calls `PartsDescriptionProcess.Handle`, which works through every part that has a screenshot, and waits for a real description. |
| **VersionProcessTests**<br>`DescribeVersion_SetsPromptResultFromRealPart` | P05 | Sets a part's description directly — skipping a second real AI call — so `partManager.AllPartsDescribed()` is true, clears the Version-level `ItemPrompt.promptResult`, calls `VersionProcess.Handle`, and waits for a real, non-error prompt result. |
| **DocumentMappingApiTests** (2 tests) | P05 | Send an invented document to the LLM. The first checks the answer can be applied: every body region is one of the 98 keys, every tool is a tool of this app, every group is existing or proposed, a tool taken into use was free, and what concerns the patient as a whole comes back as the patient text. The second drives the same thing **through the app**, with the real two-page PDF `Assets/Tests/Helper/lipoedema-report-sample.pdf` (a fictional lipoedema report): Upload, pick, upload, call, proposal on the review screen. It also proves the app finds a key, that the PDF upload path works — a text file takes a different one — and that a report full of symmetrical findings yields at least one multi-region painting. |
| **OpenAIClientTests** (9 tests) | T00 | The `OpenAIClient` class directly — no scene, derives from `TestBase` rather than `PlayModeTestBase`: simple and structured requests, invalid-key handling, an empty-key constructor throw, parallel requests, model listing, file upload, the AI component's image/PDF input path, and the full call `PartDescriptionProcess` makes, with a fixture screenshot. |
| **VersionSequenceProcessTests**<br>`RunSequence_DescribesPartAndVersionFromRealScreenshot` | K05 | Covers the "VersionSequenceProcess" GameObject's `SequenceProcess`, which chains `PartsScreenshotProcess` → `PartsDescriptionProcess` → `VersionProcess`. Paints a part with no screenshot and no description yet, calls `sequenceProcess.Handle`, and waits (60 s, for two real AI calls in a row) for a non-error part description and version prompt result. |

## Chains

| Test | Category | What it checks |
|------|----------|----------------|
| **EditUiPlayModeTests**<br>`EditButton_EnablesEditMode` | K01 | Core editing round trip: open a twin, enter Edit mode, all tool buttons present, select a view, paint with a marker, return to Main, open the group detail page and find the painted part in its group. |
| **VersionSequenceProcessTests** | K05 | See `APICalls/` above — it needs a key. |

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

**Directories a test writes into are created by the helper that writes**, on demand, and are never
deleted by a test. Creating one inside a single test makes every other test in the class depend on
the order they happen to run in — `UploadPlayModeTests` did that, passed on any machine that had
run it before, and failed on a fresh one until TWIN-447. Per-test scratch data belongs in `SetUp`,
which already wipes and recreates its own directory.


- **Painting needs a framed body.** The paint position is the screen centre; a twin's saved
  camera may point somewhere else (LipEdema's shows the lower body, where the centre falls
  between the legs and hits nothing). Select a view first — `TwinPaintTestBase.BodyView`.
- **A new part starts on a tool change**, not on a group change (known bug), so give each part
  its own marker when you need parts in different groups.
- **Painting details and gotchas** (single-frame strokes, fill tools, replaying commands):
  `NoAPICalls/CwPaintingTestGuide.md`.
