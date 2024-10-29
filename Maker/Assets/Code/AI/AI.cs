using System.Collections.Generic;
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
    
        private ChatGptParameters _parametersWithCharacterRole;

        private void Start() {
            // Check if the API Key is set in the Inspector, just in case.
            if (parameters == null || string.IsNullOrEmpty(parameters.apiKey)) {
                const string errorMessage = "Please set the <b>API Key</b> in the <b>ChatGPT Dialogue</b> Game Object.";
                characterDescription.text = errorMessage;
                characterDescription.color = Color.magenta;
                return;
            }
        
        }
        public void Summary()
        {
            string prompt = "Produce a text 100-120 word. The person is 1.60 m tall. Describe the medical findings depicted on the body.";
            prompt += PromptGeneration.PromptContributor.GeneratePrompt(Help.ShortSummary);
            OneShot(prompt, path);

        }
    
        public void CompleteReport()
        {
            string prompt = "The person is 1.60 m tall. Describe the medical findings depicted on the body and make a recommendation for treating these problems.";
            prompt += PromptGeneration.PromptContributor.GeneratePrompt(Help.CompleteReport);
            OneShot(prompt, path);

        }
   
        public void Treatment()
        {
            string prompt = "The person is 1.60 m tall. Describe in 50-70 words the medical findings depicted on the body and make a 200 -250 words recommendation for treating these problems.";
            prompt += PromptGeneration.PromptContributor.GeneratePrompt(Help.Treatment);
            OneShot(prompt, path);
        }
        
        public void Situation(string language)
        {

            string prompt = "Send back the following text ";
            if (language != "English")
            {
                prompt += " translated into " + language +", without any further changes: \n";
            }
            else
            {
                prompt += ", without any further changes except for correction of spelling mistakes: \n";
            }
            prompt += PromptGeneration.PromptContributor.GeneratePrompt(Help.Situation);
            OneShot(prompt);
        }
        
        public string DescribePart(PartManager.PartData part)
        {
            string prompt = "Send back the following text ";
            
            prompt += Part.Chapter(part);
            if (!string.IsNullOrEmpty(path))
            {
                prompt +=  "#IMAGEPATH#" + path + "#IMAGEPATHEND#";              
            }
            ChatGPTwin.Request(prompt, parameters, completeCallback: text => {
                // We've received the full text of the answer, so we can display it in the "You're chatting with" text.
                part.description = text;
            }, failureCallback: (errorCode, errorMessage) => {
                // If the request fails, display the error message in the "You're chatting with" text.
                var errorType = (ErrorCodes)errorCode;
                part.description = $"Error {errorCode}: {errorType} - {errorMessage}";
            });
            return "";
        }
     
        private void OneShot(string prompt, string imagePath="")
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                prompt +=  "#IMAGEPATH#" + imagePath + "#IMAGEPATHEND#";              
            }

            characterDescription.text = "<in progress> ";
            ChatGPTwin.Request(prompt, parameters, completeCallback: text => {
                // We've received the full text of the answer, so we can display it in the "You're chatting with" text.
                characterDescription.text = text;
            }, failureCallback: (errorCode, errorMessage) => {
                // If the request fails, display the error message in the "You're chatting with" text.
                var errorType = (ErrorCodes)errorCode;
                characterDescription.text = $"Error {errorCode}: {errorType} - {errorMessage}";
                characterDescription.color = Color.red;
            });
        }
        
        
        
        
    }
}
