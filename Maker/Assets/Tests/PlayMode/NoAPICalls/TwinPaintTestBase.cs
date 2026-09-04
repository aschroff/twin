using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Shared app flows for tests that paint into a twin: loading the LipEdema sample, framing
    /// the body, choosing the group new paint goes into, and painting with a marker.
    /// </summary>
    public abstract class TwinPaintTestBase : PlayModeTestBase
    {
        protected const string GroupOverlayPanel = "Canvas/Overlays/Group Overlay/Scroll/Panel";
        protected const string GroupListPanel = "Canvas/GroupListUI/Scroll/Panel";
        protected const string SaveTwinPanel = "Canvas/Save UI/Bottom/Scroll/Panel";

        /// <summary>A view that frames the torso. The twin's own saved camera shows the lower
        /// body, where the paint position (screen centre) falls between the legs and hits
        /// nothing — so a view has to be selected before painting.</summary>
        protected const string BodyView = "Upper body";

        protected static PartManager FindPartManager()
        {
            var partManager = Object.FindObjectOfType<PartManager>();
            Assert.IsNotNull(partManager, "PartManager not found.");
            return partManager;
        }

        /// <summary>Resets the app and loads the LipEdema sample twin (which ships with the
        /// groups Pain, Injuries, Treatment and Swell, all empty).</summary>
        protected IEnumerator LoadLipEdemaTwin()
        {
            yield return ResetApp();
            yield return SelectTwin("LipEdema");
        }

        /// <summary>Selects a twin in the twin list.</summary>
        protected IEnumerator SelectTwin(string twinName)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            var twinEntry = FindChildWithTextValue(SaveTwinPanel, twinName);
            Assert.IsNotNull(twinEntry, $"Twin '{twinName}' not found in the twin list.");
            yield return ClickButtonByPath(path: "Unselect", root: twinEntry);
        }

        /// <summary>Selects a stored view, which frames the body for painting.</summary>
        protected IEnumerator SelectView(string viewName)
        {
            var viewEntry = FindChildWithTextValue("Canvas/Overlays/View Overlay/Scroll/Panel",
                viewName, "ReadOnlyMode/Text Background/ViewName");
            Assert.IsNotNull(viewEntry, $"View '{viewName}' not found in the view overlay.");
            yield return ClickButtonByPath(path: "ReadOnlyMode/Icon", root: viewEntry);
        }

        /// <summary>Makes the group the current one, so new paint goes into it — the same handler
        /// the group overlay entry invokes.</summary>
        protected IEnumerator SelectGroupForPainting(PartManager.GroupData group)
        {
            Group entry = FindOverlayEntry(group);
            entry.HandleEdit();
            yield return null;
            Assert.AreSame(group, FindPartManager().currentGroup,
                $"Group '{group.name}' should be the current group.");
        }

        /// <summary>Shows or hides a group through its overlay toggle, which replays the visible
        /// groups onto the body.</summary>
        protected IEnumerator SetGroupVisible(PartManager.GroupData group, bool visible)
        {
            Group entry = FindOverlayEntry(group);
            Toggle toggle = entry.GetComponentInChildren<Toggle>(true);
            Assert.IsNotNull(toggle, $"No visibility toggle on the overlay entry for '{group.name}'.");
            toggle.isOn = visible;
            yield return null;
            yield return null;
            Assert.AreEqual(visible, group.visible,
                $"Group '{group.name}' should be {(visible ? "visible" : "hidden")}.");
        }

        /// <summary>Paints one part with the given marker. Must be called from Edit mode; returns
        /// to Edit mode. NOTE: a different marker per part is what makes the app start a new part
        /// — a group change currently does not (see the group/part bug ticket).</summary>
        protected IEnumerator PaintWithMarker(string marker)
        {
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            yield return WaitForModeActive("EditMarker");
            yield return ClickButtonByPath($"Canvas/EditMarker UI/Bottom/Scroll/Panel/{marker}");
            AssertGameObjectActive($"Tools/{marker}");

            yield return DragOnCanvas("Canvas", new Vector2(20, 0));

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
        }

        protected Group FindOverlayEntry(PartManager.GroupData group)
        {
            var panel = FindGameObjectByPath(GroupOverlayPanel);
            Group entry = panel.GetComponentsInChildren<Group>(true)
                .FirstOrDefault(g => g.groupdata == group);
            Assert.IsNotNull(entry, $"No group overlay entry for '{group.name}'.");
            return entry;
        }

        /// <summary>The InputField of a tool's row under the EditMarker / EditFiller panels —
        /// where the meaning of a tool lives (Item → ConfigData.itemTexts).</summary>
        protected static InputField FindToolMeaningField(string toolName)
        {
            foreach (Code.AI.PromptGeneration.Tools panel in Object.FindObjectsOfType<Code.AI.PromptGeneration.Tools>(true))
            {
                foreach (Transform row in panel.transform)
                {
                    var button = row.GetComponent<CW.Common.CwDemoButton>();
                    if (button == null || button.IsolateTarget == null) continue;
                    if (button.IsolateTarget.gameObject.name != toolName) continue;

                    var input = row.GetComponentInChildren<InputField>(true);
                    Assert.IsNotNull(input, $"Tool row of '{toolName}' has no input field.");
                    return input;
                }
            }
            Assert.Fail($"No tool row for '{toolName}'.");
            return null;
        }

        /// <summary>Clears the meaning of one tool row and returns the meaning it had. The
        /// LipEdema twin ships a meaning for every marker and filler, so a test that needs a
        /// free tool has to make one.</summary>
        protected static string FreeOneTool(string toolName)
        {
            InputField input = FindToolMeaningField(toolName);
            string had = input.text;
            Assert.IsNotEmpty(had, $"'{toolName}' was expected to carry a meaning before being freed.");
            input.text = "";
            return had;
        }

        /// <summary>Asserts that every part is linked to its group and that its commands are
        /// bound to the paintable texture — without that binding the paint is silently dropped
        /// on the next replay.</summary>
        protected static void AssertPartsAreUsable(PartManager partManager)
        {
            foreach (PartManager.GroupData group in partManager.groups)
            {
                foreach (PartManager.PartData part in group.groupParts)
                {
                    Assert.AreSame(group, part.group,
                        $"Part {part.id} in '{group.name}' is not linked to its group.");
                    Assert.Greater(part.partCommands.Count, 0, $"Part {part.id} has no commands.");
                    foreach (PartManager.CommandDataTwin command in part.partCommands)
                    {
                        Assert.IsNotNull(command.data.PaintableTexture,
                            $"A command of part {part.id} is not bound to the paintable texture.");
                    }
                }
            }
        }
    }
}
