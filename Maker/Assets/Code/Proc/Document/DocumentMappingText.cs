using System.Text;

namespace Code
{
    /*
     * The proposal as readable text. A stand-in for the review list of step 4, where every item
     * gets its own row with a toggle - but enough to judge whether what comes back is any good.
     */
    public static class DocumentMappingText
    {
        public static string Describe(DocumentMapping mapping)
        {
            if (mapping == null)
            {
                return "The answer could not be read.";
            }

            var text = new StringBuilder();
            text.AppendLine("THE DOCUMENT");
            text.AppendLine(Or(mapping.DocumentSummary, "- no summary -"));
            text.AppendLine();

            text.AppendLine("FINDINGS ON THE BODY (" + Count(mapping.Paintings) + ")");
            if (Count(mapping.Paintings) == 0)
            {
                text.AppendLine("- none");
            }
            else
            {
                int number = 0;
                foreach (ProposedPainting painting in mapping.Paintings)
                {
                    number++;
                    text.AppendLine(number + ". " + Or(painting.FindingText, "- no text -"));
                    text.AppendLine("   group:   " + Or(painting.Group, "-"));
                    text.AppendLine("   tool:    " + Or(painting.ToolName, "-"));
                    text.AppendLine("   regions: " + (Count(painting.RegionKeys) > 0
                        ? string.Join(", ", painting.RegionKeys.ToArray())
                        : "-"));
                    text.AppendLine("   says:    " + Or(painting.Description, "-"));
                    text.AppendLine("   sure:    " + painting.Confidence.ToString("0.00"));
                }
            }
            text.AppendLine();

            text.AppendLine("NEW GROUPS (" + Count(mapping.NewGroups) + ")");
            if (Count(mapping.NewGroups) == 0)
            {
                text.AppendLine("- none, the existing ones were enough");
            }
            else
            {
                foreach (ProposedGroup group in mapping.NewGroups)
                {
                    text.AppendLine("- " + Or(group.Name, "-") + " (" + Or(group.Reason, "no reason given") + ")");
                }
            }
            text.AppendLine();

            text.AppendLine("TOOLS TO TAKE INTO USE (" + Count(mapping.ToolAssignments) + ")");
            if (Count(mapping.ToolAssignments) == 0)
            {
                text.AppendLine("- none, the tools in use were enough");
            }
            else
            {
                foreach (ProposedToolMeaning tool in mapping.ToolAssignments)
                {
                    text.AppendLine("- " + Or(tool.ToolName, "-") + " would mean: " + Or(tool.Meaning, "-")
                        + " (" + Or(tool.Reason, "no reason given") + ")");
                }
            }
            text.AppendLine();

            text.AppendLine("ABOUT THE PATIENT, NOT ABOUT ONE PLACE ON THE BODY");
            text.AppendLine(Or(mapping.PatientText, "- nothing -"));

            return text.ToString();
        }

        // ---------------- one line per proposal, for the review rows ----------------

        /*
         * The rows are for deciding, the text above is for reading: a row says what the finding is,
         * which tool and group it would use, and HOW MANY REGIONS it covers - the region count is
         * the cost of ticking it. A treatment line over both legs is one row and fourteen parts,
         * and the row is where that becomes visible before it is paid.
         */
        public const string HeadingPaintings = "FINDINGS ON THE BODY";
        public const string HeadingGroups = "NEW GROUPS";
        public const string HeadingTools = "TOOLS TO TAKE INTO USE";
        public const string HeadingPatientText = "FOR THE REPORT";

        public static string Heading(string heading, int count)
        {
            return heading + " (" + count + ")";
        }

        public static string Row(ProposedPainting painting)
        {
            return Row(painting, null);
        }

        /// <param name="newGroupNames">Groups the twin does not have yet. A finding in one of them
        /// would create it, which is worth seeing on the row rather than after the fact.</param>
        public static string Row(ProposedPainting painting, System.Collections.Generic.ICollection<string> newGroupNames)
        {
            if (painting == null) return "-";

            int regions = Count(painting.RegionKeys);
            string row = Or(painting.FindingText, "a finding")
                + "  -  " + Or(painting.ToolName, "no tool")
                + ", " + regions + (regions == 1 ? " region" : " regions");

            // below this the model itself was unsure - worth seeing without opening anything
            if (painting.Confidence > 0f && painting.Confidence < 0.6f)
            {
                row += ", uncertain";
            }
            return row;
        }

        /// <summary>The group of a finding, as its chip reads. Marked when the twin does not have
        /// that group yet, because ticking the finding would then create it - which is the one
        /// consequence of a tick that is not obvious from the finding itself.</summary>
        public static string GroupChip(ProposedPainting painting,
            System.Collections.Generic.ICollection<string> newGroupNames)
        {
            if (painting == null) return "-";

            string group = Or(painting.Group, "no group");
            return newGroupNames != null && newGroupNames.Contains(group) ? group + " (new)" : group;
        }

        public static string Row(ProposedGroup group)
        {
            if (group == null) return "-";
            return Or(group.Name, "a group")
                + (string.IsNullOrWhiteSpace(group.Reason) ? "" : "  -  " + group.Reason.Trim());
        }

        public static string Row(ProposedToolMeaning tool)
        {
            if (tool == null) return "-";
            return Or(tool.ToolName, "a tool") + " would mean: " + Or(tool.Meaning, "-");
        }

        /// <summary>The patient text as one row - shortened, the whole of it is in the text below.</summary>
        public static string RowForPatientText(string patientText)
        {
            string text = Or(patientText, "- nothing -");
            return text.Length > 90 ? text.Substring(0, 90).TrimEnd() + " ..." : text;
        }

        private static string Or(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static int Count<T>(System.Collections.Generic.List<T> list)
        {
            return list != null ? list.Count : 0;
        }
    }
}
