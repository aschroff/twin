using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// The twin has to be on disk before the app is taken away.
    /// </summary>
    /// <remarks>
    /// <para>The only saves used to be the explicit ones — a twin switch, New, Save as, applying a
    /// document — and <c>OnApplicationQuit</c>. iOS does not run that one when it reclaims a
    /// backgrounded app under memory pressure, which this app has been killed by before
    /// (TWIN-459), and swiping it out of the app switcher takes the same route. An hour of
    /// painting was one background away from being gone.</para>
    ///
    /// <para><b>Why these look at the file and not at a reload.</b> Every route back into a twin
    /// goes through <c>ChangeSelectedProfileId</c>, which saves on the way in. A test that painted,
    /// reloaded and found its part again would pass with or without any of this.</para>
    /// </remarks>
    [Category(Processes.AppFrame)]
    public class AutoSavePlayModeTests : TwinPaintTestBase
    {
        /// <summary>The config file inside a twin's directory — see APP_DOCUMENTATION §2.</summary>
        private const string ConfigFileName = "ConfigTwin";

        [UnityTest]
        public IEnumerator Backgrounding_WritesThePaintedPartToDisk()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.groups[0];

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(group);
            yield return PaintWithMarker("Red");

            Assert.AreEqual(1, group.groupParts.Count, "Painting produced no part to save.");
            string partId = group.groupParts[0].id;

            // nothing has saved since the twin was opened, so backgrounding has something to do
            Assert.IsFalse(StoredConfigContains(partId),
                "The part was on disk before the app was backgrounded — this test proves nothing.");

            Background();
            yield return null;

            Assert.IsTrue(StoredConfigContains(partId),
                "Backgrounding left the painted part unsaved. iOS kills a backgrounded app without "
                + "OnApplicationQuit, so this is exactly the work a user would lose.");
        }

        /// <summary>
        /// The periodic save may not land in the middle of an annotation.
        /// </summary>
        /// <remarks>
        /// Saving runs <c>PartManager.SaveData</c>, which sets <c>startNewPart</c>. A save between
        /// two strokes of the same annotation therefore splits it into two parts — two rows in the
        /// part list, two screenshots, two AI descriptions. The idle gate is the only thing
        /// standing between the autosave and that, so it is pinned here.
        /// </remarks>
        [UnityTest]
        public IEnumerator AutoSave_HoldsOffWhileTheUserIsStillPainting()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();

            // the real gate is five seconds; the test only needs it to be a gate
            SetPaintIdleSeconds(0.5f);

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(partManager.groups[0]);
            yield return PaintWithMarker("Red");

            Assert.IsFalse(ReadyToAutoSave(),
                "A save right after a stroke would set startNewPart and cut the annotation the "
                + "user is still drawing into two parts.");

            yield return new WaitForSeconds(0.8f);

            Assert.IsTrue(ReadyToAutoSave(),
                "The automatic save never became due again after the user stopped painting.");
        }

        private static DataPersistenceManager Manager()
        {
            var manager = Object.FindObjectOfType<DataPersistenceManager>();
            Assert.IsNotNull(manager, "DataPersistenceManager not found.");
            return manager;
        }

        /// <summary>What Unity calls when the app goes to the background.</summary>
        private static void Background()
        {
            Invoke("OnApplicationPause", new object[] { true });
        }

        private static bool ReadyToAutoSave()
        {
            return (bool)Invoke("ReadyToAutoSave", null);
        }

        private static object Invoke(string methodName, object[] arguments)
        {
            MethodInfo method = typeof(DataPersistenceManager).GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"DataPersistenceManager has no {methodName}.");
            return method.Invoke(Manager(), arguments);
        }

        private static void SetPaintIdleSeconds(float seconds)
        {
            FieldInfo field = typeof(DataPersistenceManager).GetField(
                "paintIdleSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "DataPersistenceManager has no paintIdleSeconds.");
            field.SetValue(Manager(), seconds);
        }

        /// <summary>Whether the open twin's file on disk mentions that part.</summary>
        private static bool StoredConfigContains(string partId)
        {
            string path = Path.Combine(
                DataPaths.PersistentDataPath, Manager().selectedProfileId, ConfigFileName);
            return File.Exists(path) && File.ReadAllText(path).Contains(partId);
        }
    }
}
