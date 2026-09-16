using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Code.AI
{
    /// <summary>
    /// Where the OpenAI key comes from, so it does not have to sit on a component in the scene.
    ///
    /// A key typed into the AI component is serialized into Maker Main.unity and travels into the
    /// repository with it - which has happened, and meant invalidating the key afterwards. Leave
    /// that field empty and put the key in one of the places below instead; all of them are
    /// outside version control.
    ///
    /// Looked up in this order, first hit wins:
    ///   1. the environment variable OPENAI_API_KEY
    ///   2. secrets.json next to the twins, in the persistent data path - works on a device
    ///   3. in the editor only: Assets/Tests/Helper/testsecrets.json, the file the tests already
    ///      use (git-ignored), so a developer keeps exactly one copy of the key
    ///   4. Assets/Resources/secrets.json - git-ignored as well, but part of the build, so a
    ///      tester's device needs nothing put on it by hand. This is how a build gets its key.
    ///   5. whatever the component itself carries - only so nothing breaks for a scene that still
    ///      has one
    ///
    /// The json files are read as { "openAIApiKey": "sk-..." } - the member testsecrets.json uses.
    /// The name is matched without regard to case, and "apiKey" / "openai_api_key" work too, so a
    /// hand-written file cannot miss by a capital letter.
    /// </summary>
    public static class ApiKeys
    {
        public const string EnvironmentVariable = "OPENAI_API_KEY";

        /// <summary>Member the key is read from - the one testsecrets.json uses.</summary>
        public const string JsonMember = "openAIApiKey";

        /// <summary>Spellings of that member accepted in a json file, lower case.</summary>
        private static readonly string[] KeyMembers = { "openaiapikey", "apikey", "openai_api_key" };

        /// <summary>Name of the file to drop next to the twins on a device.</summary>
        public const string DeviceFileName = "secrets.json";

        /// <summary>The file the tests use, relative to Application.dataPath. Editor only - the
        /// Assets folder does not exist in a build.</summary>
        public const string EditorFile = "Tests/Helper/testsecrets.json";

        /// <summary>
        /// Resources name of the key file that travels inside the build.
        /// </summary>
        /// <remarks>
        /// <para><c>Assets/Resources/secrets.json</c>, git-ignored like the other two - so it
        /// cannot reach the repository by someone forgetting to take it out again, which is what
        /// kept happening while the key lived on the AI component in the scene.</para>
        ///
        /// <para>Unlike the other two it is <b>part of the build</b>, so a tester's device gets a
        /// key without anybody putting a file on it. The trade is that the key then sits inside
        /// the app and can be read out of it: use a key of its own with a spending limit.</para>
        /// </remarks>
        public const string ResourceName = "secrets";

        /// <summary>
        /// The key to use, or <paramref name="fromComponent"/> when none of the places outside
        /// version control has one. Empty when there is no key anywhere.
        /// </summary>
        public static string OpenAi(string fromComponent = null)
        {
            string fromEnvironment = FromEnvironment();
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                return fromEnvironment;
            }

            foreach (string path in FilePaths())
            {
                string fromFile = ReadFromFile(path);
                if (!string.IsNullOrEmpty(fromFile))
                {
                    return fromFile;
                }
            }

            string fromBuild = FromResources();
            if (!string.IsNullOrEmpty(fromBuild))
            {
                return fromBuild;
            }

            return fromComponent != null ? fromComponent.Trim() : "";
        }

        /// <summary>The places that were searched, for an error message worth reading.</summary>
        public static string SearchedPlaces()
        {
            var places = new List<string> { "the environment variable " + EnvironmentVariable };
            places.AddRange(FilePaths());
            places.Add("Assets/Resources/" + ResourceName + ".json");
            return string.Join(", ", places.ToArray());
        }

        /// <summary>The key in a json file, or empty when the file is missing or has none.</summary>
        public static string ReadFromFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return "";
            }

            try
            {
                return KeyFromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApiKeys] '{path}' could not be read: {e.Message}");
                return "";
            }
        }

        /// <summary>The key inside a json text, or empty when it holds none. Every json source
        /// goes through this, so they all accept exactly the same spellings.</summary>
        public static string KeyFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return "";
            }

            foreach (var member in JObject.Parse(json))
            {
                if (System.Array.IndexOf(KeyMembers, member.Key.ToLowerInvariant()) < 0) continue;
                if (member.Value == null) continue;

                string key = member.Value.ToString().Trim();
                if (!string.IsNullOrEmpty(key)) return key;
            }
            return "";
        }

        /// <summary>The key that travels inside the build - see <see cref="ResourceName"/>.</summary>
        public static string FromResources()
        {
            try
            {
                TextAsset asset = Resources.Load<TextAsset>(ResourceName);
                return asset != null ? KeyFromJson(asset.text) : "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ApiKeys] Resources/{ResourceName} could not be read: {e.Message}");
                return "";
            }
        }

        private static string FromEnvironment()
        {
            try
            {
                string value = Environment.GetEnvironmentVariable(EnvironmentVariable);
                return value != null ? value.Trim() : "";
            }
            catch (Exception)
            {
                // some platforms do not let a player read the environment
                return "";
            }
        }

        private static List<string> FilePaths()
        {
            var paths = new List<string> { Path.Combine(DataPaths.PersistentDataPath, DeviceFileName) };
            if (Application.isEditor)
            {
                paths.Add(Path.Combine(Application.dataPath, EditorFile));
            }
            return paths;
        }
    }
}
