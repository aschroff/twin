using System;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

public class StringLocalizer
{
    public static string localizeString(string key)
    {
        string tableName = "TwinLocalTables";
        // Load the specific String Table by its name
        var stringTable = LocalizationSettings.StringDatabase.GetTable(tableName);


        if (stringTable != null)
        {

            // Retrieve the entry by key
            StringTableEntry entry = stringTable.GetEntry(key);

            if (entry != null)
            {
                // Access the localized value
                string localizedValue = entry.GetLocalizedString();
                //text.text = localizedValue;
                Debug.Log("Localized value: " + localizedValue);
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
