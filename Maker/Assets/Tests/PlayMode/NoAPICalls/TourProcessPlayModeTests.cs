using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    public class TourProcessPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator ExportStandardViews_WritesScreenshotPerView()
        {
            yield return ResetApp();

            yield return ClickButtonByName("Save Button");

            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
            Assert.IsNotNull(twinEntry, "Fixture twin 'LipEdema' not found in save list.");

            yield return ClickButtonByPath(path: "DetailsMode", root: twinEntry);

            AssertModeActive("Menu");

            var dataManager = Object.FindObjectOfType<DataPersistenceManager>();
            var profileId = dataManager.selectedProfileId;
            var standardViewManager = Object.FindObjectOfType<StandardViewManager>();
            Assert.IsTrue(standardViewManager.views.Count > 0, "No standard views configured to export.");

            var actionEntry = FindChildWithTextValue("Canvas/Menu UI/Bottom/Scroll/Panel", "Export standard views", "Action/Text");
            Assert.IsNotNull(actionEntry, "'Export standard views' menu entry not found.");

            yield return ClickButtonByPath(path: "Icon", root: actionEntry);

            // TourProcess.Execute only starts a coroutine
            for (var i = 0; i < standardViewManager.views.Count + 2; i++)
            {
                yield return null;
            }

            foreach (var view in standardViewManager.views)
            {
                var expectedPath = Path.Combine(
                    DataPaths.PersistentDataPath,
                    profileId,
                    "screenshot_" + profileId + " - " + view.name + ".png");
                Assert.IsTrue(File.Exists(expectedPath), $"Expected screenshot for view '{view.name}' at '{expectedPath}' was not created.");
            }
        }
    }
}
