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

        /// <summary>Back to the proposal that is already in hand - no pick, no second API call.</summary>
        public const string VariantReview = "Review";

        /// <summary>Localization key of the Upload entry that leads back to the review screen. The
        /// entry is only offered while there is something to go back to.</summary>
        public const string ReviewEntryKey = "UPLOAD_REVIEW";

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

        /// <summary>What the last Apply did - what a test reads back.</summary>
        public DocumentApplyResult lastResult;

        /// <summary>Everything of <see cref="lastMapping"/> that has already been written to the
        /// twin, across every Apply. Those items come back ticked and locked, and are never applied
        /// a second time - so leaving the screen with something still ticked costs nothing.</summary>
        public DocumentMappingSelection applied = new DocumentMappingSelection();

        /// <summary>The twin <see cref="lastMapping"/> was made for. A mapping outlives the screen
        /// and would otherwise outlive the twin too: applying it to another one would paint this
        /// document's findings onto a different body, with group and tool names that mean something
        /// else there.</summary>
        public string mappingProfile;

        /*
         * The Upload panel offers a way back to the review screen, but only while there is a
         * proposal to go back to - an entry that can do nothing should not invite a tap. The panel
         * rebuilds its entries every time it opens, so this answer is asked afresh each time.
         *
         * The panel is found rather than wired: it is the panel whose menu carries this entry, which
         * is a fact about the menu and not something a scene reference could keep more truthfully.
         */
        private void Awake()
        {
            foreach (MenuManager menu in FindObjectsOfType<MenuManager>(true))
            {
                if (menu.menu == null) continue;
                foreach (MenuAction action in menu.menu.Values)
                {
                    if (action != null && action.text == ReviewEntryKey)
                    {
                        menu.entryAvailable = key => key != ReviewEntryKey || CanReview();
                    }
                }
            }
        }

        /// <summary>Whether there is a proposal to go back to: one has been read, and it was read
        /// for the twin that is open now.</summary>
        public bool CanReview()
        {
            return lastMapping != null && MappingBelongsToTheOpenTwin();
        }

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
                case VariantReview:
                    ShowReview();
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
            applied = new DocumentMappingSelection();
            mappingProfile = CurrentProfile();
            int findings = mapping != null && mapping.Paintings != null ? mapping.Paintings.Count : 0;
            int groups = mapping != null && mapping.NewGroups != null ? mapping.NewGroups.Count : 0;
            Debug.Log("DocumentUploadProcess: mapping received, " + findings + " findings");
            ShowReview();
            // the second notification: says it is done even when the user left the screen
            Report(Path.GetFileName(pickedPath) + ": " + findings + " findings"
                + (groups > 0 ? ", " + groups + " new groups" : "") + " to review");
        }

        /*
         * Puts the proposal that is already in hand back on the screen. The mapping outlives the
         * screen, so leaving it - with items still ticked, or by accident - costs neither a pick nor
         * a second API call. What has already been applied comes back ticked and locked.
         */
        public void ShowReview()
        {
            if (review == null)
            {
                Debug.LogError("DocumentUploadProcess: no review screen wired.");
                Report("The picked document cannot be shown.");
                return;
            }
            if (lastMapping == null)
            {
                Report("No document has been read yet.");
                return;
            }
            if (!MappingBelongsToTheOpenTwin())
            {
                Report("That document was read for another twin - upload it again for this one.");
                return;
            }

            // only what was picked - the screen writes the proposal out itself, so it stays in step
            // with a group that was changed on it
            review.Show(lastMapping, PickedSummary(), selection => ApplyConfirmed(selection), applied);
        }

        /*
         * Apply on the review screen: what the user ticked is written to the twin, the twin is shown
         * again, and the screen closes. Seeing the paint appear on the body is the point of applying,
         * and nothing is lost by leaving - "Continue review" brings the rest of the proposal back,
         * with what was just applied ticked and locked.
         *
         * Only what is not applied yet is written, so pressing Apply twice cannot paint the same
         * region again - which is what makes coming back safe. The result is saved right away:
         * painting by hand is saved by leaving the screen, and a change nobody made by hand should
         * not depend on the user finding that out.
         */
        public DocumentApplyResult ApplyConfirmed(DocumentMappingSelection selection)
        {
            if (lastMapping == null)
            {
                Report("There is nothing to apply.");
                return new DocumentApplyResult();
            }
            if (!MappingBelongsToTheOpenTwin())
            {
                Report("That document was read for another twin - nothing was applied.");
                return new DocumentApplyResult();
            }

            DocumentMappingSelection todo = (selection ?? DocumentMappingSelection.Nothing()).Without(applied);
            if (todo.Count == 0)
            {
                Report(selection != null && selection.Count > 0
                    ? "Everything that was ticked is already on the twin."
                    : "Nothing was ticked, so nothing was applied.");
                return new DocumentApplyResult();
            }

            DocumentApplyResult result = DocumentMappingApplier.Apply(
                lastMapping, todo, getPartManager(), getSettingsManager());
            lastResult = result;

            foreach (string problem in result.problems)
            {
                Debug.LogWarning("DocumentUploadProcess: " + problem);
            }

            if (result.ChangedAnything)
            {
                // what actually reached the twin, not what was asked for: an item that was refused
                // must stay offerable rather than come back locked as if it were painted
                applied.Merge(result.written);
                getDataManager().SaveConfig();
                // the twin, so the result is looked at on the body. The rows are left as they are:
                // coming back rebuilds them, and the applied ones lock then.
                InteractionController.EnableMode("Main");
            }

            Report(Path.GetFileName(pickedPath) + ": " + result.Summary());
            return result;
        }

        /// <summary>The twin that was open when the document was read is the only twin its findings
        /// mean anything on.</summary>
        private bool MappingBelongsToTheOpenTwin()
        {
            return string.IsNullOrEmpty(mappingProfile) || mappingProfile == CurrentProfile();
        }

        private string CurrentProfile()
        {
            DataPersistenceManager data = getDataManager();
            return data != null ? data.selectedProfileId : null;
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

        /// <summary>Puts a mapping on the review screen without calling the API - how a test
        /// reaches the review list and the Apply button.</summary>
        public void ShowMapping(string path, DocumentMapping mapping)
        {
            pickedPath = path;
            pickedPhoto = null;
            Mapped(mapping);
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
