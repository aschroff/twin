using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace Diagnostics
{
    /// <summary>
    /// Records what the app costs in memory, so it can be read off the device.
    /// </summary>
    /// <remarks>
    /// <para>iOS kills an app when its footprint crosses a per-process limit — on the iPad Air
    /// that is about 3 GB, measured from seven jetsam reports. It kills on the <b>peak</b>, and
    /// the peak happens <i>during</i> a twin switch, not after it. A reading taken when someone
    /// opens the settings page would therefore always miss it. So this class is fed every frame
    /// and keeps the highest value it ever saw.</para>
    ///
    /// <para><b>None of these numbers is the figure iOS actually uses.</b> That one is
    /// <c>phys_footprint</c>, and Unity cannot read it without native code. What is collected here
    /// are the counters that move with it: Unity's own allocations, the managed heap, and the
    /// graphics driver. They are reported separately rather than as one total, because a total
    /// would hide which of them grows — and that is the whole question.</para>
    ///
    /// <para>The per-step figures come from <see cref="BeginSwitch"/> and <see cref="Step"/>,
    /// called from the twin switch itself. They answer the question the editor could not: which
    /// part of a switch costs the memory.</para>
    /// </remarks>
    public static class MemoryProbe
    {
        /// <summary>How many twin switches to keep, so a trend is visible without writing the
        /// numbers down between switches.</summary>
        private const int RememberedSwitches = 12;

        public struct Sample
        {
            /// <summary>Everything Unity allocated natively — textures, meshes, audio.</summary>
            public long UnityAllocated;

            /// <summary>What Unity holds from the system. Never shrinks, which is why it is the
            /// closest thing here to what iOS counts against the app.</summary>
            public long UnityReserved;

            public long MonoUsed;

            /// <summary>The managed heap's high-water mark. Unity's collector does not hand this
            /// back to iOS, so once it has grown it stays.</summary>
            public long MonoReserved;

            public long GraphicsDriver;

            /// <summary>The best proxy available without native code. An estimate, not the
            /// number iOS kills on.</summary>
            public long Total { get { return UnityReserved + MonoReserved + GraphicsDriver; } }
        }

        /// <summary>One reading taken during a switch, with the step that had just finished.</summary>
        public struct Mark
        {
            public string Label;
            public long Total;
        }

        /// <summary>What one twin switch cost, step by step.</summary>
        public struct SwitchRecord
        {
            public int Index;
            public string Twin;
            public List<Mark> Marks;
            public long Peak;
            public float Seconds;

            public long Before { get { return Marks.Count > 0 ? Marks[0].Total : 0; } }
            public long After  { get { return Marks.Count > 0 ? Marks[Marks.Count - 1].Total : 0; } }
            public long Total  { get { return After - Before; } }
        }

        private static readonly List<SwitchRecord> switches = new List<SwitchRecord>();
        private static SwitchRecord open;
        private static bool switchOpen;
        private static float switchStarted;
        private static int switchCount;

        private static Sample peak;
        private static Sample first;
        private static bool haveFirst;

        public static Sample Peak { get { return peak; } }
        public static Sample First { get { return first; } }
        public static int SwitchCount { get { return switchCount; } }
        public static IList<SwitchRecord> Switches { get { return switches; } }

        /// <summary>Reads the counters. Cheap enough to call every frame — it walks nothing.</summary>
        public static Sample Take()
        {
            return new Sample
            {
                UnityAllocated = Profiler.GetTotalAllocatedMemoryLong(),
                UnityReserved  = Profiler.GetTotalReservedMemoryLong(),
                MonoUsed       = Profiler.GetMonoUsedSizeLong(),
                MonoReserved   = Profiler.GetMonoHeapSizeLong(),
                GraphicsDriver = Profiler.GetAllocatedMemoryForGraphicsDriver(),
            };
        }

        /// <summary>Called every frame by <see cref="MemoryWatch"/>. Keeps the highest value of
        /// each counter, because the moment that matters is never the moment someone looks.</summary>
        public static void Observe()
        {
            Sample now = Take();

            if (haveFirst == false)
            {
                first = now;
                haveFirst = true;
            }

            if (now.UnityAllocated > peak.UnityAllocated) peak.UnityAllocated = now.UnityAllocated;
            if (now.UnityReserved  > peak.UnityReserved)  peak.UnityReserved  = now.UnityReserved;
            if (now.MonoUsed       > peak.MonoUsed)       peak.MonoUsed       = now.MonoUsed;
            if (now.MonoReserved   > peak.MonoReserved)   peak.MonoReserved   = now.MonoReserved;
            if (now.GraphicsDriver > peak.GraphicsDriver) peak.GraphicsDriver = now.GraphicsDriver;

            if (switchOpen == true && now.Total > open.Peak)
            {
                open.Peak = now.Total;
            }
        }

        /// <summary>A switch that is already open is closed first: the app must never lose a
        /// reading because a code path returned early.</summary>
        public static void BeginSwitch(string twin)
        {
            if (switchOpen == true)
            {
                EndSwitch();
            }

            // The state just before the first switch is exactly the state a restart returns to,
            // which is what everything later has to be compared against.
            CaptureBaseline();

            switchCount++;
            switchStarted = Time.realtimeSinceStartup;

            long total = Take().Total;
            open = new SwitchRecord
            {
                Index = switchCount,
                Twin  = twin,
                Marks = new List<Mark> { new Mark { Label = "start", Total = total } },
                Peak  = total,
            };
            switchOpen = true;
        }

        /// <summary>Records that a step of the switch has finished, and what it left behind.</summary>
        public static void Step(string label)
        {
            if (switchOpen == false) return;

            long total = Take().Total;
            if (total > open.Peak) open.Peak = total;
            open.Marks.Add(new Mark { Label = label, Total = total });
        }

        public static void EndSwitch()
        {
            if (switchOpen == false) return;

            Step("done");
            open.Seconds = Time.realtimeSinceStartup - switchStarted;
            switchOpen   = false;

            switches.Add(open);
            if (switches.Count > RememberedSwitches)
            {
                switches.RemoveAt(0);
            }
        }

        /// <summary>How much memory one kind of object accounts for, and how many there are.</summary>
        public struct TypeTotal
        {
            public string Type;
            public int Count;
            public long Bytes;
        }

        private static Dictionary<string, TypeTotal> baseline;

        public static bool HasBaseline { get { return baseline != null; } }

        /// <summary>
        /// Records what the app is made of shortly after it starts, so that everything measured
        /// later can be compared against it.
        /// </summary>
        /// <remarks>This is the whole point of the panel. Restarting the app makes the problem go
        /// away, which means whatever has piled up by then is not needed. The difference between
        /// this baseline and a later reading <b>is</b> that surplus, named by type.</remarks>
        public static void CaptureBaseline()
        {
            if (baseline == null)
            {
                baseline = Composition();
            }
        }

        /// <summary>
        /// Weighs every live object and groups the result by type. Walks everything, so this is
        /// only called when the settings page is opened — never per frame.
        /// </summary>
        public static Dictionary<string, TypeTotal> Composition()
        {
            var totals = new Dictionary<string, TypeTotal>();

            foreach (Object candidate in Resources.FindObjectsOfTypeAll<Object>())
            {
                long size = Profiler.GetRuntimeMemorySizeLong(candidate);
                if (size <= 0) continue;

                string type = candidate.GetType().Name;
                TypeTotal running;
                totals.TryGetValue(type, out running);
                running.Type   = type;
                running.Count += 1;
                running.Bytes += size;
                totals[type]   = running;
            }

            return totals;
        }

        /// <summary>What has appeared since the baseline, biggest first. This is the answer to
        /// "what is in memory that a restart would throw away".</summary>
        public static List<TypeTotal> GrowthSinceBaseline(Dictionary<string, TypeTotal> now)
        {
            var growth = new List<TypeTotal>();
            if (baseline == null) return growth;

            foreach (KeyValuePair<string, TypeTotal> pair in now)
            {
                TypeTotal was;
                baseline.TryGetValue(pair.Key, out was);
                growth.Add(new TypeTotal
                {
                    Type  = pair.Key,
                    Count = pair.Value.Count - was.Count,
                    Bytes = pair.Value.Bytes - was.Bytes,
                });
            }

            growth.Sort((a, b) => b.Bytes.CompareTo(a.Bytes));
            return growth;
        }

        /// <summary>The biggest single objects alive right now, so a surplus can be pointed at by
        /// name rather than only by type.</summary>
        public static List<string> Biggest(int count, long threshold)
        {
            var found = new List<KeyValuePair<long, string>>();

            foreach (Object candidate in Resources.FindObjectsOfTypeAll<Object>())
            {
                long size = Profiler.GetRuntimeMemorySizeLong(candidate);
                if (size < threshold) continue;

                string shape = "";
                Texture texture = candidate as Texture;
                if (texture != null) shape = " " + texture.width + "x" + texture.height;

                found.Add(new KeyValuePair<long, string>(size,
                    Mb(size).PadLeft(8) + "  " + candidate.GetType().Name.PadRight(14)
                    + (string.IsNullOrEmpty(candidate.name) ? "(unnamed)" : candidate.name) + shape));
            }

            found.Sort((a, b) => b.Key.CompareTo(a.Key));

            var lines = new List<string>();
            for (int i = 0; i < found.Count && i < count; i++) lines.Add(found[i].Value);
            return lines;
        }

        /// <summary>
        /// Weighs every live texture. Expensive — it walks all of them — so this is only done when
        /// the settings page is opened, never per frame.
        /// </summary>
        /// <remarks>Unity's own <see cref="Texture.currentTextureMemory"/> is not used: it leaves
        /// the 8192-square paint targets out, and was measured 13x low in the editor.</remarks>
        public static void WeighTextures(out int count, out long bytes, out int big, out long bigBytes)
        {
            count = 0; bytes = 0; big = 0; bigBytes = 0;

            foreach (Texture texture in Resources.FindObjectsOfTypeAll<Texture>())
            {
                long size = Profiler.GetRuntimeMemorySizeLong(texture);
                count++;
                bytes += size;

                if (size >= 16L * 1024 * 1024)
                {
                    big++;
                    bigBytes += size;
                }
            }
        }

        public static string Mb(long bytes)
        {
            return (bytes / 1048576f).ToString("0") + " MB";
        }

        public static string Signed(long bytes)
        {
            return (bytes / 1048576f).ToString("+0;-0;0") + " MB";
        }
    }
}
