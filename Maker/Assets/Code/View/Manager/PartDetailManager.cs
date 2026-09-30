using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;
using CW.Common;
using Lean.Gui;
using PaintIn3D;
using UnityEngine.UI;

namespace Code
{
    /// <summary>
    /// The detail page of a single part: its picture, its text, and the three things one can do
    /// with it - shoot the picture, have it described, throw it away.
    /// </summary>
    public class PartDetailManager: MonoBehaviour
    {
        [SerializeField] public DataPersistenceManager dataManager;
        [SerializeField] public PartDescriptionProcess partDescriptionProcess;

        void OnEnable()
        {
            Display();
        }

    
        // Start is called before the first frame update
        void Start()
        {
            Display();
        }

        //get called in Part UI when generating new part description without starting the whole summary process in helpUI
        public void DescribePart()
        {
            PartManager.PartData partdata = InteractionController.Partdata;
            if (partdata == null)
            {
                return;
            }
            partDescriptionProcess.Execute("Part Description" + "##" + partdata.id); 
            Display();
        }

        /// <summary>
        /// Shoots the picture of this part. Wired to the Screenshot entry of the part menu.
        /// </summary>
        /// <remarks>
        /// Without a picture the Describe entry can do nothing - the model is asked about the
        /// picture of the part, and <see cref="PartDescriptionProcess"/> does not even send a
        /// request when the file is missing. On a device that has never run an AI summary no part
        /// has one, which is why describing a part worked in the editor and not on an iPad.
        /// </remarks>
        public void CreateScreenshot()
        {
            PartManager.PartData partdata = InteractionController.Partdata;
            if (partdata == null)
            {
                return;
            }

            PartManager partManager = Object.FindFirstObjectByType<PartManager>();
            PartsScreenshotProcess screenshots = Object.FindFirstObjectByType<PartsScreenshotProcess>();
            if (partManager == null || screenshots == null)
            {
                Debug.LogWarning("[PartDetailManager] No PartManager or PartsScreenshotProcess - cannot shoot.");
                return;
            }

            PartManager.GroupData group = partManager.getGroup(partdata);
            if (group == null)
            {
                Debug.LogWarning("[PartDetailManager] The part sits in no group - cannot name its file.");
                return;
            }

            /*
             * On the process, never on this object. The run hides the whole canvas while it works
             * (Recorder.Prepare deactivates every child of Canvas, and this page is one of them),
             * and Unity stops the coroutines of a GameObject it deactivates. Started here, the run
             * would die the moment the page went away - the screenshot itself still happens,
             * because that part runs on the process, but nothing ever puts the canvas back and the
             * app is stuck on the bare body. MissingScreenshotsButton works for the same reason in
             * reverse: it starts the run on the process too.
             *
             * Nothing to do afterwards either: Reset reactivates this page, and OnEnable calls
             * Display, which is what puts the fresh picture on it.
             */
            screenshots.StartCoroutine(screenshots.ShootPart(partdata, group));
        }

        /// <summary>
        /// Throws this part away and goes back to the body. Wired to the Delete entry.
        /// </summary>
        /// <remarks>No question asked, exactly as deleting from the part list does
        /// (<c>PartEntry.delete</c>). There is no undo for it either way: the history only covers
        /// parts painted in this session.</remarks>
        public void DeletePart()
        {
            PartManager.PartData partdata = InteractionController.Partdata;
            if (partdata == null)
            {
                return;
            }

            PartManager partManager = Object.FindFirstObjectByType<PartManager>();
            if (partManager == null)
            {
                Debug.LogWarning("[PartDetailManager] No PartManager - nothing to delete from.");
                return;
            }

            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>();
            if (viewManager != null && partdata.view != null)
            {
                viewManager.select(partdata.view);
            }

            partManager.deletePart(partdata);
            partManager.Erase();
            partManager.Refresh();

            // the page is built from this every time it opens, and the part is gone
            InteractionController.Partdata = null;
            InteractionController.EnableMode("Main");
        }

        void Display()
        {
            PartManager.PartData partdata = InteractionController.Partdata;
            if (partdata == null)
            {
                return;
            }

            InputField input_field = this.transform.GetComponentInChildren<InputField>();
            if (input_field != null)
            {
                input_field.text = partdata.description;
            }

            GameObject icon = transform.Find("Icon").gameObject;
            GameObject placeholder = transform.Find("Placeholder").gameObject;

            string fullPath = ScreenshotPath(partdata);
            if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
            {
                byte[] imageBytes = File.ReadAllBytes(fullPath);
                Texture2D loadedTexture = new Texture2D(1024, 1024);
                loadedTexture.LoadImage(imageBytes);

                // both of these have to be set every time: the page is one object that every part
                // passes through, so whatever the part before it left behind is what this one gets
                icon.SetActive(true);
                placeholder.SetActive(false);

                Image iconImage = icon.GetComponent<Image>();
                Sprite sprite = Sprite.Create(loadedTexture,
                    new Rect(0, 0, loadedTexture.width, loadedTexture.height),
                    new Vector2(0.5f, 0.5f), 100.0f);
                iconImage.sprite = sprite;
            }
            else
            {
                icon.SetActive(false);
                placeholder.SetActive(true);
            }
        }

        /// <summary>Where this part's picture lives, or empty when it sits in no group.</summary>
        private string ScreenshotPath(PartManager.PartData partdata)
        {
            PartManager.GroupData group = partdata.group;
            if (group == null)
            {
                PartManager partManager = Object.FindFirstObjectByType<PartManager>();
                group = partManager != null ? partManager.getGroup(partdata) : null;
            }
            if (group == null || dataManager == null)
            {
                return "";
            }

            string name = dataManager.selectedProfileId + " - " + group.name + " - part " + partdata.id;
            return Path.Combine(DataPaths.PersistentDataPath, dataManager.selectedProfileId,
                "screenshot_" + name + ".png");
        }
        
        public void PartChanged()
        {
            PartManager.PartData partdata = InteractionController.Partdata;
            if (partdata == null)
            {
                return;
            }
            InputField input_field = this.transform.GetComponentInChildren<InputField>();
            if (input_field != null)
            {
                partdata.description = input_field.text;
            }
           
        }
    }
}
