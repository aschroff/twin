using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PaintCore;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// A test run must not read or write the paintings of the person using the app.
    /// </summary>
    /// <remarks>
    /// <para>The data directory is redirected for tests, but the painted body texture is not kept
    /// there — it is kept in PlayerPrefs, which is one store per application. Until this was
    /// fixed, <c>Body.handleChange</c> set the save name straight from the twin's profile id, so
    /// the first twin switch in any test threw the test prefix away and the test went on to read
    /// and overwrite the real keys.</para>
    ///
    /// <para>That is not only a matter of tidiness: it made
    /// <c>UndoRedoPlayModeTests.UndoAndRedo_TakeBackAndRestoreTheLastPart</c> fail, because the
    /// twin it loads arrived carrying a painting made by hand weeks earlier. A test that depends
    /// on data it does not control passes or fails for reasons nobody can see.</para>
    /// </remarks>
    [Category(Processes.Technical)]
    public class PaintIsolationPlayModeTests : TwinPaintTestBase
    {
        private static CwPaintableTexture BodyTexture()
        {
            Body body = Object.FindObjectOfType<Body>();
            Assert.IsNotNull(body, "No Body in the scene.");
            return body.GetComponent<CwPaintableTexture>();
        }

        private static string SelectedProfile()
        {
            var manager = Object.FindObjectOfType<DataPersistenceManager>();
            Assert.IsNotNull(manager, "No DataPersistenceManager in the scene.");
            return manager.selectedProfileId;
        }

        [UnityTest]
        public IEnumerator SwitchingATwin_KeepsTheTestPrefixOnTheSaveName()
        {
            yield return ResetApp();
            yield return SelectTwin("LipEdema");

            string profile = SelectedProfile();
            string saveName = BodyTexture().SaveName;

            Assert.AreNotEqual(profile, saveName,
                "The save name is the raw profile id, so this run is writing into the real app's "
                + "PlayerPrefs key '" + profile + "'.");
            StringAssert.StartsWith("PlayModeTest", saveName,
                "The save name lost the test prefix on the twin switch.");
        }

        [UnityTest]
        public IEnumerator AWholeRound_LeavesTheRealKeysUntouched()
        {
            yield return ResetApp();

            // Snapshot before the first switch: after one switch the damage is already done, and
            // comparing a spoilt value against itself would pass while the app was overwriting
            // somebody's painting.
            var manager = Object.FindObjectOfType<DataPersistenceManager>();
            Assert.IsNotNull(manager, "No DataPersistenceManager in the scene.");

            const string absent = "<no key>";
            var before = new Dictionary<string, string>();
            foreach (KeyValuePair<string, ConfigData> entry in manager.GetAllProfilesGameData())
            {
                before[entry.Key] = PlayerPrefs.GetString(entry.Key, absent);
            }
            Assert.Greater(before.Count, 1, "Setup: there should be several twins to check.");

            // Painting matters here. Loading a twin and leaving it again writes the same image
            // back, so the key would look untouched even while the app was using the wrong one.
            // Only a stroke makes the written value differ from what was there.
            yield return SelectTwin("LipEdema");
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker("Red");

            // leaving the twin is what saves it
            yield return SelectTwin("Torso");
            yield return SelectTwin("LipEdema");

            foreach (KeyValuePair<string, string> entry in before)
            {
                Assert.AreEqual(entry.Value, PlayerPrefs.GetString(entry.Key, absent),
                    "A test run changed '" + entry.Key + "' — that is the key the installed app "
                    + "uses for this twin, and it holds the user's painting.");
            }
        }
    }
}
