using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Code.AI.PromptGeneration;
using UnityEngine;
using UnityEngine.UI;

namespace Code.AI
{
    public class AI : MonoBehaviour {

        public enum Help
        {
            ShortSummary,
            Treatment,
            CompleteReport,
            Situation
        }

        public ISet<Help> Whatever;

        [Header("OpenAI Settings")]
        public string apiKey;
        public string model = "gpt-4o-mini";
        public int timeout = 120;

        [Space]
        public Text characterDescription;

        public string path;
        public SettingsManager settingsManager;

        private OpenAIClient _openAIClient;

        private void Start() {
            // Initialize OpenAI client
            if (string.IsNullOrEmpty(apiKey)) {
                const string errorMessage = "Please set the <b>API Key</b> in the AI component.";
                if (characterDescription != null) {
                    characterDescription.text = errorMessage;
                    characterDescription.color = Color.magenta;
                }
                Debug.LogError(errorMessage);
                return;
            }

            _openAIClient = new OpenAIClient(apiKey, timeout);
            Debug.Log("OpenAI client initialized successfully");
        }
        

        private string getPromptOfLabel(string label, ItemPrompt.PromptLevel level = ItemPrompt.PromptLevel.Unknown)
        {
            Debug.Log("getPromptOfLabel");
            Debug.Log(label);
            Debug.Log(level);
            return this.getPromptResultOfLabel(label, level).gameObject.GetComponentInChildren<InputField>().text;
        }
        
        private ItemPrompt getPromptResultOfLabel(string label, ItemPrompt.PromptLevel level = ItemPrompt.PromptLevel.Unknown)
        {
            return settingsManager.getPromptObject(label, level);
        }
        
        
        private IEnumerator WaitForFileCoroutine(string path)
        {
            while (!File.Exists(path))
            {
                yield return new WaitForSeconds(0.5f); // Wait for 0.5 seconds
            }

            Debug.Log("File exists: " + path);
        }
        
        
        private IEnumerator DescribePartCoroutine(PartManager.PartData part, string variant)
        {
            string prompt = getPromptOfLabel(variant, ItemPrompt.PromptLevel.Part);
            prompt += Part.Description(part);

            string imagePath = null;
            if (!string.IsNullOrEmpty(part.pathScreenshot))
            {
                yield return StartCoroutine(WaitForFileCoroutine(part.pathScreenshot));
                imagePath = part.pathScreenshot;
            }

            // Start async request
            var task = DescribePartAsync(part, prompt, imagePath);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Exception != null)
            {
                var errorMessage = task.Exception.InnerException?.Message ?? task.Exception.Message;
                part.description = $"Error: {errorMessage}";
                Debug.LogError($"AI request failed for {part.meaning}: {errorMessage}");
            }
        }

        private async Task DescribePartAsync(PartManager.PartData part, string prompt, string imagePath)
        {
            try
            {
                var response = await _openAIClient.RequestAsync(prompt, model, imagePath: imagePath);
                part.description = response;
                Debug.Log($"AI response for {part.meaning}: {response}");

                if (characterDescription != null)
                {
                    characterDescription.text += "---------------------------------------------------\n";
                    characterDescription.text += response + "\n";
                }
            }
            catch (OpenAIException ex)
            {
                throw new System.Exception($"OpenAI API error: {ex.Message}", ex);
            }
        }

        public string DescribePart(PartManager.PartData part, string variant)
        {
            StartCoroutine(DescribePartCoroutine(part, variant));
            return "";
        }
        
        public void CompleteReport()
        {
            string prompt = "The person is 1.60 m tall. Describe the medical findings depicted on the body and make a recommendation for treating these problems.";
            prompt += PromptGeneration.PromptContributor.GeneratePrompt(Help.CompleteReport);
            OneShot(prompt, path);

        }
        
        public void DescribeVersion(PartManager partManager,  string variant)
        {
            string prompt = getPromptOfLabel(variant, ItemPrompt.PromptLevel.Version);
            int countFinding = 0;
            foreach (PartManager.GroupData group in partManager.groups)
            {
                foreach (PartManager.PartData part in group.groupParts)
                {
                    countFinding++;
                    prompt += "Part Number " + countFinding + " :\n";
                    prompt += part.description + "\n";
                }
            }
            OneShot(prompt, variant);
        }
     
        private void OneShot(string prompt, string variant, string imagePath = "")
        {
            StartCoroutine(OneShotCoroutine(prompt, variant, imagePath));
        }

        private IEnumerator OneShotCoroutine(string prompt, string variant, string imagePath)
        {
            ItemPrompt itemPrompt = getPromptResultOfLabel(variant, ItemPrompt.PromptLevel.Version);

            if (characterDescription != null)
            {
                characterDescription.text = "<in progress> ";
            }

            var task = OneShotAsync(prompt, imagePath);
            yield return new WaitUntil(() => task.IsCompleted);

            if (task.Exception != null)
            {
                var errorMessage = task.Exception.InnerException?.Message ?? task.Exception.Message;
                if (characterDescription != null)
                {
                    characterDescription.text = $"Error: {errorMessage}";
                    characterDescription.color = Color.red;
                }
                Debug.LogError($"AI request failed: {errorMessage}");
            }
            else
            {
                var response = task.Result;
                if (characterDescription != null)
                {
                    characterDescription.text = response;
                }
                itemPrompt.promptResult = response;
            }
        }

        private async Task<string> OneShotAsync(string prompt, string imagePath)
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
                throw new System.Exception($"OpenAI API error: {ex.Message}", ex);
            }
        }
        
        
        
        
    }
}
