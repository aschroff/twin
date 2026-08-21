using System.Collections;
using System.IO;
using Lean.Gui;
using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    /*
     * Picks the document whose findings are to be mapped onto the twin. A photo comes from the
     * gallery picker the sticker upload uses, a document from the OS file picker the twin import
     * uses, so the user meets the dialog they already know from those places.
     *
     * This is the first step of the feature - the pick is only reported. Sending the document to
     * the LLM together with the twin context (groups, tools, markers, fillers, body regions) and
     * turning the answer into groups and painted regions follows.
     */
    public class DocumentUploadProcess : Process
    {
        public const string VariantPhoto = "Photo";
        public const string VariantDocument = "Document";

        /// <summary>The screen the pick is handed to.</summary>
        [SerializeField] private DocumentReviewManager review;

        /// <summary>Edge length a picked photo is loaded at, as in the sticker upload.</summary>
        private const int maxPhotoSize = 2048;

        /// <summary>The file the last pick delivered - the input of the analysis step.</summary>
        public string pickedPath;

        /// <summary>The picked photo, already loaded; null when a document was picked.</summary>
        public Texture2D pickedPhoto;

        /// <summary>What went out for the last pick, and what came back - the input of the step
        /// that writes the confirmed items to the twin.</summary>
        public string lastPrompt;

        public DocumentMapping lastMapping;

        public override ProcessResult Execute(string variant = "")
        {
            switch (variant)
            {
                case VariantPhoto:
                    PickPhoto();
                    break;
                case VariantDocument:
                    PickDocument();
                    break;
                default:
                    Debug.LogError("DocumentUploadProcess: unknown variant '" + variant + "'");
                    break;
            }

            return new ProcessResult();
        }

        /*
         * Same gallery picker the sticker upload uses, so a photo of a document can be taken with
         * the camera app first and is then picked here.
         */
        private void PickPhoto()
        {
            NativeGallery.GetImageFromGallery(path =>
            {
                if (path == null)
                {
                    Debug.Log("DocumentUploadProcess: no photo picked");
                    return;
                }

                Texture2D texture = NativeGallery.LoadImageAtPath(path, maxPhotoSize, false);
                if (texture == null)
                {
                    Debug.Log("DocumentUploadProcess: could not load photo from " + path);
                    Report("The picked photo could not be read.");
                    return;
                }

                pickedPath = path;
                pickedPhoto = texture;
                Debug.Log("DocumentUploadProcess: photo picked " + path + " (" + texture.width + "x" + texture.height + ")");
                Accept();
            });
        }

        /*
         * Same file picker the twin import uses. Only PDF is offered for now; the analysis step
         * decides what else it can read.
         */
        private void PickDocument()
        {
            if (NativeFilePicker.IsFilePickerBusy())
            {
                Debug.Log("DocumentUploadProcess: file picker is busy");
                return;
            }

            NativeFilePicker.PickFile(path =>
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    Debug.Log("DocumentUploadProcess: no document picked, path was " + path);
                    return;
                }

                pickedPath = path;
                pickedPhoto = null;
                Debug.Log("DocumentUploadProcess: document picked " + path);
                Accept();
            }, NativeFilePicker.ConvertExtensionToFileType("pdf"));
        }

        /*
         * The pick is done: the document goes to the LLM and the answer to the review screen,
         * which is where the user will say what may reach the twin. Nothing is written to the
         * twin here.
         */
        private void Accept()
        {
            if (review == null)
            {
                Debug.LogError("DocumentUploadProcess: no review screen wired.");
                Report("The picked document cannot be shown.");
                return;
            }

            lastPrompt = DocumentPromptBuilder.Build(getPartManager(), getSettingsManager());
            review.Show(PickedSummary() + "\n\nReading the document ...");
            // the reading takes a while and the user may well look at the twin meanwhile
            Report("Reading " + Path.GetFileName(pickedPath) + " ...");
            StartCoroutine(Analyse());
        }

        /*
         * An image is embedded in the request; everything else has to be uploaded first and is
         * then referenced by its id.
         */
        private IEnumerator Analyse()
        {
            AI.AI ai = getAI();
            string fileId = null;

            if (pickedPhoto == null)
            {
                bool failed = false;
                yield return ai.UploadDocumentCoroutine(pickedPath,
                    id => fileId = id,
                    error => { failed = true; Failed("The document could not be uploaded", error); });
                if (failed)
                {
                    yield break;
                }
            }

            ai.MapDocument(
                lastPrompt,
                pickedPhoto != null ? pickedPath : null,
                fileId,
                DocumentPromptBuilder.RegionKeys(),
                mapping => Mapped(mapping),
                error => Failed("The document could not be read", error));
        }

        private void Mapped(DocumentMapping mapping)
        {
            lastMapping = mapping;
            int findings = mapping != null && mapping.Paintings != null ? mapping.Paintings.Count : 0;
            int groups = mapping != null && mapping.NewGroups != null ? mapping.NewGroups.Count : 0;
            Debug.Log("DocumentUploadProcess: mapping received, " + findings + " findings");
            review.Show(PickedSummary() + "\n\n" + DocumentMappingText.Describe(mapping));
            // the second notification: says it is done even when the user left the screen
            Report(Path.GetFileName(pickedPath) + ": " + findings + " findings"
                + (groups > 0 ? ", " + groups + " new groups" : "") + " to review");
        }

        private void Failed(string what, string error)
        {
            Debug.LogError("DocumentUploadProcess: " + what + ": " + error);
            review.Show(PickedSummary() + "\n\n" + what + ".\n\n" + error);
            Report(what + " - see the upload screen");
        }

        /// <summary>Runs the analysis for a document that was not picked by hand - the way a test
        /// drives this without an OS dialog.</summary>
        public void ShowPicked(string path, Texture2D photo)
        {
            pickedPath = path;
            pickedPhoto = photo;
            Accept();
        }

        /// <summary>
        /// Puts the prompt for a document on the review screen without sending anything. The way
        /// to read on the device what would go out for this twin - and what a test can check
        /// without touching the network.
        /// </summary>
        public void ShowPromptFor(string path, Texture2D photo)
        {
            pickedPath = path;
            pickedPhoto = photo;

            if (review == null)
            {
                Debug.LogError("DocumentUploadProcess: no review screen wired.");
                return;
            }

            lastPrompt = DocumentPromptBuilder.Build(getPartManager(), getSettingsManager());
            review.Show(PickedSummary()
                + "\n\n--- the prompt that would be sent (" + lastPrompt.Length + " characters) ---\n\n"
                + lastPrompt);
        }

        private string PickedSummary()
        {
            if (string.IsNullOrEmpty(pickedPath))
            {
                return "Nothing picked.";
            }

            string fileName = Path.GetFileName(pickedPath);
            if (pickedPhoto != null)
            {
                return "Picked photo: " + fileName
                    + " (" + pickedPhoto.width + " x " + pickedPhoto.height + " pixels)";
            }

            string size = File.Exists(pickedPath)
                ? " (" + (new FileInfo(pickedPath).Length / 1024) + " kB)"
                : "";
            return "Picked document: " + fileName + size;
        }

        private void Report(string message)
        {
            LeanPulse notification = getNotification();
            foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
            {
                text.text = message;
            }

            notification.Pulse();
        }
    }
}
