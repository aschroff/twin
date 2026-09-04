using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PaintCore;
using UnityEngine;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Exporting a twin and importing it back (the Export/Import buttons of the save screen).
    /// Two things make this more than unzipping a folder: the painted texture is cached per
    /// device and has to travel with the twin as a file, and the twin's identity comes from
    /// its config - never from the name of the zip file, which anything on the way here may
    /// have renamed.
    /// </summary>
    public class ImportTwinPlayModeTests : TwinPaintTestBase
    {
        /// <summary>Name for the twins created by the naming tests, short enough for
        /// TwinNameValidator (11 characters at most).</summary>
        const string NamedTwin = "abc";


        /// <summary>Paint a twin, export it, throw everything away, import it back: the paint
        /// has to show up as soon as the twin is opened, without the user having to hide and
        /// show a group to get it replayed.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_ShowsItsPaintWhenOpened()
        {
            yield return LoadLipEdemaTwin();
            Color32[] unpainted = BodyPixels();

            PartManager partManager = FindPartManager();
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(partManager.groups[0]);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            int paintedPixels = DifferingPixels(unpainted, BodyPixels());
            Assert.Greater(paintedPixels, 0, "Setup: painting should have changed the body texture.");

            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.IsTrue(File.Exists(zipFilePath), $"The export should have written a zip file to '{zipFilePath}'.");

            // drop every twin (and with it every cached texture), so the twin can only come
            // back through the import
            yield return ResetApp();

            string importedProfileId = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.IsNotNull(importedProfileId, "The import did not produce a twin.");
            Assert.IsFalse(CwCommon.SaveExists(importedProfileId),
                "Setup: an imported twin must not bring a cached texture with it.");

            yield return SelectTwin("LipEdema");
            Assert.AreEqual(importedProfileId, DataPersistenceManager.instance.selectedProfileId,
                "Setup: the imported twin should be the loaded one.");

            // the rebuild spans a few frames (the paint is drawn by CwPaintableManager in
            // LateUpdate) and caches the texture when it is done
            yield return WaitForCachedTexture(importedProfileId);

            int importedPixels = DifferingPixels(unpainted, BodyPixels());
            Assert.Greater(importedPixels, 0,
                "The paint of the imported twin is not visible - it only shows after hiding and showing a group.");
            Assert.AreEqual(paintedPixels, importedPixels, paintedPixels * 0.1,
                "The rebuilt texture should show the same paint as the exported one.");
        }

        /// <summary>The imported twin ends up in the twin list with its parts.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_KeepsItsGroupsAndParts()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();
            int groupCount = partManager.groups.Count;
            string paintedGroup = partManager.groups[0].name;

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(partManager.groups[0]);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            yield return ResetApp();

            string importedProfileId = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.IsNotNull(importedProfileId, "The import did not produce a twin.");

            yield return SelectTwin("LipEdema");

            partManager = FindPartManager();
            Assert.AreEqual(groupCount, partManager.groups.Count, "The imported twin lost groups.");
            PartManager.GroupData group = partManager.groups[0];
            Assert.AreEqual(paintedGroup, group.name, "The imported twin lost the group names.");
            Assert.AreEqual(1, group.groupParts.Count, "The painted part did not survive the export/import.");
            AssertPartsAreUsable(partManager);
        }

        /// <summary>An import must never overwrite a twin that is already on the device. The
        /// imported one gets a V01 suffix on its version instead, a second import V02.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_KeepsTheTwinThatIsAlreadyThere()
        {
            yield return LoadLipEdemaTwin();
            string existingProfileId = DataPersistenceManager.instance.selectedProfileId;
            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.IsTrue(File.Exists(zipFilePath), "Setup: the export should have written a zip file.");

            string firstImport = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.AreEqual(existingProfileId + "V01", firstImport,
                "Importing a twin that exists already should add a version, not replace it.");

            string secondImport = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.AreEqual(existingProfileId + "V02", secondImport,
                "The next import of the same twin should get the next free version.");

            Assert.IsTrue(TwinDirectoryExists(existingProfileId),
                "The twin that was already on the device must survive an import of its name.");
            Assert.IsTrue(TwinDirectoryExists(firstImport), "The first import is missing.");
            Assert.IsTrue(TwinDirectoryExists(secondImport), "The second import is missing.");
            AssertConfigMatchesDirectory(firstImport);
            AssertConfigMatchesDirectory(secondImport);
        }

        /// <summary>The zip file may arrive under any name - a mail client or a file manager
        /// renames it, a server hands it over under an id. The twin keeps the name from its
        /// config, and can be reached and opened under that name.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_IgnoresTheNameOfTheZipFile()
        {
            yield return LoadLipEdemaTwin();
            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.IsTrue(File.Exists(zipFilePath), "Setup: the export should have written a zip file.");

            string renamedZip = Path.Combine(Path.GetDirectoryName(zipFilePath), "Mail Attachment (1).zip");
            File.Move(zipFilePath, renamedZip);

            string importedProfileId = DataPersistenceManager.instance.ImportConfig(renamedZip);
            Assert.IsNotNull(importedProfileId, "The import did not produce a twin.");
            Assert.IsTrue(importedProfileId.StartsWith("LipEdema."),
                $"The twin should be named after its config, but its directory is '{importedProfileId}'.");
            AssertConfigMatchesDirectory(importedProfileId);

            // the versions screen is where the versions of a twin are listed
            Assert.IsTrue(VersionsOf("LipEdema").Contains(VersionOf(importedProfileId)),
                $"Version '{VersionOf(importedProfileId)}' is missing from the versions of LipEdema.");

            yield return OpenTwin(importedProfileId);
            Assert.AreEqual(importedProfileId, DataPersistenceManager.instance.selectedProfileId,
                "The imported twin could not be opened.");
        }

        /// <summary>The version an import gets is derived from the id in its config: that id when
        /// it is free, and the first free V-suffix of exactly that id when it is not. So the
        /// suffix is not a running number per twin - it fills the first gap.</summary>
        [UnityTest]
        public IEnumerator ImportedTwin_TakesTheIdFromItsConfigWhenItIsFree()
        {
            yield return ResetApp();
            yield return CreateTwin(NamedTwin);
            string baseProfileId = DataPersistenceManager.instance.selectedProfileId;
            Assert.AreEqual(NamedTwin + ".000", baseProfileId, "Setup: a new twin starts at version 000.");

            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.AreEqual(baseProfileId + "V01", DataPersistenceManager.instance.ImportConfig(zipFilePath),
                "Setup: with the id taken the import should get the first free suffix.");
            yield return RefreshTwinList();

            // free the id again through the app - a twin can only be deleted while another one
            // is the current twin
            yield return SelectTwin("LipEdema");
            DataPersistenceManager.instance.DeleteProfileData(baseProfileId);
            Assert.IsFalse(TwinDirectoryExists(baseProfileId), "Setup: the twin should be deleted.");

            Assert.AreEqual(baseProfileId, DataPersistenceManager.instance.ImportConfig(zipFilePath),
                "With its id free again, the import should use it instead of a suffix.");
        }

        /// <summary>A twin that comes back after a round trip carries the suffix it was given on
        /// the way out, and the next suffix is added to that whole id: 000V01 becomes 000V01V01.
        /// </summary>
        [UnityTest]
        public IEnumerator ImportedTwin_AddsTheSuffixToTheVersionItBringsAlong()
        {
            yield return ResetApp();
            yield return CreateTwin(NamedTwin);
            string baseProfileId = DataPersistenceManager.instance.selectedProfileId;

            string firstZip = DataPersistenceManager.instance.ExportConfigZip();
            string firstImport = DataPersistenceManager.instance.ImportConfig(firstZip);
            Assert.AreEqual(baseProfileId + "V01", firstImport, "Setup: the first import should be V01.");
            yield return RefreshTwinList();

            // export the imported twin, so its config carries the suffixed version
            yield return OpenTwin(firstImport);
            string secondZip = DataPersistenceManager.instance.ExportConfigZip();
            Assert.AreNotEqual(firstZip, secondZip, "Setup: the two exports should be separate files.");

            Assert.AreEqual(firstImport + "V01", DataPersistenceManager.instance.ImportConfig(secondZip),
                "The suffix of a returning twin is kept and the next one is added to it.");
        }

        /// <summary>Importing a twin while a twin of the same name is open must keep both apart:
        /// the open one goes on being edited and saved, the imported one keeps what it brought.
        /// </summary>
        [UnityTest]
        public IEnumerator Import_WhileTheSameTwinIsOpen_KeepsBothApart()
        {
            yield return LoadLipEdemaTwin();
            string openProfileId = DataPersistenceManager.instance.selectedProfileId;
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.groups[0];

            // one part goes into the export ...
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(group);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            Assert.IsTrue(File.Exists(zipFilePath), "Setup: the export should have written a zip file.");

            // ... a second one only into the twin that stays open
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return PaintWithMarker("Green");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
            Assert.AreEqual(2, group.groupParts.Count, "Setup: the open twin should hold two parts.");

            string importedProfileId = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.AreEqual(openProfileId + "V01", importedProfileId,
                "The import must not land on the twin that is open.");
            yield return RefreshTwinList();

            // opening the imported twin saves the one that was open - which must not reach into
            // the directory of the imported one
            yield return OpenTwin(importedProfileId);
            Assert.AreEqual(1, FindPartManager().groups[0].groupParts.Count,
                "The imported twin should hold the one part it was exported with.");

            yield return OpenTwin(openProfileId);
            Assert.AreEqual(2, FindPartManager().groups[0].groupParts.Count,
                "The twin that was open should have kept both of its parts.");
        }

        /// <summary>The twin list holds one row per twin name, so a twin with several versions is
        /// represented by one of them. While one of its versions is open, that has to be the one
        /// on the row - otherwise the list marks no twin as open although the app shows one.
        /// </summary>
        [UnityTest]
        public IEnumerator Import_LeavesTheOpenTwinMarkedInTheList()
        {
            yield return LoadLipEdemaTwin();
            string openProfileId = DataPersistenceManager.instance.selectedProfileId;
            string openVersion = openProfileId.Substring(openProfileId.LastIndexOf(".") + 1);

            string zipFilePath = DataPersistenceManager.instance.ExportConfigZip();
            string importedProfileId = DataPersistenceManager.instance.ImportConfig(zipFilePath);
            Assert.AreEqual(openProfileId + "V01", importedProfileId, "Setup: the import should add a version.");
            yield return RefreshTwinList();

            GameObject row = FindChildWithTextValue(SaveTwinPanel, "LipEdema");
            Assert.IsNotNull(row, "The twin is missing from the twin list.");
            Assert.AreEqual(openVersion, RowVersion(row),
                "The row should show the version the app has open, not another one.");
            Assert.IsFalse(RowChild(row, "Unselect").activeSelf,
                "The row of the open twin should not offer to open it.");
            Assert.IsTrue(RowChild(row, "Select").activeSelf,
                "The row of the open twin should be marked as the open one.");
        }

        /// <summary>Opens a twin version, the way the versions screen does. The twin list only
        /// offers one version per twin name, so a version that is not on the row is reached
        /// through the versions behind it.</summary>
        IEnumerator OpenTwin(string profileId)
        {
            DataPersistenceManager.instance.ChangeSelectedProfileId(profileId);
            yield return null;
        }

        /// <summary>The versions of a twin name, as the versions screen lists them.</summary>
        static ICollection<string> VersionsOf(string twinName)
        {
            return DataPersistenceManager.instance.GetAllVersionsGameData(twinName).Keys;
        }

        static string VersionOf(string profileId)
        {
            return profileId.Substring(profileId.LastIndexOf(".") + 1);
        }

        static string RowVersion(GameObject row)
        {
            return RowChild(row, "Version/Text").GetComponent<UnityEngine.UI.Text>().text;
        }

        static GameObject RowChild(GameObject row, string path)
        {
            Transform child = row.transform.Find(path);
            Assert.IsNotNull(child, $"The twin list row has no '{path}'.");
            return child.gameObject;
        }

        /// <summary>A twin directory can appear without the app writing it - a file manager copies
        /// one, a second download lands as "&lt;name&gt; 2". Its config then says something else than
        /// its directory, and listing the versions of that twin must still work.</summary>
        [UnityTest]
        public IEnumerator TwinDirectoryTheAppDidNotWrite_DoesNotBreakTheVersionList()
        {
            yield return LoadLipEdemaTwin();
            string openProfileId = DataPersistenceManager.instance.selectedProfileId;

            // a copy of the twin, the way a file manager or a repeated download leaves it: the
            // directory is called something else while the config still names the original
            string copyProfileId = openProfileId + " 2";
            CopyTwinDirectory(openProfileId, copyProfileId);

            ICollection<string> versions = VersionsOf("LipEdema");
            Assert.IsTrue(versions.Contains(VersionOf(openProfileId)),
                "The twin itself is missing from its versions.");
            Assert.IsTrue(versions.Contains(VersionOf(copyProfileId)),
                $"The copied directory should be listed under its own version '{VersionOf(copyProfileId)}'.");

            // and the list of twins still holds exactly one row for the name
            Assert.IsTrue(DataPersistenceManager.instance.GetAllProfileNamesGameData().ContainsKey("LipEdema"),
                "The twin is missing from the twin list.");
        }

        static void CopyTwinDirectory(string fromProfileId, string toProfileId)
        {
            string from = Path.Combine(DataPaths.PersistentDataPath, fromProfileId);
            string to = Path.Combine(DataPaths.PersistentDataPath, toProfileId);
            Directory.CreateDirectory(to);
            foreach (string file in Directory.GetFiles(from))
            {
                File.Copy(file, Path.Combine(to, Path.GetFileName(file)), true);
            }
        }

        /// <summary>A broken archive must leave the twins on the device alone, even the one
        /// whose name it carries.</summary>
        [UnityTest]
        public IEnumerator Import_OfABrokenZip_KeepsTheTwinsOnTheDevice()
        {
            yield return LoadLipEdemaTwin();
            string existingProfileId = DataPersistenceManager.instance.selectedProfileId;

            // named after the twin on purpose: an import must not touch the directory whose
            // name the archive happens to carry
            string brokenZip = Path.Combine(DataPaths.PersistentDataPath, existingProfileId + ".zip");
            File.WriteAllText(brokenZip, "this is not a zip file");

            Assert.IsNull(DataPersistenceManager.instance.ImportConfig(brokenZip),
                "A broken archive must not produce a twin.");
            Assert.IsTrue(TwinDirectoryExists(existingProfileId),
                "A broken archive must not damage the twin it is named after.");
            AssertConfigMatchesDirectory(existingProfileId);
        }

        /// <summary>Rebuilds the twin list, the way the app does it after an import (see the
        /// callback in ConfigManager.ImportTwin). A list that is already on screen is only
        /// rebuilt when asked to, since the save screen fills it when it becomes active.
        /// Yields a frame, because the rows it replaces are destroyed at the end of it.</summary>
        static IEnumerator RefreshTwinList()
        {
            FileManager[] fileManagers = Object.FindObjectsOfType<FileManager>(true);
            Assert.IsNotEmpty(fileManagers, "No FileManager found in the scene.");
            foreach (FileManager fileManager in fileManagers)
            {
                fileManager.Refresh();
            }
            yield return null;
        }

        /// <summary>Creates a twin through the save screen and leaves the app on it. The name has
        /// to pass TwinNameValidator, which allows at most 11 characters.</summary>
        IEnumerator CreateTwin(string name)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", name);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");
        }

        static bool TwinDirectoryExists(string profileId)
        {
            return Directory.Exists(Path.Combine(DataPaths.PersistentDataPath, profileId));
        }

        /// <summary>The app identifies a twin by its directory name, while the twin list shows
        /// the name and version from its config. If those disagree, the twin shows up in the
        /// list but cannot be selected.</summary>
        static void AssertConfigMatchesDirectory(string profileId)
        {
            var profiles = DataPersistenceManager.instance.GetAllProfilesGameData();
            Assert.IsTrue(profiles.ContainsKey(profileId), $"No twin directory '{profileId}'.");
            ConfigData data = profiles[profileId];
            Assert.AreEqual(profileId, data.name + "." + data.version,
                $"The config of '{profileId}' names a different twin, so the list cannot select it.");
        }

        /// <summary>Waits until the app has cached the texture of the twin.</summary>
        static IEnumerator WaitForCachedTexture(string profileId, float timeout = 5f)
        {
            float elapsed = 0f;
            while (!CwCommon.SaveExists(profileId) && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(CwCommon.SaveExists(profileId),
                $"The texture of '{profileId}' was not rebuilt and cached within {timeout}s.");
        }

        /// <summary>Reads the current state of the body texture.</summary>
        static Color32[] BodyPixels()
        {
            CwPaintableTexture texture = BodyTexture();
            Texture2D copy = texture.GetReadableCopy();
            Assert.IsNotNull(copy, "Could not read the body texture.");
            Color32[] pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        static CwPaintableTexture BodyTexture()
        {
            Body body = Object.FindObjectOfType<Body>();
            Assert.IsNotNull(body, "No Body found in the scene.");
            CwPaintableTexture texture = body.GetComponent<CwPaintableTexture>();
            Assert.IsNotNull(texture, "The Body has no paintable texture.");
            return texture;
        }

        static int DifferingPixels(Color32[] before, Color32[] after)
        {
            Assert.AreEqual(before.Length, after.Length, "The body texture changed its size.");
            int differing = 0;
            for (int i = 0; i < before.Length; i++)
            {
                if (before[i].r != after[i].r || before[i].g != after[i].g
                    || before[i].b != after[i].b || before[i].a != after[i].a)
                {
                    differing++;
                }
            }
            return differing;
        }
    }
}
