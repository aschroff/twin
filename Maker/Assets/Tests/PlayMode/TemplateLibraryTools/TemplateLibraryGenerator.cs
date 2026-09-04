using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CW.Common;
using Lean.Common;
using NUnit.Framework;
using PaintCore;
using PaintIn3D;
using UnityEngine;
using UnityEngine.TestTools;

namespace TemplateLibraryTools
{
    /// <summary>
    /// Generates the body-region template library (see Assets/Resources/BODY_REGIONS.md) by painting
    /// each region through the real CW paint pipeline — batch by batch, incrementally:
    ///
    /// - Output persists in "<project>/TemplateLibrary/" (data + per-region screenshots),
    ///   NOT in the auto-deleted PlayMode test temp directory.
    /// - The twin "Templates" is loaded if it already exists; regions whose key is already
    ///   present (PartData.description) are SKIPPED, so batches are idempotent and
    ///   re-runnable without destroying earlier results.
    /// - The twin is saved after EVERY region — a mid-batch failure keeps all finished regions.
    /// - To regenerate specific regions, list their keys (one per line) in
    ///   "TemplateLibrary/regenerate.txt"; they are deleted and repainted on the next run.
    /// - Failures (off-screen anchor, raycast miss) are collected per region and reported
    ///   at the end without aborting the batch.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: each region gets its OWN group (group name = region key). The app's save
    /// format inlines the PartData.group ↔ GroupData.groupParts reference cycle up to
    /// JsonUtility's depth limit of 10, so the file size grows EXPONENTIALLY with the number
    /// of parts per group (8 parts in one group ≈ 635 MB!). One part per group keeps the
    /// expansion linear and the save file small — and gives free region lookup by group name.
    /// </remarks>
    public class TemplateLibraryGenerator : PlayModeTestBase
    {
        private const string TwinName = "Templates"; // TwinNameValidator: max 11 chars

        private static string LibraryDir => Path.Combine(Application.dataPath, "..", "TemplateLibrary");
        private static string DataDir => Path.Combine(LibraryDir, "data");
        private static string ShotsDir => Path.Combine(LibraryDir, "Screenshots");
        private static string RegenerateFile => Path.Combine(LibraryDir, "regenerate.txt");

        private System.IDisposable saveNameScope;
        private PartManager partManager;
        private LeanPitchYaw pitchYaw;
        private Quaternion neutralBodyRotation;
        private Dictionary<string, Transform> boneCache;
        private string currentToolKind; // "marker" | "filler" | null
        private readonly List<string> failedRegions = new List<string>();

        public override IEnumerator SetUp()
        {
            // Deliberately NOT calling base.SetUp(): it wipes its temp directory each run,
            // while the template library must persist and accumulate across batch runs.
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(ShotsDir);
            DataPaths.SetPersistentDataPathForTests(DataDir);
            saveNameScope = PaintableSaveNameOverride.Begin("TemplateLibrary");

            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Maker Main", UnityEngine.SceneManagement.LoadSceneMode.Single);
            yield return null;

            Controller = Object.FindObjectOfType<InteractionController>();
            Assert.IsNotNull(Controller, "InteractionController not found in scene.");
            var modesField = typeof(InteractionController).GetField(
                "interactionModes",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            InteractionModes = modesField.GetValue(Controller) as InteractionModeDictionary;
            Assert.IsNotNull(InteractionModes, "interactionModes is null.");

            failedRegions.Clear();
            currentToolKind = null;
        }

        public override IEnumerator TearDown()
        {
            saveNameScope?.Dispose();
            saveNameScope = null;
            yield return base.TearDown();
        }

        // ---------------- Batches ----------------

        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch01_TorsoFront() { yield return RunBatch("torso_front"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch02_TorsoBack() { yield return RunBatch("torso_back"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch03_ArmsFront() { yield return RunBatch("arms_front"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch04_ArmsBack() { yield return RunBatch("arms_back"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch05_LegsFront() { yield return RunBatch("legs_front"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch06_LegsBack() { yield return RunBatch("legs_back"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch07_HeadNeck() { yield return RunBatch("head_neck"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch08_Hands() { yield return RunBatch("hands"); }
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(900000)] public IEnumerator Batch09_ExtendedMisc() { yield return RunBatch("extended_misc"); }

        /// <summary>Diagnostic for the replay-vs-live rendering difference: paints a fresh disc,
        /// replays it in the SAME session, and replays loaded (previous-session) data —
        /// screenshots + position logs isolate serialization issues from replay issues.</summary>
        [UnityTest, Explicit("Template library generation tool — run via Tools menu, not part of the app test suite"), Timeout(600000)]
        public IEnumerator Batch00_Diagnose()
        {
            yield return EnsureTemplatesTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            partManager = Object.FindObjectOfType<PartManager>();
            CacheBones();
            SetupBodyControl();
            yield return OrientBody(0f, 0f);
            neutralBodyRotation = pitchYaw.transform.rotation;

            var meshTexture = Object.FindObjectOfType<CwPaintableMeshTexture>();
            var tr = meshTexture.transform;
            Debug.Log($"[Diag] mesh transform pos={tr.position:F4} rot={tr.rotation.eulerAngles:F3} scale={tr.lossyScale:F4}");

            // Diagnostic screenshots are transient — keep them out of the library folder.
            string diagDir = Path.Combine(Application.temporaryCachePath, "TemplateDiagShots");
            Directory.CreateDirectory(diagDir);

            // 1) replay data loaded from the previous session
            partManager.Refresh();
            yield return null; yield return null;
            yield return CaptureShotTo(diagDir, "diag_1_loaded_replay");

            var loadedPart = FindPartByDescription("abdomen_central");
            if (loadedPart != null && loadedPart.partCommands.Count > 0)
            {
                var sphere = (CwCommandSphere)loadedPart.partCommands[0].data.LocalCommand;
                Debug.Log($"[Diag] LOADED abdomen_central cmd0 localPos={sphere.Position:F4} matrixCol0Len={((Vector3)sphere.Matrix.GetColumn(0)).magnitude:F5}");
            }

            // 2) erase, paint a FRESH disc at the same spot, compare visually
            partManager.Erase();
            EnsureGroup("diag_fresh");
            yield return EnsureTool(TemplateRegionTable.ToolKind.FillCircle);
            var cam = Camera.main;
            var ctx = BuildAnchorCtx();
            var belly = Vector3.Lerp(ctx.Bone("pelvis"), ctx.Bone("spine2"), 0.5f);
            TryWorldToScreen(cam, belly, out var c);
            TryWorldToScreen(cam, belly + cam.transform.right * 0.05f, out var e);
            yield return PaintStroke(Circle(c, Vector2.Distance(c, e), 24));
            partManager.EnforceNewPart();
            if (partManager.currentPart != null)
            {
                partManager.currentPart.description = "diag_fresh";
                if (partManager.currentPart.partCommands.Count > 0)
                {
                    var sphere = (CwCommandSphere)partManager.currentPart.partCommands[0].data.LocalCommand;
                    Debug.Log($"[Diag] FRESH diag_fresh cmd0 localPos={sphere.Position:F4} matrixCol0Len={((Vector3)sphere.Matrix.GetColumn(0)).magnitude:F5} cmds={partManager.currentPart.partCommands.Count}");
                }
            }
            yield return CaptureShotTo(diagDir, "diag_2_live");

            // 3) erase + replay EVERYTHING in the same session (fresh part has live refs)
            partManager.Erase();
            partManager.Refresh();
            yield return null; yield return null;
            yield return CaptureShotTo(diagDir, "diag_3_same_session_replay");

            // cleanup: remove the diagnostic part so it never pollutes the library
            var diagPart = FindPartByDescription("diag_fresh");
            if (diagPart != null)
            {
                partManager.deletePart(diagPart);
                partManager.groups.RemoveAll(g => g.name == "diag_fresh" && g.groupParts.Count == 0);
            }
            partManager.ClearRefreshAll();
        }

        private PartManager.PartData FindPartByDescription(string description)
        {
            if (partManager.groups == null) return null;
            foreach (var group in partManager.groups)
                foreach (var part in group.groupParts)
                    if (part.description == description)
                        return part;
            return null;
        }

        // ---------------- Core flow ----------------

        private IEnumerator RunBatch(string batchName)
        {
            var regions = TemplateRegionTable.ForBatch(batchName).ToList();
            Assert.Greater(regions.Count, 0, $"No regions defined for batch '{batchName}'.");

            yield return EnsureTemplatesTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            partManager = Object.FindObjectOfType<PartManager>();
            Assert.IsNotNull(partManager, "PartManager not found.");
            CacheBones();
            SetupBodyControl();
            // Loaded command data is not re-applied to the paint texture automatically, and the
            // very first replay of a session renders misaligned (crescent artifacts) until one
            // live paint has gone through the pipeline (suspected skinned-mesh bake happening on
            // first hit-based paint). Warm up with a throwaway dot first, then erase and replay.
            yield return WarmUpPaintPipeline();
            partManager.Erase();
            partManager.Refresh();
            yield return null;
            yield return null;

            // capture the neutral body rotation for patient-axis math
            yield return OrientBody(0f, 0f);
            neutralBodyRotation = pitchYaw.transform.rotation;

            var existing = ExistingRegionKeys();
            var regenerate = ReadRegenerateList();
            // a batch may only delete ITS OWN keys — otherwise it deletes regions that
            // another batch owns and never repaints them
            regenerate.IntersectWith(regions.Select(r => r.Key));
            DeleteRegionsForRegeneration(regenerate);

            int painted = 0, skipped = 0;
            foreach (var region in regions)
            {
                if (existing.Contains(region.Key) && !regenerate.Contains(region.Key))
                {
                    skipped++;
                    continue;
                }
                yield return PaintRegion(region);
                painted++;
            }

            // overview screenshots (front + back) after resetting orientation
            yield return OrientBody(0f, 0f);
            yield return CaptureShot($"_batch_{batchName}_front");
            yield return OrientBody(180f, 0f);
            yield return CaptureShot($"_batch_{batchName}_back");
            yield return OrientBody(0f, 0f);

            ExportCommandDetails();
            Debug.Log($"[TemplateLibrary] Batch '{batchName}': painted={painted}, skipped(existing)={skipped}, failed={failedRegions.Count}");
            Assert.IsEmpty(failedRegions,
                $"Batch '{batchName}' had failing regions (already-painted regions are saved): {string.Join(", ", failedRegions)}");
        }

        private IEnumerator PaintRegion(TemplateRegionTable.RegionDef region)
        {
            EnsureGroup(region.Key); // one group per region — see class remarks (save-size blowup)
            yield return OrientBody(region.Yaw, region.Pitch);
            yield return EnsureTool(region.Tool);

            var cam = Camera.main;
            var ctx = BuildAnchorCtx();
            var anchors = region.Anchors(ctx);

            // screen-space geometry; soft-fail when anchors project off screen
            Vector2[] points;
            if (region.Tool == TemplateRegionTable.ToolKind.FillCircle)
            {
                if (!TryWorldToScreen(cam, anchors[0], out var center) ||
                    !TryWorldToScreen(cam, anchors[0] + cam.transform.right * region.Radius, out var edge))
                {
                    Fail(region.Key, "anchor off screen");
                    yield break;
                }
                points = Circle(center, Vector2.Distance(center, edge), 24);
            }
            else
            {
                if (!TryWorldToScreen(cam, anchors[0], out var from) ||
                    !TryWorldToScreen(cam, anchors[1], out var to))
                {
                    Fail(region.Key, "anchor off screen");
                    yield break;
                }
                points = Line(from, to, 8);
            }

            var previousPart = partManager.currentPart;
            float restoreRadius = -1f;
            CwPaintSphere paintSphere = ActivePaintSphere();
            if (region.BrushRadius > 0f && paintSphere != null)
            {
                restoreRadius = paintSphere.Radius;
                paintSphere.Radius = region.BrushRadius;
            }

            yield return PaintStroke(points);

            if (restoreRadius >= 0f && paintSphere != null)
            {
                paintSphere.Radius = restoreRadius;
            }

            // a new part must have been created — otherwise every raycast missed the mesh
            if (partManager.currentPart == null || partManager.currentPart == previousPart ||
                partManager.currentPart.partCommands.Count == 0)
            {
                Fail(region.Key, "raycast missed the mesh (no part created)");
                yield break;
            }

            partManager.EnforceNewPart(); // stamps tool metadata + view, flags next part
            partManager.currentPart.description = region.Key;

            DataPersistenceManager.instance.SaveConfig(); // crash-safe: persist after every region
            yield return CaptureShot(region.Key);
        }

        // ---------------- Twin / group / tool management ----------------

        private IEnumerator EnsureTemplatesTwin()
        {
            var dpm = DataPersistenceManager.instance;
            Assert.IsNotNull(dpm, "DataPersistenceManager.instance is null.");
            yield return WaitForModeActive("Main");

            if (dpm.selectedProfileId != TwinName + ".000")
            {
                yield return ClickButtonByName("Save Button");
                yield return WaitForModeActive("Save");
                SetInputByName("InputField", TwinName);
                yield return ClickButtonByName("New");
                yield return WaitForModeActive("Main");
                Assert.AreEqual(TwinName + ".000", dpm.selectedProfileId,
                    "Failed to create/select the Templates twin.");
            }
        }

        private void EnsureGroup(string groupName)
        {
            if (partManager.groups != null)
            {
                var existing = partManager.groups.FirstOrDefault(g => g.name == groupName);
                if (existing != null)
                {
                    partManager.currentGroup = existing;
                    return;
                }
            }
            var groupData = partManager.StartNewGroup(null);
            groupData.id = System.Guid.NewGuid().ToString();
            groupData.name = groupName;
            groupData.visible = true;
        }

        private IEnumerator EnsureTool(TemplateRegionTable.ToolKind kind)
        {
            string wanted = kind == TemplateRegionTable.ToolKind.MarkerStroke ? "marker" : "filler";
            if (currentToolKind == wanted)
                yield break;

            // leave the current tool mode back to Edit
            if (currentToolKind == "marker")
            {
                yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
                yield return WaitForModeActive("Edit");
            }
            else if (currentToolKind == "filler")
            {
                yield return ClickButtonByPath("Canvas/EditFiller UI/Bottom/Buttons/Edit");
                yield return WaitForModeActive("Edit");
            }

            if (wanted == "marker")
            {
                yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
                yield return WaitForModeActive("EditMarker");
                yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
                AssertGameObjectActive("Tools/Red");
            }
            else
            {
                yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Filler/Text Background/Text");
                yield return WaitForModeActive("EditFiller");
                yield return ClickButtonByPath("Canvas/EditFiller UI/Bottom/Scroll/Panel/Red");
                AssertGameObjectActive("Tools/Red Filling");
            }
            currentToolKind = wanted;
        }

        private CwPaintSphere ActivePaintSphere()
        {
            var toolsRoot = GameObject.FindGameObjectsWithTag("Tools").FirstOrDefault();
            if (toolsRoot == null) return null;
            foreach (Transform child in toolsRoot.transform)
            {
                if (child.gameObject.activeSelf)
                    return child.GetComponent<CwPaintSphere>();
            }
            return null;
        }

        // ---------------- Body orientation ----------------

        private void SetupBodyControl()
        {
            var viewManager = Object.FindObjectOfType<ViewManager>();
            Assert.IsNotNull(viewManager, "ViewManager not found.");
            pitchYaw = viewManager.body.GetComponent<LeanPitchYaw>();
            Assert.IsNotNull(pitchYaw, "LeanPitchYaw not found on body.");
            // hands/head-top/soles need steep pitch, back views need yaw 180 —
            // disable the scene's clamps for the generator run (play mode only, not persisted)
            pitchYaw.PitchClamp = false;
            pitchYaw.YawClamp = false;
        }

        private IEnumerator OrientBody(float yaw, float pitch)
        {
            pitchYaw.Yaw = yaw;
            pitchYaw.Pitch = pitch;

            // LeanPitchYaw may apply damping — wait until the rotation settles
            var previous = pitchYaw.transform.rotation;
            for (int i = 0; i < 120; i++)
            {
                yield return null;
                var current = pitchYaw.transform.rotation;
                if (Quaternion.Angle(previous, current) < 0.05f)
                    yield break;
                previous = current;
            }
            Debug.LogWarning($"[TemplateLibrary] Body rotation did not settle for yaw={yaw}, pitch={pitch}.");
        }

        private TemplateRegionTable.AnchorCtx BuildAnchorCtx()
        {
            var delta = pitchYaw.transform.rotation * Quaternion.Inverse(neutralBodyRotation);
            return new TemplateRegionTable.AnchorCtx
            {
                Bone = BonePosition,
                Up = delta * Vector3.up,
                Left = delta * Vector3.left,
                Forward = delta * Vector3.forward,
            };
        }

        private void CacheBones()
        {
            boneCache = new Dictionary<string, Transform>();
            var viewManager = Object.FindObjectOfType<ViewManager>();
            foreach (var tr in viewManager.body.GetComponentsInChildren<Transform>(true))
            {
                if (!boneCache.ContainsKey(tr.name))
                    boneCache[tr.name] = tr;
            }
        }

        private Vector3 BonePosition(string boneName)
        {
            Assert.IsTrue(boneCache.TryGetValue(boneName, out var bone), $"Bone '{boneName}' not found.");
            return bone.position;
        }

        // ---------------- Region bookkeeping ----------------

        private HashSet<string> ExistingRegionKeys()
        {
            var keys = new HashSet<string>();
            if (partManager.groups == null) return keys;
            foreach (var group in partManager.groups)
                foreach (var part in group.groupParts)
                    if (!string.IsNullOrEmpty(part.description))
                        keys.Add(part.description);
            return keys;
        }

        private HashSet<string> ReadRegenerateList()
        {
            var keys = new HashSet<string>();
            if (File.Exists(RegenerateFile))
            {
                foreach (var line in File.ReadAllLines(RegenerateFile))
                {
                    var key = line.Trim();
                    if (key.Length > 0 && !key.StartsWith("#"))
                        keys.Add(key);
                }
            }
            return keys;
        }

        private void DeleteRegionsForRegeneration(HashSet<string> keys)
        {
            if (keys.Count == 0 || partManager.groups == null) return;
            var doomed = new List<PartManager.PartData>();
            foreach (var group in partManager.groups)
                foreach (var part in group.groupParts)
                    if (part.description != null && keys.Contains(part.description))
                        doomed.Add(part);
            if (doomed.Count == 0) return;
            foreach (var part in doomed)
            {
                Debug.Log($"[TemplateLibrary] Deleting '{part.description}' for regeneration.");
                partManager.deletePart(part);
            }
            // drop the now-empty per-region groups (avoid PartManager.deleteGroup — it
            // dereferences the UI item, which is null for programmatically created groups)
            partManager.groups.RemoveAll(g => keys.Contains(g.name) && g.groupParts.Count == 0);
            partManager.ClearRefreshAll();
            DataPersistenceManager.instance.SaveConfig();
        }

        /// <summary>Paints and immediately deletes a throwaway dot. The first replay of a
        /// session renders misaligned until one live paint has run through the CW pipeline
        /// (suspected lazy skinned-mesh bake) — this forces that initialization.</summary>
        private IEnumerator WarmUpPaintPipeline()
        {
            EnsureGroup("warmup");
            yield return EnsureTool(TemplateRegionTable.ToolKind.MarkerStroke);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.55f);
            var previousPart = partManager.currentPart;
            yield return PaintStroke(new[] { center, center + new Vector2(4f, 0f) });
            if (partManager.currentPart != null && partManager.currentPart != previousPart)
            {
                partManager.deletePart(partManager.currentPart);
            }
            partManager.groups.RemoveAll(g => g.name == "warmup" && g.groupParts.Count == 0);
            partManager.startNewPart = true; // the deleted warm-up part must not receive further commands
        }

        private void Fail(string key, string reason)
        {
            Debug.LogError($"[TemplateLibrary] Region '{key}' failed: {reason}");
            failedRegions.Add($"{key} ({reason})");
        }

        private void ExportCommandDetails()
        {
            var dpm = DataPersistenceManager.instance;
            var profiles = dpm.GetAllProfilesGameData();
            if (profiles.TryGetValue(dpm.selectedProfileId, out var config))
            {
                File.WriteAllText(Path.Combine(LibraryDir, "commandDetails.json"), config.commandDetails);
            }
        }

        // ---------------- Painting primitives (single-frame stroke — see CwPaintingTestGuide.md) ----------------

        private IEnumerator PaintStroke(Vector2[] screenPoints)
        {
            var hitScreens = Object.FindObjectsOfType<CwHitScreen>(false);
            Assert.Greater(hitScreens.Length, 0, "No active CwHitScreen — is a paint tool selected?");

            foreach (var hitScreen in hitScreens)
            {
                var pointerMouse = hitScreen.GetComponent<CwPointerMouse>();
                if (pointerMouse == null) continue;
                hitScreen.GuiLayers = 0;

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
            yield return null;
        }

        private static bool TryWorldToScreen(Camera cam, Vector3 world, out Vector2 screen)
        {
            Vector3 p = cam.WorldToScreenPoint(world);
            screen = new Vector2(p.x, p.y);
            return p.z > 0f && p.x >= 0f && p.x < Screen.width && p.y >= 0f && p.y < Screen.height;
        }

        private static Vector2[] Line(Vector2 from, Vector2 to, int count)
        {
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
                points[i] = Vector2.Lerp(from, to, count > 1 ? (float)i / (count - 1) : 0f);
            return points;
        }

        private static Vector2[] Circle(Vector2 center, float radius, int segments)
        {
            var points = new Vector2[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private IEnumerator CaptureShot(string shotName)
        {
            yield return CaptureShotTo(ShotsDir, shotName);
        }

        private IEnumerator CaptureShotTo(string directory, string shotName)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(directory, shotName + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
