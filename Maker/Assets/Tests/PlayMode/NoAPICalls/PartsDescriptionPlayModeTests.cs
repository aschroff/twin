using System.Collections;
using System.IO;
using System.Linq;
using Code;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// What "describe every part" may and may not touch.
    /// </summary>
    /// <remarks>
    /// <para>None of these spends a token. A part without a screenshot is never sent to the model —
    /// and that is exactly the path that used to destroy text: the run stamped a placeholder into
    /// every <c>part.description</c> <em>before</em> asking, so when the asking then did not happen,
    /// the placeholder was what stayed. "No description" where a doctor had typed a finding, with
    /// no undo.</para>
    ///
    /// <para>So these tests paint parts, leave them without a screenshot, and check that the run
    /// leaves their text alone — in the plain variant and in the forced one.</para>
    /// </remarks>
    [Category(Processes.DescribeAndReport)]
    public class PartsDescriptionPlayModeTests : TwinPaintTestBase
    {
        private const string Variant = "Part Description";
        private const string TypedByHand = "Schwellung beidseits, seit drei Wochen";

        [UnityTest]
        public IEnumerator Describe_LeavesTextThatIsAlreadyThereAlone()
        {
            yield return PaintOnePart();
            PartManager.PartData part = FirstPart();
            part.description = TypedByHand;

            yield return RunAndWait(Process(forced: false));

            Assert.AreEqual(TypedByHand, part.description,
                "Describing the parts overwrote a description that was already there.");
        }

        [UnityTest]
        public IEnumerator Forced_WithoutAScreenshot_LeavesTheTextAlone()
        {
            yield return PaintOnePart();
            PartManager.PartData part = FirstPart();
            part.description = TypedByHand;

            yield return RunAndWait(Process(forced: true));

            Assert.AreEqual(TypedByHand, part.description,
                "The forced run replaced the text although it never got an answer to replace it with. "
                + "Only the model's own answer may land in part.description.");
        }

        [UnityTest]
        public IEnumerator Candidates_SkipDescribedParts_UnlessForced()
        {
            yield return PaintTwoParts();
            PartManager partManager = FindPartManager();
            var parts = partManager.groups.SelectMany(g => g.groupParts).ToList();
            Assert.AreEqual(2, parts.Count, "Expected two painted parts.");

            parts[0].description = TypedByHand;
            parts[1].description = "";

            var plain = Process(forced: false).Candidates().Select(c => c.part).ToList();
            CollectionAssert.DoesNotContain(plain, parts[0], "A described part was offered to the plain run.");
            CollectionAssert.Contains(plain, parts[1], "The part without a description was not picked up.");

            var forced = Process(forced: true).Candidates().Select(c => c.part).ToList();
            CollectionAssert.Contains(forced, parts[0], "The forced run left a described part out.");
            CollectionAssert.Contains(forced, parts[1], "The forced run left an empty part out.");
        }

        /// <summary>
        /// The number a button shows has to be the number of parts a press would describe.
        /// </summary>
        /// <remarks>Without a screenshot there is nothing to ask the model about, so such a part
        /// does not count — it belongs to the images button instead.</remarks>
        [UnityTest]
        public IEnumerator CountDescribable_CountsOnlyPartsThatHaveAnImage()
        {
            yield return PaintOnePart();
            PartsDescriptionProcess process = Process(forced: false);

            Assert.AreEqual(1, process.Candidates().Count, "The painted part should be in scope.");
            Assert.AreEqual(0, process.CountDescribable(),
                "A part without a screenshot must not be counted - it cannot be described.");

            WriteScreenshotFor(FirstGroup(), FirstPart());

            Assert.AreEqual(1, process.CountDescribable(),
                "Once the image is there the part is describable and has to be counted.");
        }

        // ---------------- helpers ----------------

        private IEnumerator PaintOnePart()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker("Red");
        }

        private IEnumerator PaintTwoParts()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(partManager.groups[0]);
            yield return PaintWithMarker("Red");
            yield return SelectGroupForPainting(partManager.groups[1]);
            yield return PaintWithMarker("Green");
        }

        private static PartManager.GroupData FirstGroup()
        {
            return FindPartManager().groups.First(g => g.groupParts.Count > 0);
        }

        private static PartManager.PartData FirstPart()
        {
            return FirstGroup().groupParts[0];
        }

        /// <summary>The plain process or the forced one - they differ in nothing else.</summary>
        private static PartsDescriptionProcess Process(bool forced)
        {
            var process = Object
                .FindObjectsByType<PartsDescriptionProcess>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(p => p.hardRedo == forced);
            Assert.IsNotNull(process, $"No PartsDescriptionProcess with hardRedo == {forced} in the scene.");
            return process;
        }

        private static IEnumerator RunAndWait(PartsDescriptionProcess process, float timeout = 20f)
        {
            bool done = false;
            System.Action handler = null;
            handler = () => { done = true; process.ExecuteCompleted -= handler; };
            process.ExecuteCompleted += handler;

            process.ExecuteSync(Variant);

            float elapsed = 0f;
            while (!done && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(done, "The description run never reported itself finished.");
        }

        /// <summary>Puts a file where the run looks for a part's screenshot. What is in it does not
        /// matter here - only these tests' counting question does, and that asks the file system.</summary>
        private static void WriteScreenshotFor(PartManager.GroupData group, PartManager.PartData part)
        {
            var single = Object.FindFirstObjectByType<PartDescriptionProcess>();
            Assert.IsNotNull(single, "PartDescriptionProcess not found in scene.");

            string path = single.ScreenshotPath(group, part);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        }
    }
}
