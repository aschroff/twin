using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaintCore;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The language switch in Settings: five locales behind one dropdown, chosen by position
    /// (see <c>Assets/Code/Localization/README.md</c>).
    /// </summary>
    /// <remarks>
    /// <para>The selected locale is static and outlives the scene, so every test puts English
    /// back in its teardown — a test left in German would hand the next one a German app.</para>
    /// <para>A language is chosen the way a person chooses it: open the dropdown, click the
    /// entry. Setting <c>dropdown.value</c> would reach the same handler, but would not notice an
    /// option list that no longer matches the options.</para>
    /// </remarks>
    [Category(Processes.AppFrame)]
    public class LanguagePlayModeTests : TwinPaintTestBase
    {
        const string LanguageDropdown = "Canvas/Settings UI/SettingsPanel/Dropdown - Light";
        const string ResetLabel = "Canvas/Settings UI/SettingsPanel/Reset/Text";
        const string LoginStatus = "Canvas/Settings UI/SettingsPanel/Twin Account/Status Text";
        const string SettingsBack = "Canvas/Settings UI/Back Button";
        const string SaveBack = "Canvas/Save UI/Top/GameObject/Back Button";
        const string EditBack = "Canvas/Edit UI/Top/GameObject/Back Button";
        const string TableName = "TwinLocalTables";

        /// <summary>Index → locale, as saved in <c>ConfigData.languageID</c>. Changing this order
        /// changes what every saved twin means, which is why it is spelled out here.</summary>
        static readonly string[] LocaleCodes = { "en", "enmed", "de", "demed", "demedlatin" };
        static readonly string[] DropdownLabels =
            { "English", "Medical English", "Deutsch", "Medizinerdeutsch", "Deutsch (Medizin Latein)" };

        // anchors per locale, in the order of LocaleCodes: APP_RESET differs by language only,
        // GROUPS also by register
        static readonly string[] ResetCaptions =
            { "Reset App", "Reset App", "App zurücksetzen", "App zurücksetzen", "App zurücksetzen" };
        static readonly string[] GroupsCaptions = { "Groups", "Folders", "Gruppen", "Akten", "Akten" };

        const int English = 0;
        const int German = 2;
        const int MedicalGerman = 3;

        [UnityTearDown]
        public override IEnumerator TearDown()
        {
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[English];
            yield return base.TearDown();
        }

        [UnityTest]
        public IEnumerator LanguageDropdown_OffersTheLocalesInTheirOrder()
        {
            CollectionAssert.AreEqual(LocaleCodes,
                LocalizationSettings.AvailableLocales.Locales.Select(locale => locale.Identifier.Code).ToList(),
                "The available locales changed order or number — every saved languageID now means another language.");

            yield return OpenSettings();
            TMP_Dropdown dropdown = TheDropdown();
            CollectionAssert.AreEqual(DropdownLabels, dropdown.options.Select(option => option.text).ToList(),
                "The dropdown's options no longer match the locales by position.");
            Assert.AreEqual(English, dropdown.value, "A fresh app should show English as the chosen language.");
            Assert.AreEqual("en", LocalizationSettings.SelectedLocale.Identifier.Code, "A fresh app should start in English.");
        }

        [UnityTest]
        public IEnumerator ChoosingALanguage_SwitchesTheAppAndItsLabels()
        {
            for (int index = LocaleCodes.Length - 1; index >= 0; index--)
            {
                yield return ChooseLanguage(index);

                Assert.AreEqual(index, TheDropdown().value, $"The dropdown should show {DropdownLabels[index]} as chosen.");
                yield return WaitForText(ResetLabel, ResetCaptions[index]);

                yield return ClickButtonByPath(SettingsBack);
                yield return WaitForModeActive("Main");
                Text groups = ActiveLabelOf("GROUPS");
                yield return WaitForText(GetTransformPath(groups.transform), GroupsCaptions[index]);
            }
        }

        /// <summary>A key filled in no table along a locale's fallback chain shows as "No
        /// translation found" in that language only. This walks every screen in every language and
        /// compares each localized label with its entry — its own, or the fallback's when it has
        /// none.</summary>
        [UnityTest]
        public IEnumerator EveryLabelOnEveryScreen_ShowsItsEntryInEveryLanguage()
        {
            for (int index = 0; index < LocaleCodes.Length; index++)
            {
                yield return ChooseLanguage(index);
                yield return AssertLabelsShowTheirEntries("Settings", LocaleCodes[index]);

                yield return ClickButtonByPath(SettingsBack);
                yield return WaitForModeActive("Main");
                yield return AssertLabelsShowTheirEntries("Main", LocaleCodes[index]);

                yield return ClickButtonByName("Save Button");
                yield return WaitForModeActive("Save");
                yield return AssertLabelsShowTheirEntries("Save", LocaleCodes[index]);
                yield return ClickButtonByPath(SaveBack);
                yield return WaitForModeActive("Main");

                yield return ClickButtonByName("Edit Button");
                yield return WaitForModeActive("Edit");
                yield return AssertLabelsShowTheirEntries("Edit", LocaleCodes[index]);
                yield return ClickButtonByPath(EditBack);
                yield return WaitForModeActive("Main");
            }
        }

        /// <summary>Menu entries — the Help, Menu, Part and Upload lists — are built at runtime by
        /// <c>MenuManager</c>, which looks each entry's <c>text</c> up as a key and shows the raw
        /// text when there is none. A text that is not a key reads like an untranslated English
        /// label, and no LocalizeStringEvent exists for the walk above to find. An entry left
        /// empty in a register is fine as long as its fallback has one.</summary>
        [UnityTest]
        public IEnumerator EveryMenuEntry_HasAnEntryInEveryLanguage()
        {
            yield return null;
            MenuManager[] menus = Resources.FindObjectsOfTypeAll<MenuManager>()
                .Where(menu => menu.gameObject.scene.IsValid())
                .ToArray();
            Assert.IsNotEmpty(menus, "Setup: the scene has no MenuManager.");

            var missing = new List<string>();
            foreach (MenuManager menu in menus)
            {
                foreach (MenuAction action in menu.menu.Values)
                {
                    foreach (string localeCode in LocaleCodes)
                    {
                        if (Expected(action.text, localeCode) == null)
                        {
                            missing.Add($"{GetTransformPath(menu.transform)}: '{action.text}' has no entry in {localeCode} or its fallbacks");
                        }
                    }
                }
            }
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        /// <summary>Text set from code goes through <c>StringLocalizer</c>, labels through the
        /// string database. Both have to follow the same fallback chain — otherwise an entry left
        /// empty in <c>demed</c> shows German on a label and nothing at all in a status line.</summary>
        [UnityTest]
        public IEnumerator TextSetFromCode_FallsBackLikeTheLabels()
        {
            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            var wrong = new List<string>();
            foreach (string localeCode in LocaleCodes)
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
                yield return null;
                foreach (var shared in table.SharedData.Entries)
                {
                    string expected = Expected(shared.Key, localeCode);
                    if (expected == null) continue; // a gap in every table, not the localizer's business
                    string shown = StringLocalizer.localizeString(shared.Key);
                    if (shown != expected)
                    {
                        wrong.Add($"{localeCode} '{shared.Key}': localizer gives '{shown}', fallback chain gives '{expected}'");
                    }
                }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        /// <summary>Four places look text up without <c>StringLocalizer</c>: the region names and
        /// the labels of the two group-detail buttons and the description summary. Every table is
        /// filled for their keys today, so the gap is made here — one register entry emptied in
        /// memory for the length of the test — and each lookup has to answer with its
        /// fallback's text.</summary>
        [UnityTest]
        public IEnumerator DirectLookups_FallBackWhenARegisterIsEmpty()
        {
            var cases = new (string locale, string key, System.Func<string> lookup)[]
            {
                ("demedlatin", RegionNames.KeyPrefix + "chest_left", () => RegionNames.Get("chest_left")),
                ("demed", PartsDescriptionButton.LabelKeyAll, () => Localise(typeof(PartsDescriptionButton), PartsDescriptionButton.LabelKeyAll)),
                ("demed", "GROUP_DETAILS_CREATE_IMAGES", () => Localise(typeof(MissingScreenshotsButton), "GROUP_DETAILS_CREATE_IMAGES")),
                ("enmed", Code.PartsDescriptionProcess.SummaryKey, () => Localise(typeof(Code.PartsDescriptionProcess), Code.PartsDescriptionProcess.SummaryKey)),
            };

            var wrong = new List<string>();
            foreach (var (localeCode, key, lookup) in cases)
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
                yield return null;

                var entry = LocalizationSettings.StringDatabase.GetTable(TableName).GetEntry(key);
                Assert.IsNotNull(entry, $"Setup: '{key}' has no entry in {localeCode} to empty.");
                string own = entry.Value;
                entry.Value = "";
                try
                {
                    string expected = Expected(key, localeCode);
                    Assert.IsFalse(string.IsNullOrEmpty(expected), $"Setup: '{key}' has nothing to fall back to from {localeCode}.");
                    string shown = lookup();
                    if (shown != expected) wrong.Add($"{localeCode} '{key}' left empty: lookup gives '{shown}', fallback is '{expected}'");
                }
                finally
                {
                    entry.Value = own;
                }
            }
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
        }

        /// <summary>The class's own private <c>Localise(string)</c>.</summary>
        static string Localise(System.Type owner, string key)
        {
            var method = owner.GetMethod("Localise",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, $"Setup: {owner.Name} has no Localise(string) any more.");
            return (string)method.Invoke(null, new object[] { key });
        }

        /// <summary>The sign-in status is set from code, not by a LocalizeStringEvent, and sits on
        /// the very page where the language is changed — it has to follow without reopening.</summary>
        [UnityTest]
        public IEnumerator SignInStatus_FollowsTheLanguageWhileOnScreen()
        {
            yield return OpenSettings();
            string english = FindGameObjectByPath(LoginStatus).GetComponent<Text>().text;
            string key = new[] { "LOGIN_SIGNED_OUT", "LOGIN_SIGNED_IN" }
                .FirstOrDefault(candidate => Entry(candidate, "en") == english);
            Assert.IsNotNull(key, $"Setup: the status line '{english}' is neither the signed-out nor the signed-in text.");

            yield return ChooseLanguage(German);
            yield return WaitForText(LoginStatus, Entry(key, "de"));

            yield return ChooseLanguage(English);
            yield return WaitForText(LoginStatus, english);
        }

        [UnityTest]
        public IEnumerator ChosenLanguage_SurvivesARestart()
        {
            yield return ChooseLanguage(MedicalGerman);
            yield return ClickButtonByPath(SettingsBack);
            yield return WaitForModeActive("Main");

            yield return RestartApp();

            Assert.AreEqual(LocaleCodes[MedicalGerman], LocalizationSettings.SelectedLocale.Identifier.Code,
                "The app should start again in the language chosen before quitting.");
            yield return WaitForText(GetTransformPath(ActiveLabelOf("GROUPS").transform), GroupsCaptions[MedicalGerman]);
            yield return OpenSettings();
            Assert.AreEqual(MedicalGerman, TheDropdown().value, "Settings should show the restored language as chosen.");
        }

        IEnumerator OpenSettings()
        {
            if (FindGameObjectByPath(LanguageDropdown).activeInHierarchy) yield break;
            yield return ClickButtonByName("Settings Button");
            yield return WaitForModeActive("Settings");
        }

        TMP_Dropdown TheDropdown() => FindGameObjectByPath(LanguageDropdown).GetComponent<TMP_Dropdown>();

        /// <summary>Opens the dropdown and clicks the entry, then waits until the locale arrived.</summary>
        IEnumerator ChooseLanguage(int index)
        {
            yield return OpenSettings();
            TMP_Dropdown dropdown = TheDropdown();
            dropdown.Show();
            yield return null;

            Transform list = dropdown.transform.Find("Dropdown List");
            Assert.IsNotNull(list, "Opening the language dropdown did not show its list.");
            Toggle entry = list.GetComponentsInChildren<Toggle>()
                .FirstOrDefault(toggle => toggle.GetComponentInChildren<TMP_Text>().text == DropdownLabels[index]);
            Assert.IsNotNull(entry, $"The open dropdown has no entry '{DropdownLabels[index]}'.");
            ExecuteEvents.Execute(entry.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

            float elapsed = 0f;
            while (LocalizationSettings.SelectedLocale.Identifier.Code != LocaleCodes[index] && elapsed < 5f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(LocaleCodes[index], LocalizationSettings.SelectedLocale.Identifier.Code,
                $"Choosing '{DropdownLabels[index]}' did not switch the app to {LocaleCodes[index]}.");

            // the list fades out before it is destroyed; the next Show must not find the old one
            while (dropdown.transform.Find("Dropdown List") != null && elapsed < 10f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        /// <summary>Quits the way the app quits — the config is saved — and starts it again on
        /// the same data. The locale is put back to English first, so only loading can bring the
        /// chosen one back.</summary>
        IEnumerator RestartApp()
        {
            DataPersistenceManager.instance.SaveConfig();
            yield return SceneManager.LoadSceneAsync("EmptyScene", LoadSceneMode.Single);
            CwSerialization.HashToModel.Clear();
            CwSerialization.ModelToHash.Clear();
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[English];
            yield return null;

            yield return LoadAppScene();
            yield return WaitForModeActive("Main");
            yield return WaitWhileLoading();
        }

        IEnumerator WaitForText(string path, string expected, float timeout = 5f)
        {
            Text text = FindGameObjectByPath(path).GetComponent<Text>();
            float elapsed = 0f;
            while (text.text != expected && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(expected, text.text, $"'{path}' in {LocalizationSettings.SelectedLocale.Identifier.Code}.");
        }

        /// <summary>The first label on screen that shows the given key.</summary>
        static Text ActiveLabelOf(string key)
        {
            Text label = Object.FindObjectsOfType<LocalizeStringEvent>()
                .Where(localized => KeyOf(localized) == key)
                .Select(TargetOf)
                .OfType<Text>()
                .FirstOrDefault();
            Assert.IsNotNull(label, $"No label on screen shows '{key}'.");
            return label;
        }

        /// <summary>Compares every active localized label with its entry for the locale, retrying
        /// for a moment since labels update after the locale event, then names all that differ.</summary>
        IEnumerator AssertLabelsShowTheirEntries(string screen, string localeCode)
        {
            List<string> wrong = null;
            int checkedLabels = 0;
            float elapsed = 0f;
            do
            {
                yield return null;
                elapsed += Time.deltaTime;
                wrong = new List<string>();
                checkedLabels = 0;
                foreach (LocalizeStringEvent localized in Object.FindObjectsOfType<LocalizeStringEvent>())
                {
                    if (!localized.isActiveAndEnabled) continue;
                    string shown = ShownText(TargetOf(localized));
                    if (shown == null) continue;
                    checkedLabels++;

                    string key = KeyOf(localized);
                    string expected = key == null ? null : Expected(key, localeCode);
                    if (localized.StringReference.IsEmpty)
                    {
                        wrong.Add($"{GetTransformPath(localized.transform)}: shows '{shown}' in every language — its LocalizeStringEvent names no key");
                    }
                    else if (expected == null)
                    {
                        wrong.Add($"{GetTransformPath(localized.transform)}: no entry for '{key ?? localized.StringReference.TableEntryReference.ToString()}' in this language or its fallbacks");
                    }
                    else if (shown != expected)
                    {
                        wrong.Add($"{GetTransformPath(localized.transform)} ({key}): shows '{shown}', entry is '{expected}'");
                    }
                }
            } while (wrong.Count > 0 && elapsed < 3f);

            Assert.Greater(checkedLabels, 0, $"Setup: the {screen} screen shows no localized label at all.");
            Assert.IsEmpty(wrong, $"{screen} screen in {localeCode}:\n" + string.Join("\n", wrong));
        }

        /// <summary>The key a label refers to, whether it was set by id or by name.</summary>
        static string KeyOf(LocalizeStringEvent localized)
        {
            if (localized.StringReference.TableReference.TableCollectionName != TableName) return null;
            var table = LocalizationSettings.StringDatabase.GetTable(TableName);
            var entry = localized.StringReference.TableEntryReference;
            return entry.ResolveKeyName(table?.SharedData);
        }

        static Object TargetOf(LocalizeStringEvent localized)
        {
            for (int i = 0; i < localized.OnUpdateString.GetPersistentEventCount(); i++)
            {
                Object target = localized.OnUpdateString.GetPersistentTarget(i);
                if (target is Text || target is TMP_Text) return target;
            }
            // an event that writes nowhere still means "this label is localized" — what the
            // label shows then is whatever the prefab says, in every language
            if (localized.TryGetComponent(out Text text)) return text;
            return localized.TryGetComponent(out TMP_Text tmpText) ? tmpText : null;
        }

        static string ShownText(Object target) => target switch
        {
            Text text => text.text,
            TMP_Text text => text.text,
            _ => null,
        };

        /// <summary>What a locale should show for a key: its own entry, or — when that is missing
        /// or empty — the first filled one along its fallback chain (<c>demedlatin → demed → de →
        /// en</c>, <c>enmed → en</c>), as Unity walks it. Null when no table on the way has one.
        /// Walked here by hand rather than asked of the database, which would answer a gap with
        /// its "No translation found" text.</summary>
        static string Expected(string key, string localeCode)
        {
            var queue = new Queue<UnityEngine.Localization.Locale>();
            var seen = new HashSet<UnityEngine.Localization.Locale>();
            queue.Enqueue(LocalizationSettings.AvailableLocales.GetLocale(localeCode));
            while (queue.Count > 0)
            {
                var locale = queue.Dequeue();
                if (locale == null || !seen.Add(locale)) continue;
                string value = Entry(key, locale.Identifier.Code);
                if (!string.IsNullOrEmpty(value)) return value;
                foreach (var fallback in locale.GetFallbacks()) queue.Enqueue(fallback);
            }
            return null;
        }

        /// <summary>The table's value for a key in one locale, or null when that table lacks it.</summary>
        static string Entry(string key, string localeCode)
        {
            var locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
            var table = LocalizationSettings.StringDatabase.GetTable(TableName, locale);
            return table?.GetEntry(key)?.GetLocalizedString();
        }
    }
}
