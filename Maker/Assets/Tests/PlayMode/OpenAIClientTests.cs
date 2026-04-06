using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.AI;
using Code.AI.StructuredOutputs;
using NUnit.Framework;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.TestTools;

public class OpenAIClientTests
{
    private const string TestApiKey = "";
    private const string TestModel = "gpt-4o-mini";

    // Simple test output structure
    [System.Serializable]
    public class SimpleTextOutput : StructuredOutputBase
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    // Image description output structure for integration test
    [System.Serializable]
    public class ImageDescription : StructuredOutputBase
    {
        [JsonProperty("description")]
        public string Description { get; set; }
    }

    [UnityTest]
    public IEnumerator OpenAIClient_SimpleTextRequest_ReturnsResponse()
    {
        // Skip test if API key is not configured
        if (string.IsNullOrEmpty(TestApiKey) || TestApiKey == "YOUR_API_KEY_HERE")
        {
            Assert.Ignore("API key not configured. Set TestApiKey in OpenAIClientTests.cs to run this test.");
            yield break;
        }

        // Arrange
        var client = new OpenAIClient(TestApiKey, timeout: 30);
        var prompt = "Respond with a JSON object containing a 'message' field with the text 'Hello, Unity!'";
        SimpleTextOutput response = null;
        System.Exception error = null;

        // Act - Use structured output
        var task = client.RequestStructuredAsync<SimpleTextOutput>(prompt, TestModel);

        // Wait for the async task to complete
        while (!task.IsCompleted)
        {
            yield return null;
        }

        // Get result or error
        if (task.Exception != null)
        {
            error = task.Exception.InnerException ?? task.Exception;
        }
        else
        {
            response = task.Result;
        }

        // Assert
        Assert.IsNull(error, $"Request failed with error: {error?.Message}");
        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsNotNull(response.Message, "Response message should not be null");
        Assert.IsNotEmpty(response.Message, "Response message should not be empty");

        Debug.Log($"OpenAI Response: {response.Message}");

        // The response should contain something related to our prompt
        Assert.IsTrue(
            response.Message.ToLower().Contains("hello") || response.Message.ToLower().Contains("unity"),
            $"Response should contain 'hello' or 'unity'. Got: {response.Message}"
        );
    }

    [UnityTest]
    public IEnumerator OpenAIClient_WithInvalidApiKey_ThrowsException()
    {
        // Arrange
        var client = new OpenAIClient("invalid-api-key", timeout: 10);
        var prompt = "Test prompt";
        System.Exception error = null;

        // Act
        var task = client.RequestStructuredAsync<SimpleTextOutput>(prompt, TestModel);

        // Wait for the async task to complete
        while (!task.IsCompleted)
        {
            yield return null;
        }

        // Get error
        if (task.Exception != null)
        {
            error = task.Exception.InnerException ?? task.Exception;
        }

        // Assert
        Assert.IsNotNull(error, "Should throw an exception with invalid API key");
        Assert.IsInstanceOf<OpenAIException>(error, "Should throw OpenAIException");

        Debug.Log($"Expected error occurred: {error.Message}");
    }

    [UnityTest]
    public IEnumerator OpenAIClient_Constructor_WithEmptyApiKey_ThrowsException()
    {
        // Arrange & Act & Assert
        Assert.Throws<System.ArgumentException>(() =>
        {
            var client = new OpenAIClient("");
        });

        yield return null;
    }

    [UnityTest]
    public IEnumerator OpenAIClient_MultipleRequests_AllComplete()
    {
        // Skip test if API key is not configured
        if (string.IsNullOrEmpty(TestApiKey) || TestApiKey == "YOUR_API_KEY_HERE")
        {
            Assert.Ignore("API key not configured. Set TestApiKey in OpenAIClientTests.cs to run this test.");
            yield break;
        }

        // Arrange
        var client = new OpenAIClient(TestApiKey, timeout: 30);
        var prompts = new[]
        {
            "Respond with JSON: {\"message\": \"1\"}",
            "Respond with JSON: {\"message\": \"2\"}",
            "Respond with JSON: {\"message\": \"3\"}"
        };

        var tasks = new System.Threading.Tasks.Task<SimpleTextOutput>[prompts.Length];

        // Act - Start all requests
        for (int i = 0; i < prompts.Length; i++)
        {
            tasks[i] = client.RequestStructuredAsync<SimpleTextOutput>(prompts[i], TestModel);
        }

        // Wait for all to complete
        while (!System.Threading.Tasks.Task.WhenAll(tasks).IsCompleted)
        {
            yield return null;
        }

        // Assert
        for (int i = 0; i < tasks.Length; i++)
        {
            Assert.IsFalse(tasks[i].IsFaulted, $"Request {i} failed: {tasks[i].Exception?.Message}");
            Assert.IsNotNull(tasks[i].Result, $"Request {i} returned null");
            Assert.IsNotNull(tasks[i].Result.Message, $"Request {i} message is null");

            Debug.Log($"Response {i}: {tasks[i].Result.Message}");
        }
    }

    [UnityTest]
    public IEnumerator OpenAIClient_ListAvailableModels_ContainsGpt4o()
    {
        // Skip test if API key is not configured
        if (string.IsNullOrEmpty(TestApiKey) || TestApiKey == "YOUR_API_KEY_HERE")
        {
            Assert.Ignore("API key not configured. Set TestApiKey in OpenAIClientTests.cs to run this test.");
            yield break;
        }

        // Arrange
        var client = new OpenAIClient(TestApiKey, timeout: 30);
        List<string> availableModels = null;
        System.Exception error = null;

        // Act
        var task = client.ListAvailableModelsAsync();

        // Wait for the async task to complete
        while (!task.IsCompleted)
        {
            yield return null;
        }

        // Get result or error
        if (task.Exception != null)
        {
            error = task.Exception.InnerException ?? task.Exception;
        }
        else
        {
            availableModels = task.Result;
        }

        // Assert
        Assert.IsNull(error, $"Failed to list models: {error?.Message}");
        Assert.IsNotNull(availableModels, "Available models list should not be null");
        Assert.IsTrue(availableModels.Count > 0, "Should have at least one model available");

        // Log all available models
        Debug.Log($"Available models ({availableModels.Count}):");
        foreach (var model in availableModels)
        {
            Debug.Log($"  - {model}");
        }

        // Assert that gpt-4o-mini is available (the model we use in tests)
        Assert.IsTrue(
            availableModels.Any(m => m.Contains("gpt-4o")),
            $"Expected gpt-4o model to be available. Available models: {string.Join(", ", availableModels)}"
        );

        // Assert that the TestModel is available
        Assert.IsTrue(
            availableModels.Contains(TestModel) || availableModels.Any(m => m.StartsWith(TestModel)),
            $"Expected test model '{TestModel}' to be available. Available models: {string.Join(", ", availableModels)}"
        );
    }

    [UnityTest]
    public IEnumerator AI_Component_Integration_WithImageUpload()
    {
        // This test uses the actual AI component settings from the scene
        // If this test passes, the real application should work

        // Load the scene
        yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Maker Main", UnityEngine.SceneManagement.LoadSceneMode.Single);
        yield return null;

        // Find the AI component in the scene
        var aiComponent = Object.FindObjectOfType<AI>();

        if (aiComponent == null)
        {
            Assert.Ignore("AI component not found in scene. This test requires the AI component to be configured in 'Maker Main' scene.");
            yield break;
        }

        // Check if API key is configured
        if (string.IsNullOrEmpty(aiComponent.apiKey))
        {
            Assert.Ignore("API key not configured in AI component. Please set the API key in the Inspector.");
            yield break;
        }

        // Create client with settings from AI component
        var client = new OpenAIClient(aiComponent.apiKey, aiComponent.timeout);

        Debug.Log($"Testing with AI component settings:");
        Debug.Log($"  Model: {aiComponent.model}");
        Debug.Log($"  Timeout: {aiComponent.timeout}");

        // Create a simple test image (1x1 red pixel PNG)
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.red);
        texture.Apply();

        var imageBytes = texture.EncodeToPNG();
        var tempImagePath = System.IO.Path.Combine(Application.temporaryCachePath, "test_image.png");
        System.IO.File.WriteAllBytes(tempImagePath, imageBytes);

        Debug.Log($"Created test image at: {tempImagePath}");

        // Test with structured output and image - this is the real use case
        var prompt = "Describe what you see in this image. Respond with JSON containing a 'description' field.";

        ImageDescription response = null;
        System.Exception error = null;

        // Make the request using the AI component's model and settings
        var task = client.RequestStructuredAsync<ImageDescription>(
            prompt,
            aiComponent.model,
            imagePath: tempImagePath
        );

        // Wait for completion
        while (!task.IsCompleted)
        {
            yield return null;
        }

        // Get result
        if (task.Exception != null)
        {
            error = task.Exception.InnerException ?? task.Exception;
        }
        else
        {
            response = task.Result;
        }

        // Cleanup
        if (System.IO.File.Exists(tempImagePath))
        {
            System.IO.File.Delete(tempImagePath);
        }

        // Assert
        Assert.IsNull(error, $"Integration test failed with error: {error?.Message}");
        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsNotNull(response.Description, "Description should not be null");
        Assert.IsNotEmpty(response.Description, "Description should not be empty");

        Debug.Log($"AI Response with image: {response.Description}");
        Debug.Log($"✅ Integration test passed! The AI component is correctly configured and can process images with structured output.");
    }
}
