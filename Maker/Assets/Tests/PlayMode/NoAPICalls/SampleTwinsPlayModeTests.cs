using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PaintIn3D;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// Chain K02, first half — open a sample twin and see where it stands: which groups it brings,
    /// whether they hold paint, which views, markers, fillers and stickers it ships with.
    /// </summary>
    /// <remarks>
    /// <para>The expected names are written out here on purpose. They are what the business
    /// department expects to find in the app, so a template that is overwritten, renamed or
    /// truncated must fail this test instead of being compared with itself.</para>
    /// <para>What these tests do not cover: that a tool paints, that a group can be shown or
    /// hidden, that a view can be stored. Those are the process tests (P02, P03, P04); this
    /// class only says what is there when a twin is opened.</para>
    /// </remarks>
    [Category(Chains.OpenAndAddToATwin)]
    public class SampleTwinsPlayModeTests : TwinPaintTestBase
    {
        const string ViewPanel = "Canvas/Overlays/View Overlay/Scroll/Panel";

        // ---------------------------------------------------------------- the six body regions

        static readonly string[] Arms =
        {
            "shoulder_front_left", "shoulder_front_right", "shoulder_back_left", "shoulder_back_right",
            "upper_arm_front_left", "upper_arm_front_right", "upper_arm_back_left", "upper_arm_back_right",
            "elbow_front_left", "elbow_front_right", "elbow_back_left", "elbow_back_right",
            "forearm_front_left", "forearm_front_right", "forearm_back_left", "forearm_back_right",
            "wrist_left", "wrist_right", "armpit_left", "armpit_right",
        };

        static readonly string[] Feet =
        {
            "ankle_left", "ankle_right", "foot_top_left", "foot_top_right", "heel_left", "heel_right",
            "big_toe_left", "big_toe_right", "toes_left", "toes_right", "foot_sole_left", "foot_sole_right",
        };

        static readonly string[] Hands =
        {
            "hand_palm_left", "hand_palm_right", "hand_back_left", "hand_back_right",
            "thumb_left", "thumb_right", "index_finger_left", "index_finger_right",
            "middle_finger_left", "middle_finger_right", "ring_finger_left", "ring_finger_right",
            "little_finger_left", "little_finger_right",
        };

        static readonly string[] Head =
        {
            "forehead", "temple_left", "temple_right", "eye_left", "eye_right", "nose",
            "cheek_left", "cheek_right", "mouth_chin", "head_top", "head_back",
            "neck_front", "neck_back", "jaw_joint_left", "jaw_joint_right", "ear_left", "ear_right",
        };

        static readonly string[] Legs =
        {
            "hip_left", "hip_right", "thigh_front_left", "thigh_front_right",
            "thigh_back_left", "thigh_back_right", "knee_front_left", "knee_front_right",
            "knee_back_left", "knee_back_right", "shin_left", "shin_right", "calf_left", "calf_right",
        };

        static readonly string[] Torso =
        {
            "chest_left", "chest_right", "sternum",
            "abdomen_upper_left", "abdomen_upper_central", "abdomen_upper_right", "abdomen_central",
            "abdomen_lower_left", "abdomen_lower_central", "abdomen_lower_right",
            "upper_back_left", "upper_back_right", "spine_central", "lower_back_left", "lower_back_right",
            "sacrum", "groin_left", "groin_right", "genital_region", "buttock_left", "buttock_right",
        };

        static readonly Dictionary<string, string[]> BodyRegionGroups = new Dictionary<string, string[]>
        {
            { "Arms", Arms }, { "Feet", Feet }, { "Hands", Hands },
            { "Head", Head }, { "Legs", Legs }, { "Torso", Torso },
        };

        static IEnumerable<string> BodyRegionTwins() => BodyRegionGroups.Keys;

        /// <summary>A body-region twin opens with exactly its groups, each holding one part, and
        /// those parts draw something on the body.</summary>
        [UnityTest]
        public IEnumerator BodyRegionTwin_OpensWithItsGroups_EachHoldingAPartThatPaints(
            [ValueSource(nameof(BodyRegionTwins))] string twinName)
        {
            string[] expectedGroups = BodyRegionGroups[twinName];

            // the blank body of a twin without paint is the reference for "draws something"
            yield return LoadLipEdemaTwin();
            yield return new WaitForSeconds(0.5f);
            Color32[] blank = BodyPixels();

            yield return SelectTwin(twinName);
            yield return new WaitForSeconds(0.5f);
            PartManager partManager = FindPartManager();

            CollectionAssert.AreEquivalent(expectedGroups, partManager.groups.Select(g => g.name).ToList(),
                $"'{twinName}' should open with exactly its groups, named as shipped.");

            foreach (PartManager.GroupData group in partManager.groups)
            {
                Assert.AreEqual(1, group.groupParts.Count, $"'{twinName}/{group.name}' should hold one part.");
                Assert.Greater(group.groupParts[0].partCommands.Count, 0,
                    $"The part of '{twinName}/{group.name}' paints nothing.");
            }
            AssertPartsAreUsable(partManager);

            Assert.Greater(DifferingPixels(blank, BodyPixels()), 0,
                $"'{twinName}' holds parts, but nothing of them shows on the body.");
        }

        // ---------------------------------------------------------------------------- LipEdema

        /// <summary>The LipEdema twin brings four empty groups and four stored views, and each
        /// view really puts the camera somewhere of its own.</summary>
        [UnityTest]
        public IEnumerator LipEdema_OpensWithFourEmptyGroupsAndItsViews()
        {
            yield return LoadLipEdemaTwin();
            PartManager partManager = FindPartManager();

            CollectionAssert.AreEquivalent(new[] { "Pain", "Injuries", "Treatment", "Swell" },
                partManager.groups.Select(g => g.name).ToList(), "LipEdema ships with these four groups.");
            foreach (PartManager.GroupData group in partManager.groups)
            {
                Assert.IsEmpty(group.groupParts, $"LipEdema/{group.name} should ship without a part.");
            }

            string[] stored = { "Head front", "Head back", "Upper body", "Lower body" };
            CollectionAssert.IsSubsetOf(stored, ViewNames(), "LipEdema ships with these four stored views.");

            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            var poses = new List<SceneManagement.View>();
            foreach (string view in stored)
            {
                yield return SelectView(view);
                yield return new WaitForSeconds(1f);
                poses.Add(viewManager.shootView());
            }
            for (int i = 0; i < poses.Count; i++)
            {
                for (int j = i + 1; j < poses.Count; j++)
                {
                    Assert.IsFalse(SamePlace(poses[i], poses[j]),
                        $"The views '{stored[i]}' and '{stored[j]}' put the camera in the same place.");
                }
            }
        }

        static readonly Dictionary<string, string> MarkerMeanings = new Dictionary<string, string>
        {
            { "Red", "Deep Aching Pain" },
            { "Green", "Pressure Pain" },
            { "Blue", "Heavy, Tired Legs" },
            { "Cyan", "Sharp, Shooting Pain" },
            { "Pink", "Burning Sensation" },
            { "Yellow", "Muscle Cramps and Spasms" },
            { "White", "Joint Pain and Stiffness" },
            { "Black", "Skin Pain and Sensitivity" },
            { "Red Dotted", "Treatment: Compression Therapy" },
            { "Green Dotted", "Treatment: Manual Lymphatic Drainage (MLD)" },
            { "Blue Dotted", "Treatment: Liposuction" },
            { "Cyan Dotted", "Treatment: Skin Care and Wound Management" },
            { "Pink Dotted", "Treatment: Pneumatic Compression Devices" },
            { "Yellow Dotted", "Treatment: Physical Therapy" },
            { "White Dotted", "Treatment: Exercise" },
            { "Black Dotted", "Treatment TBD." },
        };

        static readonly Dictionary<string, string> FillerMeanings = new Dictionary<string, string>
        {
            { "Red", "Stretch Marks" },
            { "Green", "Cellulite" },
            { "Blue", "Fat Deposits and Bulges" },
            { "Cyan", "Skin Discoloration" },
            { "Pink", "Edema (Swelling)" },
            { "Yellow", "Lipedema Nodules" },
            { "White", "Loose or Sagging Skin" },
            { "Black", "Skin Irritation and Rashes" },
        };

        /// <summary>Slot number (the one in the slot's name in the scene) and its meaning.</summary>
        static readonly Dictionary<int, string> StickerMeanings = new Dictionary<int, string>
        {
            { 1, "Three dots from burning" },
            { 2, "ruptured blood vessels" },
            { 3, "light round hematoma " },
            { 4, "Muscle cramp" },
            { 5, "Joint stiffness" },
            { 6, "Burning sensation" },
            { 7, "Open wound" },
            { 8, "Swollen" },
            { 9, "" },
            { 10, "" },
        };

        /// <summary>Every marker, filler and sticker slot of the LipEdema twin carries the
        /// meaning it ships with, and seven of the ten stickers show an image of their own.</summary>
        [UnityTest]
        public IEnumerator LipEdema_ShipsItsMarkersFillersAndStickers()
        {
            yield return LoadLipEdemaTwin();

            AssertMeanings("marker", MarkerMeanings, MeaningsOf<Code.AI.PromptGeneration.Markers>());
            AssertMeanings("filler", FillerMeanings, MeaningsOf<Code.AI.PromptGeneration.Fillers>());

            Sticker[] slots = Object.FindObjectsOfType<Sticker>(true);
            var stickers = slots.ToDictionary(SlotNumber, slot => slot.GetComponentInChildren<InputField>(true).text);
            AssertMeanings("sticker slot", StickerMeanings.ToDictionary(e => e.Key.ToString(), e => e.Value),
                stickers.ToDictionary(e => e.Key.ToString(), e => e.Value));

            int ownImages = slots.Count(slot => slot.transform.parent.GetComponent<StickerRepo>().defaultTexture
                                                != slot.GetComponent<CW.Common.CwDemoButton>().IsolateTarget
                                                    .GetComponent<CwPaintDecal>().Texture);
            Assert.AreEqual(7, ownImages, "Seven of the ten sticker slots should show an image of their own.");
        }

        // ---------------------------------------------------------------------------- helpers

        /// <summary>Row name → meaning for every row of one tool panel (Markers or Fillers).</summary>
        static Dictionary<string, string> MeaningsOf<TPanel>() where TPanel : Code.AI.PromptGeneration.Tools
        {
            TPanel panel = Object.FindObjectOfType<TPanel>(true);
            Assert.IsNotNull(panel, $"No {typeof(TPanel).Name} panel in the scene.");
            var meanings = new Dictionary<string, string>();
            foreach (Transform row in panel.transform)
            {
                InputField input = row.GetComponentInChildren<InputField>(true);
                if (input != null) meanings[row.name] = input.text;
            }
            return meanings;
        }

        /// <summary>Names every row that differs, instead of "the dictionaries are not equal".</summary>
        static void AssertMeanings(string what, Dictionary<string, string> expected, Dictionary<string, string> actual)
        {
            var wrong = new List<string>();
            foreach (KeyValuePair<string, string> row in expected)
            {
                if (!actual.TryGetValue(row.Key, out string found)) wrong.Add($"{what} '{row.Key}' is missing");
                else if (found != row.Value) wrong.Add($"{what} '{row.Key}' reads '{found}', expected '{row.Value}'");
            }
            foreach (string extra in actual.Keys.Except(expected.Keys)) wrong.Add($"{what} '{extra}' is not expected");
            Assert.IsEmpty(wrong, "LipEdema should carry the meanings it ships with:\n" + string.Join("\n", wrong));
        }

        static int SlotNumber(Sticker slot)
        {
            return int.Parse(slot.name.Substring(slot.name.LastIndexOf(' ') + 1));
        }

        List<string> ViewNames()
        {
            var names = new List<string>();
            foreach (Transform entry in FindGameObjectByPath(ViewPanel).transform)
            {
                Transform label = entry.Find("ReadOnlyMode/Text Background/ViewName");
                if (label != null) names.Add(label.GetComponent<Text>().text);
            }
            return names;
        }

        static bool SamePlace(SceneManagement.View a, SceneManagement.View b)
        {
            return Mathf.Approximately(a.yaw, b.yaw) && Mathf.Approximately(a.pitch, b.pitch)
                   && Mathf.Approximately(a.sizeCamera, b.sizeCamera)
                   && Mathf.Approximately(a.positionCamera_x, b.positionCamera_x)
                   && Mathf.Approximately(a.positionCamera_y, b.positionCamera_y)
                   && Mathf.Approximately(a.positionCamera_z, b.positionCamera_z);
        }
    }
}
