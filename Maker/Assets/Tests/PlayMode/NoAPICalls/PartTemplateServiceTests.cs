using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Tests for the Text→Part core service (Assets/Code/Proc/Paint/PartTemplateService.cs):
    /// stamping a bundled region template onto the currently loaded twin.
    /// </summary>
    public class PartTemplateServiceTests : PlayModeTestBase
    {
        /// <summary>A twin needs a user group (Injuries/Pain/…) before regions can be painted
        /// into it — same precondition as normal painting.</summary>
        private static PartManager.GroupData EnsureActiveGroup(PartManager partManager, string name)
        {
            PartManager.GroupData group = partManager.StartNewGroup(null);
            group.id = System.Guid.NewGuid().ToString();
            group.name = name;
            group.visible = true;
            partManager.currentGroup = group;
            return group;
        }

        /// <summary>A stamped region behaves like normal painting: the new part lands in the
        /// ACTIVE group (groups are the user's categories — Injuries/Pain/… — not regions).</summary>
        [UnityTest]
        public IEnumerator PaintRegion_AddsPartToActiveGroup()
        {
            // fresh twin so assertions start from a clean data model
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", "ServiceTest");
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.IsNotNull(partManager, "PartManager not found.");

            // arrange: one user group, like "Injuries", selected as active
            var injuries = EnsureActiveGroup(partManager, "Injuries");
            int groupsBefore = partManager.groups.Count;
            int partsBefore = injuries.groupParts.Count;

            var parts = PartTemplateService.PaintRegion("Arms.twin", "shoulder_front_left", null, partManager);
            yield return null;
            yield return null; // let CwPaintableManager flush the replayed commands

            // NO new group — the part joins the active one
            Assert.AreEqual(groupsBefore, partManager.groups.Count, "No group may be created for a region.");
            Assert.AreEqual(partsBefore + 1, injuries.groupParts.Count, "Active group should gain the part.");
            Assert.AreEqual(1, parts.Count, "Region templates hold exactly one part.");

            var part = parts[0];
            Assert.AreSame(injuries, part.group, "Part must reference the active group.");
            Assert.AreSame(part, injuries.groupParts[injuries.groupParts.Count - 1]);
            Assert.AreEqual("shoulder_front_left", part.description, "Region key is carried on the part.");
            Assert.Greater(part.partCommands.Count, 50, "shoulder fill should carry its full command set.");
            Assert.IsNotNull(part.view, "Template camera view should be carried over.");
            Assert.IsFalse(string.IsNullOrEmpty(part.id));

            // every command is re-bound to the LIVE paintable texture (never the stale template ref)
            foreach (var command in part.partCommands)
            {
                Assert.IsNotNull(command.data.PaintableTexture,
                    "Command must be re-bound to the live CwPaintableTexture.");
                Assert.IsNotNull(command.data.LocalCommand, "Command data must deserialize via SerializeReference.");
                Assert.IsFalse(string.IsNullOrEmpty(command.id));
            }

            // stamping the same region again adds a second part with fresh ids
            var secondParts = PartTemplateService.PaintRegion("Arms.twin", "shoulder_front_left", null, partManager);
            yield return null;
            Assert.AreEqual(partsBefore + 2, injuries.groupParts.Count);
            Assert.AreNotEqual(part.id, secondParts[0].id);
            Assert.AreEqual(groupsBefore, partManager.groups.Count, "Still no extra group.");

            // persists through the save pipeline
            DataPersistenceManager.instance.SaveConfig();
            var profiles = DataPersistenceManager.instance.GetAllProfilesGameData();
            var saved = profiles[DataPersistenceManager.instance.selectedProfileId].commandDetails;
            StringAssert.Contains("shoulder_front_left", saved);
            StringAssert.Contains("Injuries", saved);
        }

        [UnityTest]
        public IEnumerator PaintRegion_UnknownRegion_ThrowsWithAvailableNames()
        {
            yield return WaitForModeActive("Main");
            var partManager = Object.FindObjectOfType<PartManager>();

            var ex = Assert.Throws<System.ArgumentException>(
                () => PartTemplateService.PaintRegion("Arms.twin", "no_such_region", null, partManager));
            StringAssert.Contains("shoulder_front_left", ex.Message,
                "Error should list the available region names.");
        }

        [UnityTest]
        public IEnumerator GetTemplateGroupNames_ListsAllArmRegions()
        {
            yield return WaitForModeActive("Main");
            var names = PartTemplateService.GetTemplateGroupNames("Arms.twin");
            Assert.AreEqual(20, names.Count, "Arms.twin should expose 20 regions.");
            CollectionAssert.Contains(names, "wrist_left");
            CollectionAssert.Contains(names, "armpit_right");
        }

        [UnityTest]
        public IEnumerator PaintRegion_WithTool_AppliesToolColorAndMetadata()
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", "ToolTest");
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            var partManager = Object.FindObjectOfType<PartManager>();
            var toolsRoot = GameObject.FindGameObjectsWithTag("Tools")[0];
            EnsureActiveGroup(partManager, "Pain");
            var yellowTool = toolsRoot.transform.Find("Yellow");
            Assert.IsNotNull(yellowTool, "Yellow tool not found in scene.");
            var expectedColor = yellowTool.GetComponent<PaintIn3D.CwPaintSphere>().Color;

            var parts = PartTemplateService.PaintRegion("Arms.twin", "wrist_left", "Yellow", partManager);
            yield return null;
            yield return null;

            var part = parts[0];
            Assert.AreEqual("Yellow", part.nameTool, "Part should carry the chosen tool's name.");
            Assert.AreEqual(expectedColor, part.colorTool, "Part should carry the chosen tool's color.");
            Assert.AreEqual(PartManager.Tool.MarkerLine, part.typeTool, "Yellow is a line marker tool.");
            Assert.IsFalse(string.IsNullOrEmpty(part.meaning), "Part should carry a meaning (tool text or fallback).");

            foreach (var command in part.partCommands)
            {
                var sphere = (PaintIn3D.CwCommandSphere)command.data.LocalCommand;
                Assert.AreEqual(expectedColor, sphere.Color, "Every cloned command must be recolored to the tool color.");
            }

            // sticker tools are not valid for region templates
            Assert.Throws<System.ArgumentException>(
                () => PartTemplateService.PaintRegion("Arms.twin", "wrist_right", "Sticker 1", partManager));

            // unknown tool name
            Assert.Throws<System.ArgumentException>(
                () => PartTemplateService.PaintRegion("Arms.twin", "wrist_right", "NoSuchTool", partManager));
        }

        /// <summary>The manual region-selection feature (RegionManager icon click) paints with
        /// the tool the user currently has selected, falling back to a marker when the active
        /// tool cannot carry sphere-based region templates.</summary>
        [UnityTest]
        public IEnumerator PaintWithCurrentTool_UsesActiveTool_AndFallsBackToMarker()
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", "CurToolTest");
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            var partManager = Object.FindObjectOfType<PartManager>();
            EnsureActiveGroup(partManager, "Treatment");
            var toolsRoot = GameObject.FindGameObjectsWithTag("Tools")[0];

            // (a) no paint tool selected -> marker fallback.
            // Only DEactivating tools here: activating one directly would run ToolTracker.OnEnable
            // outside the UI flow (its myButton is wired by the tool buttons).
            foreach (Transform tool in toolsRoot.transform)
                tool.gameObject.SetActive(false);

            string fallback = PartTemplateService.ResolveCurrentOrDefaultToolName();
            var fallbackTool = toolsRoot.transform.Find(fallback);
            Assert.IsNotNull(fallbackTool, $"Fallback tool '{fallback}' should exist in the Tools container.");
            Assert.IsNotNull(fallbackTool.GetComponent<PaintIn3D.CwPaintSphere>(),
                "Fallback must be a sphere-painting tool (markers/fillers only).");
            Assert.IsNull(fallbackTool.GetComponent<PaintIn3D.CwHitScreenFill>(),
                "Fallback must be a marker, not a filler.");

            var fallbackParts = PartTemplateService.PaintRegionWithCurrentTool("Arms.twin", "elbow_front_left");
            yield return null;
            Assert.AreEqual(fallback, fallbackParts[0].nameTool,
                "Part should carry the fallback marker tool.");

            // (b) tool selected through the real UI -> exactly that tool is used
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Filler/Text Background/Text");
            yield return WaitForModeActive("EditFiller");
            yield return ClickButtonByPath("Canvas/EditFiller UI/Bottom/Scroll/Panel/Cyan");
            AssertGameObjectActive("Tools/Cyan Filling");

            Assert.AreEqual("Cyan Filling", PartTemplateService.ResolveCurrentOrDefaultToolName(),
                "The active filler tool should be used as-is.");

            var fillerParts = PartTemplateService.PaintRegionWithCurrentTool("Arms.twin", "elbow_front_right");
            yield return null;
            Assert.AreEqual("Cyan Filling", fillerParts[0].nameTool);
            var expectedColor = toolsRoot.transform.Find("Cyan Filling").GetComponent<PaintIn3D.CwPaintSphere>().Color;
            Assert.AreEqual(expectedColor, fillerParts[0].colorTool,
                "Part should carry the active tool's color.");
        }

        /// <summary>Regression for the rebind-on-load fix in PartManager.LoadData: loading a
        /// twin whose serialized PaintableTexture instanceIDs are stale (always true for
        /// bundled templates inside the test harness — it allocates different IDs than app
        /// sessions) must re-bind every command to the live texture instead of leaving nulls
        /// that silently break group hide/unhide.</summary>
        [UnityTest]
        public IEnumerator LoadTwin_WithStaleTextureReferences_RebindsOnLoad()
        {
            yield return ResetApp(); // materializes the bundled templates into the (fresh) data dir
            yield return WaitForModeActive("Main");

            var dpm = DataPersistenceManager.instance;
            dpm.ChangeSelectedProfileId("Arms.twin"); // bundled template — its baked app-session IDs are stale here
            yield return null;

            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.AreEqual(20, partManager.groups.Count, "Arms template should load its 20 region groups.");

            int commands = 0;
            foreach (var group in partManager.groups)
                foreach (var part in group.groupParts)
                    foreach (var command in part.partCommands)
                    {
                        Assert.IsNotNull(command.data.PaintableTexture,
                            $"Command in '{group.name}' must be re-bound to the live texture on load.");
                        commands++;
                    }
            Assert.Greater(commands, 500, "Arms template should carry its full command set.");
        }

        [UnityTest]
        public IEnumerator GetTemplateCatalog_ListsAllTwinsAndRegions()
        {
            yield return WaitForModeActive("Main");
            var catalog = PartTemplateService.GetTemplateCatalog();

            Assert.AreEqual(6, catalog.twins.Count, "All six area twins should be present.");
            CollectionAssert.AreEquivalent(
                new[] { "Torso.twin", "Arms.twin", "Legs.twin", "Feet.twin", "Head.twin", "Hands.twin" },
                catalog.twins.Select(t => t.twinName).ToList());

            int totalRegions = catalog.twins.Sum(t => t.regions.Count);
            Assert.AreEqual(98, totalRegions, "Catalog should expose the full 98-region library.");

            var torso = catalog.twins.First(t => t.twinName == "Torso.twin");
            CollectionAssert.Contains(torso.regions, "abdomen_upper_left");
            var hands = catalog.twins.First(t => t.twinName == "Hands.twin");
            CollectionAssert.Contains(hands.regions, "index_finger_right");

            // JSON form usable for LLM prompts
            var json = PartTemplateService.GetTemplateCatalogJson();
            StringAssert.Contains("\"twinName\":\"Arms.twin\"", json);
            StringAssert.Contains("shoulder_front_left", json);
        }
    }
}
