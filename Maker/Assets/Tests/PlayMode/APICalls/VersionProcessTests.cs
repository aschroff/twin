using System.Collections;
using System.Reflection;
using Code;
using Code.AI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;


namespace APICalls {
[Category(Processes.DescribeAndReport)]
public class VersionProcessTests : PlayModeTestBase
{
    private const string Variant = "Medical Report"; 

    [UnityTest]
    public IEnumerator DescribeVersion_SetsPromptResultFromRealPart()
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

        // VersionProcess only calls the AI once every part has a description, so set one directly here
        // rather than running PartDescriptionProcess and spending a second real AI call on it.
        part.description = "Mild swelling observed.";
        Assert.IsTrue(partManager.AllPartsDescribed(), "Expected all parts to be described after setting the part description.");

        var settingsManager = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        Assert.IsNotNull(settingsManager, "SettingsManager not found in scene.");
        var itemPrompt = settingsManager.getPromptObject(Variant, ItemPrompt.PromptLevel.Version);
        Assert.IsNotNull(itemPrompt, $"No Version-level ItemPrompt found for label '{Variant}'.");
        itemPrompt.promptResult = "";

        var versionProcess = Object.FindFirstObjectByType<VersionProcess>();
        Assert.IsNotNull(versionProcess, "VersionProcess not found in scene.");

        versionProcess.Handle(Variant);

        yield return WaitUntilOrTimeout(
            () => !string.IsNullOrEmpty(itemPrompt.promptResult),
            30f,
            "VersionProcess did not set a prompt result in time.");

        Assert.IsFalse(itemPrompt.promptResult.StartsWith("Error:"), $"AI call failed: {itemPrompt.promptResult}");
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
