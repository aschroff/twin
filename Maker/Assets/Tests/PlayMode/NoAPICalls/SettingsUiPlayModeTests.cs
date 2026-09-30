using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace NoAPICalls
{
    [Category(Processes.AppFrame)]
    public class SettingsUiPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator SettingsButton_EnablesSettingsMode()
        {
            yield return ClickButtonByName("Settings Button");

            AssertModeActive("Settings");

            yield return ClickButtonByPath("Canvas/Settings UI/SettingsPanel/Reset");

            AssertModeActive("Main");

            AssertGameObjectActive("Canvas/Overlays/Group Overlay");
            AssertGameObjectActive("Canvas/Overlays/View Overlay");
            AssertGameObjectActive("Canvas/Overlays/Overview Overlay");
            AssertGameObjectActive("Canvas/Main UI/Bottom/NewVersion");
            AssertGameObjectActive("Canvas/Main UI/Bottom/GroupDetail");
            AssertGameObjectActive("Canvas/Main UI/Bottom/HelpMe");
            AssertGameObjectActive("Canvas/Main UI/Top/GameObject/Settings Button");

            AssertTextValue("Canvas/Main UI/Top/GameObject/Save Button/Profile", "default");
            AssertTextValue("Canvas/Overlays/View Overlay/Scroll/Panel/Overlay Scroll read only and icon(Clone)/ReadOnlyMode/Text Background/ViewName", "Default view");
        }
    }
}
