using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaintCore;
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

        /// <summary>Time for the replayed region commands to reach the body texture (i.e. time to paint the color onto the body) (flushed in
        /// LateUpdate, and a replay right after a change can render late - see the painting guide).</summary>
        private const float SettleSeconds = 0.5f;

        private const string ShoulderRegion = "shoulder_front_left";

        /// <summary>Opening Edit → Region shows a non-empty region list, and Link returns to Edit.</summary>
        [UnityTest]
        public IEnumerator RegionButton_OpensRegionModeWithPopulatedList()
        {
            yield return LoadLipEdemaTwin();

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Region");
            yield return OpenRegionScreen();

            var panel = FindGameObjectByPath(RegionListPanel);
            Assert.Greater(panel.transform.childCount, 0, "Region list should be populated from the template catalog.");

            yield return LeaveRegionScreen();
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

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenRegionScreen();
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] blank = BodyPixels();

            yield return ClickRegion(ShoulderRegion);
            yield return new WaitForSeconds(SettleSeconds);

            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Region paint should add one part to the active group.");
            var newPart = swell.groupParts.Last();
            Assert.AreEqual(ShoulderRegion, newPart.regionKey, "Painted part should carry the region key.");
            Assert.AreEqual(ArmsRegion(ShoulderRegion).displayName, newPart.description, "Painted part should carry the region's localized name.");
            AssertPartsAreUsable(partManager);
            Assert.Greater(DifferingPixels(blank, BodyPixels()), 0, "Region paint should show on the body.");

            yield return LeaveRegionScreen();
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

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenRegionScreen();
            yield return ClickRegion(ShoulderRegion);

            // without this, a failed paint would make Last() pick a part that was already there
            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Setup: region paint should add one part to the active group.");
            string partId = swell.groupParts.Last().id;

            yield return LeaveRegionScreen();

            // leave the twin and come back (switching twins saves the current one)
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");

            partManager = FindPartManager();
            var reloadedSwell = partManager.groups.First(g => g.name == "Swell");
            var reloadedPart = reloadedSwell.groupParts.FirstOrDefault(p => p.id == partId);

            Assert.IsNotNull(reloadedPart, "Region-painted part did not survive save/reload.");
            Assert.AreEqual(ShoulderRegion, reloadedPart.regionKey, "Region key should survive save/reload.");
            Assert.AreEqual(ArmsRegion(ShoulderRegion).displayName, reloadedPart.description, "Region name should survive save/reload.");
            Assert.AreSame(reloadedSwell, reloadedPart.group, "Part must still be linked to its group after reload.");
        }

/// <summary>A region is painted with the marker the user picked before opening the
        /// Region screen - its name and colour go onto the new part.</summary>
        [UnityTest]
        public IEnumerator SelectRegion_UsesTheMarkerSelectedBefore()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            int partsBefore = swell.groupParts.Count;

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            yield return WaitForModeActive("EditMarker");
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
            AssertGameObjectActive("Tools/Red");
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");

            yield return OpenRegionScreen();
            yield return ClickRegion(ShoulderRegion);

            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Setup: region paint should add one part to the active group.");
            var newPart = swell.groupParts.Last();
            Assert.AreEqual("Red", newPart.nameTool, "Region paint should use the marker selected before.");
            Assert.AreEqual(ToolColor("Red"), newPart.colorTool, "Region paint should carry the selected marker's colour.");
        }

        /// <summary>A sticker cannot carry a region template (those are sphere strokes), so with a
        /// sticker selected the region is still painted - with the first marker instead.</summary>
        [UnityTest]
        public IEnumerator SelectRegion_WithStickerSelected_FallsBackToFirstMarker()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            int partsBefore = swell.groupParts.Count;

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Sticker/Text Background/Text");
            yield return WaitForModeActive("EditSticker");
            yield return ClickButtonByPath("Canvas/EditSticker UI/Bottom/Scroll/Panel/Scroll tool button and link and text 1");
            AssertGameObjectActive("Tools/Sticker 1");
            yield return ClickButtonByPath("Canvas/EditSticker UI/Bottom/Buttons/Edit");
            yield return WaitForModeActive("Edit");

            yield return OpenRegionScreen();
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] blank = BodyPixels();
            yield return ClickRegion(ShoulderRegion);
            yield return new WaitForSeconds(SettleSeconds);

            // painting with the sticker itself would throw inside the click handler and add nothing
            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "With a sticker selected, the region should still be painted.");
            var newPart = swell.groupParts.Last();
            // "Blue" is the first marker (sphere tool, no fill) in the scene's Tools container
            Assert.AreEqual("Blue", newPart.nameTool, "With a sticker selected, the region should be painted with the first marker.");
            Assert.AreEqual(PartManager.Tool.MarkerLine, newPart.typeTool, "The fallback tool should be a line marker.");
            Assert.AreEqual(ToolColor("Blue"), newPart.colorTool, "The part should carry the fallback marker's colour.");
            Assert.Greater(DifferingPixels(blank, BodyPixels()), 0, "The fallback paint should show on the body.");
        }

/// <summary>The Region screen switches off the camera gestures (LeanTouch), like every
        /// other edit screen - otherwise a swipe over the list would turn the body.</summary>
        [UnityTest]
        public IEnumerator RegionScreen_DisablesCameraGestures()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            // Edit mode already switches them off - switch them on again, so that only the
            // Region screen can be the one that turns them off
            GameObject touch = FindTouchOfRegionMode();
            touch.SetActive(true);

            yield return OpenRegionScreen();

            Assert.IsFalse(touch.activeSelf, "Camera gestures must be off on the Region screen.");
        }
        

        /// <summary>From Edit mode into the Region screen.</summary>
        private IEnumerator OpenRegionScreen()
        {
            // unlike its siblings (Marker, Filler, …), only the Region button's Icon child is
            // wired to InteractionController.EnableMode — Icon Background/Text/Text Background
            // carry no listener, so clicking there does nothing in the real app either.
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Region/Icon");
            yield return WaitForModeActive("EditRegion");
        }

        /// <summary>From the Region screen back to Edit mode.</summary>
        private IEnumerator LeaveRegionScreen()
        {
            yield return ClickButtonByPath("Canvas/EditRegion UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
        }

        /// <summary>Taps the row of an Arms.twin region and waits until its paint is flushed.</summary>
        private IEnumerator ClickRegion(string regionKey)
        {
            var region = ArmsRegion(regionKey);
            var regionEntry = FindChildWithTextValue(RegionListPanel, region.displayName, "Action/Region");
            Assert.IsNotNull(regionEntry, $"Region row for '{region.displayName}' not found in the region list.");
            yield return ClickButtonByPath(path: "Icon", root: regionEntry);
            yield return null;
            yield return null; // let CwPaintableManager flush the replayed commands
        }

        private static PartTemplateService.TemplateRegionInfo ArmsRegion(string regionKey)
        {
            return PartTemplateService.GetTemplateCatalog().twins
                .First(t => t.twinName == "Arms.twin").regions.First(r => r.key == regionKey);
        }

        private Color ToolColor(string toolName)
        {
            return FindGameObjectByPath($"Tools/{toolName}").GetComponent<PaintIn3D.CwPaintSphere>().Color;
        }

        /// <summary>The LeanTouch object the Region screen is wired to (a private serialized field).</summary>
        private static GameObject FindTouchOfRegionMode()
        {
            var mode = Resources.FindObjectsOfTypeAll<EditRegionMode>().First(m => m.gameObject.scene.IsValid());
            var field = typeof(EditRegionMode).GetField("Touch",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var touch = (GameObject)field.GetValue(mode);
            Assert.IsNotNull(touch, "EditRegionMode has no Touch object wired.");
            return touch;
        }

        private static Color32[] BodyPixels()
        {
            Body body = Object.FindObjectOfType<Body>();
            Assert.IsNotNull(body, "No Body found in the scene.");
            CwPaintableTexture texture = body.GetComponent<CwPaintableTexture>();
            Assert.IsNotNull(texture, "The Body has no paintable texture.");
            Texture2D copy = texture.GetReadableCopy();
            Assert.IsNotNull(copy, "Could not read the body texture.");
            Color32[] pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        private static int DifferingPixels(Color32[] before, Color32[] after)
        {
            Assert.AreEqual(before.Length, after.Length, "The body texture changed its size.");
            int differing = 0;
            for (int i = 0; i < before.Length; i++)
            {
                Color32 a = before[i];
                Color32 b = after[i];
                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a)
                {
                    differing++;
                }
            }
            return differing;
        }
    }
}
