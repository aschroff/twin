using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    public class SaveTwinPlayModeTests : PlayModeTestBase
    {
        const string TwinName = "TestTwin";
    
        [UnityTest]
        public IEnumerator SaveButton_OpensSaveMode()
        {
        
            yield return ClickButtonByName("Save Button");

            AssertModeActive("Save");

            AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/config/Create/New");
            AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/config/Create/Save as");
            AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/config/Create/InputField");
            AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/Reset");
            AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/Export");
        
            SetInputByName("InputField", TwinName);
        
            yield return ClickButtonByName("New");
        
            AssertModeActive("Main");
        
            yield return ClickButtonByName("Save Button");

            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", TwinName);
            Assert.IsNotNull(twinEntry, $"Twin with name '{TwinName}' not found in save list.");
        }
    }
}
