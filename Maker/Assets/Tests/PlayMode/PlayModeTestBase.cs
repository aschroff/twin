using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public abstract class PlayModeTestBase
{
    protected InteractionController Controller;
    protected InteractionModeDictionary InteractionModes;
    private System.IDisposable _saveNameScope;

    [UnitySetUp]
    public virtual IEnumerator SetUp()
    {
        var testDataPath = Path.Combine(Application.temporaryCachePath, "MakerPlayModeTests");
        Directory.CreateDirectory(testDataPath);
        DataPaths.SetPersistentDataPathForTests(testDataPath);
        _saveNameScope = PaintableSaveNameOverride.Begin("PlayModeTest");

        yield return SceneManager.LoadSceneAsync("Maker Main", LoadSceneMode.Single);
        yield return null;

        Controller = Object.FindObjectOfType<InteractionController>();
        Assert.IsNotNull(Controller, "InteractionController not found in scene.");

        var modesField = typeof(InteractionController).GetField(
            "interactionModes",
            BindingFlags.NonPublic | BindingFlags.Instance
        );
        Assert.IsNotNull(modesField, "interactionModes field not found on InteractionController.");

        InteractionModes = modesField.GetValue(Controller) as InteractionModeDictionary;
        Assert.IsNotNull(InteractionModes, "interactionModes is null.");
    }

    [UnityTearDown]
    public virtual IEnumerator TearDown()
    {
        _saveNameScope?.Dispose();
        DataPaths.ClearPersistentDataPathOverride();
        yield return null;
    }

    protected void AssertModeActive(string modeName)
    {
        Assert.IsTrue(
            InteractionModes.TryGetValue(modeName, out var modeObject),
            $"Mode '{modeName}' not configured in interactionModes."
        );
        Assert.IsTrue(
            modeObject.activeInHierarchy,
            $"Mode '{modeName}' GameObject is not active."
        );
    }

    protected void AssertGameObjectActive(string path)
    {
        var gameObjects = Object.FindObjectsOfType<GameObject>(true);
        foreach (var go in gameObjects)
        {
            if (go == null)
            {
                continue;
            }

            if (GetTransformPath(go.transform) == path)
            {
                Assert.IsTrue(go.activeInHierarchy, $"GameObject at path '{path}' is not active in hierarchy.");
                return;
            }
        }

        Assert.Fail($"GameObject at path '{path}' not found in scene.");
    }

    protected static IEnumerator ClickButtonByName(string name)
    {
        var button = FindButtonByName(name);
        button.onClick.Invoke();
        yield return null;
        yield return null;
    }

    protected static IEnumerator ClickButtonByPath(string path)
    {
        var button = FindButtonByPath(path);
        button.onClick.Invoke();
        yield return null;
        yield return null;
    }

    protected static Button FindButtonByName(string name)
    {
        var buttons = Object.FindObjectsOfType<Button>(true);
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.name == name)
            {
                return button;
            }
        }

        Assert.IsNotNull(null, $"Button with name '{name}' not found in scene.");
        return null;
    }

    protected static Button FindButtonByPath(string path)
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

        Assert.IsNotNull(null, $"Button with path '{path}' not found in scene.");
        return null;
    }

    protected static string GetTransformPath(Transform target)
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
