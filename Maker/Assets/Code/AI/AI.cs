using System.Collections;
using System.Collections.Generic;
using System.IO;
using AiToolbox;
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
        public ChatGptParameters parameters;

        [Space]
        public Text characterDescription;

        public string path;
        public SettingsManager settingsManager;
        private List<(string, string)> prompts; 
    
        private ChatGptParameters _parametersWithCharacterRole;

        private void Start() {
            // Check if the API Key is set in the Inspector, just in case.
            if (parameters == null || string.IsNullOrEmpty(parameters.apiKey)) {
                const string errorMessage = "Please set the <b>API Key</b> in the <b>ChatGPT Dialogue</b> Game Object.";
                characterDescription.text = errorMessage;
                characterDescription.color = Color.magenta;
            }
        
        }

        public void OnEnable()
        {
            prompts = settingsManager.getPrompts();
        }

        private string getPromptOfLabel(string label)
        {
            foreach ((string, string) tupel in prompts)
            {
                if (tupel.Item1 == label)
                {
                    return tupel.Item2;
                }
            }
            return "";
        }
        
        private ItemPrompt getPromptResultOfLabel(string label)
        {
            return settingsManager.getPromptObject(label);
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
            string prompt = getPromptOfLabel(variant);
            
            prompt += Part.Description(part);


            if (!string.IsNullOrEmpty(part.pathScreenshot))
            {
                yield return StartCoroutine(WaitForFileCoroutine(part.pathScreenshot));
                prompt += "#IMAGEPATH#" + part.pathScreenshot + "#IMAGEPATHEND#";
            }

            ChatGPTwin.Request(prompt, parameters, completeCallback: text =>
            {
                part.description = text;
                Debug.Log("AI response " + part.meaning + " :" + text);
                characterDescription.text += "---------------------------------------------------\n";
                characterDescription.text += text + "\n";
            }, failureCallback: (errorCode, errorMessage) =>
            {
                var errorType = (ErrorCodes)errorCode;
                part.description = $"Error {errorCode}: {errorType} - {errorMessage}";
            });
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
            string prompt = getPromptOfLabel(variant);
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
     
        private void OneShot(string prompt, string variant, string imagePath="")
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                prompt +=  "#IMAGEPATH#" + imagePath + "#IMAGEPATHEND#";              
            }

            ItemPrompt itemPrompt = getPromptResultOfLabel(variant);
            characterDescription.text = "<in progress> ";
            ChatGPTwin.Request(prompt, parameters, completeCallback: text => {
                characterDescription.text = text;
                itemPrompt.promptResult = text;
            }, failureCallback: (errorCode, errorMessage) => {
                var errorType = (ErrorCodes)errorCode;
                characterDescription.text = $"Error {errorCode}: {errorType} - {errorMessage}";
                characterDescription.color = Color.red;
            });
        }
        
        
        
        
    }
}
