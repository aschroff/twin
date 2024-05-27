using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private FileManager fileManager;
    [SerializeField] private GameObject inputField;

    public void newConfig()
    {
        string newTwinNameFromInput = GetTwinNameFromInput();
        if (CheckInputTwinName(newTwinNameFromInput))
        {
            dataPersistenceManager.createNewConfig(newTwinNameFromInput);
            inputField.transform.Find("Text").GetComponent<Text>().text = "TwinName";
            //textbox should be cleared
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
        } else {
            Debug.Log("Twin-Name-Test of New-button push failed!");
        }
        
    }
    
    public void saveAsConfig()
    {
        string newTwinNameFromInput = GetTwinNameFromInput();
        if (CheckInputTwinName(newTwinNameFromInput))
        {
            //Text origin = inputField.GetComponent<Text>();
            dataPersistenceManager.saveAsConfig(newTwinNameFromInput);
            inputField.transform.parent.gameObject.GetComponent<InputField>().text = "TwinName";
            //textbox should be cleared
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
        } else {
            Debug.Log("Twin-Name-Test of saveAs-button push failed!");
        }
    }
    
    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        fileManager.Refresh();
        InteractionController.EnableMode("Main");
    }

    private string GetTwinNameFromInput() {
        Transform Text = inputField.transform.Find("Text");
        Text origin = Text.GetComponent<Text>();
        Debug.Log("Text from Input: " + origin.text.ToString() + ".");//delete after Testing

        return origin.text.ToString();
    }

    private bool CheckInputTwinName(string inputForTwinName) {
        // Here I want to acces the the component from the input field
        // this component twinNameValidator 
        TwinNameValidator nameValidator = inputField.GetComponent<TwinNameValidator>();
        return nameValidator.CheckInput(inputForTwinName);
    }

}
