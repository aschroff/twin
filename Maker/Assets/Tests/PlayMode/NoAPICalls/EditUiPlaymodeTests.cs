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
            
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Delete");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Alphanumeric");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Sticker");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Marker");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Filler");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Shape");

            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            
            AssertModeActive("EditMarker");

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
            
            AssertGameObjectActive("Tools/Red");


        }
    }
}