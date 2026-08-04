# CW Painting Plugin — Test Guide

## Why EventSystem won't work

CW painting (PaintCore / PaintIn3D) has its **own input pipeline** and never reads from Unity's EventSystem. `ExecuteEvents.Execute` with pointer handlers does nothing for painting.

## Input pipeline (simplified)

```
CwPointerMouse.Update()          polls CwInput.GetKeyIsHeld(KeyCode.Mouse0)
  └─► CwPointer.GetFinger()      creates / steps a Finger object
        └─► CwHitPointers.HandleFingerUpdate()   (= CwHitScreen)
              └─► Physics.Raycast from screen position into 3D mesh
                    └─► CwPointConnector.SubmitPoint()
                          └─► CwPaintableManager.LateUpdate()  executes paint
```

Key files:
- `Assets/Plugins/CW/PaintCore/Required/Scripts/CwPointerMouse.cs`
- `Assets/Plugins/CW/PaintCore/Required/Scripts/CwPointer.cs`
- `Assets/Plugins/CW/PaintCore/Required/Scripts/CwHitPointers.cs`
- `Assets/Plugins/CW/PaintIn3D/Required/Scripts/CwHitScreen.cs`
- `Assets/Plugins/CW/PaintIn3D/Required/Scripts/CwHitScreenBase.cs`
- `Assets/Plugins/CW/Shared/Common/Examples/Scripts/CwInputManager.cs`  (Finger / Link classes)

## Simulating a paint drag in a PlayMode test

```csharp
using CW.Common;   // CwInputManager.Finger
using PaintIn3D;   // CwHitScreen
using PaintCore;   // CwPointerMouse

// 1. Find active hit screens (only present while a painting tool is selected)
var hitScreens = Object.FindObjectsOfType<CwHitScreen>(false);

foreach (var hitScreen in hitScreens)
{
    var pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
    if (pointerMouse == null) continue;

    // Disable GUI-layer guard so the test position is never "over UI"
    hitScreen.GuiLayers = 0;

    // Pointer down  (index 1 = PAINT_FINGER_INDEX inside CwPointerMouse)
    CwInputManager.Finger finger;
    pointerMouse.GetFinger(1, startScreenPos, 1.0f, true, out finger);
    hitScreen.HandleFingerUpdate(finger, down: true, up: false);
}
yield return null;  // let CwHitScreen process

foreach (var hitScreen in hitScreens)
{
    var pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
    if (pointerMouse == null) continue;

    // Drag  (down=false = continuing stroke, not a new press)
    CwInputManager.Finger finger;
    pointerMouse.GetFinger(1, startScreenPos + dragDelta, 1.0f, true, out finger);
    hitScreen.HandleFingerUpdate(finger, down: false, up: false);
}
yield return null;

// Release — TryNullFinger calls hitScreen.BreakFinger internally
foreach (var hitScreen in hitScreens)
{
    var pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
    if (pointerMouse == null) continue;
    pointerMouse.TryNullFinger(1);
}

yield return null;
yield return null;  // let CwPaintableManager.LateUpdate() execute commands
```

`PlayModeTestBase.DragOnCanvas()` already implements this pattern and falls back to EventSystem when no `CwHitScreen` is active.

## Key gotchas

| Gotcha | Detail |
|--------|--------|
| **GUI guard** | `CwHitScreen.HandleFingerUpdate` returns early (no paint) when `down==true` and `CwInputManager.PointOverGui(pos, GuiLayers)` is true. Set `hitScreen.GuiLayers = 0` to bypass in tests. |
| **No link on drag** | If `down==false` and no link exists for that finger, the method returns immediately. Always send a `down=true` event first. |
| **Finger index** | Use `1` (`PAINT_FINGER_INDEX`) for actual painting. `-1` is the preview/hover finger. |
| **`GetFinger` manages state** | Call `pointerMouse.GetFinger()` rather than constructing `Finger` yourself; it handles the old-position history needed for smooth strokes. |
| **Raycast must hit mesh** | Painting only occurs if the screen-space ray hits a collider on `hitScreen.Layers`. The center of the screen should hit the 3D model in most EditMarker-mode layouts. |
| **LateUpdate flush** | `CwPaintableManager` batches commands and flushes in `LateUpdate`. Yield at least one frame after the drag before asserting paint results. |
| **⚠ Multi-frame strokes DON'T work** | `CwPointerMouse.Update()` polls the REAL mouse every frame; when no button is held it calls `TryNullFinger(1)`, destroying synthetic fingers between frames. A synthetic stroke spread over several frames (yield between moves) only ever paints its first (down) event: the next `GetFinger` creates a NEW `Finger` object, `Link.Find` matches by finger reference, finds no link, and `down==false` returns early. **Perform the entire stroke (down → moves → up) within a single frame** — `CwPaintableManager` still flushes it in the same frame's `LateUpdate`. (This also means `PlayModeTestBase.DragOnCanvas`'s drag step never actually paints — only its initial down event does.) |
| **Fill tools** | `CwHitScreenFill : CwHitScreen` fills on **finger up**: send `HandleFingerUpdate(finger, false, up: true)` at the end. It grid-fills the area enclosed by the drawn outline (`link.History`), so draw a *closed* polygon (e.g. a screen-space circle) before releasing. Spacing between fill spheres is `FillSpacing` (px). |
| **Fills fail on curved surfaces** | The grid fill's `PaintAt` calls reuse the outline's cached raycast data (`Connector.HitCache`) — on strongly curved regions (buttock, groin) the interior points miss the receding surface and only edge arcs paint. Use thick marker strokes (fresh raycast per point) for such regions. |
| **Replaying serialized commands** | `CommandData.PaintableTexture` is serialized as an instanceID → always null after a scene reload. Since 2026-07-31 `PartManager.LoadData` re-binds null references automatically (regression test: `PartTemplateServiceTests.LoadTwin_WithStaleTextureReferences_RebindsOnLoad`); only code that bypasses `LoadData` needs to re-bind manually. Also: the FIRST replay in a harness session can render misaligned ("crescents") — do one live warm-up paint first, then Erase+Refresh. Harness-only (in-app replay renders correctly); suspect lazy skinned-mesh bake. The serialized data itself round-trips byte-perfectly. |
| **Finger up vs TryNullFinger** | `HandleFingerUpdate(..., up: true)` triggers `OnFingerUp` (needed for fill). `TryNullFinger` only calls `BreakFinger` (breaks stroke connection, no `OnFingerUp`). Send `up: true` first, then `TryNullFinger(1)` to clean up pointer state. |

## Painting at specific body positions (template parts)

See `ProgrammaticPaintingTests.cs` for the full pattern:
1. Find SMPL-X bone transforms by name (`left_elbow`, `pelvis`, `head`, …) — unique in scene.
2. Project anchor world positions to screen: `Camera.main.WorldToScreenPoint` (ray through that
   pixel hits the camera-facing surface of the mesh).
3. Select the tool through the real UI buttons (e.g. `Canvas/EditMarker UI/Bottom/Scroll/Panel/Red`),
   which activates the tool GameObject under `Tools/`.
4. Paint the whole stroke in one frame (see gotcha above), yield 2 frames.
5. Call `PartManager.EnforceNewPart()` while the tool is still active to finalize the part,
   then set `currentPart.description` for the region name.
