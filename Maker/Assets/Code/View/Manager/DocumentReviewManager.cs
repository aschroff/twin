using System;
using System.Collections.Generic;
using Code;
using Code.AI.PromptGeneration;
using UnityEngine;
using UnityEngine.UI;

/*
 * The screen that shows what an uploaded document turned into, and the only place the user can
 * let any of it reach the twin.
 *
 * Two things are on it. A list of rows, one per proposal, each with a toggle - all unticked, so
 * Apply does nothing until something is ticked. And below it the whole proposal as text, which is
 * where the detail lives: the regions of a finding, the reasons, the patient text in full. The
 * rows are for deciding, the text is for reading.
 *
 * Without a mapping - while the analysis runs, when it fails, or when only the prompt is being
 * shown - the list and the Apply button are hidden and the screen is text only.
 *
 * See Assets/Code/Proc/Document/FEATURE_DOCUMENT_TO_TWIN.md.
 */
public class DocumentReviewManager : MonoBehaviour
{
    [SerializeField] private Text text;

    /// <summary>Where the proposal rows go - one child per row.</summary>
    [SerializeField] private Transform proposals;

    /// <summary>The row: a toggle and a line of text (<see cref="DocumentReviewRow"/>).</summary>
    [SerializeField] private GameObject rowPrefab;

    /// <summary>Hidden while there is nothing to apply.</summary>
    [SerializeField] private GameObject applyButton;

    /// <summary>Asks which group a finding belongs in. The model's choice is a recommendation, not
    /// a decision - and a group cannot be changed once its part is painted, so this is the only
    /// chance to correct it.</summary>
    [SerializeField] private GroupPickerManager groupPicker;

    /// <summary>TwinLocalTables key of the Apply button's label.</summary>
    public const string ApplyLabelKey = "UPLOAD_APPLY";

    private readonly List<DocumentReviewRow> rows = new List<DocumentReviewRow>();
    private Action<DocumentMappingSelection> onApply;

    /// <summary>The proposal on the screen - the group chips write their choice back into it.</summary>
    private DocumentMapping shown;

    private DocumentMappingSelection shownApplied;

    /// <summary>Guards <see cref="SyncDependencies"/> against re-entering itself through the very
    /// toggles it sets.</summary>
    private bool syncing;

    /// <summary>What the caller said about the pick. The proposal text underneath is generated from
    /// the mapping every time the screen is built, so changing a group cannot leave a stale copy of
    /// it on screen saying the old one.</summary>
    private string shownHeader;

    /// <summary>Text only: the prompt, a progress line, an error. No rows, no Apply.</summary>
    public void Show(string body)
    {
        ClearRows();
        onApply = null;
        SetApplyVisible(false);
        Put(body);
        InteractionController.EnableMode("UploadReview");
    }

    /// <summary>The proposal: a row per item, all unticked, and Apply enabled. What Apply does is
    /// the caller's business (<see cref="Code.DocumentUploadProcess"/>) - this screen only collects
    /// what the user ticked.</summary>
    public void Show(DocumentMapping mapping, string body, Action<DocumentMappingSelection> apply)
    {
        Show(mapping, body, apply, null);
    }

    /// <param name="applied">What is already on the twin - those rows come back ticked and locked.
    /// There is no undo, so an applied item may neither be offered again nor applied twice.</param>
    public void Show(DocumentMapping mapping, string body, Action<DocumentMappingSelection> apply,
        DocumentMappingSelection applied)
    {
        /*
         * Showing the SAME proposal again is a rebuild, not a fresh start: changing a group and
         * applying part of the list both rebuild every row, and a tick the user set must not die
         * with the row that carried it. A different mapping starts from nothing ticked, as it must.
         *
         * Read before ClearRows, because that is what destroys the rows the ticks live on.
         */
        DocumentMappingSelection ticked = ReferenceEquals(mapping, shown) ? Selection() : null;

        ClearRows();
        onApply = apply;
        shown = mapping;
        shownApplied = applied;
        shownHeader = body;
        if (groupPicker != null)
        {
            groupPicker.Hide();
        }
        BuildRows(mapping, applied);
        Restore(ticked);
        SyncDependencies();
        SetApplyVisible(mapping != null && rows.Count > 0);
        Put(body + "\n\n" + DocumentMappingText.Describe(mapping));
        InteractionController.EnableMode("UploadReview");
    }

    /// <summary>Puts the ticks back on the rebuilt rows. An applied row is skipped: it is ticked and
    /// locked already, and its state comes from the twin rather than from the user.</summary>
    /*
     * A ticked finding needs two other things, and neither is obvious from the finding's own row:
     * the group it goes into has to exist, and the tool it is painted with has to mean something.
     * The applier creates a missing group either way - a confirmed finding must live somewhere - but
     * it claims a tool meaning only when that row is ticked. Left alone, that asymmetry paints a
     * part in a colour that means nothing, and the version report is built from those meanings.
     *
     * So the dependencies are ticked with the finding, on the screen, where they can be seen and
     * argued with - rather than the applier quietly writing things nobody confirmed. A group or tool
     * row that no ticked finding needs stays entirely the user's own choice.
     */
    private void SyncDependencies()
    {
        if (syncing || shown == null || shown.Paintings == null)
        {
            return;
        }

        syncing = true;
        try
        {
            // first: which groups and tools the findings that are ticked right now depend on
            var neededGroups = new List<int>();
            var neededTools = new List<int>();
            foreach (DocumentReviewRow row in rows)
            {
                if (row.kind != DocumentReviewRow.ItemKind.Painting || !row.Confirmed) continue;
                if (row.index >= shown.Paintings.Count) continue;

                ProposedPainting painting = shown.Paintings[row.index];
                if (painting == null) continue;

                Need(neededGroups, GroupIndex(painting.Group));
                Need(neededTools, FreeToolIndex(painting.ToolName));
            }

            // then: a needed row is ticked and locked, and one nothing needs is the user's again
            foreach (DocumentReviewRow row in rows)
            {
                if (row.kind == DocumentReviewRow.ItemKind.Group)
                {
                    row.SetRequired(neededGroups.Contains(row.index));
                }
                else if (row.kind == DocumentReviewRow.ItemKind.Tool)
                {
                    row.SetRequired(neededTools.Contains(row.index));
                }
            }
        }
        finally
        {
            syncing = false;
        }
    }

    private static void Need(List<int> needed, int index)
    {
        if (index >= 0 && !needed.Contains(index))
        {
            needed.Add(index);
        }
    }

    /// <summary>The proposed group of that name, or -1 when the twin already has it (then there is
    /// nothing to create and no row to tick).</summary>
    private int GroupIndex(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        for (int i = 0; i < Count(shown.NewGroups); i++)
        {
            ProposedGroup group = shown.NewGroups[i];
            if (group != null && string.Equals(group.Name?.Trim(), name.Trim(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>The proposed meaning for that tool, but only while the tool is still free. A tool
    /// that already means something keeps its meaning, so ticking that row would only be refused.
    /// </summary>
    private int FreeToolIndex(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return -1;
        for (int i = 0; i < Count(shown.ToolAssignments); i++)
        {
            ProposedToolMeaning proposed = shown.ToolAssignments[i];
            if (proposed == null || !string.Equals(proposed.ToolName?.Trim(), toolName.Trim(),
                    StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            foreach (ToolInfo tool in ToolInventory.All())
            {
                if (string.Equals(tool.name, toolName.Trim(), StringComparison.CurrentCultureIgnoreCase))
                {
                    return tool.inUse ? -1 : i;
                }
            }
            return -1;
        }
        return -1;
    }

    private void Restore(DocumentMappingSelection ticked)
    {
        if (ticked == null)
        {
            return;
        }

        syncing = true;   // one sync afterwards, on the finished state, not once per row
        foreach (DocumentReviewRow row in rows)
        {
            if (row.Applied)
            {
                continue;
            }
            switch (row.kind)
            {
                case DocumentReviewRow.ItemKind.Painting:
                    row.Confirmed = ticked.IsPaintingConfirmed(row.index);
                    break;
                case DocumentReviewRow.ItemKind.Group:
                    row.Confirmed = ticked.IsGroupConfirmed(row.index);
                    break;
                case DocumentReviewRow.ItemKind.Tool:
                    row.Confirmed = ticked.IsToolConfirmed(row.index);
                    break;
                case DocumentReviewRow.ItemKind.PatientText:
                    row.Confirmed = ticked.PatientTextConfirmed;
                    break;
            }
        }
        syncing = false;
    }

    /// <summary>What the user has ticked, ready for the applier.</summary>
    public DocumentMappingSelection Selection()
    {
        var selection = new DocumentMappingSelection();
        foreach (DocumentReviewRow row in rows)
        {
            if (!row.Confirmed) continue;
            switch (row.kind)
            {
                case DocumentReviewRow.ItemKind.Painting: selection.SetPainting(row.index, true); break;
                case DocumentReviewRow.ItemKind.Group: selection.SetGroup(row.index, true); break;
                case DocumentReviewRow.ItemKind.Tool: selection.SetTool(row.index, true); break;
                case DocumentReviewRow.ItemKind.PatientText: selection.PatientTextConfirmed = true; break;
            }
        }
        return selection;
    }

    /// <summary>The Apply button. Wired on the button in the prefab.</summary>
    public void HandleApply()
    {
        if (onApply == null)
        {
            Debug.LogWarning("[DocumentReviewManager] Apply with nothing to apply to.");
            return;
        }
        onApply(Selection());
    }

    /// <summary>Ticks or unticks every row at once.</summary>
    public void SetAllConfirmed(bool confirmed)
    {
        foreach (DocumentReviewRow row in rows)
        {
            row.Confirmed = confirmed;
        }
    }

    /// <summary>The rows as they stand - what a test reads.</summary>
    public List<DocumentReviewRow> Rows()
    {
        return new List<DocumentReviewRow>(rows);
    }

    /// <summary>What the screen shows at the moment.</summary>
    public string GetShownText()
    {
        return text != null ? text.text : "";
    }

    // ---------------- the list ----------------

    /*
     * The order is the order of the work: what goes onto the body first, because that is what the
     * user is really deciding about, then the groups and tools that go with it, then the text for
     * the report. A heading precedes each block and carries no toggle.
     */
    private void BuildRows(DocumentMapping mapping, DocumentMappingSelection applied)
    {
        if (mapping == null || proposals == null || rowPrefab == null)
        {
            if (mapping != null)
            {
                Debug.LogWarning("[DocumentReviewManager] No row list wired - the proposal is text only.");
            }
            return;
        }

        // which groups the twin does not have yet, so a finding that would create one says so
        var newGroups = new List<string>();
        for (int i = 0; i < Count(mapping.NewGroups); i++)
        {
            if (mapping.NewGroups[i] != null && !string.IsNullOrWhiteSpace(mapping.NewGroups[i].Name))
            {
                newGroups.Add(mapping.NewGroups[i].Name.Trim());
            }
        }

        if (Count(mapping.Paintings) > 0)
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingPaintings, mapping.Paintings.Count));
            for (int i = 0; i < mapping.Paintings.Count; i++)
            {
                int painting = i;   // captured for the chip's and the toggle's callbacks
                Add(DocumentReviewRow.ItemKind.Painting, i,
                    DocumentMappingText.Row(mapping.Paintings[i], newGroups),
                    applied != null && applied.IsPaintingConfirmed(i),
                    DocumentMappingText.GroupChip(mapping.Paintings[i], newGroups),
                    () => AskForGroup(painting),
                    on => SyncDependencies());
            }
        }

        if (Count(mapping.NewGroups) > 0)
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingGroups, mapping.NewGroups.Count));
            for (int i = 0; i < mapping.NewGroups.Count; i++)
            {
                Add(DocumentReviewRow.ItemKind.Group, i, DocumentMappingText.Row(mapping.NewGroups[i]),
                    applied != null && applied.IsGroupConfirmed(i));
            }
        }

        if (Count(mapping.ToolAssignments) > 0)
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingTools, mapping.ToolAssignments.Count));
            for (int i = 0; i < mapping.ToolAssignments.Count; i++)
            {
                Add(DocumentReviewRow.ItemKind.Tool, i, DocumentMappingText.Row(mapping.ToolAssignments[i]),
                    applied != null && applied.IsToolConfirmed(i));
            }
        }

        if (!string.IsNullOrWhiteSpace(mapping.PatientText))
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingPatientText, 1));
            Add(DocumentReviewRow.ItemKind.PatientText, 0,
                DocumentMappingText.RowForPatientText(mapping.PatientText),
                applied != null && applied.PatientTextConfirmed);
        }
    }

    /*
     * The chip was tapped: offer the twin's groups and the ones the document proposed, and write the
     * answer back into the proposal. Only the proposal changes - the twin is untouched until Apply.
     */
    private void AskForGroup(int painting)
    {
        if (shown == null || shown.Paintings == null || painting >= shown.Paintings.Count) return;
        ProposedPainting proposal = shown.Paintings[painting];
        if (proposal == null) return;

        if (groupPicker == null)
        {
            Debug.LogWarning("[DocumentReviewManager] No group picker wired - the group cannot be changed.");
            return;
        }

        var existing = new List<string>();
        PartManager partManager = FindObjectOfType<PartManager>();
        if (partManager != null && partManager.groups != null)
        {
            foreach (PartManager.GroupData group in partManager.groups)
            {
                if (group != null && !string.IsNullOrWhiteSpace(group.name)) existing.Add(group.name);
            }
        }

        var proposed = new List<string>();
        for (int i = 0; i < Count(shown.NewGroups); i++)
        {
            if (shown.NewGroups[i] != null) proposed.Add(shown.NewGroups[i].Name);
        }

        groupPicker.Ask(proposal.FindingText, proposal.Group, existing, proposed, chosen =>
        {
            proposal.Group = chosen;
            // rebuild, so the chips, the "(new)" marks and the text below all agree again
            Show(shown, shownHeader, onApply, shownApplied);
        });
    }

    private void AddHeading(string heading)
    {
        Add(DocumentReviewRow.ItemKind.Heading, 0, heading, false, null, null, null);
    }

    private void Add(DocumentReviewRow.ItemKind kind, int index, string line, bool applied)
    {
        Add(kind, index, line, applied, null, null, null);
    }

    private void Add(DocumentReviewRow.ItemKind kind, int index, string line, bool applied,
        string group, UnityEngine.Events.UnityAction changeGroup,
        UnityEngine.Events.UnityAction<bool> confirmedChanged)
    {
        GameObject instance = Instantiate(rowPrefab, proposals, false);
        instance.transform.localScale = rowPrefab.transform.localScale;

        DocumentReviewRow row = instance.GetComponent<DocumentReviewRow>();
        if (row == null)
        {
            Debug.LogError("[DocumentReviewManager] The row prefab carries no DocumentReviewRow.");
            Destroy(instance);
            return;
        }

        row.Fill(kind, index, line, group, changeGroup);
        row.SetApplied(applied);
        row.WhenConfirmedChanges(confirmedChanged);
        rows.Add(row);
    }

    private void ClearRows()
    {
        foreach (DocumentReviewRow row in rows)
        {
            if (row != null)
            {
                Destroy(row.gameObject);
            }
        }
        rows.Clear();
    }

    private void SetApplyVisible(bool visible)
    {
        if (applyButton == null)
        {
            return;
        }

        applyButton.SetActive(visible);
        if (visible)
        {
            // in the language of the app, like every other label; done here rather than in the
            // prefab because the prefab has no localized-text component
            foreach (Text label in applyButton.GetComponentsInChildren<Text>(true))
            {
                label.text = StringLocalizer.localizeString(ApplyLabelKey);
            }
        }
    }

    private void Put(string body)
    {
        if (text == null)
        {
            Debug.LogError("[DocumentReviewManager] No text field wired - nothing to show.");
            return;
        }
        text.text = body;
    }

    private static int Count<T>(List<T> list)
    {
        return list != null ? list.Count : 0;
    }
}
