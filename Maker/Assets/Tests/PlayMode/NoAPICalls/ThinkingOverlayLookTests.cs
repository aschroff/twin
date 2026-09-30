using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>Takes a picture of the logo the app shows while it is asking the model, because
    /// no assertion says whether it looks right. Explicit: for looking at, not for the suite.</summary>
    [Category(Processes.AppFrame)]
    [Explicit("Produces a screenshot for a human to look at.")]
    public class ThinkingOverlayLookTests : TwinPaintTestBase
    {
        [UnityTest]
        public IEnumerator ShowMeTheThinkingLogo()
        {
            yield return LoadLipEdemaTwin();
            for (int frame = 0; frame < 4; frame++) yield return null;

            string folder = Path.Combine(Application.dataPath, "..", "Temp");

            BusyOverlay.BeginThinking();
            yield return null;
            yield return null;

            string shot = Path.Combine(folder, "thinking-logo.png");
            ScreenCapture.CaptureScreenshot(shot);
            for (int frame = 0; frame < 12; frame++) yield return null;

            BusyOverlay.EndThinking();
            Debug.Log("[thinking look] " + Screen.width + "x" + Screen.height + " -> " + shot);
        }
    }
}
