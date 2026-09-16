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
    /// The three things the part detail page offers have to do something.
    /// </summary>
    /// <remarks>
    /// <para>The Delete entry sat there with an empty <c>onClick</c> - a button that looked like
    /// every other one and did nothing when tapped. Nothing failed, nothing was logged; it was
    /// found by a tester. An entry without an action is exactly the kind of thing no other test
    /// notices, so it is asserted here.</para>
    ///
    /// <para>The labels are checked as well, because they are localization keys: they used to read
    /// "Delete" and "Describe", which no table has, so <c>StringLocalizer</c> handed the key back
    /// and all five locales showed the English word.</para>
    /// </remarks>
    [Category(Processes.DescribeAndReport)]
    public class PartMenuPlayModeTests : TwinPaintTestBase
    {
        private const string MenuPath = "Canvas/Part UI/Scroll/Panel";

        [UnityTest]
        public IEnumerator PartMenu_OffersThreeEntries_EachWithAnActionAndALabel()
        {
            yield return OpenPartDetail();

            MenuManager menu = FindGameObjectByPath(MenuPath).GetComponent<MenuManager>();
            Assert.IsNotNull(menu, "No MenuManager at '" + MenuPath + "'.");
            Assert.AreEqual(3, menu.menu.Count,
                "The part page should offer taking the picture, describing, and deleting.");

            foreach (var pair in menu.menu)
            {
                MenuAction action = pair.Value;
                Assert.IsNotNull(action, "Entry '" + pair.Key + "' has no action object at all.");

                Assert.Greater(action.onClick.GetPersistentEventCount(), 0,
                    "Entry '" + action.text + "' has an empty onClick - it would look like a button "
                    + "and do nothing when tapped.");
                Assert.IsNotNull(action.onClick.GetPersistentTarget(0),
                    "Entry '" + action.text + "' points at nothing.");
                Assert.IsNotEmpty(action.onClick.GetPersistentMethodName(0),
                    "Entry '" + action.text + "' names no method.");
                Assert.IsNotNull(action.icon, "Entry '" + action.text + "' has no icon.");

                string shown = StringLocalizer.localizeString(action.text);
                Assert.AreNotEqual(action.text, shown,
                    "'" + action.text + "' is not in TwinLocalTables, so the menu shows the key "
                    + "itself in every language.");
            }
        }

        /// <summary>The entry that was dead: deleting has to actually take the part away.</summary>
        [UnityTest]
        public IEnumerator Delete_TakesThePartAwayAndGoesBackToTheBody()
        {
            yield return OpenPartDetail();

            PartManager partManager = FindPartManager();
            PartManager.PartData part = InteractionController.Partdata;
            Assert.IsNotNull(part, "No part on the detail page.");
            string id = part.id;

            var detail = Object.FindFirstObjectByType<PartDetailManager>();
            Assert.IsNotNull(detail, "No PartDetailManager in the scene.");
            detail.DeletePart();

            yield return WaitForModeActive("Main");

            Assert.IsNull(partManager.getPart(id), "The part is still in the twin after deleting it.");
            Assert.IsNull(InteractionController.Partdata,
                "The deleted part is still what the detail page would show next time.");
        }

        /// <summary>
        /// Taking the picture has to give the screen back.
        /// </summary>
        /// <remarks>
        /// The run hides the whole canvas while it works - <c>Recorder.Prepare</c> deactivates
        /// every child of Canvas, and this page is one of them. A run started <em>on</em> this page
        /// therefore dies the moment the page is deactivated: the screenshot still appears on disk,
        /// because that step runs on the process, but <c>Recorder.Reset</c> is never reached and the
        /// app is left on the bare body with no menus at all, until it is restarted. The file alone
        /// is not proof that this worked, so both are asserted.
        /// </remarks>
        [UnityTest]
        public IEnumerator CreateImage_ShootsThePartAndGivesTheScreenBack()
        {
            yield return OpenPartDetail();

            PartManager partManager = FindPartManager();
            PartManager.PartData part = InteractionController.Partdata;
            PartManager.GroupData group = partManager.getGroup(part);

            var single = Object.FindFirstObjectByType<PartDescriptionProcess>();
            string path = single.ScreenshotPath(group, part);
            Assert.IsFalse(File.Exists(path), "The part must start without a picture.");

            GameObject page = FindGameObjectByPath("Canvas/Part UI");
            var detail = Object.FindFirstObjectByType<PartDetailManager>();
            detail.CreateScreenshot();

            float waited = 0f;
            while (waited < 25f && !(File.Exists(path) && page.activeInHierarchy))
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(File.Exists(path), "No picture was written for the part.");
            Assert.IsTrue(page.activeInHierarchy,
                "The part page never came back - the canvas is still hidden and the app is stuck "
                + "on the bare body.");

            // and the picture is on the page rather than the placeholder
            yield return null;
            Assert.IsTrue(FindGameObjectByPath("Canvas/Part UI/Icon").activeSelf,
                "The picture is there but the page still shows the placeholder.");
        }

        private IEnumerator OpenPartDetail()
        {
            yield return LoadLipEdemaTwin();

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            PartManager.GroupData group = FindPartManager().groups.First(g => g.groupParts.Count > 0);
            InteractionController.Partdata = group.groupParts[0];
            InteractionController.EnableMode("Part");
            yield return WaitForModeActive("Part");
            yield return null;
        }
    }
}
