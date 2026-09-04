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

- **Run C#:** `unity cmd eval_file --file x.cs -- --timeout 60000`. The `--` matters: `unity cmd
  --timeout` is the CLI's own HTTP wait in *seconds* and never reaches the command, whose own
  budget defaults to 5000 ms. Without it anything slow fails with `Main thread operation timed
  out` — and that error does **not** mean the code did not run: an operation that already started
  on the main thread runs to completion. Only retry idempotent scripts.
- The code is compiled as a method body: top-level `return` works, `using X = Y;` aliases do not,
  and types need their namespace (`UnityEngine.UI.Image`). Reflection is available.
- **Check `editor_status` before `run_tests`** — a run started in play mode dies badly (§9).
- After writing a `.cs` file, `recompile` and poll `recompile_status` before using the new type.
- `capture_game_view --save_path` must point inside the project; `Temp/` is gitignored, so write
  there and copy out.
- Prefer the typed commands (`set_serialized_field`, `get_serialized_fields`,
  `save_prefab_contents`, `find_gameobjects`) over hand-written C# for simple edits — except for
  `SerializableDictionary`, which needs a single apply and therefore `eval`.
