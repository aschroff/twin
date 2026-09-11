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
| **P05** | **Describe and report** | describe a part · report on a version · turn a document into a twin · screenshots · skin export |
| **P06** | **Exchange twins** | export a zip · import a zip · versions · server sync |
| **P07** | **App frame** | settings · language · start and quit |

**Persistence is not a process.** It is the same question asked at the end of every one of them:
leave the twin, open it again, is everything still there. Each process carries that check itself
rather than having a test of its own.

---

## Part 2 — Chains

| | Chain | Crosses |
|---|---|---|
| **K01** | A new twin, with its first parts painted into groups | P01 · P02 · P03 |
| **K02** | Open a twin, see where it stands, add to it | P01 · P06 · P04 · P03 |
| **K03** | Receive a twin from someone else and open it | P06 · P02 · P01 |
| **K04** | A document becomes a twin | P05 · P03 · P02 |
| **K05** | Report on a version | P05 · P03 |

K5 needs a key for the language model and therefore lives with the tests that cost money. The
other four run offline.

---

## Part 3 — Coverage today

Manual test numbers refer to the business department's catalogue (`01 navigation tests`,
`05/03 Twin names Test`, and so on).

### P01 — Manage twins

| | |
|---|---|
| Automated | `SaveTwinPlayModeTests`, `InfoDisplayPlayModeTests` (5), `FileDataHandlerTests` (13), `DataPersistenceManagerTests` (9) |
| Manual | 02 Twin management · 03 App reset · 05/01 Twins save functionality · 05/02 Duplicated twin name · 05/03 Twin names · Name tests |
| **Gap** | **A** — name validation (valid and invalid characters, length, duplicates and their error messages) is checked by hand only. `TwinNameValidator` has no test at all. |

### P02 — Mark up the body

| | |
|---|---|
| Automated | `EditUiPlayModeTests`, `UndoRedoPlayModeTests` (2), `StickerPlayModeTests` (2), `ProgrammaticPaintingTests`, `PartTemplateServiceTests` (11), `PartHistoryTests` (13) |
| Manual | 06/01 Edit navigation · 06/02 Marker · 06/03 Sticker · 06/04 Delete · 06/05 Filler · 06/06 Text · 06/08 placement |
| **Gap** | **A** — Text, Filler and Delete are checked by hand only.<br>**C** — the **Region screen** is checked by nobody: the service behind it has 11 tests, the screen has none, and the manual catalogue does not mention it. 06/01 lists five tools; the app has seven (Marker, Filler, Sticker, Text, Delete, Region, Shape). |

### P03 — Organise into groups

| | |
|---|---|
| Automated | `GroupPlayModeTests` (3), `GroupDetailPlayModeTests`, `PartManagerTests` (11) |
| Manual | 07/02 Store group · 01/03 overview |
| **Gap** | none worth naming. |

### P04 — Look at the twin

| | |
|---|---|
| Automated | `TourProcessPlayModeTests` (standard views only) |
| Manual | 01/01 Top navigation · 01/02 view and group window · 07/01 Store view · 07/01a Stored view after turning · 06/07 Shape |
| **Gap** | **A** — storing a view, activating it, and activating it *after the twin has been turned* are checked by hand only. This is the weakest capability in the landscape. |

### P05 — Describe and report

| | |
|---|---|
| Automated | `DocumentPromptPlayModeTests` (3), `DocumentApplyPlayModeTests` (7), `UploadPlayModeTests` (2), `PartsScreenshotProcessPlayModeTests`, `SkinProcessPlayModeTests`, `PartsDescriptionProcessTests`, and with a key `PartDescriptionProcessTests`, `VersionProcessTests`, `VersionSequenceProcessTests` |
| Manual | — |
| **Gap** | **B** — the business catalogue does not cover this at all, although it is the newer half of the app. Nothing is unchecked; the two sides simply do not know about each other. |

### P06 — Exchange twins

| | |
|---|---|
| Automated | `ImportTwinPlayModeTests` (10), `TwinVersionsClientTests` (9), `TwinAuthTests` (17) |
| Manual | — |
| **Gap** | **B**, as above. |

### P07 — App frame

| | |
|---|---|
| Automated | `SettingsUiPlayModeTests`, `InfoDisplayPlayModeTests` (the reset case) |
| Manual | 03 App reset · 04 Persistence |
| **Gap** | **A** — a reset is checked by hand against seven separate expectations (blank twin, all twins gone, camera, groups, views, stickers, text boxes); automatically only two of them. |

### Chains

None of the five exists as a named test yet. `EditUiPlayModeTests` is already chain-shaped — open a
twin, edit, pick a view, paint, return, check the group detail page — and would become **K01**
rather than being written again.

---

## What the gaps mean

| | Meaning | What it costs |
|---|---|---|
| **A** | checked by hand, not automatically | somebody keeps doing it by hand, and it is only as reliable as the last time they had time |
| **B** | automated, not in the manual catalogue | no risk to the product; the business department cannot see what is already safe |
| **C** | checked by neither | a real hole |

**The only C today is the Region screen.** In order of value, the A gaps are: stored views (P04),
twin names (P01), the remaining tools (P02), the reset checklist (P07).

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

| Category | Category |
|---|---|
| `P01_manage_twins` | `K01_new_twin_first_parts` |
| `P02_mark_up_the_body` | `K02_open_and_add_to_a_twin` |
| `P03_organise_into_groups` | `K03_receive_a_twin` |
| `P04_look_at_the_twin` | `K04_document_becomes_a_twin` |
| `P05_describe_and_report` | `K05_report_on_a_version` |
| `P06_exchange_twins` | |
| `P07_app_frame` | |

They are written once as constants and used from there, so a typo is a compile error rather than a
test that quietly drops out of the map:

```csharp
[Category(Processes.MarkUpTheBody)]
public class UndoRedoPlayModeTests : PlayModeTestBase
```

A guard test walks both test assemblies and fails when a test carries no category, or more than
one — that is what keeps this document honest when someone adds a test in six months.

`NoAPICalls/` is a promise about **cost**, not about failing: every test that spends tokens guards
itself with `Assert.Ignore` when there is no key, so a run without a key was green wherever the
test sat. With a key it was not free — one test in `NoAPICalls/` really called the model and took
26 seconds of every offline run. Three files were moved into `APICalls/` for that reason
(`OpenAIClientTests` and `DocumentMappingApiTests`, which sat beside the folders, and
`PartsDescriptionProcessTests`, which sat in `NoAPICalls/`).

Tools that are driven through the test runner rather than run with the suite — the template
library generator — mark themselves `[Explicit]` and are outside the landscape.

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

---

## Not in this document

Purely technical tests without a business-visible flow: the schema builder for structured model
answers, the key lookup, and the token and session handling of the twin server. They are listed in
`PlayMode/TESTS_OVERVIEW.md`, which stays the technical inventory.

## Where the tests sit today

| Address | Tests |
|---|---|
| `P01_manage_twins` | 25 |
| `P02_mark_up_the_body` | 30 |
| `P03_organise_into_groups` | 16 |
| `P04_look_at_the_twin` | **1** |
| `P05_describe_and_report` | 19 |
| `P06_exchange_twins` | 36 |
| `P07_app_frame` | 2 |
| `T00_technical` | 25 |
| `K01_new_twin_first_parts` | 1 |
| `K05_report_on_a_version` | 1 |
| **total** | **156** |

By cost: 88 in `EditMode/`, 53 in `PlayMode/NoAPICalls/`, 15 in `PlayMode/APICalls/`.

The single test behind `P04_look_at_the_twin` is the number to look at: turning the twin, storing a
view and activating one again are what the app is for, and one test stands behind all of it. K02,
K03 and K04 have no test at all yet.

**Counted on 11 September 2026**, by reflection over both test assemblies — the same walk the guard
test makes.
