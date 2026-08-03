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
        [UnityTest]
        public IEnumerator PaintTemplateGroup_StampsRegionOntoCurrentTwin()
        {
            // fresh twin so assertions start from a clean data model
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", "ServiceTest");
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.IsNotNull(partManager, "PartManager not found.");
            int groupsBefore = partManager.groups?.Count ?? 0;

            var group = PartTemplateService.PaintTemplateGroup("Arms.twin", "shoulder_front_left", partManager);
            yield return null;
            yield return null; // let CwPaintableManager flush the replayed commands

            // group inserted and named after the region
            Assert.AreEqual(groupsBefore + 1, partManager.groups.Count, "Exactly one group should be added.");
            Assert.AreEqual("shoulder_front_left", group.name);
            Assert.IsTrue(group.visible);
            Assert.IsFalse(string.IsNullOrEmpty(group.id));

            // part carries the template's tool metadata, commands and a stored view
            Assert.AreEqual(1, group.groupParts.Count, "Region templates hold exactly one part.");
            var part = group.groupParts[0];
            Assert.AreEqual("shoulder_front_left", part.description);
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

            // ids are fresh — no collisions with a second stamp of the same region
            var secondGroup = PartTemplateService.PaintTemplateGroup("Arms.twin", "shoulder_front_left", partManager);
            yield return null;
            Assert.AreNotEqual(group.id, secondGroup.id);
            Assert.AreNotEqual(group.groupParts[0].id, secondGroup.groupParts[0].id);

            // persists through the save pipeline
            DataPersistenceManager.instance.SaveConfig();
            var profiles = DataPersistenceManager.instance.GetAllProfilesGameData();
            var saved = profiles[DataPersistenceManager.instance.selectedProfileId].commandDetails;
            StringAssert.Contains("shoulder_front_left", saved);
        }

        [UnityTest]
        public IEnumerator PaintTemplateGroup_UnknownRegion_ThrowsWithAvailableNames()
        {
            yield return WaitForModeActive("Main");
            var partManager = Object.FindObjectOfType<PartManager>();

            var ex = Assert.Throws<System.ArgumentException>(
                () => PartTemplateService.PaintTemplateGroup("Arms.twin", "no_such_region", partManager));
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
        public IEnumerator PaintTemplateGroup_WithTool_AppliesToolColorAndMetadata()
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", "ToolTest");
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            var partManager = Object.FindObjectOfType<PartManager>();
            var toolsRoot = GameObject.FindGameObjectsWithTag("Tools")[0];
            var yellowTool = toolsRoot.transform.Find("Yellow");
            Assert.IsNotNull(yellowTool, "Yellow tool not found in scene.");
            var expectedColor = yellowTool.GetComponent<PaintIn3D.CwPaintSphere>().Color;

            var group = PartTemplateService.PaintTemplateGroup("Arms.twin", "wrist_left", "Yellow", partManager);
            yield return null;
            yield return null;

            var part = group.groupParts[0];
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
                () => PartTemplateService.PaintTemplateGroup("Arms.twin", "wrist_right", "Sticker 1", partManager));

            // unknown tool name
            Assert.Throws<System.ArgumentException>(
                () => PartTemplateService.PaintTemplateGroup("Arms.twin", "wrist_right", "NoSuchTool", partManager));
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
