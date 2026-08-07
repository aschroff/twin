using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Display names of the body regions in the current language. The names live in the
/// TwinLocalTables string tables under "region.&lt;region key&gt;" and are imported from
/// Assets/Resources/region_names.tsv (Tools → Localization → Import Region Names).
///
/// The region key itself stays language independent — it identifies the template.
/// </summary>
public static class RegionNames
{
    public const string KeyPrefix = "region.";
    private const string TableName = "TwinLocalTables";

    /// <summary>Localized name of a region, or the region key if no name is available.</summary>
    public static string Get(string regionKey)
    {
        if (string.IsNullOrEmpty(regionKey))
        {
            return regionKey;
        }

        StringTable table = LocalizationSettings.StringDatabase.GetTable(TableName);
        if (table == null)
        {
            return regionKey;
        }

        StringTableEntry entry = table.GetEntry(KeyPrefix + regionKey);
        if (entry == null || string.IsNullOrEmpty(entry.LocalizedValue))
        {
            return regionKey;
        }
        return entry.LocalizedValue;
    }
}
