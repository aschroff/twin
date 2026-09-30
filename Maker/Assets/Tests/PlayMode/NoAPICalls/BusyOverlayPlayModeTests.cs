using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// The app says when it is busy, and does not let the user tap into it meanwhile.
    /// </summary>
    /// <remarks>Switching a twin blocks the main thread for about two seconds, measured. Nothing
    /// on screen used to change in that time, which left no way to tell a slow app from a hung
    /// one — and a second, impatient tap started the whole switch again.</remarks>
    [Category(Processes.AppFrame)]
    public class BusyOverlayPlayModeTests : TwinPaintTestBase
    {
        /// <summary>Clicks a twin without the usual wait, so the frame between the click and the
        /// work can be looked at — that is where the panel has to be.</summary>
        private IEnumerator ClickTwinWithoutWaiting(string twinName)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            GameObject twinEntry = FindChildWithTextValue(SaveTwinPanel, twinName);
            Assert.IsNotNull(twinEntry, $"Twin '{twinName}' not found in the twin list.");
            FindButtonByPath("Unselect", twinEntry).onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator WhileATwinLoads_TheUserIsToldAndCannotTapIntoIt()
        {
            yield return ResetApp();
            Assert.IsFalse(BusyOverlay.Working, "Setup: nothing should be loading yet.");

            yield return ClickTwinWithoutWaiting("LipEdema");

            Assert.IsTrue(BusyOverlay.Working, "The app is loading but does not say so.");
            Assert.IsTrue(BusyOverlay.Visible, "The panel is not on screen.");
            Assert.IsTrue(BusyOverlay.BlocksInput,
                "Taps are not being swallowed, so an impatient second tap would load again.");

            string message = BusyOverlay.Message;
            Assert.IsNotEmpty(message, "The panel says nothing.");
            Assert.AreNotEqual("LOADING_TWIN", message,
                "The message is the raw key — the string is missing from the locale tables.");

            yield return WaitWhileLoading();

            Assert.IsFalse(BusyOverlay.Working, "Loading finished but the app still thinks it is busy.");
            Assert.IsFalse(BusyOverlay.BlocksInput, "Taps are still being swallowed after loading.");
        }

        /// <summary>
        /// The twin really is loaded by the time the panel goes, not merely started.
        /// </summary>
        [UnityTest]
        public IEnumerator WhenThePanelGoes_TheTwinIsActuallyThere()
        {
            yield return ResetApp();
            yield return SelectTwin("LipEdema");

            var manager = Object.FindObjectOfType<DataPersistenceManager>();
            Assert.IsNotNull(manager, "No DataPersistenceManager in the scene.");
            StringAssert.StartsWith("LipEdema", manager.selectedProfileId,
                "The panel was taken down before the twin was loaded.");
        }
    }
}
