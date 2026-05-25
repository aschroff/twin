using System.Collections;
using System.Collections.Generic;
using Code.AI.PromptGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace Code.AI
{
    /// <summary>
    /// Medical AI component for Unity integration.
    /// Bridges Unity UI and game objects with MedicalAI service.
    /// </summary>
    public class AI : MedicalAI
    {
        public enum Help
        {
            ShortSummary,
            Treatment,
            CompleteReport,
            Situation
        }

        public ISet<Help> Whatever;

        [Space]
        public Text characterDescription;
        [Space]
        public InputField partDescription;

        public string path;
        public SettingsManager settingsManager;

        protected override void Start()
        {
            base.Start();

            // UI feedback for missing API key
            if (string.IsNullOrEmpty(apiKey))
            {
                const string errorMessage = "Please set the <b>API Key</b> in the AI component.";
                if (characterDescription != null)
                {
                    characterDescription.text = errorMessage;
                    characterDescription.color = Color.magenta;
                }
            }
        }
        

        private string GetPromptOfLabel(string label, ItemPrompt.PromptLevel level = ItemPrompt.PromptLevel.Unknown)
        {
            Debug.Log("getPromptOfLabel");
            Debug.Log(label);
            Debug.Log(level);
            return GetPromptResultOfLabel(label, level).gameObject.GetComponentInChildren<InputField>().text;
        }

        private ItemPrompt GetPromptResultOfLabel(string label, ItemPrompt.PromptLevel level = ItemPrompt.PromptLevel.Unknown)
        {
            return settingsManager.getPromptObject(label, level);
        }
        
        
        private IEnumerator DescribePartCoroutine(PartManager.PartData part, string variant)
        {
            string prompt = GetPromptOfLabel(variant, ItemPrompt.PromptLevel.Part);
            prompt += Part.Description(part);

            string imagePath = !string.IsNullOrEmpty(part.pathScreenshot) ? part.pathScreenshot : null;

            // Use MedicalAI's injury analysis
            yield return StartCoroutine(AnalyzeInjuryCoroutine(
                prompt,
                imagePath,
                response => OnInjuryAnalyzed(part, response),
                error => OnInjuryAnalysisError(part, error)
            ));
        }

        private void OnInjuryAnalyzed(PartManager.PartData part, InjuryDescriptionResponse response)
        {
            part.description = response.Description;
            Debug.Log($"AI response for {part.meaning}: {response.Description} (Category: {response.Category})");

            if (characterDescription != null & characterDescription.isActiveAndEnabled)
            {
                characterDescription.text += "---------------------------------------------------\n";
                characterDescription.text += $"{response.Description}\n";
                characterDescription.text += $"Category: {response.Category}\n";
            }
            else if (partDescription != null & partDescription.isActiveAndEnabled)
            {
                partDescription.text = response.Description;
            }
        }

        private void OnInjuryAnalysisError(PartManager.PartData part, string errorMessage)
        {
            part.description = $"Error: {errorMessage}";
            Debug.LogError($"AI request failed for {part.meaning}: {errorMessage}");
        }

        public string DescribePart(PartManager.PartData part, string variant)
        {
            StartCoroutine(DescribePartCoroutine(part, variant));
            return "";
        }
        
        public void CompleteReport()
        {
            string prompt = "The person is 1.60 m tall. Describe the medical findings depicted on the body and make a recommendation for treating these problems.";
            prompt += PromptGeneration.PromptContributor.GeneratePrompt(Help.CompleteReport);
            GenerateSummary(prompt, path);
        }

        public void DescribeVersion(PartManager partManager, string variant)
        {
            string prompt = GetPromptOfLabel(variant, ItemPrompt.PromptLevel.Version);
            int countFinding = 0;
            foreach (PartManager.GroupData group in partManager.groups)
            {
                foreach (PartManager.PartData part in group.groupParts)
                {
                    countFinding++;
                    prompt += "Part Number " + countFinding + " :\n";
                    prompt += part.description + "\n";
                }
            }
            GenerateSummary(prompt, variant);
        }

        private void GenerateSummary(string prompt, string variant, string imagePath = "")
        {
            StartCoroutine(GenerateSummaryCoroutine(prompt, variant, imagePath));
        }

        private IEnumerator GenerateSummaryCoroutine(string prompt, string variant, string imagePath)
        {
            ItemPrompt itemPrompt = GetPromptResultOfLabel(variant, ItemPrompt.PromptLevel.Version);

            if (characterDescription != null)
            {
                characterDescription.text = "<in progress> ";
            }

            // Use MedicalAI's patient summary generation
            yield return StartCoroutine(GeneratePatientSummaryCoroutine(
                prompt,
                response => OnPatientSummaryGenerated(itemPrompt, response),
                error => OnPatientSummaryError(error),
                string.IsNullOrEmpty(imagePath) ? null : imagePath
            ));
        }

        private void OnPatientSummaryGenerated(ItemPrompt itemPrompt, PatientSummaryResponse response)
        {
            if (characterDescription != null)
            {
                characterDescription.text = $"{response.Description}\n\nCondition: {response.Condition}";
            }
            itemPrompt.promptResult = response.Description;
        }

        private void OnPatientSummaryError(string errorMessage)
        {
            if (characterDescription != null)
            {
                characterDescription.text = $"Error: {errorMessage}";
                characterDescription.color = Color.red;
            }
            Debug.LogError($"AI request failed: {errorMessage}");
        }
    }
}
