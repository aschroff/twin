using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One proposal on the upload review screen: a line of text and a toggle. What the toggle stands
/// for is <see cref="kind"/> plus <see cref="index"/> — the position in the matching list of the
/// DocumentMapping — so reading the rows back gives a DocumentMappingSelection and the applier
/// never has to be told anything twice.
///
/// The row starts unticked, always: nothing reaches the twin that the user did not tick.
/// See Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md.
/// </summary>
public class DocumentReviewRow : MonoBehaviour
{
    public enum ItemKind
    {
        /// <summary>A heading, no toggle — it groups the rows below it.</summary>
        Heading,
        Painting,
        Group,
        Tool,
        PatientText
    }

    public ItemKind kind;

    /// <summary>Position in the mapping's list for this kind; unused for Heading and PatientText.</summary>
    public int index;

    private Toggle toggle;
    private Text label;

    public bool Confirmed
    {
        get { return kind != ItemKind.Heading && Toggle() != null && Toggle().isOn; }
        set { if (Toggle() != null) Toggle().isOn = value; }
    }

    /// <summary>Fills the row and puts its toggle in the unticked start state. A heading has no
    /// toggle to offer, so its selector is hidden rather than shown unticked.</summary>
    public void Fill(ItemKind itemKind, int itemIndex, string text)
    {
        kind = itemKind;
        index = itemIndex;

        Text field = Label();
        if (field != null)
        {
            field.text = text;
        }

        Toggle box = Toggle();
        if (box != null)
        {
            box.isOn = false;
            box.gameObject.SetActive(kind != ItemKind.Heading);
        }
    }

    /// <summary>What the row says - what a test reads.</summary>
    public string Text()
    {
        Text field = Label();
        return field != null ? field.text : "";
    }

    private Toggle Toggle()
    {
        if (toggle == null)
        {
            toggle = GetComponentInChildren<Toggle>(true);
        }
        return toggle;
    }

    private Text Label()
    {
        if (label == null)
        {
            // the row's text sits inside its (read-only) InputField, as in the group list rows
            InputField input = GetComponentInChildren<InputField>(true);
            label = input != null ? input.textComponent : GetComponentInChildren<Text>(true);
        }
        return label;
    }
}
