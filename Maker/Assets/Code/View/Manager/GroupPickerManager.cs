using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/*
 * Asks which group a finding should go into. An overlay on the review screen rather than a mode of
 * its own: a picker is a modal question, and this way it needs no entry in
 * InteractionController.interactionModes or UIController.uiPanels - it is simply shown and hidden.
 *
 * The candidates are the twin's own groups plus the groups the document proposed. Picking one only
 * changes the proposal; nothing is written to the twin until Apply, as everywhere else in this
 * feature. See Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md.
 */
public class GroupPickerManager : MonoBehaviour
{
    /// <summary>The overlay itself - hidden until something asks.</summary>
    [SerializeField] private GameObject panel;

    /// <summary>Where the candidate rows go.</summary>
    [SerializeField] private Transform candidates;

    /// <summary>One candidate: a button with a label (<see cref="DocumentReviewRow"/>'s prefab
    /// without its checkbox does the job).</summary>
    [SerializeField] private GameObject rowPrefab;

    /// <summary>Says what is being asked about.</summary>
    [SerializeField] private Text title;

    private readonly List<GameObject> rows = new List<GameObject>();
    private Action<string> onPicked;

    /// <summary>Marks a candidate the twin does not have yet, so choosing it visibly creates a
    /// group rather than reusing one.</summary>
    public const string NewSuffix = " (new)";

    /*
     * No Awake() that hides this: the prefab already ships the overlay inactive, and hiding it here
     * would be worse than redundant. Awake runs on the FIRST activation, so Ask() switching the
     * panel on would immediately run Awake, which would switch it straight back off - the picker
     * silently never appeared.
     */

    /// <summary>
    /// Asks for a group. <paramref name="existing"/> are the twin's groups and
    /// <paramref name="proposed"/> the ones the document suggested; the current choice is shown
    /// first so it is obvious what would change. <paramref name="picked"/> gets the plain group
    /// name - never the "(new)" marker.
    /// </summary>
    public void Ask(string forWhat, string current, IEnumerable<string> existing,
        IEnumerable<string> proposed, Action<string> picked)
    {
        onPicked = picked;
        Clear();

        if (title != null)
        {
            title.text = string.IsNullOrWhiteSpace(forWhat) ? "Which group?" : "Which group for: " + forWhat;
        }

        // the current choice first, then the twin's own groups, then what the document proposed
        var seen = new List<string>();
        if (!string.IsNullOrWhiteSpace(current))
        {
            Add(current.Trim(), IsNew(current, existing), seen);
        }
        foreach (string name in Names(existing))
        {
            Add(name, false, seen);
        }
        foreach (string name in Names(proposed))
        {
            Add(name, IsNew(name, existing), seen);
        }

        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        Clear();
        onPicked = null;
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>The Cancel button of the overlay - nothing is changed.</summary>
    public void HandleCancel()
    {
        Hide();
    }

    /// <summary>Whether the overlay is up - what a test checks.</summary>
    public bool IsAsking()
    {
        return panel != null && panel.activeSelf;
    }

    /// <summary>What is on offer, in order - what a test reads.</summary>
    public List<string> Candidates()
    {
        var texts = new List<string>();
        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            Text label = row.GetComponentInChildren<Text>(true);
            if (label != null) texts.Add(label.text);
        }
        return texts;
    }

    /// <summary>Picks a candidate by the name it shows - how a test taps a row.</summary>
    public bool Pick(string candidate)
    {
        foreach (GameObject row in rows)
        {
            if (row == null) continue;
            Text label = row.GetComponentInChildren<Text>(true);
            if (label == null || label.text != candidate) continue;
            Toggle toggle = row.GetComponentInChildren<Toggle>(true);
            if (toggle == null) return false;
            toggle.isOn = true;
            return true;
        }
        return false;
    }

    private void Add(string name, bool isNew, List<string> seen)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        string plain = name.Trim();
        if (seen.Contains(plain)) return;
        seen.Add(plain);

        if (candidates == null || rowPrefab == null)
        {
            Debug.LogWarning("[GroupPickerManager] No candidate list wired - nothing to pick from.");
            return;
        }

        GameObject instance = Instantiate(rowPrefab, candidates, false);
        instance.transform.localScale = rowPrefab.transform.localScale;

        /*
         * The row prefab already carries a Toggle, and that Toggle is what picks here - no
         * component surgery. Swapping it for a Button does not work: both derive from Selectable
         * and AddComponent<Button> on a GameObject that still has a Toggle returns null (which it
         * did, silently, until a test caught the NullReferenceException). Ticking one candidate is
         * anyway what picking a group is.
         */
        var row = instance.GetComponent<DocumentReviewRow>();
        if (row != null)
        {
            row.Fill(DocumentReviewRow.ItemKind.Group, 0, plain + (isNew ? NewSuffix : ""));
        }

        Toggle toggle = instance.GetComponentInChildren<Toggle>(true);
        if (toggle != null)
        {
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(on => { if (on) Picked(plain); });
        }

        rows.Add(instance);
    }

    private void Picked(string name)
    {
        Action<string> callback = onPicked;
        Hide();
        if (callback != null)
        {
            callback(name);
        }
    }

    private void Clear()
    {
        foreach (GameObject row in rows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }
        rows.Clear();
    }

    private static bool IsNew(string name, IEnumerable<string> existing)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        foreach (string candidate in Names(existing))
        {
            if (string.Equals(candidate, name.Trim(), StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
        }
        return true;
    }

    private static IEnumerable<string> Names(IEnumerable<string> source)
    {
        if (source == null) yield break;
        foreach (string name in source)
        {
            if (!string.IsNullOrWhiteSpace(name)) yield return name.Trim();
        }
    }
}
