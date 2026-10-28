using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The Text screen (Edit → Text, mode <c>EditText</c>): typing a text, picking its colour and
    /// size, and stamping it onto the body as a part of the current group. Each colour row
    /// isolates one "&lt;Colour&gt; Tool Paint Dynamic Decal" under Tools; the typed text goes into
    /// "Dynamic Decal/Decal Canvas/Decal Text", which a camera renders into the decal's shape.
    /// </summary>
    [Category(Processes.MarkUpTheBody)]
    public class TextUiPlayModeTests : TwinPaintTestBase
    {
        private const string TextScreen = "Canvas/EditText UI";
        private const string ColourPanel = TextScreen + "/Bottom/Dyna/Scroll/Panel";
        private const string TextInput = TextScreen + "/Bottom/Dyna/InputField";
        private const string FontSizeDropdown = TextScreen + "/Bottom/Dyna/DropdownFontsize";
        private const string DecalText = "Dynamic Decal/Decal Canvas/Decal Text";

        private static readonly string[] Colours = { "Red", "Green", "Blue", "Cyan", "Pink", "Yellow", "White", "Black" };

        /// <summary>See RegionUiPlayModeTests: paint reaches the body texture late.</summary>
        private const float SettleSeconds = 0.5f;

        /// <summary>Edit → Text opens the Text screen with one row per text colour, and its Edit
        /// button leads back to Edit.</summary>
        [UnityTest]
        public IEnumerator TextButton_OpensTextScreenWithEveryColour()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            yield return OpenTextScreen();

            AssertGameObjectActive(TextInput);
            AssertGameObjectActive(FontSizeDropdown);
            foreach (string colour in Colours)
            {
                AssertGameObjectActive($"{ColourPanel}/{colour}");
            }

            yield return LeaveTextScreen();
        }

        /// <summary>The Text screen switches off the camera gestures (LeanTouch), like the other
        /// edit screens — otherwise placing a text would also turn the body.</summary>
        [UnityTest]
        public IEnumerator TextScreen_DisablesCameraGestures()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            // Edit mode already switches them off - switch them on again, so that only the
            // Text screen can be the one that turns them off
            GameObject touch = FindTouchOfTextMode();
            touch.SetActive(true);

            yield return OpenTextScreen();

            Assert.IsFalse(touch.activeSelf, "Camera gestures must be off on the Text screen.");
        }

        /// <summary>What is typed into the input field is what the decal camera renders, i.e.
        /// what gets stamped.</summary>
        [UnityTest]
        public IEnumerator TypedText_IsWhatTheDecalShows()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenTextScreen();

            yield return TypeText("Knee pain");

            Assert.AreEqual("Knee pain", FindGameObjectByPath(DecalText).GetComponent<Text>().text,
                "The decal should show the typed text.");
        }

        /// <summary>Tapping a colour row makes that colour's text tool the only active tool.</summary>
        [UnityTest]
        public IEnumerator ColourRow_SelectsOnlyThatTextTool()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenTextScreen();

            foreach (string colour in Colours)
            {
                yield return ClickColour(colour);

                GameObject[] active = ActiveTools();
                Assert.AreEqual(1, active.Length, $"After tapping {colour}, exactly one tool should be active.");
                Assert.AreEqual(TextTool(colour), active[0].name, $"Tapping {colour} should select its text tool.");
                Assert.AreEqual(PartManager.Tool.Text, PartManager.DeriveType(active[0]),
                    $"The tool behind the {colour} row should be a text tool.");
            }
        }

        /// <summary>Picking a size in the dropdown sets the size of every text tool, not only the
        /// selected one: option n (10, 20, … 100) gives a decal radius of n / 100.</summary>
        [UnityTest]
        public IEnumerator FontSize_SetsTheSizeOfEveryTextTool()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenTextScreen();
            var dropdown = FindGameObjectByPath(FontSizeDropdown).GetComponent<TMPro.TMP_Dropdown>();

            foreach (int option in new[] { 2, 7 })
            {
                dropdown.value = option;
                yield return null;

                float expected = int.Parse(dropdown.options[option].text) / 100f;
                foreach (string colour in Colours)
                {
                    Assert.AreEqual(expected, TextDecal(colour).Radius, 0.0001f,
                        $"Size {dropdown.options[option].text} should apply to the {colour} text tool.");
                }
            }
        }

        /// <summary>The size the dropdown shows when the screen opens is the size a text is
        /// stamped with.</summary>
        [UnityTest]
        public IEnumerator FontSize_DropdownShowsTheCurrentSize()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return OpenTextScreen();
            var dropdown = FindGameObjectByPath(FontSizeDropdown).GetComponent<TMPro.TMP_Dropdown>();

            string shown = dropdown.captionText.text;
            float stamped = TextDecal("Red").Radius;
            Assert.AreEqual(stamped, int.Parse(shown) / 100f, 0.0001f,
                $"The dropdown shows {shown}, but a text is stamped with radius {stamped}.");
        }

        /// <summary>Stamping a text adds one part to the current group, carrying the text, the
        /// colour and the tool it was stamped with, and the text shows on the body.</summary>
        [UnityTest]
        public IEnumerator StampText_AddsATextPartToTheCurrentGroup()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            int partsBefore = swell.groupParts.Count;

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return OpenTextScreen();
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] blank = BodyPixels();

            yield return TypeText("Knee pain");
            yield return ClickColour("Red");
            yield return StampText();
            yield return new WaitForSeconds(SettleSeconds);

            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Stamping a text should add one part to the current group.");
            Assert.Greater(DifferingPixels(blank, BodyPixels()), 0, "The stamped text should show on the body.");

            // the tool information is written onto a part when it is closed - leaving Edit for
            // the main screen does that
            yield return LeaveTextScreen();
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            var part = swell.groupParts.Last();
            Assert.AreEqual(PartManager.Tool.Text, part.typeTool, "The part should be a text part.");
            Assert.AreEqual("Knee pain", part.textTool, "The part should carry the stamped text.");
            Assert.AreEqual(TextTool("Red"), part.nameTool, "The part should carry the text tool it was stamped with.");
            Assert.AreEqual(TextDecal("Red").Color, part.colorTool, "The part should carry the colour of the text.");
            Assert.AreEqual(RowMeaning("Red"), part.meaning, "The part should carry the meaning of the colour row.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>A text part survives leaving and reloading the twin, with its text and colour.</summary>
        [UnityTest]
        public IEnumerator StampedText_SurvivesSaveAndReload()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            yield return SelectGroupForPainting(swell);
            int partsBefore = swell.groupParts.Count;

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return OpenTextScreen();
            yield return TypeText("Swollen");
            yield return ClickColour("Blue");
            yield return StampText();

            // without this, a failed stamp would make Last() pick a part that was already there
            Assert.AreEqual(partsBefore + 1, swell.groupParts.Count, "Setup: stamping should add one part to the current group.");
            string partId = swell.groupParts.Last().id;

            yield return LeaveTextScreen();
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");

            partManager = FindPartManager();
            var reloadedSwell = partManager.groups.First(g => g.name == "Swell");
            var reloadedPart = reloadedSwell.groupParts.FirstOrDefault(p => p.id == partId);

            Assert.IsNotNull(reloadedPart, "The text part did not survive save/reload.");
            Assert.AreEqual(PartManager.Tool.Text, reloadedPart.typeTool, "The tool type should survive save/reload.");
            Assert.AreEqual("Swollen", reloadedPart.textTool, "The text should survive save/reload.");
            Assert.AreEqual(TextDecal("Blue").Color, reloadedPart.colorTool, "The colour should survive save/reload.");
            Assert.AreSame(reloadedSwell, reloadedPart.group, "The part must still be linked to its group after reload.");
        }

        // /// <summary>Two texts stamped one after the other in the same colour are two findings:
        // /// each becomes a part of its own and keeps its own text.</summary>
        // [UnityTest]
        // public IEnumerator TwoTextsInTheSameColour_AreTwoPartsWithTheirOwnText()
        // {
        //     yield return LoadLipEdemaTwin();
        //     var partManager = FindPartManager();
        //     var swell = partManager.groups.First(g => g.name == "Swell");
        //     yield return SelectGroupForPainting(swell);
        //     int partsBefore = swell.groupParts.Count;

        //     yield return ClickButtonByName("Edit Button");
        //     yield return WaitForModeActive("Edit");
        //     yield return SelectView(BodyView);
        //     yield return OpenTextScreen();
        //     yield return ClickColour("Red");
        //     yield return TypeText("Knee");
        //     yield return StampText();
        //     yield return TypeText("Hip");
        //     yield return StampText();

        //     yield return LeaveTextScreen();
        //     yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
        //     yield return WaitForModeActive("Main");

        //     var texts = swell.groupParts.Skip(partsBefore).Select(p => p.textTool).ToArray();
        //     CollectionAssert.AreEqual(new[] { "Knee", "Hip" }, texts,
        //         "Each stamped text should be a part of its own, carrying the text it was stamped with.");
        // }


        /// <summary>A placed text keeps its own text when the body is redrawn from the recorded
        /// commands (screenshots, hiding a group, undo, reopening the twin) — also after another
        /// text has been typed and placed in another colour.</summary>
        [UnityTest]
        public IEnumerator PlacedText_KeepsItsTextWhenTheBodyIsRedrawn()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var swell = partManager.groups.First(g => g.name == "Swell");
            var pain = partManager.groups.First(g => g.name == "Pain");
            yield return SelectGroupForPainting(swell);

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return OpenTextScreen();
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] blank = BodyPixels();

            yield return ClickColour("Red");
            yield return TypeText("Knee");
            yield return StampText();
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] placed = BodyPixels();
            int kneePixels = DifferingPixels(blank, placed);
            Assert.Greater(kneePixels, 0, "Setup: the text 'Knee' should show on the body.");

            // a redraw while the input field still says "Knee" - checks that a redraw reproduces
            // the text at all, so the assertion below can only fail on the text itself
            yield return Redraw(swell);
            Color32[] redrawn = BodyPixels();
            Assert.Less(DifferingPixels(placed, redrawn), kneePixels / 20,
                "Setup: redrawing the body should reproduce the placed text.");

            // a second text in another colour, into another group, so that hiding that group
            // redraws the body with nothing but "Knee"
            yield return SelectGroupForPainting(pain);
            yield return ClickColour("Blue");
            yield return TypeText("Hip");
            yield return StampText();
            yield return new WaitForSeconds(SettleSeconds);
            yield return SetGroupVisible(pain, false);
            yield return new WaitForSeconds(SettleSeconds);

            Assert.Less(DifferingPixels(redrawn, BodyPixels()), kneePixels / 20,
                "After 'Hip' was placed, redrawing the body should still show 'Knee', not the latest text.");
        }


        /// <summary>From Edit mode into the Text screen.</summary>
        private IEnumerator OpenTextScreen()
        {
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Alphanumeric/Text Background/Text");
            yield return WaitForModeActive("EditText");
        }

        /// <summary>From the Text screen back to Edit mode.</summary>
        private IEnumerator LeaveTextScreen()
        {
            yield return ClickButtonByPath(TextScreen + "/Bottom/Buttons/Edit");
            yield return WaitForModeActive("Edit");
        }

        private IEnumerator TypeText(string text)
        {
            FindGameObjectByPath(TextInput).GetComponent<InputField>().text = text;
            yield return null;
        }

        private IEnumerator ClickColour(string colour)
        {
            yield return ClickButtonByPath($"{ColourPanel}/{colour}");
            AssertGameObjectActive($"Tools/{TextTool(colour)}");
        }

        /// <summary>Taps the centre of the screen with the selected text tool and waits until the
        /// paint is flushed.</summary>
        /// <remarks>Not <see cref="PlayModeTestBase.DragOnCanvas"/>: the text tools paint
        /// OnceOnRelease, and that helper never sends the release (up = true) to the hit-screen,
        /// so the text would only ever be previewed.</remarks>
        private IEnumerator StampText()
        {
            var hitScreen = FindGameObjectByPath("Tools").GetComponentsInChildren<CwHitScreen>(false).Single();
            var pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
            hitScreen.GuiLayers = 0; // the "started over GUI" guard would see the test's click as a UI click
            var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);

            pointerMouse.GetFinger(1, centre, 1.0f, true, out CW.Common.CwInputManager.Finger finger);
            hitScreen.HandleFingerUpdate(finger, true, false);
            yield return null;
            hitScreen.HandleFingerUpdate(finger, false, true);
            pointerMouse.TryNullFinger(1);
            yield return null;
            yield return null; // let CwPaintableManager flush the command
        }

        /// <summary>Redraws the body from the recorded commands by hiding and showing a group,
        /// the way the group overlay does.</summary>
        private IEnumerator Redraw(PartManager.GroupData group)
        {
            yield return SetGroupVisible(group, false);
            yield return SetGroupVisible(group, true);
            yield return new WaitForSeconds(SettleSeconds);
        }

        private static string TextTool(string colour)
        {
            return $"{colour} Tool Paint Dynamic Decal";
        }

        private CwPaintDecal TextDecal(string colour)
        {
            return FindGameObjectByPath($"Tools/{TextTool(colour)}").GetComponent<CwPaintDecal>();
        }

        /// <summary>The meaning the user gave a colour row (its input field).</summary>
        private string RowMeaning(string colour)
        {
            return FindGameObjectByPath($"{ColourPanel}/{colour}/InputField").GetComponent<InputField>().text;
        }

        private GameObject[] ActiveTools()
        {
            var tools = FindGameObjectByPath("Tools").transform;
            return Enumerable.Range(0, tools.childCount).Select(i => tools.GetChild(i).gameObject)
                .Where(t => t.activeSelf).ToArray();
        }

        /// <summary>The LeanTouch object the Text screen is wired to (a private serialized field).</summary>
        private static GameObject FindTouchOfTextMode()
        {
            var mode = Resources.FindObjectsOfTypeAll<EditTextMode>().First(m => m.gameObject.scene.IsValid());
            var field = typeof(EditTextMode).GetField("Touch",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var touch = (GameObject)field.GetValue(mode);
            Assert.IsNotNull(touch, "EditTextMode has no Touch object wired.");
            return touch;
        }
    }
}
