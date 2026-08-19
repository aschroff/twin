using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

/// <summary>
/// Imports the body region names from Assets/Resources/region_names.tsv into the
/// TwinLocalTables string tables, one entry per region (key "region.&lt;region key&gt;").
///
/// The TSV is generated from Assets/Resources/BODY_REGIONS.md and is the place to edit
/// wording. Re-running the import updates existing entries and adds missing ones; it never
/// removes anything.
///
/// Also creates the "demedlatin" locale (German with Latin anatomical terms) on first run:
/// its table starts as a copy of demed, and only the region entries carry Latin wording.
/// </summary>
public static class RegionNamesImporter
{
    private const string TableCollectionName = "TwinLocalTables";
    private const string KeyPrefix = "region.";
    private const string TsvPath = "Assets/Resources/region_names.tsv";
    private const string LatinCode = "demedlatin";
    private const string LatinName = "GermanMediLatin (demedlatin)";
    private const string LocalesFolder = "Assets/Localization/Locales";

    /// <summary>TSV column -> locale code. en/de get the medical wording as a fallback so no
    /// locale shows raw region keys.</summary>
    private static readonly (string column, string[] locales)[] ColumnLocales =
    {
        ("enmed", new[] { "enmed", "en" }),
        ("demed", new[] { "demed", "de" }),
        ("demedlatin", new[] { LatinCode }),
    };

    [MenuItem("Tools/Localization/Import Region Names")]
    public static void Import()
    {
        List<string[]> rows = ReadTsv(out string[] header);
        if (rows == null)
        {
            return;
        }

        EnsureLatinLocale();

        var collection = LocalizationEditorSettings.GetStringTableCollection(TableCollectionName);
        if (collection == null)
        {
            Debug.LogError($"[RegionNames] String table collection '{TableCollectionName}' not found.");
            return;
        }

        EnsureLatinTableFromDemed(collection);

        SharedTableData shared = collection.SharedData;
        int added = 0;
        int updated = 0;

        foreach ((string column, string[] locales) in ColumnLocales)
        {
            int columnIndex = System.Array.IndexOf(header, column);
            if (columnIndex < 0)
            {
                Debug.LogWarning($"[RegionNames] Column '{column}' missing in {TsvPath} — skipped.");
                continue;
            }

            foreach (string localeCode in locales)
            {
                StringTable table = FindTable(collection, localeCode);
                if (table == null)
                {
                    Debug.LogWarning($"[RegionNames] No table for locale '{localeCode}' — skipped.");
                    continue;
                }

                foreach (string[] row in rows)
                {
                    if (row.Length <= columnIndex)
                    {
                        continue;
                    }
                    string key = KeyPrefix + row[0];
                    string value = row[columnIndex];
                    if (string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    if (shared.GetEntry(key) == null)
                    {
                        shared.AddKey(key);
                    }

                    StringTableEntry entry = table.GetEntry(key);
                    if (entry == null)
                    {
                        table.AddEntry(key, value);
                        added++;
                    }
                    else if (entry.Value != value)
                    {
                        entry.Value = value;
                        updated++;
                    }
                }
                EditorUtility.SetDirty(table);
            }
        }

        EditorUtility.SetDirty(shared);
        AssetDatabase.SaveAssets();
        Debug.Log($"[RegionNames] Imported {rows.Count} regions: {added} entries added, {updated} updated.");
    }

    private static List<string[]> ReadTsv(out string[] header)
    {
        header = null;
        string full = Path.Combine(Application.dataPath, "..", TsvPath);
        if (!File.Exists(full))
        {
            Debug.LogError($"[RegionNames] {TsvPath} not found.");
            return null;
        }

        string[] lines = File.ReadAllLines(full);
        if (lines.Length < 2)
        {
            Debug.LogError($"[RegionNames] {TsvPath} has no data rows.");
            return null;
        }

        header = lines[0].Split('\t');
        var rows = new List<string[]>();
        for (int i = 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }
            rows.Add(lines[i].Split('\t'));
        }
        return rows;
    }

    private static void EnsureLatinLocale()
    {
        foreach (Locale existing in LocalizationEditorSettings.GetLocales())
        {
            if (existing.Identifier.Code == LatinCode)
            {
                return;
            }
        }

        Locale locale = Locale.CreateLocale(new LocaleIdentifier(LatinCode));
        locale.name = LatinName;
        locale.LocaleName = LatinName;
        Directory.CreateDirectory(LocalesFolder);
        AssetDatabase.CreateAsset(locale, $"{LocalesFolder}/{LatinName}.asset");
        LocalizationEditorSettings.AddLocale(locale);
        Debug.Log($"[RegionNames] Created locale '{LatinCode}'.");
    }

    /// <summary>The Latin table is a copy of demed — only the region entries get Latin wording
    /// (done by the import itself), everything else stays identical to German. Copies by key id
    /// and only what is missing, so a re-run completes an interrupted copy.</summary>
    private static void EnsureLatinTableFromDemed(StringTableCollection collection)
    {
        if (FindTable(collection, LatinCode) == null)
        {
            collection.AddNewTable(new LocaleIdentifier(LatinCode));
        }

        StringTable latin = FindTable(collection, LatinCode);
        StringTable source = FindTable(collection, "demed");
        if (latin == null || source == null)
        {
            Debug.LogError($"[RegionNames] Missing '{LatinCode}' or 'demed' table.");
            return;
        }

        int copied = 0;
        foreach (var pair in source)
        {
            StringTableEntry sourceEntry = pair.Value;
            if (sourceEntry == null || string.IsNullOrEmpty(sourceEntry.LocalizedValue))
            {
                continue;
            }
            if (latin.GetEntry(sourceEntry.KeyId) != null)
            {
                continue;
            }
            latin.AddEntry(sourceEntry.KeyId, sourceEntry.LocalizedValue);
            copied++;
        }
        if (copied > 0)
        {
            EditorUtility.SetDirty(latin);
            Debug.Log($"[RegionNames] Copied {copied} entries from demed into '{LatinCode}'.");
        }
    }

    private static StringTable FindTable(StringTableCollection collection, string localeCode)
    {
        foreach (var table in collection.StringTables)
        {
            if (table != null && table.LocaleIdentifier.Code == localeCode)
            {
                return table;
            }
        }
        return null;
    }
}
