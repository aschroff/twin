using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Base class for all tests providing access to test secrets.
/// Secrets are loaded from Assets/Tests/testsecrets.json which is .gitignored.
///
/// Setup:
/// 1. Copy Assets/Tests/testsecrets.example.json to Assets/Tests/testsecrets.json
/// 2. Fill in your actual API keys and credentials
/// 3. testsecrets.json is already in .gitignore and won't be committed
/// </summary>
public abstract class TestBase
{
    private static TestSecrets _secrets;
    private static bool _secretsLoaded = false;

    /// <summary>
    /// Test secrets data structure
    /// </summary>
    [Serializable]
    private class TestSecrets
    {
        [JsonProperty("openAIApiKey")]
        public string OpenAIApiKey { get; set; }

        [JsonProperty("meshcapadeUser")]
        public string MeshcapadeUser { get; set; }

        [JsonProperty("meshcapadePassword")]
        public string MeshcapadePassword { get; set; }
    }

    /// <summary>
    /// Load secrets from testsecrets.json file
    /// </summary>
    private static void LoadSecrets()
    {
        if (_secretsLoaded)
        {
            return;
        }

        _secretsLoaded = true;

        var secretsPath = Path.Combine(Application.dataPath, "Tests", "Helper", "testsecrets.json");

        if (!File.Exists(secretsPath))
        {
            Debug.LogWarning(
                $"Test secrets file not found at: {secretsPath}\n" +
                "Copy Assets/Tests/Helper/testsecrets.example.json to Assets/Tests/Helper/testsecrets.json and fill in your credentials.\n" +
                "Tests requiring secrets will be skipped."
            );
            _secrets = new TestSecrets();
            return;
        }

        try
        {
            var json = File.ReadAllText(secretsPath);
            _secrets = JsonConvert.DeserializeObject<TestSecrets>(json);
            Debug.Log("Test secrets loaded successfully from testsecrets.json");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to load test secrets from {secretsPath}: {ex.Message}");
            _secrets = new TestSecrets();
        }
    }

    /// <summary>
    /// OpenAI API key for testing AI functionality.
    /// Returns null if not configured.
    /// </summary>
    protected static string OpenAIApiKey
    {
        get
        {
            LoadSecrets();
            return _secrets?.OpenAIApiKey;
        }
    }

    /// <summary>
    /// Meshcapade username for testing Meshcapade integration.
    /// Returns null if not configured.
    /// </summary>
    protected static string MeshcapadeUser
    {
        get
        {
            LoadSecrets();
            return _secrets?.MeshcapadeUser;
        }
    }

    /// <summary>
    /// Meshcapade password for testing Meshcapade integration.
    /// Returns null if not configured.
    /// </summary>
    protected static string MeshcapadePassword
    {
        get
        {
            LoadSecrets();
            return _secrets?.MeshcapadePassword;
        }
    }

    /// <summary>
    /// Check if OpenAI API key is configured.
    /// </summary>
    protected static bool HasOpenAIApiKey => !string.IsNullOrEmpty(OpenAIApiKey);

    /// <summary>
    /// Check if Meshcapade credentials are configured.
    /// </summary>
    protected static bool HasMeshcapadeCredentials =>
        !string.IsNullOrEmpty(MeshcapadeUser) && !string.IsNullOrEmpty(MeshcapadePassword);
}
