using System;
using System.Collections.Generic;
using Code;
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

    /// <summary>TwinLocalTables key of the Apply button's label.</summary>
    public const string ApplyLabelKey = "UPLOAD_APPLY";

    private readonly List<DocumentReviewRow> rows = new List<DocumentReviewRow>();
    private Action<DocumentMappingSelection> onApply;

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
        ClearRows();
        onApply = apply;
        BuildRows(mapping);
        SetApplyVisible(mapping != null && rows.Count > 0);
        Put(body);
        InteractionController.EnableMode("UploadReview");
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
    private void BuildRows(DocumentMapping mapping)
    {
        if (mapping == null || proposals == null || rowPrefab == null)
        {
            if (mapping != null)
            {
                Debug.LogWarning("[DocumentReviewManager] No row list wired - the proposal is text only.");
            }
            return;
        }

        if (Count(mapping.Paintings) > 0)
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingPaintings, mapping.Paintings.Count));
            for (int i = 0; i < mapping.Paintings.Count; i++)
            {
                Add(DocumentReviewRow.ItemKind.Painting, i, DocumentMappingText.Row(mapping.Paintings[i]));
            }
        }

        if (Count(mapping.NewGroups) > 0)
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingGroups, mapping.NewGroups.Count));
            for (int i = 0; i < mapping.NewGroups.Count; i++)
            {
                Add(DocumentReviewRow.ItemKind.Group, i, DocumentMappingText.Row(mapping.NewGroups[i]));
            }
        }

        if (Count(mapping.ToolAssignments) > 0)
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingTools, mapping.ToolAssignments.Count));
            for (int i = 0; i < mapping.ToolAssignments.Count; i++)
            {
                Add(DocumentReviewRow.ItemKind.Tool, i, DocumentMappingText.Row(mapping.ToolAssignments[i]));
            }
        }

        if (!string.IsNullOrWhiteSpace(mapping.PatientText))
        {
            AddHeading(DocumentMappingText.Heading(DocumentMappingText.HeadingPatientText, 1));
            Add(DocumentReviewRow.ItemKind.PatientText, 0,
                DocumentMappingText.RowForPatientText(mapping.PatientText));
        }
    }

    private void AddHeading(string heading)
    {
        Add(DocumentReviewRow.ItemKind.Heading, 0, heading);
    }

    private void Add(DocumentReviewRow.ItemKind kind, int index, string line)
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

        row.Fill(kind, index, line);
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
