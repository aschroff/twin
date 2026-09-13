using System.Collections;
using System.Linq;
using NUnit.Framework;
using PaintCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Temporary: what exactly is left on the body after an undo.
    /// </summary>
    /// <remarks>"6732 pixels differ" does not say whether paint is left behind or whether two
    /// renderings of the same blank texture disagree in the last bit. This reports the magnitude,
    /// and whether waiting longer changes anything.</remarks>
    [Category(Processes.MarkUpTheBody)]
    [Explicit("Diagnostic, not a regression test.")]
    public class UndoResidueDiagnosticTests : TwinPaintTestBase
    {
        private static Color32[] BodyPixels()
        {
            Body body = Object.FindObjectOfType<Body>();
            Texture2D copy = body.GetComponent<CwPaintableTexture>().GetReadableCopy();
            Color32[] pixels = copy.GetPixels32();
            Object.DestroyImmediate(copy);
            return pixels;
        }

        /// <summary>How much of the body is not plain white, and in which colours.</summary>
        /// <summary>The state of the group data, which is what Refresh() replays from.</summary>
        private static string Groups(string label, PartManager manager)
        {
            var sb = new System.Text.StringBuilder(label + ": " + manager.groups.Count + " groups");
            foreach (PartManager.GroupData g in manager.groups)
            {
                sb.Append("\n      visible=" + g.visible + "  parts=" + g.groupParts.Count
                          + "  id=" + (g.id ?? "null").Substring(0, 8)
                          + "  groupObject=" + (g.group == null ? "null" : "set"));
            }
            sb.Append("\n      CanUndo=" + manager.CanUndo + "  Listening=" + manager.Listening);
            return sb.ToString();
        }

        private static string Content(string label, Color32[] pixels)
        {
            int nonWhite = 0, reddish = 0, greenish = 0, other = 0;
            foreach (Color32 p in pixels)
            {
                if (p.r == 255 && p.g == 255 && p.b == 255) continue;
                nonWhite++;
                if (p.r > p.g && p.r > p.b) reddish++;
                else if (p.g > p.r && p.g > p.b) greenish++;
                else other++;
            }
            return label + ": " + nonWhite + " non-white  (reddish " + reddish
                   + ", greenish " + greenish + ", other " + other + ")";
        }

        private static string Describe(string label, Color32[] before, Color32[] after)
        {
            int differing = 0, byOne = 0, upToFive = 0, upToTwenty = 0, worse = 0, maxDelta = 0;
            string examples = "";

            for (int i = 0; i < before.Length; i++)
            {
                Color32 a = before[i], b = after[i];
                int delta = Mathf.Max(Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g)),
                                      Mathf.Max(Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a)));
                if (delta == 0) continue;

                differing++;
                if (delta > maxDelta) maxDelta = delta;
                if (delta == 1) byOne++;
                else if (delta <= 5) upToFive++;
                else if (delta <= 20) upToTwenty++;
                else
                {
                    worse++;
                    if (worse <= 3)
                    {
                        examples += "\n      pixel " + i + ": was rgba(" + a.r + "," + a.g + "," + a.b + "," + a.a
                                    + ")  now rgba(" + b.r + "," + b.g + "," + b.b + "," + b.a + ")";
                    }
                }
            }

            return label + ": " + differing + " differing"
                   + "   max delta " + maxDelta
                   + "   [by 1: " + byOne + " | 2-5: " + upToFive
                   + " | 6-20: " + upToTwenty + " | >20: " + worse + "]" + examples;
        }

        [UnityTest]
        public IEnumerator WhatIsLeftOnTheBodyAfterUndo()
        {
            // Torso instead of LipEdema: LipEdema has a painted texture left in
            // PlayerPrefs from real use, Torso has none. Same flow, clean slate.
            yield return ResetApp();
            yield return SelectTwin("Torso");
            PartManager partManager = FindPartManager();
            PartManager.GroupData group = partManager.groups[0];

            yield return ClickButtonByName("Edit Button");
            yield return WaitForModeActive("Edit");
            // Torso has no stored "Upper body" view, so frame the camera directly
            ViewManager viewManager = Object.FindFirstObjectByType<ViewManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(viewManager, "No ViewManager in the scene.");
            viewManager.select(new SceneManagement.View
            {
                yaw = 0f, pitch = 0f, sizeCamera = 0.9f,
                positionCamera_x = 0f, positionCamera_y = 1.0f, positionCamera_z = 1f,
            });
            yield return null;
            yield return SelectGroupForPainting(group);
            yield return new WaitForSeconds(0.5f);

            Color32[] blankPixels = BodyPixels();
            Color32[] blank = blankPixels;
            string blankContent = Content("  content of blank", blankPixels)
                                  + "\n" + Groups("  groups at blank", partManager);
            // is the texture even stable when nothing happens at all?
            yield return new WaitForSeconds(0.5f);
            string idle = Describe("  blank vs blank (nothing done)", blank, BodyPixels());

            yield return PaintWithMarker("Red");
            Color32[] paintedPixels = BodyPixels();
            string painted = Describe("  blank vs painted", blank, paintedPixels);
            string paintedContent = Content("  content of painted", paintedPixels)
                                    + "\n" + Groups("  groups after painting", partManager);

            var pointer = new PointerEventData(EventSystem.current)
                { button = PointerEventData.InputButton.Left };
            PartHistoryButton undo = Object.FindObjectsOfType<PartHistoryButton>(false)
                .First(b => b.isActiveAndEnabled && b.Action == PartHistoryButton.HistoryAction.Undo);
            ExecuteEvents.Execute(undo.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;

            yield return new WaitForSeconds(0.5f);
            string afterHalf = Describe("  after undo, 0.5 s (what the test measures)", blank, BodyPixels());
            yield return new WaitForSeconds(2.5f);
            string afterThree = Describe("  after undo, 3.0 s", blank, BodyPixels());
            yield return new WaitForSeconds(5f);
            Color32[] undonePixels = BodyPixels();
            string afterEight = Describe("  after undo, 8.0 s", blank, undonePixels);
            string undoneContent = Content("  content after undo", undonePixels)
                                   + "\n" + Groups("  groups after undo", partManager);

            Debug.Log("[undo residue]\n" + blankContent + "\n" + paintedContent + "\n"
                      + undoneContent + "\n---\n" + idle + "\n" + painted + "\n"
                      + afterHalf + "\n" + afterThree + "\n" + afterEight);
        }
    }
}
