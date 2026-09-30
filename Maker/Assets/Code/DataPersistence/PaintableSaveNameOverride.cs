using System;
using System.Collections.Generic;
using PaintCore;
using UnityEngine;

public static class PaintableSaveNameOverride
{
    private static readonly Dictionary<CwPaintableTexture, string> OriginalNames = new Dictionary<CwPaintableTexture, string>();

    /// <summary>Every save name handed out while active, so they can all be removed again.</summary>
    private static readonly HashSet<string> Issued = new HashSet<string>();
    private static string prefix;
    private static bool active;

    public static IDisposable Begin(string testPrefix)
    {
        if (string.IsNullOrEmpty(testPrefix))
        {
            throw new ArgumentException("Test prefix must be non-empty.", nameof(testPrefix));
        }

        if (active)
        {
            End();
        }

        prefix = testPrefix;
        active = true;

        CwPaintableTexture.OnInstanceAdded += HandleInstanceAdded;
        ApplyToExisting();

        return new Scope();
    }

    /// <summary>
    /// The name the painted texture of <paramref name="profile"/> is stored under.
    /// </summary>
    /// <remarks>
    /// <para>Everything that reads, writes, compares or deletes a paint save name has to go
    /// through here. The painted texture does not live in the data directory - it lives in
    /// PlayerPrefs, which is one store per application and cannot be redirected the way
    /// <see cref="DataPaths"/> redirects the directory. Without this, a test that switches twins
    /// reads and overwrites the real user's paintings: the key it touches is simply the twin's
    /// name, the same one the installed app uses.</para>
    ///
    /// <para>Outside a test nothing is active and the profile is returned unchanged.</para>
    /// </remarks>
    public static string Resolve(string profile)
    {
        if (!active)
        {
            return profile;
        }

        string resolved = string.IsNullOrEmpty(profile) ? prefix : prefix + "_" + profile;
        Issued.Add(resolved);
        return resolved;
    }

    public static void End()
    {
        if (!active)
        {
            return;
        }

        active = false;
        CwPaintableTexture.OnInstanceAdded -= HandleInstanceAdded;

        // PlayerPrefs is one store for the whole application and nothing else clears it, so
        // without this the painted textures of one test would still be there for the next one -
        // which is the same trap as before, only between tests instead of against the real user.
        foreach (var name in Issued)
        {
            PlayerPrefs.DeleteKey(name);
        }
        Issued.Clear();
        PlayerPrefs.Save();

        foreach (var entry in OriginalNames)
        {
            if (entry.Key != null)
            {
                entry.Key.SaveName = entry.Value;
            }
        }

        OriginalNames.Clear();
        prefix = null;
    }

    private static void HandleInstanceAdded(CwPaintableTexture texture)
    {
        if (!active || texture == null)
        {
            return;
        }

        ApplyTo(texture);
    }

    private static void ApplyToExisting()
    {
        var textures = UnityEngine.Object.FindObjectsOfType<CwPaintableTexture>(true);
        foreach (var texture in textures)
        {
            ApplyTo(texture);
        }
    }

    private static void ApplyTo(CwPaintableTexture texture)
    {
        if (texture == null)
        {
            return;
        }

        if (!OriginalNames.ContainsKey(texture))
        {
            OriginalNames[texture] = texture.SaveName;
        }

        var original = texture.SaveName ?? string.Empty;
        texture.SaveName = Resolve(original);
    }

    private sealed class Scope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            End();
        }
    }
}
