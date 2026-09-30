using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>Takes a picture of the busy panel, because no assertion tells you whether it looks
    /// right. Explicit: it is for looking at, not for the suite.</summary>
    [Category(Processes.AppFrame)]
    [Explicit("Produces a screenshot for a human to look at.")]
    public class BusyOverlayLookTests : TwinPaintTestBase
    {
        [UnityTest]
        public IEnumerator ShowMeTheBusyPanel()
        {
            yield return LoadLipEdemaTwin();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            string folder = Path.Combine(Application.dataPath, "..", "Temp");

            // without the panel first, so the dimming can actually be judged
            string before = Path.Combine(folder, "busy-overlay-before.png");
            ScreenCapture.CaptureScreenshot(before);
            for (int frame = 0; frame < 12; frame++) yield return null;

            BusyOverlay.Show(StringLocalizer.localizeString("LOADING_TWIN"));
            yield return null;
            yield return null;

            string after = Path.Combine(folder, "busy-overlay.png");
            ScreenCapture.CaptureScreenshot(after);
            for (int frame = 0; frame < 12; frame++) yield return null;

            BusyOverlay.Hide();
            Debug.Log("[busy look] before=" + File.Exists(before) + " after=" + File.Exists(after));
        }
    }
}
