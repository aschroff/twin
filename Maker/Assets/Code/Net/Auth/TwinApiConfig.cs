using System;
using UnityEngine;

namespace Code.Net.Auth
{
    /// <summary>
    /// Where the API lives, and how long we wait for it.
    /// </summary>
    /// <remarks>
    /// A ScriptableObject rather than constants, so the environment is switched
    /// in the Inspector without a recompile, and so a build can ship pointing at
    /// a different host than the editor does.
    ///
    /// Create one via <c>Assets &gt; Create &gt; Twin &gt; API Config</c> and put it in a
    /// <c>Resources</c> folder if you want <see cref="LoadDefault"/> to find it.
    ///
    /// Nothing secret belongs in here. Anything in a ScriptableObject ships
    /// inside the build and can be read out of it — the only credentials in this
    /// system are the ones a person types into the login form.
    /// </remarks>
    [CreateAssetMenu(fileName = "TwinApiConfig", menuName = "Twin/API Config")]
    public class TwinApiConfig : ScriptableObject
    {
        /// <summary>Resources path <see cref="LoadDefault"/> looks at.</summary>
        public const string DefaultResourcePath = "TwinApiConfig";

        [Header("Server")]
        [Tooltip("Origin only — no trailing slash, no /v1. The client appends the path.\n\n" +
                 "Backend running natively:  http://localhost:8000\n" +
                 "Backend deployed to k3d:   http://localhost\n" +
                 "AWS dev / prod:            not decided yet")]
        [SerializeField]
        private string baseUrl = "http://localhost:8000";

        [Header("Timeouts")]
        [Tooltip("Seconds before a request is abandoned. Login runs argon2 on the " +
                 "server and is deliberately slow — do not tune this below ~10s.")]
        [SerializeField]
        [Range(5, 120)]
        private int timeoutSeconds = 30;

        [Header("Diagnostics")]
        [Tooltip("Log every request and its status code. Never logs tokens or passwords.")]
        [SerializeField]
        private bool verboseLogging;

        public string BaseUrl => NormaliseBaseUrl(baseUrl);
        public int TimeoutSeconds => timeoutSeconds;
        public bool VerboseLogging => verboseLogging;

        /// <summary>
        /// Build a config in code — for tests, or for reading the host from a
        /// command-line argument in a CI build.
        /// </summary>
        public static TwinApiConfig Create(string baseUrl, int timeoutSeconds = 30, bool verboseLogging = false)
        {
            var config = CreateInstance<TwinApiConfig>();
            config.baseUrl = baseUrl;
            config.timeoutSeconds = timeoutSeconds;
            config.verboseLogging = verboseLogging;
            return config;
        }

        /// <summary>
        /// Load the config from <c>Resources/TwinApiConfig</c>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// When no asset exists there. Deliberately an exception rather than a
        /// silent fallback to localhost: a build that quietly talks to a machine
        /// that is not there is harder to diagnose than one that refuses to start.
        /// </exception>
        public static TwinApiConfig LoadDefault()
        {
            var config = Resources.Load<TwinApiConfig>(DefaultResourcePath);
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"No TwinApiConfig found at Resources/{DefaultResourcePath}. " +
                    "Create one via Assets > Create > Twin > API Config and place it in a Resources folder, " +
                    "or pass a config to the client explicitly.");
            }

            return config;
        }

        /// <summary>
        /// Strip trailing slashes, so <c>http://host/</c> and <c>http://host</c>
        /// both produce <c>http://host/v1/auth/login</c> rather than a double slash.
        /// </summary>
        private static string NormaliseBaseUrl(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');
        }

        private void OnValidate()
        {
            // Caught in the Inspector rather than at the first request, where it
            // would surface as an opaque connection error.
            if (!string.IsNullOrWhiteSpace(baseUrl) && baseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.Ordinal))
            {
                Debug.LogWarning(
                    $"[{nameof(TwinApiConfig)}] baseUrl should be the origin only. " +
                    "The client appends /v1 itself, so this will produce /v1/v1/auth/login.", this);
            }
        }
    }
}
