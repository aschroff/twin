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
    /// Paints one part into every group of the LipEdema twin — each with a different marker —
    /// plus one into a newly created group, then checks on the group detail page that selecting
    /// a single group lists exactly that group's one part.
    /// </summary>
    public class GroupDetailPlayModeTests : PlayModeTestBase
    {
        private const string GroupOverlayPanel = "Canvas/Overlays/Group Overlay/Scroll/Panel";
        private const string DetailGroupPanel = "Canvas/GroupDetailUI/Scroll/Panel";
        private const string DetailPartPanel = "Canvas/GroupDetailUI/ScrollDetails/Panel";
        private const string NewGroupName = "TestGroup";

        /// <summary>One marker per group — switching the tool is what makes the app start a new
        /// part, so every group ends up with exactly one.</summary>
        private static readonly string[] Markers = { "Red", "Green", "Blue", "Cyan", "Pink" };

        [UnityTest]
        public IEnumerator PaintingPerGroup_ShowsOnePartPerSelectedGroup()
        {
            yield return ResetApp();

            // load the LipEdema twin (ships with empty groups: Pain, Injuries, Treatment, Swell)
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
            Assert.IsNotNull(twinEntry, "LipEdema twin not found in the save list.");
            yield return ClickButtonByPath(path: "Unselect", root: twinEntry);

            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.IsNotNull(partManager, "PartManager not found.");
            var groups = new List<PartManager.GroupData>(partManager.groups);
            Assert.Greater(groups.Count, 0, "LipEdema should ship with groups.");
            Assert.LessOrEqual(groups.Count, Markers.Length - 1,
                "More groups than markers prepared for this test.");
            foreach (var group in groups)
            {
                Assert.AreEqual(0, group.groupParts.Count,
                    $"Group '{group.name}' is expected to start empty.");
            }

            // one part per existing group, each with its own marker
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            // the twin's saved camera frames the lower body, where the screen centre falls
            // between the legs — pick a view that puts the body under the paint position
            yield return SelectView("Upper body");
            for (int i = 0; i < groups.Count; i++)
            {
                yield return SelectGroupForPainting(groups[i]);
                yield return PaintWithMarker(Markers[i]);
                Assert.AreEqual(1, groups[i].groupParts.Count,
                    $"Group '{groups[i].name}' should hold exactly the one painted part.");
            }

            // a new group, painted with yet another marker
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            var newGroup = CreateGroup(partManager, NewGroupName);
            yield return null;
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectGroupForPainting(newGroup);
            yield return PaintWithMarker(Markers[groups.Count]);
            Assert.AreEqual(1, newGroup.groupParts.Count,
                $"New group '{NewGroupName}' should hold exactly the one painted part.");
            groups.Add(newGroup);

            // group detail page: select one group at a time and count the listed parts
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            yield return ClickButtonByPath("Canvas/Main UI/Bottom/GroupDetail/Icon");
            yield return WaitForModeActive("GroupDetail");

            foreach (var group in groups)
            {
                yield return SelectOnlyGroupInDetailList(group);
                Assert.AreEqual(1, CountListedParts(),
                    $"With only '{group.name}' selected, its single part should be listed.");
            }
        }

        /// <summary>Selects a stored view, which frames the body for painting.</summary>
        private IEnumerator SelectView(string viewName)
        {
            var viewEntry = FindChildWithTextValue("Canvas/Overlays/View Overlay/Scroll/Panel",
                viewName, "ReadOnlyMode/Text Background/ViewName");
            Assert.IsNotNull(viewEntry, $"View '{viewName}' not found in the view overlay.");
            yield return ClickButtonByPath(path: "ReadOnlyMode/Icon", root: viewEntry);
        }

        /// <summary>Makes the group the current one, so new paint goes into it — the same handler
        /// the group overlay entry invokes.</summary>
        private IEnumerator SelectGroupForPainting(PartManager.GroupData group)
        {
            var panel = FindGameObjectByPath(GroupOverlayPanel);
            Group entry = panel.GetComponentsInChildren<Group>(true)
                .FirstOrDefault(g => g.groupdata == group);
            Assert.IsNotNull(entry, $"No group overlay entry for '{group.name}'.");
            entry.HandleEdit();
            yield return null;

            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.AreSame(group, partManager.currentGroup,
                $"Group '{group.name}' should be the current group.");
        }

        private IEnumerator PaintWithMarker(string marker)
        {
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            yield return WaitForModeActive("EditMarker");
            yield return ClickButtonByPath($"Canvas/EditMarker UI/Bottom/Scroll/Panel/{marker}");
            AssertGameObjectActive($"Tools/{marker}");

            yield return DragOnCanvas("Canvas", new Vector2(20, 0));

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
        }

        /// <summary>Creates a group the way the group list does for a newly named entry.</summary>
        private static PartManager.GroupData CreateGroup(PartManager partManager, string name)
        {
            PartManager.GroupData group = partManager.StartNewGroup(null);
            group.id = System.Guid.NewGuid().ToString();
            group.name = name;
            group.visible = true;
            var groupManager = Object.FindObjectOfType<GroupManager>(true);
            Assert.IsNotNull(groupManager, "GroupManager not found.");
            groupManager.rebuild();
            return group;
        }

        /// <summary>Ticks the given group's selector in the detail list and unticks all others,
        /// which rebuilds the part list from the selected groups.</summary>
        private IEnumerator SelectOnlyGroupInDetailList(PartManager.GroupData group)
        {
            var panel = FindGameObjectByPath(DetailGroupPanel);
            GroupSelect[] entries = panel.GetComponentsInChildren<GroupSelect>(true);
            Assert.Greater(entries.Length, 0, "Group detail page lists no groups.");
            bool found = false;
            foreach (GroupSelect entry in entries)
            {
                Toggle toggle = entry.transform.Find("Selector").GetComponent<Toggle>();
                bool select = entry.groupdata == group;
                found |= select;
                toggle.isOn = select;
            }
            Assert.IsTrue(found, $"Group '{group.name}' is not listed on the group detail page.");

            // the part list is rebuilt by the toggle handler; destroyed entries go away next frame
            yield return null;
            yield return null;
        }

        private int CountListedParts()
        {
            var panel = FindGameObjectByPath(DetailPartPanel);
            return panel.GetComponentsInChildren<PartEntry>(true).Length;
        }
    }
}
