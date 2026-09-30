using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The Region screen (Edit → Region): picking a body region from the bundled template
    /// catalog and painting it into the current group. The service behind it
    /// (PartTemplateService) has its own tests; these drive the screen itself, which had none
    /// (PROCESS_LANDSCAPE.md gap C).
    /// </summary>
    [Category(Processes.MarkUpTheBody)]
    public class RegionUiPlayModeTests : TwinPaintTestBase
    {
        private const string RegionListPanel = "Canvas/EditRegion UI/Bottom/Scroll/Panel";

        /// <summary>Opening Edit → Region shows a non-empty region list, and Link returns to Edit.</summary>
        [UnityTest]
        public IEnumerator RegionButton_OpensRegionModeWithPopulatedList()
        {
            yield return LoadLipEdemaTwin();

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Region");

            // unlike its siblings (Marker, Filler, …), only the Region button's Icon child is
            // wired to InteractionController.EnableMode — Icon Background/Text/Text Background
            // carry no listener, so clicking there does nothing in the real app either.
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Region/Icon");
            yield return WaitForModeActive("EditRegion");

            var panel = FindGameObjectByPath(RegionListPanel);
            Assert.Greater(panel.transform.childCount, 0, "Region list should be populated from the template catalog.");

            yield return ClickButtonByPath("Canvas/EditRegion UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
        }

        /// <summary>Tapping a region row paints it into the currently active group, with the
        /// new part carrying the region's key and localized name, and the paint showing on the body.</summary>
        [UnityTest]
        public IEnumerator SelectRegion_PaintsItIntoCurrentGroup()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            int partsBefore = swell.groupParts.Count;

            var armsTwin = PartTemplateService.GetTemplateCatalog().twins.First(t => t.twinName == "Arms.twin");
            var shoulderRegion = armsTwin.regions.First(r => r.key == "shoulder_front_left");

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Region/Icon");
            yield return WaitForModeActive("EditRegion");

            var regionEntry = FindChildWithTextValue(RegionListPanel, shoulderRegion.displayName, "Action/Region");
            Assert.IsNotNull(regionEntry, $"Region row for '{shoulderRegion.displayName}' not found in the region list.");

            yield return ClickButtonByPath(path: "Icon", root: regionEntry);
            yield return null;
            yield return null; // let CwPaintableManager flush the replayed commands

            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Region paint should add one part to the active group.");
            var newPart = swell.groupParts[swell.groupParts.Count - 1];
            Assert.AreEqual("shoulder_front_left", newPart.regionKey, "Painted part should carry the region key.");
            Assert.AreEqual(shoulderRegion.displayName, newPart.description, "Painted part should carry the region's localized name.");

            yield return ClickButtonByPath("Canvas/EditRegion UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
        }

        /// <summary>A region-painted part survives leaving and reloading the twin, keeping its
        /// region key and its link back to the group.</summary>
        [UnityTest]
        public IEnumerator SelectRegion_SurvivesSaveAndReload()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            int partsBefore = swell.groupParts.Count;

            var armsTwin = PartTemplateService.GetTemplateCatalog().twins.First(t => t.twinName == "Arms.twin");
            var shoulderRegion = armsTwin.regions.First(r => r.key == "shoulder_front_left");

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Region/Icon");
            yield return WaitForModeActive("EditRegion");

            var regionEntry = FindChildWithTextValue(RegionListPanel, shoulderRegion.displayName, "Action/Region");
            Assert.IsNotNull(regionEntry, $"Region row for '{shoulderRegion.displayName}' not found in the region list.");
            yield return ClickButtonByPath(path: "Icon", root: regionEntry);
            yield return null;
            yield return null;

            // without this, a failed paint would make Last() pick a part that was already there
            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Setup: region paint should add one part to the active group.");
            string partId = swell.groupParts.Last().id;

            yield return ClickButtonByPath("Canvas/EditRegion UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");

            // leave the twin and come back (switching twins saves the current one)
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");

            partManager = FindPartManager();
            var reloadedSwell = partManager.groups.First(g => g.name == "Swell");
            var reloadedPart = reloadedSwell.groupParts.FirstOrDefault(p => p.id == partId);

            Assert.IsNotNull(reloadedPart, "Region-painted part did not survive save/reload.");
            Assert.AreEqual("shoulder_front_left", reloadedPart.regionKey, "Region key should survive save/reload.");
            Assert.AreEqual(shoulderRegion.displayName, reloadedPart.description, "Region name should survive save/reload.");
            Assert.AreSame(reloadedSwell, reloadedPart.group, "Part must still be linked to its group after reload.");
        }
    }
}
