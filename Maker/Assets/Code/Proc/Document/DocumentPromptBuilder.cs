using System.Collections.Generic;
using System.Text;
using Code.AI.PromptGeneration;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace Code
{
    /*
     * Builds the prompt that asks the LLM to map the findings of an uploaded document onto the
     * twin. Two parts of it are the user's, editable under Settings -> Prompts and stored per
     * twin: the task and the rules. Everything else is the state of the app at that moment -
     * which groups the twin has, which tools carry a meaning already and which are still free,
     * and the body regions that can be painted - so the prompt describes this twin and no other.
     *
     * The document itself is not part of this string; it travels as an image or as an uploaded
     * file next to it. See FEATURE_DOCUMENT_TO_TWIN.md in this folder.
     */
    public static class DocumentPromptBuilder
    {
        /// <summary>Label of the Settings row holding the task description.</summary>
        public const string PromptTask = "Document Mapping";

        /// <summary>Label of the Settings row holding the mapping rules.</summary>
        public const string PromptRules = "Document Rules";

        // the section headers, so a reader (and a test) can tell the sections apart
        public const string HeaderGroups = "EXISTING GROUPS of this twin - reuse one of these whenever a finding fits it:";
        public const string HeaderToolsInUse = "TOOLS THAT ALREADY HAVE A MEANING - use one of these when it fits the finding:";
        public const string HeaderToolsFree = "TOOLS STILL FREE - take one of these when no tool above fits, and give it a meaning:";
        public const string HeaderRegions = "BODY REGIONS - a finding can only be painted onto these, so use these keys and no others:";

        public static string Build(PartManager partManager, SettingsManager settingsManager)
        {
            var prompt = new StringBuilder();

            // first, so that an instruction the user wrote into the rows below overrides it
            prompt.AppendLine("Write every text you produce in " + LanguageName() + ".");
            prompt.AppendLine();

            AppendPromptRow(prompt, settingsManager, PromptTask);
            AppendPromptRow(prompt, settingsManager, PromptRules);

            AppendGroups(prompt, partManager);
            AppendTools(prompt);
            AppendRegions(prompt);

            return prompt.ToString();
        }

        /// <summary>The language the LLM has to answer in: the app's language family, so an
        /// English locale (en, enmed) gets English and a German one (de, demed, demedlatin)
        /// German. Region names in the prompt are already localized the same way.</summary>
        public static string LanguageName()
        {
            string code = LocalizationSettings.SelectedLocale != null
                ? LocalizationSettings.SelectedLocale.Identifier.Code
                : "en";
            return code.StartsWith("de") ? "German" : "English";
        }

        private static void AppendPromptRow(StringBuilder prompt, SettingsManager settingsManager, string label)
        {
            if (settingsManager == null)
            {
                Debug.LogWarning("[DocumentPromptBuilder] No SettingsManager - prompt row '" + label + "' is missing.");
                return;
            }

            ItemPrompt row = settingsManager.getPromptObject(label, ItemPrompt.PromptLevel.Document);
            if (row == null)
            {
                Debug.LogWarning("[DocumentPromptBuilder] No prompt row '" + label + "' on level Document.");
                return;
            }

            string text = row.GetPromptText();
            if (string.IsNullOrWhiteSpace(text))
            {
                Debug.LogWarning("[DocumentPromptBuilder] Prompt row '" + label + "' is empty.");
                return;
            }

            prompt.AppendLine(text.Trim());
            prompt.AppendLine();
        }

        private static void AppendGroups(StringBuilder prompt, PartManager partManager)
        {
            List<GroupInfo> groups = GroupInventory.All(partManager);
            prompt.AppendLine(HeaderGroups);
            if (groups.Count == 0)
            {
                prompt.AppendLine("- none yet, so every finding needs a new group");
            }
            foreach (GroupInfo group in groups)
            {
                prompt.Append("- ").Append(group.name).Append(" (").Append(group.partCount).Append(" findings");
                if (group.meanings.Count > 0)
                {
                    prompt.Append(", so far about: ").Append(string.Join(", ", group.meanings));
                }
                prompt.AppendLine(")");
            }
            prompt.AppendLine();
        }

        private static void AppendTools(StringBuilder prompt)
        {
            List<ToolInfo> tools = ToolInventory.All();

            prompt.AppendLine(HeaderToolsInUse);
            bool anyInUse = false;
            foreach (ToolInfo tool in tools)
            {
                if (!tool.inUse) continue;
                anyInUse = true;
                prompt.Append("- ").Append(tool.name).Append(", draws ").Append(tool.kindText)
                    .Append(", ").Append(Rgb(tool.color))
                    .Append(", means: ").AppendLine(tool.meaning);
            }
            if (!anyInUse)
            {
                prompt.AppendLine("- none yet");
            }
            prompt.AppendLine();

            prompt.AppendLine(HeaderToolsFree);
            bool anyFree = false;
            foreach (ToolInfo tool in tools)
            {
                if (tool.inUse) continue;
                anyFree = true;
                prompt.Append("- ").Append(tool.name).Append(", draws ").Append(tool.kindText)
                    .Append(", ").AppendLine(Rgb(tool.color));
            }
            if (!anyFree)
            {
                prompt.AppendLine("- none, so every finding has to use a tool from the list above");
            }
            prompt.AppendLine();
        }

        /// <summary>Every body region key the answer may name - the same list the prompt shows,
        /// handed to the schema so an unknown region cannot come back.</summary>
        public static List<string> RegionKeys()
        {
            var keys = new List<string>();
            foreach (PartTemplateService.TemplateTwinInfo twin in PartTemplateService.GetTemplateCatalog().twins)
            {
                foreach (PartTemplateService.TemplateRegionInfo region in twin.regions)
                {
                    keys.Add(region.key);
                }
            }
            return keys;
        }

        private static void AppendRegions(StringBuilder prompt)
        {
            prompt.AppendLine(HeaderRegions);
            PartTemplateService.TemplateCatalog catalog = PartTemplateService.GetTemplateCatalog();
            foreach (PartTemplateService.TemplateTwinInfo twin in catalog.twins)
            {
                var regions = new List<string>();
                foreach (PartTemplateService.TemplateRegionInfo region in twin.regions)
                {
                    regions.Add(region.key + " (" + region.displayName + ")");
                }
                prompt.Append(Area(twin.twinName)).Append(": ").AppendLine(string.Join("; ", regions));
            }
            prompt.AppendLine();
        }

        /// <summary>"Torso.twin" is how the template library names its files - the prompt only
        /// needs the body area, the twin a region comes from is resolved by the app.</summary>
        private static string Area(string twinName)
        {
            return twinName.EndsWith(".twin")
                ? twinName.Substring(0, twinName.Length - ".twin".Length)
                : twinName;
        }

        private static string Rgb(Color color)
        {
            return "RGB " + color.r.ToString("0.00") + " " + color.g.ToString("0.00") + " " + color.b.ToString("0.00");
        }
    }
}
