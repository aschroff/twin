using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Lean.Gui;

public class TwinNameValidator : MonoBehaviour
{ 
    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private LeanPulse notification;

    private InputField nameInputField;
    private static readonly string nameRegex = "^[a-zA-Z0-9_()-]{1,11}$";
    //Regex which only allows string that are made out of the characters a-z, A-Z, 0-9 and "_", "(", ")","-"
    //with the minimum length of 1 and the maximum length of 14


    private void Start()
    {   
        nameInputField = this.GetComponent<InputField>();
    }

    public bool validInput(string twinNameWithVersion) {
        if (twinNameWithVersion.Count(t => t == '.') != 1) {
            //there has to be exactly one dot in the name - between the twinName and its version
            DisplayErrorMessage("Twin name can only contain following characters: a-z, A-Z, 0-9 and \"_\", \"(\", \")\",\" - \". With length between 1 and 14.");
            return false;
        }
        string[] twinName = twinNameWithVersion.Split('.');

        if (dataPersistenceManager.ExistsProfileId(twinName[0]))
            {
                //if there is already a twin with this name - the version of this twin should be checked
                if (twinName[1] != "000")
                {
                    DisplayErrorMessage("Twin name can only contain following characters: a-z, A-Z, 0-9 and \"_\", \"(\", \")\",\" - \". With length between 1 and 14.");
                    return false;
                } else {
                //if twinName already exists we know that this twins name is valid
                    return true;
                }
            }
            else {
            //if twin Name did not already exists it has to be checked!
            if (!CheckInput(twinName[0]))
            {
                DisplayErrorMessage("Twin name can only contain following characters: a-z, A-Z, 0-9 and \"_\", \"(\", \")\",\" - \". With length between 1 and 14.");
                return false;
            }
            else {
                return true;
            }
        }
    }

    public void ValidateInput()
    {
        string input = nameInputField.text;
        validInput(input);
    }

    public bool CheckInput(string input)
    {
        //return true;
        return Regex.IsMatch(input, nameRegex) /*&& !dataPersistenceManager.ExistsProfileId(input)*/;
        // regex is white page regex so it accepts exactly when the name meets the naming conditions
    }

    private void DisplayErrorMessage(string errorMessage) {
        string message = errorMessage;

        foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
            // in case there are more text boxes we write the error Message in every of them
        {
            text.text = message;
        }
        notification.Pulse();
    }
}