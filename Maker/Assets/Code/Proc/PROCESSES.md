# Processes — the app's job layer

> **Purpose:** reference for `Assets/Code/Proc/`. Companion document:
> `Assets/Code/AI/AI_INTEGRATION.md` (what the AI-facing processes call).

---

## 1. What a process is

A **Process** is one user-triggerable job: take a screenshot, describe a part, export the skin,
build a report. Processes are MonoBehaviours parked as **children of the scene's `Process`
object**, which itself carries `ProcessManager` (and the `AI` component).

```
Process (GameObject)            ← ProcessManager + AI
├── TourProcess
├── SkinProcess
├── CompleteReportProcess
├── PartsProcess
├── PartsDescriptionProcess
├── PartDescriptionProcess
├── VersionProcess
├── PartsScreenshotProcess
├── VersionSequenceProcess      (SequenceProcess)
├── PartsDescriptionProcessHardRedo
├── PhotoProcess                (AvatarProcess)
└── DocumentUploadProcess
```

The parent–child relation is load-bearing: `Process.processManager()` is
`transform.parent.GetComponent<ProcessManager>()`. A process placed anywhere else gets a
`NullReferenceException` on its first accessor.

---

## 2. The base classes

### `Process` (`Process.cs`)

```csharp
public abstract ProcessResult Execute(string variant = "");
public void Handle(string variant = "") => Execute(variant);
```

`Handle` is the **UI entry point** — that is the method wired into a button or a `MenuManager`
entry, with `variant` as the persistent call's string argument. `ProcessResult` is currently just
`{ int code }` and every process returns a fresh empty one; results travel through the data
model or the UI, not through the return value.

`variant` does double duty. Usually it names the prompt (`"Medical Report"`), which is looked up
as an `ItemPrompt` label. `PartDescriptionProcess` additionally packs an id into it —
`"<variant>##<partId>"`, split by `SplitStringByDoubleHash`.

Protected accessors, all resolved through `ProcessManager`:

| Accessor | Gives |
|----------|-------|
| `getAI()` | the `AI` component (`Code.AI.AI`) |
| `getPartManager()` | groups, parts, current group, part lookup |
| `getDataManager()` | `DataPersistenceManager` — `selectedProfileId`, save/load, export/import |
| `getViewmanager()` / `getStandardViewManager()` | camera views |
| `getRecorder()` | the screenshot recorder |
| `getBody()` | the paintable body |
| `getSettingsManager()` | prompt rows, settings input fields |
| `getNotification()` | the `LeanPulse` toast overlay |

`ProcessManager` is a plain field holder; all of these are wired in the scene.

### `ProcessSync` (`ProcessSync.cs`)

Adds `ExecuteSync(variant)` plus an `ExecuteCompleted` event, so a process can be awaited.
`SequenceProcess` holds a `List<ProcessSync>` and runs them one after another, awaiting each
one's event (`VersionSequenceProcess` = screenshots → part descriptions → version report).
A `ProcessSync` that never raises `OnExecuteCompleted()` stalls the sequence forever.

### `QuickHelpProcess` (`Proc/AI/`)

Convenience base for "screenshot the whole body, then ask": takes the shot through `Recorder`,
puts its path on `AI.path`, then calls the abstract `CallAI(ai, variant)`.
`CompleteReportProcess` is its only subclass.

---

## 3. Reporting back to the user

There is one pattern, repeated in every process — the toast overlay:

```csharp
LeanPulse notification = getNotification();
foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
{
    text.text = message;
}
notification.Pulse();
```

Long jobs otherwise give no progress; `StartingProcessingMode` is the only "busy" screen and is
not driven from here.

---

## 4. `Recorder` — screenshots

Lives on `Canvas` (wired as `ProcessManager.recorder`).

| Member | Does |
|--------|------|
| `Prepare()` | hides all of its children (the overlays) and returns those that were visible |
| `Do()` | `ReadPixels` of `Rect(0, 900, Screen.width, Screen.height - 1200)` → writes `<data>/<folder>/screenshot_<name>.png` and into the OS gallery |
| `Reset(list)` | shows the overlays again |
| `Post(notification)` | the toast naming the file |
| `get_path()` | the path `Do()` writes |

`name` / `folder` are set by the caller before each shot — that is how one run produces many
files (`TourProcess` per view, `PartsProcess` per part). The **crop rect is hard-coded** for a
tall portrait screen; on other resolutions the shot is off.

`Do()` must run right after `yield return new WaitForEndOfFrame()`, otherwise the frame is not
rendered yet.

---

## 5. The processes

| Process | Kind | What it does |
|---------|------|--------------|
| `TourProcess` | Process | one screenshot per standard view |
| `PartsProcess` | Process | isolates every part in turn (`ClearRefreshPart` + its stored view) and shoots it |
| `PartsScreenshotProcess` | Process | screenshots for parts, variant of the above |
| `SkinProcess` | Process | writes the painted body texture as `skin_<twin>.png` into the twin folder and the gallery |
| `PartDescriptionProcess` | Process | one part → AI (`variant##partId`); needs the part's screenshot on disk, otherwise toasts "No Screenshot" |
| `PartsDescriptionProcess` | ProcessSync | fans out `PartDescriptionProcess` over every part; `hardRedo` redoes parts that already have a description |
| `VersionProcess` | ProcessSync | waits for `AllPartsDescribed()`, then one report over all part descriptions |
| `CompleteReportProcess` | QuickHelpProcess | whole-body screenshot + marker/filler legend → report |
| `SequenceProcess` | Process | runs a configured list of `ProcessSync` in order |
| `DocumentUploadProcess` | Process | picks a photo or a PDF, has it mapped onto the twin by the LLM, opens the review screen for it, and on Apply writes what the user ticked (`ApplyConfirmed` → `DocumentMappingApplier`) and saves (`Proc/Document/`, see its feature spec) |

Note that `PartsDescriptionProcess` starts all part coroutines in the same frame — the requests
run in parallel and `VersionProcess` only waits on `AllPartsDescribed()` with a 10 s timeout.

---

## 6. Wiring a new process

1. Add a child GameObject under `Process` carrying the new `Process` subclass.
2. Give the UI a `MenuManager` entry (or a button) calling `Handle` on it with the variant string.
   The entries of a panel's `MenuManager` are configured on the **scene instance**, because they
   reference scene objects; see `Assets/Prefabs/GUI/Upload UI.prefab` for the shape.
3. If it prompts the LLM, add its `ItemPrompt` row under **Settings → Prompts** with a matching
   `label`/`level` — there is no `Default` row to fall back to.
4. Report the outcome through `getNotification()`.
5. Cover it with a PlayMode test under `Assets/Tests/PlayMode/NoAPICalls/` if it can be tested
   without the network, and register it in `Assets/Tests/Editor/TemplatePoCRunner.cs`.
