using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public class StringLocalizer
{
    public static string localizeString(string key)
    {
        string tableName = "TwinLocalTables";
        // Ask the database, not the table: only the database follows the fallback chain
        // (demedlatin → demed → de → en, enmed → en) when an entry is missing or empty
        var result = LocalizationSettings.StringDatabase.GetTableEntry(tableName, key);


        if (result.Table != null)
        {

            // Retrieve the entry by key
            StringTableEntry entry = result.Entry;

            if (entry != null)
            {
                // Access the localized value
                string localizedValue = entry.GetLocalizedString();
                return localizedValue;
            }
            else
            {
                Debug.LogWarning("Entry not found for key: " + key);
                return key;
            }
        }
        else
        {
            Debug.LogError("Failed to load table: " + tableName);
            return key;
        }
    }
}
