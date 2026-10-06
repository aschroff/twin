using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PaintIn3D;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The reset button in Settings, against the checklist the business department works through
    /// by hand (03 App reset, 05/01 step 4): a blank twin in the default position, every twin
    /// gone, the default camera, no groups, no stored views, the default stickers, and every text
    /// box back at its default.
    /// </summary>
    /// <remarks>
    /// <para>"Default" is not spelled out here but recorded: the setup resets an app that starts
    /// with an empty data directory and takes that state as the reference. The test then changes
    /// every one of the seven things, asserts that each change really took, resets again, and
    /// compares. A reset that forgot one of them leaves the change behind and names it.</para>
    /// </remarks>
    [Category(Processes.AppFrame)]
    public class AppResetPlayModeTests : TwinPaintTestBase
    {
        const string ViewPanel = "Canvas/Overlays/View Overlay/Scroll/Panel";
        const string StoreViewButton = "Canvas/Overlays/View Overlay/StoreView/Icon";
        const string DefaultProfile = "default.000";

        /// <summary>Kept short on purpose: TwinNameValidator rejects names over 11 characters.</summary>
        const string OwnTwin = "ResetTwin";
        const string OwnView = "ResetView";
        const string OwnMeaning = "ResetMeaning";
        const string MeaningTool = "Red";

        /// <summary>Everything the checklist looks at, read off the running app.</summary>
        class AppState
        {
            public string profile;
            public List<string> twins;
            public SceneManagement.View pose;
            public List<string> groups;
            public int parts;
            public List<string> views;
            public Dictionary<string, Texture> stickers;
            public Dictionary<string, string> textBoxes;
        }

        [UnityTest]
        public IEnumerator Reset_BringsEveryChecklistItemBackToTheFreshApp()
        {

            //resets the app and records that state as the reference
            yield return ResetApp();
            AppState fresh = null;
            yield return ReadState(s => fresh = s);
            Assert.AreEqual(DefaultProfile, fresh.profile, "Setup: a fresh app should be on the default twin.");
            Assert.IsEmpty(fresh.groups, "Setup: the default twin should have no groups.");
            Assert.AreEqual(1, fresh.views.Count, "Setup: a fresh app should offer only the default view.");

            //creates a twin "ResetTwin" and select "LipEdema"
            //paints a part, stores a view, moves and turns the twin, changes the Red marker's label and gives a sticker slot an image of its own
            yield return CreateTwin(OwnTwin);
            yield return SelectTwin("LipEdema");
            yield return PaintOnePart();
            yield return StoreView(OwnView);
            PoseTheTwin(yaw: 70f, pitch: 20f, zoom: 1.1f, cameraPosition: new Vector3(0.2f, 0.8f, 0.7f));
            FindToolMeaningField(MeaningTool).text = OwnMeaning;
            yield return GiveTheOpenTwinItsOwnSticker();

            AppState changed = null;
            yield return ReadState(s => changed = s);
            Assert.Contains(OwnTwin, changed.twins, "Setup: the twin of our own should be in the list.");
            Assert.AreNotEqual(fresh.pose.yaw, changed.pose.yaw, "Setup: the twin should be turned.");
            Assert.AreNotEqual(fresh.pose.positionCamera_z, changed.pose.positionCamera_z, "Setup: the camera should have moved.");
            Assert.IsNotEmpty(changed.groups, "Setup: LipEdema should show its groups.");
            Assert.Greater(changed.parts, 0, "Setup: the painted part should be in a group.");
            Assert.Contains(OwnView, changed.views, "Setup: the stored view should be in the view list.");
            CollectionAssert.AreNotEquivalent(fresh.stickers, changed.stickers, "Setup: a sticker slot should show the twin's own image.");
            CollectionAssert.AreNotEquivalent(fresh.textBoxes, changed.textBoxes, "Setup: a text box should carry a text of our own.");

            //resets again and compares all seven expectations with the reference: 
            //a blank twin, only the reference twin list, the default camera, no groups, only the default view, 
            //the default sticker images and the default text in every saved text box.
            yield return ResetApp();
            yield return WaitForModeActive("Main");
            AppState reset = null;
            yield return ReadState(s => reset = s);

            Assert.AreEqual(DefaultProfile, reset.profile, "A reset should leave the default twin open.");
            CollectionAssert.AreEquivalent(fresh.twins, reset.twins,
                "After a reset the twin list should hold what a fresh app holds — no twin of our own.");
            AssertPoseIs(fresh.pose, reset.pose, "A reset should put the twin and the camera back into the default position");
            CollectionAssert.AreEqual(fresh.groups, reset.groups, "A reset should leave no groups in the group overlay.");
            Assert.AreEqual(0, reset.parts, "A reset should leave a blank twin, without any part.");
            CollectionAssert.AreEqual(fresh.views, reset.views, "A reset should leave only the default view.");
            foreach (KeyValuePair<string, Texture> slot in fresh.stickers)
            {
                Assert.AreSame(slot.Value, reset.stickers[slot.Key],
                    $"A reset should give sticker slot {slot.Key} its default image back.");
            }
            foreach (KeyValuePair<string, string> textBox in fresh.textBoxes)
            {
                Assert.AreEqual(textBox.Value, reset.textBoxes[textBox.Key],
                    $"A reset should set the text box {textBox.Key} back to its default.");
            }
        }

        /// <summary>paints and stores a view in LipEdema, resets, reopens LipEdema and checks that both are gone. 
        /// A reset rewrites the sample twins from their shipped templates, so this checks that rewrite.</summary>
        [UnityTest]
        public IEnumerator Reset_RestoresTheSampleTwinsAsShipped()
        {
            yield return LoadLipEdemaTwin();
            AppState shipped = null;
            yield return ReadState(s => shipped = s);

            yield return PaintOnePart();
            yield return StoreView(OwnView);

            // a reset does not save the open twin, so the changes have to be on disk already —
            // switching twins saves the one being left, and opening it again proves they arrived
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");
            AppState saved = null;
            yield return ReadState(s => saved = s);
            Assert.Greater(saved.parts, shipped.parts, "Setup: the painted part should have been saved with LipEdema.");
            Assert.Contains(OwnView, saved.views, "Setup: the stored view should have been saved with LipEdema.");

            yield return ResetApp();
            yield return WaitForModeActive("Main");
            yield return SelectTwin("LipEdema");
            AppState reopened = null;
            yield return ReadState(s => reopened = s);

            CollectionAssert.AreEqual(shipped.groups, reopened.groups, "LipEdema should come back with the groups it shipped with.");
            Assert.AreEqual(shipped.parts, reopened.parts, "LipEdema should come back without the part painted before the reset.");
            CollectionAssert.AreEqual(shipped.views, reopened.views, "LipEdema should come back without the view stored before the reset.");
        }

        /// <summary>Reads the checklist off the app. Opens the twin list to read it as a person
        /// sees it, and returns to the main screen afterwards.</summary>
        IEnumerator ReadState(System.Action<AppState> result)
        {
            PartManager partManager = FindPartManager();
            var state = new AppState
            {
                profile = DataPersistenceManager.instance.selectedProfileId,
                pose = TheViewManager().shootView(),
                groups = FindGameObjectByPath(GroupOverlayPanel).GetComponentsInChildren<Group>(true)
                    .Where(entry => entry.groupdata != null)
                    .Select(entry => entry.groupdata.name)
                    .ToList(),
                parts = partManager.groups.Sum(group => group.groupParts.Count),
                views = ViewNames(),
                stickers = Object.FindObjectsOfType<Sticker>(true)
                    .ToDictionary(slot => slot.GetComponent<Item>().getId(), StickerImage),
                textBoxes = TextBoxes(),
            };

            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            state.twins = TwinNames();
            yield return ClickButtonByPath("Canvas/Save UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            result(state);
        }

        List<string> ViewNames()
        {
            var names = new List<string>();
            foreach (Transform entry in FindGameObjectByPath(ViewPanel).transform)
            {
                Transform name = entry.Find("ReadOnlyMode/Text Background/ViewName");
                if (name != null)
                {
                    names.Add(name.GetComponent<Text>().text);
                }
            }
            return names;
        }

        List<string> TwinNames()
        {
            var names = new List<string>();
            foreach (Transform entry in FindGameObjectByPath(SaveTwinPanel).transform)
            {
                Transform name = entry.Find("Name/Text");
                if (name != null && entry.gameObject.activeSelf)
                {
                    names.Add(name.GetComponent<Text>().text);
                }
            }
            return names;
        }

        /// <summary>Every text box whose content is saved with the twin — the meanings of markers,
        /// fillers, stickers and text, and the names in the lists — keyed by where it sits, since
        /// the id alone is not guaranteed to be unique.</summary>
        static Dictionary<string, string> TextBoxes()
        {
            var texts = new Dictionary<string, string>();
            foreach (Item item in Object.FindObjectsOfType<Item>(true))
            {
                if (!item.persistent) continue;
                InputField input = item.GetComponentInChildren<InputField>(true);
                if (input == null) continue;
                texts[$"{GetTransformPath(item.transform)} ({item.getId()})"] = input.text;
            }
            return texts;
        }

        static Texture StickerImage(Sticker slot)
        {
            GameObject tool = slot.GetComponent<CW.Common.CwDemoButton>().IsolateTarget.gameObject;
            return tool.GetComponent<CwPaintDecal>().Texture;
        }

        static ViewManager TheViewManager()
        {
            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            return viewManager;
        }

        static void PoseTheTwin(float yaw, float pitch, float zoom, Vector3 cameraPosition)
        {
            TheViewManager().select(new SceneManagement.View
            {
                yaw = yaw,
                pitch = pitch,
                sizeCamera = zoom,
                positionCamera_x = cameraPosition.x,
                positionCamera_y = cameraPosition.y,
                positionCamera_z = cameraPosition.z,
            });
        }

        static void AssertPoseIs(SceneManagement.View expected, SceneManagement.View actual, string because)
        {
            Assert.AreEqual(expected.yaw, actual.yaw, 0.01f, because + " — the twin is turned (yaw).");
            Assert.AreEqual(expected.pitch, actual.pitch, 0.01f, because + " — the twin is tilted (pitch).");
            Assert.AreEqual(expected.sizeCamera, actual.sizeCamera, 0.01f, because + " — the zoom.");
            Assert.AreEqual(expected.positionCamera_x, actual.positionCamera_x, 0.01f, because + " — camera x.");
            Assert.AreEqual(expected.positionCamera_y, actual.positionCamera_y, 0.01f, because + " — camera y.");
            Assert.AreEqual(expected.positionCamera_z, actual.positionCamera_z, 0.01f, because + " — camera z.");
        }

        /// <summary>Creates a twin through the save screen and leaves the app on it.</summary>
        IEnumerator CreateTwin(string twinName)
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", twinName);
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Paints one part into the first group of the open LipEdema twin and returns to
        /// the main screen.</summary>
        IEnumerator PaintOnePart()
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            yield return SelectView(BodyView);
            yield return SelectGroupForPainting(FindPartManager().groups[0]);
            yield return PaintWithMarker("Red");
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Stores the current pose under a name, through the button a person would press.</summary>
        IEnumerator StoreView(string viewName)
        {
            yield return ClickButtonByPath(StoreViewButton);
            InputField nameField = null;
            foreach (Transform entry in FindGameObjectByPath(ViewPanel).transform)
            {
                Transform editRow = entry.Find("EditMode");
                if (editRow != null && editRow.gameObject.activeSelf)
                {
                    nameField = editRow.Find("InputField").GetComponent<InputField>();
                }
            }
            Assert.IsNotNull(nameField, "Storing a view did not open a row to name it.");
            nameField.text = viewName;
            nameField.onEndEdit.Invoke(viewName);
            yield return null;
        }

        /// <summary>Puts an image of its own behind the first sticker slot of the open twin, the
        /// way picking one from the gallery does, and opens the twin again so the slot shows it.</summary>
        IEnumerator GiveTheOpenTwinItsOwnSticker()
        {
            string profileId = DataPersistenceManager.instance.selectedProfileId;
            Sticker slot = Object.FindObjectsOfType<Sticker>(true).First();

            var image = new Texture2D(4, 4);
            image.SetPixels(Enumerable.Repeat(Color.magenta, 16).ToArray());
            image.Apply();
            string path = Path.Combine(DataPaths.PersistentDataPath, profileId, slot.GetComponent<Item>().getId() + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            // reopening keeps the pose and the text box only because the switch saves them first
            DataPersistenceManager.instance.ChangeSelectedProfileId(profileId);
            yield return null;
        }
    }
}
