using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>Takes a picture of the group detail panel with its three buttons, because no
    /// assertion says whether three stacked rows of icon-plus-label actually fit. Explicit: it is
    /// for looking at, not for the suite.</summary>
    [Category(Processes.DescribeAndReport)]
    [Explicit("Produces a screenshot for a human to look at.")]
    public class GroupDetailButtonsLookTests : TwinPaintTestBase
    {
        [UnityTest]
        public IEnumerator ShowMeTheGroupDetailButtons()
        {
            yield return LoadLipEdemaTwin();

            // a painted part, so the list underneath is not empty and the counts are not all zero
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            InteractionController.Groupdata = FindPartManager().groups[0];
            InteractionController.EnableMode("GroupDetail");
            yield return WaitForModeActive("GroupDetail");
            for (int frame = 0; frame < 6; frame++) yield return null;

            string shot = Path.Combine(Application.dataPath, "..", "Temp", "group-detail-buttons.png");
            ScreenCapture.CaptureScreenshot(shot);
            for (int frame = 0; frame < 12; frame++) yield return null;

            Debug.Log("[group detail look] screen " + Screen.width + "x" + Screen.height
                      + " written=" + File.Exists(shot) + " -> " + shot);
        }
    }
}
