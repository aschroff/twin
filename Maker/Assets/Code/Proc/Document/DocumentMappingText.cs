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
