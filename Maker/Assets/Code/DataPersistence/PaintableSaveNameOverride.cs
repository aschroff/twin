using System;
using System.Collections.Generic;
using PaintCore;
using UnityEngine;

public static class PaintableSaveNameOverride
{
    private static readonly Dictionary<CwPaintableTexture, string> OriginalNames = new Dictionary<CwPaintableTexture, string>();
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

    public static void End()
    {
        if (!active)
        {
            return;
        }

        active = false;
        CwPaintableTexture.OnInstanceAdded -= HandleInstanceAdded;

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
        texture.SaveName = string.IsNullOrEmpty(original) ? prefix : prefix + "_" + original;
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
