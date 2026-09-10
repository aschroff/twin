using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Code;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The Upload button on the main screen offers the two ways a document can reach the twin —
    /// a photo and a file. The pick itself opens an OS dialog and is therefore not driven here;
    /// what is checked is that both ways are offered and wired to the upload process.
    ///
    /// Screenshots for visual QA are written to:
    ///   Application.temporaryCachePath/UploadShots/
    /// The directory is created on demand and no longer cleared, so shots from the previous run
    /// are simply overwritten - clearing it was what made one test depend on the other's order.
    /// </summary>
    public class UploadPlayModeTests : PlayModeTestBase
    {
        private const string UploadButton = "Canvas/Main UI/Bottom/Upload/Icon";
        private const string UploadPanel = "Canvas/Upload UI/Bottom/Scroll/Panel";
        private const string UploadBackButton = "Canvas/Upload UI/Top/GameObject/Back Button";

        private static string ShotsDir => Path.Combine(Application.temporaryCachePath, "UploadShots");

        [UnityTest]
        public IEnumerator UploadButton_OffersPhotoAndDocument()
        {
            yield return CaptureShot("main-with-upload-button");

            yield return ClickButtonByPath(UploadButton);
            yield return WaitForModeActive("Upload");
            AssertGameObjectActive(UploadPanel);

            var menu = FindGameObjectByPath(UploadPanel).GetComponent<MenuManager>();
            Assert.IsNotNull(menu, "The upload panel is expected to list its actions via a MenuManager.");
            Assert.AreEqual(3, menu.menu.Count,
                "Upload can offer a photo, a document, and the way back to a proposal in hand.");

            AssertEntry(menu, "UPLOAD_PHOTO", DocumentUploadProcess.VariantPhoto);
            AssertEntry(menu, "UPLOAD_DOCUMENT", DocumentUploadProcess.VariantDocument);
            // the third is not a pick: it reopens the proposal that is still in memory, so leaving
            // the review screen never costs an upload or a second call to the API
            AssertEntryIsConfigured(menu, DocumentUploadProcess.ReviewEntryKey,
                DocumentUploadProcess.VariantReview);

            // ...but nothing has been read yet, so it is not offered - an entry that could only
            // answer "no document has been read" has no business being tappable
            Assert.AreEqual(2, CountRows(), "Only the two picks are offered before anything is read.");
            Assert.IsNull(FindRowWithText(StringLocalizer.localizeString(DocumentUploadProcess.ReviewEntryKey)),
                "The way back may not be offered while there is nothing to go back to.");
            yield return CaptureShot("upload-panel");

            yield return ClickButtonByPath(UploadBackButton);
            yield return WaitForModeActive("Main");
        }

        /// <summary>Once a document has been read, the way back to its proposal is offered - and it
        /// goes away again when a twin it does not belong to is opened.</summary>
        [UnityTest]
        public IEnumerator ContinueReview_IsOfferedOnlyWhenThereIsSomethingToGoBackTo()
        {
            var upload = Object.FindObjectOfType<DocumentUploadProcess>(true);
            Assert.IsNotNull(upload, "DocumentUploadProcess not found.");
            Assert.IsFalse(upload.CanReview(), "Nothing has been read at the start of a session.");

            // a proposal arrives, without an upload dialog or a call to the API
            upload.ShowMapping("report.pdf", new Code.DocumentMapping
            {
                DocumentSummary = "A fictional report, for the tests.",
                PatientText = "Something about the patient.",
            });
            yield return WaitForModeActive("UploadReview");
            Assert.IsTrue(upload.CanReview());

            yield return ClickButtonByPath("Canvas/UploadReview UI/Back Button");
            yield return ClickButtonByPath(UploadButton);
            yield return WaitForModeActive("Upload");
            yield return null;

            string label = StringLocalizer.localizeString(DocumentUploadProcess.ReviewEntryKey);
            Assert.AreEqual(3, CountRows(), "The way back joins the two picks.");
            Assert.IsNotNull(FindRowWithText(label), $"No row offers '{label}'.");
            yield return CaptureShot("upload-panel-with-review");

            // it belongs to the twin it was read for, so for another twin it is gone again
            upload.mappingProfile = "some other twin";
            Assert.IsFalse(upload.CanReview());
            yield return ClickButtonByPath(UploadBackButton);
            yield return WaitForModeActive("Main");
            yield return ClickButtonByPath(UploadButton);
            yield return WaitForModeActive("Upload");
            yield return null;

            Assert.AreEqual(2, CountRows(),
                "A proposal read for another twin may not be offered for this one.");
            Assert.IsNull(FindRowWithText(label));
        }

        /// <summary>How many entries the panel actually built.</summary>
        private int CountRows()
        {
            return FindGameObjectByPath(UploadPanel).transform.childCount;
        }

        /// <summary>An entry the panel *can* offer: its label resolves and its click carries the
        /// variant. Says nothing about whether it is offered right now.</summary>
        private void AssertEntryIsConfigured(MenuManager menu, string textKey, string variant)
        {
            MenuAction action = null;
            foreach (var candidate in menu.menu.Values)
            {
                if (candidate.text == textKey) action = candidate;
            }
            Assert.IsNotNull(action, $"No upload entry configured for '{textKey}'.");
            Assert.AreNotEqual(textKey, StringLocalizer.localizeString(textKey),
                $"'{textKey}' is not in the localization table.");
            Assert.AreEqual(1, action.onClick.GetPersistentEventCount());
            Assert.IsNotNull(action.onClick.GetPersistentTarget(0) as DocumentUploadProcess);
            Assert.AreEqual("Handle", action.onClick.GetPersistentMethodName(0));
            Assert.AreEqual(variant, GetStringArgument(action.onClick, 0));
        }

        /// <summary>
        /// One offered way: its label is a localization key that resolves, and its click reaches
        /// the upload process with the variant that picks that kind of document.
        /// </summary>
        private void AssertEntry(MenuManager menu, string textKey, string variant)
        {
            MenuAction action = null;
            foreach (var candidate in menu.menu.Values)
            {
                if (candidate.text == textKey) action = candidate;
            }
            Assert.IsNotNull(action, $"No upload entry labelled '{textKey}'.");
            Assert.IsNotNull(action.icon, $"Upload entry '{textKey}' has no icon.");

            var localized = StringLocalizer.localizeString(textKey);
            Assert.AreNotEqual(textKey, localized, $"'{textKey}' is not in the localization table.");

            Assert.AreEqual(1, action.onClick.GetPersistentEventCount(),
                $"Upload entry '{textKey}' should call exactly one method.");
            var target = action.onClick.GetPersistentTarget(0) as DocumentUploadProcess;
            Assert.IsNotNull(target, $"Upload entry '{textKey}' does not call the upload process.");
            Assert.AreEqual("Handle", action.onClick.GetPersistentMethodName(0));
            Assert.AreEqual(variant, GetStringArgument(action.onClick, 0),
                $"Upload entry '{textKey}' should pick a {variant}.");

            // the row the menu built shows the localized label
            var row = FindRowWithText(localized);
            Assert.IsNotNull(row, $"No row in the upload panel shows '{localized}'.");
        }

        /// <summary>
        /// Creates the directory itself rather than trusting a previous test to have done it.
        /// It used to be created - and deleted - inside <see cref="UploadButton_OffersPhotoAndDocument"/>,
        /// which NUnit runs *after* the other test in this class because it sorts alphabetically.
        /// The other test therefore wrote into a directory nobody had made, and only got away with
        /// it when an earlier run had left one behind: green on a machine that had run these tests
        /// before, red on a fresh one and in CI (TWIN-447).
        /// </summary>
        private IEnumerator CaptureShot(string shotName)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(ShotsDir);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(ShotsDir, shotName + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }

        /// <summary>
        /// The argument a persistent UnityEvent call carries — the only part of the wiring
        /// UnityEventBase does not expose, and the part that tells the two entries apart.
        /// </summary>
        private static string GetStringArgument(UnityEngine.Events.UnityEventBase evt, int index)
        {
            var callsField = typeof(UnityEngine.Events.UnityEventBase).GetField(
                "m_PersistentCalls", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(callsField, "UnityEventBase.m_PersistentCalls not found.");
            var group = callsField.GetValue(evt);

            var listField = group.GetType().GetField("m_Calls", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(listField, "PersistentCallGroup.m_Calls not found.");
            var calls = (IList)listField.GetValue(group);
            var call = calls[index];

            var argsField = call.GetType().GetField("m_Arguments", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(argsField, "PersistentCall.m_Arguments not found.");
            var args = argsField.GetValue(call);

            var stringField = args.GetType().GetField("m_StringArgument", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(stringField, "ArgumentCache.m_StringArgument not found.");
            return (string)stringField.GetValue(args);
        }

        private GameObject FindRowWithText(string localized)
        {
            var panel = FindGameObjectByPath(UploadPanel);
            foreach (var text in panel.GetComponentsInChildren<Text>(true))
            {
                if (text.text == localized) return text.gameObject;
            }
            return null;
        }
    }
}
