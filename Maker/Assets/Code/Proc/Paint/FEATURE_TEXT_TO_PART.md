# Feature: Text → Part (AI-generated Parts from textual descriptions)

> **Status:** In progress — template creation PoC done (see §"Template Creation" below).
> A PlayMode test (`Assets/Tests/PlayMode/NoAPICalls/ProgrammaticPaintingTests.cs`) paints named
> region template parts programmatically through the real CW paint pipeline.

---

## Goal

Allow the AI to *create* parts from textual descriptions – the reverse of the existing Part → Text flow. A user provides a medical description, and the system generates a visual annotation (Part) on the mesh.

---

## Data Model Deep-Dive

A **Part** in the persisted JSON (`commandDetails`) consists of:
- **Metadata:** `id` (GUID), `typeTool`, `nameTool`, `colorTool` (RGBA), `guidTool`, `meaning`, `textTool`, `description`, `group` reference, `view` (camera snapshot), `pathScreenshot`
- **`partCommands`:** A list of Command references, each with an `id` (GUID) and a `data` block referencing a `PaintableTexture` and a `LocalCommand` (by `rid`)

Each `rid` resolves to a serialised **`CwCommandSphere`** (class in `PaintIn3D` namespace, inherits `PaintCore.CwCommand`). A single `CwCommandSphere` contains:
- `Position` / `EndPosition` – 3D world-space coordinates on the mesh surface
- `Matrix` – 4×4 orientation matrix (derived from surface normal + camera direction at paint time)
- `Color` (RGBA), `Opacity`, `Hardness` – brush appearance
- `Material` hash, `Model` hash – which shader and which mesh
- `Blend` – blend mode settings
- `Extrusions` – 0 = isolated dot, 1 = connected to previous point (stroke)
- `Clip`, `Tile*`, `Mask*`, `DepthMask` – advanced painting parameters

**Key insight:** A brush stroke is NOT a simple list of UV coordinates. It is a sequence of 3D sphere positions with full orientation matrices. These are normally produced internally by PaintIn3D when the user touches the mesh (Raycast → hit point → normal → matrix).

### Groups in the data model
```
groups: [ { id, name, visible, selected, groupParts: [ Part, Part, … ] } ]
```
Each Part is nested inside its Group with full Command data.

---

## Chosen Approach: Pre-painted Part Templates ("Part Library")

Instead of programmatically generating CwCommandSphere data, we use a library of manually painted template parts.

### 1. Template Creation (automated — PoC proven)

> **Refined constraint:** Template data cannot be *synthesised* by hand or by code — the valid
> `CwCommandSphere` position/matrix data only comes out of the real paint pipeline
> (raycast → hit point → normal → matrix). However, the pipeline itself CAN be driven
> programmatically: a PlayMode test simulates fingers on the active `CwHitScreen`
> (`GetFinger` → `HandleFingerUpdate`), which runs the identical raycast/paint code a human
> touch would. The resulting command data is indistinguishable from hand-painted data.

**Proven by `ProgrammaticPaintingTests.PaintTemplateParts_ThreeRegions`** (2026-07):
- Creates a fresh twin `Templates`, a group `Templates`, and paints three named regions:
  `left_forearm_anterior` (marker stroke), `abdomen_central` (filler, 300 grid-fill commands),
  `head_forehead` (marker stroke)
- **Aiming:** region anchor points are derived from SMPL-X bone transforms (`left_elbow`,
  `left_wrist`, `pelvis`, `spine2`, `head`, …) projected into screen space via
  `Camera.main.WorldToScreenPoint`; the paint ray through that pixel hits the camera-facing
  surface (front view ⇒ anterior side)
- **Region name storage:** `PartData.description` (the free-text field the user edits in the
  Part detail UI; `meaning`/`nameTool` are overwritten from the active tool by
  `StoreCurrentPartInformation`)
- **Part finalisation:** call `PartManager.EnforceNewPart()` while the tool used for the part
  is still active — it stamps tool metadata + camera view onto the part and flags the next
  command to start a fresh part
- Verified round trip: saved via `DataPersistenceManager.SaveConfig()` and re-loaded from disk;
  `commandDetails` contains all region names with full command data
- Screenshots for visual QA are written to `Application.temporaryCachePath/TemplatePoCShots/`

Scaling to the full library:
- Extend the test with a region table: `region name → bone anchor(s) + view direction (front/back)
  + tool (marker stroke / filler outline) + size`
- For posterior/lateral regions the body must be rotated first (LeanPitchYaw yaw on the body,
  e.g. 180° for posterior) — note the bone positions must be read AFTER rotation
- Name each part by its body region (e.g. `"left_forearm_anterior"`, `"right_thigh_lateral"`,
  `"abdomen_central"`, `"head_forehead_left"`, etc.)
- Save as a dedicated "template" twin (**twin names are limited to 11 chars** by
  `TwinNameValidator`: `^[a-zA-Z0-9_()-]{1,11}$`)
- Stickers are not covered by the PoC yet — their placement flow (`StickerEditHandler`)
  needs separate investigation

### 2. Template Storage

- The template twin's `commandDetails` JSON contains all the Command data (positions, matrices, material hashes) for every region
- This data is valid because it was produced by real painting on the actual mesh — it cannot be synthesised
- **Storage decision (2026-07-31):** templates ship as bundled area twins under
  `Assets/Resources/templates/<Area>.twin/ConfigTwin.txt` (registered in the scene's
  `DataPersistenceManager.templates`, so they also appear in the app after Reset for review).
  All six area twins are promoted (98 regions total): `Torso.twin` (21), `Arms.twin` (20),
  `Legs.twin` (14), `Feet.twin` (12), `Head.twin` (17), `Hands.twin` (14) — split per area
  so the group overlay stays scrollable (13 MB total; the command data is what dominates —
  the fill regions carry 100–300 spheres each). Generation workspace + promotion recipe:
  `TemplateLibrary/README.md`.
- **Open design decision for PartTemplateService (step 5):** a runtime **manifest** is needed
  either way (region key → area twin, EN/DE display names, tool kind, default size — feeds the
  LLM region enum, the UI, and the service lookup; generated from BODY_REGIONS.md + the twins).
  Whether to ALSO distill self-contained per-region command files
  (`Resources/PartTemplates/regions/<key>.json`) or to load regions directly from the bundled
  area twins (one-time parse, group name = region key) is a perf call to make on device —
  prefer the twin-direct approach if parse time is acceptable (one format less to maintain).

### 3. AI Workflow (Text → Part)

1. User provides textual description (e.g. "burning sensation on left forearm")
2. LLM receives the list of available region names + available tools
3. LLM returns structured response:
   ```json
   {
     "templateRegion": "left_forearm_anterior",
     "tool": "Burning Sensation",
     "toolColor": {"r": 1, "g": 0.5, "b": 0, "a": 1},
     "meaning": "Burning Sensation",
     "group": "Pain",
     "description": "Burning sensation reported on the anterior left forearm"
   }
   ```
4. A **`PartTemplateService`** class:
   - Loads the template Part for the given region
   - Clones all its `CwCommandSphere` command data
   - Overwrites `Color` in each command with the target tool color
   - Sets part metadata (tool name, meaning, group, description)
   - Generates new GUIDs for the part and its commands
   - Inserts the part into the active version's data model

### 4. What needs to be adjusted per-clone

- `Color` field in each `CwCommandSphere` → new tool color
- Part metadata: `nameTool`, `colorTool`, `meaning`, `description`, `group`, `id`
- Command IDs (new GUIDs)
- `Material` hash may need changing if different tools use different shaders

### 5. Advantages

- No reverse-engineering of PaintIn3D internals
- Position/Matrix data is guaranteed correct (from real painting)
- Both stickers AND filled regions work immediately
- Extensible: add new regions just by painting more templates
- Works across shape parameters (SMPL-X vertex indices are stable)

### 6. Considerations

- Templates are created on the neutral SMPL-X shape. If the patient's shape differs significantly, positions may be slightly off. Acceptable for medical annotation purposes.
- Multiple template sizes per region could be offered (small/medium/large)
- The `view` (camera angle) in the template can be reused to auto-frame the new part

---

## Implementation Steps

1. [x] **PoC:** programmatic template painting via PlayMode test (marker + filler, 3 regions,
       verified save round trip) — `ProgrammaticPaintingTests.cs`
2. [x] Define region naming convention and document all region names — `Assets/Resources/BODY_REGIONS.md`
       (98 keys; EN/DE names; open questions: breast, flanks, spine split, jaw tier)
3. [~] Generate the full region library — **torso done & app-verified (21/98 keys)**;
       remaining: arms front/back, legs front/back, head/neck, hands (palm-orientation
       pitch experiments), extended (ears/jaw side views, soles)
4. [ ] Sticker-based templates (needs investigation of the sticker placement flow)
5. [x] **`PartTemplateService`** (this folder) — `PaintRegion(twin, region [, toolName])` and
       `PaintRegionWithCurrentTool(twin, region)`: loads the region from a bundled area twin,
       clones its part(s) with fresh GUIDs, binds the commands to the live paintable texture,
       recolors them to the chosen tool, and adds the part to the twin's **active group**
       (groups are the user's categories; the region is not a group). The part carries
       `regionKey` (language independent) and the localized region name as `description`.
       Without a tool argument the template's own tool is kept; `PaintRegionWithCurrentTool`
       uses the tool the user has selected and falls back to the first marker when that tool
       cannot carry sphere templates (stickers/text paint decals and are rejected).
       `GetTemplateCatalog()` / `GetTemplateCatalogJson()` return all template twins with their
       regions (`key` + localized `displayName`), read from the bundled assets.
       **Languages:** region names live in `TwinLocalTables` (`region.<key>`), see
       `Assets/Resources/BODY_REGIONS.md`; `enmed`, `demed` and `demedlatin` (Latin anatomy)
       are filled from `Assets/Resources/region_names.tsv`.
       Manual selection UI: `RegionManager` + prefab `Scroll read only two texts and icon`.
       Tests: `Assets/Tests/PlayMode/NoAPICalls/PartTemplateServiceTests.cs` (11 tests).
6. [ ] Build LLM prompt that includes available regions + tools as structured output schema
       (incl. multi-region selection for circumferential descriptions, joint vocabulary
       table from Assets/Resources/BODY_REGIONS.md)
7. [ ] Integrate into UI (new mode or button to trigger text→part)
8. [ ] User review step before committing AI-generated part
9. [ ] Test with various descriptions and validate placement accuracy

---

## Template Library Generation — Findings (2026-07-30)

Generator: `Assets/Tests/PlayMode/TemplateLibraryTools/TemplateLibraryGenerator.cs` (+ `TemplateRegionTable.cs`),
run via **Tools → Template Library → Batch …**. Output persists in `<project>/TemplateLibrary/`
(twin data, per-region screenshots, exported `commandDetails.json`). Batches are idempotent:
existing regions are skipped; keys listed in `TemplateLibrary/regenerate.txt` are deleted and repainted.

Hard-won lessons — all of these affect the eventual `PartTemplateService`:

1. **Save-file size explodes exponentially with parts per group.** `PartData.group` ↔
   `GroupData.groupParts` is a reference cycle that JsonUtility inlines to depth 10:
   8 parts in one group ≈ 635 MB `ConfigTwin`. The template twin therefore uses
   **one group per region** (group name = region key) → 21 regions ≈ 8 MB.
   The PartTemplateService must keep inserted template parts in *small* groups, or the
   serialization cycle must be fixed app-side first.
2. **The paintable texture is a runtime binding, not saved data.** `PaintCommandSerialization`
   (`Assets/Code/DataPersistence/`) is the app-owned base class of `PartManager`, adopted from
   the PaintIn3D example `CwCommandSerialization` so plugin updates need no patching. Its
   `CommandData.PaintableTexture` is `[NonSerialized]` — a Unity object reference is stored as
   a session-local `instanceID` and never resolves in a later session. `PartManager.LoadData`
   binds all loaded commands to the scene's paintable texture; `PartTemplateService` does the
   same for cloned template parts. Old saves (and the bundled template twins) still contain
   the old `instanceID` entries — they are ignored on load and disappear on the next save.
   Same pattern for `PartData.group`: not serialized, re-linked on load (it formed a cycle
   with `GroupData.groupParts` that JsonUtility inlined, growing files by ~(parts/group)^5;
   now every part costs a constant ~17.5 KB).
3. **Cold-replay rendering artifact (test-harness only — confirmed in practice):** the first
   command replay in a harness session can render misaligned (crescent remnants); a warm-up
   paint usually fixes it. In the real app, replay renders correctly (dot experiment +
   full 21-region in-app review). Suspected cause: harness-specific state (CW
   singletons/statics surviving the test's scene unloads). Only relevant to harness
   screenshots — per-region LIVE screenshots and in-app review are the QA basis.
   The persisted command data itself is byte-perfect either way (verified: fresh vs. loaded
   command positions/matrices identical).
4. **The fill tool degrades on strongly curved surfaces** (buttock, groin, genital):
   its grid points reuse the outline's cached raycast depth and miss receding geometry —
   only edge arcs get painted. Curved regions use **thick marker strokes** instead
   (fresh raycast per point). Flat/gently-curved regions (abdomen, chest, back) fill fine.
5. **Marker brush `Radius` renders at ~1:10** — Radius 0.1 paints a ~1 cm line core.
   Stroke widths in the region table are calibrated accordingly (a "3 cm" patch = 0.3).
6. Per-region **live** screenshots (taken right after painting) are the reliable QA basis;
   whole-body overview shots after a reload are subject to the cold-replay artifact (3).
7. **Regeneration scope:** `regenerate.txt` keys are intersected with the running batch's
   own keys — an earlier bug let one batch delete another batch's regions without
   repainting them.

**Status (2026-07-31): torso complete.** All 21 torso regions generated, data-verified AND
reviewed in the app (per-group toggling; regression on previously good regions passed).
Remaining batches: arms front/back, legs front/back, head/neck, hands, extended.

## Open Questions

- How to handle the `Material` hash when switching between tool types (sticker vs brush vs fill)?
- Should the AI be able to place multiple parts from a single description?
- How to handle regions that don't exist in the template library?
- Should we support "partial" regions (e.g. only the upper half of the forearm)?

