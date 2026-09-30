using System.Collections;
using System.IO;
using Lean.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Stored views: what the app remembers of a camera angle, and whether it really comes back.
    /// </summary>
    /// <remarks>
    /// <para>A view is six numbers - the body's yaw and pitch, the camera's position, and its zoom.
    /// <c>ViewManager.shootView</c> reads them off the live scene and <c>select</c> writes them
    /// back, so these tests compare numbers rather than pictures: a screenshot would fail over a
    /// driver, a resolution or a button somewhere else on the screen, and when it did fail it
    /// would only say "looks different" instead of naming the number that is wrong.</para>
    ///
    /// <para>The pose is changed by a different route than the one under test - by turning the
    /// body directly, or through <c>setDefaultPosition</c> - and each test asserts that the pose
    /// really did change before activating the view again. Otherwise a <c>select</c> that does
    /// nothing at all would pass.</para>
    ///
    /// <para>A screenshot is written to <c>Application.temporaryCachePath/ViewShots/</c> for
    /// looking at. It is evidence, not an assertion.</para>
    /// </remarks>
    [Category(Processes.LookAtTheTwin)]
    public class ViewPlayModeTests : TwinPaintTestBase
    {
        private const string StoreViewButton = "Canvas/Overlays/View Overlay/StoreView/Icon";
        private const string ViewPanel = "Canvas/Overlays/View Overlay/Scroll/Panel";

        private static string ShotsDir => Path.Combine(Application.temporaryCachePath, "ViewShots");

        private static ViewManager TheViewManager()
        {
            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            return viewManager;
        }

        /// <summary>The pose the app is in right now, read the way the app reads it when storing.</summary>
        private static SceneManagement.View CurrentPose()
        {
            return TheViewManager().shootView();
        }

        /// <summary>Turns the twin, and nothing else - the camera stays where it is.</summary>
        private static void TurnTheTwin(float yaw, float pitch)
        {
            LeanPitchYaw control = TheViewManager().body.GetComponent<LeanPitchYaw>();
            Assert.IsNotNull(control, "The body has no LeanPitchYaw, so it cannot be turned.");
            control.Yaw = yaw;
            control.Pitch = pitch;
        }

        /// <summary>Puts the app into a pose worth recognising again.</summary>
        private static void PoseTheTwin(float yaw, float pitch, float zoom, Vector3 cameraPosition)
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

        /// <summary>Stores the current pose under a name, through the button a person would press.</summary>
        private IEnumerator StoreCurrentViewAs(string viewName)
        {
            yield return ClickButtonByPath(StoreViewButton);

            // a freshly stored view opens in its edit row, waiting for a name
            GameObject panel = FindGameObjectByPath(ViewPanel);
            InputField nameField = null;
            foreach (Transform entry in panel.transform)
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

        private static void AssertPoseIs(SceneManagement.View expected, SceneManagement.View actual, string because)
        {
            Assert.AreEqual(expected.yaw, actual.yaw, 0.01f, because + " — the twin is turned differently (yaw).");
            Assert.AreEqual(expected.pitch, actual.pitch, 0.01f, because + " — the twin is tilted differently (pitch).");
            Assert.AreEqual(expected.sizeCamera, actual.sizeCamera, 0.01f, because + " — the zoom is different.");
            Assert.AreEqual(expected.positionCamera_x, actual.positionCamera_x, 0.01f, because + " — camera x.");
            Assert.AreEqual(expected.positionCamera_y, actual.positionCamera_y, 0.01f, because + " — camera y.");
            Assert.AreEqual(expected.positionCamera_z, actual.positionCamera_z, 0.01f, because + " — camera z.");
        }

        private IEnumerator CaptureShot(string shotName)
        {
            yield return new WaitForEndOfFrame();
            Directory.CreateDirectory(ShotsDir);
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(ShotsDir, shotName + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }

        /// <summary>
        /// The whole point of storing a view: get that framing back later. All six numbers, because
        /// losing one of them is what a person would notice as "it is not quite the same picture".
        /// </summary>
        [UnityTest]
        public IEnumerator StoredView_ComesBackWhenItIsActivated()
        {
            yield return LoadLipEdemaTwin();

            PoseTheTwin(yaw: 40f, pitch: 15f, zoom: 1.2f, cameraPosition: new Vector3(0.1f, 0.6f, 0.9f));
            yield return null;
            SceneManagement.View stored = CurrentPose();

            yield return StoreCurrentViewAs("foot");
            yield return CaptureShot("01-stored-as-foot");

            TheViewManager().setDefaultPosition();
            yield return null;
            Assert.AreNotEqual(stored.yaw, CurrentPose().yaw,
                "Setup: the pose should have changed, otherwise this test cannot tell activation from doing nothing.");

            yield return SelectView("foot");
            yield return null;
            yield return CaptureShot("02-foot-activated-again");

            AssertPoseIs(stored, CurrentPose(), "The stored view did not come back");
        }

        /// <summary>
        /// Turning the twin moves the body, not the camera, and the two are restored by different
        /// lines of <c>select</c>. This is the case the business department checks by hand as
        /// 07/01a, and the one most likely to break: a view that only put the camera back would
        /// still look right in a test that never turned anything.
        /// </summary>
        [UnityTest]
        public IEnumerator StoredView_ComesBackAfterTheTwinWasTurned()
        {
            yield return LoadLipEdemaTwin();

            PoseTheTwin(yaw: -30f, pitch: 8f, zoom: 1.5f, cameraPosition: new Vector3(0f, 0.55f, 1.1f));
            yield return null;
            SceneManagement.View stored = CurrentPose();

            yield return StoreCurrentViewAs("front");

            // only the twin is turned; the camera is left exactly where it was
            TurnTheTwin(yaw: 170f, pitch: -25f);
            yield return null;
            yield return CaptureShot("03-turned-away");
            Assert.AreNotEqual(stored.yaw, CurrentPose().yaw, "Setup: the twin should be turned by now.");
            Assert.AreEqual(stored.positionCamera_z, CurrentPose().positionCamera_z, 0.01f,
                "Setup: turning the twin must not have moved the camera.");

            yield return SelectView("front");
            yield return null;
            yield return CaptureShot("04-front-activated-after-turning");

            AssertPoseIs(stored, CurrentPose(), "The stored view did not survive turning the twin");
        }

        /// <summary>
        /// The persistence check that every process carries: leave the twin, open it again, and the
        /// view is still there and still means the same framing.
        /// </summary>
        [UnityTest]
        public IEnumerator StoredView_SurvivesLeavingAndReopeningTheTwin()
        {
            yield return LoadLipEdemaTwin();

            PoseTheTwin(yaw: 95f, pitch: -12f, zoom: 0.9f, cameraPosition: new Vector3(-0.2f, 0.7f, 0.8f));
            yield return null;
            SceneManagement.View stored = CurrentPose();

            yield return StoreCurrentViewAs("knee");

            // switching twins saves the one being left
            yield return SelectTwin("default");
            yield return SelectTwin("LipEdema");

            TheViewManager().setDefaultPosition();
            yield return null;

            yield return SelectView("knee");
            yield return null;
            yield return CaptureShot("05-knee-after-reopening");

            AssertPoseIs(stored, CurrentPose(), "The stored view did not survive leaving the twin");
        }
    }
}
