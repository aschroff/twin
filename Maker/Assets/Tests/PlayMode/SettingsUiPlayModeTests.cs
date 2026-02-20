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

        Assert.IsTrue(
            InteractionModes.TryGetValue("Settings", out var settingsMode),
            "Settings mode not configured in interactionModes."
        );
        Assert.IsTrue(
            settingsMode.activeInHierarchy,
            "Settings mode GameObject is not active after clicking Settings Button."
        );

        yield return ClickButtonByPath("Canvas/Settings UI/SettingsPanel/Reset");
    }
}
