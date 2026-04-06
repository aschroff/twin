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
                    var errorMsg = $"File upload failed: {request.error}\nResponse: {request.downloadHandler?.text}";
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
            Type structuredOutputType = null)
        {
            // Build request payload (handles both fileId and imagePath)
            var payload = BuildRequestPayload(prompt, model, fileId, imagePath, structuredOutputType);
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
                    var errorMsg = $"Request failed: {request.error}\nResponse: {request.downloadHandler?.text}";
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
            string model = "gpt-4.5",
            string fileId = null,
            string imagePath = null) where T : class
        {
            var rawResponse = await RequestAsync(prompt, model, fileId, imagePath, typeof(T));

            if (string.IsNullOrEmpty(rawResponse))
            {
                throw new OpenAIException(0, "Received null or empty response from API");
            }

            // The raw response is the full API response JSON
            // We need to extract the actual content and parse it
            Debug.Log($"Parsing raw response to extract structured output...");

            // Parse the response using JObject
            var apiResponse = JObject.Parse(rawResponse);

            // Extract the JSON content from the Responses API structure
            // Based on actual API response, the structure is: output[0].content[0].text
            var outputArray = apiResponse["output"] as JArray;
            if (outputArray == null || outputArray.Count == 0)
            {
                throw new OpenAIException(0, $"No output array in API response. Response: {rawResponse}");
            }

            var firstOutput = outputArray[0] as JObject;
            var contentArray = firstOutput["content"] as JArray;
            if (contentArray == null || contentArray.Count == 0)
            {
                throw new OpenAIException(0, $"No content array in output. Response: {rawResponse}");
            }

            var firstContent = contentArray[0] as JObject;
            var textToken = firstContent["text"];
            if (textToken == null)
            {
                throw new OpenAIException(0, $"No text field in content. Response: {rawResponse}");
            }

            var contentJson = textToken.ToString();
            Debug.Log($"Extracted JSON content: {contentJson}");

            // Now deserialize the actual content
            return JsonConvert.DeserializeObject<T>(contentJson);
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
                    var errorMsg = $"Failed to list models: {request.error}\nResponse: {request.downloadHandler?.text}";
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

        private object BuildRequestPayload(string prompt, string model, string fileId, string imagePath, Type structuredOutputType)
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
                        ? GetSchemaForType(structuredOutputType)
                        : new { type = "text" } // Default to text output if no structured type specified
                }
            };
        }

        private object GetSchemaForType(Type type)
        {
            // Generate basic JSON schema for the type
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var prop in type.GetProperties())
            {
                var jsonProp = prop.GetCustomAttributes(typeof(JsonPropertyAttribute), false);
                var propName = jsonProp.Length > 0
                    ? ((JsonPropertyAttribute)jsonProp[0]).PropertyName
                    : prop.Name;

                // Determine type
                var propType = prop.PropertyType;
                string schemaType;

                if (propType == typeof(string))
                {
                    schemaType = "string";
                }
                else if (propType == typeof(int) || propType == typeof(long))
                {
                    schemaType = "integer";
                }
                else if (propType == typeof(float) || propType == typeof(double))
                {
                    schemaType = "number";
                }
                else if (propType == typeof(bool))
                {
                    schemaType = "boolean";
                }
                else if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    schemaType = "array";
                }
                else
                {
                    schemaType = "object";
                }

                properties[propName] = new { type = schemaType };
                required.Add(propName);
            }

            // Return in the format expected by Responses API: text.format
            return new
            {
                type = "json_schema",
                name = type.Name,
                schema = new
                {
                    type = "object",
                    properties,
                    required = required.ToArray(),
                    additionalProperties = false
                },
                strict = true
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
