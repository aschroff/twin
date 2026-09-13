using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Loading one twin after another must not cost more memory each time.
    /// </summary>
    /// <remarks>
    /// <para>A user reported the app dying after opening about four twins in a row. That is not a
    /// question of how much the app needs — it is a question of whether each load gives back what
    /// the one before it took. So the app is put into the <b>same state twice</b>, with four other
    /// twins opened in between, and the two measurements are compared: same state, same memory, or
    /// something is being kept.</para>
    ///
    /// <para>The first round is a warm-up and is not measured. A first load pays for shaders,
    /// pools and assets that are loaded lazily, and comparing it to a later one would report that
    /// one-off cost as a leak. Both measured rounds are taken after a full round through the same
    /// twins, so whatever is one-off cancels out.</para>
    ///
    /// <para><b>What this test cannot do:</b> say whether the app fits on an iPad Air. It runs in
    /// the editor on a desktop, where the absolute numbers mean nothing. It catches <i>growth</i>,
    /// which is device independent — and growth is what killed that iPad. Absolute figures have to
    /// be measured on the device itself.</para>
    ///
    /// <para>Two numbers are taken each time: before a forced cleanup, which is what the device
    /// sees between loads and what iOS decides to kill you over, and after one, which is what is
    /// really being retained. The assertion is on the retained figure — the other is reported,
    /// because a large gap between them is itself worth knowing.</para>
    ///
    /// <para><b>Why the textures are weighed and not only counted.</b> Seven jetsam reports from
    /// the iPad Air on 2026-09-09 show the app killed with <c>per-process-limit</c> at a footprint
    /// of 3.0-3.2 GB, and only about 600 MB of that was ordinary heap. The body paint texture is
    /// 8192x8192 ARGB32 — 256 MB each — and <see cref="Texture.currentTextureMemory"/> does not
    /// report it, so a test watching that counter would have called the run clean. What is
    /// asserted on here is therefore the summed
    /// <see cref="Profiler.GetRuntimeMemorySizeLong(Object)"/> of every live texture, and the
    /// number of textures large enough to be one of the paint targets. One more of those per twin
    /// opened is four twins from a dead iPad.</para>
    /// </remarks>
    [Category(Processes.ManageTwins)]
    public class TwinLoadingMemoryTests : TwinPaintTestBase
    {
        private const string HomeTwin = "LipEdema";
        private const int OtherTwinsPerRound = 4;

        /// <summary>Texture count may wobble by a little; four more per round would not.</summary>
        private const int AllowedExtraTextures = 3;

        /// <summary>Whichever is larger, so a big scene is not judged by a percentage alone.</summary>
        private const long AllowedExtraTextureBytes = 4L * 1024 * 1024;
        private const double AllowedTextureGrowthShare = 0.05;

        /// <summary>A texture this big is a paint target or a twin's own image, not scenery: the
        /// 8192-square body paint weighs 256 MB, and 2048-square is the smallest that still costs
        /// 16 MB. Anything at or above this is listed by name, so a leak can be pointed at.</summary>
        private const long BigTextureThreshold = 16L * 1024 * 1024;

        private struct Snapshot
        {
            public int Textures;

            /// <summary>The body paint lives in a RenderTexture, so counting Texture2D alone would
            /// miss exactly the object this app spends its memory on.</summary>
            public int RenderTextures;

            public int PaintableTextures;

            /// <summary>Every live texture weighed individually. This is the figure that matters:
            /// unlike <see cref="TextureMemory"/> it includes the 8192-square paint targets.</summary>
            public long TextureBytes;

            /// <summary>How many textures are at or above <see cref="BigTextureThreshold"/>.</summary>
            public int BigTextures;

            public long BigTextureBytes;

            /// <summary>Those big ones by name and size, largest first, for the report.</summary>
            public List<string> Biggest;

            /// <summary>Unity's own counter. Reported but never asserted on — it leaves the paint
            /// texture out, which is the one object this app can die of.</summary>
            public long TextureMemory;

            public long Allocated;

            public override string ToString()
            {
                return string.Format(
                    "{0} textures, {1} render textures, {2} paintable, {3:0.0} MB in textures "
                    + "({4} of them big, {5:0.0} MB), {6:0.0} MB allocated, Unity counter {7:0.0} MB",
                    Textures, RenderTextures, PaintableTextures,
                    TextureBytes / 1024f / 1024f, BigTextures, BigTextureBytes / 1024f / 1024f,
                    Allocated / 1024f / 1024f, TextureMemory / 1024f / 1024f);
            }
        }

        /// <summary>
        /// Walks every live texture once, weighing each one. One pass rather than three, because
        /// the counts and the bytes have to describe the same moment to be comparable.
        /// </summary>
        private static Snapshot Take()
        {
            var snapshot = new Snapshot
            {
                PaintableTextures = PaintCore.CwPaintableTexture.Instances.Count,
                TextureMemory = (long)Texture.currentTextureMemory,
                Allocated = Profiler.GetTotalAllocatedMemoryLong(),
                Biggest = new List<string>(),
            };

            var big = new List<KeyValuePair<long, string>>();

            foreach (Texture texture in Resources.FindObjectsOfTypeAll<Texture>())
            {
                if (texture is Texture2D)
                {
                    snapshot.Textures++;
                }
                else if (texture is RenderTexture)
                {
                    snapshot.RenderTextures++;
                }

                long bytes = Profiler.GetRuntimeMemorySizeLong(texture);
                snapshot.TextureBytes += bytes;

                if (bytes >= BigTextureThreshold)
                {
                    snapshot.BigTextures++;
                    snapshot.BigTextureBytes += bytes;
                    big.Add(new KeyValuePair<long, string>(bytes, string.Format(
                        "{0:0} MB  {1}x{2}  {3}  [{4}]",
                        bytes / 1024f / 1024f, texture.width, texture.height,
                        string.IsNullOrEmpty(texture.name) ? "(unnamed)" : texture.name,
                        texture.GetType().Name)));
                }
            }

            big.Sort((left, right) => right.Key.CompareTo(left.Key));
            foreach (KeyValuePair<long, string> entry in big)
            {
                snapshot.Biggest.Add(entry.Value);
            }

            return snapshot;
        }

        /// <summary>
        /// Gives the engine the chance to hand back what is no longer referenced, so that what is
        /// left really is held rather than merely not collected yet.
        /// </summary>
        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;
        }

        private IEnumerator OpenTwinList()
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
        }

        /// <summary>The twins the app offers, read off the list rather than hard-coded.</summary>
        private List<string> TwinsInTheList()
        {
            GameObject panel = FindGameObjectByPath(SaveTwinPanel);
            Assert.IsNotNull(panel, "The twin list is not on screen.");

            var names = new List<string>();
            foreach (Transform entry in panel.transform)
            {
                Transform label = entry.Find("Name/Text");
                Text text = label != null ? label.GetComponent<Text>() : null;
                if (text != null && !string.IsNullOrWhiteSpace(text.text))
                {
                    names.Add(text.text);
                }
            }
            return names;
        }

        private IEnumerator LoadRound(IEnumerable<string> twins)
        {
            foreach (string twin in twins)
            {
                yield return SelectTwin(twin);
            }
        }

        [UnityTest]
        public IEnumerator LoadingTheSameTwinAgainAfterOthers_CostsNoMoreMemory()
        {
            yield return ResetApp();
            yield return OpenTwinList();

            List<string> others = TwinsInTheList()
                .Where(name => name != HomeTwin)
                .Take(OtherTwinsPerRound)
                .ToList();
            Assert.GreaterOrEqual(others.Count, 2,
                "Need at least two other twins to load in between; the list held: "
                + string.Join(", ", TwinsInTheList()));
            Debug.Log("[memory] twins in between: " + string.Join(", ", others));

            // warm-up: a first load pays one-off costs that are not a leak
            yield return SelectTwin(HomeTwin);
            yield return LoadRound(others);

            yield return SelectTwin(HomeTwin);
            Snapshot firstRaw = Take();
            yield return Settle();
            Snapshot first = Take();

            yield return LoadRound(others);

            yield return SelectTwin(HomeTwin);
            Snapshot secondRaw = Take();
            yield return Settle();
            Snapshot second = Take();

            Report(others, firstRaw, first, secondRaw, second);

            long allowedBytes = (long)System.Math.Max(AllowedExtraTextureBytes,
                first.TextureBytes * AllowedTextureGrowthShare);

            Assert.LessOrEqual(second.RenderTextures, first.RenderTextures + AllowedExtraTextures,
                string.Format("Opening {0} twins and coming back to {1} left {2} more render textures behind "
                              + "({3} then, {4} now). The body paint lives in one of these.",
                    others.Count, HomeTwin, second.RenderTextures - first.RenderTextures,
                    first.RenderTextures, second.RenderTextures));

            Assert.LessOrEqual(second.PaintableTextures, first.PaintableTextures + 1,
                string.Format("{0} paintable textures are alive, {1} after the first round — one per twin "
                              + "that was opened is a leak.", second.PaintableTextures, first.PaintableTextures));

            Assert.LessOrEqual(second.Textures, first.Textures + AllowedExtraTextures,
                string.Format("Opening {0} twins and coming back to {1} left {2} more textures behind "
                              + "({3} then, {4} now). That is what fills a device up until it is killed.",
                    others.Count, HomeTwin, second.Textures - first.Textures, first.Textures, second.Textures));

            // The one that would have caught the iPad Air: a paint target is 256 MB, so a single
            // extra one is the difference between an app that runs and an app the kernel shoots.
            Assert.LessOrEqual(second.BigTextures, first.BigTextures,
                string.Format("Opening {0} twins and coming back to {1} left {2} more large texture(s) "
                              + "alive ({3} then, {4} now, {5:0.0} MB then, {6:0.0} MB now).\n"
                              + "Alive now:\n  {7}",
                    others.Count, HomeTwin, second.BigTextures - first.BigTextures,
                    first.BigTextures, second.BigTextures,
                    first.BigTextureBytes / 1024f / 1024f, second.BigTextureBytes / 1024f / 1024f,
                    string.Join("\n  ", second.Biggest.ToArray())));

            Assert.LessOrEqual(second.TextureBytes, first.TextureBytes + allowedBytes,
                string.Format("Texture memory grew by {0:0.0} MB over one round of the same twins "
                              + "({1:0.0} MB then, {2:0.0} MB now). On the iPad Air the app is killed "
                              + "at about 3 GB.\nAlive now:\n  {3}",
                    (second.TextureBytes - first.TextureBytes) / 1024f / 1024f,
                    first.TextureBytes / 1024f / 1024f, second.TextureBytes / 1024f / 1024f,
                    string.Join("\n  ", second.Biggest.ToArray())));
        }

        /// <summary>
        /// Writes the figures out even when the test passes: the trend on one machine over time is
        /// the useful part, and a pass that quietly doubled is worth seeing.
        /// </summary>
        private static void Report(IEnumerable<string> others,
            Snapshot firstRaw, Snapshot first, Snapshot secondRaw, Snapshot second)
        {
            string report = string.Join("\n", new[]
            {
                "Twin loading, memory over one round",
                "  twins in between : " + string.Join(", ", others),
                "  after round 1    : " + first + "   (before cleanup: " + firstRaw + ")",
                "  after round 2    : " + second + "   (before cleanup: " + secondRaw + ")",
                string.Format("  difference       : {0:+#;-#;0} textures, {1:+#;-#;0} render textures, "
                              + "{2:+#;-#;0} paintable, {3:+#;-#;0} large, {4:+0.0;-0.0;0.0} MB in textures",
                    second.Textures - first.Textures,
                    second.RenderTextures - first.RenderTextures,
                    second.PaintableTextures - first.PaintableTextures,
                    second.BigTextures - first.BigTextures,
                    (second.TextureBytes - first.TextureBytes) / 1024f / 1024f),
                "  large textures after round 2 (>= 16 MB each):",
                second.Biggest.Count == 0
                    ? "    none"
                    : "    " + string.Join("\n    ", second.Biggest.ToArray()),
            });

            Debug.Log("[memory] " + report);

            string directory = Path.Combine(Application.temporaryCachePath, "MemoryReports");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "twin-loading.txt"),
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" + report + "\n");
        }
    }
}
