using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Essential group behaviour: showing/hiding groups, the group list dialog (part counts,
    /// adding and deleting groups) and that groups, their parts and the links between them
    /// survive a save/load round trip.
    /// </summary>
    public class GroupPlayModeTests : TwinPaintTestBase
    {
        /// <summary>Hiding and showing a group replays the visible groups onto the body. The parts
        /// of a hidden group must survive that untouched and stay paintable afterwards.</summary>
        [UnityTest]
        public IEnumerator HideAndShowGroup_KeepsItsParts()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var group = partManager.groups[0];

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(group);
            yield return PaintWithMarker("Red");
            Assert.AreEqual(1, group.groupParts.Count, "Setup: the group should hold one part.");
            string partId = group.groupParts[0].id;

            yield return SetGroupVisible(group, false);
            Assert.AreEqual(1, group.groupParts.Count, "Hiding a group must not remove its parts.");
            Assert.AreEqual(partId, group.groupParts[0].id);
            AssertPartsAreUsable(partManager);

            yield return SetGroupVisible(group, true);
            Assert.AreEqual(1, group.groupParts.Count, "Showing a group must not remove its parts.");
            Assert.AreEqual(partId, group.groupParts[0].id);
            AssertPartsAreUsable(partManager);

            // painting still works after the hide/show cycle
            yield return PaintWithMarker("Green");
            Assert.AreEqual(2, group.groupParts.Count,
                "After showing the group again, painting should add another part.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>The group list dialog shows the part count per group and can add and delete
        /// groups. Deletes a group that is NOT the current one — deleting the current group has
        /// its own (unfinished) behaviour.</summary>
        [UnityTest]
        public IEnumerator GroupList_ShowsPartCounts_AddsAndDeletesGroups()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var painted = partManager.groups[0];
            var toDelete = partManager.groups[1];
            int groupsBefore = partManager.groups.Count;

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(painted);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            // open the group list (pencil on the group overlay)
            yield return ClickButtonByPath("Canvas/Overlays/Group Overlay/Edit");
            yield return WaitForModeActive("GroupList");

            // part counts per group
            foreach (PartManager.GroupData group in partManager.groups)
            {
                GroupEdit entry = FindGroupListEntry(group);
                string details = entry.transform.Find("Details").GetComponent<Text>().text;
                Assert.AreEqual($"{group.groupParts.Count} parts", details,
                    $"Wrong part count shown for group '{group.name}'.");
            }

            // add a group: name the empty entry at the end of the list
            const string newName = "AddedGroup";
            GroupEdit emptyEntry = FindGroupListEntries().FirstOrDefault(e => e.persistent == false);
            Assert.IsNotNull(emptyEntry, "The group list should offer an empty entry for a new group.");
            InputField input = emptyEntry.GetComponentInChildren<InputField>(true);
            Assert.IsNotNull(input, "The empty group entry has no input field.");
            input.text = newName;
            yield return null;
            emptyEntry.HandleEdit();
            yield return null;

            Assert.AreEqual(groupsBefore + 1, partManager.groups.Count, "The group was not added.");
            var added = partManager.groups.FirstOrDefault(g => g.name == newName);
            Assert.IsNotNull(added, $"No group named '{newName}' was created.");
            Assert.AreSame(added, partManager.currentGroup, "A new group becomes the current group.");
            Assert.IsNotNull(FindGroupListEntries().FirstOrDefault(e => e.persistent == false),
                "The list should offer a fresh empty entry after adding a group.");

            // delete a group that is not the current one
            string deletedName = toDelete.name;
            GroupEdit deleteEntry = FindGroupListEntry(toDelete);
            yield return ClickButtonByPath(path: "Delete", root: deleteEntry.gameObject);
            yield return null;

            Assert.AreEqual(groupsBefore, partManager.groups.Count, "The group was not deleted.");
            Assert.IsFalse(partManager.groups.Any(g => g.name == deletedName),
                $"Group '{deletedName}' should be gone.");
            Assert.IsTrue(partManager.groups.Any(g => g.name == newName), "The added group should remain.");
            Assert.AreEqual(1, painted.groupParts.Count, "The painted group should keep its part.");
        }

        /// <summary>Groups, their parts, the part↔group links and the visibility and selection
        /// state must survive saving and loading the twin.</summary>
        [UnityTest]
        public IEnumerator SaveAndReload_KeepsGroupsPartsAndTheirLinks()
        {
            yield return LoadLipEdemaTwin();
            var partManager = FindPartManager();
            var firstGroup = partManager.groups[0];
            var secondGroup = partManager.groups[1];

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(firstGroup);
            yield return PaintWithMarker("Red");
            yield return SelectGroupForPainting(secondGroup);
            yield return PaintWithMarker("Green");

            // hide one group and leave the other one selected
            yield return SetGroupVisible(secondGroup, false);
            yield return SelectGroupForPainting(firstGroup);

            string firstName = firstGroup.name;
            string secondName = secondGroup.name;
            string firstPartId = firstGroup.groupParts[0].id;
            string secondPartId = secondGroup.groupParts[0].id;
            int groupCount = partManager.groups.Count;

            // leave the twin and come back (switching twins saves the current one)
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");

            partManager = FindPartManager();
            Assert.AreEqual(groupCount, partManager.groups.Count, "Groups were lost.");

            var reloadedFirst = partManager.groups.FirstOrDefault(g => g.name == firstName);
            var reloadedSecond = partManager.groups.FirstOrDefault(g => g.name == secondName);
            Assert.IsNotNull(reloadedFirst, $"Group '{firstName}' is missing after reload.");
            Assert.IsNotNull(reloadedSecond, $"Group '{secondName}' is missing after reload.");

            Assert.AreEqual(1, reloadedFirst.groupParts.Count, $"'{firstName}' lost its part.");
            Assert.AreEqual(1, reloadedSecond.groupParts.Count, $"'{secondName}' lost its part.");
            Assert.AreEqual(firstPartId, reloadedFirst.groupParts[0].id, "Part landed in the wrong group.");
            Assert.AreEqual(secondPartId, reloadedSecond.groupParts[0].id, "Part landed in the wrong group.");

            Assert.IsTrue(reloadedFirst.visible, $"'{firstName}' should still be visible.");
            Assert.IsFalse(reloadedSecond.visible, $"'{secondName}' should still be hidden.");
            Assert.AreSame(reloadedFirst, partManager.currentGroup,
                "The selected group should be current again after loading.");

            AssertPartsAreUsable(partManager);
        }

        private GroupEdit[] FindGroupListEntries()
        {
            var panel = FindGameObjectByPath(GroupListPanel);
            return panel.GetComponentsInChildren<GroupEdit>(true);
        }

        private GroupEdit FindGroupListEntry(PartManager.GroupData group)
        {
            GroupEdit entry = FindGroupListEntries().FirstOrDefault(e => e.groupdata == group);
            Assert.IsNotNull(entry, $"Group '{group.name}' is not listed in the group list.");
            return entry;
        }
    }
}
