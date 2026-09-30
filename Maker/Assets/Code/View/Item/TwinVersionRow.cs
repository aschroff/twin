using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One version of the current twin on the upload or the download screen: its name, where it
/// lives, and a checkbox for the ones that could still be moved.
///
/// The row is dumb on purpose. It is handed finished strings rather than keys, because what a row
/// says depends on which of five locales is active and on values only the manager has — the
/// uploader's address and the time. Composing that here would put localisation in two places.
///
/// A version the other side already has cannot be selected. On the upload screen that is not
/// tidiness: the server refuses a second upload of the same twin name and version with
/// TWIN_VERSION_ALREADY_EXISTS, and it is a permanent answer, so offering the tick would be
/// offering a guaranteed failure. On the download screen it is the same promise kept for the same
/// reason — fetching a version this device already has would either duplicate it under a V01
/// suffix or overwrite work, and neither is what a tick appears to offer.
///
/// Modelled on <see cref="DocumentReviewRow"/> — same shape, same reason for the Toggle sitting on
/// the row itself rather than on the little box: a 38-unit box is nothing to aim at on a tablet.
/// </summary>
public class TwinVersionRow : MonoBehaviour
{
    /// <summary>
    /// Which direction the screen this row sits on moves versions in.
    /// </summary>
    /// <remarks>
    /// The row cannot work this out for itself, and it decides the only two
    /// things that differ between the two screens: which state can be ticked, and
    /// which one reads as "nothing left to do". Upload is the default so that the
    /// screen that existed first needs to say nothing.
    /// </remarks>
    public enum Role
    {
        /// <summary>From this device to the server.</summary>
        Upload,

        /// <summary>From the server to this device.</summary>
        Download
    }

    public enum State
    {
        /// <summary>
        /// Exists in this app only. The one state that can be uploaded — and on
        /// the download screen, one of the two that are there for reference.
        /// </summary>
        LocalOnly,

        /// <summary>On the server and not on this device. The one state that can be downloaded.</summary>
        ServerOnly,

        /// <summary>An upload is in flight for this row.</summary>
        Uploading,

        /// <summary>A download is in flight for this row.</summary>
        Downloading,

        /// <summary>On the server — either found there, or just put there.</summary>
        Uploaded,

        /// <summary>On this device as well as on the server — either found so, or just fetched.</summary>
        Downloaded,

        /// <summary>The transfer failed. Selectable again, unless the failure was permanent.</summary>
        Failed
    }

    /// <summary>Name of the child holding the box and the tick.</summary>
    private const string SelectorName = "Selector";

    /// <summary>The child holding the version name.</summary>
    private const string LabelPath = "InputField/Text";

    /// <summary>The child holding the second line: where this version lives.</summary>
    private const string StatePath = "State/Text";

    /// <summary>How far a row is dimmed once it can no longer be acted on.</summary>
    private const float DimmedAlpha = 0.45f;

    private Toggle toggle;
    private Text label;
    private Text stateLabel;
    private Transform selector;

    /// <summary>The twin this version belongs to, as the app exported it.</summary>
    public string TwinName { get; private set; }

    /// <summary>The version part alone, e.g. <c>000</c>.</summary>
    public string VersionName { get; private set; }

    /// <summary>The local profile id, <c>"&lt;TwinName&gt;.&lt;VersionName&gt;"</c>.</summary>
    public string ProfileId => $"{TwinName}.{VersionName}";

    public State CurrentState { get; private set; } = State.LocalOnly;

    /// <summary>Which screen this row is on. See <see cref="Role"/>.</summary>
    public Role CurrentRole { get; private set; } = Role.Upload;

    /// <summary>
    /// Say which screen the row is on. Before <see cref="Fill"/>, so the first
    /// state it lands in is already read the right way round.
    /// </summary>
    public void SetRole(Role role) => CurrentRole = role;

    /// <summary>
    /// Whether this row is ticked. False for anything that cannot be acted on, whatever the
    /// Toggle happens to hold — a disabled box is not a selection.
    /// </summary>
    public bool Selected
    {
        get => CanBeSelected && Toggle() != null && Toggle().isOn;
        set { if (Toggle() != null && CanBeSelected) Toggle().isOn = value; }
    }

    /// <summary>
    /// Only a version the other side does not have can be moved: one that is not
    /// already up there can go up, one that is not already here can come down.
    /// </summary>
    public bool CanBeSelected => CurrentState == State.Failed || CurrentState == (CurrentRole == Role.Upload
        ? State.LocalOnly
        : State.ServerOnly);

    /// <summary>
    /// Whether the side this screen moves versions to already has this one, so
    /// there is nothing left to do with the row.
    /// </summary>
    /// <remarks>
    /// The tick and the dimming both come from here, which is what makes the two
    /// screens read the same way: a ticked, dead, dimmed box means "that side has
    /// it", and an empty live box means "this is what the button would move".
    ///
    /// On the download screen that covers both reference states — a version that
    /// is on the server as well as here, and one that has never been uploaded —
    /// because from the device's point of view it already has both.
    /// </remarks>
    public bool IsSettled => CurrentRole == Role.Upload
        ? CurrentState == State.Uploaded
        : CurrentState == State.Downloaded || CurrentState == State.LocalOnly;

    /// <summary>
    /// Put a version on the row. It starts unticked: nothing leaves the device that the user did
    /// not tick.
    /// </summary>
    public void Fill(string twinName, string versionName, string stateText)
    {
        TwinName = twinName;
        VersionName = versionName;

        if (Label() != null) Label().text = versionName;
        SetState(State.LocalOnly, stateText);
        if (Toggle() != null) Toggle().isOn = false;
    }

    /// <summary>
    /// Move the row to a state and say so in its second line.
    /// </summary>
    /// <remarks>
    /// One method rather than four, so the three things that always belong together — the state,
    /// what the box does, and what the line says — cannot drift apart.
    /// </remarks>
    public void SetState(State state, string stateText)
    {
        CurrentState = state;

        if (StateLabel() != null) StateLabel().text = stateText ?? string.Empty;

        Toggle box = Toggle();
        if (box != null)
        {
            // A settled row shows a ticked, dead box: it reads as "done" rather than as an
            // offer. A transfer in flight is locked for the same reason a login button is.
            box.isOn = IsSettled;
            box.interactable = CanBeSelected;
        }

        if (Selector() != null) Selector().gameObject.SetActive(true);

        var group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = IsSettled ? DimmedAlpha : 1f;
        }
    }

    /// <summary>Called when the user ticks or unticks this row.</summary>
    public void WhenSelectionChanges(UnityEngine.Events.UnityAction<bool> handler)
    {
        if (handler == null || Toggle() == null) return;

        Toggle().onValueChanged.AddListener(handler);
    }

    // Looked up lazily and cached, like the other rows: Awake does not run on a prefab asset,
    // and the manager fills a row in the same frame it instantiates it.
    private Toggle Toggle() => toggle != null ? toggle : (toggle = GetComponent<Toggle>());

    private Text Label() => label != null ? label : (label = FindText(LabelPath));

    private Text StateLabel() => stateLabel != null ? stateLabel : (stateLabel = FindText(StatePath));

    private Transform Selector() => selector != null ? selector : (selector = transform.Find(SelectorName));

    private Text FindText(string path)
    {
        Transform found = transform.Find(path);
        if (found == null)
        {
            Debug.LogError($"[{nameof(TwinVersionRow)}] The row prefab has no '{path}'.", this);
            return null;
        }

        return found.GetComponent<Text>();
    }
}
