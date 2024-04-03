using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

    //invalid fodername characters are colon(":"), the dot(".") at the start of the word(".*"),
    // the maximum input length is 255 characters and it is not allowed to be empty or named after an extisting folder
public class TwinNameValidator : MonoBehaviour
{ 
    [SerializeField] private DataPersistenceManager dataPersistenceManager;

    private InputField nameInputField;
    private static readonly string nameRegex = "^(?!.*:)(?!^\\.)(?!^\\s*$)[^\\n\\r]{1,255}$";


    private void Start()
    {   
        nameInputField = this.GetComponent<InputField>();
        nameInputField.onValueChanged.AddListener(ValidateInput);
    }

    private void ValidateInput(string input)
    {
        EnableButtons(false);
        if (!Regex.IsMatch(input, nameRegex))
        {
            EnableButtons(false);
            char invalidingInput = ParseInvalidCharacter(input);
            string errorMessage = CreateErrorMessage(invalidingInput);
            DisplayErrorMessage(errorMessage);
            Debug.Log("InvalidUserInput: " + input);
        }
        else if (dataPersistenceManager.ExistsProfileId(input))
        {
            EnableButtons(false);
            DisplayErrorMessage("This twin name is already taken.");
            Debug.Log("ExistingFolderName: " + input);
        }
        else
        {
            DisplayErrorMessage("");
            EnableButtons(true);
            Debug.Log("InputValid: " + input + ", Button interactable.");
            //enable the button
        }
    }

    private char ParseInvalidCharacter(string input) {
        if (input.Contains(":"))
        {
            return ':';
        }
        else if (string.IsNullOrWhiteSpace(input))
        {
            return ' ';
        }
        else if (input[0] == '.')
        {
            return '.';
        }
        else
        {
            return 'l'; //l for too long
        }
    }

    private string CreateErrorMessage(char invalidingWordPart) {
        switch (invalidingWordPart) {
            case ':':
                return "Colons(:) within the name of the twin is not allowed.";
            case '.':
                return "A dot(.) at the start of the name of the twin is not allowed.";
            case ' ':
                return "The Name of the twin must not be empty or must not consits only of whitespaces.";
            case 'l':
                return "Your name of the twin is too long. Maximum is 255 chracters.";
            default:
                return "This is not a valid name for a new twin.";
        }
    }

    private void DisplayErrorMessage(string errorMessage) {
        GameObject errorMessageArea = this.transform.parent.Find("UserMessage").gameObject;
        Text errorMessageDisplay = errorMessageArea.GetComponent<Text>();
        errorMessageDisplay.text = errorMessage;
    }

    private void EnableButtons(bool isEnabled) {
        GameObject safeButtonObject = this.transform.parent.Find("Save as").gameObject;
        GameObject newButtonObject = this.transform.parent.Find("New").gameObject;
        Button safeTwinButton = safeButtonObject.GetComponent<Button>();
        Button newTwinButton = newButtonObject.GetComponent<Button>();
        if (isEnabled)
        {
            safeTwinButton.interactable = true;
            newTwinButton.interactable = true;
        }
        else {
            safeTwinButton.interactable = false;
            newTwinButton.interactable = false;
        }
    }
}