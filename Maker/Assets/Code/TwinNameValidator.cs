using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

public class TwinNameValidator : MonoBehaviour
{ 
    [SerializeField] private DataPersistenceManager dataPersistenceManager;

    private InputField nameInputField;
    private static readonly string nameRegex = "^[a-zA-Z0-9_()-]{1,11}$";
    //Regex which only allows string that are made out of the characters a-z, A-Z, 0-9 and "_", "(", ")","-"
    //with the minimum length of 1 and the maximum length of 14


    private void Start()
    {   
        nameInputField = this.GetComponent<InputField>();
        nameInputField.onValueChanged.AddListener(ValidateInput);
        nameInputField.onEndEdit.AddListener(ValidateInput);
    }

    public void ValidateInput(string input)
    {
        EnableButtons(false);
        if (Regex.IsMatch(input, nameRegex) && !dataPersistenceManager.ExistsProfileId(input))
        {
            //input matches regex
            DisplayErrorMessage("");
            EnableButtons(true);
            Debug.Log("InputValid: " + input + ", Button interactable.");
            //enable the button
        }
        else if (dataPersistenceManager.ExistsProfileId(input) && !input.Contains("."))
        {
            //dot is excluded because of ".", ".." and ".DS_Store" (shown when "ls -a" in folder: Maker)
            //input is already a foldername
           EnableButtons(false);
           DisplayErrorMessage("This twin name is already taken.");
           Debug.Log("ExistingFolderName: " + input); 
            
        }
        else
        {
            EnableButtons(false);
            DisplayErrorMessage("Twin name can only contain following characters: a-z, A-Z, 0-9 and \"_\", \"(\", \")\",\" - \". With length between 1 and 14.");
            Debug.Log("InvalidUserInput: " + input);
        }
    }

    public void ValidateInput()
    {
        string input = nameInputField.text;
        ValidateInput(input);
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