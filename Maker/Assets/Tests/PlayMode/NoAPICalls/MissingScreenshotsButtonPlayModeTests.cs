using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The "create missing images" button on the group detail panel: it knows how many parts have
    /// no screenshot, it shoots exactly those, and the list shows the picture afterwards.
    /// </summary>
    [Category(Processes.DescribeAndReport)]
    public class MissingScreenshotsButtonPlayModeTests : PlayModeTestBase
    {
        private const string ButtonPath = "Canvas/GroupDetailUI/Create Images Button";
        private const string PartListPath = "Canvas/GroupDetailUI/ScrollDetails/Panel";

        [UnityTest]
        public IEnumerator CreateMissing_ShootsTheUnshotPartAndShowsItInTheList()
        {
            yield return ResetApp();

            yield return ClickButtonByName("Save Button");

            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
            Assert.IsNotNull(twinEntry, "Fixture twin 'LipEdema' not found in save list.");

            yield return ClickButtonByPath(path: "Unselect", root: twinEntry);
            AssertModeActive("Save");

            // One painted mark, linked to a group: exactly one part, and nothing on disk for it -
            // the stock LipEdema fixture ships with its groups empty.
            yield return ClickButtonByName("Edit Button");
            AssertModeActive("Edit");

            var viewEntry = FindChildWithTextValue("Canvas/Overlays/View Overlay/Scroll/Panel", "Head front", "ReadOnlyMode/Text Background/ViewName");
            yield return ClickButtonByPath(path: "ReadOnlyMode/Icon", root: viewEntry);

            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            AssertModeActive("EditMarker");

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
            yield return DragOnCanvas("Canvas", new Vector2(20, 0));

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
                if (group.name == "Swell") swellGroup = group;
            }
            Assert.IsNotNull(swellGroup, "'Swell' group not found on PartManager.");
            Assert.AreEqual(1, swellGroup.groupParts.Count, "Expected exactly one part linked to 'Swell'.");

            var expectedPath = Path.Combine(
                DataPaths.PersistentDataPath,
                profileId,
                "screenshot_" + profileId + " - " + swellGroup.name + " - part " + swellGroup.groupParts[0].id + ".png");
            Assert.IsFalse(File.Exists(expectedPath), "The part must start without a screenshot.");

            // The group detail panel, reached the way GroupEdit.showDetails() reaches it.
            InteractionController.EnableMode("GroupDetail");
            yield return WaitForModeActive("GroupDetail");
            yield return null;
            yield return null;

            var button = FindGameObjectByPath(ButtonPath).GetComponent<MissingScreenshotsButton>();
            Assert.IsNotNull(button, "MissingScreenshotsButton not found at '" + ButtonPath + "'.");
            Assert.AreEqual(1, button.Missing, "The panel should have counted the one part without a screenshot.");
            Assert.IsTrue(FindButtonByPath(ButtonPath).interactable, "The button should be pressable while something is missing.");

            AssertRowShows(placeholder: true);

            yield return ClickButtonByPath(ButtonPath);

            // The run hides the whole canvas while it works, so nothing on screen can be asked;
            // the component is still there and says when it is over.
            yield return WaitUntilOrTimeout(() => !button.Running, 30f, "The screenshot run did not finish.");

            // GroupListSelectionManager.OnEnable rebuilds the part list one frame after the run
            // puts the panel back.
            yield return null;
            yield return null;

            Assert.IsTrue(File.Exists(expectedPath), $"Expected screenshot at '{expectedPath}' was not created.");
            Assert.AreEqual(0, button.Missing, "Nothing should be missing after the run.");
            Assert.IsFalse(FindButtonByPath(ButtonPath).interactable, "With nothing missing the button should be dead.");

            AssertRowShows(placeholder: false);
        }

        /// <summary>
        /// The one part row shows either its picture or the placeholder, never both.
        /// </summary>
        private void AssertRowShows(bool placeholder)
        {
            var list = FindGameObjectByPath(PartListPath);
            Assert.AreEqual(1, list.transform.childCount, "Expected exactly one part row.");

            var row = list.transform.GetChild(0);
            var icon = row.Find("Icon");
            var stand_in = row.Find("Placeholder");
            Assert.IsNotNull(icon, "Part row has no 'Icon'.");
            Assert.IsNotNull(stand_in, "Part row has no 'Placeholder'.");

            Assert.AreEqual(placeholder, stand_in.gameObject.activeSelf,
                placeholder ? "The row should show the placeholder." : "The placeholder should be gone.");
            Assert.AreEqual(!placeholder, icon.gameObject.activeSelf,
                placeholder ? "The row should not show a picture yet." : "The row should show the picture.");
        }

        private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds, string failureMessage)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(condition(), failureMessage);
        }
    }
}
