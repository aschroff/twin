using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Adding a version to the open twin (manual tests 11/01 New version, 11/02 Clone version).
    /// There are three ways to do it, all in <see cref="ConfigManager"/>:
    /// the New Version button of the main screen and the New button of the versions screen both
    /// start an empty version - no groups, no parts - and differ only in where the label comes
    /// from; the Save as button of the versions screen clones the open version with everything
    /// in it. In all three the version that was open has to stay as it was.
    /// </summary>
    [Category(Processes.ExchangeTwins)]
    public class NewVersionPlayModeTests : TwinPaintTestBase
    {
        const string NewVersionButton = "Canvas/Main UI/Bottom/NewVersion/Icon";
        const string VersionsButton = "Canvas/Main UI/Bottom/Versions/Icon";
        const string VersionsNewButton = "Canvas/Twin versions UI/Bottom/Buttons/config/New/New";
        const string VersionsSaveAsButton = "Canvas/Twin versions UI/Bottom/Buttons/config/New/Save as";
        const string VersionInput = "Canvas/Twin versions UI/Bottom/Buttons/config/New/InputFieldVersion";

        /// <summary>The DATE_VERSION_SMART format, MM-dd-yy-mmss, the same in all five locales.
        /// It has no dot in it, which TwinNameValidator would refuse.</summary>
        const string DateVersionPattern = @"^\d{2}-\d{2}-\d{2}-\d{4}$";

        const string CloneLabel = "clone";

        /// <summary>A twin created in the test, short enough for TwinNameValidator.</summary>
        const string OwnTwin = "abc";

        /// <summary>The New Version button of the main screen starts an empty version, named after
        /// the date - the same as New on the versions screen, without asking for the label.
        /// </summary>
        [UnityTest]
        public IEnumerator NewVersionOnMainScreen_StartsAnEmptyVersion()
        {
            yield return LoadLipEdemaTwin();
            yield return PaintOnePart("Red");
            string original = SelectedProfileId;

            yield return ClickButtonByPath(NewVersionButton);
            yield return WaitForModeActive("Main");

            string created = SelectedProfileId;
            Assert.AreNotEqual(original, created, "The new version should be the open one.");
            Assert.AreEqual("LipEdema", NameOf(created), "The new version should belong to the same twin.");
            StringAssert.IsMatch(DateVersionPattern, VersionOf(created), "The new version should be named after the date.");
            yield return AssertNewVersionIsEmpty(original, created);
        }

        /// <summary>The New button of the versions screen takes the label from the input, which
        /// proposes the date, and starts an empty version.</summary>
        [UnityTest]
        public IEnumerator NewOnTheVersionsScreen_StartsAnEmptyVersion()
        {
            yield return LoadLipEdemaTwin();
            yield return PaintOnePart("Red");
            string original = SelectedProfileId;

            yield return OpenVersionsScreen();
            string proposed = FindGameObjectByPath(VersionInput).GetComponent<InputField>().text;
            StringAssert.IsMatch(DateVersionPattern, proposed, "The versions screen should propose the date as the label.");

            yield return ClickButtonByPath(VersionsNewButton);
            yield return WaitForModeActive("Main");

            Assert.AreEqual("LipEdema." + proposed, SelectedProfileId, "The new version should be the open one.");
            yield return AssertNewVersionIsEmpty(original, "LipEdema." + proposed);
        }

        /// <summary>What both New buttons have to leave behind: an open version without groups and
        /// parts, on disk too, listed next to the version that was open - which keeps what it had.
        /// Expects the new version to be the open one.</summary>
        IEnumerator AssertNewVersionIsEmpty(string original, string created)
        {
            Assert.AreEqual(0, FindPartManager().groups.Count, "The new version should start without groups.");
            Assert.AreEqual(0, PartCount(), "The new version should start without parts.");

            // both are saved when switching between them, so this is what is on disk
            yield return OpenVersion(original);
            Assert.AreEqual(1, PartCount(), "The version that was open lost its part.");
            yield return OpenVersion(created);
            Assert.AreEqual(0, FindPartManager().groups.Count, "The saved new version should hold no groups.");
            Assert.AreEqual(0, PartCount(), "The saved new version should hold no parts.");

            CollectionAssert.IsSubsetOf(new[] { VersionOf(original), VersionOf(created) }, VersionsOf("LipEdema"),
                "Both versions should be listed on the versions screen.");
            AssertConfigMatchesDirectory(created);
        }

        /// <summary>Save as on the versions screen clones the open version under the label from
        /// the input, groups and parts included, and opens the clone.</summary>
        [UnityTest]
        public IEnumerator CloneVersion_KeepsTheGroupsAndTheParts()
        {
            yield return LoadLipEdemaTwin();
            yield return PaintOnePart("Red");
            string original = SelectedProfileId;
            List<string> groupNames = GroupNames();

            yield return CloneOpenVersion(CloneLabel);

            string clone = "LipEdema." + CloneLabel;
            Assert.AreEqual(clone, SelectedProfileId, "The clone should be the open one.");
            CollectionAssert.AreEqual(groupNames, GroupNames(), "The clone should keep the groups.");
            Assert.AreEqual(1, PartCount(), "The clone should keep the part.");
            AssertPartsAreUsable(FindPartManager());

            yield return OpenVersion(original);
            Assert.AreEqual(1, PartCount(), "The version that was cloned lost its part.");
            yield return OpenVersion(clone);
            Assert.AreEqual(1, PartCount(), "The saved clone lost its part.");

            CollectionAssert.IsSubsetOf(new[] { VersionOf(original), CloneLabel }, VersionsOf("LipEdema"),
                "Both versions should be listed on the versions screen.");
            AssertConfigMatchesDirectory(clone);
        }

        /// <summary>A clone is a version of its own: what is added to it does not show up in the
        /// version it was cloned from.</summary>
        [UnityTest]
        public IEnumerator CloneVersion_IsEditedWithoutTouchingTheOriginal()
        {
            yield return LoadLipEdemaTwin();
            yield return PaintOnePart("Red");
            string original = SelectedProfileId;

            yield return CloneOpenVersion(CloneLabel);
            yield return PaintOnePart("Green");
            Assert.AreEqual(2, PartCount(), "Setup: the clone should hold two parts.");

            yield return OpenVersion(original);
            Assert.AreEqual(1, PartCount(), "A part added to the clone showed up in the original.");
            yield return OpenVersion("LipEdema." + CloneLabel);
            Assert.AreEqual(2, PartCount(), "The clone lost the part added to it.");
        }

        /// <summary>A twin created in the app starts at version 000, and TwinNameValidator
        /// refuses a clone onto it: the app stays on the versions screen and nothing is written.
        /// (The sample twins ship with other versions, so for them 000 is a free label.)</summary>
        [UnityTest]
        public IEnumerator CloneVersion_OntoVersion000_IsRefused()
        {
            yield return ResetApp();
            yield return CreateTwin(OwnTwin);
            Assert.AreEqual(OwnTwin + ".000", SelectedProfileId, "Setup: a new twin starts at version 000.");

            yield return OpenVersionsScreen();
            SetInputByName("InputFieldVersion", "000");
            yield return ClickButtonByPath(VersionsSaveAsButton);

            Assert.AreEqual(OwnTwin + ".000", SelectedProfileId, "A refused clone must leave the open version open.");
            Assert.AreEqual(1, VersionsOf(OwnTwin).Count, "A refused clone must not add a version.");
            AssertModeActive("Version");
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

        /// <summary>Paints one part into the first group and returns to the main screen. A
        /// different marker per call, or the app adds the paint to the part before.</summary>
        IEnumerator PaintOnePart(string marker)
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker(marker);
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Opens the versions screen of the open twin from the main screen.</summary>
        IEnumerator OpenVersionsScreen()
        {
            yield return ClickButtonByPath(VersionsButton);
            yield return WaitForModeActive("Version");
        }

        IEnumerator CloneOpenVersion(string label)
        {
            yield return OpenVersionsScreen();
            SetInputByName("InputFieldVersion", label);
            yield return ClickButtonByPath(VersionsSaveAsButton);
            yield return WaitForModeActive("Main");
        }

        /// <summary>Opens a version the way the versions screen does. Saves the one that was
        /// open first.</summary>
        static IEnumerator OpenVersion(string profileId)
        {
            DataPersistenceManager.instance.ChangeSelectedProfileId(profileId);
            yield return null;
            Assert.AreEqual(profileId, SelectedProfileId, $"Could not open '{profileId}'.");
        }

        static string SelectedProfileId => DataPersistenceManager.instance.selectedProfileId;

        static List<string> GroupNames()
        {
            return FindPartManager().groups.Select(group => group.name).ToList();
        }

        static int PartCount()
        {
            return FindPartManager().groups.Sum(group => group.groupParts.Count);
        }

        static ICollection<string> VersionsOf(string twinName)
        {
            return DataPersistenceManager.instance.GetAllVersionsGameData(twinName).Keys;
        }

        static string NameOf(string profileId)
        {
            return profileId.Remove(profileId.LastIndexOf("."));
        }

        static string VersionOf(string profileId)
        {
            return profileId.Substring(profileId.LastIndexOf(".") + 1);
        }

        /// <summary>The twin list shows name and version from the config, the app finds the
        /// version by its directory - if they disagree, the version cannot be selected.</summary>
        static void AssertConfigMatchesDirectory(string profileId)
        {
            var profiles = DataPersistenceManager.instance.GetAllProfilesGameData();
            Assert.IsTrue(profiles.ContainsKey(profileId), $"No twin directory '{profileId}'.");
            ConfigData data = profiles[profileId];
            Assert.AreEqual(profileId, data.name + "." + data.version,
                $"The config of '{profileId}' names a different version, so the list cannot select it.");
        }
    }
}
