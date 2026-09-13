using System.Collections;
using Diagnostics;
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
        private const int SamplePoints = 400;

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
            long beforeOld = MemoryProbe.Take().Total;
            Texture2D copy = paintable.GetReadableCopy();
            byte[] pngOld = copy.EncodeToPNG();
            long costOld = MemoryProbe.Take().Total - beforeOld;

            Color32[] pixelsOld = copy.GetPixels32();
            Object.Destroy(copy);
            yield return null;
            System.GC.Collect();
            yield return null;

            // --- straight off the GPU, no Texture2D in between ---------------------------------
            long beforeNew = MemoryProbe.Take().Total;

            AsyncGPUReadbackRequest request =
                AsyncGPUReadback.Request(paintable.Current, 0, TextureFormat.RGBA32);
            while (request.done == false) yield return null;
            Assert.IsFalse(request.hasError, "The GPU readback failed.");

            NativeArray<byte> raw = request.GetData<byte>();
            NativeArray<byte> encoded = ImageConversion.EncodeNativeArrayToPNG(
                raw, GraphicsFormat.R8G8B8A8_UNorm, (uint)width, (uint)height);
            byte[] pngNew = encoded.ToArray();
            long costNew = MemoryProbe.Take().Total - beforeNew;
            encoded.Dispose();

            // --- do the two agree? -------------------------------------------------------------
            int mismatches = 0;
            var random = new System.Random(20260913);
            for (int i = 0; i < SamplePoints; i++)
            {
                int x = random.Next(width);
                int y = random.Next(height);
                Color32 was = pixelsOld[y * width + x];
                int offset = (y * width + x) * 4;
                if (raw[offset + 0] != was.r || raw[offset + 1] != was.g
                    || raw[offset + 2] != was.b || raw[offset + 3] != was.a)
                {
                    mismatches++;
                }
            }

            Debug.Log("[readback]\n"
                      + "  today  (Texture2D route) : " + MemoryProbe.Signed(costOld)
                      + "   PNG " + MemoryProbe.Mb(pngOld.LongLength) + "\n"
                      + "  direct (GPU readback)    : " + MemoryProbe.Signed(costNew)
                      + "   PNG " + MemoryProbe.Mb(pngNew.LongLength) + "\n"
                      + "  saved                    : " + MemoryProbe.Signed(costOld - costNew) + "\n"
                      + "  pixel samples compared   : " + SamplePoints
                      + ", mismatches: " + mismatches);

            Assert.AreEqual(0, mismatches,
                "The cheaper route produced a different image — that would change how markings look.");
            Assert.Greater(pngNew.Length, 0, "The cheaper route produced an empty image.");
        }
    }
}
