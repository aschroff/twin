using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace NoAPICalls
{
    /// <summary>
    /// Twin name validation on the save screen (<c>TwinNameValidator</c>): a duplicate name, an
    /// invalid character, an empty/whitespace-only name, a name containing a dot, or a too-long
    /// name must all leave the screen in Save mode and show a toast - "New" and "Save as" both go
    /// through the same check.
    /// </summary>
    public class TwinNameValidationPlayModeTests : PlayModeTestBase
    {
        const string NotificationText = "Canvas/Overlays/Notification Overlay/Panel/Text";

        /// <summary>"default" is the only twin left after a reset, so it is always available as
        /// a name already taken.</summary>
        const string ExistingTwin = "default";

        [UnityTest]
        public IEnumerator DuplicateName_IsRejectedByNewAndBySaveAs()
        {
            yield return ResetApp();
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            SetInputByName("InputField", ExistingTwin);
            yield return ClickButtonByName("New");

            AssertModeActive("Save");
            AssertTextValue(NotificationText, TwinNameValidator.AlreadyExistingNameMessage);

            SetInputByName("InputField", ExistingTwin);
            yield return ClickButtonByName("Save as");

            AssertModeActive("Save");
            AssertTextValue(NotificationText, TwinNameValidator.AlreadyExistingNameMessage);
        }

        [UnityTest]
        public IEnumerator InvalidCharacters_AreRejected()
        {
            yield return ResetApp();
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            // non-ASCII letters, symbols, and reserved punctuation - all from the manual "Name
            // tests" table.
            string[] invalidNames = { "äüö", "!@#$%^&*", "=+.>,</'\"?" };
            foreach (string invalidName in invalidNames)
            {
                SetInputByName("InputField", invalidName);
                yield return ClickButtonByName("New");

                AssertModeActive("Save");
                AssertTextValue(NotificationText, TwinNameValidator.InvalidNameMessage);
            }
        }

        [UnityTest]
        public IEnumerator EmptyOrWhitespaceName_IsRejected()
        {
            yield return ResetApp();
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            foreach (string blankName in new[] { "", " " })
            {
                SetInputByName("InputField", blankName);
                yield return ClickButtonByName("New");

                AssertModeActive("Save");
                AssertTextValue(NotificationText, TwinNameValidator.InvalidNameMessage);
            }
        }

        /// <summary>The UI always appends ".000" to whatever the user types (see
        /// <c>ConfigManager.GetTwinNameFromInput</c>), and <c>TwinNameValidator.IsValidInput</c>
        /// requires exactly one dot in that combined string. A typed name that already contains a
        /// dot therefore ends up with two dots and is rejected before the character regex ever
        /// runs - even though every individual character in it is otherwise allowed.</summary>
        [UnityTest]
        public IEnumerator NameContainingDot_IsRejectedByDotCountCheck()
        {
            yield return ResetApp();
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            const string nameWithDot = "abc.def";
            SetInputByName("InputField", nameWithDot);
            yield return ClickButtonByName("New");

            AssertModeActive("Save");
            AssertTextValue(NotificationText, TwinNameValidator.InvalidNameMessage);
        }

        /// <summary>The regex is "^[a-zA-Z0-9_()-]{1,11}$" - 11 characters is still valid, 12 is
        /// already too long.</summary>
        [UnityTest]
        public IEnumerator NameLength_ElevenCharactersSucceeds_TwelveCharactersFails()
        {
            yield return ResetApp();
            yield return ClickButtonByName("Save Button");
            yield return WaitForModeActive("Save");

            const string twelveChars = "0123456789ab";
            SetInputByName("InputField", twelveChars);
            yield return ClickButtonByName("New");

            AssertModeActive("Save");
            AssertTextValue(NotificationText, TwinNameValidator.InvalidNameMessage);

            const string elevenChars = "Eleven12345";
            Assert.AreEqual(11, elevenChars.Length, "Setup: the boundary name should be exactly 11 characters.");
            SetInputByName("InputField", elevenChars);
            yield return ClickButtonByName("New");

            AssertModeActive("Main");

            yield return ClickButtonByName("Save Button");
            var twinEntry = FindChildWithTextValue("Canvas/Save UI/Bottom/Scroll/Panel", elevenChars);
            Assert.IsNotNull(twinEntry, $"Twin with name '{elevenChars}' not found in save list.");
        }
    }
}
