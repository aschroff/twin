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
