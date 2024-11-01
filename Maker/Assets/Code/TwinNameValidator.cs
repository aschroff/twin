using UnityEngine;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Lean.Gui;

public class TwinNameValidator : MonoBehaviour
{
    [SerializeField] private bool disableValidation;

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private LeanPulse notification;

    private InputField nameInputField;
    private static readonly string nameRegex = "^[a-zA-Z0-9_()-]{1,11}$";
    //Regex which only allows string that are made out of the characters a-z, A-Z, 0-9 and "_", "(", ")","-"
    //with the minimum length of 1 and the maximum length of 14
    // regex is white page regex so it accepts exactly when the name meets the naming conditions

    private static readonly string invalidNameMessge = "Twin name can only contain following characters: a-z, A-Z, 0-9 and \"_\", \"(\", \")\",\" - \". With length between 1 and 14.";
    private static readonly string alreadyExistingNameMessage = "This Twin Name already exists.";

    private void Start()
    {
            nameInputField = this.GetComponent<InputField>();
    }

    public bool IsValidInput(string twinNameWithVersion)
    {
        if (twinNameWithVersion.Count(t => t == '.') != 1 && !disableValidation)
        {
            //there has to be exactly one dot in the name - between the twinName and its version
            DisplayMessage(invalidNameMessge);
            return false;
        }
        string[] twinName = twinNameWithVersion.Split('.');

        if (dataPersistenceManager.ExistsProfileId(twinName[0] + ".000") && !disableValidation)
        {
            //if there is already a twin with this name - the version of this twin should be checked
            //it could be the case that we have only a new version of an already existing twin
            return IsValidVersion(twinName[1]);
        }
        else if (!disableValidation)
        {
            //if we want to disable the version validation, we dont want a validation on the name of the twin since
            // we dont want to be interrupter by the twin name check in times we work with the version validation
            return IsValidTwinName(twinName[0]);
        }
        else
        {
            Debug.LogWarning("Twin Name/Version Validation is currently disabled!");
            return true;
        }   
    }

    private bool IsValidTwinName(string twinName)
    {
        //if twin Name did not already exists it has to be checked!
        if (!Regex.IsMatch(twinName, nameRegex))
        {
            DisplayMessage(invalidNameMessge);
            return false;
        }
        else
        {
            return true;
        }
    }

    private bool IsValidVersion(string twinName)
    {
        if (twinName.Equals("000"))
        {
            // the Version is 000 so there was an attempt to create a twin with a name that already exists or the version was typed in manually which is
            // not allowed so nothing happens
            DisplayMessage(alreadyExistingNameMessage);
            return false;
        }
        else
        {
            //if the version is unlike 000 it had to be altered by the user or the code so is should be correct? (set criteria for version input)

            //!!!insert here actual check for valid version!!!
            return true;
        }
    }

    public void ValidateInput()
    {
        string input = nameInputField.text;
        IsValidInput(input);
    }

    private void DisplayMessage(string errorMessage) {
        string message = errorMessage;

        foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
            // in case there are more text boxes we write the error Message in every of them
        {
            text.text = message;
        }
        notification.Pulse();
    }
}