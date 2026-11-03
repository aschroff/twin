using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// The sticker images of a twin. Each sticker slot keeps its image as a PNG in the twin
    /// directory, and its paint commands refer to that image by the hash of the slot. The hash
    /// belongs to the slot, so all twins share it and every twin has an image of its own behind
    /// it - which is what makes loading a twin, and importing one, interesting.
    /// </summary>
    [Category(Processes.MarkUpTheBody)]
    public class StickerPlayModeTests : TwinPaintTestBase
    {
        // both short enough for TwinNameValidator, which allows 11 characters
        const string RedStickerTwin = "RedSticker";
        const string BlueStickerTwin = "BlueSticker";
        const string StickerPanel = "Canvas/EditSticker UI/Bottom/Scroll/Panel";
        const string StickerButtons = "Canvas/EditSticker UI/Bottom/Buttons/Sticker";
        const string TurnLeft = "LR/Left Button";
        const string TurnRight = "LR/Right Button";
        const string FlipHorizontal = "HV/Horizontal Button";
        const string FlipVertical = "HV/Vertical Button";
        const string Expand = "Size/Expand Button";
        const string Shrink = "Size/Shrink Button";
        const int SlotCount = 10;

        /// <summary>Time for a replay of the body to reach the texture (see the painting guide).</summary>
        const float SettleSeconds = 0.5f;

        /// <summary>Shrink is 0.6667, not exactly the inverse of 1.5, so a size never comes back
        /// exactly.</summary>
        const float ScaleTolerance = 0.001f;
        const float AngleTolerance = 0.01f;

        /// <summary>The size every sticker tool starts with.</summary>
        static readonly Vector3 DefaultScale = new Vector3(0.2f, 0.2f, 0.2f);

        /// <summary>One colour per slot, far enough apart to be told apart on the body. Not white:
        /// the body texture starts out white.</summary>
        static readonly Color[] SlotColours =
        {
            Color.red, Color.green, Color.blue, Color.cyan, Color.magenta, Color.yellow, Color.black,
            new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 0.5f), new Color(0.5f, 0.5f, 0.5f)
        };

        /// <summary>The quarters of the test image that shows orientation.</summary>
        static readonly Color TopLeft = Color.red;
        static readonly Color TopRight = Color.blue;
        static readonly Color BottomLeft = Color.green;
        static readonly Color BottomRight = Color.yellow;

        #region Test functions

        /// <summary>Two twins with a different image in the same sticker slot: the image behind
        /// the slot and the image its hash points at have to follow the twin that is open.
        /// </summary>
        [UnityTest]
        public IEnumerator StickerImage_FollowsTheTwinThatIsOpen()
        {
            yield return ResetApp();
            Sticker slot = FindStickerSlot();
            string slotId = SlotId(slot);
            int slotHash = SlotHash(slot);

            yield return CreateTwin(RedStickerTwin);
            string redTwin = DataPersistenceManager.instance.selectedProfileId;
            yield return CreateTwin(BlueStickerTwin);
            string blueTwin = DataPersistenceManager.instance.selectedProfileId;

            WriteStickerImage(redTwin, slotId, Color.red);
            WriteStickerImage(blueTwin, slotId, Color.blue);

            yield return OpenTwin(redTwin);
            AssertStickerImage(slot, Color.red, "after opening the twin with the red sticker");
            AssertRegisteredImage(slot, slotHash);

            yield return OpenTwin(blueTwin);
            AssertStickerImage(slot, Color.blue, "after opening the twin with the blue sticker");
            AssertRegisteredImage(slot, slotHash);

            yield return OpenTwin(redTwin);
            AssertStickerImage(slot, Color.red, "after going back to the twin with the red sticker");
            AssertRegisteredImage(slot, slotHash);
        }

        /// <summary>Every sticker image of a twin travels with it, and an imported twin brings its
        /// own images for the slots the open twin is using with different ones.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_BringsAllItsStickerImages()
        {
            yield return ResetApp();
            Sticker[] slots = FindStickerSlots(2);

            yield return CreateTwin(BlueStickerTwin);
            string exportedTwin = DataPersistenceManager.instance.selectedProfileId;
            WriteStickerImage(exportedTwin, SlotId(slots[0]), Color.blue);
            WriteStickerImage(exportedTwin, SlotId(slots[1]), Color.green);
            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.IsTrue(File.Exists(zipFilePath), "Setup: the export should have written a zip file.");

            // a twin using the same two slots with other images is open when the import arrives
            yield return CreateTwin(RedStickerTwin);
            string openTwin = DataPersistenceManager.instance.selectedProfileId;
            WriteStickerImage(openTwin, SlotId(slots[0]), Color.red);
            WriteStickerImage(openTwin, SlotId(slots[1]), Color.yellow);
            yield return OpenTwin(openTwin);
            AssertStickerImage(slots[0], Color.red, "Setup: the open twin should show its own stickers");
            AssertStickerImage(slots[1], Color.yellow, "Setup: the open twin should show its own stickers");

            string importedTwin = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.AreEqual(exportedTwin + "V01", importedTwin, "The import did not produce a new version.");
            foreach (Sticker slot in slots)
            {
                Assert.IsTrue(File.Exists(Path.Combine(DataPaths.PersistentDataPath, importedTwin, SlotId(slot) + ".png")),
                    $"The image of sticker slot {SlotId(slot)} should travel with the twin.");
            }

            yield return OpenTwin(importedTwin);
            AssertStickerImage(slots[0], Color.blue, "after opening the imported twin");
            AssertStickerImage(slots[1], Color.green, "after opening the imported twin");
            AssertRegisteredImage(slots[0], SlotHash(slots[0]));
            AssertRegisteredImage(slots[1], SlotHash(slots[1]));
        }

        /// <summary>A blank twin offers 10 sticker slots, all showing the default image. Every
        /// slot switches on a tool of its own and nothing else, starts unturned and at its default
        /// size, carries no name yet, and has an id and a hash of its own - two slots sharing a
        /// hash would show each other's image after a load.</summary>
        [UnityTest]
        public IEnumerator StickerPanel_OffersTenDefaultStickers()
        {
            yield return ResetApp();
            yield return CreateTwin("Blank");
            yield return OpenStickerPanel();

            List<StickerRow> rows = StickerRows();
            Assert.AreEqual(SlotCount, rows.Count, "The sticker panel should offer 10 slots.");
            Texture defaultImage = FindGameObjectByPath(StickerPanel).GetComponent<StickerRepo>().defaultTexture;
            Assert.IsNotNull(defaultImage, "Setup: the sticker panel should know its default image.");

            var tools = new HashSet<GameObject>();
            var ids = new HashSet<string>();
            var hashes = new HashSet<int>();
            foreach (StickerRow row in rows)
            {
                Assert.AreEqual(PartManager.Tool.Sticker, PartManager.DeriveType(row.Tool),
                    $"The tool '{row.Tool.name}' of '{row.Row.name}' is not a sticker.");
                Assert.IsTrue(tools.Add(row.Tool), $"'{row.Row.name}' shares its tool with another slot.");
                Assert.AreSame(defaultImage, row.Decal.Texture, $"'{row.Tool.name}' should paint the default image.");
                Assert.AreEqual(0f, row.Decal.Angle, $"'{row.Tool.name}' should start unturned.");
                Assert.AreEqual(DefaultScale, row.Decal.Scale, $"'{row.Tool.name}' should start at its default size.");

                yield return ClickButtonByPath($"{StickerPanel}/{row.Row.name}");
                AssertOnlyActiveTool(row.Tool);

                Assert.IsEmpty(row.Name.text, $"'{row.Tool.name}' should carry no name in a blank twin.");
                Assert.IsTrue(row.Item.persistent, $"The name of '{row.Tool.name}' would not be saved.");
                Assert.IsNotEmpty(row.Item.id, $"'{row.Tool.name}' has no id to save its name and image under.");
                Assert.IsTrue(ids.Add(row.Item.id), $"'{row.Tool.name}' shares its id with another slot.");
                Assert.IsTrue(hashes.Add(row.Item.getHash()),
                    $"'{row.Tool.name}' shares its hash {row.Item.getHash()} with another slot, so one of them " +
                    "would paint the other's image.");
            }
        }

        /// <summary>Every slot paints its own image: each slot gets a one-coloured image of its
        /// own, and the average of the pixels its sticker changes is closest to that colour.</summary>
        [UnityTest]
        public IEnumerator EverySticker_PaintsItsOwnImage()
        {
            yield return LoadLipEdemaTwin();
            string twin = DataPersistenceManager.instance.selectedProfileId;
            List<StickerRow> rows = StickerRows();
            for (int i = 0; i < rows.Count; i++)
            {
                WriteStickerImage(twin, rows[i].Item.getId(), SlotColours[i]);
            }
            yield return OpenTwin(twin);
            yield return PrepareToPaint();

            Color32[] before = BodyPixels();
            var painted = new List<(StickerRow row, Color expected, int changed, Color average)>();
            for (int i = 0; i < rows.Count; i++)
            {
                yield return SelectSticker(rows[i]);
                yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());
                Color32[] after = BodyPixels();
                int changed = ChangedPixels(before, after, out Color average);
                painted.Add((rows[i], SlotColours[i], changed, average));
                before = after;
            }

            // all slots first, then the verdict - a wrong image shows next to all the others
            string report = string.Join("\n", painted.Select(p =>
                $"{p.row.Tool.name}: {p.changed} pixels, average {p.average}, image {p.expected}"));
            Debug.Log("[Test] Sticker images on the body:\n" + report);

            var wrong = new List<string>();
            foreach ((StickerRow row, Color expected, int changed, Color average) in painted)
            {
                if (changed == 0)
                {
                    wrong.Add($"'{row.Tool.name}' left nothing on the body.");
                }
                else if (Nearest(SlotColours, average) != expected)
                {
                    wrong.Add($"'{row.Tool.name}' looks like another slot's image: average {average}, " +
                              $"its own is {expected}.");
                }
            }
            Assert.IsEmpty(wrong, "Stickers that do not paint their own image:\n" + string.Join("\n", wrong) +
                                  "\n\nAll stickers:\n" + report);
        }

        /// <summary>The name of a sticker belongs to the twin it was given in: entering, changing
        /// and deleting it shows in that twin, survives switching away and back, and never shows
        /// in another twin.</summary>
        [UnityTest]
        public IEnumerator StickerName_BelongsToTheTwinItWasGivenIn()
        {
            yield return ResetApp();
            yield return CreateTwin(RedStickerTwin);
            string twinA = DataPersistenceManager.instance.selectedProfileId;
            yield return CreateTwin(BlueStickerTwin);
            string twinB = DataPersistenceManager.instance.selectedProfileId;
            StickerRow first = StickerRows()[0];

            yield return OpenTwin(twinA);
            yield return EnterStickerName(first, "Bruise");
            yield return AssertStickerName(twinB, first, "", "after naming it in the other twin");
            yield return AssertStickerName(twinA, first, "Bruise", "after entering it");

            yield return EnterStickerName(first, "Old bruise");
            yield return AssertStickerName(twinB, first, "", "after changing it in the other twin");
            yield return AssertStickerName(twinA, first, "Old bruise", "after changing it");

            yield return EnterStickerName(first, "");
            yield return AssertStickerName(twinB, first, "", "after deleting it in the other twin");
            yield return AssertStickerName(twinA, first, "", "after deleting it");
        }

        /// <summary>The six buttons turn, flip and resize exactly the sticker that is selected, by
        /// the steps they promise, and every step can be taken back. The other slots keep their
        /// settings.</summary>
        [UnityTest]
        public IEnumerator EditButtons_TurnFlipAndResizeTheSelectedStickerOnly()
        {
            yield return ResetApp();
            yield return CreateTwin("Edit");
            yield return OpenStickerPanel();
            List<StickerRow> rows = StickerRows();
            CwPaintDecal first = rows[0].Decal;
            yield return SelectSticker(rows[0]);

            yield return ClickEditButton(TurnLeft);
            Assert.AreEqual(45f, first.Angle, AngleTolerance, "Left turns the sticker by 45 degrees.");
            yield return ClickEditButton(TurnRight);
            yield return ClickEditButton(TurnRight);
            Assert.AreEqual(-45f, first.Angle, AngleTolerance, "Right turns it further back by 45 degrees.");
            yield return ClickEditButton(TurnLeft);
            for (int i = 0; i < 8; i++)
            {
                yield return ClickEditButton(TurnLeft);
            }
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, first.Angle), AngleTolerance, "Eight turns are a full circle.");

            yield return ClickEditButton(FlipHorizontal);
            AssertScale(new Vector3(-0.2f, 0.2f, 0.2f), first, "Horizontal mirrors the sticker left to right.");
            yield return ClickEditButton(FlipHorizontal);
            AssertScale(DefaultScale, first, "A second horizontal flip takes the first back.");
            yield return ClickEditButton(FlipVertical);
            AssertScale(new Vector3(0.2f, -0.2f, 0.2f), first, "Vertical mirrors the sticker top to bottom.");
            yield return ClickEditButton(FlipVertical);
            AssertScale(DefaultScale, first, "A second vertical flip takes the first back.");

            yield return ClickEditButton(Expand);
            AssertScale(DefaultScale * 1.5f, first, "Expand makes the sticker one and a half times as large.");
            yield return ClickEditButton(Shrink);
            AssertScale(DefaultScale, first, "Shrink takes an expand back.");

            // leave the first slot turned, flipped and enlarged, then look at all the others
            yield return ClickEditButton(TurnLeft);
            yield return ClickEditButton(FlipHorizontal);
            yield return ClickEditButton(Expand);
            yield return SelectSticker(rows[1]);
            yield return ClickEditButton(TurnRight);
            Assert.AreEqual(45f, first.Angle, AngleTolerance, "Turning the second slot must not turn the first.");
            AssertScale(new Vector3(-0.3f, 0.3f, 0.3f), first, "Selecting another slot must not change the first.");
            Assert.AreEqual(-45f, rows[1].Decal.Angle, AngleTolerance, "The second slot turns on its own.");
            AssertScale(DefaultScale, rows[1].Decal, "The second slot keeps its size.");
            foreach (StickerRow other in rows.Skip(2))
            {
                Assert.AreEqual(0f, other.Decal.Angle, $"'{other.Tool.name}' was turned with another slot.");
                AssertScale(DefaultScale, other.Decal, $"'{other.Tool.name}' was resized with another slot.");
            }
        }

        /// <summary>What the buttons do shows on the body. The test image has four coloured
        /// quarters, so the position of the quarters to each other gives the orientation: an
        /// expanded sticker covers about 2.25 times the area, a shrunk one about 0.44 times; a
        /// horizontal flip swaps left and right, a vertical one top and bottom; two turns to the left
        /// or right turn the quarters by 90 degrees, in opposite directions.</summary>
        [UnityTest]
        public IEnumerator EditButtons_ShowOnTheBody()
        {
            yield return LoadLipEdemaTwin();
            string twin = DataPersistenceManager.instance.selectedProfileId;
            StickerRow row = StickerRows()[0];
            WriteQuarteredImage(twin, row.Item.getId());
            yield return OpenTwin(twin);
            yield return PrepareToPaint();
            yield return SelectSticker(row);
            PartManager partManager = FindPartManager();

            Placement plain = null;
            yield return PaintAndMeasure(partManager, placement => plain = placement);
            Debug.Log($"[Test] plain sticker: {plain}");
            Assert.Greater(plain.Area, 0, "Setup: the sticker should show on the body.");

            Placement expanded = null;
            yield return ClickEditButton(Expand);
            yield return PaintAndMeasure(partManager, placement => expanded = placement);
            yield return ClickEditButton(Shrink);
            Placement shrunk = null;
            yield return ClickEditButton(Shrink);
            yield return PaintAndMeasure(partManager, placement => shrunk = placement);
            yield return ClickEditButton(Expand);

            Placement flippedHorizontally = null;
            yield return ClickEditButton(FlipHorizontal);
            yield return PaintAndMeasure(partManager, placement => flippedHorizontally = placement);
            yield return ClickEditButton(FlipHorizontal);
            Placement flippedVertically = null;
            yield return ClickEditButton(FlipVertical);
            yield return PaintAndMeasure(partManager, placement => flippedVertically = placement);
            yield return ClickEditButton(FlipVertical);

            Placement turnedLeft = null;
            yield return ClickEditButton(TurnLeft);
            yield return ClickEditButton(TurnLeft);
            yield return PaintAndMeasure(partManager, placement => turnedLeft = placement);
            yield return ClickEditButton(TurnRight);
            yield return ClickEditButton(TurnRight);
            Placement turnedRight = null;
            yield return ClickEditButton(TurnRight);
            yield return ClickEditButton(TurnRight);
            yield return PaintAndMeasure(partManager, placement => turnedRight = placement);

            Debug.Log($"[Test] expanded: {expanded}\nshrunk: {shrunk}\nflipped horizontally: {flippedHorizontally}\n" +
                      $"flipped vertically: {flippedVertically}\nturned left: {turnedLeft}\nturned right: {turnedRight}");

            float grown = (float)expanded.Area / plain.Area;
            float lost = (float)shrunk.Area / plain.Area;
            Assert.That(grown, Is.InRange(1.8f, 2.7f), $"Expanded, the sticker should cover about 2.25 times the area, not {grown:F2}.");
            Assert.That(lost, Is.InRange(0.3f, 0.6f), $"Shrunk, the sticker should cover about 0.44 times the area, not {lost:F2}.");

            Assert.Less(Cosine(plain.LeftToRight, flippedHorizontally.LeftToRight), -0.9f,
                "A horizontal flip should swap the left and the right half.");
            Assert.Greater(Cosine(plain.TopToBottom, flippedHorizontally.TopToBottom), 0.9f,
                "A horizontal flip should leave top and bottom where they are.");
            Assert.Less(Cosine(plain.TopToBottom, flippedVertically.TopToBottom), -0.9f,
                "A vertical flip should swap the top and the bottom half.");
            Assert.Greater(Cosine(plain.LeftToRight, flippedVertically.LeftToRight), 0.9f,
                "A vertical flip should leave left and right where they are.");

            float left = Vector2.SignedAngle(plain.LeftToRight, turnedLeft.LeftToRight);
            float right = Vector2.SignedAngle(plain.LeftToRight, turnedRight.LeftToRight);
            Assert.AreEqual(90f, Mathf.Abs(left), 20f, $"Two turns to the left should turn the sticker by 90 degrees, not {left:F0}.");
            Assert.AreEqual(90f, Mathf.Abs(right), 20f, $"Two turns to the right should turn the sticker by 90 degrees, not {right:F0}.");
            Assert.AreNotEqual(Mathf.Sign(left), Mathf.Sign(right), "Left and right should turn in opposite directions.");
        }

        /// <summary>A sticker becomes a part of the current group that knows its slot: kind, tool,
        /// the slot's id (which finds its image again) and the slot's name as meaning, plus the view
        /// it was placed from. A second sticker from the same slot continues the part, another
        /// slot starts a new one.</summary>
        [UnityTest]
        public IEnumerator Sticker_BecomesAPartThatKnowsItsSlot()
        {
            yield return LoadLipEdemaTwin();
            yield return PrepareToPaint();
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.currentGroup;
            List<StickerRow> rows = StickerRows();
            rows[0].Name.text = "Bruise";
            rows[1].Name.text = "Scar";

            yield return SelectSticker(rows[0]);
            yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());
            yield return SelectSticker(rows[1]);
            yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());
            yield return PaintInOneFrame(ScreenCentre() + new Vector2(0f, -80f), ScreenCentre() + new Vector2(0f, -80f));

            // saving is what completes the part being painted - the app saves every two minutes,
            // when it is sent to the background and when it quits
            DataPersistenceManager.instance.SaveConfig();

            Assert.AreEqual(2, group.groupParts.Count,
                "One part per slot: another slot starts a part, the same slot continues it.");
            for (int i = 0; i < 2; i++)
            {
                PartManager.PartData part = group.groupParts[i];
                StickerRow row = rows[i];
                Assert.AreSame(group, part.group, $"The part of '{row.Tool.name}' is not linked to its group.");
                Assert.AreEqual(PartManager.Tool.Sticker, part.typeTool, $"Kind of the part of '{row.Tool.name}'.");
                Assert.AreEqual(row.Tool.name, part.nameTool, "The part should know its sticker tool.");
                Assert.AreEqual(row.Item.getId(), part.guidTool, "The part should know its slot, to find the image again.");
                Assert.AreEqual(row.Name.text, part.meaning, $"The part of '{row.Tool.name}' should carry the slot's name.");
                Assert.IsNotNull(part.view, $"The part of '{row.Tool.name}' did not store its view.");
                Assert.Greater(part.partCommands.Count, 0, $"The part of '{row.Tool.name}' has no commands.");
            }
            Assert.AreEqual(2 * group.groupParts[0].partCommands.Count, group.groupParts[1].partCommands.Count,
                "Both stickers of the second slot should be in its part.");
            Assert.AreNotEqual(group.groupParts[0].id, group.groupParts[1].id, "Every part needs an id of its own.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>Undo takes a sticker off the body and out of its group, redo brings both back -
        /// a sticker is a decal, not the sphere of a marker, so it is replayed differently. Redo is
        /// a replay, so it is compared with a replay of the sticker, not with the live placement
        /// (see <see cref="ReplayDeviation"/>).</summary>
        [UnityTest]
        public IEnumerator UndoAndRedo_TakeBackAndRestoreASticker()
        {
            yield return LoadLipEdemaTwin();
            yield return PrepareToPaint();
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.currentGroup;
            Color32[] blank = BodyPixels();

            yield return SelectSticker(StickerRows()[0]);
            yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());
            Assert.AreEqual(1, group.groupParts.Count, "Setup: the sticker should be one part.");
            string partId = group.groupParts[0].id;
            Color32[] replayed = null;
            yield return Replay(partManager, blank, pixels => replayed = pixels);

            Assert.IsTrue(partManager.Undo(), "A sticker should be undoable.");
            yield return new WaitForSeconds(SettleSeconds);
            Assert.AreEqual(0, group.groupParts.Count, "Undo takes the sticker's part out of its group.");
            Assert.AreEqual(0, ChangedPixels(blank, BodyPixels(), out _), "Undo takes the sticker off the body.");

            Assert.IsTrue(partManager.Redo(), "An undone sticker should be redoable.");
            yield return new WaitForSeconds(SettleSeconds);
            Assert.AreEqual(1, group.groupParts.Count, "Redo puts the part back into its group.");
            Assert.AreEqual(partId, group.groupParts[0].id, "It is the same part.");
            Assert.AreEqual(0, ChangedPixels(replayed, BodyPixels(), out _), "Redo puts the sticker back exactly.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>Placed stickers come back after the app is restarted, pixel for pixel: one with
        /// an image of its own, turned and flipped - turn and flip are part of the paint command,
        /// not of the tool - and one with the default image. What is compared is the body as the
        /// saved parts paint it before the restart and after it (see
        /// <see cref="ReplayDeviation"/>).</summary>
        [UnityTest]
        public IEnumerator PlacedSticker_SurvivesARestartPixelForPixel()
        {
            yield return LoadLipEdemaTwin();
            string twin = DataPersistenceManager.instance.selectedProfileId;
            List<StickerRow> rows = StickerRows();
            WriteQuarteredImage(twin, rows[0].Item.getId());
            yield return OpenTwin(twin);
            yield return PrepareToPaint();
            PartManager partManager = FindPartManager();
            string groupId = partManager.currentGroup.id;
            Color32[] blank = BodyPixels();

            yield return SelectSticker(rows[0]);
            yield return ClickEditButton(TurnLeft);
            yield return ClickEditButton(FlipHorizontal);
            yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());
            yield return SelectSticker(rows[1]);
            Vector2 below = ScreenCentre() + new Vector2(0f, -300f);
            yield return PaintInOneFrame(below, below);
            Color32[] replayed = null;
            yield return Replay(partManager, blank, pixels => replayed = pixels);
            int area = ChangedPixels(blank, replayed, out _);

            yield return RestartApp();
            Assert.AreEqual(twin, DataPersistenceManager.instance.selectedProfileId,
                "The app should come back with the twin it was closed with.");
            yield return new WaitForSeconds(SettleSeconds);

            PartManager.GroupData group = FindPartManager().groups.FirstOrDefault(g => g.id == groupId);
            Assert.IsNotNull(group, "The group the stickers went into did not come back.");
            Assert.AreEqual(2, group.groupParts.Count, "The stickers' parts did not come back.");
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(PartManager.Tool.Sticker, group.groupParts[i].typeTool);
                Assert.AreEqual(rows[i].Item.getId(), group.groupParts[i].guidTool, "The part should know its slot.");
            }
            AssertPartsAreUsable(FindPartManager());

            int differing = ChangedPixels(replayed, BodyPixels(), out _);
            Debug.Log($"[Test] stickers of {area} pixels, {differing} differ after the restart");
            Assert.AreEqual(0, differing, $"{differing} of the stickers' {area} pixels came back different.");
        }

        #endregion

        #region Helper functions

        static Sticker FindStickerSlot()
        {
            return FindStickerSlots(1)[0];
        }

        /// <summary>Sticker slots of the scene, each with an id of its own and therefore a hash
        /// of its own.</summary>
        static Sticker[] FindStickerSlots(int count)
        {
            Sticker[] slots = Object.FindObjectsOfType<Sticker>(true);
            Assert.GreaterOrEqual(slots.Length, count, $"The scene should offer at least {count} sticker slots.");
            Sticker[] picked = new Sticker[count];
            System.Array.Copy(slots, picked, count);
            for (int i = 1; i < count; i++)
            {
                Assert.AreNotEqual(SlotHash(picked[i - 1]), SlotHash(picked[i]),
                    "Setup: the picked sticker slots should have hashes of their own.");
            }
            return picked;
        }

        static string SlotId(Sticker slot)
        {
            return slot.GetComponent<Item>().getId();
        }

        static int SlotHash(Sticker slot)
        {
            return slot.GetComponent<Item>().getHash();
        }

        /// <summary>Loads a twin the way the twin list does.</summary>
        IEnumerator OpenTwin(string profileId)
        {
            DataPersistenceManager.instance.ChangeSelectedProfileId(profileId);
            yield return null;
        }

        /// <summary>Creates a twin through the save screen and leaves the app on it.</summary>
        IEnumerator CreateTwin(string name)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", name);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Puts a sticker image of one colour into the directory of a twin, the way
        /// picking an image from the gallery does.</summary>
        static void WriteStickerImage(string profileId, string slotId, Color color)
        {
            Texture2D image = new Texture2D(4, 4);
            Color[] pixels = new Color[image.width * image.height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            image.SetPixels(pixels);
            image.Apply();

            string fullPath = Path.Combine(DataPaths.PersistentDataPath, profileId, slotId + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        /// <summary>The image the sticker tool paints with.</summary>
        static Texture2D StickerImage(Sticker slot)
        {
            CwDemoButton button = slot.GetComponent<CwDemoButton>();
            Assert.IsNotNull(button, "The sticker slot has no button, so its tool cannot be found.");
            CwPaintDecal decal = button.IsolateTarget.gameObject.GetComponent<CwPaintDecal>();
            Assert.IsNotNull(decal, "The tool of the sticker slot has no decal.");
            return decal.Texture as Texture2D;
        }

        static void AssertStickerImage(Sticker slot, Color expected, string when)
        {
            Texture2D image = StickerImage(slot);
            Assert.IsNotNull(image, $"The sticker slot has no image {when}.");
            Color actual = image.GetPixel(0, 0);
            Assert.AreEqual(expected.r, actual.r, 0.1f, $"Red channel of the sticker image {when}.");
            Assert.AreEqual(expected.g, actual.g, 0.1f, $"Green channel of the sticker image {when}.");
            Assert.AreEqual(expected.b, actual.b, 0.1f, $"Blue channel of the sticker image {when}.");
        }

        /// <summary>The hash of a slot is what its paint commands refer to, so it has to point at
        /// the same image the tool paints with - otherwise replayed stickers show another twin's
        /// image.</summary>
        static void AssertRegisteredImage(Sticker slot, int slotHash)
        {
            Texture registered;
            Assert.IsTrue(CwSerialization.HashToTexture.TryGetValue(new CwHash(slotHash), out registered),
                $"No image is registered for the hash {slotHash} of the sticker slot.");
            Assert.AreSame(StickerImage(slot), registered,
                "The hash of the sticker slot points at another image than its tool paints with.");
        }

        #endregion

        // ------------------------------------------------------------------------------------
        // App flows
        // ------------------------------------------------------------------------------------
        #region App flows

        /// <summary>From the twin list: the body framed for painting, the first group the current
        /// one, the sticker panel open.</summary>
        IEnumerator PrepareToPaint()
        {
            yield return ClickButtonByPath("Canvas/Save UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Sticker/Text Background/Text");
            yield return WaitForModeActive("EditSticker");
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>From the main screen to the sticker panel of the Edit screen.</summary>
        IEnumerator OpenStickerPanel()
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Sticker/Text Background/Text");
            yield return WaitForModeActive("EditSticker");
        }

        /// <summary>From the sticker panel back to the main screen.</summary>
        IEnumerator CloseStickerPanel()
        {
            yield return ClickButtonByPath("Canvas/EditSticker UI/Bottom/Buttons/Edit");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Picks a sticker the way its slot in the panel does.</summary>
        IEnumerator SelectSticker(StickerRow row)
        {
            yield return ClickButtonByPath($"{StickerPanel}/{row.Row.name}");
            AssertOnlyActiveTool(row.Tool);
        }

        /// <summary>Clicks one of the six turn / flip / size buttons of the sticker panel.</summary>
        static IEnumerator ClickEditButton(string button)
        {
            yield return ClickButtonByPath($"{StickerButtons}/{button}");
        }

        /// <summary>Types a name into a sticker slot of the twin that is open, from the main screen
        /// and back to it.</summary>
        IEnumerator EnterStickerName(StickerRow row, string stickerName)
        {
            yield return OpenStickerPanel();
            row.Name.text = stickerName;
            row.Name.onEndEdit.Invoke(stickerName);
            yield return CloseStickerPanel();
        }

        /// <summary>Opens a twin and checks the name a sticker slot shows there and the name stored
        /// in the twin's file.</summary>
        IEnumerator AssertStickerName(string profileId, StickerRow row, string expected, string when)
        {
            yield return OpenTwin(profileId);
            Assert.AreEqual(expected, row.Name.text, $"The name shown for the sticker in twin {profileId} {when}.");

            ConfigData saved = DataPersistenceManager.instance.GetAllProfilesGameData()[profileId];
            saved.itemTexts.TryGetValue(row.Item.getId(), out string stored);
            Assert.AreEqual(expected, stored ?? "", $"The name stored for the sticker in twin {profileId} {when}.");
        }

        /// <summary>Saves, closes and starts the app again on the same data, like a user quitting
        /// and reopening it.</summary>
        IEnumerator RestartApp()
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

        #endregion
        
        // ------------------------------------------------------------------------------------
        // Sticker slots
        // ------------------------------------------------------------------------------------
        #region Sticker slots

        /// <summary>One slot of the sticker panel and the tool it switches on.</summary>
        class StickerRow
        {
            public Transform Row;
            public Sticker Slot;
            public GameObject Tool;
            public CwPaintDecal Decal;
            public InputField Name;
            public Item Item;
        }

        /// <summary>The slots of the sticker panel, in panel order.</summary>
        List<StickerRow> StickerRows()
        {
            var rows = new List<StickerRow>();
            foreach (Transform row in FindGameObjectByPath(StickerPanel).transform)
            {
                Sticker slot = row.GetComponent<Sticker>();
                Assert.IsNotNull(slot, $"'{row.name}' in the sticker panel is no sticker slot.");
                CwDemoButton button = row.GetComponent<CwDemoButton>();
                Assert.IsTrue(button != null && button.IsolateTarget != null, $"'{row.name}' switches no tool on.");
                GameObject tool = button.IsolateTarget.gameObject;
                CwPaintDecal decal = tool.GetComponent<CwPaintDecal>();
                Assert.IsNotNull(decal, $"The tool '{tool.name}' of '{row.name}' paints no image.");
                InputField name = row.GetComponentInChildren<InputField>(true);
                Assert.IsNotNull(name, $"'{row.name}' has no field for its name.");

                rows.Add(new StickerRow
                {
                    Row = row,
                    Slot = slot,
                    Tool = tool,
                    Decal = decal,
                    Name = name,
                    Item = row.GetComponent<Item>()
                });
            }
            return rows;
        }

        void AssertOnlyActiveTool(GameObject tool)
        {
            Assert.IsTrue(tool.activeInHierarchy, $"The slot of '{tool.name}' did not switch its tool on.");
            string[] active = FindGameObjectByPath("Tools").transform.Cast<Transform>()
                .Where(child => child.gameObject.activeSelf)
                .Select(child => child.name)
                .ToArray();
            CollectionAssert.AreEqual(new[] { tool.name }, active, $"Only '{tool.name}' should be switched on.");
        }

        static void AssertScale(Vector3 expected, CwPaintDecal decal, string message)
        {
            Assert.AreEqual(expected.x, decal.Scale.x, ScaleTolerance, message + " (x)");
            Assert.AreEqual(expected.y, decal.Scale.y, ScaleTolerance, message + " (y)");
            Assert.AreEqual(expected.z, decal.Scale.z, ScaleTolerance, message + " (z)");
       }

        /// <summary>Puts the four-quarter test image into a sticker slot of a twin, the way picking
        /// an image from the gallery does. Row 0 of a texture is its bottom.</summary>
        static void WriteQuarteredImage(string profileId, string slotId)
        {
            const int size = 64;
            Texture2D image = new Texture2D(size, size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool top = y >= size / 2;
                    bool left = x < size / 2;
                    pixels[y * size + x] = top ? (left ? TopLeft : TopRight) : (left ? BottomLeft : BottomRight);
                }
            }
            image.SetPixels(pixels);
            image.Apply();

            string fullPath = Path.Combine(DataPaths.PersistentDataPath, profileId, slotId + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        #endregion

        // ------------------------------------------------------------------------------------
        // Painting and reading the paint
        // ------------------------------------------------------------------------------------
        #region Painting and reading the paint

        /// <summary>Where the quarters of the test image landed on the body texture.</summary>
        class Placement
        {
            public int Area;
            public Vector2 LeftToRight;
            public Vector2 TopToBottom;

            public override string ToString()
            {
                return $"{Area} pixels, left to right {LeftToRight}, top to bottom {TopToBottom}";
            }
        }

        /// <summary>Places the selected sticker in the screen centre, measures where its quarters
        /// landed, and takes it off again.</summary>
        IEnumerator PaintAndMeasure(PartManager partManager, System.Action<Placement> result)
        {
            Color32[] before = BodyPixels();
            yield return PaintInOneFrame(ScreenCentre(), ScreenCentre());
            result(Measure(before, BodyPixels()));
            Assert.IsTrue(partManager.Undo(), "Setup: the sticker should be undoable.");
            yield return new WaitForSeconds(SettleSeconds);
        }

        /// <summary>A sticker repainted from its saved command differs from the live placement in a
        /// few pixels along its edge - measured 224 of 646 087 with an image of its own, 2 188 with
        /// the default image, the same on every replay, after an undo and redo and after a restart.
        /// What is restored is therefore compared with a replay, and the replay with the live
        /// placement only within this share.</summary>
        const float ReplayDeviation = 0.01f;

        /// <summary>Repaints the body from the recorded parts, as undo, redo, hiding a group and
        /// loading a twin do, checks it against the live placement and hands the result over.</summary>
        IEnumerator Replay(PartManager partManager, Color32[] blank, System.Action<Color32[]> result)
        {
            Color32[] live = BodyPixels();
            int area = ChangedPixels(blank, live, out _);
            Assert.Greater(area, 0, "Setup: the sticker should show on the body.");
            partManager.ClearRefreshAll();
            yield return new WaitForSeconds(SettleSeconds);
            Color32[] replayed = BodyPixels();
            int differing = ChangedPixels(live, replayed, out _);
            Debug.Log($"[Test] replay differs from the live placement in {differing} of {area} pixels");
            Assert.LessOrEqual(differing, area * ReplayDeviation,
                $"The replay differs from the live placement in {differing} of {area} pixels.");
            result(replayed);
        }

        /// <summary>Sorts every changed pixel into the quarter whose colour it is closest to and
        /// returns the directions between the centres of the quarters, in texture space.</summary>
        static Placement Measure(Color32[] before, Color32[] after)
        {
            Color[] quarters = { TopLeft, TopRight, BottomLeft, BottomRight };
            var sums = new Vector2[4];
            var counts = new int[4];
            int width = (int)Mathf.Sqrt(after.Length);
            int area = 0;
            for (int i = 0; i < after.Length; i++)
            {
                Color32 x = before[i];
                Color32 y = after[i];
                if (x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a) continue;
                area++;
                int quarter = System.Array.IndexOf(quarters, Nearest(quarters, y));
                sums[quarter] += new Vector2(i % width, i / width);
                counts[quarter]++;
            }
            for (int q = 0; q < 4; q++)
            {
                Assert.Greater(counts[q], 0, $"No pixel of quarter {q} of the test image landed on the body.");
            }
            Vector2 Centre(int q) => sums[q] / counts[q];
            Vector2 left = (Centre(0) + Centre(2)) / 2f;
            Vector2 right = (Centre(1) + Centre(3)) / 2f;
            Vector2 top = (Centre(0) + Centre(1)) / 2f;
            Vector2 bottom = (Centre(2) + Centre(3)) / 2f;
            return new Placement { Area = area, LeftToRight = right - left, TopToBottom = bottom - top };
        }

        static float Cosine(Vector2 a, Vector2 b)
        {
            return Vector2.Dot(a.normalized, b.normalized);
        }

        static Vector2 ScreenCentre()
        {
            return new Vector2(Screen.width / 2f, Screen.height / 2f);
        }

        /// <summary>Drives the paint pipeline of the active tool through the given screen
        /// positions - down on the first, up on the last; a sticker is placed on the way up. The
        /// whole gesture has to happen within one frame: between frames the real mouse, which is
        /// not pressed, ends the synthetic finger (see the painting guide).</summary>
        static IEnumerator PaintInOneFrame(params Vector2[] screenPoints)
        {
            CwHitScreen[] hitScreens = Object.FindObjectsOfType<CwHitScreen>(false);
            Assert.Greater(hitScreens.Length, 0, "No active CwHitScreen - is a sticker selected?");

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

        static Color32[] BodyPixels()
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
        static int ChangedPixels(Color32[] before, Color32[] after, out Color average)
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

        static Color Nearest(Color[] palette, Color colour)
        {
            return palette.OrderBy(candidate =>
                Mathf.Pow(candidate.r - colour.r, 2) +
                Mathf.Pow(candidate.g - colour.g, 2) +
                Mathf.Pow(candidate.b - colour.b, 2)).First();
        }
        #endregion
    }
}
