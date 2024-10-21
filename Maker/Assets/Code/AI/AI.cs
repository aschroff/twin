using System.Collections.Generic;
using AiToolbox;
using UnityEngine;
using UnityEngine.UI;

public class AI : MonoBehaviour {
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
        const string prompt = "Produce a text 100-120 word. The person is 1.60 m tall. Describe the medical findings depicted on the body. Colour red represents deep aching pain, blue represents heavy tired legs and mint is for sharp shooting pain.";
        OneShot(prompt, path);

    }
    
    public void CompleteReport()
    {
        const string prompt = "The person is 1.60 m tall. Describe the medical findings depicted on the body and make a recommendation for treating these problems. Colour red represents deep aching pain, blue represents heavy tired legs and mint is for sharp shooting pain.";
        OneShot(prompt, path);

    }
   
    public void Treatment()
    {
        const string prompt = "The person is 1.60 m tall. Describe in 50-70 words the medical findings depicted on the body and make a 200 -250 words recommendation for treating these problems. Colour red represents deep aching pain, blue represents heavy tired legs and mint is for sharp shooting pain.";
        OneShot(prompt, path);
    }
    
    private void OneShot(string prompt, string imagePath)
    {
        string promptWithImage = prompt + "#IMAGEPATH#" + imagePath + "#IMAGEPATHEND#";
        ChatGPTwin.Request(promptWithImage, parameters, completeCallback: text => {
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
