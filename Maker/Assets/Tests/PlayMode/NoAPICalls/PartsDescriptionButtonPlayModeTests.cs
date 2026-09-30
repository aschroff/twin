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
    /// The two description buttons on the group detail panel say what a press would do.
    /// </summary>
    /// <remarks>
    /// <para>No token is spent here. Pressing is not what is checked - what a press is
    /// <em>offered for</em> is: the number on the label and whether the button is alive. A button
    /// that offers work it cannot do is the whole problem this replaces, because a part without a
    /// screenshot can never be described - the model is asked about the picture.</para>
    ///
    /// <para>Three states, one part: no image (nothing to offer), image but no text (the plain
    /// button offers it), image and text (only the forced button still offers it).</para>
    /// </remarks>
    [Category(Processes.DescribeAndReport)]
    public class PartsDescriptionButtonPlayModeTests : TwinPaintTestBase
    {
        private const string MissingPath = "Canvas/GroupDetailUI/Describe Missing Button";
        private const string AllPath = "Canvas/GroupDetailUI/Describe All Button";

        [UnityTest]
        public IEnumerator Buttons_OfferOnlyThePartsThatCanActuallyBeDescribed()
        {
            yield return LoadLipEdemaTwin();

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            PartManager partManager = FindPartManager();
            yield return SelectGroupForPainting(partManager.groups[0]);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            PartManager.GroupData group = FindPartManager().groups.First(g => g.groupParts.Count > 0);
            PartManager.PartData part = group.groupParts[0];

            InteractionController.EnableMode("GroupDetail");
            yield return WaitForModeActive("GroupDetail");
            yield return null;
            yield return null;

            PartsDescriptionButton missing = Button(MissingPath);
            PartsDescriptionButton all = Button(AllPath);
            Assert.IsFalse(missing.Forced, "The first button is the one that fills the gaps.");
            Assert.IsTrue(all.Forced, "The second button is the one that describes everything again.");

            // no screenshot: neither button has anything to offer, however empty the text is
            Assert.AreEqual(0, missing.Describable,
                "A part without a screenshot cannot be described and must not be counted.");
            Assert.AreEqual(0, all.Describable,
                "The forced button cannot describe a part without a screenshot either.");
            Assert.IsFalse(FindButtonByPath(MissingPath).interactable, "Nothing to do, so the button is dead.");
            Assert.IsFalse(FindButtonByPath(AllPath).interactable, "Nothing to do, so the button is dead.");
            AssertDimmed(MissingPath, dimmed: true);
            AssertDimmed(AllPath, dimmed: true);

            // an image arrives - now the part is describable and has no text yet
            WriteScreenshotFor(group, part);
            missing.Refresh();
            all.Refresh();

            Assert.AreEqual(1, missing.Describable, "With an image and no text the plain button offers the part.");
            Assert.AreEqual(1, all.Describable, "The forced button offers every part that has an image.");
            Assert.IsTrue(FindButtonByPath(MissingPath).interactable, "The button should be pressable now.");
            Assert.IsTrue(FindButtonByPath(AllPath).interactable, "The button should be pressable now.");
            AssertDimmed(MissingPath, dimmed: false);
            AssertDimmed(AllPath, dimmed: false);

            // the part gets a description - the plain button is done, the forced one is not
            part.description = "Schwellung beidseits";
            missing.Refresh();
            all.Refresh();

            Assert.AreEqual(0, missing.Describable,
                "A part that already says something is not a gap to fill.");
            Assert.AreEqual(1, all.Describable,
                "Describing everything again is exactly what the forced button is for.");
            Assert.IsFalse(FindButtonByPath(MissingPath).interactable, "Nothing left to fill in.");
            Assert.IsTrue(FindButtonByPath(AllPath).interactable, "The forced button stays pressable.");
        }

        /// <summary>The button is drawn through a CanvasGroup, like every other button on this
        /// panel - a dead button has to look dead, not merely refuse the tap.</summary>
        private static void AssertDimmed(string path, bool dimmed)
        {
            var group = Button(path).GetComponent<CanvasGroup>();
            Assert.IsNotNull(group, "No CanvasGroup on '" + path + "' - it cannot be dimmed.");
            if (dimmed)
            {
                Assert.Less(group.alpha, 0.3f,
                    "'" + path + "' has nothing to do and should be drawn faint, alpha was " + group.alpha + ".");
            }
            else
            {
                Assert.Greater(group.alpha, 0.3f,
                    "'" + path + "' is usable and should be drawn like the other buttons, alpha was "
                    + group.alpha + ".");
            }
        }

        private static PartsDescriptionButton Button(string path)
        {
            GameObject host = Object.FindObjectsByType<PartsDescriptionButton>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(b => b.gameObject)
                .FirstOrDefault(go => GetPath(go.transform).EndsWith(path));
            Assert.IsNotNull(host, "No PartsDescriptionButton at '" + path + "'.");
            return host.GetComponent<PartsDescriptionButton>();
        }

        private static string GetPath(Transform target)
        {
            string path = target.name;
            for (Transform t = target.parent; t != null; t = t.parent) path = t.name + "/" + path;
            return path;
        }

        /// <summary>Puts a file where the run looks for a part's screenshot - only its presence
        /// is the question here.</summary>
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
