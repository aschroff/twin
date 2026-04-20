using System.Collections;
using UnityEngine.TestTools;
using UnityEngine;

namespace NoAPICalls
{
    public class EditUiPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator EditButton_EnablesEditMode()
        {
            yield return ResetApp();
            
            yield return ClickButtonByName("Save Button");

            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", "LipEdema");
            
            yield return ClickButtonByPath(path: "Unselect", root: twinEntry);
            
            AssertModeActive("Save");
            
            
            yield return ClickButtonByName("Edit Button");

            AssertModeActive("Edit");
            
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Delete");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Alphanumeric");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Sticker");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Marker");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Filler");
            AssertGameObjectActive("Canvas/Edit UI/Bottom/Shape");
            
            var viewEntry = FindChildWithTextValue("Canvas/Overlays/View Overlay/Scroll/Panel", "Head front", "ReadOnlyMode/Text Background/ViewName");
            
            yield return ClickButtonByPath(path: "ReadOnlyMode/Icon", root: viewEntry);
            

            yield return ClickButtonByPath("Canvas/Edit UI/Bottom/Marker/Text Background/Text");
            
            AssertModeActive("EditMarker");

            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Scroll/Panel/Red");
            
            AssertGameObjectActive("Tools/Red");

            yield return DragOnCanvas("Canvas", new Vector2(20, 0));
            
            yield return ClickButtonByPath("Canvas/EditMarker UI/Bottom/Buttons/Link");
            
            AssertModeActive("Edit");
           
            yield return ClickButtonByPath("Canvas/Edit UI/Top/GameObject/Back Button");

            AssertModeActive("Main");

            yield return ClickButtonByPath("Canvas/Main UI/Bottom/GroupDetail/Icon");
            
            AssertModeActive("GroupDetail");

            AssertDirectChildCount("Canvas/GroupDetailUI/ScrollDetails/Panel", 1);

        }
    }
}