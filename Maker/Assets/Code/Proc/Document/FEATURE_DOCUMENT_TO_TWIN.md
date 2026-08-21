# Feature: Document → Twin (map a document's findings onto the twin)

> **Status:** the whole path works — pick, prompt, call, review, apply.
> Step 1: the way in (Upload button, photo or document, picker).
> Step 2: the dynamic prompt, and the review screen it is shown on.
> Step 3: schema, call, the parsed proposal on the screen; verified against the real API.
> Steps 6 + 7: the review list with a toggle per proposal, and the applier behind Apply.
> Open: tests with real documents of other kinds, and the region-library gaps below.

---

## Goal

Somebody photographs or uploads a document — a referral letter, a hand drawing, a photo of a
body chart, a report — and the app maps the information in it onto the twin: groups, tools and
painted body regions. It is the sibling of **Text → Part**
(`Assets/Code/Proc/Paint/FEATURE_TEXT_TO_PART.md`), with a document instead of a typed
description as the input, and it reuses that feature's region library for the painting.

---

## Step 1 — the way in (done)

| Piece | Where |
|-------|-------|
| `Upload` button, 4th in the bottom row of the main screen | in **`Assets/Prefabs/GUI/Main UI.prefab`** under `Bottom`, a nested `Icon in circle with text` instance like its three neighbours. Only the click target is a scene override — `Maker`/`InteractionController` cannot be referenced from a prefab asset — as it is for `HelpMe` and `NewVersion`. |
| `Upload` mode | `UploadMode` (`Assets/Code/View/Mode/UploadMode.cs`), registered in `InteractionController.interactionModes` |
| `Upload` panel: the action list with the two ways in | `Assets/Prefabs/GUI/Upload UI.prefab` (a copy of `Menu UI`), registered in `UIController.uiPanels`; the two entries are wired on the scene instance's `MenuManager`, as on `Menu UI` |
| The picking | `DocumentUploadProcess` (`Assets/Code/Proc/Document/`), a `Process` under the scene's `Process` object, variants `Photo` and `Document` |
| Labels | `TwinLocalTables`: `_UPLOAD_`, `UPLOAD_PHOTO`, `UPLOAD_DOCUMENT` (en, enmed, de, demed, demedlatin) |
| Test | `Assets/Tests/PlayMode/NoAPICalls/UploadPlayModeTests.cs`, **Tools → Template PoC → Run Upload Tests** |

The pickers are deliberately the ones the user already knows from elsewhere:
`NativeGallery.GetImageFromGallery` as in the sticker upload (`Sticker.SelectTexture`), and
`NativeFilePicker.PickFile` as in the twin import (`FileDataHandler.PickFileAsync`). A photo is
loaded to `pickedPhoto`, a document only recorded as `pickedPath`; both are the input of the
analysis step. After a successful pick the twin is shown again and a notification names the file.

Note: the bottom row's grid spacing went from 95 to 70 (in the prefab) so a fourth button still
fits the portrait width (4 × 80 + 3 × 70 = 530 within the ~593 the canvas has on a phone).

The panels are prefabs, so **UI is added to the prefab, never to the scene instance** — the scene
carries only what has to point at scene objects. `Canvas` itself is not a prefab, which is why
`Upload UI` is a child of it in the scene.

---

## Step 2 — the dynamic prompt (done)

`DocumentPromptBuilder.Build(partManager, settingsManager)` returns the prompt. Six sections, in
this order:

| # | Section | Comes from |
|---|---------|-----------|
| 1 | `Write every text you produce in <language>.` | the app's locale family: `en`/`enmed` → English, `de`/`demed`/`demedlatin` → German. First, so an instruction the user writes into a row below overrides it |
| 2 | the task | `ItemPrompt` row `Document Mapping`, level `Document` |
| 3 | the rules | `ItemPrompt` row `Document Rules`, level `Document` |
| 4 | existing groups + what they hold so far | `GroupInventory.All(partManager)` |
| 5 | tools with a meaning / tools still free | `ToolInventory.All()` |
| 6 | the body regions, `key (localized name)` per area | `PartTemplateService.GetTemplateCatalog()` |

About 6.4 kB for a twin with 24 tools and 98 regions. The document itself is not in the string —
it travels as an image or as an uploaded file beside it.

### The two editable rows

They live in `Assets/Prefabs/GUI/Settings UI.prefab` under `Prompts/Panel`, the same rows the
other prompts use, and are stored per twin in `ConfigData.prompts` keyed by their `Label` text.
Splitting task from rules lets the policy be retuned without rewriting the task.

**Shipped defaults.** `ItemPrompt` used to blank its field whenever a twin had nothing stored, so
every new twin started with empty prompts — the *existing* prompts included. It now takes a
`defaultKey`, a `TwinLocalTables` key (`prompt.*`), and falls back to that text in the language of
the app. Filled in for the two new rows and backfilled for `Part Description`,
`Medical Report (Part level)` and `Medical Report`.

The input field's `characterLimit` was 400 and silently cut the shipped rules text in half. The
row prefab (`Assets/Prefabs/Label and InputField.prefab`, used by the Settings prompts only) now
allows 2000. The test asserts the default arrives *in full*, so a limit that is too small fails
loudly instead of shortening a rule.

### `ToolInventory` / `GroupInventory`

`Assets/Code/AI/PromptGeneration/`. A tool row's meaning is read from its `Item`'s InputField —
the authoritative value, persisted per twin in `ConfigData.itemTexts`. Deliberately not from the
row's `Text` labels the way `Marker.GetMarkerText()` does: those are only synced once their panel
has been open, so an unvisited panel reports the text the prefab shipped with. `ToolInventory`
also walks *every* child, unlike `Tools.ContributeFromChildren`, which starts at index 1 and
therefore skips the first marker and the first filler. `PartManager.DeriveType` became
`public static` so both classify a tool the same way.

### Test

`Assets/Tests/PlayMode/NoAPICalls/DocumentPromptPlayModeTests.cs`,
**Tools → Template PoC → Run Document Prompt Tests**. Builds the prompt against the LipEdema twin
(which ships a meaning for all 24 tools, so the test frees one to cover the "still free" half) and
checks the language line, both rows in full, every group, the tool split, and all 98 region keys.
It writes the assembled prompt to `Application.temporaryCachePath/DocumentPrompt/` for reading —
the wording is meant to be reviewed by a human, not asserted word by word.

---

## The review screen

`UploadReview UI` (`Assets/Prefabs/GUI/UploadReview UI.prefab`, a copy of `Answer UI`), mode
`UploadReview`, filled by `DocumentReviewManager` (`Assets/Code/View/Manager/`). A pick opens it:
`DocumentUploadProcess.Accept()` builds the prompt and shows it there.

This is where the user will confirm what may reach the twin, so it is where the picked document
goes. Until the analysis exists it shows what was picked and the prompt that would be sent —
which makes the prompt reviewable on the device, against a real twin with its own groups and tool
meanings. `DocumentUploadProcess.ShowPicked(path, photo)` is the same entry without an OS dialog,
which is how the test drives it.

### The list (done)

The screen is two things. **Above:** a row per proposal, each with a toggle, all unticked — that
is the gate. **Below:** the whole proposal as text (`DocumentMappingText.Describe`), which is
where the detail lives: the regions of a finding, the reasons, the patient text in full. The rows
are for deciding, the text is for reading.

| Piece | Where |
|-------|-------|
| One row | `DocumentReviewRow` (`Assets/Code/View/Item/`) — an `ItemKind` (`Painting`/`Group`/`Tool`/`PatientText`/`Heading`) plus the index in the matching list of the `DocumentMapping`, so reading the rows back gives a `DocumentMappingSelection` |
| The row prefab | `Assets/Prefabs/GUI/UploadReview Row.prefab` — a copy of `Scroll readonly text and toggle` (the group-list row) with its `Group` component and its `InputField` Button removed and `DocumentReviewRow` added |
| Building and reading the list | `DocumentReviewManager.Show(mapping, body, apply)` / `.Selection()` / `.HandleApply()` |
| Apply | bottom centre of the panel, label from `TwinLocalTables` key `UPLOAD_APPLY`, `onClick` → `DocumentReviewManager.HandleApply` (wired inside the prefab), hidden while there is no mapping |

The scroll content is now a column: `Scroll Answer/Viewport/Content` (`VerticalLayoutGroup` +
`ContentSizeFitter`) holding `Proposals` (where the rows are instantiated) and `Text Overview`.
`Text Overview` lost its own `ContentSizeFitter` — its height comes from the column now, and two
fitters would fight over it.

A row reads `<finding>  -  <tool>, <group>, <n> regions` (`DocumentMappingText.Row`). **The region
count is on the row on purpose**: it is what ticking the row costs. A treatment line over both
legs is one row and fourteen parts, and this is where that becomes visible before it is paid — see
"treatments" below. `, uncertain` is appended when the model's own confidence is below 0.6.

Headings carry no toggle (their `Selector` is hidden) and are English, like the rest of the text on
this screen; the *content* is in the language of the app, because the prompt asks for it.

`Show(string)` is the text-only form — the prompt, the progress line, an error — and hides the list
and the Apply button.

Two things worth keeping in mind about the panel:

- The Back Button copied from `Answer UI` did nothing: its onClick is a **static** call with a
  null `m_Target` and `m_TargetAssemblyTypeName: "InteractionController, Assembly-CSharp"`, and
  `InteractionController` lives in `Maker.Runtime`, so UnityEvent cannot find the method. It is
  now wired like `Menu UI`'s Back Button, with the `InteractionController` on `Maker` as target.
  `Answer UI` still carries the broken form — its own back button cannot work either.

---

## Step 3 — the call (done)

| Piece | Where |
|-------|-------|
| Recursive JSON schema, strict mode, injected value lists | `Code.AI.JsonSchemaBuilder` — replaces the flat generator in `OpenAIClient`; see `Assets/Code/AI/AI_INTEGRATION.md` §2.1 |
| The answer | `DocumentMapping` + `ProposedGroup` / `ProposedToolMeaning` / `ProposedPainting` (this folder) |
| The call | `AI.MapDocument(...)`, with `AI.UploadDocumentCoroutine` for anything that is not an image |
| The flow | `DocumentUploadProcess.Accept()` → prompt → upload if needed → `MapDocument` → `DocumentMappingText.Describe` on the review screen |
| Model | `gpt-5.5-2026-04-23`, pinned on the `AI` component — **all** flows moved to it, not just this one |
| Notifications | two toasts: "Reading <file> ..." when the request goes out and "<file>: n findings ... to review" (or the failure) when it comes back — so leaving the review screen while it works is safe |
| Tests | `Assets/Tests/EditMode/` (14, no scene, no network) and `Assets/Tests/PlayMode/DocumentMappingApiTests.cs` (2, call the API, need `testsecrets.json`) — the second of those drives the whole flow through the app |

The 98 region keys go into the schema as an `enum` on `paintings.regionKeys`, so an unknown
region cannot come back — and the API test additionally checks every tool and group name against
what the app knows, because those are not enums.

`DocumentUploadProcess.lastPrompt` / `.lastMapping` hold what went out and what came back; they
are the input of the step that writes the confirmed items to the twin. Nothing is persisted.
`ShowPromptFor(path, photo)` puts the prompt on the screen **without** sending anything — how to
read on the device what would go out, and how the offline test checks the screen.

### What a realistic report produces

`Assets/Tests/Helper/lipoedema-report-sample.pdf` is a fictional two-page lipoedema report written
against the LipEdema twin's own tool meanings, for hand testing and for the API test. Against that
twin it comes back with 24 paintings, one new group ("Skin changes"), no tool reassignments, and a
long patient text — all names valid. Worth knowing before the applier is built:

- **Treatments come back as body paintings, and they are broad.** "Compression garments for both
  legs" mapped to 14 regions, and six treatment lines did the same. That is defensible — the dotted
  tools *are* the treatments in this twin — but one line of a plan then becomes 14 parts. 24
  findings across ~100 regions is ~1.8 MB of save file at ~17.5 KB per part.
  **Decided (2026-08-21): the review row carries the cost.** A treatment stays on the body — that
  is what the twin's dotted tools mean — but every proposal is one row with its region count on it
  and starts unticked, so 14 parts are only paid when someone ticks a row that says "14 regions".
  No filter in the prompt, none in the applier: the user decides per import. The alternatives were
  keeping treatments off the body (loses the twin's own convention) and merging a finding's regions
  into a single part (needs new code in the paint layer, and the part then has no single
  `regionKey`) — both rejected.
- **The region library has no medial or lateral thigh.** The model said so itself: "outer thigh
  approximated using available thigh regions", "inner aspect approximated using front thigh
  regions". Candidates for `BODY_REGIONS.md` if inner/outer matters clinically.
- **A healed scar had no tool.** The model noted "no exact existing tool meaning fits a healed
  scar" and put it in the patient text rather than forcing a wrong tool — the right behaviour, and
  a good argument for keeping a tool or two free.
- **Restraint held.** The report says the feet, toes, forearms and hands are spared; none of them
  were painted, and the negative Stemmer sign made it into the patient text.

### Two things learned from the first real answer

- **The `toolAssignments` rule had to be spelled out.** Asked to map a twin where every tool
  already carries a meaning, the model filled that list with the three tools it *used*, restating
  their existing meanings. The rules default now says a tool belongs in that list only when it was
  taken from the free list, and that the list stays empty when nothing was free. The applier
  rejects an assignment for a tool that is already in use regardless — it reports the meaning it
  kept instead of writing the proposed one.
- **The API key no longer lives on the `AI` component.** The one that did was committed into the
  scene and had gone invalid (`401`). The field is empty now and the key is resolved from outside
  version control by `Code.AI.ApiKeys` — in the editor from the `testsecrets.json` the tests
  already use. See `Assets/Code/AI/AI_INTEGRATION.md` §"The API key".

---

## Steps 6 + 7 — applying it (done)

`DocumentMappingApplier.Apply(mapping, selection, partManager, settingsManager)` — a plain static
class, so a test drives it without going through the screen. It is **the only place in this feature
that changes anything**: pick, prompt, call and review all leave the twin untouched.

| Piece | Where |
|-------|-------|
| The applier | `DocumentMappingApplier` (this folder) |
| What the user ticked | `DocumentMappingSelection` — the indices per list, plus the patient text. `Nothing()` / `Everything(mapping)` |
| What it did | `DocumentApplyResult` — created groups, claimed tools, findings and parts painted, whether the report grew, and `problems` (everything refused, in the user's words) plus `Summary()` for the toast |
| The button behind it | `DocumentReviewManager.HandleApply` → `DocumentUploadProcess.ApplyConfirmed(selection)` |
| Region key → template twin | `PartTemplateService.TwinOfRegion` / `PaintRegionByKey` — the answer only ever names a key, so the app resolves the area itself |
| The report row | `SettingsManager.getPromptObjectByLabelText("Medical Report", Version)` + `ItemPrompt.LabelText()` |
| Tests | `Assets/Tests/PlayMode/NoAPICalls/DocumentApplyPlayModeTests.cs` (4), **Tools → Template PoC → Run Document Apply Tests** |

**The order is load bearing.** Groups, then tool meanings, then the paintings, then the report text.
A part copies the tool's meaning at the moment it is painted, so a tool that is being taken into use
has to get its meaning *before* anything is painted with it. The test asserts exactly that (the
chest part must carry `healed scar`, not `Yellow`).

What it refuses, per item, without giving up the rest of the run:

- a tool that **already carries a meaning** keeps it — the answer is a proposal, and the meanings are
  the user's vocabulary for this twin. The refusal names the meaning that was kept.
- a tool or a region **the app does not know** costs only its own item; the other regions of the same
  finding are still painted. The same region twice in one finding becomes one part, not two.
- a finding with **no region or no tool** is skipped.

Two decisions worth knowing:

- **A ticked finding gets its group even when the group's own row was left unticked.** A confirmed
  finding has to live somewhere, and the review screen offers no way to say where else. So the group
  toggle really only decides about groups that no ticked finding needs. Reported as created either
  way.
- **The part's description becomes the finding's text**, not the region name (`PaintRegion`'s
  default). That is what the group detail page shows and what the version report is built from;
  the region stays readable through `PartData.regionKey`.

Afterwards: the user's current group is put back (painting moves it), the group overlay is rebuilt —
which is also what wires `GroupData.group` for a group created here — the twin is saved, and the app
returns to the main screen so the result is looked at on the body.

### A trap in the row prefab: the tick that was always on

`Scroll readonly text and toggle` is the group-list row, where the toggle means *visible*. Its
`Checkmark` sits **inside** the `Foreground` image that the `Toggle` fades — and Unity fades only
that graphic's own `CanvasRenderer`, not its children's. So the square around the tick was hidden
and the tick itself stayed at full alpha: every row looked ticked while `Selection()` correctly
said none was. The row now has the tick as the `Toggle`'s `graphic` directly, with the empty box as
`targetGraphic`, and the test asserts the tick's alpha — a tick that stops following the toggle
fails loudly instead of quietly inviting an Apply nobody meant.

### A bug found on the way: painted parts carried no meaning

`PartTemplateService.ExtractMeaning` read the tool's meaning through `ToolTracker.myButton`, and
**`ToolTracker` wires that field in `OnEnable`** — so it is null for every tool that is not the one
the user currently has selected, which is every tool a programmatic paint uses. Every part painted
from a template silently got the fallback (the colour name) as its `meaning`. It now finds the
tool's row through its `CwDemoButton` and reads that row's InputField, the way `ToolInventory` does.
The 11 `PartTemplateServiceTests` still pass; the one that touched this only asserted the meaning
was *non-empty*, which the fallback satisfied.

---

## Open — background on the design

### What the LLM has to be told

- The document (image, or the PDF as a file/converted pages)
- **Existing groups** of the current version (`PartManager.groups` → name + what is in them)
- **Existing tools with their meaning** — markers, fillers, stickers, text; the prompt code for
  this exists for the Part → Text direction (`Assets/Code/AI/PromptGeneration/`:
  `Tools.cs`, `Markers.cs`, `Marker.cs`, `Fillers.cs`, `PromptContributor.cs`)
- **Which markers and fillers are still unused**, so the LLM can give one a new meaning
- **The body regions** it may paint: `PartTemplateService.GetTemplateCatalogJson()` (98 keys,
  catalog in `Assets/Resources/BODY_REGIONS.md`)

### What the LLM has to deliver (target structure)

- **New groups** that are needed — existing groups are to be reused wherever they fit
- **Markers/fillers to take into use** for meanings that no existing tool covers; whether a
  marker or a filler fits better is the LLM's call
- **The paintings**: per finding a tool, one or more body regions, and the group
- **One patient-level text** for findings that no body region can carry and for statements about
  the patient as a whole. It is appended to the version report field
  (`ItemPrompt.promptResult` → `ConfigData.resultsVersion`) of the row labelled
  `Medical Report` — appended, so an existing report survives. Note that three rows share the
  label/level pair `Medical Report`/`Version`, so the row has to be picked by its `Label` text.

A finding that maps to a single body region uses that region; a finding may need several
(left and right chest, for example), so the region field is a list.

The tool is named by its **tool GameObject name** (`Cyan`, `Red Filling`), which is what
`PartTemplateService.PaintRegion` takes. The answer names only region **keys**; the app resolves
which area twin a key belongs to, so that pairing cannot go wrong. The keys should be a schema
`enum`, which makes an invalid region impossible rather than merely unlikely.

### Steps

1. [x] The way in: Upload button, photo or document, picker
2. [x] The dynamic prompt (see above)
3. [x] Turn a picked PDF into what the API accepts (the AI component already takes PDF input —
       see the `OpenAIClientTests` PDF case) and a photo into the base64 image the existing
       calls use
4. [x] Build the request: twin context (groups, tools, free markers/fillers, region catalog)
       plus the document, with the target structure as a structured output schema
       (`Assets/Code/AI/StructuredOutputs.cs`)
5. [x] The review screen (shows the prompt until there is an answer to show)
6. [x] Apply the answer: create the new groups, claim the markers/fillers, paint the regions via
       `PartTemplateService.PaintRegion` — mind the save-file size rule from
       `FEATURE_TEXT_TO_PART.md` (parts per group)
7. [x] Fill the review screen with the proposal, one toggle per item, and an Apply button
8. [ ] Tests with real documents (body chart, referral letter, hand drawing) — only the fictional
       lipoedema PDF has been through the whole path so far

### Open questions

Decided: the whole file goes in one call; unmappable findings go into the patient-level text; the
document itself is not kept; review items start unchecked; a treatment stays on the body and the
review row carries its region count (see "treatments" above). Model: `gpt-5.5-2026-04-23`, which
needs a per-call model override — the other flows stay on the component's `gpt-4o-mini`.

Still open:
- How is a wrong mapping corrected — undo the whole import, or edit part by part? Nothing exists for
  this yet: Apply saves straight away, so the only route back today is not applying in the first
  place.
- Does the region list stay affordable once the prompt also carries a long document?
- **Which `Medical Report`/`Version` row does the *report* flow write to?** The applier picks the row
  by its visible Label, deliberately. `AI.DescribeVersion` still goes through
  `SettingsManager.getPromptObject`, which returns whichever of the three rows comes first in the
  hierarchy — possibly one of the Meshcapade leftovers. If it is not the same row, a document's
  patient text and a generated report end up in different places. The two leftover rows should go.
- **Applying ~100 regions loads a template twin per region.** `PartTemplateService.PaintRegion`
  parses the whole `commandDetails` of the area twin on every call, and it may not be cached: the
  cloned parts are *adopted* from that parse, so a shared parse would hand the same `PartData`
  objects out twice. A batch form (one parse, several regions, keys deduped) is the way out if a
  big import turns out to be slow.
- The review rows and the text below them are English; only the model's own text follows the app's
  language. `DocumentMappingText` would need the localization table for that.
