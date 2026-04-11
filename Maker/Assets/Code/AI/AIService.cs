using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Code.AI
{
    /// <summary>
    /// Generic AI service providing Unity/Coroutine bridge for OpenAI client.
    /// Handles async/await to coroutine conversion, file waiting, and error handling.
    /// This class is reusable for any AI use case, not specific to medical domain.
    /// </summary>
    public class AIService : MonoBehaviour
    {
        [Header("OpenAI Settings")]
        public string apiKey;
        public string model = "gpt-4o-mini";
        public int timeout = 120;

        protected OpenAIClient _openAIClient;

        protected virtual void Start()
        {
            InitializeClient();
        }

        protected void InitializeClient()
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("Please set the API Key in the AI component.");
                return;
            }

            _openAIClient = new OpenAIClient(apiKey, timeout);
            Debug.Log("OpenAI client initialized successfully");
        }

        /// <summary>
        /// Wait for a file to exist on disk. Useful for screenshots or generated files.
        /// </summary>
        protected IEnumerator WaitForFileCoroutine(string path)
        {
            while (!File.Exists(path))
            {
                yield return new WaitForSeconds(0.5f);
            }

            Debug.Log($"File exists: {path}");
        }

        /// <summary>
        /// Generic coroutine wrapper for async text requests.
        /// </summary>
        protected IEnumerator RequestTextCoroutine(
            string prompt,
            Action<string> onSuccess,
            Action<string> onError,
            string imagePath = null)
        {
            var task = RequestTextAsync(prompt, imagePath);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Exception != null)
            {
                var errorMessage = task.Exception.InnerException?.Message ?? task.Exception.Message;
                Debug.LogError($"AI request failed: {errorMessage}");
                onError?.Invoke(errorMessage);
            }
            else
            {
                onSuccess?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Generic coroutine wrapper for async structured requests.
        /// </summary>
        protected IEnumerator RequestStructuredCoroutine<T>(
            string prompt,
            Action<T> onSuccess,
            Action<string> onError,
            string imagePath = null) where T : class
        {
            var task = RequestStructuredAsync<T>(prompt, imagePath);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Exception != null)
            {
                var errorMessage = task.Exception.InnerException?.Message ?? task.Exception.Message;
                Debug.LogError($"AI request failed: {errorMessage}");
                onError?.Invoke(errorMessage);
            }
            else
            {
                onSuccess?.Invoke(task.Result);
            }
        }

        /// <summary>
        /// Async text request to OpenAI.
        /// </summary>
        protected async Task<string> RequestTextAsync(string prompt, string imagePath = null)
        {
            try
            {
                return await _openAIClient.RequestAsync(
                    prompt,
                    model,
                    imagePath: string.IsNullOrEmpty(imagePath) ? null : imagePath
                );
            }
            catch (OpenAIException ex)
            {
                throw new Exception($"OpenAI API error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Async structured request to OpenAI.
        /// </summary>
        protected async Task<T> RequestStructuredAsync<T>(string prompt, string imagePath = null) where T : class
        {
            try
            {
                return await _openAIClient.RequestStructuredAsync<T>(
                    prompt,
                    model,
                    imagePath: string.IsNullOrEmpty(imagePath) ? null : imagePath
                );
            }
            catch (OpenAIException ex)
            {
                throw new Exception($"OpenAI API error: {ex.Message}", ex);
            }
        }
    }
}
