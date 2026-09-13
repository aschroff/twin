using System.Collections;
using System.Reflection;
using System.Text;
using Diagnostics;
using NUnit.Framework;
using PaintCore;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Who owns the 8192-square render textures.
    /// </summary>
    /// <remarks>
    /// <para>Two of them are alive at rest, 256 MB each, on a device that is killed at 3000 MB.
    /// One is the paint target and has to exist. The second one has to be accounted for before
    /// anyone proposes a fix, because the answer decides where a fix may live: our own code, or
    /// a vendored package that is not even in version control.</para>
    /// </remarks>
    [Category(Processes.Technical)]
    public class PaintBufferTests : TwinPaintTestBase
    {
        private static RenderTexture PrivateField(CwPaintableTexture paintable, string name)
        {
            FieldInfo field = typeof(CwPaintableTexture).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? field.GetValue(paintable) as RenderTexture : null;
        }

        [UnityTest]
        public IEnumerator AtRest_TheBigBuffersAreAccountedFor()
        {
            yield return ResetApp();
            yield return SelectTwin("Torso");

            // settle: Unity's temporary render texture pool frees on a delay, so anything still
            // alive after this many frames is held, not merely pending
            for (int frame = 0; frame < 20; frame++) yield return null;
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            for (int frame = 0; frame < 20; frame++) yield return null;

            CwPaintableTexture paintable = Object.FindFirstObjectByType<CwPaintableTexture>();
            Assert.IsNotNull(paintable, "No paintable texture in the scene.");

            RenderTexture current = paintable.Current;
            RenderTexture preview = PrivateField(paintable, "preview");

            var report = new StringBuilder();
            report.AppendLine("paintable.Current = " + (current != null ? current.name : "null"));
            report.AppendLine("paintable.preview = " + (preview != null ? preview.name : "null"));
            report.AppendLine();

            int unaccounted = 0;
            long unaccountedBytes = 0;
            foreach (RenderTexture rt in Resources.FindObjectsOfTypeAll<RenderTexture>())
            {
                if (rt.width < 4096 || rt.height < 4096) continue;

                string owner = "UNACCOUNTED — pool or leak";
                if (rt == current) owner = "the paint target";
                else if (rt == preview) owner = "the preview buffer";

                long size = Profiler.GetRuntimeMemorySizeLong(rt);
                report.AppendLine("  " + MemoryProbe.Mb(size).PadLeft(8) + "  "
                                  + (rt.width + "x" + rt.height).PadRight(12)
                                  + "created=" + rt.IsCreated().ToString().PadRight(6)
                                  + rt.name.PadRight(26) + owner);

                if (owner.StartsWith("UNACCOUNTED"))
                {
                    unaccounted++;
                    unaccountedBytes += size;
                }
            }

            report.AppendLine();
            report.AppendLine("unaccounted: " + unaccounted + " buffers, " + MemoryProbe.Mb(unaccountedBytes));
            Debug.Log("[paint buffers]\n" + report);
        }
    }
}
