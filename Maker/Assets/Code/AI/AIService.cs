using System;
using System.Collections;
using System.Collections.Generic;
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
        /// <summary>Leave empty. A key here is serialized into the scene and ends up in the
        /// repository - see ApiKeys for where to put it instead.</summary>
        public string apiKey;
        public string model = "gpt-4o-mini";
        public int timeout = 120;

        protected OpenAIClient _openAIClient;

        /// <summary>The key actually in use, wherever it came from. Empty when there is none.</summary>
        public string resolvedApiKey { get; private set; }

        public bool hasApiKey => !string.IsNullOrEmpty(resolvedApiKey);

        protected virtual void Start()
        {
            InitializeClient();
        }

        protected void InitializeClient()
        {
            resolvedApiKey = ApiKeys.OpenAi(apiKey);

            if (!hasApiKey)
            {
                Debug.LogError("No OpenAI API key. Put it into " + ApiKeys.SearchedPlaces()
                    + " as {\"" + ApiKeys.JsonMember + "\": \"sk-...\"} - not onto the AI component,"
                    + " which would carry it into the repository.");
                return;
            }

            _openAIClient = new OpenAIClient(resolvedApiKey, timeout);
            Debug.Log("OpenAI client initialized successfully");
        }

        /// <summary>
        /// Uploads a file and hands back its id. Everything that is not an image has to travel
        /// this way; an image is embedded into the request instead.
        /// </summary>
        public async Task<string> UploadFileAsync(string filePath)
        {
            try
            {
                return await _openAIClient.UploadFileAsync(filePath);
            }
            catch (OpenAIException ex)
            {
                throw new Exception($"OpenAI file upload failed: {ex.Message}", ex);
            }
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
        /// <param name="fileId">Id of a file uploaded beforehand - the only way to send anything
        /// that is not an image, a PDF above all.</param>
        /// <param name="allowedValues">Value lists for members whose options only exist at
        /// runtime, keyed by member path. See JsonSchemaBuilder.</param>
        protected IEnumerator RequestStructuredCoroutine<T>(
            string prompt,
            Action<T> onSuccess,
            Action<string> onError,
            string imagePath = null,
            string fileId = null,
            IDictionary<string, IEnumerable<string>> allowedValues = null) where T : class
        {
            var task = RequestStructuredAsync<T>(prompt, imagePath, fileId, allowedValues);
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
        protected async Task<T> RequestStructuredAsync<T>(
            string prompt,
            string imagePath = null,
            string fileId = null,
            IDictionary<string, IEnumerable<string>> allowedValues = null) where T : class
        {
            try
            {
                return await _openAIClient.RequestStructuredAsync<T>(
                    prompt,
                    model,
                    fileId: string.IsNullOrEmpty(fileId) ? null : fileId,
                    imagePath: string.IsNullOrEmpty(imagePath) ? null : imagePath,
                    allowedValues: allowedValues
                );
            }
            catch (OpenAIException ex)
            {
                throw new Exception($"OpenAI API error: {ex.Message}", ex);
            }
        }
    }
}
