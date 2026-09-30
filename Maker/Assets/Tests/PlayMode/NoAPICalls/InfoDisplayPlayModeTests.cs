using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The status displays of the app: the twin name in the header of every screen and the
    /// Twin / Version / Tool / Group block of the overview overlay. Each of them is fed from a
    /// different place — the config load, the tool activation and the current group — so every
    /// one of them can go stale on its own.
    /// </summary>
    public class InfoDisplayPlayModeTests : TwinPaintTestBase
    {
        // The header is part of the "GUI top" prefab, so every screen has its own copy of it.
        const string MainHeaderTwin = "Canvas/Main UI/Top/GameObject/Save Button/Profile";
        const string HelpHeaderTwin = "Canvas/Help UI/Top/GameObject/Save Button/Profile";

        const string OverviewTwin = "Canvas/Overlays/Overview Overlay/Info/Twin/CurrentProfil";
        const string OverviewVersion = "Canvas/Overlays/Overview Overlay/Info/Version/CurrentVersion";
        const string OverviewTool = "Canvas/Overlays/Overview Overlay/Info/Tool/CurrentTool";
        const string OverviewGroup = "Canvas/Overlays/Overview Overlay/Info/Group/CurrentGroup";

        /// <summary>The twin the app falls back to after a reset: an empty, fresh config.</summary>
        const string DefaultTwin = "default";
        const string DefaultVersion = "000";

        /// <summary>Shown by the overview while nothing is selected.</summary>
        const string Nothing = "-";

        /// <summary>Kept short on purpose: TwinNameValidator rejects names longer than 11
        /// characters, and the app then silently stays on the save screen.</summary>
        const string CreatedTwin = "InfoTwin";

        /// <summary>A reset drops the current twin and loads a fresh "default.000", so every
        /// display of the twin name and version has to follow.</summary>
        [Category(Processes.AppFrame)]
        [UnityTest]
        public IEnumerator Reset_ShowsTheTwinLoadedAfterTheReset()
        {
            yield return LoadLipEdemaTwin();
            AssertDisplay(MainHeaderTwin, "LipEdema", "Setup: the header should show the loaded twin.");

            yield return ResetApp();

            Assert.AreEqual($"{DefaultTwin}.{DefaultVersion}", SelectedProfileId,
                "A reset should leave the fresh default twin loaded.");
            AssertDisplay(MainHeaderTwin, DefaultTwin, "The header of the main screen is stale after a reset.");
            AssertDisplay(HelpHeaderTwin, DefaultTwin, "The header of the help screen is stale after a reset.");
            AssertDisplay(OverviewTwin, DefaultTwin, "The overview shows the wrong twin after a reset.");
            AssertDisplay(OverviewVersion, DefaultVersion, "The overview shows the wrong version after a reset.");
            AssertDisplay(OverviewGroup, Nothing, "The default twin has no groups, so none can be current.");
        }

        /// <summary>Loading another twin updates the name in the header and the name and version
        /// in the overview.</summary>
        [Category(Processes.ManageTwins)]
        [UnityTest]
        public IEnumerator SelectTwin_UpdatesNameAndVersion()
        {
            yield return ResetApp();
            AssertDisplay(MainHeaderTwin, DefaultTwin, "Setup: the reset should leave the default twin loaded.");

            yield return SelectTwin("LipEdema");

            AssertDisplay(MainHeaderTwin, "LipEdema");
            AssertDisplay(HelpHeaderTwin, "LipEdema");
            AssertDisplay(OverviewTwin, "LipEdema");
            AssertDisplay(OverviewVersion, VersionOfSelectedProfile);
        }

        /// <summary>A newly created twin is loaded right away, so the displays have to show it
        /// with its initial version.</summary>
        [Category(Processes.ManageTwins)]
        [UnityTest]
        public IEnumerator CreateTwin_UpdatesNameAndVersion()
        {
            yield return LoadLipEdemaTwin();

            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", CreatedTwin);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            AssertDisplay(MainHeaderTwin, CreatedTwin);
            AssertDisplay(HelpHeaderTwin, CreatedTwin);
            AssertDisplay(OverviewTwin, CreatedTwin);
            AssertDisplay(OverviewVersion, DefaultVersion, "A new twin starts at version 000.");
        }

        /// <summary>Selecting a tool shows its name in the overview, switching to another one
        /// replaces it.</summary>
        [Category(Processes.MarkUpTheBody)]
        [UnityTest]
        public IEnumerator SelectTool_ShowsTheCurrentTool()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            yield return SelectMarker("Red");
            AssertDisplay(OverviewTool, MarkerLabel("Red"));
            yield return CloseMarkerList();

            yield return SelectMarker("Green");
            AssertDisplay(OverviewTool, MarkerLabel("Green"));
        }

        /// <summary>Selecting a group shows its name in the overview, and it keeps up when
        /// another group becomes the current one.</summary>
        [Category(Processes.OrganiseIntoGroups)]
        [UnityTest]
        public IEnumerator SelectGroup_ShowsTheCurrentGroup()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            PartManager.GroupData first = partManager.groups[0];
            PartManager.GroupData second = partManager.groups[1];

            yield return SelectGroupForPainting(first);
            yield return null;
            AssertDisplay(OverviewGroup, first.name);

            yield return SelectGroupForPainting(second);
            yield return null;
            AssertDisplay(OverviewGroup, second.name);
        }

        /// <summary>Opens the marker list from Edit mode and picks a marker, without painting
        /// with it. Stays on the marker screen — see <see cref="CloseMarkerList"/>.</summary>
        IEnumerator SelectMarker(string marker)
        {
            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            yield return WaitForModeActive("EditMarker");
            yield return ClickButtonByPath($"Canvas/EditMarker UI/Bottom/Scroll/Panel/{marker}");
            AssertGameObjectActive($"Tools/{marker}");
        }

        /// <summary>Leaves the marker screen and returns to Edit mode.</summary>
        IEnumerator CloseMarkerList()
        {
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            yield return WaitForModeActive("Edit");
        }

        /// <summary>The label the marker carries in the marker list — that is what the overview
        /// is expected to show, and it is not necessarily the GameObject name.</summary>
        string MarkerLabel(string marker)
        {
            GameObject entry = FindGameObjectByPath($"Canvas/EditMarker UI/Bottom/Scroll/Panel/{marker}");
            InputField label = entry.GetComponentsInChildren<InputField>(true).FirstOrDefault();
            Assert.IsNotNull(label, $"Marker '{marker}' has no label in the marker list.");
            Assert.IsNotEmpty(label.text, $"Marker '{marker}' has an empty label.");
            return label.text;
        }

        static string SelectedProfileId => DataPersistenceManager.instance.selectedProfileId;

        static string VersionOfSelectedProfile => SelectedProfileId.Split('.').Last();

        void AssertDisplay(string path, string expected, string message = null)
        {
            string actual = DisplayText(path);
            Assert.AreEqual(expected, actual,
                message ?? $"Display at '{path}' shows '{actual}' instead of '{expected}'.");
        }

        /// <summary>Reads a label regardless of whether it is a UI Text (header, twin, version)
        /// or a TextMeshPro one (tool, group).</summary>
        string DisplayText(string path)
        {
            GameObject display = FindGameObjectByPath(path);
            var text = display.GetComponent<Text>();
            if (text != null)
            {
                return text.text;
            }
            var textMeshPro = display.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(textMeshPro, $"No Text or TextMeshProUGUI on the display at '{path}'.");
            return textMeshPro.text;
        }
    }
}
