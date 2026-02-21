using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public class EditUiPlayModeTests : PlayModeTestBase
{
    [UnityTest]
    public IEnumerator EditButton_EnablesEditMode()
    {
        yield return ResetApp();

        yield return ClickButtonByName("Edit Button");

        AssertModeActive("Edit");
    }
}
