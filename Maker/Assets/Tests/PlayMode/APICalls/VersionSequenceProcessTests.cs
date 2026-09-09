using System.Collections;
using System.Reflection;
using Code;
using Code.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


namespace APICalls {
// Covers the "VersionSequenceProcess" GameObject in the scene, which chains
// PartsScreenshotProcess -> PartsDescriptionProcess -> VersionProcess via SequenceProcess.
public class VersionSequenceProcessTests : PlayModeTestBase
{
    private const string Variant = "Medical Report"; // matches the "Medical Report" ItemPrompts (Part + Version) in Settings UI

    [UnityTest]
    public IEnumerator RunSequence_DescribesPartAndVersionFromRealScreenshot()
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

        // paint on twin to ensure existence of a part, with no screenshot and no description yet -
        // the sequence has to produce both itself.
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

        var settingsManager = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        Assert.IsNotNull(settingsManager, "SettingsManager not found in scene.");
        var itemPrompt = settingsManager.getPromptObject(Variant, ItemPrompt.PromptLevel.Version);
        Assert.IsNotNull(itemPrompt, $"No Version-level ItemPrompt found for label '{Variant}'.");
        itemPrompt.promptResult = "";

        var sequenceProcess = Object.FindFirstObjectByType<SequenceProcess>();
        Assert.IsNotNull(sequenceProcess, "SequenceProcess (VersionSequenceProcess) not found in scene.");

        sequenceProcess.Handle(Variant);

        // The sequence runs PartsScreenshotProcess, then PartsDescriptionProcess, then VersionProcess
        // one after another (each awaited via ExecuteCompleted), so give it room for two real AI calls
        // plus VersionProcess's own internal wait for the part description to land.
        yield return WaitUntilOrTimeout(
            () => !string.IsNullOrEmpty(itemPrompt.promptResult),
            60f,
            "VersionSequenceProcess did not produce a version prompt result in time.");

        Assert.IsFalse(part.description.StartsWith("Error:"), $"Part description AI call failed: {part.description}");
        Assert.IsFalse(itemPrompt.promptResult.StartsWith("Error:"), $"Version AI call failed: {itemPrompt.promptResult}");
        Debug.Log($"AI part description: {part.description}");
        Debug.Log($"AI version description: {itemPrompt.promptResult}");
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
