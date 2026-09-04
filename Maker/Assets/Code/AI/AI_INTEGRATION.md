# AI Integration — OpenAI calls and prompt building

> **Purpose:** reference for everything under `Assets/Code/AI/`. Companion document:
> `Assets/Code/Proc/PROCESSES.md` (who triggers these calls).

---

## 1. The layers

```
Process (Assets/Code/Proc/)        the app-side job: collect data, call the AI, write results back
   │
   ▼
AI            : MedicalAI         Unity glue: reads the prompt text from the UI, adds the
(Code.AI.AI)                      generated context, hands results to UI / PartData
   │
   ▼
MedicalAI     : AIService         medical response types + the coroutines that request them
   │
   ▼
AIService     : MonoBehaviour     apiKey / model / timeout, async→coroutine bridge, error routing
   │
   ▼
OpenAIClient  (plain class)       HTTP against https://api.openai.com/v1
```

`AI`, `MedicalAI` and `AIService` are one MonoBehaviour: the **`AI` component sits on the
`Process` root object**, the same GameObject as `ProcessManager`. Processes reach it through
`Process.getAI()`.

| Field | Where set | Current value |
|-------|-----------|---------------|
| `apiKey` | **leave empty** — resolved by `ApiKeys`, see below | empty |
| `model` | `AI` component in the scene | `gpt-5.5-2026-04-23` (all flows) |
| `timeout` | `AI` component in the scene | 120 s |

### The API key — `ApiKeys`

A key typed into the `AI` component is serialized into `Maker Main.unity` and committed with it;
that has happened, and cost a key. The field is empty now and `ApiKeys.OpenAi(...)` looks the key
up outside the scene, first hit wins:

1. the environment variable `OPENAI_API_KEY`
2. `secrets.json` in the persistent data path, next to the twins — the way to give a device a key
3. **editor only:** `Assets/Tests/Helper/testsecrets.json`, the file the tests already use and
   which `.gitignore` already covers, so a developer keeps exactly one copy
4. whatever the component carries — last resort, so an old scene still works

The json member is `openAIApiKey`, matched without regard to case; `apiKey` and `openai_api_key`
work too. `AIService.resolvedApiKey` / `.hasApiKey` say what was found; when nothing is found the
error message lists every place that was searched. Tests: `Assets/Tests/EditMode/ApiKeysTests.cs`.

The key that was committed is invalid, but it is still in the git history — the history would have
to be rewritten to remove it. The legacy `ChatGpt` component (AiToolbox) in the scene keeps its own
`parameters.apiKey`, which this does not touch.

Beware of stale defaults elsewhere: `OpenAIClientTests` pins `gpt-5.4-2026-03-05`. The value that
actually runs is the one on the component. There is also an unrelated legacy `ChatGpt` component
(AiToolbox) in the scene with its own `parameters.model` — not this pipeline.

---

## 2. OpenAIClient

Endpoint: **`POST /v1/responses`** (the Responses API), built by `BuildRequestPayload`:

```jsonc
{
  "model": "...",
  "input": [ { "role": "user", "content": [
      { "type": "input_text",  "text": "<prompt>" },
      // exactly one of:
      { "type": "input_file",  "file_id": "<from UploadFileAsync>" },   // documents
      { "type": "input_image", "image_url": "data:image/png;base64,…" } // images
  ] } ],
  "text": { "format": { /* json_schema, or {"type":"text"} */ } }
}
```

| Member | Does |
|--------|------|
| `RequestAsync(prompt, model, fileId, imagePath, structuredOutputType)` | one call, returns the **raw response JSON** as string |
| `RequestStructuredAsync<T>(prompt, model, fileId, imagePath, allowedValues)` | same, then finds the first output item that carries text (not `output[0]` — a reasoning model puts its reasoning first) and `JsonConvert.DeserializeObject<T>`. A refusal is reported as such. |
| `UploadFileAsync(path, purpose = "user_data")` | `POST /v1/files`, returns the file id for `input_file` |
| `ListAvailableModelsAsync()` | `GET /v1/models` |
| `OpenAIException` | carries the HTTP status code |

**Image vs. document.** An image path is embedded as base64 (`input_image`) and needs no
upload. Everything else (PDF) must go through `UploadFileAsync` first and is then referenced by
`fileId` (`input_file`). `BuildRequestPayload` takes `fileId` **or** `imagePath`, never both —
`fileId` wins. `OpenAIClientTests.AI_Component_Integration_PDF_vs_PNG` exercises both paths.

### 2.1 The schema — `JsonSchemaBuilder`

`JsonSchemaBuilder.Format(type, allowedValues)` builds what `text.format` needs, walking nested
classes and lists. Strict mode (which this always requests) demands of **every** object: all
properties in `required`, `additionalProperties: false`, and an `items` schema for every array —
anything missing is rejected before the model runs.

- Members are read the way Newtonsoft reads them: public properties and public fields, named by
  `[JsonProperty]` when present, `[JsonIgnore]` skipped.
- `allowedValues` injects a value list for a member whose options only exist at runtime, keyed by
  member path — `"paintings.regionKeys"` carries the 98 body regions, so an unknown region cannot
  come back. On an array member the list applies to its items.
- A self-referencing type throws past `MaxDepth` instead of hanging.
- Tests: `Assets/Tests/EditMode/JsonSchemaBuilderTests.cs`, which checks the strict-mode
  invariants recursively over a whole schema.

The old flat `GetSchemaForType` is gone. `Code.AI.StructuredOutputs.SchemaGenerator` is still a
second, unused copy of that flat logic (its `required` is always empty) — nothing calls it.

**Known limits — read before designing a new structured output:**

1. Every request and every raw response is `Debug.Log`ged in full — patient data in the console.

---

## 3. Structured responses in use

`MedicalAI` defines the response types and one coroutine each:

| Type | Fields | Requested by |
|------|--------|--------------|
| `InjuryDescriptionResponse` | `description`, `category` | `AnalyzeInjuryCoroutine` — one part |
| `PatientSummaryResponse` | `description`, `condition` | `GeneratePatientSummaryCoroutine` — one version |
| `DocumentMapping` | summary, patient text, new groups, tool meanings, paintings | `AI.MapDocument` — one uploaded document (`Assets/Code/Proc/Document/`) |

The first two are flat; `DocumentMapping` is nested and is why the schema builder exists. The
types in `StructuredOutputs.cs` (`MedicalFinding`, `MedicalFindingsList`, …) are examples;
nothing requests them.

`AI.MapDocument(prompt, imagePath, fileId, regionKeys, onSuccess, onError)` is the document call;
`AI.UploadDocumentCoroutine(path, …)` turns a file into the `fileId` it needs. `AIService` now
passes `fileId` and `allowedValues` through, and exposes `UploadFileAsync`.

---

## 4. Where the prompt text comes from

A prompt is assembled from **three** sources:

### 4.1 The instruction — user editable, per twin

`ItemPrompt` components live in **Settings → Prompts** (`Canvas/Settings UI/SettingsPanel/Prompts/Panel/…`).
Each has a `label` (e.g. `Part Description`, `Medical Report`), a `level`
(`Part` / `Version`), and an `InputField` holding the text. `AI.GetPromptOfLabel(label, level)`
→ `SettingsManager.getPromptObject` finds it by label + level; the text is persisted **per twin**
in `ConfigData.prompts`, keyed by the row's visible `Label` text. Results of `Version`-level
prompts are stored back into `ConfigData.resultsVersion` (`ItemPrompt.promptResult`).

Current entries:

| label | level | note |
|-------|-------|------|
| `Part Description` | Part | describe one part |
| `Medical Report` | Part | describe one part, report style |
| `Medical Report` | Version | aggregate the version into a report |
| `Medical Report` | Version | ×2 more, on the GameObjects `Meshcapade User` / `Meshcapade Password` — leftovers that duplicate the label/level pair; `getPromptObject` returns whichever comes first in the hierarchy |
| `Document Mapping` | Document | the task, for mapping a document onto the twin |
| `Document Rules` | Document | the mapping policy for the same |

**Shipped defaults.** `ItemPrompt.defaultKey` names a `TwinLocalTables` key (`prompt.*`); when the
twin has no text stored, the field takes that text in the language of the app. Before this existed
the field was blanked instead, so *every new twin started with empty prompts*. Set for the two
Document rows and backfilled for `Part Description`, `Medical Report (Part level)` and
`Medical Report`.

The row prefab is `Assets/Prefabs/Label and InputField.prefab` (used by the Settings prompts only);
its `characterLimit` is 2000 — it was 400, which silently truncated a shipped default.

Gotcha: `getPromptObject` falls back to a `Default` entry, and **no `Default` entry exists**, so
an unknown label returns `null` and `GetPromptOfLabel` throws. A new caller must ship its own row.

Worse gotcha: `getPromptObject` matches on `label` + `level`, and **three rows share
`Medical Report`/`Version`** — the real one plus two leftovers on the `Meshcapade User` /
`Meshcapade Password` objects. It returns whichever comes first in the hierarchy, which is not
necessarily the row the user edits in Settings. `getPromptObjectByLabelText(labelText, level)`
matches on the row's **visible Label** instead (`ItemPrompt.LabelText()`, also the key the row is
persisted under), which is unambiguous; the document applier uses it to find the report row. The
version-report flow (`AI.DescribeVersion`) still uses the ambiguous lookup — see the open question
in `Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md`. Deleting the two leftover rows would
settle it.

### 4.2 The twin context — generated from the scene

`PromptGeneration/PromptContributor.GeneratePrompt(AI.Help)` finds every MonoBehaviour in the
scene implementing `IRoot` and concatenates its `Chapter(help)`:

| `IRoot` | Lives on | Emits |
|---------|----------|-------|
| `Markers` | `Canvas/EditMarker UI/Bottom/Scroll/Panel` | "…list of meaning for lines (not surfaces)…" + one line per marker |
| `Fillers` | `Canvas/EditFiller UI/Bottom/Scroll/Panel` | the same for surfaces |

Both derive from `Tools` and use `ContributeFromChildren`, which asks every child implementing
`IPromptContributingGameObject` (that is `Marker`) for one line:

```
Yellow (RGB: 1, 1, 0) has the follwing meaning: <the meaning the user typed>
```

`Marker.Contribute` gets the colour from the tool the row isolates
(`CwDemoButton.IsolateTarget` → `CwPaintSphere.Color`) and the meaning from
`GetMarkerText()`.

**A tool row** is one GameObject per colour under those panels, carrying:

| Component | Role |
|-----------|------|
| `CwDemoButton` | `IsolateTarget` → the actual tool GameObject under the `Tools` container |
| `Item` (in `ItemText.cs`) | `id` (GUID) + the row's `InputField` — **the meaning, persisted per twin** in `ConfigData.itemTexts[id]` |
| `Marker` | the prompt contributor |

So "which tools exist and what do they mean" is: the rows of those two panels, meaning =
`Item`'s `InputField.text`, empty = **not yet given a meaning** (`Item.defaultValue` is `""`).

Gotchas found while writing this:
- `Tools.ContributeFromChildren` loops `for (i = childCount - 1; i >= 1; i--)` — **child 0 is
  skipped**, so the first marker and the first filler never reach the prompt (today: `Red`,
  `Red Filling`).
- `Marker.GetMarkerText()` reads the first enabled `Text` in the children, not the `InputField`.
  That label is only synced while the row has been active, so an unopened panel contributes the
  stale prefab text. Reading `Item`'s `InputField.text` is the reliable route.
- **Both are avoided by `ToolInventory`** (see §4.4); the two classes above still have them.
- `PromptPart` implements `IRoot` but is not a MonoBehaviour, so it is never found — dead code.
- Sticker and text tools have no contributor; only markers and fillers describe themselves.

### 4.4 The inventories — the same data, without the gotchas

For prompts that describe the *state* of the twin rather than one finding:

| Class | Returns |
|-------|---------|
| `ToolInventory.All()` / `.InUse()` / `.Free()` | every marker and filler as `ToolInfo { name, kind, color, meaning, inUse }`. `name` is the **tool GameObject** name — what `PartTemplateService.PaintRegion` takes. The meaning comes from the row's `Item` InputField, and every child of the panel is walked. |
| `GroupInventory.All(partManager)` | the twin's groups as `GroupInfo { name, partCount, meanings }` |

`PartManager.DeriveType` is `public static` so both this and `PartManager` classify a tool the
same way. First user: `DocumentPromptBuilder`
(`Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md`).

### 4.3 The findings — from the data model

`PromptGeneration/Part.Description(PartData)` turns one part into prose, branching on
`PartData.typeTool` (`MarkerLine`, `MarkerDotted`, `Filler`, `Sticker`, `Text`) and appending
`"The finding belongs to the category: <group name>."`. `AI.DescribeVersion` instead
concatenates the already-computed `part.description` of every part of every group.

---

## 5. The two flows that exist today

**Part → text** (`PartDescriptionProcess` → `AI.DescribePart`)

```
prompt = ItemPrompt(variant, Part)          // user's instruction
       + Part.Description(part)             // tool type, colour, meaning, group
image  = part.pathScreenshot                // screenshot of the part on the mesh
      → AnalyzeInjuryCoroutine → InjuryDescriptionResponse
      → part.description = response.Description
```

**Version → report** (`VersionProcess` → `AI.DescribeVersion`)

```
prompt = ItemPrompt(variant, Version) + every part.description, numbered
      → GeneratePatientSummaryCoroutine → PatientSummaryResponse
      → ItemPrompt.promptResult (→ ConfigData.resultsVersion)
```

**Whole body** (`CompleteReportProcess` → `AI.CompleteReport`) is the only caller of
`PromptContributor.GeneratePrompt`: a hard-coded instruction plus the marker/filler legend,
with a whole-body screenshot.

---

## 6. Body regions as prompt material

For the reverse direction (text/document → painted part) the vocabulary the LLM may choose from
is the body-region library: `PartTemplateService.GetTemplateCatalog()` /
`GetTemplateCatalogJson()` return every template twin with its regions (`key` +
localized `displayName`), 98 keys, catalog in `Assets/Resources/BODY_REGIONS.md`. See
`Assets/Code/Proc/Paint/FEATURE_TEXT_TO_PART.md`.
