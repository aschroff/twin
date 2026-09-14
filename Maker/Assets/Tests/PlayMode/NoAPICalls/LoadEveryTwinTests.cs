using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Loads every twin in the list, one after the other, twice round.
    /// </summary>
    /// <remarks>Reported from manual use: switching between twins throws a
    /// <c>NullReferenceException</c> in <c>Model.LoadData</c>. The existing tests switch between a
    /// handful of twins and stay green, so whatever it is, it needs the whole list — or a twin the
    /// other tests never open.</remarks>
    [Category(Processes.ManageTwins)]
    public class LoadEveryTwinTests : TwinPaintTestBase
    {
        private List<string> TwinsInTheList()
        {
            GameObject panel = FindGameObjectByPath(SaveTwinPanel);
            Assert.IsNotNull(panel, "The twin list is not on screen.");

            var names = new List<string>();
            foreach (Transform entry in panel.transform)
            {
                Transform label = entry.Find("Name/Text");
                Text text = label != null ? label.GetComponent<Text>() : null;
                if (text != null && !string.IsNullOrWhiteSpace(text.text))
                {
                    names.Add(text.text);
                }
            }
            return names;
        }

        [UnityTest]
        public IEnumerator EveryTwinInTheList_LoadsWithoutError()
        {
            yield return ResetApp();
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            List<string> twins = TwinsInTheList();
            Assert.Greater(twins.Count, 1, "Setup: the list should hold several twins.");
            Debug.Log("[load every twin] the list holds: " + string.Join(", ", twins));

            for (int round = 1; round <= 2; round++)
            {
                foreach (string twin in twins)
                {
                    yield return SelectTwin(twin);
                }
            }
        }
    }
}
