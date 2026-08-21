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
            if (Directory.Exists(ShotsDir))
                Directory.Delete(ShotsDir, recursive: true);
            Directory.CreateDirectory(ShotsDir);

            yield return CaptureShot("main-with-upload-button");

            yield return ClickButtonByPath(UploadButton);
            yield return WaitForModeActive("Upload");
            AssertGameObjectActive(UploadPanel);

            var menu = FindGameObjectByPath(UploadPanel).GetComponent<MenuManager>();
            Assert.IsNotNull(menu, "The upload panel is expected to list its actions via a MenuManager.");
            Assert.AreEqual(2, menu.menu.Count, "Upload offers exactly a photo and a document.");

            AssertEntry(menu, "UPLOAD_PHOTO", DocumentUploadProcess.VariantPhoto);
            AssertEntry(menu, "UPLOAD_DOCUMENT", DocumentUploadProcess.VariantDocument);

            AssertDirectChildCount(UploadPanel, 2);
            yield return CaptureShot("upload-panel");

            yield return ClickButtonByPath(UploadBackButton);
            yield return WaitForModeActive("Main");
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

        private IEnumerator CaptureShot(string shotName)
        {
            yield return new WaitForEndOfFrame();
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
