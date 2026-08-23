using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One proposal on the upload review screen: its text and a checkbox. What the checkbox stands
/// for is <see cref="kind"/> plus <see cref="index"/> — the position in the matching list of the
/// DocumentMapping — so reading the rows back gives a DocumentMappingSelection and the applier
/// never has to be told anything twice.
///
/// The row starts unticked, always: nothing reaches the twin that the user did not tick.
///
/// The text wraps and the row grows with it, so a long finding is readable instead of cut off:
/// the row's layout group takes its height from the text. The whole row is the tap target — the
/// Toggle sits on the row itself, not on the box, because a 30-unit box next to a three-line row
/// is nothing to aim at on a phone.
///
/// See Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md.
/// </summary>
public class DocumentReviewRow : MonoBehaviour
{
    public enum ItemKind
    {
        /// <summary>A heading, no checkbox — it groups the rows below it.</summary>
        Heading,
        Painting,
        Group,
        Tool,
        PatientText
    }

    public ItemKind kind;

    /// <summary>Position in the mapping's list for this kind; unused for Heading and PatientText.</summary>
    public int index;

    /// <summary>Name of the child holding the box and the tick — hidden on a heading.</summary>
    private const string SelectorName = "Selector";

    private Toggle toggle;
    private Text label;
    private Transform selector;

    public bool Confirmed
    {
        get { return kind != ItemKind.Heading && Toggle() != null && Toggle().isOn; }
        set { if (Toggle() != null) Toggle().isOn = value; }
    }

    /// <summary>Fills the row and puts its checkbox in the unticked start state. A heading has
    /// nothing to tick, so it loses its box and stops reacting to a tap.</summary>
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
            // the Toggle is on the row itself, so it may not be switched off by hiding the object
            box.enabled = kind != ItemKind.Heading;
            box.interactable = kind != ItemKind.Heading;
        }

        Transform marks = Selector();
        if (marks != null)
        {
            marks.gameObject.SetActive(kind != ItemKind.Heading);
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

    private Transform Selector()
    {
        if (selector == null)
        {
            selector = transform.Find(SelectorName);
        }
        return selector;
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
