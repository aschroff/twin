using System;
using System.Collections;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Code.AI
{
    /// <summary>
    /// Medical-specific AI service for injury/treatment analysis and patient assessment.
    /// Contains medical domain logic and structured response types.
    /// </summary>
    public class MedicalAI : AIService
    {
        /// <summary>
        /// Structured response for single injury/treatment description
        /// </summary>
        [Serializable]
        public class InjuryDescriptionResponse
        {
            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("category")]
            public string Category { get; set; } // "treatment", "injury", "both", "unknown"
        }

        /// <summary>
        /// Structured response for patient summary
        /// </summary>
        [Serializable]
        public class PatientSummaryResponse
        {
            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("condition")]
            public string Condition { get; set; } // "serious", "not serious", "unknown"
        }

        /// <summary>
        /// Analyze an injury/treatment from an image and text prompt.
        /// Returns structured response with description and category.
        /// </summary>
        public IEnumerator AnalyzeInjuryCoroutine(
            string prompt,
            string imagePath,
            Action<InjuryDescriptionResponse> onSuccess,
            Action<string> onError)
        {
            // Wait for image file if needed
            if (!string.IsNullOrEmpty(imagePath))
            {
                yield return StartCoroutine(WaitForFileCoroutine(imagePath));
            }

            // Request structured output
            yield return StartCoroutine(RequestStructuredCoroutine<InjuryDescriptionResponse>(
                prompt,
                onSuccess,
                onError,
                imagePath
            ));
        }

        /// <summary>
        /// Async version of injury analysis.
        /// </summary>
        public async Task<InjuryDescriptionResponse> AnalyzeInjuryAsync(string prompt, string imagePath = null)
        {
            return await RequestStructuredAsync<InjuryDescriptionResponse>(prompt, imagePath);
        }

        /// <summary>
        /// Generate patient summary from multiple injury descriptions.
        /// Returns structured response with summary and condition assessment.
        /// </summary>
        public IEnumerator GeneratePatientSummaryCoroutine(
            string prompt,
            Action<PatientSummaryResponse> onSuccess,
            Action<string> onError,
            string imagePath = null)
        {
            yield return StartCoroutine(RequestStructuredCoroutine<PatientSummaryResponse>(
                prompt,
                onSuccess,
                onError,
                imagePath
            ));
        }

        /// <summary>
        /// Async version of patient summary generation.
        /// </summary>
        public async Task<PatientSummaryResponse> GeneratePatientSummaryAsync(string prompt, string imagePath = null)
        {
            return await RequestStructuredAsync<PatientSummaryResponse>(prompt, imagePath);
        }
    }
}
