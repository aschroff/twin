# Twin Maker — notes for Claude Code

Everything a *person or any tool* needs is in the documents below; this file only adds what is
specific to working here through Claude Code.

## Read before changing anything

| Topic | Document |
|---|---|
| The app as a whole, twin layout, known limits, **driving the editor from outside (§9)** | `Assets/APP_DOCUMENTATION.md` — start here |
| Tests: what exists, how to run it, why PlayMode tests need a base class | `Assets/Tests/PlayMode/TESTS_OVERVIEW.md` |
| Painting tests | `Assets/Tests/PlayMode/NoAPICalls/CwPaintingTestGuide.md` |
| Five locales, and why they are not five languages | `Assets/Code/Localization/README.md` |
| Signing in to the backend | `Assets/Code/Net/Auth/README.md` |
| Twin versions on the server | `Assets/Code/Net/Twins/README.md` |

**§9 of `APP_DOCUMENTATION.md` is required reading before any scripted edit of a scene, prefab or
string table.** The traps listed there are the expensive ones.

## House rules

- UI goes into the prefab, never onto the prefab instance in the scene.
- A PlayMode test derives from `PlayModeTestBase`, or it runs the real app against the user's real
  twins. A test that needs no scene is a unit test and belongs in `Assets/Tests/EditMode/`.
- Never edit config files while the app is running — it overwrites them on quit.
- Every new UI string needs an entry in all five locale tables. There is no fallback.
- Write the test. A fix is verified by watching it fail without the change.

## Using the `unity` CLI from here

There are two mutually exclusive modes, and picking the wrong one wastes a full editor launch:
`unity cmd …` talks to the **running** editor, while `unity test` and `unity build` spawn their
**own** batch-mode editor — which cannot open a project another editor already has, and aborts
with "another Unity instance is running with this project open". With the editor open, use
`unity cmd run_tests`. Everything below assumes a connected editor (`unity status`).

**Check that the editor is there before you plan around it.** `unity status` with no row means no
editor, and `unity cmd …` then fails with "No Unity Editor instances found with reachable Pipeline
servers". Run `unity status` *and* `unity cmd editor_status` as the first step of any sequence that
needs the editor — not after writing the files, and never for the first time three minutes into a
test run. `editor_status` also tells you whether it is compiling, mid domain reload, or in play
mode, all of which make a run fail in ways that look like something else. A changed `PID` in
`unity status` means the editor restarted and anything you had started is gone.

**Say how long it will take, then report while it runs.** A PlayMode test costs roughly 5-15 s
including its scene load, so the `NoAPICalls` suite is about seven minutes; `unity cmd list_tests`
gives the count to base an estimate on. State the estimate *before* starting, then report at least
once a minute. Two things make silence expensive: `test_status` answers only `running` with an
empty summary until the very end (so it is no progress signal, and neither is
`Temp/pipeline_test_status.json`, which has the same shape), and a backgrounded poller may never
report at all. What does grow during a run is the console — `unity cmd get_console_logs` — because
every test reloads the scene and logs, though its `total` **saturates at 1000** and is useless as
a progress signal past that point (about 100 s into a suite). The reliable liveness signal is
`unity cmd editor_status`: `"playMode":"playing"` for as long as PlayMode tests are actually
running, back to `"stopped"` when the run ends. A person watching an idle-looking editor cannot tell a
seven-minute run from a crashed one; that is on the person driving, not on them.

- **Run C#:** `unity cmd eval_file --file x.cs -- --timeout 60000`. The `--` matters: `unity cmd
  --timeout` is the CLI's own HTTP wait in *seconds* and never reaches the command, whose own
  budget defaults to 5000 ms. Without it anything slow fails with `Main thread operation timed
  out` — and that error does **not** mean the code did not run: an operation that already started
  on the main thread runs to completion. Only retry idempotent scripts.
- The code is compiled as a method body: top-level `return` works, `using X = Y;` aliases do not,
  and types need their namespace (`UnityEngine.UI.Image`). Reflection is available.
- **No editor connected?** `unity open .` (from `Maker/`) launches the project's editor; it shows
  up in `unity status` after about a minute.
- **PlayMode `run_tests` returns at once** with `"result":"running"` and an empty summary, async
  flag or not. Poll `unity cmd test_status` until `status` is `completed`; parse its JSON with
  `strict=False`, failure messages carry raw newlines. EditMode runs return their results inline.
- **Check `editor_status` before `run_tests`** — a run started in play mode dies badly (§9).
- After writing a `.cs` file, `recompile` and poll `recompile_status` before using the new type.
- `capture_game_view --save_path` must point inside the project; `Temp/` is gitignored, so write
  there and copy out.
- Prefer the typed commands (`set_serialized_field`, `get_serialized_fields`,
  `save_prefab_contents`, `find_gameobjects`) over hand-written C# for simple edits — except for
  `SerializableDictionary`, which needs a single apply and therefore `eval`.
