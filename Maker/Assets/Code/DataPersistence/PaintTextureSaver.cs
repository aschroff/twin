using PaintCore;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

/// <summary>
/// Writes the painted body texture to its save slot without a gigabyte of working memory.
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> <c>CwPaintableTexture.Save()</c> reaches the pixels by way of a
/// <c>Texture2D</c>: it allocates a full-size render texture, a full-size Texture2D that holds the
/// pixels in system memory <i>and</i> on the GPU, and then encodes. At 8192x8192 that measured
/// <b>1024 MB</b> for a picture that comes out as a 1 MB file. iOS kills the app at about 3000 MB,
/// so a twin switch could put the app over the edge on its own — which is exactly what users saw
/// on an iPad Air.</para>
///
/// <para>Reading straight off the GPU needs one buffer instead of three: <b>257 MB</b>, measured,
/// for a pixel-for-pixel identical image. See <c>TextureSaveReadbackTests</c>, which compares the
/// two routes and fails if a single pixel differs — the markings must look exactly as they did.</para>
///
/// <para>Nothing else changes: the same bytes go to the same save slot under the same name, so
/// twins saved by an older build load unchanged. Hardware that cannot read back asynchronously
/// falls back to the original route, which is slow on memory but correct.</para>
/// </remarks>
public static class PaintTextureSaver
{
    /// <summary>
    /// Saves the texture under its current <see cref="CwPaintableTexture.SaveName"/>.
    /// </summary>
    /// <returns>True if the cheap route was used, false if it fell back to the original one.</returns>
    public static bool Save(CwPaintableTexture texture)
    {
        // the same guard CwPaintableTexture.Save applies, so callers see no behaviour change
        if (texture == null || texture.Activated == false || string.IsNullOrEmpty(texture.SaveName))
        {
            return false;
        }

        RenderTexture source = texture.Current;
        if (source == null || SystemInfo.supportsAsyncGPUReadback == false)
        {
            texture.Save();
            return false;
        }

        AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(source, 0, TextureFormat.RGBA32);
        // blocking on purpose: the caller saves the twin it is leaving and the next one is loaded
        // immediately afterwards, so there is no frame in which a half-saved twin would be correct
        request.WaitForCompletion();

        if (request.hasError == true)
        {
            Debug.LogWarning("Reading the painted texture off the GPU failed; saving the slow way.");
            texture.Save();
            return false;
        }

        NativeArray<byte> pixels = request.GetData<byte>();
        NativeArray<byte> png = ImageConversion.EncodeNativeArrayToPNG(
            pixels, GraphicsFormat.R8G8B8A8_UNorm, (uint)source.width, (uint)source.height);

        try
        {
            CwCommon.SaveBytes(texture.SaveName, png.ToArray());
        }
        finally
        {
            png.Dispose();
        }

        return true;
    }
}
