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

        private struct Snapshot
        {
            public int Textures;

            /// <summary>The body paint lives in a RenderTexture, so counting Texture2D alone would
            /// miss exactly the object this app spends its memory on.</summary>
            public int RenderTextures;

            public int PaintableTextures;
            public long TextureMemory;
            public long Allocated;

            public override string ToString()
            {
                return string.Format(
                    "{0} textures, {1} render textures, {2} paintable, {3:0.0} MB texture memory, {4:0.0} MB allocated",
                    Textures, RenderTextures, PaintableTextures,
                    TextureMemory / 1024f / 1024f, Allocated / 1024f / 1024f);
            }
        }

        private static Snapshot Take()
        {
            return new Snapshot
            {
                Textures = Resources.FindObjectsOfTypeAll<Texture2D>().Length,
                RenderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Length,
                PaintableTextures = PaintCore.CwPaintableTexture.Instances.Count,
                TextureMemory = (long)Texture.currentTextureMemory,
                Allocated = Profiler.GetTotalAllocatedMemoryLong(),
            };
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
                first.TextureMemory * AllowedTextureGrowthShare);

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

            Assert.LessOrEqual(second.TextureMemory, first.TextureMemory + allowedBytes,
                string.Format("Texture memory grew by {0:0.0} MB over one round of the same twins "
                              + "({1:0.0} MB then, {2:0.0} MB now).",
                    (second.TextureMemory - first.TextureMemory) / 1024f / 1024f,
                    first.TextureMemory / 1024f / 1024f, second.TextureMemory / 1024f / 1024f));
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
                string.Format("  difference       : {0} textures, {2} render textures, {3} paintable, {1:+0.0;-0.0} MB texture memory",
                    second.Textures - first.Textures,
                    (second.TextureMemory - first.TextureMemory) / 1024f / 1024f,
                    second.RenderTextures - first.RenderTextures,
                    second.PaintableTextures - first.PaintableTextures),
            });

            Debug.Log("[memory] " + report);

            string directory = Path.Combine(Application.temporaryCachePath, "MemoryReports");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "twin-loading.txt"),
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" + report + "\n");
        }
    }
}
