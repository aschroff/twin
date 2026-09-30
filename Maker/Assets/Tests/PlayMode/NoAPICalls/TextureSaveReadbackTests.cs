using System.Collections;
using NUnit.Framework;
using PaintCore;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Whether the paint texture can be written out without a gigabyte of working memory.
    /// </summary>
    /// <remarks>
    /// <para>Saving costs 1024 MB today and kills the app on an iPad. Routing the pixels through a
    /// <c>Texture2D</c> is what makes it expensive: that object holds the pixels in system memory
    /// and on the GPU at the same time, on top of the render texture used to reach them.</para>
    ///
    /// <para><b>A cheaper save is worthless if the image changes.</b> The user has ruled out
    /// anything that alters how markings look, so this compares the two images pixel by pixel at
    /// sample points before reporting what was saved.</para>
    /// </remarks>
    [Category(Processes.ManageTwins)]
    public class TextureSaveReadbackTests : TwinPaintTestBase
    {
        [UnityTest]
        public IEnumerator ReadingBackDirectly_IsCheaperAndGivesTheSameImage()
        {
            yield return ResetApp();
            yield return SelectTwin("Torso");

            CwPaintableTexture paintable = Object.FindFirstObjectByType<CwPaintableTexture>();
            Assert.IsNotNull(paintable, "No paintable texture in the scene.");
            int width = paintable.Width;
            int height = paintable.Height;

            System.GC.Collect();
            yield return null;

            // --- the way the app does it today -------------------------------------------------
            long beforeOld = MemoryMeasure.Total();
            Texture2D copy = paintable.GetReadableCopy();
            byte[] pngOld = copy.EncodeToPNG();
            long costOld = MemoryMeasure.Total() - beforeOld;

            Color32[] pixelsOld = copy.GetPixels32();
            Object.Destroy(copy);
            yield return null;
            System.GC.Collect();
            yield return null;

            // --- straight off the GPU, no Texture2D in between ---------------------------------
            long beforeNew = MemoryMeasure.Total();

            AsyncGPUReadbackRequest request =
                AsyncGPUReadback.Request(paintable.Current, 0, TextureFormat.RGBA32);
            while (request.done == false) yield return null;
            Assert.IsFalse(request.hasError, "The GPU readback failed.");

            NativeArray<byte> raw = request.GetData<byte>();
            NativeArray<byte> encoded = ImageConversion.EncodeNativeArrayToPNG(
                raw, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height);
            byte[] pngNew = encoded.ToArray();
            long costNew = MemoryMeasure.Total() - beforeNew;
            encoded.Dispose();

            // --- do the two agree? -------------------------------------------------------------
            // every pixel, not a sample: the user has ruled out anything that changes how
            // markings look, so "probably identical" is not good enough
            int mismatches = 0;
            int pixels = width * height;
            for (int i = 0; i < pixels; i++)
            {
                Color32 was = pixelsOld[i];
                int offset = i * 4;
                if (raw[offset + 0] != was.r || raw[offset + 1] != was.g
                    || raw[offset + 2] != was.b || raw[offset + 3] != was.a)
                {
                    mismatches++;
                }
            }

            Debug.Log("[readback]\n"
                      + "  today  (Texture2D route) : " + MemoryMeasure.Signed(costOld)
                      + "   PNG " + MemoryMeasure.Mb(pngOld.LongLength) + "\n"
                      + "  direct (GPU readback)    : " + MemoryMeasure.Signed(costNew)
                      + "   PNG " + MemoryMeasure.Mb(pngNew.LongLength) + "\n"
                      + "  saved                    : " + MemoryMeasure.Signed(costOld - costNew) + "\n"
                      + "  pixels compared          : " + pixels
                      + ", mismatches: " + mismatches);

            Assert.AreEqual(0, mismatches,
                "The cheaper route produced a different image — that would change how markings look.");
            Assert.Greater(pngNew.Length, 0, "The cheaper route produced an empty image.");
        }
    }
}
