using System;
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

            // UI feedback for missing API key - the key may come from outside the scene, so it is
            // the resolved one that matters, not the field
            if (!hasApiKey)
            {
                const string errorMessage = "No <b>API Key</b> found. See ApiKeys for where to put it.";
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
        
        /// <summary>
        /// Asks for the findings of a document, mapped onto this twin. The prompt comes from
        /// DocumentPromptBuilder; the document travels as an image path or, for anything that is
        /// not an image, as the id of a file uploaded first. The body regions the answer may name
        /// are handed over as a value list, so the schema itself rules out an unknown region.
        /// </summary>
        public void MapDocument(
            string prompt,
            string imagePath,
            string fileId,
            IEnumerable<string> regionKeys,
            Action<DocumentMapping> onSuccess,
            Action<string> onError)
        {
            var allowedValues = new Dictionary<string, IEnumerable<string>>
            {
                { "paintings.regionKeys", new List<string>(regionKeys) }
            };

            StartCoroutine(RequestStructuredCoroutine(
                prompt,
                onSuccess,
                onError,
                imagePath,
                fileId,
                allowedValues));
        }

        /// <summary>Uploads a document so it can be sent as a file - see MapDocument.</summary>
        public IEnumerator UploadDocumentCoroutine(string path, Action<string> onSuccess, Action<string> onError)
        {
            var task = UploadFileAsync(path);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Exception != null)
            {
                onError?.Invoke(task.Exception.InnerException?.Message ?? task.Exception.Message);
            }
            else
            {
                onSuccess?.Invoke(task.Result);
            }
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
