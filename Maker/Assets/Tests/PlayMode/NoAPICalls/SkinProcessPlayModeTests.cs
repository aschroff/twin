using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    [Category(Processes.DescribeAndReport)]
    public class SkinProcessPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator ExportSkin_WritesSkinPngForSelectedProfile()
        {
            yield return ResetApp();

            yield return ClickButtonByName("Save Button");

            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
            Assert.IsNotNull(twinEntry, "Fixture twin 'LipEdema' not found in save list.");

            yield return ClickButtonByPath(path: "DetailsMode", root: twinEntry);

            AssertModeActive("Menu");

            var dataManager = Object.FindObjectOfType<DataPersistenceManager>();
            var profileId = dataManager.selectedProfileId;

            var actionEntry = FindChildWithTextValue("Canvas/Menu UI/Bottom/Scroll/Panel", "Export skin", "Action/Text");
            Assert.IsNotNull(actionEntry, "'Export skin' menu entry not found.");

            yield return ClickButtonByPath(path: "Icon", root: actionEntry);
            yield return null;

            var expectedPath = Path.Combine(DataPaths.PersistentDataPath, profileId, "skin_" + profileId + ".png");
            Assert.IsTrue(File.Exists(expectedPath), $"Expected skin export at '{expectedPath}' was not created.");
        }
    }
}
