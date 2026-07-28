using UnityEngine;

public static class DataPaths
{
    private static string persistentDataPathOverride;

    public static string PersistentDataPath
    {
        get
        {
            if (!string.IsNullOrEmpty(persistentDataPathOverride))
            {
                return persistentDataPathOverride;
            }

            return Application.persistentDataPath;
        }
    }

    public static void SetPersistentDataPathForTests(string path)
    {
        persistentDataPathOverride = path;
    }

    public static void ClearPersistentDataPathOverride()
    {
        persistentDataPathOverride = null;
    }
}
