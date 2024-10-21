using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private FileManager fileManager;
    [SerializeField] private GameObject inputField;
    [SerializeField] private GameObject inputFieldVersion;

    /*
     * This methode is responsible, if the button newConfig is pressed in Twin Mode.
     * This Button creates a complete new Twin
     */
    public void newConfig()
    {
        string newTwinNameFromInput = GetTwinNameFromInput();
        // getting newest input from the InputField
        if (CheckInputTwinName(newTwinNameFromInput)) { 
            // check if the new input is a already existing folder
            dataPersistenceManager.createNewConfig(newTwinNameFromInput);      
            inputField.transform.GetComponent<InputField>().text = "TwinName";  //resets the input Field 
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
        } else {
            Debug.Log("Twin-Name-Test of New-button push failed!");
            //This is the new-Button because is create a completely new Twin
        }
        
    }
    
    /*
     * This methode is responsible, if the button safeAs is pressed in Twin-Mode.
     * This Button creates a new Twin on the basis of another Twin.
     */
    public void saveAsConfig()
    {
        string newTwinNameFromInput = GetTwinNameFromInput();
        // getting newest input from the InputField
        if (CheckInputTwinName(newTwinNameFromInput)) {
            // check if the new input is a already existing folder
            dataPersistenceManager.saveAsConfig(newTwinNameFromInput);
            inputField.transform.GetComponent<InputField>().text = "TwinName";  //resets the input Field
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
        } else {
            Debug.Log("Twin-Name-Test of saveAs-button push failed!");
            //This is the safeAs-Button because is create a completely new Twin
        }
    }
    
    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        fileManager.Refresh();
        InteractionController.EnableMode("Main");
    }

    private string GetTwinNameFromInput()
    {
        Transform textName = null;
        Text originName = null;
        Transform textVersion = null;
        Text originVersion = null;
        string twinName = "";
        if (inputField == null) {
            Debug.Log("InputField is null");
        }
        else if (inputFieldVersion == null)
        {
            textName = inputField.transform.Find("Text");
            originName = textName.GetComponent<Text>();
            twinName = originName.text.ToString();
            Debug.Log("Name only.Name from Input: " + twinName + ".");
            twinName += "." + "000";
        }
        else
        {
            twinName = fileManager.GetProfile();
            textVersion = inputFieldVersion.transform.Find("Text");
            originVersion = textVersion.GetComponent<Text>();
            twinName += "." + originVersion.text.ToString();
            Debug.Log("Version from Input: " + originVersion.text.ToString() + ".");
        }
        return twinName;
    }

    private bool CheckInputTwinName(string inputForTwinName) {
        // Here I want to acces the the component from the input field
        // this component twinNameValidator 
        TwinNameValidator nameValidator = inputField.GetComponent<TwinNameValidator>();
        return nameValidator.CheckInput(inputForTwinName);
    }

}
