using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.AI
{
    /// <summary>
    /// OpenAI API client supporting GPT-4.5, file uploads, and structured outputs.
    /// Uses async/await pattern for cleaner code flow.
    /// </summary>
    public class OpenAIClient
    {
        private readonly string _apiKey;
        private readonly int _timeout;
        private const string BaseUrl = "https://api.openai.com/v1";

        public OpenAIClient(string apiKey, int timeout = 120)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));
            }

            _apiKey = apiKey;
            _timeout = timeout;
        }

        /// <summary>
        /// Upload a file to OpenAI for use in API requests.
        /// </summary>
        /// <param name="filePath">Path to the file to upload</param>
        /// <param name="purpose">Purpose of the file (default: "user_data")</param>
        /// <returns>Uploaded file ID</returns>
        public async Task<string> UploadFileAsync(string filePath, string purpose = "user_data")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            var fileName = Path.GetFileName(filePath);
            var fileBytes = File.ReadAllBytes(filePath);

            // Use WWWForm for better compatibility
            var form = new WWWForm();
            form.AddBinaryData("file", fileBytes, fileName, DetermineContentType(filePath));
            form.AddField("purpose", purpose);

            var request = UnityWebRequest.Post($"{BaseUrl}/files", form);
            request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            request.timeout = _timeout;

            var tcs = new TaskCompletionSource<string>();

            request.SendWebRequest().completed += _ =>
            {
                if (!string.IsNullOrEmpty(request.error) || request.responseCode >= 400)
                {
                    var maskedKey = _apiKey.Length > 8
                        ? _apiKey.Substring(0, 4) + "..." + _apiKey.Substring(_apiKey.Length - 4)
                        : "***";
                    var errorMsg = $"File upload failed: {request.error}\nResponse: {request.downloadHandler?.text}\nAPI Key: {maskedKey}";
                    Debug.LogError(errorMsg);
                    tcs.SetException(new OpenAIException(request.responseCode, errorMsg));
                }
                else
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<FileUploadResponse>(request.downloadHandler.text);
                        Debug.Log($"File uploaded successfully: {response.id}");
                        tcs.SetResult(response.id);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(new OpenAIException(0, $"Failed to parse file upload response: {ex.Message}"));
                    }
                }

                request.Dispose();
            };

            return await tcs.Task;
        }

        /// <summary>
        /// Determine the MIME content type based on file extension.
        /// </summary>
        private string DetermineContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".txt" => "text/plain",
                ".pdf" => "application/pdf",
                ".json" => "application/json",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Send a request to OpenAI with optional file and structured output.
        /// </summary>
        /// <param name="prompt">The text prompt</param>
        /// <param name="model">Model to use (default: gpt-4o-mini)</param>
        /// <param name="fileId">Optional uploaded file ID (for non-image documents)</param>
        /// <param name="imagePath">Optional local image path (will be embedded as base64)</param>
        /// <param name="structuredOutputType">Optional C# type for structured output parsing</param>
        /// <returns>Response text or structured output as JSON</returns>
        public async Task<string> RequestAsync(
            string prompt,
            string model = "gpt-4o-mini",
            string fileId = null,
            string imagePath = null,
            Type structuredOutputType = null,
            IDictionary<string, IEnumerable<string>> allowedValues = null)
        {
            // Build request payload (handles both fileId and imagePath)
            var payload = BuildRequestPayload(prompt, model, fileId, imagePath, structuredOutputType, allowedValues);
            var jsonPayload = JsonConvert.SerializeObject(payload);

            Debug.Log($"Request JSON: {jsonPayload}");

            // Use Responses API (not responses/parse)
            var endpoint = $"{BaseUrl}/responses";

            var request = new UnityWebRequest(endpoint, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            request.timeout = _timeout;

            var tcs = new TaskCompletionSource<string>();

            request.SendWebRequest().completed += _ =>
            {
                if (!string.IsNullOrEmpty(request.error) || request.responseCode >= 400)
                {
                    var maskedKey = _apiKey.Length > 8
                        ? _apiKey.Substring(0, 4) + "..." + _apiKey.Substring(_apiKey.Length - 4)
                        : "***";
                    var errorMsg = $"Request failed: {request.error}\nResponse: {request.downloadHandler?.text}\nModel: {model}\nAPI Key: {maskedKey}";
                    // Don't log as error - let the caller decide how to handle it
                    tcs.SetException(new OpenAIException(request.responseCode, errorMsg));
                }
                else
                {
                    try
                    {
                        var responseText = request.downloadHandler.text;

                        if (string.IsNullOrEmpty(responseText))
                        {
                            tcs.SetException(new OpenAIException(0, "Received empty response from API"));
                            return;
                        }

                        Debug.Log($"Raw API Response: {responseText}");

                        // Just return the raw response - let the caller parse it
                        // This way we can see what we actually get and handle it properly
                        tcs.SetResult(responseText);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(new OpenAIException(0, $"Failed to get response: {ex.Message}"));
                    }
                }

                request.Dispose();
            };

            return await tcs.Task;
        }

        /// <summary>
        /// Send a request with structured output. Returns deserialized object of type T.
        /// </summary>
        public async Task<T> RequestStructuredAsync<T>(
            string prompt,
            string model = "gpt-4o-mini",
            string fileId = null,
            string imagePath = null,
            IDictionary<string, IEnumerable<string>> allowedValues = null) where T : class
        {
            var rawResponse = await RequestAsync(prompt, model, fileId, imagePath, typeof(T), allowedValues);

            if (string.IsNullOrEmpty(rawResponse))
            {
                throw new OpenAIException(0, "Received null or empty response from API");
            }

            // The raw response is the full API response JSON
            // We need to extract the actual content and parse it
            Debug.Log($"Parsing raw response to extract structured output...");

            // Parse the response using JObject
            var apiResponse = JObject.Parse(rawResponse);
            var contentJson = ExtractOutputText(apiResponse, rawResponse);
            Debug.Log($"Extracted JSON content: {contentJson}");

            // Now deserialize the actual content
            return JsonConvert.DeserializeObject<T>(contentJson);
        }

        /// <summary>
        /// The answer text out of a Responses API reply. The output array is a list of items and
        /// only some of them carry text - a reasoning model puts its reasoning first - so the
        /// first text item is looked for instead of assuming output[0].content[0].
        /// </summary>
        private static string ExtractOutputText(JObject apiResponse, string rawResponse)
        {
            // the API offers this shortcut when it is available
            var direct = apiResponse["output_text"];
            if (direct != null && direct.Type == JTokenType.String && !string.IsNullOrEmpty(direct.ToString()))
            {
                return direct.ToString();
            }

            var outputArray = apiResponse["output"] as JArray;
            if (outputArray == null || outputArray.Count == 0)
            {
                throw new OpenAIException(0, $"No output array in API response. Response: {rawResponse}");
            }

            foreach (var output in outputArray)
            {
                var contentArray = output["content"] as JArray;
                if (contentArray == null) continue;

                foreach (var content in contentArray)
                {
                    var textToken = content["text"];
                    if (textToken != null && !string.IsNullOrEmpty(textToken.ToString()))
                    {
                        return textToken.ToString();
                    }
                }
            }

            // a refusal is the other thing the model can put where the answer should be
            foreach (var output in outputArray)
            {
                var contentArray = output["content"] as JArray;
                if (contentArray == null) continue;
                foreach (var content in contentArray)
                {
                    var refusal = content["refusal"];
                    if (refusal != null && !string.IsNullOrEmpty(refusal.ToString()))
                    {
                        throw new OpenAIException(0, $"The model refused the request: {refusal}");
                    }
                }
            }

            throw new OpenAIException(0, $"No text in any output item. Response: {rawResponse}");
        }

        /// <summary>
        /// List all available models from OpenAI API.
        /// </summary>
        /// <returns>List of model IDs available to your account</returns>
        public async Task<List<string>> ListAvailableModelsAsync()
        {
            var request = new UnityWebRequest($"{BaseUrl}/models", "GET");
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            request.timeout = _timeout;

            var tcs = new TaskCompletionSource<List<string>>();

            request.SendWebRequest().completed += _ =>
            {
                if (!string.IsNullOrEmpty(request.error) || request.responseCode >= 400)
                {
                    var maskedKey = _apiKey.Length > 8
                        ? _apiKey.Substring(0, 4) + "..." + _apiKey.Substring(_apiKey.Length - 4)
                        : "***";
                    var errorMsg = $"Failed to list models: {request.error}\nResponse: {request.downloadHandler?.text}\nAPI Key: {maskedKey}";
                    tcs.SetException(new OpenAIException(request.responseCode, errorMsg));
                }
                else
                {
                    try
                    {
                        var responseText = request.downloadHandler.text;
                        var response = JObject.Parse(responseText);
                        var dataArray = response["data"] as JArray;

                        if (dataArray == null)
                        {
                            tcs.SetException(new OpenAIException(0, "No data array in models response"));
                            return;
                        }

                        var modelIds = new List<string>();
                        foreach (var item in dataArray)
                        {
                            var modelId = item["id"]?.ToString();
                            if (!string.IsNullOrEmpty(modelId))
                            {
                                modelIds.Add(modelId);
                            }
                        }

                        tcs.SetResult(modelIds);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(new OpenAIException(0, $"Failed to parse models response: {ex.Message}"));
                    }
                }

                request.Dispose();
            };

            return await tcs.Task;
        }

        private object BuildRequestPayload(string prompt, string model, string fileId, string imagePath,
            Type structuredOutputType, IDictionary<string, IEnumerable<string>> allowedValues = null)
        {
            // Build content array for Responses API
            var content = new List<object>();

            // Add text prompt
            content.Add(new Dictionary<string, string>
            {
                { "type", "input_text" },
                { "text", prompt }
            });

            // Add file if provided (for non-image documents)
            if (!string.IsNullOrEmpty(fileId))
            {
                content.Add(new Dictionary<string, string>
                {
                    { "type", "input_file" },
                    { "file_id", fileId }
                });
            }
            // Add image if provided (embedded as base64)
            else if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
            {
                var extension = Path.GetExtension(imagePath).ToLower();
                var isImage = extension == ".png" || extension == ".jpg" || extension == ".jpeg" ||
                              extension == ".gif" || extension == ".webp" || extension == ".bmp";

                if (isImage)
                {
                    var imageBytes = File.ReadAllBytes(imagePath);
                    var base64Image = Convert.ToBase64String(imageBytes);

                    // Determine MIME type
                    var mimeType = extension switch
                    {
                        ".png" => "image/png",
                        ".jpg" => "image/jpeg",
                        ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        ".webp" => "image/webp",
                        ".bmp" => "image/bmp",
                        _ => "image/png"
                    };

                    content.Add(new Dictionary<string, object>
                    {
                        { "type", "input_image" },
                        { "image_url", $"data:{mimeType};base64,{base64Image}" }
                    });
                }
            }

            return new
            {
                model,
                input = new[]
                {
                    new
                    {
                        role = "user",
                        content
                    }
                },
                text = new
                {
                    format = structuredOutputType != null
                        ? JsonSchemaBuilder.Format(structuredOutputType, allowedValues)
                        : new { type = "text" } // Default to text output if no structured type specified
                }
            };
        }

        #region Response Data Structures

        [Serializable]
        private class FileUploadResponse
        {
            public string id;
            public string @object;
            public int bytes;
            public long created_at;
            public string filename;
            public string purpose;
        }

        [Serializable]
        private class ChatCompletionResponse
        {
            public string id;
            public string @object;
            public long created;
            public string model;
            public Choice[] choices;
            public Usage usage;

            [Serializable]
            public class Choice
            {
                public int index;
                public Message message;
                public string finish_reason;
            }

            [Serializable]
            public class Message
            {
                public string role;
                public string content;
            }

            [Serializable]
            public class Usage
            {
                public int prompt_tokens;
                public int completion_tokens;
                public int total_tokens;
            }
        }

        [Serializable]
        private class ResponsesParsedResponse
        {
            public string id;
            public string @object;
            public long created;
            public string model;
            public string output_parsed_json;
        }

        #endregion
    }

    /// <summary>
    /// Exception thrown when OpenAI API calls fail.
    /// </summary>
    public class OpenAIException : Exception
    {
        public long StatusCode { get; }

        public OpenAIException(long statusCode, string message) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
