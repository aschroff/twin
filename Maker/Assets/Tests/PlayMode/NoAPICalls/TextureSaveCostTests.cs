using System.Collections;
using Diagnostics;
using NUnit.Framework;
using PaintCore;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Breaks the cost of saving the painted texture down into its steps.
    /// </summary>
    /// <remarks>
    /// <para>Saving was measured at over a gigabyte per twin switch, which is what kills the app on
    /// an iPad. That figure alone does not say what to change, because the save is a chain of five
    /// things and only some of them are avoidable. This walks the same chain
    /// <c>CwPaintableTexture.Save</c> walks and weighs each link.</para>
    ///
    /// <para>Nothing is asserted: the absolute numbers belong to this machine. The output is the
    /// point, and it is what a fix should be judged against.</para>
    /// </remarks>
    [Category(Processes.ManageTwins)]
    public class TextureSaveCostTests : TwinPaintTestBase
    {
        [UnityTest]
        public IEnumerator SavingThePaintedTexture_CostsThisMuchPerStep()
        {
            yield return ResetApp();
            yield return SelectTwin("Torso");

            CwPaintableTexture paintable = Object.FindFirstObjectByType<CwPaintableTexture>();
            Assert.IsNotNull(paintable, "No paintable texture in the scene.");

            System.GC.Collect();
            yield return null;

            long start = MemoryProbe.Take().Total;
            var report = new System.Text.StringBuilder();
            report.AppendLine("Cost of saving one " + paintable.Width + "x" + paintable.Height + " paint texture");
            report.AppendLine("  start                        = " + MemoryProbe.Mb(start));

            // 1. the readable copy: a render texture plus a Texture2D, both full size
            Texture2D copy = paintable.GetReadableCopy();
            long afterCopy = MemoryProbe.Take().Total;
            report.AppendLine("  GetReadableCopy   " + MemoryProbe.Signed(afterCopy - start).PadLeft(9)
                              + "  = " + MemoryProbe.Mb(afterCopy));

            // 2. the PNG
            byte[] png = copy.EncodeToPNG();
            long afterPng = MemoryProbe.Take().Total;
            report.AppendLine("  EncodeToPNG       " + MemoryProbe.Signed(afterPng - afterCopy).PadLeft(9)
                              + "  = " + MemoryProbe.Mb(afterPng)
                              + "   (PNG is " + MemoryProbe.Mb(png.LongLength) + ")");

            // 3. base64 - a C# string is two bytes per character, so this is 2.67x the PNG
            string base64 = System.Convert.ToBase64String(png);
            long afterBase64 = MemoryProbe.Take().Total;
            report.AppendLine("  ToBase64String    " + MemoryProbe.Signed(afterBase64 - afterPng).PadLeft(9)
                              + "  = " + MemoryProbe.Mb(afterBase64)
                              + "   (string is " + MemoryProbe.Mb(base64.Length * 2L) + ")");

            // 4. PlayerPrefs, which keeps its own copy for as long as the app runs
            PlayerPrefs.SetString("MemoryProbeScratch", base64);
            long afterPrefs = MemoryProbe.Take().Total;
            report.AppendLine("  PlayerPrefs.Set   " + MemoryProbe.Signed(afterPrefs - afterBase64).PadLeft(9)
                              + "  = " + MemoryProbe.Mb(afterPrefs));

            report.AppendLine("  TOTAL             " + MemoryProbe.Signed(afterPrefs - start).PadLeft(9));

            PlayerPrefs.DeleteKey("MemoryProbeScratch");
            Object.Destroy(copy);

            Debug.Log("[save cost]\n" + report);
        }
        /// <summary>
        /// The same picture, produced without the two steps that look unnecessary: the
        /// <c>Apply()</c> that pushes the pixels back onto the GPU although only the CPU side is
        /// read afterwards, and the base64 detour. If this is much cheaper, the saving is real.
        /// </summary>
        [UnityTest]
        public IEnumerator SavingWithoutTheWastedCopies_CostsLess()
        {
            yield return ResetApp();
            yield return SelectTwin("Torso");

            CwPaintableTexture paintable = Object.FindFirstObjectByType<CwPaintableTexture>();
            Assert.IsNotNull(paintable, "No paintable texture in the scene.");

            System.GC.Collect();
            yield return null;

            long start = MemoryProbe.Take().Total;
            var report = new System.Text.StringBuilder();
            report.AppendLine("Same save, without Apply() and without base64");
            report.AppendLine("  start                        = " + MemoryProbe.Mb(start));

            var descriptor = new RenderTextureDescriptor(paintable.Width, paintable.Height,
                RenderTextureFormat.ARGB32, 0);
            RenderTexture buffer = RenderTexture.GetTemporary(descriptor);
            var copy = new Texture2D(paintable.Width, paintable.Height, TextureFormat.ARGB32, false, false);

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = buffer;
            Graphics.Blit(paintable.Current, buffer);
            copy.ReadPixels(new Rect(0, 0, paintable.Width, paintable.Height), 0, 0);
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(buffer);
            // deliberately no copy.Apply() - EncodeToPNG reads the CPU side, which ReadPixels filled

            long afterCopy = MemoryProbe.Take().Total;
            report.AppendLine("  readable copy     " + MemoryProbe.Signed(afterCopy - start).PadLeft(9)
                              + "  = " + MemoryProbe.Mb(afterCopy));

            byte[] png = copy.EncodeToPNG();
            long afterPng = MemoryProbe.Take().Total;
            report.AppendLine("  EncodeToPNG       " + MemoryProbe.Signed(afterPng - afterCopy).PadLeft(9)
                              + "  = " + MemoryProbe.Mb(afterPng)
                              + "   (PNG is " + MemoryProbe.Mb(png.LongLength) + ")");
            report.AppendLine("  TOTAL             " + MemoryProbe.Signed(afterPng - start).PadLeft(9));

            Assert.IsNotNull(png, "The cheap route produced no image.");
            Assert.Greater(png.Length, 0, "The cheap route produced an empty image.");

            Object.Destroy(copy);
            Debug.Log("[save cost, cheap route]\n" + report);
        }
    }
}
