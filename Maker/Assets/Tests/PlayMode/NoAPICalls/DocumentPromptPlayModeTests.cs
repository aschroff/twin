using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Code;
using Code.AI.PromptGeneration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NoAPICalls
{
    /// <summary>
    /// The prompt that asks the LLM to map a document onto the twin. Built against the LipEdema
    /// twin, which ships a meaning for every marker and filler, so the test frees one tool to
    /// cover both halves of the tool inventory.
    ///
    /// The assembled prompt is written to Application.temporaryCachePath/DocumentPrompt/ for
    /// reading - the wording is meant to be reviewed by a human, not asserted word by word.
    /// </summary>
    public class DocumentPromptPlayModeTests : TwinPaintTestBase
    {
        /// <summary>Tool whose meaning the test clears, so the "still free" section has content.</summary>
        private const string FreedTool = "Yellow";

        private static string DumpDir => Path.Combine(Application.temporaryCachePath, "DocumentPrompt");

        [UnityTest]
        public IEnumerator DocumentPrompt_DescribesTheTwinTheToolsAndTheRegions()
        {
            yield return LoadLipEdemaTwin();

            var partManager = FindPartManager();
            var settingsManager = Object.FindObjectOfType<SettingsManager>(true);
            Assert.IsNotNull(settingsManager, "SettingsManager not found in scene.");

            // the two editable rows have to arrive with their shipped text - a twin that never
            // stored one of them (LipEdema stores neither) falls back to the localization table
            AssertPromptRowHasDefault(settingsManager, DocumentPromptBuilder.PromptTask);
            AssertPromptRowHasDefault(settingsManager, DocumentPromptBuilder.PromptRules);

            string freedMeaning = FreeOneTool(FreedTool);
            yield return null;

            string prompt = DocumentPromptBuilder.Build(partManager, settingsManager);
            Write(prompt);
            Assert.IsNotEmpty(prompt, "The document prompt came out empty.");

            // 1. the language the answer has to be written in
            StringAssert.StartsWith("Write every text you produce in " + DocumentPromptBuilder.LanguageName(), prompt);

            // 2. the user's two rows
            StringAssert.Contains(PromptRowText(settingsManager, DocumentPromptBuilder.PromptTask), prompt);
            StringAssert.Contains(PromptRowText(settingsManager, DocumentPromptBuilder.PromptRules), prompt);

            // 3. every group of this twin, so the LLM can reuse instead of invent
            StringAssert.Contains(DocumentPromptBuilder.HeaderGroups, prompt);
            Assert.Greater(partManager.groups.Count, 0, "LipEdema should ship with groups.");
            foreach (PartManager.GroupData group in partManager.groups)
            {
                StringAssert.Contains(group.name, prompt);
            }

            // 4. the tools, split into the ones that carry a meaning and the ones still free
            string inUseSection = Section(prompt, DocumentPromptBuilder.HeaderToolsInUse, DocumentPromptBuilder.HeaderToolsFree);
            string freeSection = Section(prompt, DocumentPromptBuilder.HeaderToolsFree, DocumentPromptBuilder.HeaderRegions);

            StringAssert.Contains(FreedTool + ", draws", freeSection);
            Assert.IsFalse(inUseSection.Contains("- " + FreedTool + ", draws"),
                $"'{FreedTool}' has no meaning any more, so it must not be listed as in use.");
            Assert.IsFalse(inUseSection.Contains(freedMeaning),
                $"The meaning '{freedMeaning}' was cleared, so it must be gone from the tools in use.");

            // the tool a caller has to name is the tool GameObject, and both kinds appear
            StringAssert.Contains("Red, draws a line", inUseSection);
            StringAssert.Contains("Red Filling, draws a surface", inUseSection);
            StringAssert.Contains("Red dotted, draws a dotted line", inUseSection);

            // 5. the regions, all of them, keyed the way PaintRegion wants them
            StringAssert.Contains(DocumentPromptBuilder.HeaderRegions, prompt);
            string regionSection = prompt.Substring(prompt.IndexOf(DocumentPromptBuilder.HeaderRegions));
            List<string> keys = AllRegionKeys();
            Assert.Greater(keys.Count, 90, "The region catalog should hold the whole body.");
            foreach (string key in keys)
            {
                StringAssert.Contains(key, regionSection);
            }

            Debug.Log($"[Test] Document prompt: {prompt.Length} characters, " +
                      $"{partManager.groups.Count} groups, {ToolInventory.All().Count} tools, {keys.Count} regions. " +
                      $"Written to {DumpDir}");
        }

        /// <summary>
        /// The review screen - where the user will confirm what may reach the twin - names the
        /// picked file and carries the whole prompt. Driven through ShowPromptFor, which stops
        /// short of the request: this test must not reach the network.
        /// </summary>
        [UnityTest]
        public IEnumerator ReviewScreen_ShowsThePickedFileAndTheWholePrompt()
        {
            Directory.CreateDirectory(DumpDir);
            yield return LoadLipEdemaTwin();

            var partManager = FindPartManager();
            var settingsManager = Object.FindObjectOfType<SettingsManager>(true);
            var process = Object.FindObjectOfType<DocumentUploadProcess>(true);
            Assert.IsNotNull(process, "DocumentUploadProcess not found in scene.");

            // selecting a twin leaves the app on the twin screen, so come back to the main one
            yield return ClickButtonByPath("Canvas/Save UI/Top/GameObject/Back Button");
            yield return WaitForModeActive("Main");

            // the way a user gets here
            yield return ClickButtonByPath("Canvas/Main UI/Bottom/Upload/Icon");
            yield return WaitForModeActive("Upload");

            // the OS picker cannot be driven from a test, so hand the pick over directly
            string document = Path.Combine(Application.temporaryCachePath, "bodychart.pdf");
            File.WriteAllBytes(document, new byte[5 * 1024]);
            process.ShowPromptFor(document, null);

            yield return WaitForModeActive("UploadReview");
            AssertGameObjectActive("Canvas/UploadReview UI");

            var review = Object.FindObjectOfType<DocumentReviewManager>(true);
            Assert.IsNotNull(review, "No DocumentReviewManager on the review screen.");

            string shown = review.GetShownText();
            StringAssert.Contains("bodychart.pdf", shown);
            StringAssert.Contains("5 kB", shown);

            // the prompt reaches the screen whole - a truncated prompt is worse than none
            string prompt = DocumentPromptBuilder.Build(partManager, settingsManager);
            StringAssert.Contains(prompt, shown);
            File.WriteAllText(Path.Combine(DumpDir, "review-screen.txt"), shown);

            yield return CaptureShot("review-screen-document");

            // a photo names its size instead
            var photo = new Texture2D(1024, 768);
            process.ShowPromptFor(Path.Combine(Application.temporaryCachePath, "IMG_4711.jpg"), photo);
            yield return null;
            shown = review.GetShownText();
            StringAssert.Contains("IMG_4711.jpg", shown);
            StringAssert.Contains("1024 x 768 pixels", shown);
            Object.Destroy(photo);

            yield return ClickButtonByPath("Canvas/UploadReview UI/Back Button");
            yield return WaitForModeActive("Main");
        }

        /// <summary>Every marker and filler of the app is listed - including the first row of
        /// each panel, which the older report prompt skips.</summary>
        [UnityTest]
        public IEnumerator ToolInventory_ListsEveryMarkerAndFiller()
        {
            yield return LoadLipEdemaTwin();

            List<ToolInfo> tools = ToolInventory.All();
            var names = tools.Select(tool => tool.name).ToList();

            Assert.AreEqual(names.Count, names.Distinct().Count(), "A tool is listed twice.");
            CollectionAssert.Contains(names, "Red", "The first marker row must be listed.");
            CollectionAssert.Contains(names, "Red Filling", "The first filler row must be listed.");
            CollectionAssert.Contains(names, "Black dotted");

            Assert.AreEqual(8, tools.Count(t => t.kind == PartManager.Tool.MarkerLine), "8 solid markers expected.");
            Assert.AreEqual(8, tools.Count(t => t.kind == PartManager.Tool.MarkerDotted), "8 dotted markers expected.");
            Assert.AreEqual(8, tools.Count(t => t.kind == PartManager.Tool.Filler), "8 fillers expected.");

            // LipEdema gives every tool a meaning, so nothing is free in this twin
            foreach (ToolInfo tool in tools)
            {
                Assert.IsTrue(tool.inUse, $"'{tool.name}' should carry a meaning in the LipEdema twin.");
            }
        }

        private static void AssertPromptRowHasDefault(SettingsManager settingsManager, string label)
        {
            ItemPrompt row = settingsManager.getPromptObject(label, ItemPrompt.PromptLevel.Document);
            Assert.IsNotNull(row, $"No prompt row '{label}' on level Document - add it in Settings UI.prefab.");
            Assert.IsNotEmpty(row.defaultKey, $"Prompt row '{label}' has no default key.");

            string shipped = StringLocalizer.localizeString(row.defaultKey);
            Assert.AreNotEqual(row.defaultKey, shipped,
                $"'{row.defaultKey}' is not in the localization table.");
            // equal, not merely contained: the input field's character limit silently cuts a
            // prompt that is too long, and half a rule is worse than none
            Assert.AreEqual(shipped, row.GetPromptText(),
                $"Prompt row '{label}' does not hold its shipped default in full.");
        }

        private static string PromptRowText(SettingsManager settingsManager, string label)
        {
            return settingsManager.getPromptObject(label, ItemPrompt.PromptLevel.Document).GetPromptText().Trim();
        }

        private static List<string> AllRegionKeys()
        {
            var keys = new List<string>();
            foreach (var twin in PartTemplateService.GetTemplateCatalog().twins)
            {
                foreach (var region in twin.regions)
                {
                    keys.Add(region.key);
                }
            }
            return keys;
        }

        /// <summary>The part of the prompt between two section headers.</summary>
        private static string Section(string prompt, string from, string until)
        {
            int start = prompt.IndexOf(from);
            Assert.Greater(start, -1, $"Section '{from}' missing from the prompt.");
            int end = prompt.IndexOf(until, start);
            Assert.Greater(end, start, $"Section '{until}' missing after '{from}'.");
            return prompt.Substring(start, end - start);
        }

        private static void Write(string prompt)
        {
            Directory.CreateDirectory(DumpDir);
            File.WriteAllText(Path.Combine(DumpDir, "document-prompt.txt"), prompt);
        }

        private static IEnumerator CaptureShot(string shotName)
        {
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(DumpDir, shotName + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }
}
