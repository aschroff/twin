using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// The Delete screen (Edit → Delete) and the eraser behind it. The eraser is the tool
    /// <c>Tools/Original</c> — a sphere in blend mode REPLACE_ORIGINAL that paints the body's
    /// original texture back. (<c>Tools/Delete</c> is an unused leftover: inactive, no button
    /// isolates it, and its blend mode would make it a marker.) An erase stroke is recorded like
    /// any other stroke: as a part of the current group, of tool type Delete.
    /// Until now checked by hand only (manual catalogue 06/04, PROCESS_LANDSCAPE.md P02 gap A).
    /// </summary>
    [Category(Processes.MarkUpTheBody)]
    public class DeleteUiPlayModeTests : TwinPaintTestBase
    {
        private const string Eraser = "Tools/Original";
        private const string SizeDropdown = "Canvas/EditDelete UI/Bottom/Size/GameObject/DeleteSize";

        /// <summary>Time for paint to reach the body texture and for a replay after a twin load
        /// to render (see the painting guide).</summary>
        private const float SettleSeconds = 0.5f;

        /// <summary>Radius step of the size dropdown (<c>SetRadius.multiplicator</c>): entry i
        /// sets the radius to (i + 1) × this.</summary>
        private const float RadiusStep = 0.01f;

        // ------------------------------------------------------------------ the screen

        /// <summary>Edit → Delete opens the Delete screen with the eraser as the active tool,
        /// and its Edit button returns to Edit mode.</summary>
        [UnityTest]
        public IEnumerator DeleteButton_OpensDeleteScreenWithEraser()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Delete");

            yield return OpenDeleteScreen();

            AssertGameObjectActive("Canvas/EditDelete UI");
            AssertGameObjectActive(Eraser);
            Assert.AreEqual(PartManager.Tool.Delete, PartManager.DeriveType(FindGameObjectByPath(Eraser)),
                "The tool the Delete button selects must be the eraser.");
            Assert.AreEqual(1, ActiveTools().Length,
                $"Only the eraser may be active, found: {string.Join(", ", ActiveTools().Select(t => t.name))}");

            yield return LeaveDeleteScreen();
        }

        /// <summary>Every part of the Delete button (icon, its background, the label and the
        /// label's background) opens the Delete screen — a tap is not lost on any of them.</summary>
        [UnityTest]
        public IEnumerator DeleteButton_EveryPartOpensTheScreen(
            [Values("Icon", "Icon Background", "Text Background", "Text Background/Text")] string part)
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            yield return TapByPath($"Canvas/Edit UI/Bottom/Delete/{part}");
            yield return WaitForModeActive("EditDelete");
            AssertGameObjectActive(Eraser);
        }

        /// <summary>The Delete screen switches off the camera gestures (LeanTouch) — otherwise
        /// an erase stroke would turn the body at the same time.</summary>
        [UnityTest]
        public IEnumerator DeleteScreen_DisablesCameraGestures()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            // Edit mode already switches them off - switch them on again, so that only the
            // Delete screen can be the one that turns them off
            GameObject touch = FindTouchOfDeleteMode();
            touch.SetActive(true);

            yield return OpenDeleteScreen();

            Assert.IsFalse(touch.activeSelf, "Camera gestures must be off on the Delete screen.");
        }

        /// <summary>The size the dropdown shows is the size the eraser has when the screen opens.</summary>
        [UnityTest]
        public IEnumerator SizeDropdown_ShowsTheEraserSize()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenDeleteScreen();

            var dropdown = FindGameObjectByPath(SizeDropdown).GetComponent<TMPro.TMP_Dropdown>();
            string shown = dropdown.captionText.text;
            int shownIndex = dropdown.options.FindIndex(o => o.text == shown);
            Assert.GreaterOrEqual(shownIndex, 0, $"The dropdown shows '{shown}', which is none of its entries.");
            Assert.AreEqual((shownIndex + 1) * RadiusStep, EraserRadius(), 1e-6f,
                $"The dropdown shows '{shown}', but the eraser has another size.");
        }

        /// <summary>Picking a size in the dropdown sets the eraser's radius: the smallest entry
        /// gives the smallest radius, the largest the largest.</summary>
        [UnityTest]
        public IEnumerator SizeDropdown_SetsTheEraserRadius()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenDeleteScreen();

            var dropdown = FindGameObjectByPath(SizeDropdown).GetComponent<TMPro.TMP_Dropdown>();
            int last = dropdown.options.Count - 1;
            Assert.Greater(last, 0, "The size dropdown should offer more than one size.");

            dropdown.value = 0;
            yield return null;
            Assert.AreEqual(1 * RadiusStep, EraserRadius(), 1e-6f, "Smallest entry, smallest radius.");

            dropdown.value = 4;
            yield return null;
            Assert.AreEqual(5 * RadiusStep, EraserRadius(), 1e-6f, "Fifth entry, five steps.");

            dropdown.value = last;
            yield return null;
            Assert.AreEqual((last + 1) * RadiusStep, EraserRadius(), 1e-6f, "Largest entry, largest radius.");
        }

        // ------------------------------------------------------------------ the eraser

        /// <summary>Erasing over a marker stroke takes the paint off the body again.</summary>
        [UnityTest]
        public IEnumerator Erase_RemovesPaintFromTheBody()
        {
            yield return LoadLipEdemaTwin();
            var swell = FindPartManager().groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            yield return PaintRedStroke();
            Color32[] blank = blankBody;
            int painted = DifferingPixels(blank, BodyPixels());
            Assert.Greater(painted, 0, "Setup: the red stroke should show on the body.");

            yield return EraseAtTheSameSpot();

            int left = DifferingPixels(blank, BodyPixels());
            Assert.Less(left, painted / 10,
                $"Erasing should take the paint off: {left} of {painted} painted pixels are still there.");
        }

        /// <summary>An erase stroke goes into the current group as a part of its own, of tool
        /// type Delete — the paint it erases is not touched in the data.</summary>
        [UnityTest]
        public IEnumerator Erase_IsRecordedAsDeletePartOfCurrentGroup()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            yield return PaintRedStroke();
            Assert.AreEqual(1, swell.groupParts.Count, "Setup: the red stroke should be one part.");
            var redPart = swell.groupParts[0];

            yield return EraseAtTheSameSpot();
            yield return LeaveDeleteScreen();
            // leaving Edit closes the current part, which stamps its tool information
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            Assert.AreEqual(2, swell.groupParts.Count, "The erase stroke should be a part of its own.");
            Assert.AreSame(redPart, swell.groupParts[0], "The erased part itself stays in the group.");
            var erasePart = swell.groupParts[1];
            Assert.AreEqual(PartManager.Tool.Delete, erasePart.typeTool, "The erase part should be of tool type Delete.");
            Assert.AreEqual(PartManager.Tool.MarkerLine, redPart.typeTool, "The red part keeps its tool type.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>What was erased stays erased after leaving the twin and coming back.</summary>
        [UnityTest]
        public IEnumerator Erase_SurvivesSaveAndReload()
        {
            yield return LoadLipEdemaTwin();
            var swell = FindPartManager().groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            yield return PaintRedStroke();
            Color32[] blank = blankBody;
            int painted = DifferingPixels(blank, BodyPixels());
            yield return EraseAtTheSameSpot();
            yield return LeaveDeleteScreen();

            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");
            yield return new WaitForSeconds(SettleSeconds);

            var reloadedSwell = FindPartManager().groups.First(g => g.name == "Swell");
            Assert.AreEqual(2, reloadedSwell.groupParts.Count, "Paint and erase part should both survive the reload.");
            Assert.AreEqual(PartManager.Tool.Delete, reloadedSwell.groupParts[1].typeTool,
                "The erase part should still be of tool type Delete after the reload.");

            int left = DifferingPixels(blank, BodyPixels());
            Assert.Less(left, painted / 10,
                $"The erased paint came back after the reload: {left} of {painted} painted pixels are there again.");
        }

        /// <summary>Undo takes the erase stroke back, so the erased paint shows again.</summary>
        [UnityTest]
        public IEnumerator Undo_BringsTheErasedPaintBack()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            yield return PaintRedStroke();
            Color32[] blank = blankBody;
            int painted = DifferingPixels(blank, BodyPixels());
            yield return EraseAtTheSameSpot();
            Assert.Less(DifferingPixels(blank, BodyPixels()), painted / 10, "Setup: the paint should be erased.");

            Assert.IsTrue(partManager.Undo(), "The erase stroke should be undoable.");
            yield return new WaitForSeconds(SettleSeconds);

            Assert.AreEqual(1, swell.groupParts.Count, "Undo should take only the erase part out of the group.");
            int back = DifferingPixels(blank, BodyPixels());
            Assert.Greater(back, painted * 9 / 10,
                $"After undoing the erase the paint should show again: {back} of {painted} pixels.");
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>The body as it was right before <see cref="PaintRedStroke"/> painted on it.</summary>
        private Color32[] blankBody;

        /// <summary>From the Main screen: into Edit, frames the body and paints one red stroke at
        /// the screen centre into the current group. Ends in Edit mode; the untouched body is
        /// kept in <see cref="blankBody"/>.</summary>
        private IEnumerator PaintRedStroke()
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return new WaitForSeconds(SettleSeconds);
            blankBody = BodyPixels();

            yield return PaintWithMarker("Red");
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>From Edit mode: opens the Delete screen, sets the eraser to the marker's
        /// size and erases at the screen centre, where <see cref="PaintRedStroke"/> painted.
        /// Stays on the Delete screen.</summary>
        private IEnumerator EraseAtTheSameSpot()
        {
            yield return OpenDeleteScreen();
            var dropdown = FindGameObjectByPath(SizeDropdown).GetComponent<TMPro.TMP_Dropdown>();
            dropdown.value = dropdown.options.Count - 1; // the largest size, the marker's radius
            yield return null;

            yield return DragOnCanvas("Canvas", new Vector2(20, 0));
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>From Edit mode into the Delete screen. A tap, not a click: the Delete button
        /// opens the screen through its onClick but selects the eraser through a CwDemoButton.</summary>
        private IEnumerator OpenDeleteScreen()
        {
            yield return TapByPath("Canvas/Edit UI/Bottom/Delete/Text Background/Text");
            yield return WaitForModeActive("EditDelete");
        }

        /// <summary>From the Delete screen back to Edit mode.</summary>
        private IEnumerator LeaveDeleteScreen()
        {
            yield return ClickButtonByPath("Canvas/EditDelete UI/Bottom/Buttons/Edit");
            yield return WaitForModeActive("Edit");
        }

        private IEnumerator LeaveDeleteScreenIfOpen()
        {
            if (FindGameObjectByPath("Canvas/EditDelete UI").activeInHierarchy)
            {
                yield return LeaveDeleteScreen();
            }
        }

        private float EraserRadius()
        {
            return FindGameObjectByPath(Eraser).GetComponent<PaintIn3D.CwPaintSphere>().Radius;
        }

        private GameObject[] ActiveTools()
        {
            return FindGameObjectByPath(Eraser).transform.parent.Cast<Transform>()
                .Where(t => t.gameObject.activeSelf).Select(t => t.gameObject).ToArray();
        }

        /// <summary>The LeanTouch object the Delete screen is wired to (a private serialized field).</summary>
        private static GameObject FindTouchOfDeleteMode()
        {
            var mode = Resources.FindObjectsOfTypeAll<EditDeleteMode>().First(m => m.gameObject.scene.IsValid());
            var field = typeof(EditDeleteMode).GetField("Touch",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var touch = (GameObject)field.GetValue(mode);
            Assert.IsNotNull(touch, "EditDeleteMode has no Touch object wired.");
            return touch;
        }
    }
}
