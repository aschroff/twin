using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace EditModeTests
{
    /// <summary>
    /// Which twin, and which version of it, the app puts in front of the user.
    /// </summary>
    /// <remarks>
    /// <para>The rule behind the twin list is not obvious and was never checked: one entry per
    /// twin name, represented by the version that is currently open - and only by the newest one
    /// when none of them is. A twin the user has open must not disappear from the list behind a
    /// version that happens to carry a later timestamp.</para>
    ///
    /// <para>No scene: the manager sits on a bare GameObject and its file handler is injected,
    /// because <c>Awake</c> - which normally builds it from the persistent data path - does not
    /// run outside play mode. Everything the manager does through the scene (loading, saving,
    /// import, export, and the fan-out to every IDataPersistence) is covered against the real app
    /// by SaveTwinPlayModeTests, ImportTwinPlayModeTests and GroupPlayModeTests.</para>
    /// </remarks>
    [Category(Processes.ManageTwins)]
    public class DataPersistenceManagerTests
    {
        private const string FileName = "ConfigTwin.txt";

        private string dataDir;
        private string templateDir;
        private GameObject host;
        private DataPersistenceManager manager;

        [SetUp]
        public void SetUp()
        {
            string root = Path.Combine(Application.temporaryCachePath, "DataPersistenceManagerTests");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            dataDir = Path.Combine(root, "data");
            templateDir = Path.Combine(root, "templates");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(templateDir);

            host = new GameObject("DataPersistenceManagerTests");
            manager = host.AddComponent<DataPersistenceManager>();
            UseHandler(new FileDataHandler(dataDir, templateDir, FileName, false));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
            string root = Path.Combine(Application.temporaryCachePath, "DataPersistenceManagerTests");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        /// <summary>
        /// The field is private and normally filled by <c>Awake</c>, which play mode calls and an
        /// EditMode test does not. Reaching in keeps these tests out of the scene; the alternative
        /// would be a seam in the manager that exists only for tests.
        /// </summary>
        private void UseHandler(FileDataHandler handler)
        {
            FieldInfo field = typeof(DataPersistenceManager)
                .GetField("dataHandler", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "DataPersistenceManager no longer has a 'dataHandler' field.");
            field.SetValue(manager, handler);
        }

        /// <summary>One twin on disk. <paramref name="updatedAt"/> is what the list sorts by.</summary>
        private void GiveTwin(string name, string version, int updatedAt, string group = null)
        {
            var data = new ConfigData(name, version);
            data.lastUpdated = updatedAt;
            if (group != null) data.groupList.Add(group);
            new FileDataHandler(dataDir, templateDir, FileName, false).Save(data, name + "." + version);
        }

        [Test]
        public void GetAllProfileNamesGameData_listsEachTwinNameOnce()
        {
            GiveTwin("Knee", "001", updatedAt: 10);
            GiveTwin("Knee", "002", updatedAt: 20);
            GiveTwin("Shoulder", "001", updatedAt: 30);

            Dictionary<string, ConfigData> byName = manager.GetAllProfileNamesGameData();

            CollectionAssert.AreEquivalent(new[] { "Knee", "Shoulder" }, byName.Keys);
        }

        [Test]
        public void GetAllProfileNamesGameData_withNothingOpen_representsATwinByItsNewestVersion()
        {
            GiveTwin("Knee", "001", updatedAt: 10);
            GiveTwin("Knee", "002", updatedAt: 20);
            manager.selectedProfileId = "Shoulder.001";

            Dictionary<string, ConfigData> byName = manager.GetAllProfileNamesGameData();

            Assert.AreEqual("002", byName["Knee"].version);
        }

        /// <summary>
        /// The open version wins over a newer one. Otherwise the twin the user is working on would
        /// vanish from the list the moment another version of it was touched more recently - by an
        /// import, say.
        /// </summary>
        [Test]
        public void GetAllProfileNamesGameData_representsATwinByTheOpenVersion_evenWhenAnotherIsNewer()
        {
            GiveTwin("Knee", "001", updatedAt: 10);
            GiveTwin("Knee", "002", updatedAt: 99);
            manager.selectedProfileId = "Knee.001";

            Dictionary<string, ConfigData> byName = manager.GetAllProfileNamesGameData();

            Assert.AreEqual("001", byName["Knee"].version,
                "The open version was displaced by a newer one.");
        }

        [Test]
        public void GetAllVersionsGameData_listsOnlyThatTwinsVersions_keyedByVersion()
        {
            GiveTwin("Knee", "001", updatedAt: 10, group: "first");
            GiveTwin("Knee", "002", updatedAt: 20, group: "second");
            GiveTwin("Shoulder", "001", updatedAt: 30);

            Dictionary<string, ConfigData> versions = manager.GetAllVersionsGameData("Knee");

            CollectionAssert.AreEquivalent(new[] { "001", "002" }, versions.Keys);
            CollectionAssert.AreEqual(new[] { "first" }, versions["001"].groupList);
            CollectionAssert.AreEqual(new[] { "second" }, versions["002"].groupList);
        }

        [Test]
        public void GetAllVersionsGameData_ofATwinThatIsNotThere_isEmpty()
        {
            GiveTwin("Knee", "001", updatedAt: 10);

            CollectionAssert.IsEmpty(manager.GetAllVersionsGameData("Elbow"));
        }

        [Test]
        public void ExistsProfileId_tellsAStoredTwinFromOneThatIsNotThere()
        {
            GiveTwin("Knee", "001", updatedAt: 10);

            Assert.IsTrue(manager.ExistsProfileId("Knee.001"));
            Assert.IsFalse(manager.ExistsProfileId("Knee.002"));
        }

        /// <summary>Asked before Awake has run, this must answer rather than throw.</summary>
        [Test]
        public void ExistsProfileId_withoutAHandler_isFalseRatherThanAThrow()
        {
            UseHandler(null);

            Assert.IsFalse(manager.ExistsProfileId("Knee.001"));
        }

        [Test]
        public void HasGameData_isFalseUntilAConfigIsInHand()
        {
            Assert.IsFalse(manager.HasGameData());

            manager.NewConfig("Knee", "001");

            Assert.IsTrue(manager.HasGameData());
        }

        /// <summary>
        /// Deleting the twin that is open does nothing at all - no deletion, and no word to the
        /// caller either. Written down as it stands after a deliberate decision (TWIN-451): telling
        /// the user would mean touching the UI, which this ticket does not.
        /// </summary>
        [Test]
        public void DeleteProfileData_leavesTheTwinThatIsOpenAlone()
        {
            GiveTwin("Knee", "001", updatedAt: 10);
            manager.selectedProfileId = "Knee.001";

            manager.DeleteProfileData("Knee.001");

            Assert.IsTrue(manager.ExistsProfileId("Knee.001"), "The open twin was deleted.");
        }
    }
}
