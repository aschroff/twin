# Feature: Document → Twin (map a document's findings onto the twin)

> **Status:** step 1 done — the way in (Upload button, photo or document, picker).
> Step 2 done — the dynamic prompt, and the review screen it will be shown on.
> Step 3 done — schema, call, and the parsed proposal on the screen; verified against the real API.
> The applying is open.

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

The proposal list (groups, tool meanings, paintings, each with a toggle, all unchecked to start)
goes above the text on this screen, with an Apply button; the text area then holds the
patient-level text. Nothing is written to the twin until Apply.

Two things worth keeping in mind about the panel:

- The text is a single `Text` with a `ContentSizeFitter` (`PreferredSize`) and
  `verticalOverflow = Overflow`, top-anchored, and it is the `ScrollRect`'s content. `Answer UI`
  instead has a fixed 1500-unit height and `Truncate`, which would have cut a 6.4 kB prompt off.
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
  findings across ~100 regions is ~1.8 MB of save file at ~17.5 KB per part. Decide whether a
  treatment belongs on the body at all, or whether the review step should collapse it.
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
  should reject an assignment for a tool that is already in use regardless.
- **The API key no longer lives on the `AI` component.** The one that did was committed into the
  scene and had gone invalid (`401`). The field is empty now and the key is resolved from outside
  version control by `Code.AI.ApiKeys` — in the editor from the `testsecrets.json` the tests
  already use. See `Assets/Code/AI/AI_INTEGRATION.md` §"The API key".

---

## Open — the analysis step

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
6. [ ] Apply the answer: create the new groups, claim the markers/fillers, paint the regions via
       `PartTemplateService.PaintRegion` — mind the save-file size rule from
       `FEATURE_TEXT_TO_PART.md` (parts per group)
7. [ ] Fill the review screen with the proposal, one toggle per item, and an Apply button
8. [ ] Tests with real documents (body chart, referral letter, hand drawing)

### Open questions

Decided: the whole file goes in one call; unmappable findings go into the patient-level text; the
document itself is not kept; review items start unchecked. Model: `gpt-5.5-2026-04-23`, which
needs a per-call model override — the other flows stay on the component's `gpt-4o-mini`.

Still open:
- How is a wrong mapping corrected — undo the whole import, or edit part by part?
- Does the region list stay affordable once the prompt also carries a long document?
