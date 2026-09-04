using System.Collections.Generic;
using Newtonsoft.Json;

namespace Code
{
    /*
     * What the LLM answers when it is asked to map the findings of a document onto the twin -
     * the target structure of FEATURE_DOCUMENT_TO_TWIN.md.
     *
     * Nothing here is applied to the twin on its own: the user confirms it on the review screen
     * first. Names are the ones the app acts on, so they have to come back exactly as they went
     * out: a tool is named by its tool GameObject ("Cyan", "Red Filling"), a group by its name,
     * and a body region by its key. The area twin a region key belongs to is resolved by the app,
     * so the answer cannot get that pairing wrong.
     */
    public class DocumentMapping
    {
        /// <summary>What the document is, in one or two sentences.</summary>
        [JsonProperty("documentSummary")]
        public string DocumentSummary { get; set; }

        /// <summary>Findings that no body region can carry, and everything that concerns the
        /// patient as a whole. Goes into the version report field, not onto the body.</summary>
        [JsonProperty("patientText")]
        public string PatientText { get; set; }

        [JsonProperty("newGroups")]
        public List<ProposedGroup> NewGroups { get; set; } = new List<ProposedGroup>();

        [JsonProperty("toolAssignments")]
        public List<ProposedToolMeaning> ToolAssignments { get; set; } = new List<ProposedToolMeaning>();

        [JsonProperty("paintings")]
        public List<ProposedPainting> Paintings { get; set; } = new List<ProposedPainting>();
    }

    /// <summary>A group the twin does not have yet and that no existing group covers.</summary>
    public class ProposedGroup
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    /// <summary>A free tool taken into use for a meaning no existing tool covers.</summary>
    public class ProposedToolMeaning
    {
        /// <summary>Name of the tool GameObject, from the list of free tools in the prompt.</summary>
        [JsonProperty("toolName")]
        public string ToolName { get; set; }

        [JsonProperty("meaning")]
        public string Meaning { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    /// <summary>One finding, on the body.</summary>
    public class ProposedPainting
    {
        /// <summary>What the document says about it.</summary>
        [JsonProperty("findingText")]
        public string FindingText { get; set; }

        /// <summary>An existing group, or one of <see cref="DocumentMapping.NewGroups"/>.</summary>
        [JsonProperty("group")]
        public string Group { get; set; }

        /// <summary>An existing tool, or one from <see cref="DocumentMapping.ToolAssignments"/>.</summary>
        [JsonProperty("toolName")]
        public string ToolName { get; set; }

        /// <summary>Every body region the finding covers - left and right, or several parts.</summary>
        [JsonProperty("regionKeys")]
        public List<string> RegionKeys { get; set; } = new List<string>();

        /// <summary>Becomes the description of the painted part.</summary>
        [JsonProperty("description")]
        public string Description { get; set; }

        /// <summary>How sure the LLM is, 0 to 1.</summary>
        [JsonProperty("confidence")]
        public float Confidence { get; set; }
    }
}
