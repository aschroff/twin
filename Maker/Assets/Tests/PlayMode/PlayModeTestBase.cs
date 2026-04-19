using System.Collections;
using System.IO;
using System.Reflection;
using CW.Common;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
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
    
    protected IEnumerator ResetApp()
    {
        yield return ClickButtonByName("Settings Button");
        yield return ClickButtonByPath("Canvas/Settings UI/SettingsPanel/Reset");
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

    protected GameObject FindGameObjectByPath(string path, GameObject root = null)
    {
        if (root != null)
        {
            var found = root.transform.Find(path);
            Assert.IsNotNull(found, $"GameObject at path '{path}' not found under '{root.name}'.");
            return found.gameObject;
        }

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
        var button = TryFindButtonByName(name);
        if (button != null)
        {
            button.onClick.Invoke();
        }
        else
        {
            var cwButton = FindCwDemoButtonByName(name);
            cwButton.OnPointerDown(new PointerEventData(EventSystem.current));
        }
        yield return null;
        yield return null;
    }

    protected static IEnumerator ClickButtonByNameDebug(string name, float timeout = 10f)
    {
        var button = TryFindButtonByName(name);
        if (button != null)
        {
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
        }
        else
        {
            var cwButton = FindCwDemoButtonByName(name);
            float elapsed = 0f;
            while (!cwButton.gameObject.activeInHierarchy && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Debug.Log($"[Test] ClickButtonByName (CwDemoButton): '{name}', active={cwButton.gameObject.activeInHierarchy}, waited={elapsed:F2}s");
            Assert.IsTrue(cwButton.gameObject.activeInHierarchy, $"CwDemoButton '{name}' is not active after {timeout}s.");
            cwButton.OnPointerDown(new PointerEventData(EventSystem.current));
        }
        yield return null;
        yield return null;
    }

    protected static IEnumerator ClickButtonByPath(string path, float timeout = 10f, GameObject root = null)
    {
        var button = TryFindButtonByPath(path, root);
        if (button != null)
        {
            float elapsed = 0f;
            while ((!button.gameObject.activeInHierarchy || !button.interactable) && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(button.gameObject.activeInHierarchy, $"Button at path '{path}' is not active after {timeout}s.");
            button.onClick.Invoke();
        }
        else
        {
            var cwButton = FindCwDemoButtonByPath(path, root);
            float elapsed = 0f;
            while (!cwButton.gameObject.activeInHierarchy && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(cwButton.gameObject.activeInHierarchy, $"CwDemoButton at path '{path}' is not active after {timeout}s.");
            cwButton.OnPointerDown(new PointerEventData(EventSystem.current));
        }
        yield return null;
        yield return null;
    }

    protected static Button FindButtonByName(string name)
    {
        var button = TryFindButtonByName(name);
        Assert.IsNotNull(button, $"Button with name '{name}' not found in scene.");
        return button;
    }

    protected static Button FindButtonByPath(string path, GameObject root = null)
    {
        var button = TryFindButtonByPath(path, root);
        Assert.IsNotNull(button, $"Button with path '{path}' not found in scene.");
        return button;
    }

    private static Button TryFindButtonByName(string name)
    {
        var buttons = Object.FindObjectsOfType<Button>(false);
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.name == name)
                return button;
        }
        return null;
    }

    private static Button TryFindButtonByPath(string path, GameObject root = null)
    {
        var buttons = root != null
            ? root.GetComponentsInChildren<Button>(true)
            : Object.FindObjectsOfType<Button>(true);
        foreach (var button in buttons)
        {
            var buttonPath = root != null
                ? GetRelativeTransformPath(button.transform, root.transform)
                : GetTransformPath(button.transform);
            if (button != null && buttonPath == path)
                return button;
        }
        return null;
    }

    private static CwDemoButton FindCwDemoButtonByName(string name)
    {
        var buttons = Object.FindObjectsOfType<CwDemoButton>(false);
        foreach (var button in buttons)
        {
            if (button != null && button.gameObject.name == name)
                return button;
        }
        Assert.Fail($"Neither Button nor CwDemoButton with name '{name}' found in scene.");
        return null;
    }

    private static CwDemoButton FindCwDemoButtonByPath(string path, GameObject root = null)
    {
        var buttons = root != null
            ? root.GetComponentsInChildren<CwDemoButton>(true)
            : Object.FindObjectsOfType<CwDemoButton>(true);
        foreach (var button in buttons)
        {
            var buttonPath = root != null
                ? GetRelativeTransformPath(button.transform, root.transform)
                : GetTransformPath(button.transform);
            if (button != null && buttonPath == path)
                return button;
        }
        Assert.Fail($"Neither Button nor CwDemoButton at path '{path}' found in scene.");
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

    protected GameObject FindChildWithTextValue(string parentPath, string textValue, string textPath = "Name/Text")
    {
        var parent = FindGameObjectByPath(parentPath);
        if (parent == null) return null;

        foreach (Transform child in parent.transform)
        {
            var textTransform = child.Find(textPath);
            if (textTransform == null) continue;
            var textComponent = textTransform.GetComponent<Text>();
            if (textComponent != null && textComponent.text == textValue)
                return child.gameObject;
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

    private static string GetRelativeTransformPath(Transform target, Transform root)
    {
        if (target == null) return string.Empty;
        var path = target.name;
        var parent = target.parent;
        while (parent != null && parent != root)
        {
            path = $"{parent.name}/{path}";
            parent = parent.parent;
        }
        return parent == root ? path : string.Empty;
    }
}
