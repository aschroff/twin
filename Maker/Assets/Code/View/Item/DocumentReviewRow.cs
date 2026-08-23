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

    /// <summary>The child that holds the group chip. It is a layout child, so hiding it also takes
    /// its height out of the row - a row without a group must not reserve space for one.</summary>
    private const string GroupHolderName = "Group";

    private Toggle toggle;
    private Text label;
    private Transform selector;
    private Transform groupHolder;
    private Button groupButton;
    private Text groupLabel;

    public bool Confirmed
    {
        get { return kind != ItemKind.Heading && Toggle() != null && Toggle().isOn; }
        set { if (Toggle() != null) Toggle().isOn = value; }
    }

    /// <summary>Fills the row and puts its checkbox in the unticked start state. A heading has
    /// nothing to tick, so it loses its box and stops reacting to a tap.</summary>
    public void Fill(ItemKind itemKind, int itemIndex, string text)
    {
        Fill(itemKind, itemIndex, text, null, null);
    }

    /// <param name="group">What the group chip reads, or null when this row has no group to change
    /// (a heading, a proposed group, a tool, the report text).</param>
    /// <param name="changeGroup">Called when the chip is tapped - the row itself knows nothing
    /// about groups, only that something wants to be asked.</param>
    public void Fill(ItemKind itemKind, int itemIndex, string text, string group,
        UnityEngine.Events.UnityAction changeGroup)
    {
        kind = itemKind;
        index = itemIndex;

        Text field = Label();
        if (field != null)
        {
            field.text = text;
        }

        FillGroup(group, changeGroup);

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

    /*
     * The chip is a Button inside the row, which is itself a Toggle. That nesting is deliberate:
     * a Button consumes the click, so tapping the chip opens the picker and does NOT tick the row,
     * while a tap anywhere else on the row still ticks it.
     */
    private void FillGroup(string group, UnityEngine.Events.UnityAction changeGroup)
    {
        Transform holder = GroupHolder();
        if (holder == null)
        {
            return;
        }

        bool hasGroup = !string.IsNullOrWhiteSpace(group);
        holder.gameObject.SetActive(hasGroup);

        Button button = GroupButton();
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
        }
        if (!hasGroup || button == null)
        {
            return;
        }

        Text text = GroupLabel();
        if (text != null)
        {
            text.text = group;
        }
        if (changeGroup != null)
        {
            button.onClick.AddListener(changeGroup);
        }
        button.interactable = changeGroup != null;
    }

    /// <summary>What the group chip reads - what a test checks, and empty when there is none.</summary>
    public string GroupText()
    {
        Transform holder = GroupHolder();
        Text text = GroupLabel();
        return holder != null && holder.gameObject.activeSelf && text != null ? text.text : "";
    }

    /// <summary>The chip itself - a test taps this to open the picker.</summary>
    public Button GroupButton()
    {
        if (groupButton == null)
        {
            Transform holder = GroupHolder();
            groupButton = holder != null ? holder.GetComponentInChildren<Button>(true) : null;
        }
        return groupButton;
    }

    private Transform GroupHolder()
    {
        if (groupHolder == null)
        {
            groupHolder = transform.Find(GroupHolderName);
        }
        return groupHolder;
    }

    private Text GroupLabel()
    {
        if (groupLabel == null)
        {
            Button button = GroupButton();
            groupLabel = button != null ? button.GetComponentInChildren<Text>(true) : null;
        }
        return groupLabel;
    }

    /// <summary>Whether this row has already been written to the twin.</summary>
    public bool Applied { get; private set; }

    /// <summary>An item that is already on the twin: shown ticked, dimmed, and not tickable. There
    /// is no undo, so it may not be offered again - and it may not silently be applied twice.</summary>
    public void SetApplied(bool alreadyOnTheTwin)
    {
        Applied = alreadyOnTheTwin;
        if (!alreadyOnTheTwin)
        {
            return;
        }

        Toggle box = Toggle();
        if (box != null)
        {
            box.isOn = true;
            box.interactable = false;
        }

        Button chip = GroupButton();
        if (chip != null)
        {
            // the part is painted into that group already - changing the chip would be a lie
            chip.interactable = false;
        }

        var group = GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 0.45f;
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
