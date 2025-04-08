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
        
        
        
        private IEnumerator WaitForFileCoroutine(string path)
        {
            while (!File.Exists(path))
            {
                yield return new WaitForSeconds(0.5f); // Wait for 0.5 seconds
            }

            Debug.Log("File exists: " + path);
            // Continue with the rest of your logic here
        }
        
        
        private IEnumerator DescribePartCoroutine(PartManager.PartData part)
        {
            string prompt =
                "The person is 1.60 m tall. Describe the medical findings depicted on the body in the style of a medical report." +
                "Include the size and shape of the findings, their location on the body including the relative position on the body part and the orientation, and any other relevant details. The result should be approxiamtely 150 characters long. ";
            prompt += Part.Description(part);

            if (!string.IsNullOrEmpty(part.pathScreenshot))
            {
                yield return StartCoroutine(WaitForFileCoroutine(part.pathScreenshot));
                prompt += "#IMAGEPATH#" + part.pathScreenshot + "#IMAGEPATHEND#";
            }

            ChatGPTwin.Request(prompt, parameters, completeCallback: text => {
                part.description = text;
                Debug.Log("AI response " + part.meaning + " :" + text);
                characterDescription.text += "---------------------------------------------------\n";
                characterDescription.text += text + "\n";
            }, failureCallback: (errorCode, errorMessage) => {
                var errorType = (ErrorCodes)errorCode;
                part.description = $"Error {errorCode}: {errorType} - {errorMessage}";
            });
        }

        public string DescribePart(PartManager.PartData part)
        {
            StartCoroutine(DescribePartCoroutine(part));
            return "";
        }
        
        public void DescribeVersion(PartManager partManager)
        {
            string prompt = "Create a medical report with approximately 250 words. Herefore, describe each medical finding in two to three sentences, summarize the overall situation" +
                            "and recommended treatments. The medical findings depicted on the body are listed below: \n ";
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
            OneShot(prompt);
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
