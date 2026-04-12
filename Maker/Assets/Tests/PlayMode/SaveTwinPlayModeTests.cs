using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class SaveTwinPlayModeTests : PlayModeTestBase
{
    const string TwinName = "TestTwin";
    
    [UnityTest]
    public IEnumerator SaveButton_OpensSaveMode()
    {
        yield return ClickButtonByName("SaveButton");

        AssertModeActive("SaveMode");

        AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/config/New/New");
        AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/config/New/Save as");
        AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/config/New/InputField");
        AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/Reset");
        AssertGameObjectActive("Canvas/Save UI/Bottom/Buttons/Export");
        
        SetInputByName("InputField", TwinName);
        
        yield return ClickButtonByName("New");
        
        AssertModeActive("MainMode");
        
        yield return ClickButtonByName("SaveButton");

        var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", TwinName);
        Assert.IsNotNull(twinEntry, $"Twin with name '{TwinName}' not found in save list.");
    }
}
