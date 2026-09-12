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
    /// The same as <see cref="TwinLoadingMemoryTests"/>, but with work done in each twin — because
    /// a twin that is only opened may never touch the parts that cost memory.
    /// </summary>
    /// <remarks>
    /// <para>Each round opens several twins, paints a stroke in each, and comes back to the same
    /// twin to be measured. A twin carries its body paint and its sticker images as files — the
    /// LipEdema sample alone ships seven PNGs, together 5.6 MB — so opening and painting is what
    /// moves image memory around.</para>
    ///
    /// <para><b>Why three rounds instead of two.</b> With two measurements the only question one
    /// can ask is "is the difference below a threshold", and that threshold is a guess: too tight
    /// and the test flickers, too loose and a slow leak of one texture per twin hides under it
    /// forever. Three measurements allow a better question — <i>does it keep climbing?</i> Noise
    /// wobbles, a leak rises. So each step is checked against the noise band, and the whole climb
    /// from the first to the last round against twice that. One round that happens to sit a little
    /// higher stays green; three rounds that each add the same amount do not.</para>
    ///
    /// <para>As with the other memory test: this runs in the editor, so the absolute numbers say
    /// nothing about an iPad. It measures growth, which is device independent.</para>
    /// </remarks>
    [Category(Processes.ManageTwins)]
    public class TwinActivityMemoryTests : TwinPaintTestBase
    {
        private const string HomeTwin = "LipEdema";
        private const int OtherTwinsPerRound = 3;
        private const int Rounds = 3;

        private const int AllowedExtraTextures = 3;
        private const long AllowedExtraTextureBytes = 4L * 1024 * 1024;

        private struct Snapshot
        {
            public int Textures;
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

        private static IEnumerator Settle()
        {
            yield return null;
            yield return null;
            System.GC.Collect();
            yield return Resources.UnloadUnusedAssets();
            System.GC.Collect();
            yield return null;
        }

        /// <summary>
        /// Frames the torso without relying on a stored view: a twin other than the sample does not
        /// have one, and the camera a twin comes with points between the legs, where a stroke in
        /// the middle of the screen would hit nothing.
        /// </summary>
        private static void FrameTheTorso()
        {
            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            viewManager.select(new SceneManagement.View
            {
                yaw = 0f,
                pitch = 0f,
                sizeCamera = 0.9f,
                positionCamera_x = 0f,
                positionCamera_y = 1.0f,
                positionCamera_z = 1f,
            });
        }

        private IEnumerator OpenTwinList()
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
        }

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

        /// <summary>Open a twin and actually use it, the way a person would.</summary>
        private IEnumerator WorkInTwin(string twin)
        {
            yield return SelectTwin(twin);

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            FrameTheTorso();
            yield return null;

            yield return PaintWithMarker("Black");

            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
        }

        [UnityTest]
        public IEnumerator WorkingInSeveralTwins_DoesNotKeepGrowingMemory()
        {
            yield return ResetApp();
            yield return OpenTwinList();

            List<string> others = TwinsInTheList()
                .Where(name => name != HomeTwin)
                .Take(OtherTwinsPerRound)
                .ToList();
            Assert.GreaterOrEqual(others.Count, 2, "Need at least two other twins to work in.");
            Debug.Log("[memory] twins worked in: " + string.Join(", ", others));

            var measured = new List<Snapshot>();

            for (int round = 0; round < Rounds; round++)
            {
                foreach (string twin in others)
                {
                    yield return WorkInTwin(twin);
                }

                yield return WorkInTwin(HomeTwin);
                yield return Settle();
                measured.Add(Take());
            }

            Report(others, measured);

            Snapshot firstRound = measured[0];
            for (int round = 1; round < measured.Count; round++)
            {
                Snapshot previous = measured[round - 1];
                Snapshot current = measured[round];

                Assert.LessOrEqual(current.PaintableTextures, previous.PaintableTextures + 1,
                    Because(round, "paintable textures", previous.PaintableTextures, current.PaintableTextures));
                Assert.LessOrEqual(current.RenderTextures, previous.RenderTextures + AllowedExtraTextures,
                    Because(round, "render textures", previous.RenderTextures, current.RenderTextures));
                Assert.LessOrEqual(current.Textures, previous.Textures + AllowedExtraTextures,
                    Because(round, "textures", previous.Textures, current.Textures));
                Assert.LessOrEqual(current.TextureMemory, previous.TextureMemory + AllowedExtraTextureBytes,
                    Because(round, "texture memory (bytes)", previous.TextureMemory, current.TextureMemory));
            }

            // One round may sit a little higher by chance; a climb that holds over every round is
            // what a leak looks like, so the whole series gets twice the slack of a single step.
            Snapshot lastRound = measured[measured.Count - 1];
            Assert.LessOrEqual(lastRound.Textures, firstRound.Textures + 2 * AllowedExtraTextures,
                string.Format("Textures climbed over {0} rounds of the same work: {1} → {2}.",
                    Rounds, firstRound.Textures, lastRound.Textures));
            Assert.LessOrEqual(lastRound.TextureMemory, firstRound.TextureMemory + 2 * AllowedExtraTextureBytes,
                string.Format("Texture memory climbed over {0} rounds of the same work: {1:0.0} MB → {2:0.0} MB.",
                    Rounds, firstRound.TextureMemory / 1024f / 1024f, lastRound.TextureMemory / 1024f / 1024f));
        }

        private static string Because(int round, string what, long before, long after)
        {
            return string.Format("Round {0} left {1} more {2} behind than round {3} ({4} → {5}). "
                                 + "The same work should cost the same every time.",
                round + 1, after - before, what, round, before, after);
        }

        private static void Report(IEnumerable<string> others, IList<Snapshot> measured)
        {
            var lines = new List<string>
            {
                "Twin activity, memory over " + measured.Count + " rounds",
                "  twins worked in : " + string.Join(", ", others),
            };
            for (int i = 0; i < measured.Count; i++)
            {
                lines.Add(string.Format("  after round {0}   : {1}", i + 1, measured[i]));
            }
            lines.Add(string.Format("  climb           : {0} textures, {1:+0.0;-0.0} MB texture memory",
                measured[measured.Count - 1].Textures - measured[0].Textures,
                (measured[measured.Count - 1].TextureMemory - measured[0].TextureMemory) / 1024f / 1024f));

            string report = string.Join("\n", lines);
            Debug.Log("[memory] " + report);

            string directory = Path.Combine(Application.temporaryCachePath, "MemoryReports");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "twin-activity.txt"),
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n" + report + "\n");
        }
    }
}
