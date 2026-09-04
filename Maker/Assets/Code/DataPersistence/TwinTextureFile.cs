using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The painted body texture of a twin, stored as a PNG next to its config.
///
/// While the app runs, the painted texture is cached per twin (CwPaintableTexture.Save writes
/// it to PlayerPrefs) so switching twins does not have to replay every paint command. That
/// cache never leaves the device though, so a twin that arrives through an import would show
/// up unpainted. Exporting therefore drops the texture into the twin directory, where it is
/// packed into the zip along with the config and the stickers.
/// </summary>
public static class TwinTextureFile
{
    public const string FileName = "Texture.png";

    public static string PathOf(string profileId)
    {
        return Path.Combine(DataPaths.PersistentDataPath, profileId, FileName);
    }

    public static bool Exists(string profileId)
    {
        return string.IsNullOrEmpty(profileId) == false && File.Exists(PathOf(profileId));
    }

    /// <summary>Returns the stored PNG of the twin, or null if it has none.</summary>
    public static byte[] Read(string profileId)
    {
        if (!Exists(profileId))
        {
            return null;
        }
        try
        {
            return File.ReadAllBytes(PathOf(profileId));
        }
        catch (Exception e)
        {
            Debug.LogError("Could not read the texture of twin " + profileId + ".\n" + e);
            return null;
        }
    }

    public static void Write(string profileId, byte[] pngData)
    {
        if (string.IsNullOrEmpty(profileId) || pngData == null || pngData.Length == 0)
        {
            return;
        }
        try
        {
            string fullPath = PathOf(profileId);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, pngData);
        }
        catch (Exception e)
        {
            Debug.LogError("Could not write the texture of twin " + profileId + ".\n" + e);
        }
    }
}
