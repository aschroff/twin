using System.Collections;
using System.IO;
using CW.Common;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Proof of concept for Assets/Code/Proc/Paint/FEATURE_TEXT_TO_PART.md ("Part Library" approach):
    /// template parts for named body regions are created by driving the REAL CW paint
    /// pipeline programmatically (GetFinger -> HandleFingerUpdate -> Physics.Raycast),
    /// so the resulting CwCommandSphere data is exactly as valid as hand-painted data.
    ///
    /// Region aim points are derived from SMPL-X bone transforms projected into screen
    /// space with the main camera. The region name is stored in PartData.description
    /// (the free-text field the user edits in the Part detail UI).
    ///
    /// Screenshots for visual QA are written to:
    ///   Application.temporaryCachePath/TemplatePoCShots/
    /// </summary>
    public class ProgrammaticPaintingTests : PlayModeTestBase
    {
        // NOTE: TwinNameValidator limits twin names to 11 chars ("^[a-zA-Z0-9_()-]{1,11}$").
        private const string TwinName = "Templates";

        private static string ShotsDir => Path.Combine(Application.temporaryCachePath, "TemplatePoCShots");

        [UnityTest]
        public IEnumerator PaintTemplateParts_ThreeRegions()
        {
            if (Directory.Exists(ShotsDir))
                Directory.Delete(ShotsDir, recursive: true);
            Directory.CreateDirectory(ShotsDir);

            // SetUp already redirects persistence to a clean temp directory,
            // so a fresh twin is enough — no ResetApp needed.
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", TwinName);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.IsNotNull(partManager, "PartManager not found in scene.");

            // Template group, created programmatically. The Group UI item stays null
            // until the next LoadData/rebuild, which is fine for data-only purposes.
            var groupData = partManager.StartNewGroup(null);
            groupData.id = System.Guid.NewGuid().ToString();
            groupData.name = "Templates";
            groupData.visible = true;

            var cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera not found.");

            // --- Region 1: left_forearm_anterior (marker stroke elbow -> wrist) ---
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            AssertModeActive("EditMarker");
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
            AssertGameObjectActive("Tools/Red");

            var elbow = FindBone("left_elbow").position;
            var wrist = FindBone("left_wrist").position;
            var stroke = ScreenLine(cam,
                Vector3.Lerp(elbow, wrist, 0.30f),
                Vector3.Lerp(elbow, wrist, 0.80f),
                6);
            yield return PaintStroke(stroke);
            FinishPart(partManager, "left_forearm_anterior");
            Assert.Greater(partManager.currentPart.partCommands.Count, 0,
                "left_forearm_anterior: painting produced no commands.");
            yield return CaptureShot("01_left_forearm_anterior");

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            AssertModeActive("Edit");

            // --- Region 2: abdomen_central (filler: closed outline, fills on finger up) ---
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Filler/Text Background/Text");
            AssertModeActive("EditFiller");
            yield return ClickButtonByPath("Canvas/EditFiller UI/Bottom/Scroll/Panel/Red");
            AssertGameObjectActive("Tools/Red Filling");

            var pelvis = FindBone("pelvis").position;
            var spine2 = FindBone("spine2").position;
            var belly = Vector3.Lerp(pelvis, spine2, 0.5f);
            var circle = ScreenCircle(cam, belly, worldRadius: 0.07f, segments: 24);
            yield return PaintStroke(circle);
            FinishPart(partManager, "abdomen_central");
            Assert.Greater(partManager.currentPart.partCommands.Count, 0,
                "abdomen_central: filling produced no commands.");
            yield return CaptureShot("02_abdomen_central");

            yield return ClickButtonByPath("Canvas/EditFiller UI/Bottom/Buttons/Edit");
            AssertModeActive("Edit");

            // --- Region 3: head_forehead (short marker stroke above the head bone) ---
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            AssertModeActive("EditMarker");
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Blue");
            AssertGameObjectActive("Tools/Blue");

            var forehead = FindBone("head").position + Vector3.up * 0.07f;
            var foreheadStroke = ScreenLine(cam,
                forehead - cam.transform.right * 0.025f,
                forehead + cam.transform.right * 0.025f,
                4);
            yield return PaintStroke(foreheadStroke);
            FinishPart(partManager, "head_forehead");
            Assert.Greater(partManager.currentPart.partCommands.Count, 0,
                "head_forehead: painting produced no commands.");
            yield return CaptureShot("03_head_forehead");

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            AssertModeActive("Edit");

            yield return CaptureShot("04_all_regions");

            // --- Verify in-memory data model ---
            Assert.AreEqual(1, partManager.groups.Count, "Expected exactly one group.");
            var parts = partManager.groups[0].groupParts;
            Assert.AreEqual(3, parts.Count, "Expected exactly three template parts.");

            AssertPart(parts[0], "left_forearm_anterior", PartManager.Tool.MarkerLine);
            AssertPart(parts[1], "abdomen_central", PartManager.Tool.Filler);
            AssertPart(parts[2], "head_forehead", PartManager.Tool.MarkerLine);

            // --- Persist and verify the round trip through the save file ---
            var dpm = DataPersistenceManager.instance;
            Assert.IsNotNull(dpm, "DataPersistenceManager.instance is null.");
            dpm.SaveConfig();

            var profiles = dpm.GetAllProfilesGameData();
            Assert.IsTrue(profiles.ContainsKey(dpm.selectedProfileId),
                $"Saved profile '{dpm.selectedProfileId}' not found on disk.");
            var commandDetails = profiles[dpm.selectedProfileId].commandDetails;
            StringAssert.Contains("left_forearm_anterior", commandDetails);
            StringAssert.Contains("abdomen_central", commandDetails);
            StringAssert.Contains("head_forehead", commandDetails);

            // Dump the serialized template data next to the screenshots for inspection.
            File.WriteAllText(Path.Combine(ShotsDir, "commandDetails.json"), commandDetails);
            Debug.Log($"[TemplatePoC] Review output written to: {ShotsDir}");
        }

        private static void AssertPart(PartManager.PartData part, string regionName, PartManager.Tool expectedTool)
        {
            Assert.AreEqual(regionName, part.description, $"Part description mismatch for {regionName}.");
            Assert.AreEqual(expectedTool, part.typeTool, $"Tool type mismatch for {regionName}.");
            Assert.Greater(part.partCommands.Count, 0, $"{regionName} has no commands.");
            Assert.IsNotNull(part.view, $"{regionName} has no stored view.");
            Assert.IsFalse(string.IsNullOrEmpty(part.id), $"{regionName} has no id.");
        }

        /// <summary>Finalize the currently painted part and stamp it with the region name.
        /// EnforceNewPart stores tool metadata + view on the current part and flags the
        /// next paint command to start a fresh part.</summary>
        private static void FinishPart(PartManager partManager, string regionName)
        {
            partManager.EnforceNewPart();
            Assert.IsNotNull(partManager.currentPart,
                $"{regionName}: no current part after painting — did the ray miss the mesh?");
            partManager.currentPart.description = regionName;
        }

        /// <summary>Bone transforms of the SMPL-X rig, found by their unique joint names.</summary>
        private static Transform FindBone(string boneName)
        {
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go.name == boneName)
                    return go.transform;
            }
            Assert.Fail($"Bone '{boneName}' not found in scene.");
            return null;
        }

        /// <summary>Screen-space points along a world-space line, for marker strokes.</summary>
        private static Vector2[] ScreenLine(Camera cam, Vector3 from, Vector3 to, int count)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = count > 1 ? (float)i / (count - 1) : 0f;
                points[i] = WorldToScreen(cam, Vector3.Lerp(from, to, t));
            }
            return points;
        }

        /// <summary>Closed screen-space circle around a world-space center, for the fill tool
        /// (CwHitScreenFill grid-fills the area enclosed by the drawn outline on finger up).</summary>
        private static Vector2[] ScreenCircle(Camera cam, Vector3 center, float worldRadius, int segments)
        {
            Vector2 c = WorldToScreen(cam, center);
            Vector2 edge = WorldToScreen(cam, center + cam.transform.right * worldRadius);
            float radius = Vector2.Distance(c, edge);
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static Vector2 WorldToScreen(Camera cam, Vector3 world)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            Assert.Greater(screen.z, 0f, $"World point {world} is behind the camera.");
            Assert.IsTrue(screen.x >= 0 && screen.x < Screen.width && screen.y >= 0 && screen.y < Screen.height,
                $"Projected point {screen} is off screen ({Screen.width}x{Screen.height}). Adjust camera/zoom.");
            return new Vector2(screen.x, screen.y);
        }

        /// <summary>Drive the CW paint pipeline through the given screen positions.
        /// IMPORTANT: the whole stroke happens within a single frame. CwPointerMouse.Update
        /// polls the REAL mouse every frame and calls TryNullFinger(1) when no button is
        /// held, which destroys synthetic fingers between frames — a multi-frame synthetic
        /// stroke therefore only ever paints its first (down) event. Within one frame the
        /// poll cannot interfere, and CwPaintableManager still flushes in LateUpdate.
        /// The final finger-up event is what triggers CwHitScreenFill's flood fill.</summary>
        private IEnumerator PaintStroke(Vector2[] screenPoints)
        {
            Assert.Greater(screenPoints.Length, 1, "PaintStroke needs at least two points.");
            var hitScreens = Object.FindObjectsOfType<CwHitScreen>(false);
            Assert.Greater(hitScreens.Length, 0,
                "No active CwHitScreen — is a paint tool selected?");

            foreach (var hitScreen in hitScreens)
            {
                var pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
                if (pointerMouse == null) continue;
                hitScreen.GuiLayers = 0; // bypass the "started over GUI" guard in tests

                CwInputManager.Finger finger;
                pointerMouse.GetFinger(1, screenPoints[0], 1.0f, true, out finger);
                hitScreen.HandleFingerUpdate(finger, true, false);

                for (int i = 1; i < screenPoints.Length - 1; i++)
                {
                    pointerMouse.GetFinger(1, screenPoints[i], 1.0f, true, out finger);
                    hitScreen.HandleFingerUpdate(finger, false, false);
                }

                pointerMouse.GetFinger(1, screenPoints[screenPoints.Length - 1], 1.0f, true, out finger);
                hitScreen.HandleFingerUpdate(finger, false, true);
                pointerMouse.TryNullFinger(1);
            }

            yield return null;
            yield return null; // let CwPaintableManager.LateUpdate flush the commands
        }

        private IEnumerator CaptureShot(string shotName)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(ShotsDir, shotName + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
