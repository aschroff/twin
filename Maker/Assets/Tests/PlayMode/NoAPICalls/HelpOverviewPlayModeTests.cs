using System.Collections;
using Code.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The overview of the Help UI (<c>AI.characterDescription</c>) shows the answer of the last
    /// report request. It belongs to the twin that was open when the request came back, so
    /// resetting the app or opening another twin has to take it away - including the red of a
    /// failed request.
    /// </summary>
    [Category(Processes.ManageTwins)]
    public class HelpOverviewPlayModeTests : PlayModeTestBase
    {
        const string Report = "Report of the twin that is open";
        const string StaleReport = "Report of the twin that was open before";
        const string OtherTwin = "OtherTwin";

        AI ai;
        string idleText;
        Color idleColor;

        [UnitySetUp]
        public override IEnumerator SetUp()
        {
            yield return base.SetUp();
            ai = Object.FindObjectOfType<AI>(true);
            Assert.IsNotNull(ai, "AI not found in scene.");
            Assert.IsNotNull(ai.characterDescription, "AI.characterDescription is not wired.");

            // what a fresh start leaves in the overview: nothing, or the missing-key notice
            idleText = ai.characterDescription.text;
            idleColor = ai.characterDescription.color;
            if (ai.hasApiKey)
            {
                Assert.IsEmpty(idleText, "With an API key the overview starts empty.");
            }
        }

        [UnityTest]
        public IEnumerator ResetApp_EmptiesTheOverview()
        {
            ShowStaleReport();

            yield return ResetApp();

            AssertOverviewIsIdle("after Reset App");
        }

        [UnityTest]
        public IEnumerator OpeningAnotherTwin_EmptiesTheOverview()
        {
            string firstTwin = DataPersistenceManager.instance.selectedProfileId;
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", OtherTwin);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");
            Assert.AreNotEqual(firstTwin, DataPersistenceManager.instance.selectedProfileId,
                "Creating a twin should leave the app on the new twin.");

            ShowStaleReport();

            DataPersistenceManager.instance.ChangeSelectedProfileId(firstTwin);
            yield return null;

            AssertOverviewIsIdle("after opening another twin");
        }

        /// <summary>Closing the Help UI and opening it again is not a twin load: the report has to
        /// stay where it is.</summary>
        [UnityTest]
        public IEnumerator ReopeningTheHelpUI_KeepsTheOverview()
        {
            InteractionController.EnableMode("Help");
            yield return WaitForModeActive("Help");
            ShowReport();

            InteractionController.EnableMode("Main");
            yield return WaitForModeActive("Main");
            InteractionController.EnableMode("Help");
            yield return WaitForModeActive("Help");
            yield return null;

            Assert.AreEqual(Report, ai.characterDescription.text,
                "Closing and reopening the Help UI took the report away.");
        }

        /// <summary>Leaves the overview the way a failed request after a report does.</summary>
        void ShowStaleReport()
        {
            ai.characterDescription.text = StaleReport;
            ai.characterDescription.color = Color.red;
        }

        void ShowReport()
        {
            ai.characterDescription.text = Report;
        }

        void AssertOverviewIsIdle(string when)
        {
            Assert.AreEqual(idleText, ai.characterDescription.text,
                $"The overview still shows the report of the previous twin {when}.");
            Assert.AreEqual(idleColor, ai.characterDescription.color,
                $"The overview kept the colour of an earlier request {when}.");
        }
    }
}
