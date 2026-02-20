using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class SettingsUiPlayModeTests : PlayModeTestBase
{
    [UnityTest]
    public IEnumerator SettingsButton_EnablesSettingsMode()
    {
        yield return ClickButtonByName("Settings Button");

        AssertModeActive("Settings");

        yield return ClickButtonByPath("Canvas/Settings UI/SettingsPanel/Reset");

        AssertModeActive("Main");
    }
}
