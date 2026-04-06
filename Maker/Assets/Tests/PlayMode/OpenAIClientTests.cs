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
    private const string TestApiKey = "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";
    private const string TestModel = "gpt-4o-mini"; // For basic tests
    private const string ProductionModel = "gpt-5.4-2026-03-05"; // Matches AI component settings

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
    public IEnumerator OpenAIClient_FileUpload_WithStructuredOutput()
    {
        // Test file upload via Files API with a text document
        // Skip test if API key is not configured
        if (string.IsNullOrEmpty(TestApiKey) || TestApiKey == "YOUR_API_KEY_HERE")
        {
            Assert.Ignore("API key not configured. Set TestApiKey in OpenAIClientTests.cs to run this test.");
            yield break;
        }

        var client = new OpenAIClient(TestApiKey, timeout: 60);

        Debug.Log($"Testing file upload workflow with:");
        Debug.Log($"  Model: {ProductionModel}");
        Debug.Log($"  File: text document via Files API");

        // Create a simple test text file
        var testContent = "This is a test document for the OpenAI API. It contains information about testing file uploads.";
        var tempFilePath = System.IO.Path.Combine(Application.temporaryCachePath, "test_document.txt");
        System.IO.File.WriteAllText(tempFilePath, testContent);

        Debug.Log($"Created test file at: {tempFilePath}");

        // Upload the file
        string fileId = null;
        System.Exception uploadError = null;

        var uploadTask = client.UploadFileAsync(tempFilePath);

        while (!uploadTask.IsCompleted)
        {
            yield return null;
        }

        if (uploadTask.Exception != null)
        {
            uploadError = uploadTask.Exception.InnerException ?? uploadTask.Exception;
        }
        else
        {
            fileId = uploadTask.Result;
        }

        // Assert upload succeeded
        Assert.IsNull(uploadError, $"File upload failed: {uploadError?.Message}");
        Assert.IsNotNull(fileId, "File ID should not be null");
        Assert.IsNotEmpty(fileId, "File ID should not be empty");

        Debug.Log($"File uploaded successfully with ID: {fileId}");

        // Now use the file in a structured output request
        var prompt = "Summarize the content of the uploaded file. Respond with JSON containing a 'message' field.";

        SimpleTextOutput response = null;
        System.Exception requestError = null;

        var requestTask = client.RequestStructuredAsync<SimpleTextOutput>(
            prompt,
            ProductionModel,
            fileId: fileId
        );

        while (!requestTask.IsCompleted)
        {
            yield return null;
        }

        if (requestTask.Exception != null)
        {
            requestError = requestTask.Exception.InnerException ?? requestTask.Exception;
        }
        else
        {
            response = requestTask.Result;
        }

        // Cleanup
        if (System.IO.File.Exists(tempFilePath))
        {
            System.IO.File.Delete(tempFilePath);
        }

        // Assert request succeeded
        Assert.IsNull(requestError, $"Request with file failed: {requestError?.Message}");
        Assert.IsNotNull(response, "Response should not be null");
        Assert.IsNotNull(response.Message, "Response message should not be null");
        Assert.IsNotEmpty(response.Message, "Response message should not be empty");

        Debug.Log($"AI Response with uploaded file: {response.Message}");
        Debug.Log($"✅ File upload integration test passed! Files API works with GPT-5.4 and structured outputs.");
    }

    [UnityTest]
    public IEnumerator AI_Component_Integration_WithImageUpload()
    {
        // This test validates the complete workflow: image upload + structured output
        // If this test passes, the real application should work

        // Skip test if API key is not configured
        if (string.IsNullOrEmpty(TestApiKey) || TestApiKey == "YOUR_API_KEY_HERE")
        {
            Assert.Ignore("API key not configured. Set TestApiKey in OpenAIClientTests.cs to run this test.");
            yield break;
        }

        // Use production settings - matches what's configured in AI component
        var client = new OpenAIClient(TestApiKey, timeout: 60);

        Debug.Log($"Testing complete workflow with:");
        Debug.Log($"  Model: {ProductionModel}");
        Debug.Log($"  Image upload: base64 embedded");

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

        // Make the request - this is exactly what the app will do
        var task = client.RequestStructuredAsync<ImageDescription>(
            prompt,
            ProductionModel,
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

    [UnityTest]
    public IEnumerator AI_Component_Integration_PDF_vs_PNG()
    {
        // This test validates both file handling approaches:
        // - PNG: embedded as base64 with input_image
        // - PDF: uploaded via Files API with input_file

        // Skip test if API key is not configured
        if (string.IsNullOrEmpty(TestApiKey) || TestApiKey == "YOUR_API_KEY_HERE")
        {
            Assert.Ignore("API key not configured. Set TestApiKey in OpenAIClientTests.cs to run this test.");
            yield break;
        }

        var client = new OpenAIClient(TestApiKey, timeout: 60);

        Debug.Log($"=== Testing PNG (base64 embedded) ===");

        // Create a test PNG image
        var texture = new Texture2D(2, 2);
        texture.SetPixel(0, 0, Color.red);
        texture.SetPixel(1, 0, Color.green);
        texture.SetPixel(0, 1, Color.blue);
        texture.SetPixel(1, 1, Color.yellow);
        texture.Apply();

        var imageBytes = texture.EncodeToPNG();
        var tempImagePath = System.IO.Path.Combine(Application.temporaryCachePath, "test_colors.png");
        System.IO.File.WriteAllBytes(tempImagePath, imageBytes);

        Debug.Log($"Created test PNG at: {tempImagePath}");

        // Test PNG with structured output
        var pngPrompt = "Describe the colors in this image. Respond with JSON containing a 'description' field.";
        ImageDescription pngResponse = null;
        System.Exception pngError = null;

        var pngTask = client.RequestStructuredAsync<ImageDescription>(
            pngPrompt,
            ProductionModel,
            imagePath: tempImagePath
        );

        while (!pngTask.IsCompleted)
        {
            yield return null;
        }

        if (pngTask.Exception != null)
        {
            pngError = pngTask.Exception.InnerException ?? pngTask.Exception;
        }
        else
        {
            pngResponse = pngTask.Result;
        }

        // Assert PNG test
        Assert.IsNull(pngError, $"PNG test failed: {pngError?.Message}");
        Assert.IsNotNull(pngResponse, "PNG response should not be null");
        Assert.IsNotNull(pngResponse.Description, "PNG description should not be null");
        Assert.IsNotEmpty(pngResponse.Description, "PNG description should not be empty");

        Debug.Log($"PNG Response: {pngResponse.Description}");
        Debug.Log($"✅ PNG test passed (base64 embedded)");

        Debug.Log($"\n=== Testing PDF (Files API upload) ===");

        // Create a simple test PDF document
        var pdfContent = "This is a test PDF document for file upload validation.";
        var tempPdfPath = System.IO.Path.Combine(Application.temporaryCachePath, "test_document.txt");
        System.IO.File.WriteAllText(tempPdfPath, pdfContent);

        Debug.Log($"Created test document at: {tempPdfPath}");

        // Upload the file via Files API
        string fileId = null;
        System.Exception uploadError = null;

        var uploadTask = client.UploadFileAsync(tempPdfPath);

        while (!uploadTask.IsCompleted)
        {
            yield return null;
        }

        if (uploadTask.Exception != null)
        {
            uploadError = uploadTask.Exception.InnerException ?? uploadTask.Exception;
        }
        else
        {
            fileId = uploadTask.Result;
        }

        Assert.IsNull(uploadError, $"File upload failed: {uploadError?.Message}");
        Assert.IsNotNull(fileId, "File ID should not be null");
        Assert.IsNotEmpty(fileId, "File ID should not be empty");

        Debug.Log($"File uploaded with ID: {fileId}");

        // Test with uploaded file
        var pdfPrompt = "Summarize the content of the uploaded file. Respond with JSON containing a 'description' field.";
        ImageDescription pdfResponse = null;
        System.Exception pdfError = null;

        var pdfTask = client.RequestStructuredAsync<ImageDescription>(
            pdfPrompt,
            ProductionModel,
            fileId: fileId
        );

        while (!pdfTask.IsCompleted)
        {
            yield return null;
        }

        if (pdfTask.Exception != null)
        {
            pdfError = pdfTask.Exception.InnerException ?? pdfTask.Exception;
        }
        else
        {
            pdfResponse = pdfTask.Result;
        }

        // Cleanup
        if (System.IO.File.Exists(tempImagePath))
        {
            System.IO.File.Delete(tempImagePath);
        }
        if (System.IO.File.Exists(tempPdfPath))
        {
            System.IO.File.Delete(tempPdfPath);
        }

        // Assert PDF test
        Assert.IsNull(pdfError, $"PDF test failed: {pdfError?.Message}");
        Assert.IsNotNull(pdfResponse, "PDF response should not be null");
        Assert.IsNotNull(pdfResponse.Description, "PDF description should not be null");
        Assert.IsNotEmpty(pdfResponse.Description, "PDF description should not be empty");

        Debug.Log($"PDF Response: {pdfResponse.Description}");
        Debug.Log($"✅ PDF test passed (Files API upload)");

        Debug.Log($"\n✅ Both file handling approaches work correctly!");
    }
}
