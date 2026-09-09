using System.Collections;
using System.Reflection;
using Code;
using Code.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


namespace NoAPICalls {

/* paints one part, generates a real screenshot via PartsScreenshotProcess, calls PartsDescriptionProcess.Handle, 
and waits for the part's description to move past the "Part Number N :\n..." placeholder that Execute() stamps in 
before the real per-part AI call overwrites it. */
public class PartsDescriptionProcessTests : PlayModeTestBase
{
    private const string Variant = "Medical Report"; // matches the "Medical Report (Part Description)" ItemPrompt in Settings UI

    [UnityTest]
    public IEnumerator DescribeParts_SetsDescriptionFromRealScreenshot()
    {
        // Skip test if API key is not configured
        if (!HasOpenAIApiKey)
        {
            Assert.Ignore("API key not configured. Create Assets/Tests/Helper/testsecrets.json from testsecrets.example.json");
            yield break;
        }

        // Use our own test key instead of whatever is baked into the scene's AI component.
        var ai = Object.FindFirstObjectByType<AI>();
        Assert.IsNotNull(ai, "AI component not found in scene.");
        ai.apiKey = OpenAIApiKey;
        typeof(AIService)
            .GetMethod("InitializeClient", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(ai, null);

        yield return ResetApp();

        yield return ClickButtonByName("Save Button");

        var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
        Assert.IsNotNull(twinEntry, "Fixture twin 'LipEdema' not found in save list.");

        yield return ClickButtonByPath(path: "Unselect", root: twinEntry);
        AssertModeActive("Save");

        // paint on twin to ensure existence of a part
        yield return ClickButtonByName("Edit Button");
        AssertModeActive("Edit");

        var viewEntry = FindChildWithTextValue("Canvas/Overlays/View Overlay/Scroll/Panel", "Head front", "ReadOnlyMode/Text Background/ViewName");
        yield return ClickButtonByPath(path: "ReadOnlyMode/Icon", root: viewEntry);

        yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
        AssertModeActive("EditMarker");

        yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
        yield return DragOnCanvas("Canvas", new Vector2(20, 0));

        yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
        AssertModeActive("Edit");

        yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");
        AssertModeActive("Main");

        var partManager = Object.FindFirstObjectByType<PartManager>();

        PartManager.GroupData swellGroup = null;
        foreach (var group in partManager.groups)
        {
            if (group.name == "Swell")
            {
                swellGroup = group;
                break;
            }
        }
        Assert.IsNotNull(swellGroup, "'Swell' group not found on PartManager.");
        Assert.AreEqual(1, swellGroup.groupParts.Count, "Expected exactly one part linked to 'Swell'.");
        var part = swellGroup.groupParts[0];
        Assert.AreEqual("", part.description, "Part should not have a description yet.");

        // PartsDescriptionProcess fans out over every part and, per part, calls PartDescriptionProcess -
        // which only calls the AI if a screenshot for that part already exists on disk. Produce a real
        // one first via PartsScreenshotProcess, the same order VersionSequenceProcess uses.
        var screenshotProcess = Object.FindFirstObjectByType<PartsScreenshotProcess>();
        Assert.IsNotNull(screenshotProcess, "PartsScreenshotProcess not found in scene.");
        var screenshotDone = false;
        screenshotProcess.ExecuteCompleted += () => screenshotDone = true;
        screenshotProcess.ExecuteSync(Variant);
        yield return WaitUntilOrTimeout(() => screenshotDone, 10f, "PartsScreenshotProcess did not complete in time.");

        var partsDescriptionProcess = Object.FindFirstObjectByType<PartsDescriptionProcess>();
        Assert.IsNotNull(partsDescriptionProcess, "PartsDescriptionProcess not found in scene.");

        partsDescriptionProcess.Handle(Variant);

        // Execute() immediately stamps a "Part Number N :\n..." placeholder onto every part before
        // the real per-part AI call (fired concurrently) overwrites it with the actual description.
        yield return WaitUntilOrTimeout(
            () => !string.IsNullOrEmpty(part.description) && !part.description.StartsWith("Part Number"),
            30f,
            "PartsDescriptionProcess did not set a real description in time.");

        Assert.IsFalse(part.description.StartsWith("Error:"), $"AI call failed: {part.description}");
        Debug.Log($"AI part description: {part.description}");
    }

    private static IEnumerator WaitUntilOrTimeout(System.Func<bool> condition, float timeoutSeconds, string failureMessage)
    {
        var elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            yield return null;
            elapsed += Time.deltaTime;
        }
        Assert.IsTrue(condition(), failureMessage);
    }
}
}
