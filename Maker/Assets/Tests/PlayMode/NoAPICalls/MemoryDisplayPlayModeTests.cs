using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// The diagnostic memory panel on the settings page.
    /// </summary>
    /// <remarks>
    /// <para>This is a temporary build aid, but it goes onto a user's device and we get one
    /// deployment to get it right, so it is worth a test: the settings page must still open, and
    /// the panel must show real figures rather than an empty box.</para>
    /// </remarks>
    [Category(Processes.AppFrame)]
    public class MemoryDisplayPlayModeTests : TwinPaintTestBase
    {
        private const string DisplayPath = "Canvas/Settings UI/MemoryDisplay (diagnostic)/Text";

        private IEnumerator OpenSettings()
        {
            yield return ClickButtonByName("Settings Button");
            yield return WaitForModeActive("Settings");
        }

        [UnityTest]
        public IEnumerator SettingsPage_ShowsTheMemoryPanel()
        {
            yield return ResetApp();
            yield return OpenSettings();

            GameObject text = FindGameObjectByPath(DisplayPath);
            Assert.IsNotNull(text, "The memory panel is not on the settings page.");

            string shown = text.GetComponent<TextMeshProUGUI>().text;
            Debug.Log("[memory panel]\n" + shown);

            Assert.IsNotEmpty(shown, "The memory panel is empty.");
            StringAssert.Contains("peak", shown);
            StringAssert.Contains("graphics", shown);
            StringAssert.Contains("WHAT GREW SINCE THE APP STARTED", shown);
            StringAssert.Contains("BIGGEST OBJECTS ALIVE NOW", shown);
        }

        /// <summary>
        /// The panel is only useful if it attributes a switch to its steps — that is the question
        /// the editor could not answer and the device has to.
        /// </summary>
        [UnityTest]
        public IEnumerator AfterSwitchingATwin_ThePanelBreaksTheSwitchDown()
        {
            yield return ResetApp();
            yield return SelectTwin("LipEdema");
            yield return OpenSettings();

            string shown = FindGameObjectByPath(DisplayPath).GetComponent<TextMeshProUGUI>().text;
            Debug.Log("[memory panel after a switch]\n" + shown);

            StringAssert.Contains("last switch", shown);
            StringAssert.Contains("config saved", shown);
            StringAssert.Contains("config loaded", shown);
            StringAssert.Contains("texture saved", shown);
            StringAssert.Contains("peak during switch", shown);
            Assert.IsFalse(shown.Contains("none yet"),
                "The panel did not notice the twin switch.");
        }
        /// <summary>
        /// Three switches in a row, so a one-off warm-up cost cannot be mistaken for the real
        /// price of a switch. The figures are logged rather than asserted on: absolute numbers in
        /// the editor say nothing about an iPad, and the point here is the shape of the table.
        /// </summary>
        [UnityTest]
        public IEnumerator ThreeSwitchesInARow_ReportTheirCost()
        {
            yield return ResetApp();
            yield return SelectTwin("LipEdema");
            yield return SelectTwin("Torso");
            yield return SelectTwin("LipEdema");
            yield return OpenSettings();

            Debug.Log("[memory panel after three switches]\n"
                      + FindGameObjectByPath(DisplayPath).GetComponent<TextMeshProUGUI>().text);
        }
    }
}
