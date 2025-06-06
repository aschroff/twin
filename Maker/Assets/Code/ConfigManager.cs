using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private PartManager partManager;
    [SerializeField] private FileManager fileManager;
    [SerializeField] private GameObject inputField;
    [SerializeField] private GameObject inputFieldVersion;
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private LocalizedString dateFormat;

    /*
     * This methode is responsible, if the button newConfig is pressed in Twin Mode.
     */
    public void newConfigFromInput()
    {
        // getting newest input
        string newTwinNameFromInput = GetTwinNameFromInput();
        newConfig(newTwinNameFromInput);
    }

    public void cloneConfigFromInput()
    {
        string newTwinNameFromInput = GetTwinNameFromInput();
        saveAsConfig(newTwinNameFromInput);
    }

    /*
     * This methode is responsible, if the button New Version is pressed in MainUI or TurnUI
     */
    public void newConfigFromMainMenu()
    {
        //get time
        DateTime currentDate = DateTime.Now; // getting current Time to put into version input field

        // format the date to current localization settings
        LocalizedString formattedLocalizedString = DateFormatter.formatDate(currentDate, dateFormat);

        String twinAndVersion = dataPersistenceManager.selectedProfileId;
        string[] splitTwinName = twinAndVersion.Split('.');
        String twin = splitTwinName[0];
        twin += "." + formattedLocalizedString.GetLocalizedString();
        Debug.Log("Version from Input: " + formattedLocalizedString.GetLocalizedString() + ".");

        saveAsConfig(twin);

        //deleting parts of cloned twin
        foreach (PartManager.GroupData group in partManager.groups)
        {
            for(int i = group.groupParts.Count - 1; i >= 0; i--)
            {
                partManager.deletePart(group.groupParts[i]); 
            }
        }
        partManager.ClearRefreshAll();

    }


    /*
     * This method creates a complete new Twin
     */
    private void newConfig(string newTwinNameFromInput)
    {

        if (CheckInput(newTwinNameFromInput))
        {
            // check if the new input is a already existing folder
            dataPersistenceManager.createNewConfig(newTwinNameFromInput);      
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
        } else {
            Debug.Log("Twin-Name-Test of New-button push failed!");
        }
    }
    
    /*
     * This methode is responsible, if the button safeAs is pressed in Twin-Mode.
     * This Button creates a new Twin on the basis of another Twin.
     */
    public void saveAsConfig(string newTwinNameFromInput)
    {

        if (CheckInput(newTwinNameFromInput))
        { 
            // check if the new input is a already existing folder
            dataPersistenceManager.saveAsConfig(newTwinNameFromInput);
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
            viewManager.setDefaultPosition();
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

            //resets the input Field directly after reading it
            inputField.transform.GetComponent<InputField>().text = "TwinName";
        }

        if (inputFieldVersion != null)
        {
            twinName = fileManager.GetProfile();
            textVersion = inputFieldVersion.transform.Find("Text");
            originVersion = textVersion.GetComponent<Text>();
            twinName += "." + originVersion.text.ToString();
            Debug.Log("Version from Input: " + originVersion.text.ToString() + ".");
            
            //resets the input Field directly after reading it
            inputFieldVersion.transform.GetComponent<InputField>().text = this.GetComponentInChildren<TwinVersionSetter>().GetLastFormattedDate();          
        }
        return twinName;
    }

    private bool CheckInput(string inputNameWithVersion) {
        TwinNameValidator nameValidator;

        if (inputFieldVersion == null) {
            //we adress the text in the error message toast with inputField/inputFieldVersion so we have to distinguish here

            nameValidator = inputField.GetComponent<TwinNameValidator>();
        } else {
            nameValidator = inputFieldVersion.GetComponent<TwinNameValidator>();
        }

        return nameValidator.IsValidInput(inputNameWithVersion);
        //return true;
    }
}