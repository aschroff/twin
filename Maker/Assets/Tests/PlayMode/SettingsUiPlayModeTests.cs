using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SettingsUiPlayModeTests
{
    [UnityTest]
    public IEnumerator SettingsButton_EnablesSettingsMode()
    {
        yield return SceneManager.LoadSceneAsync("Maker Main", LoadSceneMode.Single);
        yield return null;

        var settingsButton = FindSettingsButton();
        Assert.IsNotNull(settingsButton, "Settings Button not found in scene.");

        settingsButton.onClick.Invoke();
        yield return null;
        yield return null;

        var controller = Object.FindObjectOfType<InteractionController>();
        Assert.IsNotNull(controller, "InteractionController not found in scene.");

        var modesField = typeof(InteractionController).GetField(
            "interactionModes",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.IsNotNull(modesField, "interactionModes field not found on InteractionController.");

        var modes = modesField.GetValue(controller) as InteractionModeDictionary;
        Assert.IsNotNull(modes, "interactionModes is null.");
        Assert.IsTrue(
            modes.TryGetValue("Settings", out var settingsMode),
            "Settings mode not configured in interactionModes."
        );
        Assert.IsTrue(
            settingsMode.activeInHierarchy,
            "Settings mode GameObject is not active after clicking Settings Button."
        );

        var testDataPath = Path.Combine(Application.temporaryCachePath, "MakerPlayModeTests");
        Directory.CreateDirectory(testDataPath);
        DataPaths.SetPersistentDataPathForTests(testDataPath);
        var saveNameScope = PaintableSaveNameOverride.Begin("PlayModeTest");
        try
        {
            var resetButton = FindButtonByPath("Canvas/Settings UI/SettingsPanel/Reset");
            Assert.IsNotNull(resetButton, "Reset Button not found in scene.");
            resetButton.onClick.Invoke();
            yield return null;
        }
        finally
        {
            saveNameScope.Dispose();
            DataPaths.ClearPersistentDataPathOverride();
        }
    }

    private static Button FindSettingsButton()
    {
        var buttons = Object.FindObjectsOfType<Button>(true);
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.name == "Settings Button")
            {
                return button;
            }
        }

        return null;
    }

    private static Button FindButtonByPath(string path)
    {
        var buttons = Object.FindObjectsOfType<Button>(true);
        foreach (var button in buttons)
        {
            if (button == null)
            {
                continue;
            }

            var buttonPath = GetTransformPath(button.transform);
            if (buttonPath == path)
            {
                return button;
            }
        }

        return null;
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return string.Empty;
        }

        var path = target.name;
        var parent = target.parent;
        while (parent != null)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }

        return path;
    }
}
