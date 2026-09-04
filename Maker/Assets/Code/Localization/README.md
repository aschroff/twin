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
additionally uses Latin anatomy. As of writing, 112 of 179 keys differ between
`de` and `demed`, and 99 between `demed` and `demedlatin` — almost all of the
latter are `region.*`.

**What this means for a new key.** If its text names a domain concept — twin,
group, view, edit, a body part — it needs different wording per register. If it
names something outside the domain — account, password, server, a date — the
same value belongs in all five tables, and that is not laziness.

That distinction is easy to get wrong in one direction: `LOGIN_TITLE` was first
drafted as "Twin account" / "Twin-Konto", which contains exactly the word that
`demed` replaces with "Patient". Since it is the clinician's account and not the
patient's, "Patient account" would have been wrong too — so the title avoids the
word entirely and reads "Account" / "Konto".

## 2. There is no fallback

`m_UseFallback` is `0` for both the string and the asset database, and every
Locale asset has empty metadata, so there is no fallback chain either. `demed`
inherits **nothing** from `de`.

A key that is missing in one table is not silently filled from another one. The
user sees, literally:

```
No translation found for 'IMPORT_TWIN' in TwinLocalTables
```

So: fill every new key in all five tables. It currently looks as if fallbacks
exist, only because all keys happen to be filled.

## 3. Adding a key

**For static text in a prefab** — a label, a button caption, a placeholder: add
a `LocalizeStringEvent` to the `Text`, point it at the collection, and add a
dynamic listener to `Text.text`. Reference the entry by its **key ID**, not by
name; a name reference breaks the moment someone renames the key. `APP_RESET` on
the settings page is the example to copy.

**For text the code picks** — status lines, error messages: keep the key in the
code and resolve it when you need it.

```csharp
private const string TableName = "TwinLocalTables";
private const string KeySignedIn = "LOGIN_SIGNED_IN";

var table = LocalizationSettings.StringDatabase?.GetTable(TableName);
var entry = table?.GetEntry(KeySignedIn);
```

Do **not** use `StringLocalizer.localizeString` for this: it writes a line to the
console on every *successful* lookup, which turns a status line into console
noise. `TwinLoginPanel.Localise` shows the shape without that.

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
