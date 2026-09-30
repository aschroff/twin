using System.Collections;
using System.IO;
using Code;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    [Category(Processes.DescribeAndReport)]
    public class PartsScreenshotProcessPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator ExecuteSync_WritesScreenshotForLinkedPart()
        {
            yield return ResetApp();

            yield return ClickButtonByName("Save Button");

            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
            Assert.IsNotNull(twinEntry, "Fixture twin 'LipEdema' not found in save list.");

            yield return ClickButtonByPath(path: "Unselect", root: twinEntry);
            AssertModeActive("Save");

            // paint on twin to ensure existence of a part
            yield return ClickButtonByName("Edit Button");
            AssertModeActive("Edit");

            var viewEntry = FindChildWithTextValue("Canvas/Overlays/View Overlay/Scroll/Panel", "Head front", "ReadOnlyMode/Text Background/ViewName");
            yield return ClickButtonByPath(path: "ReadOnlyMode/Icon", root: viewEntry);

            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            AssertModeActive("EditMarker");

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
            yield return DragOnCanvas("Canvas", new Vector2(20, 0));

            // Links the just-painted mark to the "Swell" group, creating one real PartData entry
            // to screenshot - the stock LipEdema fixture ships with its groups empty.
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            AssertModeActive("Edit");

            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            AssertModeActive("Main");

            var partManager = Object.FindFirstObjectByType<PartManager>();
            var dataManager = Object.FindFirstObjectByType<DataPersistenceManager>();
            var profileId = dataManager.selectedProfileId;

            PartManager.GroupData swellGroup = null;
            foreach (var group in partManager.groups)
            {
                if (group.name == "Swell")
                {
                    swellGroup = group;
                    break;
                }
            }
            Assert.IsNotNull(swellGroup, "'Swell' group not found on PartManager.");
            Assert.AreEqual(1, swellGroup.groupParts.Count, "Expected exactly one part linked to 'Swell'.");
            var part = swellGroup.groupParts[0];

            // PartsScreenshotProcess is invoked this way (ExecuteSync + awaiting ExecuteCompleted)
            // by Processes.VersionSequenceProcess (SequenceProcess) in production.
            var process = Object.FindFirstObjectByType<PartsScreenshotProcess>();
            Assert.IsNotNull(process, "PartsScreenshotProcess not found in scene.");

            var completed = false;
            process.ExecuteCompleted += () => completed = true;
            process.ExecuteSync("Medical Report");

            var elapsed = 0f;
            while (!completed && elapsed < 10f)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }
            Assert.IsTrue(completed, "PartsScreenshotProcess did not signal ExecuteCompleted in time.");

            var expectedPath = Path.Combine(
                DataPaths.PersistentDataPath,
                profileId,
                "screenshot_" + profileId + " - " + swellGroup.name + " - part " + part.id + ".png");
            Assert.IsTrue(File.Exists(expectedPath), $"Expected screenshot at '{expectedPath}' was not created.");
        }
    }
}
