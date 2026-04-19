using System.Collections;
using UnityEngine.TestTools;

namespace NoAPICalls
{
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
}