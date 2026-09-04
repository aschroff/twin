using System;
using System.Collections.Generic;
using Code.AI.PromptGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace Code
{
    /*
     * Writes the confirmed part of a DocumentMapping to the twin - the only place in this feature
     * that changes anything. Everything before it (pick, prompt, call, review) leaves the twin
     * untouched; see FEATURE_DOCUMENT_TO_TWIN.md in this folder.
     *
     * A plain class on purpose: it takes the mapping, what the user ticked, and the two managers,
     * so a test can drive it without going through the review screen.
     *
     * The order is load bearing. Groups first, then tool meanings, then the paintings - a painted
     * part copies the tool's meaning at the moment it is painted, so a tool that gets a new
     * meaning has to get it before anything is painted with it.
     *
     * One bad item never costs the rest: every proposal is applied on its own and a failure is
     * recorded in DocumentApplyResult.problems instead of aborting the run.
     */
    public static class DocumentMappingApplier
    {
        /// <summary>Label of the Settings row the patient text is appended to. Picked by the
        /// row's visible Label, not by label+level: three rows share Medical Report/Version.</summary>
        public const string ReportRowLabel = "Medical Report";

        public static DocumentApplyResult Apply(DocumentMapping mapping, DocumentMappingSelection selection,
            PartManager partManager, SettingsManager settingsManager)
        {
            return Apply(mapping, selection, partManager, settingsManager, null);
        }

        /// <param name="source">What was read, for the report - the document's name. Without it the
        /// report gains findings with no record of where they came from.</param>
        public static DocumentApplyResult Apply(DocumentMapping mapping, DocumentMappingSelection selection,
            PartManager partManager, SettingsManager settingsManager, string source)
        {
            var result = new DocumentApplyResult();

            if (mapping == null)
            {
                result.problems.Add("There is nothing to apply.");
                return result;
            }
            if (partManager == null)
            {
                result.problems.Add("No PartManager - is a twin open?");
                return result;
            }
            selection = selection ?? DocumentMappingSelection.Nothing();

            // the current group is the user's, not ours: painting moves it, so put it back after
            PartManager.GroupData groupBefore = partManager.currentGroup;

            CreateGroups(mapping, selection, partManager, result);
            ClaimTools(mapping, selection, result);
            Paint(mapping, selection, partManager, result);
            AppendPatientText(mapping, selection, settingsManager, source, result);
            SettleProposedGroups(mapping, partManager, result);

            if (groupBefore != null)
            {
                partManager.currentGroup = groupBefore;
            }

            // the group overlay is built from partManager.groups and also wires GroupData.group,
            // which a group created here does not have yet
            if (result.createdGroups.Count > 0)
            {
                GroupManager overlay = UnityEngine.Object.FindObjectOfType<GroupManager>();
                if (overlay != null)
                {
                    overlay.rebuild();
                }
            }

            return result;
        }

        // ---------------- groups ----------------

        private static void CreateGroups(DocumentMapping mapping, DocumentMappingSelection selection,
            PartManager partManager, DocumentApplyResult result)
        {
            if (mapping.NewGroups == null) return;

            for (int i = 0; i < mapping.NewGroups.Count; i++)
            {
                if (!selection.IsGroupConfirmed(i)) continue;

                ProposedGroup proposed = mapping.NewGroups[i];
                if (proposed == null || string.IsNullOrWhiteSpace(proposed.Name))
                {
                    result.problems.Add("A proposed group has no name and was skipped.");
                    continue;
                }
                CreateGroupIfMissing(proposed.Name, partManager, result);
            }
        }

        /// <summary>The group of that name, creating it when the twin has none. Also the route a
        /// confirmed painting takes: a finding the user ticked must have somewhere to live, so its
        /// group is created even when the group's own row was left unticked - the group toggle
        /// only decides about groups that nothing else needs.</summary>
        private static PartManager.GroupData CreateGroupIfMissing(string name, PartManager partManager,
            DocumentApplyResult result)
        {
            PartManager.GroupData existing = FindGroup(name, partManager);
            if (existing != null)
            {
                return existing;
            }

            PartManager.GroupData group = partManager.StartNewGroup(null);
            group.id = Guid.NewGuid().ToString();
            group.name = name.Trim();
            group.visible = true;
            partManager.currentGroup = group;
            result.createdGroups.Add(group.name);
            Debug.Log("[DocumentMappingApplier] created group '" + group.name + "'");
            return group;
        }

        /*
         * A proposed group that now exists needs nothing further, however it got there - its own row
         * ticked, or a confirmed finding that had to have somewhere to live. Recording it as written
         * is what stops it coming back as a tickable row that would do nothing.
         */
        private static void SettleProposedGroups(DocumentMapping mapping, PartManager partManager,
            DocumentApplyResult result)
        {
            if (mapping.NewGroups == null) return;

            for (int i = 0; i < mapping.NewGroups.Count; i++)
            {
                ProposedGroup proposed = mapping.NewGroups[i];
                if (proposed == null || string.IsNullOrWhiteSpace(proposed.Name)) continue;
                if (FindGroup(proposed.Name, partManager) != null)
                {
                    result.written.SetGroup(i, true);
                }
            }
        }

        private static PartManager.GroupData FindGroup(string name, PartManager partManager)
        {
            if (partManager.groups == null || string.IsNullOrWhiteSpace(name)) return null;
            foreach (PartManager.GroupData group in partManager.groups)
            {
                if (group != null && !string.IsNullOrEmpty(group.name)
                    && string.Equals(group.name.Trim(), name.Trim(), StringComparison.CurrentCultureIgnoreCase))
                {
                    return group;
                }
            }
            return null;
        }

        // ---------------- tool meanings ----------------

        /*
         * A tool that already carries a meaning keeps it, whatever the answer says: the meanings
         * are the user's own vocabulary for this twin, and the first real answer showed the model
         * will happily restate them. Only a tool that is genuinely free can be claimed.
         */
        private static void ClaimTools(DocumentMapping mapping, DocumentMappingSelection selection,
            DocumentApplyResult result)
        {
            if (mapping.ToolAssignments == null) return;

            for (int i = 0; i < mapping.ToolAssignments.Count; i++)
            {
                if (!selection.IsToolConfirmed(i)) continue;

                ProposedToolMeaning proposed = mapping.ToolAssignments[i];
                if (proposed == null || string.IsNullOrWhiteSpace(proposed.ToolName)
                    || string.IsNullOrWhiteSpace(proposed.Meaning))
                {
                    result.problems.Add("A proposed tool meaning was incomplete and was skipped.");
                    continue;
                }

                ToolInfo tool = FindTool(proposed.ToolName);
                if (tool == null)
                {
                    result.problems.Add("'" + proposed.ToolName + "' is not a tool of this app - meaning not set.");
                    continue;
                }
                if (tool.inUse)
                {
                    result.problems.Add("'" + tool.name + "' already means: " + tool.meaning
                        + " - kept, the proposed meaning was not written.");
                    continue;
                }

                InputField field = ToolMeaningField(tool.name);
                if (field == null)
                {
                    result.problems.Add("The row of '" + tool.name + "' has no input field - meaning not set.");
                    continue;
                }

                field.text = proposed.Meaning.Trim();
                result.claimedTools.Add(tool.name);
                result.written.SetTool(i, true);
                Debug.Log("[DocumentMappingApplier] '" + tool.name + "' now means: " + field.text);
            }
        }

        private static ToolInfo FindTool(string toolName)
        {
            foreach (ToolInfo tool in ToolInventory.All())
            {
                if (string.Equals(tool.name, toolName, StringComparison.CurrentCultureIgnoreCase))
                {
                    return tool;
                }
            }
            return null;
        }

        /// <summary>The InputField of the tool's row under the EditMarker / EditFiller panels -
        /// the meaning as the app stores it (Item -> ConfigData.itemTexts), which is also what
        /// ToolInventory reads back.</summary>
        private static InputField ToolMeaningField(string toolName)
        {
            foreach (Tools panel in UnityEngine.Object.FindObjectsOfType<Tools>(true))
            {
                foreach (Transform row in panel.transform)
                {
                    CW.Common.CwDemoButton button = row.GetComponent<CW.Common.CwDemoButton>();
                    if (button == null || button.IsolateTarget == null) continue;
                    if (!string.Equals(button.IsolateTarget.gameObject.name, toolName,
                            StringComparison.CurrentCultureIgnoreCase)) continue;
                    return row.GetComponentInChildren<InputField>(true);
                }
            }
            return null;
        }

        // ---------------- paintings ----------------

        private static void Paint(DocumentMapping mapping, DocumentMappingSelection selection,
            PartManager partManager, DocumentApplyResult result)
        {
            if (mapping.Paintings == null) return;

            for (int i = 0; i < mapping.Paintings.Count; i++)
            {
                if (!selection.IsPaintingConfirmed(i)) continue;

                ProposedPainting painting = mapping.Paintings[i];
                if (painting == null || painting.RegionKeys == null || painting.RegionKeys.Count == 0)
                {
                    result.problems.Add("A finding names no body region and was skipped.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(painting.ToolName))
                {
                    result.problems.Add(Describe(painting) + ": no tool named - skipped.");
                    continue;
                }

                ToolInfo tool = FindTool(painting.ToolName);
                if (tool == null)
                {
                    result.problems.Add(Describe(painting) + ": '" + painting.ToolName
                        + "' is not a tool of this app - skipped.");
                    continue;
                }
                if (!tool.inUse)
                {
                    // paintable, but the part then carries no meaning worth reading
                    result.problems.Add(Describe(painting) + ": '" + tool.name
                        + "' has no meaning - painted anyway.");
                }

                PartManager.GroupData group = string.IsNullOrWhiteSpace(painting.Group)
                    ? partManager.currentGroup
                    : (FindGroup(painting.Group, partManager)
                       ?? CreateGroupIfMissing(painting.Group, partManager, result));
                if (group == null)
                {
                    result.problems.Add(Describe(painting) + ": no group to put it in - skipped.");
                    continue;
                }
                partManager.currentGroup = group;

                int painted = 0;
                foreach (string regionKey in Distinct(painting.RegionKeys))
                {
                    try
                    {
                        List<PartManager.PartData> parts =
                            PartTemplateService.PaintRegionByKey(regionKey, tool.name, partManager);
                        foreach (PartManager.PartData part in parts)
                        {
                            // the finding, not the region name: this is what the group detail page
                            // shows and what the version report is built from. The region stays
                            // readable through part.regionKey.
                            if (!string.IsNullOrWhiteSpace(painting.Description))
                            {
                                part.description = painting.Description.Trim();
                            }
                            result.paintedParts++;
                            painted++;
                        }
                    }
                    catch (Exception e)
                    {
                        result.problems.Add(Describe(painting) + ": region '" + regionKey
                            + "' could not be painted - " + e.Message);
                    }
                }

                if (painted > 0)
                {
                    result.paintedFindings++;
                    // partly painted counts as applied: retrying would duplicate the regions that
                    // did work. Which region failed is in problems.
                    result.written.SetPainting(i, true);
                }
            }
        }

        private static string Describe(ProposedPainting painting)
        {
            return string.IsNullOrWhiteSpace(painting.FindingText) ? "a finding" : "'" + painting.FindingText.Trim() + "'";
        }

        private static List<string> Distinct(List<string> keys)
        {
            var seen = new List<string>();
            foreach (string key in keys)
            {
                if (!string.IsNullOrWhiteSpace(key) && !seen.Contains(key))
                {
                    seen.Add(key);
                }
            }
            return seen;
        }

        // ---------------- the patient text ----------------

        /*
         * Appended, never replacing: the twin's report may already say something, and a document
         * adds to it. The row is found by its visible Label because three rows carry label
         * "Medical Report" on level Version - two of them leftovers on the Meshcapade settings
         * objects, whose own Labels read differently.
         */
        private static void AppendPatientText(DocumentMapping mapping, DocumentMappingSelection selection,
            SettingsManager settingsManager, string source, DocumentApplyResult result)
        {
            if (!selection.PatientTextConfirmed || string.IsNullOrWhiteSpace(mapping.PatientText))
            {
                return;
            }
            if (settingsManager == null)
            {
                result.problems.Add("No SettingsManager - the patient text was not written.");
                return;
            }

            ItemPrompt row = settingsManager.getPromptObjectByLabelText(ReportRowLabel, ItemPrompt.PromptLevel.Version);
            if (row == null)
            {
                result.problems.Add("No '" + ReportRowLabel + "' row - the patient text was not written.");
                return;
            }

            // the document's name and what it was travel with the text: the review screen shows them
            // and then closes, and this is the only place they are kept
            string provenance = DocumentMappingText.Provenance(source, mapping);
            string addition = string.IsNullOrWhiteSpace(provenance)
                ? mapping.PatientText.Trim()
                : provenance + "\n" + mapping.PatientText.Trim();

            string existing = row.promptResult ?? "";
            row.promptResult = string.IsNullOrWhiteSpace(existing)
                ? addition
                : existing.TrimEnd() + "\n\n" + addition;
            result.patientTextAppended = true;
            result.written.PatientTextConfirmed = true;
        }
    }

    /// <summary>What the user ticked on the review screen, by position in the mapping's lists.
    /// Everything starts unticked - nothing reaches the twin that was not chosen.</summary>
    public class DocumentMappingSelection
    {
        private readonly HashSet<int> groups = new HashSet<int>();
        private readonly HashSet<int> tools = new HashSet<int>();
        private readonly HashSet<int> paintings = new HashSet<int>();

        public bool PatientTextConfirmed { get; set; }

        public static DocumentMappingSelection Nothing()
        {
            return new DocumentMappingSelection();
        }

        /// <summary>Everything the mapping proposes - what a test uses, and what a
        /// "select all" button on the review screen produces.</summary>
        public static DocumentMappingSelection Everything(DocumentMapping mapping)
        {
            var selection = new DocumentMappingSelection { PatientTextConfirmed = true };
            if (mapping == null) return selection;
            for (int i = 0; mapping.NewGroups != null && i < mapping.NewGroups.Count; i++) selection.SetGroup(i, true);
            for (int i = 0; mapping.ToolAssignments != null && i < mapping.ToolAssignments.Count; i++) selection.SetTool(i, true);
            for (int i = 0; mapping.Paintings != null && i < mapping.Paintings.Count; i++) selection.SetPainting(i, true);
            return selection;
        }

        public void SetGroup(int index, bool confirmed) { Set(groups, index, confirmed); }
        public void SetTool(int index, bool confirmed) { Set(tools, index, confirmed); }
        public void SetPainting(int index, bool confirmed) { Set(paintings, index, confirmed); }

        public bool IsGroupConfirmed(int index) { return groups.Contains(index); }
        public bool IsToolConfirmed(int index) { return tools.Contains(index); }
        public bool IsPaintingConfirmed(int index) { return paintings.Contains(index); }

        /// <summary>How many items are ticked - what the Apply button counts.</summary>
        public int Count
        {
            get { return groups.Count + tools.Count + paintings.Count + (PatientTextConfirmed ? 1 : 0); }
        }

        /// <summary>Takes everything <paramref name="other"/> holds into this one. Used to keep a
        /// running record of what has already been written to the twin across several Applies.</summary>
        public void Merge(DocumentMappingSelection other)
        {
            if (other == null) return;
            groups.UnionWith(other.groups);
            tools.UnionWith(other.tools);
            paintings.UnionWith(other.paintings);
            PatientTextConfirmed |= other.PatientTextConfirmed;
        }

        /// <summary>This selection minus everything <paramref name="other"/> holds - what is left
        /// to do when part of the mapping has already been applied. Nothing is written twice, so a
        /// second Apply cannot paint the same region again.</summary>
        public DocumentMappingSelection Without(DocumentMappingSelection other)
        {
            var rest = new DocumentMappingSelection { PatientTextConfirmed = PatientTextConfirmed };
            rest.groups.UnionWith(groups);
            rest.tools.UnionWith(tools);
            rest.paintings.UnionWith(paintings);
            if (other != null)
            {
                rest.groups.ExceptWith(other.groups);
                rest.tools.ExceptWith(other.tools);
                rest.paintings.ExceptWith(other.paintings);
                if (other.PatientTextConfirmed) rest.PatientTextConfirmed = false;
            }
            return rest;
        }

        private static void Set(HashSet<int> set, int index, bool confirmed)
        {
            if (confirmed) set.Add(index); else set.Remove(index);
        }
    }

    /// <summary>What applying did - the material for the toast and for a test.</summary>
    public class DocumentApplyResult
    {
        public List<string> createdGroups = new List<string>();
        public List<string> claimedTools = new List<string>();

        /// <summary>Findings that reached the body, and the parts they became - a finding over
        /// both legs is one finding and many parts.</summary>
        public int paintedFindings;

        public int paintedParts;
        public bool patientTextAppended;

        /// <summary>Everything that was refused or went wrong, in the user's words.</summary>
        public List<string> problems = new List<string>();

        /// <summary>Exactly what reached the twin, by position in the mapping's lists - and the only
        /// thing that may be remembered as applied. A ticked item that was refused is NOT in here,
        /// so it stays offerable instead of being locked as if it were on the body.</summary>
        public DocumentMappingSelection written = new DocumentMappingSelection();

        public bool ChangedAnything
        {
            get
            {
                return createdGroups.Count > 0 || claimedTools.Count > 0 || paintedParts > 0
                    || patientTextAppended;
            }
        }

        /// <summary>One line for the toast.</summary>
        public string Summary()
        {
            var parts = new List<string>();
            if (paintedFindings > 0) parts.Add(paintedFindings + " findings on the body (" + paintedParts + " parts)");
            if (createdGroups.Count > 0) parts.Add(createdGroups.Count + " new groups");
            if (claimedTools.Count > 0) parts.Add(claimedTools.Count + " tools given a meaning");
            if (patientTextAppended) parts.Add("the patient text added to the report");
            if (parts.Count == 0) parts.Add("nothing was applied");

            string summary = string.Join(", ", parts.ToArray());
            if (problems.Count > 0)
            {
                summary += " - " + problems.Count + (problems.Count == 1 ? " problem" : " problems");
            }
            return summary;
        }
    }
}
