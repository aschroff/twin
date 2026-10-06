# Process Landscape — what the app does, and what we test

This document is the shared map between the people who test the app by hand and the tests that
run automatically. It exists so both sides can point at the same square and say "this is covered"
or "this is not".

It is written in the app's own words — twin, group, part, view, tool — not in medical terms, so
that a step in the map can be found on a screen.

**How to read it.** There are two views of the same thing:

- **Capabilities (P01–P07)** are what the app can do, cut into steps. They run vertically: every
  automated test has exactly one address here.
- **Chains (K01–K05)** are what a person does in one sitting. They run horizontally, across several
  capabilities, and they find what breaks *between* steps rather than inside one.

Every test belongs to exactly one of the two. What a chain already checks, a capability test does
not claim again, and the other way round.

---

## Part 1 — Capabilities

| | Process | Steps |
|---|---|---|
| **P01** | **Manage twins** | create · name · save as copy · open · list · delete · reset the app |
| **P02** | **Mark up the body** | pick a tool (Marker, Filler, Sticker, Text, Delete) · paint freehand · pick a body region and paint it · place · undo / redo |
| **P03** | **Organise into groups** | create · name · select · hide and show · delete · which part sits in which group |
| **P04** | **Look at the twin** | turn · move · zoom · store a view · activate a stored view · Shape |
| **P05** | **Describe and report** | describe a part · report on a version · turn a document or a photo into a twin · screenshots · skin export |
| **P06** | **Exchange twins** | export a zip · import a zip · versions · upload to the server · download from the server |
| **P07** | **App frame** | settings · language · start and quit · saving when the app is sent to the background · telling the user that it is busy |

**Persistence is not a process.** It is the same question asked at the end of every one of them:
leave the twin, open it again, is everything still there. Each process carries that check itself
rather than having a test of its own. (The manual catalogue files its one persistence test,
`04 Persistence`, under P07; it is listed there.)

---

## Part 2 — Chains

| | Chain | Crosses |
|---|---|---|
| **K01** | A new twin, with its first parts painted into groups | P01 · P02 · P03 |
| **K02** | Open a twin, see where it stands, add to it | P01 · P06 · P04 · P03 |
| **K03** | Receive a twin from someone else and open it | P06 · P02 · P01 |
| **K04** | A document becomes a twin | P05 · P03 · P02 |
| **K05** | Report on a version | P05 · P03 |

K05 needs a key for the language model and therefore lives with the tests that cost money. The
other four run offline.

---

## Part 3 — Coverage today

Manual test numbers refer to the business department's catalogue (`01/01 Top Navigation`,
`05/03 Twin names Test`, and so on). Since its export of October 2026 the catalogue is itself
sorted by P1–P7, so a manual test is found under the same process in both documents. One
exception: `06/07 Shape Test` sits under P2 there and under **P04** here, because Shape changes how
the twin looks, not what is painted on it.

### P01 — Manage twins

| | |
|---|---|
| Automated | `SaveTwinPlayModeTests` (1), `InfoDisplayPlayModeTests` (2 of its 5), `FileDataHandlerTests` (14), `DataPersistenceManagerTests` (9), `LoadEveryTwinTests` (1), `TwinLoadingMemoryTests` (1), `TwinActivityMemoryTests` (1), `TextureSaveCostTests` (2), `TextureSaveReadbackTests` (1) |
| Manual | 02 Twin management (WIP) · 02/01 Create, name twins · 02/02 Delete twin · 03 App reset · 05/01 Twins save functionality · 05/02 Duplicated twin name · 05/03 Twin names |
| **Gap** | **A** — name validation (valid and invalid characters, length, duplicates and their error messages) is checked by hand only. `TwinNameValidator` has no test at all.<br>**A** — a reset is checked by hand against seven separate expectations (blank twin, all twins gone, camera, groups, views, stickers, text boxes); automatically only the twin that is loaded afterwards (`InfoDisplayPlayModeTests`, filed under P07).<br>**B** — loading every twin in the list, and the memory a twin switch costs (TWIN-457, TWIN-459), are automated and have no manual counterpart. `TextureSaveCostTests` asserts nothing; it reports what each step of saving the paint costs. |

Deleting a twin (02/02) is covered on the data side — `DataPersistenceManagerTests` keeps the open
twin alive, `FileDataHandlerTests` removes the whole directory — but not through the twin list.

### P02 — Mark up the body

| | |
|---|---|
| Automated | `UndoRedoPlayModeTests` (2), `StickerPlayModeTests` (2), `ProgrammaticPaintingTests` (1), `PartTemplateServiceTests` (11), `PartHistoryTests` (13), `InfoDisplayPlayModeTests` (1 of its 5) |
| Manual | 06/01 Edit navigation · 06/02 a Marker painting · 06/02 b Marker name · 06/03 a Sticker edit with default sticker · 06/03 b Importing a photo as sticker · 06/04 Delete · 06/05 Filler · 06/06 a Text colour and size · 06/08 Placement |
| **Gap** | **A** — Text, Filler and Delete are checked by hand only, and so are naming a marker (06/02 b) and turning, flipping and resizing a sticker (06/03 a).<br>**C** — the **Region screen** is checked by nobody: the service behind it has 11 tests, the screen has none, and the manual catalogue does not mention it. 06/01 lists five tools; the Edit screen's bottom bar has seven (Marker, Filler, Sticker, Text, Delete, Region, Shape). Shape is the seventh but belongs to **P04**, where it is covered. |

`EditUiPlayModeTests`, which paints through the Edit screen, is the chain **K01** and is therefore
not counted here.

### P03 — Organise into groups

| | |
|---|---|
| Automated | `GroupPlayModeTests` (3), `GroupDetailPlayModeTests` (1), `PartManagerTests` (11), `InfoDisplayPlayModeTests` (1 of its 5) |
| Manual | 07/02 a Basic group functionality · 01/03 Overview |
| **Gap** | none worth naming. |

### P04 — Look at the twin

| | |
|---|---|
| Automated | `ViewPlayModeTests` (3), `TourProcessPlayModeTests` (1, standard views only), `ShapePlayModeTests` (8) |
| Manual | 01/01 Top navigation · 01/02 View and group window · 07/01 a Stored view after turning the twin · 06/07 Shape (filed under P2 in the catalogue) |
| **Gap** | Storing a view, activating it, and activating it *after the twin has been turned* are covered since TWIN-456. Shape is covered since TWIN-473 — screen, button wiring, all five functions, and that a changed figure survives save and reload; what a test cannot say is whether the figure then *looks* plausible, so 06/07 stays a manual check. What is still by hand only: turning, moving and zooming as such. |

### P05 — Describe and report

| | |
|---|---|
| Automated | offline: `DocumentPromptPlayModeTests` (3), `DocumentApplyPlayModeTests` (7), `UploadPlayModeTests` (2), `PartsScreenshotProcessPlayModeTests` (1), `SkinProcessPlayModeTests` (1), `MissingScreenshotsButtonPlayModeTests` (1), `PartsDescriptionButtonPlayModeTests` (1), `PartsDescriptionPlayModeTests` (4), `PartMenuPlayModeTests` (3)<br>with a key: `DocumentMappingApiTests` (2), `PartDescriptionProcessTests` (1), `PartsDescriptionProcessTests` (1), `VersionProcessTests` (1) |
| Manual | 08/01 Part descriptions · 08/02 Version description (with images) · 08/03 Version description · 09 Screenshots · 12 Turn a document into a twin · 12/01 Turn a photo into a twin |
| **Gap** | The old gap **B** is closed: the catalogue now covers this process. What remains:<br>**A** — turning a *photo* into a twin (12/01) is checked end to end by hand only. The tests prove the photo entry is offered and that a PDF goes all the way to the review screen, not that a photo does.<br>**A** — whether the screenshot also lands in the Photos app (08/02, 09) is a device question; no test can see it.<br>Describing parts and versions for real is automated only *with a key*. Offline, the tests pin what the buttons offer ("missing images", "missing descriptions") and that a run never overwrites text that is already there. |

### P06 — Exchange twins

| | |
|---|---|
| Automated | `ImportTwinPlayModeTests` (10), `TwinVersionsClientTests` (17), `TwinVersionRowTests` (9), `TwinAuthTests` (17) |
| Manual | 10/01 Export a zip · 10/02 Import a zip · 11/01 New version · 11/02 Clone version |
| **Gap** | **A** — creating a new version and cloning one (11/01, 11/02) are checked by hand only.<br>**B** — uploading to and downloading from the server, and signing in to it, are automated at the level of the client and the version rows, and have no manual test. |

### P07 — App frame

| | |
|---|---|
| Automated | `SettingsUiPlayModeTests` (1), `InfoDisplayPlayModeTests` (1 of its 5, the reset case), `AutoSavePlayModeTests` (2), `BusyOverlayPlayModeTests` (2), `ThinkingOverlayPlayModeTests` (4) |
| Manual | 04 Persistence · 12 Language |
| **Gap** | **A** — switching the language (12 Language) is checked by hand only; `SettingsUiPlayModeTests` opens the screen and goes no further.<br>**B** — saving when the app is sent to the background (TWIN-467), the loading panel during a twin switch (TWIN-461) and the logo while the language model is asked (TWIN-475) are automated and have no manual counterpart. The manual tests 12 and 12/01 (P05) do check that "the loading indicator is visible". |

### Chains

Two of the five exist. **K01** is `EditUiPlayModeTests` — open a twin, edit, pick a view, paint,
return, check the group detail page — which carried the chain's category since TWIN-455 instead of
being written again. **K05** is `VersionSequenceProcessTests` (screenshot → part description →
version report, with a key). K02, K03 and K04 have no test.

---

## What the gaps mean

| | Meaning | What it costs |
|---|---|---|
| **A** | checked by hand, not automatically | somebody keeps doing it by hand, and it is only as reliable as the last time they had time |
| **B** | automated, not in the manual catalogue | no risk to the product; the business department cannot see what is already safe |
| **C** | checked by neither | a real hole |

**The only C today is the Region screen.** In order of value, the A gaps are: twin names (P01),
the reset checklist (P01), the remaining tools (P02), new and cloned versions (P06), the language
switch (P07), and a photo turned into a twin (P05).

---

## How the tests are organised

Two things have to be visible at once, and they are not the same thing:

**What a test costs** decides the folder. Tests that call the language model cost money and need a
key; they must stay apart so that everyone else can run everything else.

- `Assets/Tests/EditMode/` — no scene, no app, no network. Seconds.
- `Assets/Tests/PlayMode/NoAPICalls/` — drives the real app, no external service. Minutes.
- `Assets/Tests/PlayMode/APICalls/` — needs a key in `testsecrets.json`, costs tokens.

**Which process a test belongs to** decides its category, not its folder — a folder tree can only
carry one of the two, and cost is the one that must never be guessed. Each test carries exactly one
category, so a process can be run across all three folders:

```
unity cmd run_tests --mode PlayMode --filter_type category --filter P04_look_at_the_twin
```

The filter matches the **whole** category name — no prefixes, no patterns — so the names to copy are:

| Process | Chain |
|---|---|
| `P01_manage_twins` | `K01_new_twin_first_parts` |
| `P02_mark_up_the_body` | `K02_open_and_add_to_a_twin` |
| `P03_organise_into_groups` | `K03_receive_a_twin` |
| `P04_look_at_the_twin` | `K04_document_becomes_a_twin` |
| `P05_describe_and_report` | `K05_report_on_a_version` |
| `P06_exchange_twins` | |
| `P07_app_frame` | |
| `T00_technical` | |

They are written once as constants (`Assets/Tests/Helper/TestCategories.cs`) and used from there,
so a typo is a compile error rather than a test that quietly drops out of the map:

```csharp
[Category(Processes.MarkUpTheBody)]
public class UndoRedoPlayModeTests : PlayModeTestBase
```

A guard test (`TestCategoriesGuardTests`) walks both test assemblies and fails when a test carries
no category, or more than one — that is what keeps this document honest when someone adds a test
in six months. The category sits on the class as a rule; `InfoDisplayPlayModeTests` is the one
class whose five tests carry it per method, because they check the status display of four
different processes.

`NoAPICalls/` is a promise about **cost**, not about failing: every test that spends tokens guards
itself with `Assert.Ignore` when there is no key, so a run without a key was green wherever the
test sat. With a key it was not free — one test in `NoAPICalls/` really called the model and took
26 seconds of every offline run. Three files were moved into `APICalls/` for that reason
(`OpenAIClientTests` and `DocumentMappingApiTests`, which sat beside the folders, and
`PartsDescriptionProcessTests`, which sat in `NoAPICalls/`).

Tests marked `[Explicit]` do not run with the suite and are not counted below. There are two
kinds: the template library generator (`TemplateLibraryTools/`, ten batches), a tool that is merely
driven through the test runner; and five tests that produce something for a person to look at
rather than assert anything — screenshots of the loading panel, the thinking logo and the group
detail buttons, a timing of twin switches, and a diagnosis of what an undo leaves on the body.
The five carry a category all the same.

---

## Known open points

Known, and deliberately not covered by tests, because they are business decisions:

- **Version names grow on every exchange.** A twin passed back and forth ends up called
  `000V01V01`. To be settled once versions are kept as timestamps.
- **Imported versions are hard to find.** The twin list shows one row per twin name; further
  versions sit in the version overview. Once twins arrive unannounced over a server, it has to be
  decided how the user learns about them.
- **Twin names are limited to 11 characters**, but the error message says 14.
- **The twin that is currently open cannot be deleted**, so the twin list can never be emptied
  completely from inside the app.
- **Export size.** Every export carries the body paint as an image (about 1.2 MB). Exports from
  older versions of the app do not, and arrive unpainted.
- **The face is never saved.** The figure, the arms and the hands are in the save file; the
  expressions are not — `ConfigData` has no field for them. A face the user set is gone at the next
  app start. No test claims it either way.

### Notes on the manual catalogue

Found while matching the October 2026 export against this map, for whoever maintains it:

- The number **12** is used twice — `12 Turn a document into a twin` (P5) and `12 Language` (P7).
- `02 Twin management (WIP)` repeats the first four steps of `03 App reset` and stops there.
- Several tables end in numbered steps without content (`03`, `06/04`, `06/05`, `06/07`, `01/03`),
  and `11/02 Clone Versions` numbers steps 7 and 8 twice.
- `06/02 b Marker name` still carries the open question which characters a marker name allows.

---

## Not in this document

Purely technical tests without a business-visible flow: the schema builder for structured model
answers, the key lookup, and the token and session handling of the twin server. They are listed in
`PlayMode/TESTS_OVERVIEW.md`, which stays the technical inventory.

## Where the tests sit today

| Address | Tests |
|---|---|
| `P01_manage_twins` | 32 |
| `P02_mark_up_the_body` | 30 |
| `P03_organise_into_groups` | 16 |
| `P04_look_at_the_twin` | 12 |
| `P05_describe_and_report` | 28 |
| `P06_exchange_twins` | 53 |
| `P07_app_frame` | 10 |
| `T00_technical` | 30 |
| `K01_new_twin_first_parts` | 1 |
| `K05_report_on_a_version` | 1 |
| **total** | **213** |

By cost: 109 in `EditMode/`, 89 in `PlayMode/NoAPICalls/`, 15 in `PlayMode/APICalls/`. Not
counted: the 15 `[Explicit]` tests described above.

`P04_look_at_the_twin` stood at one test until TWIN-456, went to four with stored views, and is now
at twelve — the Shape screen brought eight with TWIN-473. What has no test at all is K02, K03 and
K04.

**Counted on 6 October 2026** (at `3dfbde0`, TWIN-473), by reflection over both test assemblies —
the same walk the guard test makes.
