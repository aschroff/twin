using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace EditModeTests
{
    /// <summary>
    /// Which rows can be ticked on each of the two version screens.
    /// </summary>
    /// <remarks>
    /// <para>One row serves both the upload and the download screen, and the only thing that
    /// differs between them is this: which single state is an offer, and which ones mean the other
    /// side already has that version. Getting it backwards is not a crash - it is a tick that
    /// promises a transfer the server refuses, or a version that can never be fetched, and neither
    /// shows up anywhere but on screen.</para>
    ///
    /// <para>An EditMode test because none of this needs a scene: a row is a Toggle, two labels and
    /// a rule. The hierarchy below is the row prefab's, reduced to the children the component looks
    /// up by path.</para>
    /// </remarks>
    [Category(Processes.ExchangeTwins)]
    public class TwinVersionRowTests
    {
        private GameObject _go;
        private TwinVersionRow _row;
        private Toggle _toggle;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Row");
            _toggle = _go.AddComponent<Toggle>();
            _go.AddComponent<CanvasGroup>();

            Child("Selector");
            Text("InputField", "Text");
            Text("State", "Text");

            _row = _go.AddComponent<TwinVersionRow>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // --- the upload screen ------------------------------------------------

        [Test]
        public void A_version_only_on_this_device_can_be_uploaded()
        {
            Fill(TwinVersionRow.Role.Upload, TwinVersionRow.State.LocalOnly);

            Assert.IsTrue(_row.CanBeSelected);
            Assert.IsFalse(_row.IsSettled, "The server does not have it yet.");
        }

        [Test]
        public void A_version_already_on_the_server_cannot_be_uploaded_again()
        {
            Fill(TwinVersionRow.Role.Upload, TwinVersionRow.State.Uploaded);

            Assert.IsFalse(_row.CanBeSelected,
                "A second upload of the same name and version is refused permanently.");
            Assert.IsTrue(_toggle.isOn, "It shows as done rather than as an offer.");
            Assert.IsFalse(_toggle.interactable);
        }

        // --- the download screen ----------------------------------------------

        [Test]
        public void A_version_only_on_the_server_can_be_downloaded()
        {
            Fill(TwinVersionRow.Role.Download, TwinVersionRow.State.ServerOnly);

            Assert.IsTrue(_row.CanBeSelected);
            Assert.IsFalse(_row.IsSettled, "This device does not have it yet.");
            Assert.IsFalse(_toggle.isOn, "Nothing is fetched that the user did not tick.");
            Assert.IsTrue(_toggle.interactable);
        }

        [Test]
        public void A_version_that_is_here_and_on_the_server_is_reference_only()
        {
            Fill(TwinVersionRow.Role.Download, TwinVersionRow.State.Downloaded);

            Assert.IsFalse(_row.CanBeSelected,
                "Fetching it again would land beside it as a V01 rather than replace it.");
            Assert.IsTrue(_toggle.isOn);
            Assert.IsFalse(_toggle.interactable);
        }

        [Test]
        public void A_version_only_on_this_device_is_reference_only_when_downloading()
        {
            Fill(TwinVersionRow.Role.Download, TwinVersionRow.State.LocalOnly);

            Assert.IsFalse(_row.CanBeSelected, "There is nothing on the server to fetch.");
            Assert.IsTrue(_row.IsSettled, "The device already has it, which is what the tick says.");
            Assert.IsTrue(_toggle.isOn);
        }

        [Test]
        public void The_same_state_reads_opposite_ways_on_the_two_screens()
        {
            // The one property the whole split rests on, stated once as a test rather than left
            // implied by the five above.
            Fill(TwinVersionRow.Role.Upload, TwinVersionRow.State.LocalOnly);
            Assert.IsTrue(_row.CanBeSelected, "Upload: local-only is the offer.");

            _row.SetRole(TwinVersionRow.Role.Download);
            _row.SetState(TwinVersionRow.State.LocalOnly, "local only");
            Assert.IsFalse(_row.CanBeSelected, "Download: local-only is reference.");
        }

        // --- rules both screens share -----------------------------------------

        [Test]
        public void A_failed_transfer_can_be_tried_again_on_either_screen()
        {
            Fill(TwinVersionRow.Role.Upload, TwinVersionRow.State.Failed);
            Assert.IsTrue(_row.CanBeSelected);

            _row.SetRole(TwinVersionRow.Role.Download);
            _row.SetState(TwinVersionRow.State.Failed, "failed");
            Assert.IsTrue(_row.CanBeSelected);
        }

        [Test]
        public void A_transfer_in_flight_is_locked()
        {
            Fill(TwinVersionRow.Role.Download, TwinVersionRow.State.Downloading);

            Assert.IsFalse(_row.CanBeSelected, "Ticking a row mid-transfer would queue it twice.");
            Assert.IsFalse(_toggle.interactable);
        }

        [Test]
        public void A_dead_box_that_is_ticked_is_still_not_a_selection()
        {
            Fill(TwinVersionRow.Role.Download, TwinVersionRow.State.Downloaded);

            // Whatever the Toggle holds - and a settled row holds a tick - the row is not offering
            // itself. A manager that read the Toggle instead would try to fetch every version the
            // device already has.
            Assert.IsTrue(_toggle.isOn);
            Assert.IsFalse(_row.Selected);

            _row.Selected = true;
            Assert.IsFalse(_row.Selected, "Setting it does not make it selectable either.");
        }

        // --- helpers -----------------------------------------------------------

        private void Fill(TwinVersionRow.Role role, TwinVersionRow.State state)
        {
            _row.SetRole(role);
            _row.Fill("Anna Beispiel", "003", "somewhere");
            _row.SetState(state, state.ToString());
        }

        private Transform Child(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(_go.transform, false);
            return child.transform;
        }

        private void Text(string parent, string name)
        {
            var holder = Child(parent);
            var text = new GameObject(name);
            text.transform.SetParent(holder, false);
            text.AddComponent<Text>();
        }
    }
}
