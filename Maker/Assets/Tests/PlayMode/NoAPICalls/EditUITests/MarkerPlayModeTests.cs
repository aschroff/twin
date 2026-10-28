using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CW.Common;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The Marker tool of the Edit screen: the 16 rows of its panel (8 solid, 8 dotted), the
    /// colour each of them leaves on the body, the name the user gives a marker, the part a
    /// stroke becomes, and that the paint survives a restart of the app.
    ///
    /// Colours are read from the body's paint texture, not from a screenshot - light and shading
    /// would change them on screen. The texture starts out white, so a white marker is only
    /// visible on top of another colour; the colour test therefore paints all markers onto the
    /// same spot, each one over the one before.
    /// </summary>
    [Category(Processes.MarkUpTheBody)]
    public class MarkerPlayModeTests : TwinPaintTestBase
    {
        private const string MarkerPanel = "Canvas/EditMarker UI/Bottom/Scroll/Panel";
        private const string ToolsRoot = "Tools";
        private const int MarkersPerKind = 8;

        /// <summary>Time for a replay of the body to reach the texture (see the painting guide).</summary>
        private const float SettleSeconds = 0.5f;

        /// <summary>Largest distance per colour channel between the average of the painted pixels
        /// and the marker's colour. The soft rim of a dot blends with what was there before; measured
        /// at most 0.28 (Black dotted over White dotted). Telling the colours apart is the job of
        /// the nearest-colour check, this only catches a colour that is off.</summary>
        private const float ColourTolerance = 0.35f;

        /// <summary>How much further away than the body view the dotted stroke is painted from.</summary>
        private const float DottedZoomOut = 2.5f;

        /// <summary>The largest share of the solid stroke's pixels the dotted one may cover; measured
        /// 36 % in a 1668x2388 game view. In the plain body view it is 78 %, too close to tell.</summary>
        private const float DottedShareOfSolid = 0.6f;

        // both short enough for TwinNameValidator, which allows 11 characters
        private const string TwinA = "MarkerA";
        private const string TwinB = "MarkerB";

        /// <summary>A blank twin offers 16 markers, 8 solid and 8 dotted, one of each per colour.
        /// Every row switches on its own tool and nothing else, and carries no name yet.</summary>
        [UnityTest]
        public IEnumerator MarkerPanel_OffersEightSolidAndEightDottedMarkers()
        {
            yield return ResetApp();
            yield return CreateTwin("Blank");
            yield return OpenMarkerPanel();

            List<MarkerRow> rows = MarkerRows();
            Assert.AreEqual(2 * MarkersPerKind, rows.Count, "The marker panel should offer 16 rows.");

            List<MarkerRow> solid = rows.Where(row => row.Kind == PartManager.Tool.MarkerLine).ToList();
            List<MarkerRow> dotted = rows.Where(row => row.Kind == PartManager.Tool.MarkerDotted).ToList();
            Assert.AreEqual(MarkersPerKind, solid.Count, "8 solid markers expected: " + Names(solid));
            Assert.AreEqual(MarkersPerKind, dotted.Count, "8 dotted markers expected: " + Names(dotted));

            Assert.AreEqual(MarkersPerKind, solid.Select(row => row.Colour).Distinct().Count(),
                "Every solid marker should have a colour of its own.");
            foreach (MarkerRow row in solid)
            {
                Assert.IsTrue(dotted.Any(other => other.Colour == row.Colour),
                    $"There is no dotted marker in the colour of '{row.Tool.name}'.");
            }

            var ids = new HashSet<string>();
            foreach (MarkerRow row in rows)
            {
                yield return ClickButtonByPath($"{MarkerPanel}/{row.Row.name}");
                AssertOnlyActiveTool(row.Tool);

                Assert.IsEmpty(row.Name.text, $"'{row.Tool.name}' should carry no name in a blank twin.");
                Assert.IsTrue(row.Item.persistent, $"The name of '{row.Tool.name}' would not be saved.");
                Assert.IsNotEmpty(row.Item.id, $"The name of '{row.Tool.name}' has no id to be saved under.");
                Assert.IsTrue(ids.Add(row.Item.id),
                    $"'{row.Tool.name}' shares its id with another row, so their names overwrite each other.");
            }
        }

        /// <summary>Every marker leaves its own colour on the body: the average of the pixels a
        /// dot changes is closest to the colour of the marker's paint component.</summary>
        [UnityTest]
        public IEnumerator EveryMarker_PaintsInItsOwnColour()
        {
            yield return PrepareToPaint();

            List<MarkerRow> rows = MarkerRows();
            Color[] palette = rows.Select(row => row.Colour).Distinct().ToArray();
            Color32[] before = BodyPixels();

            var painted = new List<(MarkerRow row, int changed, Color average)>();
            foreach (MarkerRow row in rows)
            {
                yield return ClickButtonByPath($"{MarkerPanel}/{row.Row.name}");
                yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());

                Color32[] after = BodyPixels();
                int changed = ChangedPixels(before, after, out Color average);
                painted.Add((row, changed, average));
                before = after;
            }

            // all markers first, then the verdict - a wrong colour shows next to all the others
            string report = string.Join("\n", painted.Select(p =>
                $"{p.row.Tool.name}: {p.changed} pixels, average {p.average}, marker {p.row.Colour}"));
            Debug.Log("[Test] Marker colours on the body:\n" + report);

            var wrong = new List<string>();
            foreach ((MarkerRow row, int changed, Color average) in painted)
            {
                if (changed == 0)
                {
                    wrong.Add($"'{row.Tool.name}' left nothing on the body.");
                }
                else if (Nearest(palette, average) != row.Colour)
                {
                    wrong.Add($"'{row.Tool.name}' looks like another marker's colour: average {average}.");
                }
                else if (Mathf.Abs(row.Colour.r - average.r) > ColourTolerance ||
                         Mathf.Abs(row.Colour.g - average.g) > ColourTolerance ||
                         Mathf.Abs(row.Colour.b - average.b) > ColourTolerance)
                {
                    wrong.Add($"'{row.Tool.name}' is too far from its colour {row.Colour}: average {average}.");
                }
            }
            Assert.IsEmpty(wrong, "Markers that do not paint in their colour:\n" + string.Join("\n", wrong) +
                                  "\n\nAll markers:\n" + report);
        }

        /// <summary>The name of a marker belongs to the twin it was given in: entering, changing
        /// and deleting it shows in that twin, survives switching away and back, and never shows
        /// in another twin.</summary>
        [UnityTest]
        public IEnumerator MarkerName_BelongsToTheTwinItWasGivenIn()
        {
            yield return ResetApp();
            yield return CreateTwin(TwinA);
            string twinA = DataPersistenceManager.instance.selectedProfileId;
            yield return CreateTwin(TwinB);
            string twinB = DataPersistenceManager.instance.selectedProfileId;
            string id = MarkerRows().First(row => row.Tool.name == "Red").Item.id;

            yield return OpenTwin(TwinA);
            yield return EnterMarkerName("Red", "Pain");
            yield return AssertMarkerName(TwinB, twinB, id, "", "after naming it in the other twin");
            yield return AssertMarkerName(TwinA, twinA, id, "Pain", "after entering it");
            Assert.IsTrue(Code.AI.PromptGeneration.ToolInventory.InUse().Any(tool => tool.name == "Red"),
                "A named marker counts as in use.");

            yield return EnterMarkerName("Red", "Burning pain");
            yield return AssertMarkerName(TwinB, twinB, id, "", "after changing it in the other twin");
            yield return AssertMarkerName(TwinA, twinA, id, "Burning pain", "after changing it");

            yield return EnterMarkerName("Red", "");
            yield return AssertMarkerName(TwinB, twinB, id, "", "after deleting it in the other twin");
            yield return AssertMarkerName(TwinA, twinA, id, "", "after deleting it");
            Assert.IsTrue(Code.AI.PromptGeneration.ToolInventory.Free().Any(tool => tool.name == "Red"),
                "A marker without a name is free again.");
        }

        /// <summary>A stroke becomes a part of the current group that knows its marker: kind,
        /// name, colour and meaning, plus the view it was painted from. A second stroke with the
        /// same marker continues the part, another marker starts a new one.</summary>
        [UnityTest]
        public IEnumerator Stroke_BecomesAPartThatKnowsItsMarker()
        {
            yield return PrepareToPaint();
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.currentGroup;

            string[] markers = { "Red", "Blue dotted", "Green" };
            foreach (string marker in markers)
            {
                yield return SelectMarker(marker);
                yield return PaintInOneFrame(Stroke(0));
            }
            yield return SelectMarker("Green");
            yield return PaintInOneFrame(Stroke(-60));

            // saving is what completes the part being painted - the app saves every two minutes,
            // when it is sent to the background and when it quits
            DataPersistenceManager.instance.SaveConfig();

            Assert.AreEqual(markers.Length, group.groupParts.Count,
                "One part per marker: a marker change starts a part, the same marker continues it.");
            for (int i = 0; i < markers.Length; i++)
            {
                PartManager.PartData part = group.groupParts[i];
                MarkerRow row = MarkerRows().First(r => r.Tool.name == markers[i]);

                Assert.AreSame(group, part.group, $"The part of '{markers[i]}' is not linked to its group.");
                Assert.AreEqual(row.Kind, part.typeTool, $"Kind of the part of '{markers[i]}'.");
                Assert.AreEqual(markers[i], part.nameTool, "The part should know its marker.");
                Assert.AreEqual(row.Colour, part.colorTool, $"Colour of the part of '{markers[i]}'.");
                Assert.AreEqual(row.Name.text, part.meaning,
                    $"The part of '{markers[i]}' should carry the marker's name as its meaning.");
                Assert.IsNotNull(part.view, $"The part of '{markers[i]}' did not store its view.");
                Assert.Greater(part.partCommands.Count, 0, $"The part of '{markers[i]}' has no commands.");
            }
            Assert.AreEqual(markers.Length, group.groupParts.Select(part => part.id).Distinct().Count(),
                "Every part needs an id of its own.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>The dotted marker leaves gaps: the same stroke covers clearly fewer pixels
        /// than with the solid marker of the same colour.</summary>
        /// <remarks>The dots are a fixed number of screen pixels apart, but their size is fixed on
        /// the body. Close up they grow into each other, so the stroke is painted from further away,
        /// where the gaps are wide.</remarks>
        [UnityTest]
        public IEnumerator DottedMarker_LeavesGapsWhereTheSolidOneDoesNot()
        {
            yield return PrepareToPaint();
            PartManager partManager = FindPartManager();
            yield return ZoomOut(DottedZoomOut);
            Color32[] blank = BodyPixels();

            yield return SelectMarker("Red");
            yield return PaintInOneFrame(Stroke(0));
            int solid = ChangedPixels(blank, BodyPixels(), out _);
            int solidCommands = partManager.currentPart.partCommands.Count;

            Assert.IsTrue(partManager.Undo(), "Setup: the solid stroke should be undoable.");
            yield return new WaitForSeconds(SettleSeconds);
            Assert.AreEqual(0, ChangedPixels(blank, BodyPixels(), out _), "Setup: undo should leave the body blank.");

            yield return SelectMarker("Red dotted");
            yield return PaintInOneFrame(Stroke(0));
            int dotted = ChangedPixels(blank, BodyPixels(), out _);
            int dottedCommands = partManager.currentPart.partCommands.Count;
            Debug.Log($"[Test] screen {Screen.width}x{Screen.height}; solid: {solid} pixels, {solidCommands} commands; " +
                      $"dotted: {dotted} pixels, {dottedCommands} commands");

            Assert.Greater(dotted, 0, "The dotted marker left nothing on the body.");
            Assert.Less(dotted, solid * DottedShareOfSolid,
                $"The dotted stroke ({dotted} pixels) is hardly sparser than the solid one ({solid}).");
        }

        /// <summary>A painted marker comes back after the app is restarted: the part with its
        /// marker, and its paint in the marker's colour on the body.</summary>
        [UnityTest]
        public IEnumerator PaintedMarker_SurvivesARestartOfTheApp()
        {
            yield return PrepareToPaint();
            string twin = DataPersistenceManager.instance.selectedProfileId;
            string groupId = FindPartManager().currentGroup.id;
            Color32[] blank = BodyPixels();

            yield return SelectMarker("Blue");
            yield return PaintInOneFrame(Stroke(0));
            int painted = ChangedPixels(blank, BodyPixels(), out _);
            Assert.Greater(painted, 0, "Setup: the stroke should show on the body.");

            yield return RestartApp();
            Assert.AreEqual(twin, DataPersistenceManager.instance.selectedProfileId,
                "The app should come back with the twin it was closed with.");
            yield return new WaitForSeconds(SettleSeconds);

            PartManager.GroupData group = FindPartManager().groups.FirstOrDefault(g => g.id == groupId);
            Assert.IsNotNull(group, "The group painted into did not come back.");
            Assert.AreEqual(1, group.groupParts.Count, "The painted part did not come back.");
            Assert.AreEqual(PartManager.Tool.MarkerLine, group.groupParts[0].typeTool);
            Assert.AreEqual("Blue", group.groupParts[0].nameTool);
            AssertPartsAreUsable(FindPartManager());

            int replayed = ChangedPixels(blank, BodyPixels(), out Color average);
            Debug.Log($"[Test] painted {painted} pixels, replayed {replayed}, average {average}");
            Assert.GreaterOrEqual(replayed, painted , $"Only {replayed} of {painted} painted pixels came back.");
            Assert.AreEqual(0f, average.r, ColourTolerance, "Red channel of the replayed paint.");
            Assert.AreEqual(0f, average.g, ColourTolerance, "Green channel of the replayed paint.");
            Assert.AreEqual(1f, average.b, ColourTolerance, "Blue channel of the replayed paint.");
        }

        // ------------------------------------------------------------------------------------
        // App flows
        // ------------------------------------------------------------------------------------

        /// <summary>The LipEdema twin framed for painting, its first group the current one, the
        /// marker panel open.</summary>
        private IEnumerator PrepareToPaint()
        {
            yield return LoadLipEdemaTwin();
            yield return BackToMain();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            yield return WaitForModeActive("EditMarker");
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>Opens a twin through the twin list and comes back to the main screen.</summary>
        private IEnumerator OpenTwin(string twinName)
        {
            yield return SelectTwin(twinName);
            yield return BackToMain();
        }

        /// <summary>Selecting a twin leaves the app on the twin list.</summary>
        private IEnumerator BackToMain()
        {
            yield return ClickButtonByPath("Canvas/Save UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Creates a twin through the save screen and leaves the app on it.</summary>
        private IEnumerator CreateTwin(string twinName)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", twinName);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");
        }

        /// <summary>From the main screen to the marker panel of the Edit screen.</summary>
        private IEnumerator OpenMarkerPanel()
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            yield return WaitForModeActive("EditMarker");
        }

        /// <summary>From the marker panel back to the main screen.</summary>
        private IEnumerator CloseMarkerPanel()
        {
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Picks a marker by the name of its tool, the way its row in the panel does.</summary>
        private IEnumerator SelectMarker(string toolName)
        {
            MarkerRow row = MarkerRows().FirstOrDefault(r => r.Tool.name == toolName);
            Assert.IsNotNull(row, $"No marker '{toolName}' in the panel.");
            yield return ClickButtonByPath($"{MarkerPanel}/{row.Row.name}");
            AssertOnlyActiveTool(row.Tool);
        }

        /// <summary>Types a name into the row of a marker in the twin that is open, from the main
        /// screen and back to it.</summary>
        private IEnumerator EnterMarkerName(string toolName, string markerName)
        {
            yield return OpenMarkerPanel();
            InputField field = FindToolMeaningField(toolName);
            field.text = markerName;
            field.onEndEdit.Invoke(markerName);
            yield return CloseMarkerPanel();
        }

        /// <summary>Opens a twin through the twin list and checks the name a marker shows there and
        /// the name stored in the twin's file. Leaves the app on the main screen.</summary>
        private IEnumerator AssertMarkerName(string twinName, string profileId, string itemId, string expected, string when)
        {
            yield return OpenTwin(twinName);

            Assert.AreEqual(expected, FindToolMeaningField("Red").text,
                $"The name shown for the marker in twin {twinName} {when}.");

            ConfigData saved = DataPersistenceManager.instance.GetAllProfilesGameData()[profileId];
            saved.itemTexts.TryGetValue(itemId, out string stored);
            Assert.AreEqual(expected, stored ?? "", $"The name stored for the marker in twin {twinName} {when}.");
        }

        /// <summary>Moves the camera further away from the body by the given factor, keeping what
        /// is in the centre of the screen.</summary>
        private static IEnumerator ZoomOut(float factor)
        {
            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            var view = viewManager.shootView();
            view.sizeCamera *= factor;
            viewManager.select(view);
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>Saves, closes and starts the app again on the same data, like a user quitting
        /// and reopening it.</summary>
        private IEnumerator RestartApp()
        {
            DataPersistenceManager.instance.SaveConfig();
            yield return SceneManager.LoadSceneAsync("EmptyScene", LoadSceneMode.Single);
            CwSerialization.HashToModel.Clear();
            CwSerialization.ModelToHash.Clear();
            yield return null;

            yield return LoadAppScene();
            yield return WaitForModeActive("Main");
            yield return WaitWhileLoading();
        }

        // ------------------------------------------------------------------------------------
        // Marker rows
        // ------------------------------------------------------------------------------------

        /// <summary>One row of the marker panel and the tool it switches on.</summary>
        private class MarkerRow
        {
            public Transform Row;
            public GameObject Tool;
            public PartManager.Tool Kind;
            public Color Colour;
            public InputField Name;
            public Item Item;
        }

        /// <summary>The rows of the marker panel, in panel order.</summary>
        private List<MarkerRow> MarkerRows()
        {
            var rows = new List<MarkerRow>();
            foreach (Transform row in FindGameObjectByPath(MarkerPanel).transform)
            {
                CwDemoButton button = row.GetComponent<CwDemoButton>();
                if (button == null || button.IsolateTarget == null) continue;

                GameObject tool = button.IsolateTarget.gameObject;
                CwPaintSphere sphere = tool.GetComponent<CwPaintSphere>();
                Assert.IsNotNull(sphere, $"The tool '{tool.name}' of the marker row '{row.name}' paints no colour.");
                InputField name = row.GetComponentInChildren<InputField>(true);
                Assert.IsNotNull(name, $"The marker row '{row.name}' has no field for its name.");
                Item item = row.GetComponentInChildren<Item>(true);
                Assert.IsNotNull(item, $"The marker row '{row.name}' has no Item, so its name is never saved.");

                rows.Add(new MarkerRow
                {
                    Row = row,
                    Tool = tool,
                    Kind = PartManager.DeriveType(tool),
                    Colour = sphere.Color,
                    Name = name,
                    Item = item
                });
            }
            return rows;
        }

        private void AssertOnlyActiveTool(GameObject tool)
        {
            Assert.IsTrue(tool.activeInHierarchy, $"The row of '{tool.name}' did not switch its tool on.");
            string[] active = FindGameObjectByPath(ToolsRoot).transform.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf)
                .Select(child => child.name)
                .ToArray();
            CollectionAssert.AreEqual(new[] { tool.name }, active,
                $"Only '{tool.name}' should be switched on.");
        }

        private static string Names(IEnumerable<MarkerRow> rows)
        {
            return string.Join(", ", rows.Select(row => row.Tool.name));
        }

        // ------------------------------------------------------------------------------------
        // Painting and reading the paint
        // ------------------------------------------------------------------------------------

        private static Vector2 ScreenCentre()
        {
            return new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        /// <summary>A horizontal stroke through the screen centre, shifted up or down by
        /// <paramref name="offsetY"/> pixels.</summary>
        private static Vector2[] Stroke(float offsetY)
        {
            const int points = 25;
            const float halfLength = 120f;
            Vector2 centre = ScreenCentre() + new Vector2(0f, offsetY);
            var stroke = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                stroke[i] = centre + new Vector2(Mathf.Lerp(-halfLength, halfLength, i / (points - 1f)), 0f);
            }
            return stroke;
        }

        /// <summary>Drives the paint pipeline of the active tool through the given screen
        /// positions - down on the first, up on the last. The whole stroke has to happen within one
        /// frame: between frames the real mouse, which is not pressed, ends the synthetic finger
        /// (see the painting guide).</summary>
        private static IEnumerator PaintInOneFrame(params Vector2[] screenPoints)
        {
            CwHitScreen[] hitScreens = Object.FindObjectsOfType<CwHitScreen>(false);
            Assert.Greater(hitScreens.Length, 0, "No active CwHitScreen - is a marker selected?");

            foreach (CwHitScreen hitScreen in hitScreens)
            {
                CwPointerMouse pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
                if (pointerMouse == null) continue;
                hitScreen.GuiLayers = 0; // bypass the "started over GUI" guard

                for (int i = 0; i < screenPoints.Length; i++)
                {
                    pointerMouse.GetFinger(1, screenPoints[i], 1.0f, true, out CwInputManager.Finger finger);
                    hitScreen.HandleFingerUpdate(finger, i == 0, i == screenPoints.Length - 1);
                }
                pointerMouse.TryNullFinger(1);
            }

            yield return null;
            yield return null; // CwPaintableManager flushes the commands in LateUpdate
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

        /// <summary>How many pixels differ between two reads of the body texture, and the average
        /// colour of those pixels in the second read.</summary>
        private static int ChangedPixels(Color32[] before, Color32[] after, out Color average)
        {
            Assert.AreEqual(before.Length, after.Length, "The body texture changed its size.");
            long r = 0, g = 0, b = 0;
            int changed = 0;
            for (int i = 0; i < before.Length; i++)
            {
                Color32 x = before[i];
                Color32 y = after[i];
                if (x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a) continue;
                changed++;
                r += y.r;
                g += y.g;
                b += y.b;
            }
            average = changed == 0
                ? Color.clear
                : new Color(r / (255f * changed), g / (255f * changed), b / (255f * changed));
            return changed;
        }

        private static Color Nearest(Color[] palette, Color colour)
        {
            return palette.OrderBy(candidate =>
                Mathf.Pow(candidate.r - colour.r, 2) +
                Mathf.Pow(candidate.g - colour.g, 2) +
                Mathf.Pow(candidate.b - colour.b, 2)).First();
        }
    }
}
