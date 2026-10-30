# Localization

How text reaches the screen in five locales, and the parts of it that are not
obvious from the code.

Everything lives in one string table collection, `TwinLocalTables`
(`Assets/Tables/`). Locales are in `Assets/Localization/Locales/`, the settings
asset is `Assets/Localization/TwinLocalizationSettings.asset`. Keys are
`SCREAMING_SNAKE_CASE`, grouped by an area prefix — `UPLOAD_*`, `LOGIN_*`,
`region.*`.

---

## 1. Five locales, but two languages

The other three are *registers*: the same language with a different vocabulary
for the same concepts. This is the thing to understand before writing a key.

| Key | `en` | `enmed` | `de` | `demed` | `demedlatin` |
|---|---|---|---|---|---|
| `GROUPS` | Groups | **Folders** | Gruppen | **Akten** | Akten |
| `TWIN` | Twin: | **Patient:** | Twin: | **Patient:** | Patient: |
| `region.forehead` | – | – | Stirn | Stirn | **Regio frontalis** |

`demed` swaps the app's generic vocabulary for clinical wording; `demedlatin`
additionally uses Latin anatomy. As of October 2026, 15 of 212 keys differ between
`de` and `demed` (the domain words: group, view, twin, shape, region, …), and 99 between
`demed` and `demedlatin` — almost all of the latter are `region.*`.

**What this means for a new key.** If its text names a domain concept — twin,
group, view, edit, a body part — it needs different wording per register. If it
names something outside the domain — account, password, server, a date — it
needs `en` and `de` only; the registers take it from there through the fallback
(§2).

That distinction is easy to get wrong in one direction: `LOGIN_TITLE` was first
drafted as "Twin account" / "Twin-Konto", which contains exactly the word that
`demed` replaces with "Patient". Since it is the clinician's account and not the
patient's, "Patient account" would have been wrong too — so the title avoids the
word entirely and reads "Account" / "Konto".

## 2. Fallback: registers inherit from their language

`m_UseFallback` is `1` for the string database (the asset database still has `0`),
and each Locale asset names its fallback in its metadata:

| Locale | Falls back to |
|---|---|
| `demedlatin` | `demed` |
| `demed` | `de` |
| `de` | `en` |
| `enmed` | `en` |

An entry that is **missing or empty** in a table is taken from the next locale on
that chain. So a key whose wording is the same in a register as in its language
only needs `en` and `de`; `enmed`, `demed` and `demedlatin` carry just the words
that differ (`GROUPS` → "Akten", `region.*` in Latin). A value of a single space
is not empty and does not fall back.

Only a key filled in no table along the chain still shows, literally:

```
No translation found for 'IMPORT_TWIN' in TwinLocalTables
```

The fallback lives in `LocalizationSettings.StringDatabase`, not in the tables. A
`LocalizeStringEvent`, `StringLocalizer.localizeString`, `RegionNames.Get` and the
`Localise` helpers of `PartsDescriptionProcess`, `PartsDescriptionButton` and
`MissingScreenshotsButton` go through the database
(`StringDatabase.GetTableEntry(table, key).Entry`) and follow it. Code that reads a
`StringTable` directly with `GetTable(…).GetEntry(key)` does not — don't write new
lookups that way.

The fallback has a price: a register entry that was forgotten no longer shows up
as an error, it quietly shows the plain wording ("Gruppen" instead of "Akten").
For a key that names a domain concept (§1), fill the registers on purpose.

## 3. Adding a key

**For static text in a prefab** — a label, a button caption, a placeholder: add
a `LocalizeStringEvent` to the `Text`, point it at the collection, and add a
dynamic listener to `Text.text`. Reference the entry by its **key ID**, not by
name; a name reference breaks the moment someone renames the key. `APP_RESET` on
the settings page is the example to copy.

**For text the code picks** — status lines, error messages: keep the key in the
code and resolve it with `StringLocalizer.localizeString(key)`
(`Assets/Code/Localization/StringLocalizer.cs`), the shared lookup against
`TwinLocalTables`. It falls back to logging a warning and returning the raw key
if the entry is missing. `TwinLoginPanel.ApplyMessage` and
`TwinNameValidator.InvalidNameMessage`/`AlreadyExistingNameMessage` show the
shape.

If the text stays on screen, also subscribe to
`LocalizationSettings.SelectedLocaleChanged` and re-resolve. A message set before
the language changed will otherwise sit there in the old language — and the
settings page, where the language is switched, is exactly where such messages
live.

## 4. How the language gets chosen

`LanguageSelector.ChangeLocale(int)` takes an **index** into
`LocalizationSettings.AvailableLocales.Locales` and nothing else.
`SettingsManager.GetSelectedLanguage()` passes the dropdown's selected index
straight through, and `LanguageSelector` persists that same integer as
`ConfigData.languageID`.

Today the two orders match:

| Index | Locale | Dropdown |
|---|---|---|
| 0 | `en` | English |
| 1 | `enmed` | Medical English |
| 2 | `de` | Deutsch |
| 3 | `demed` | Medizinerdeutsch |
| 4 | `demedlatin` | Deutsch (Medizin Latein) |

**This coupling is positional and unchecked.** Reordering the dropdown, or
inserting a locale anywhere but at the end, silently changes what every *already
saved* `languageID` means — existing users would come back in a different
language. Add new locales at the end, and keep the dropdown in the same order.

## 5. Addressables

Each locale's table is an addressable asset in its own group, labelled
`Locale-<code>` and `Preload`. The `Preload` label is what gets the table loaded
at startup; without it, a synchronous `GetTable` can come back `null` on first
use.

Unity re-registers these groups by itself when tables change. That is usually
invisible, but it has dropped the `Preload` label once while doing so. After
adding or removing keys, glance at:

```bash
git diff Assets/AddressableAssetsData/
```

Entries and labels should be identical across the five groups; only genuine
changes belong in the commit.
