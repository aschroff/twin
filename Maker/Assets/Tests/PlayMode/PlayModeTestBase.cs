using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PaintCore;

/// <summary>
/// Base class for Play Mode tests with scene loading and Unity-specific helpers.
/// Inherits test secrets management from TestBase.
/// </summary>
public abstract class PlayModeTestBase : TestBase
{
    protected InteractionController Controller;
    protected InteractionModeDictionary InteractionModes;
    private System.IDisposable _saveNameScope;

    [UnitySetUp]
    public virtual IEnumerator SetUp()
    {
        var testDataPath = Path.Combine(Application.temporaryCachePath, "MakerPlayModeTests");
        if (Directory.Exists(testDataPath))
            Directory.Delete(testDataPath, recursive: true);
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
        
        yield return SceneManager.LoadSceneAsync("EmptyScene", LoadSceneMode.Single);
        yield return null;

        CwSerialization.HashToModel.Clear();
        CwSerialization.ModelToHash.Clear();

    }

    protected IEnumerator WaitForModeActive(string modeName, float timeout = 10f)
    {
        Assert.IsTrue(
            InteractionModes.TryGetValue(modeName, out var modeObject),
            $"Mode '{modeName}' not configured in interactionModes."
        );
        float elapsed = 0f;
        while (!modeObject.activeInHierarchy && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Assert.IsTrue(modeObject.activeInHierarchy, $"Mode '{modeName}' was not active after {timeout}s.");
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
        var go = FindGameObjectByPath(path);
        Assert.IsTrue(go.activeInHierarchy, $"GameObject at path '{path}' is not active in hierarchy.");
    }

    protected void AssertTextValue(string path, string expectedValue)
    {
        var go = FindGameObjectByPath(path);
        var textComponent = go.GetComponent<Text>();
        Assert.IsNotNull(textComponent, $"Text component not found on GameObject at path '{path}'.");
        Assert.AreEqual(expectedValue, textComponent.text, $"Text value at path '{path}' does not match.");
    }

    protected GameObject FindGameObjectByPath(string path)
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
                return go;
            }
        }

        Assert.Fail($"GameObject at path '{path}' not found in scene.");
        return null;
    }

    protected static IEnumerator ClickButtonByName(string name)
    {
        var button = FindButtonByName(name);
        button.onClick.Invoke();
        yield return null;
        yield return null;
    }
    
    protected static IEnumerator ClickButtonByNameDebug(string name, float timeout = 10f)
    {
        var button = FindButtonByName(name);
        float elapsed = 0f;
        while ((!button.gameObject.activeInHierarchy || !button.interactable) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        if (!button.gameObject.activeInHierarchy)
        {
            var t = button.transform;
            while (t != null)
            {
                Debug.Log($"[Test] Hierarchy: '{t.name}' activeSelf={t.gameObject.activeSelf}");
                t = t.parent;
            }
        }
        Debug.Log($"[Test] ClickButtonByName: '{name}', active={button.gameObject.activeInHierarchy}, interactable={button.interactable}, waited={elapsed:F2}s");
        Assert.IsTrue(button.gameObject.activeInHierarchy, $"Button '{name}' is not active after {timeout}s.");
        button.onClick.Invoke();
        yield return null;
        yield return null;
    }

    protected static IEnumerator ClickButtonByPath(string path, float timeout = 10f)
    {
        var button = FindButtonByPath(path);
        float elapsed = 0f;
        while ((!button.gameObject.activeInHierarchy || !button.interactable) && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        Assert.IsTrue(button.gameObject.activeInHierarchy, $"Button at path '{path}' is not active after {timeout}s.");
        button.onClick.Invoke();
        yield return null;
        yield return null;
    }

    protected static Button FindButtonByName(string name)
    {
        var buttons = Object.FindObjectsOfType<Button>(false);
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

    protected static void SetInputByName(string name, string value)
    {
        var gameObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var go in gameObjects)
        {
            if (go != null && go.name == name)
            {
                var inputField = go.GetComponent<InputField>();
                if (inputField != null)
                {
                    inputField.text = value;
                    return;
                }
            }
        }

        Assert.Fail($"Active GameObject with name '{name}' and InputField component not found in scene.");
    }

    protected GameObject FindChildWithTextValue(string parentPath, string textValue)
    {
        var parent = FindGameObjectByPath(parentPath);
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent.transform)
        {
            var nameChild = child.Find("Name");
            if (nameChild != null)
            {
                var textChild = nameChild.Find("Text");
                if (textChild != null)
                {
                    var textComponent = textChild.GetComponent<Text>();
                    if (textComponent != null && textComponent.text == textValue)
                    {
                        return child.gameObject;
                    }
                }
            }
        }

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
