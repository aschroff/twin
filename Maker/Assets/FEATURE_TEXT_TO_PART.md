# Feature: Text → Part (AI-generated Parts from textual descriptions)

> **Status:** Planning / Not yet implemented

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

### 1. Template Creation (one-time manual work)

> **Important constraint:** Template data cannot be created by hand or by code. It can only be produced by using the app itself. The painting process is what generates the valid `CwCommandSphere` position/matrix data. The workflow is:
> 1. Open the app, paint each body region as a Part
> 2. Give each Part a name that matches the agreed region naming convention (e.g. `"left_forearm_anterior"`)
> 3. The app serialises the result into its big JSON (`commandDetails` in `ConfigData`)
> 4. That JSON is then extracted and used as the template library

- Paint ~50 parts covering all relevant body regions using both **stickers** and the **fill/area tool**
- Name each part by its body region (e.g. `"left_forearm_anterior"`, `"right_thigh_lateral"`, `"abdomen_central"`, `"head_forehead_left"`, etc.)
- Save these as a dedicated "template" twin/version

### 2. Template Storage

- The template twin's `commandDetails` JSON contains all the Command data (positions, matrices, material hashes) for every region
- This data is valid because it was produced by real painting on the actual mesh — it cannot be synthesised
- Templates stored as a bundled asset or a special `.twin` file shipped with the app

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

1. [ ] Create template twin with ~50 body region parts (manual painting)
2. [ ] Define region naming convention and document all region names
3. [ ] Implement `PartTemplateService` class (load, clone, recolor, insert)
4. [ ] Build LLM prompt that includes available regions + tools as structured output schema
5. [ ] Integrate into UI (new mode or button to trigger text→part)
6. [ ] User review step before committing AI-generated part
7. [ ] Test with various descriptions and validate placement accuracy

---

## Open Questions

- How to handle the `Material` hash when switching between tool types (sticker vs brush vs fill)?
- Should the AI be able to place multiple parts from a single description?
- How to handle regions that don't exist in the template library?
- Should we support "partial" regions (e.g. only the upper half of the forearm)?

