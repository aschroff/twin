using System.Collections;
using System.Collections.Generic;
using System.Text;
using Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// What the app's memory is actually made of, object by object.
    /// </summary>
    /// <remarks>
    /// <para>Saving the paint texture was measured at a gigabyte, but the app already sits at
    /// well over two before that spike even starts. Fixing the spike alone would leave the larger
    /// half unexplained, so this weighs every live object and reports where the weight is.</para>
    ///
    /// <para>Running in the editor means editor-only objects are in the list too — the game view
    /// render target, the gizmo atlas, inspector textures. They are named, so they can be
    /// discounted. What is left is what a device would also carry.</para>
    /// </remarks>
    [Category(Processes.Technical)]
    public class MemoryBreakdownTests : TwinPaintTestBase
    {
        private const int TopObjects = 25;

        private struct Entry
        {
            public long Bytes;
            public string Line;
        }

        [UnityTest]
        public IEnumerator WhereTheMemoryIs()
        {
            yield return ResetApp();
            yield return SelectTwin("Torso");

            System.GC.Collect();
            yield return null;
            yield return Resources.UnloadUnusedAssets();
            yield return null;

            var byType = new Dictionary<string, long>();
            var counts = new Dictionary<string, int>();
            var biggest = new List<Entry>();
            long everything = 0;

            foreach (Object candidate in Resources.FindObjectsOfTypeAll<Object>())
            {
                long size = Profiler.GetRuntimeMemorySizeLong(candidate);
                if (size <= 0) continue;

                string type = candidate.GetType().Name;
                byType.TryGetValue(type, out long running);
                byType[type] = running + size;
                counts.TryGetValue(type, out int count);
                counts[type] = count + 1;
                everything += size;

                if (size >= 4L * 1024 * 1024)
                {
                    string shape = "";
                    if (candidate is Texture texture)
                    {
                        shape = " " + texture.width + "x" + texture.height;
                    }
                    biggest.Add(new Entry
                    {
                        Bytes = size,
                        Line = MemoryProbe.Mb(size).PadLeft(8) + "  " + type.PadRight(16)
                               + (string.IsNullOrEmpty(candidate.name) ? "(unnamed)" : candidate.name) + shape,
                    });
                }
            }

            var report = new StringBuilder();
            MemoryProbe.Sample now = MemoryProbe.Take();
            report.AppendLine("Unity counters: reserved " + MemoryProbe.Mb(now.UnityReserved)
                              + ", allocated " + MemoryProbe.Mb(now.UnityAllocated)
                              + ", graphics driver " + MemoryProbe.Mb(now.GraphicsDriver)
                              + ", mono heap " + MemoryProbe.Mb(now.MonoReserved));
            report.AppendLine("Weighed objects total: " + MemoryProbe.Mb(everything));
            report.AppendLine();

            report.AppendLine("BY TYPE");
            var types = new List<KeyValuePair<string, long>>(byType);
            types.Sort((a, b) => b.Value.CompareTo(a.Value));
            foreach (KeyValuePair<string, long> pair in types)
            {
                if (pair.Value < 1024 * 1024) continue;
                report.AppendLine("  " + MemoryProbe.Mb(pair.Value).PadLeft(8) + "  "
                                  + pair.Key.PadRight(18) + counts[pair.Key] + " objects");
            }

            report.AppendLine();
            report.AppendLine("BIGGEST SINGLE OBJECTS (>= 4 MB)");
            biggest.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            for (int i = 0; i < biggest.Count && i < TopObjects; i++)
            {
                report.AppendLine("  " + biggest[i].Line);
            }

            Debug.Log("[breakdown]\n" + report);
            Assert.Greater(everything, 0, "Nothing was weighed at all.");
        }
    }
}
