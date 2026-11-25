using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Chain K02, second half — add to a twin that already holds something.
    /// </summary>
    /// <remarks>
    /// <para>One test, on purpose. That a group can be added (<c>GroupPlayModeTests</c>), that a
    /// tool paints (<c>UndoRedoPlayModeTests</c>, <c>ProgrammaticPaintingTests</c>) and that a
    /// blank twin takes its first part (<c>EditUiPlayModeTests</c>, chain K01) are checked
    /// elsewhere. What only this chain can ask is what happens to a twin that is <b>already
    /// full</b> when something is added to it: the Torso sample brings 21 groups with a part
    /// each, and none of them may change.</para>
    /// </remarks>
    [Category(Chains.OpenAndAddToATwin)]
    public class AddToOpenTwinPlayModeTests : TwinPaintTestBase
    {
        const string AddedGroup = "AddedGroup";

        [UnityTest]
        public IEnumerator AddingAGroupAndAPartToTorso_LeavesWhatItShippedUntouched()
        {
            yield return ResetApp();
            yield return SelectTwin("Torso");
            yield return new WaitForSeconds(0.5f);

            PartManager partManager = FindPartManager();
            Dictionary<string, string> shipped = PartIdsByGroup(partManager);
            int shippedGroups = partManager.groups.Count;
            Assert.Greater(shippedGroups, 0, "Setup: Torso should come with groups.");
            Color32[] before = BodyPixels();

            // add a group to the open twin, then paint a part into it
            yield return ClickButtonByPath("Canvas/Overlays/Group Overlay/Edit");
            yield return WaitForModeActive("GroupList");
            GroupEdit emptyEntry = FindGameObjectByPath(GroupListPanel).GetComponentsInChildren<GroupEdit>(true)
                .FirstOrDefault(e => e.persistent == false);
            Assert.IsNotNull(emptyEntry, "The group list should offer an empty entry for a new group.");
            emptyEntry.GetComponentInChildren<InputField>(true).text = AddedGroup;
            yield return null;
            emptyEntry.HandleEdit();
            yield return null;
            Assert.AreEqual(shippedGroups + 1, partManager.groups.Count, "The group was not added.");
            PartManager.GroupData added = partManager.groups.First(g => g.name == AddedGroup);
            Controller.EnableMode("Main");
            yield return WaitForModeActive("Main");

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            // Torso has no stored view, so frame the camera on the body directly
            Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include).select(new SceneManagement.View
            {
                yaw = 0f, pitch = 0f, sizeCamera = 0.9f,
                positionCamera_x = 0f, positionCamera_y = 1.0f, positionCamera_z = 1f,
            });
            yield return null;
            yield return SelectGroupForPainting(added);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            AssertTorsoIsIntactPlusTheAddedPart(partManager, shipped, shippedGroups, before, "after adding");

            // leave the twin and come back: the addition is on disk, the rest still as shipped
            string addedPartId = added.groupParts[0].id;
            yield return SelectTwin("default");
            yield return SelectTwin("Torso");
            yield return new WaitForSeconds(0.5f);
            partManager = FindPartManager();
            AssertTorsoIsIntactPlusTheAddedPart(partManager, shipped, shippedGroups, before, "after reopening");
            Assert.AreEqual(addedPartId, partManager.groups.First(g => g.name == AddedGroup).groupParts[0].id,
                "The added part should be the same one after reopening.");
            AssertPartsAreUsable(partManager);
        }

        static Dictionary<string, string> PartIdsByGroup(PartManager partManager)
        {
            return partManager.groups.ToDictionary(g => g.name, g => string.Join(",", g.groupParts.Select(p => p.id)));
        }

        static void AssertTorsoIsIntactPlusTheAddedPart(PartManager partManager, Dictionary<string, string> shipped,
            int shippedGroups, Color32[] before, string when)
        {
            Assert.AreEqual(shippedGroups + 1, partManager.groups.Count, $"Groups lost or doubled {when}.");
            PartManager.GroupData added = partManager.groups.FirstOrDefault(g => g.name == AddedGroup);
            Assert.IsNotNull(added, $"The added group is missing {when}.");
            Assert.AreEqual(1, added.groupParts.Count, $"The added group should hold the one part painted into it ({when}).");

            foreach (KeyValuePair<string, string> group in shipped)
            {
                PartManager.GroupData found = partManager.groups.FirstOrDefault(g => g.name == group.Key);
                Assert.IsNotNull(found, $"Group '{group.Key}' is missing {when}.");
                Assert.AreEqual(group.Value, string.Join(",", found.groupParts.Select(p => p.id)),
                    $"The parts of '{group.Key}' changed {when}.");
            }
            Assert.Greater(DifferingPixels(before, BodyPixels()), 0, $"The added part shows nothing on the body ({when}).");
        }
    }
}
