using System.Collections.Generic;

namespace Code.AI.PromptGeneration
{
    /// <summary>One of the twin's groups, as a prompt needs to see it.</summary>
    public class GroupInfo
    {
        public string name;
        public int partCount;

        /// <summary>The meanings of the parts already in the group - what the group is about
        /// in practice, which is what tells the LLM whether a new finding belongs here.</summary>
        public List<string> meanings = new List<string>();
    }

    /// <summary>
    /// The groups of the twin that is open. Groups are the user's own categories (Injuries,
    /// Pain, Treatment, ...), so an LLM that maps a document onto the twin has to be told
    /// which ones exist before it proposes new ones.
    /// </summary>
    public static class GroupInventory
    {
        public static List<GroupInfo> All(PartManager partManager)
        {
            var groups = new List<GroupInfo>();
            if (partManager == null || partManager.groups == null)
            {
                return groups;
            }

            foreach (PartManager.GroupData group in partManager.groups)
            {
                var info = new GroupInfo
                {
                    name = group.name,
                    partCount = group.groupParts != null ? group.groupParts.Count : 0
                };

                if (group.groupParts != null)
                {
                    foreach (PartManager.PartData part in group.groupParts)
                    {
                        if (!string.IsNullOrWhiteSpace(part.meaning) && !info.meanings.Contains(part.meaning))
                        {
                            info.meanings.Add(part.meaning);
                        }
                    }
                }

                groups.Add(info);
            }
            return groups;
        }
    }
}
