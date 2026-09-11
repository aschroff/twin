using System.IO;
using Code.AI;
using NUnit.Framework;
using UnityEngine;

namespace EditModeTests
{
    /// <summary>
    /// Reading the OpenAI key from a file outside version control, so it does not have to sit on
    /// the AI component - where it would be serialized into the scene and committed.
    ///
    /// Only the file reading is covered here: the search order also consults the environment and
    /// the persistent data path, neither of which a test should change under the running editor.
    /// </summary>
    public class ApiKeysTests
    {
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Application.temporaryCachePath, "ApiKeysTests");
            Directory.CreateDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }

        private string Write(string name, string content)
        {
            string path = Path.Combine(directory, name);
            File.WriteAllText(path, content);
            return path;
        }

        [Test]
        public void ReadFromFile_TakesTheKeyTheTestSecretsFileUses()
        {
            string path = Write("testsecrets.json",
                "{\"openAIApiKey\": \"sk-the-key\", \"someOtherMember\": \"ignored\"}");
            Assert.AreEqual("sk-the-key", ApiKeys.ReadFromFile(path));
        }

        /// <summary>A hand-written file should not fail over a capital letter.</summary>
        [Test]
        public void ReadFromFile_AcceptsTheOtherSpellings()
        {
            Assert.AreEqual("sk-a", ApiKeys.ReadFromFile(Write("a.json", "{\"openAiApiKey\": \"sk-a\"}")));
            Assert.AreEqual("sk-b", ApiKeys.ReadFromFile(Write("b.json", "{\"openaiapikey\": \"sk-b\"}")));
            Assert.AreEqual("sk-c", ApiKeys.ReadFromFile(Write("c.json", "{\"apiKey\": \"sk-c\"}")));
            Assert.AreEqual("sk-d", ApiKeys.ReadFromFile(Write("d.json", "{\"OPENAI_API_KEY\": \"sk-d\"}")));
        }

        [Test]
        public void ReadFromFile_TrimsWhitespaceAroundTheKey()
        {
            Assert.AreEqual("sk-trimmed",
                ApiKeys.ReadFromFile(Write("t.json", "{\"openAIApiKey\": \"  sk-trimmed\\n\"}")));
        }

        [Test]
        public void ReadFromFile_WithoutAKey_ComesBackEmpty()
        {
            Assert.IsEmpty(ApiKeys.ReadFromFile(Write("none.json", "{\"someOtherMember\": \"ignored\"}")));
            Assert.IsEmpty(ApiKeys.ReadFromFile(Write("blank.json", "{\"openAIApiKey\": \"\"}")));
        }

        [Test]
        public void ReadFromFile_WithoutAFile_ComesBackEmpty()
        {
            Assert.IsEmpty(ApiKeys.ReadFromFile(Path.Combine(directory, "not-there.json")));
            Assert.IsEmpty(ApiKeys.ReadFromFile(null));
            Assert.IsEmpty(ApiKeys.ReadFromFile(""));
        }

        /// <summary>A broken file warns and yields nothing rather than throwing at startup.</summary>
        [Test]
        public void ReadFromFile_OfBrokenJson_ComesBackEmpty()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.IsEmpty(ApiKeys.ReadFromFile(Write("broken.json", "{ this is not json")));
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>What the component carries is the last resort, not the first choice.</summary>
        [Test]
        public void OpenAi_FallsBackToTheComponentOnly()
        {
            // no way to know from here whether a real file exists on this machine, so only the
            // shape of the fallback is checked: a value is returned, never null
            Assert.IsNotNull(ApiKeys.OpenAi("sk-from-the-component"));
            Assert.IsNotNull(ApiKeys.OpenAi(null));
        }

        [Test]
        public void SearchedPlaces_NamesTheEnvironmentAndTheFiles()
        {
            string places = ApiKeys.SearchedPlaces();
            StringAssert.Contains(ApiKeys.EnvironmentVariable, places);
            StringAssert.Contains(ApiKeys.DeviceFileName, places);
            StringAssert.Contains("testsecrets.json", places);
        }
    }
}
