using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace EditModeTests
{
    /// <summary>
    /// Saving, loading and deleting a twin on disk - and the backup that is supposed to survive a
    /// file going bad.
    /// </summary>
    /// <remarks>
    /// <para>These run without a scene because <c>FileDataHandler</c> is a plain class that takes
    /// its directories as constructor arguments. Each test gets its own temporary directory, so
    /// nothing here can touch a real twin.</para>
    ///
    /// <para>Half of this class was already "covered" before a line of it was tested: the app
    /// saves and loads twins during unrelated PlayMode tests, so the lines ran while nothing
    /// checked what came out of them. Backup and rollback - the mechanism that exists to prevent
    /// data loss - had never been exercised at all.</para>
    ///
    /// <para>Import and export are deliberately absent: <c>ImportTwinPlayModeTests</c> covers them
    /// against the real app, including a broken zip.</para>
    /// </remarks>
    [Category(Processes.ManageTwins)]
    public class FileDataHandlerTests
    {
        private const string FileName = "ConfigTwin.txt";

        /// <summary>Twin directories are "&lt;name&gt;.&lt;version&gt;", which is what the app writes.</summary>
        private const string ProfileId = "Knee.001";

        private string dataDir;
        private string templateDir;

        [SetUp]
        public void SetUp()
        {
            string root = Path.Combine(Application.temporaryCachePath, "FileDataHandlerTests");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            dataDir = Path.Combine(root, "data");
            templateDir = Path.Combine(root, "templates");
            Directory.CreateDirectory(dataDir);
            Directory.CreateDirectory(templateDir);
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            string root = Path.Combine(Application.temporaryCachePath, "FileDataHandlerTests");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        private FileDataHandler Handler(bool useEncryption = false)
        {
            return new FileDataHandler(dataDir, templateDir, FileName, useEncryption);
        }

        /// <summary>A twin with enough in it that an empty one could not pass by accident.</summary>
        private static ConfigData ATwin()
        {
            var data = new ConfigData("Knee", "001");
            data.groupList.Add("Left knee");
            data.groupList.Add("Right knee");
            data.itemTexts.Add("part-1", "swelling, lateral");
            data.prompts.Add("Medical Report", "Describe the findings.");
            data.commandDetails = "{\"strokes\":3}";
            data.yaw = 12.5f;
            data.pitch = -4.25f;
            data.languageID = 2;
            return data;
        }

        private string FilePath(string profileId)
        {
            return Path.Combine(dataDir, profileId, FileName);
        }

        private string TemplatePath(string profileId)
        {
            return Path.Combine(templateDir, profileId, FileName);
        }

        /// <summary>A template on disk, with its backup, the way a shipped template arrives.</summary>
        private void WriteTemplate(string profileId, ConfigData data)
        {
            Directory.CreateDirectory(Path.Combine(templateDir, profileId));
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(TemplatePath(profileId), json);
            File.WriteAllText(TemplatePath(profileId) + ".bak", json);
        }

        [Test]
        public void Save_thenLoad_bringsBackWhatWasStored()
        {
            Handler().Save(ATwin(), ProfileId);

            ConfigData loaded = Handler().Load(ProfileId);

            Assert.IsNotNull(loaded, "Nothing came back for a twin that was just saved.");
            Assert.AreEqual("Knee", loaded.name);
            Assert.AreEqual("001", loaded.version);
            CollectionAssert.AreEqual(new[] { "Left knee", "Right knee" }, loaded.groupList);
            Assert.AreEqual("swelling, lateral", loaded.itemTexts["part-1"]);
            Assert.AreEqual("Describe the findings.", loaded.prompts["Medical Report"]);
            Assert.AreEqual("{\"strokes\":3}", loaded.commandDetails);
            Assert.AreEqual(12.5f, loaded.yaw, 0.001f);
            Assert.AreEqual(-4.25f, loaded.pitch, 0.001f);
            Assert.AreEqual(2, loaded.languageID);
        }

        /// <summary>
        /// Encryption is a constructor flag, and the round trip has to survive it. The check on the
        /// raw file is what tells a working cipher from a flag that is quietly ignored.
        /// </summary>
        [Test]
        public void Save_withEncryption_writesSomethingUnreadable_andStillLoadsIt()
        {
            Handler(useEncryption: true).Save(ATwin(), ProfileId);

            string raw = File.ReadAllText(FilePath(ProfileId));
            StringAssert.DoesNotContain("Left knee", raw, "The file on disk is readable, so nothing was encrypted.");

            ConfigData loaded = Handler(useEncryption: true).Load(ProfileId);
            Assert.IsNotNull(loaded);
            CollectionAssert.AreEqual(new[] { "Left knee", "Right knee" }, loaded.groupList);
        }

        [Test]
        public void Load_ofATwinThatIsNotThere_isNull()
        {
            Assert.IsNull(Handler().Load("NoSuchTwin.001"));
        }

        [Test]
        public void Load_ofNull_isNullRatherThanAThrow()
        {
            Assert.IsNull(Handler().Load(null));
        }

        [Test]
        public void Save_withoutAProfileId_writesNothing()
        {
            Handler().Save(ATwin(), null);

            CollectionAssert.IsEmpty(Directory.GetDirectories(dataDir),
                "Saving without a profile id created a directory.");
        }

        [Test]
        public void Save_createsTheDirectoryItWritesInto()
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(dataDir, ProfileId)), "Precondition: no directory yet.");

            Handler().Save(ATwin(), ProfileId);

            Assert.IsTrue(File.Exists(FilePath(ProfileId)));
            Assert.IsTrue(Handler().Exists(ProfileId));
        }

        /// <summary>The backup is written only after the new file has been read back successfully.</summary>
        [Test]
        public void Save_leavesABackupNextToTheFile()
        {
            Handler().Save(ATwin(), ProfileId);

            Assert.IsTrue(File.Exists(FilePath(ProfileId) + ".bak"), "No .bak was written.");
            Assert.AreEqual(File.ReadAllText(FilePath(ProfileId)), File.ReadAllText(FilePath(ProfileId) + ".bak"));
        }

        /// <summary>
        /// The point of the whole backup mechanism: a twin whose file went bad comes back from the
        /// copy instead of being lost. Nothing exercised this before.
        /// </summary>
        [Test]
        public void Load_ofACorruptedFile_comesBackFromTheBackup()
        {
            Handler().Save(ATwin(), ProfileId);
            File.WriteAllText(FilePath(ProfileId), "this is not json at all");

            ConfigData loaded = Handler().Load(ProfileId);

            Assert.IsNotNull(loaded, "A corrupted twin was not restored from its backup.");
            CollectionAssert.AreEqual(new[] { "Left knee", "Right knee" }, loaded.groupList);
            Assert.AreEqual("{\"strokes\":3}", loaded.commandDetails);
        }

        /// <summary>
        /// With the backup gone bad too there is nothing to restore. The interesting part is that
        /// it ends: <c>Load</c> calls itself after a rollback, and only the second argument stops
        /// that from going on forever.
        /// </summary>
        [Test]
        public void Load_withFileAndBackupCorrupted_isNullRatherThanEndlessRecursion()
        {
            LogAssert.ignoreFailingMessages = true;

            Handler().Save(ATwin(), ProfileId);
            File.WriteAllText(FilePath(ProfileId), "this is not json at all");
            File.WriteAllText(FilePath(ProfileId) + ".bak", "this is not json either");

            Assert.IsNull(Handler().Load(ProfileId));
        }


        /// <summary>
        /// An empty file is what a crash or a full disk leaves behind, and it is exactly what the
        /// backup exists for. It parses to null <b>without throwing</b>, so it used to slip past
        /// the rollback and the twin was reported as simply absent.
        /// </summary>
        [Test]
        public void Load_ofAnEmptyFile_comesBackFromTheBackup()
        {
            Handler().Save(ATwin(), ProfileId);
            File.WriteAllText(FilePath(ProfileId), string.Empty);

            ConfigData loaded = Handler().Load(ProfileId);

            Assert.IsNotNull(loaded, "An empty file was treated as 'no twin' instead of a damaged one.");
            CollectionAssert.AreEqual(new[] { "Left knee", "Right knee" }, loaded.groupList);
        }

        /// <summary>
        /// A damaged template must come back from the template's own backup. It used to fall into
        /// the <i>data</i> directory after the rollback, so a new twin could be created holding
        /// another twin's findings - the worst outcome this class can produce.
        /// </summary>
        [Test]
        public void LoadFromTemplate_ofADamagedTemplate_staysInTheTemplateDirectory()
        {
            var template = new ConfigData("Knee", "001");
            template.groupList.Add("Template knee");
            WriteTemplate(ProfileId, template);
            File.WriteAllText(TemplatePath(ProfileId), "this is not json at all");

            // a different twin, under the same id, in the data directory
            var somebodyElse = new ConfigData("Knee", "001");
            somebodyElse.groupList.Add("Another patient");
            Handler().Save(somebodyElse, ProfileId);

            ConfigData loaded = Handler().LoadFromTemplate(ProfileId);

            Assert.IsNotNull(loaded, "The damaged template was not restored from its backup.");
            CollectionAssert.AreEqual(new[] { "Template knee" }, loaded.groupList,
                "LoadFromTemplate returned the twin from the data directory instead of the template.");
        }

        [Test]
        public void Delete_removesTheWholeTwinDirectory()
        {
            Handler().Save(ATwin(), ProfileId);
            Assert.IsTrue(Directory.Exists(Path.Combine(dataDir, ProfileId)), "Precondition: the twin is there.");

            Handler().Delete(ProfileId);

            Assert.IsFalse(Directory.Exists(Path.Combine(dataDir, ProfileId)), "The directory survived Delete.");
            Assert.IsFalse(Handler().Exists(ProfileId));
        }

        /// <summary>
        /// A directory without a config is not a twin - a stray folder, a half-finished copy - and
        /// must not stop the others from being listed.
        /// </summary>
        [Test]
        public void LoadAllProfiles_skipsADirectoryWithoutAConfig()
        {
            Handler().Save(ATwin(), ProfileId);
            Directory.CreateDirectory(Path.Combine(dataDir, "NotATwin"));

            var profiles = Handler().LoadAllProfiles();

            CollectionAssert.AreEquivalent(new[] { ProfileId }, profiles.Keys);
        }
    }
}
