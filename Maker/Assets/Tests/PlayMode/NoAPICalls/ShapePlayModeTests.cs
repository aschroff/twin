using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The Shape screen (Assets/Code/View/Mode/ShapeMode.cs, Assets/Code/Interface/Model.cs):
    /// changing the figure itself rather than painting on it.
    ///
    /// <para><c>ShapeMode</c> only shows the UI; the whole function sits in five buttons of
    /// <c>Assets/Prefabs/GUI/EditShape UI.prefab</c> that call <see cref="Model"/> directly through
    /// the inspector.
    ///
    /// <para>What these tests deliberately do NOT claim: that the body <em>looks</em> right
    /// afterwards. Betas being different is not the same as a plausible figure, and that stays a
    /// manual check.</para>
    /// </summary>
    [Category(Processes.LookAtTheTwin)]
    public class ShapePlayModeTests : PlayModeTestBase
    {
        const string ButtonsPath = "Canvas/EditShape UI/Bottom/Buttons";

        /// <summary>The figure, read the way the save file reads it. <c>Model</c> keeps handPose
        /// and bodyPose private, and <c>SaveData</c> is the app's own way of getting at them.</summary>
        private static ConfigData ReadModelState()
        {
            var model = Object.FindObjectOfType<Model>();
            Assert.IsNotNull(model, "Model component not found in scene.");
            var data = new ConfigData("ShapeTest");
            model.SaveData(data);
            return data;
        }

        private static SMPLX FindSmplx()
        {
            var model = Object.FindObjectOfType<Model>();
            Assert.IsNotNull(model, "Model component not found in scene.");
            var smplx = model.GetComponent<SMPLX>();
            Assert.IsNotNull(smplx, "SMPLX component not found next to Model.");
            return smplx;
        }

        /// <summary>Asserts that some persistent listener on the button calls
        /// <paramref name="method"/> on a <see cref="Model"/>. Scans all calls rather than index 0:
        /// the navigation call and the model call sit on the same button.</summary>
        private static void AssertCallsModel(string buttonName, string method)
        {
            var button = FindButtonByPath($"{ButtonsPath}/{buttonName}");
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentMethodName(i) != method) continue;
                Assert.IsInstanceOf<Model>(button.onClick.GetPersistentTarget(i),
                    $"'{buttonName}' calls {method}, but not on the Model.");
                return;
            }
            Assert.Fail($"Button '{buttonName}' has no persistent call to Model.{method}() — " +
                        "the reference was probably lost while editing the prefab.");
        }

        /// <summary>Shape is not reachable from the main screen: it is the seventh tool in the
        /// Edit screen's bottom bar, next to Marker, Filler, Sticker, Text, Delete and Region. The
        /// clickable Button sits on the label rather than on the tool itself — see
        /// Assets/Prefabs/Icon in circle with text.prefab, and the same path in
        /// EditUiPlayModeTests.</summary>
        private IEnumerator OpenShapeScreen()
        {
            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");

            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Shape/Text Background/Text");
            yield return WaitForModeActive("Shape");
        }

        /// <summary>The Shape button leads somewhere, and the screen it leads to still has all
        /// six of its buttons.</summary>
        [UnityTest]
        public IEnumerator ShapeButton_OpensTheShapeScreen()
        {
            yield return OpenShapeScreen();

            AssertGameObjectActive("Canvas/EditShape UI");
            AssertGameObjectActive($"{ButtonsPath}/Edit");
            AssertGameObjectActive($"{ButtonsPath}/Shape");
            AssertGameObjectActive($"{ButtonsPath}/Arms");
            AssertGameObjectActive($"{ButtonsPath}/Hands");
            AssertGameObjectActive($"{ButtonsPath}/Face");
            AssertGameObjectActive($"{ButtonsPath}/Reset");
        }

        /// <summary>A button can be visible and do nothing: the five calls into Model are wired in
        /// the prefab's inspector, where a rename or a re-parent silently drops them.</summary>
        [UnityTest]
        public IEnumerator ShapeButtons_AreStillWiredToTheModel()
        {
            yield return OpenShapeScreen();

            AssertCallsModel("Shape", "overall_random");
            AssertCallsModel("Face", "face_random");
            AssertCallsModel("Hands", "toggle_hands");
            AssertCallsModel("Arms", "toggle_arms");
            AssertCallsModel("Reset", "reset");
        }

        /// <summary>"Shape" changes the statue, and — despite the method being called
        /// <c>overall_random</c> — it writes a fixed set of betas, so pressing it twice must give
        /// the same figure. A test that only asked for "different" would pass on a broken
        /// randomiser.</summary>
        [UnityTest]
        public IEnumerator Shape_ChangesTheFigure_AndRepeatsItself()
        {
            yield return OpenShapeScreen();

            var smplx = FindSmplx();
            for (int i = 0; i < SMPLX.NUM_BETAS; i++)
                smplx.betas[i] = 0f;

            yield return ClickButtonByPath($"{ButtonsPath}/Shape");

            var first = (float[])smplx.betas.Clone();
            Assert.AreEqual(SMPLX.NUM_BETAS, first.Length);
            foreach (float beta in first)
                Assert.AreNotEqual(0f, beta, "Every beta should have been set.");

            yield return ClickButtonByPath($"{ButtonsPath}/Shape");

            for (int i = 0; i < SMPLX.NUM_BETAS; i++)
                Assert.AreEqual(first[i], smplx.betas[i], 1e-6f,
                    $"beta[{i}] changed on the second press — the shape is not reproducible.");
        }

        /// <summary>"Face" really is random, so this can only ask for "changed, and inside the
        /// range the code promises".</summary>
        [UnityTest]
        public IEnumerator Face_ChangesTheExpressions_WithinRange()
        {
            yield return OpenShapeScreen();

            var smplx = FindSmplx();
            for (int i = 0; i < SMPLX.NUM_EXPRESSIONS; i++)
                smplx.expressions[i] = 0f;

            yield return ClickButtonByPath($"{ButtonsPath}/Face");

            int changed = 0;
            foreach (float expression in smplx.expressions)
            {
                Assert.GreaterOrEqual(expression, -2.0f, "Expression outside the documented range.");
                Assert.LessOrEqual(expression, 2.0f, "Expression outside the documented range.");
                if (!Mathf.Approximately(expression, 0f)) changed++;
            }
            Assert.Greater(changed, 0, "No expression changed at all.");
        }

        /// <summary>Both toggles read their starting point rather than assuming it, so the test
        /// still means something on a twin that was saved in the other pose.</summary>
        [UnityTest]
        public IEnumerator Arms_ToggleBetweenTheTwoPoses()
        {
            yield return OpenShapeScreen();

            SMPLX.BodyPose before = ReadModelState().bodyPose;

            yield return ClickButtonByPath($"{ButtonsPath}/Arms");
            Assert.AreNotEqual(before, ReadModelState().bodyPose, "Arms did not change the pose.");

            yield return ClickButtonByPath($"{ButtonsPath}/Arms");
            Assert.AreEqual(before, ReadModelState().bodyPose, "Arms did not toggle back.");
        }

        [UnityTest]
        public IEnumerator Hands_ToggleBetweenTheTwoPoses()
        {
            yield return OpenShapeScreen();

            SMPLX.HandPose before = ReadModelState().handPose;

            yield return ClickButtonByPath($"{ButtonsPath}/Hands");
            Assert.AreNotEqual(before, ReadModelState().handPose, "Hands did not change the pose.");

            yield return ClickButtonByPath($"{ButtonsPath}/Hands");
            Assert.AreEqual(before, ReadModelState().handPose, "Hands did not toggle back.");
        }

        /// <summary>Reset is the one button that has to undo all four of the others at once.</summary>
        [UnityTest]
        public IEnumerator Reset_ClearsShapeExpressionsAndPose()
        {
            yield return OpenShapeScreen();

            yield return ClickButtonByPath($"{ButtonsPath}/Shape");
            yield return ClickButtonByPath($"{ButtonsPath}/Face");
            yield return ClickButtonByPath($"{ButtonsPath}/Arms");
            yield return ClickButtonByPath($"{ButtonsPath}/Hands");

            yield return ClickButtonByPath($"{ButtonsPath}/Reset");

            var smplx = FindSmplx();
            foreach (float beta in smplx.betas)
                Assert.AreEqual(0f, beta, 1e-6f, "Reset left a beta behind.");
            foreach (float expression in smplx.expressions)
                Assert.AreEqual(0f, expression, 1e-6f, "Reset left an expression behind.");

            ConfigData state = ReadModelState();
            Assert.AreEqual(SMPLX.BodyPose.T, state.bodyPose, "Reset should return the body to the T pose.");
            Assert.AreEqual(SMPLX.HandPose.Flat, state.handPose, "Reset should return the hands to flat.");
        }

        /// <summary>The one that matters most: a figure the user shaped has to still be there after
        /// the twin is closed and opened again. Goes through the file — ChangeSelectedProfileId
        /// saves on the way out and reads from disk on the way in.
        ///
        /// <para>The face is deliberately not part of this — it is never saved at all. See
        /// <see cref="Face_IsNotSaved_KnownGap"/>.</para></summary>
        [UnityTest]
        public IEnumerator ChangedFigure_SurvivesSaveAndReload()
        {
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");
            SetInputByName("InputField", "ShapeTwin");
            yield return ClickButtonByName("New");
            yield return WaitForModeActive("Main");

            yield return OpenShapeScreen();
            yield return ClickButtonByPath($"{ButtonsPath}/Shape");
            yield return ClickButtonByPath($"{ButtonsPath}/Arms");
            yield return ClickButtonByPath($"{ButtonsPath}/Hands");

            // Clone: Model.SaveData hands out the live array, so the "expected" values would
            // otherwise follow the model through the reload and the test would pass on anything.
            float[] expectedBetas = (float[])FindSmplx().betas.Clone();
            ConfigData before = ReadModelState();

            var dpm = DataPersistenceManager.instance;
            dpm.SaveConfig();
            string profileId = dpm.selectedProfileId;

            dpm.ChangeSelectedProfileId("default.000");
            yield return null;
            dpm.ChangeSelectedProfileId(profileId);
            yield return null;

            float[] actualBetas = FindSmplx().betas;
            for (int i = 0; i < SMPLX.NUM_BETAS; i++)
                Assert.AreEqual(expectedBetas[i], actualBetas[i], 1e-4f,
                    $"beta[{i}] did not survive the reload.");

            ConfigData after = ReadModelState();
            Assert.AreEqual(before.bodyPose, after.bodyPose, "Body pose did not survive the reload.");
            Assert.AreEqual(before.handPose, after.handPose, "Hand pose did not survive the reload.");
        }
    }
}
