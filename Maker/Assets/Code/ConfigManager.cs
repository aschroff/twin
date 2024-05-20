using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private FileManager fileManager;

    [SerializeField] private GameObject inputField;


    private TwinNameValidator nameValidator;
    public void newConfig()
    {
        //I want the children "Text" from the GameObject inputField
        // in this children Text I want the component Text to get acces to the origin of the name of the new twin
        //Text origin = this.transform.GetComponentInChildren()<Text>();
        Transform Text = inputField.transform.Find("Text");
        Text origin = Text.GetComponent<Text>();
        //Text origin = this.transform.Find("Text");
        Debug.Log("Text from Input: " + origin.text.ToString() + ".");

        // Here I want to acces the the component from the input field
        // this component twinNameValidator 
        nameValidator = inputField.GetComponent<TwinNameValidator>();

        // getting the name of the new created Twin

        //GameObject NewTwin = this.transform.parent.GetComponentInChildren<TwinNameValidator>().gameObject;
        //GameObject NewTwin = this.transform.parent.Find("New").gameObject;
        //nameValidator = NewTwin.GetComponentInChildren<TwinNameValidator>();
        // getting Component (TwinNameValidator)of Input Field to call CheckInput-Function

        if (nameValidator.CheckInput(origin.text.ToString())) {
            dataPersistenceManager.createNewConfig(origin.text);
            origin.transform.parent.gameObject.GetComponent<InputField>().text = "TwinName";
            //textbox should be cleared
            dataPersistenceManager.SaveConfig();
            fileManager.Refresh();
            InteractionController.EnableMode("Main");
        }
        
    }
    
    public void saveAsConfig()
    {
        Text origin = inputField.GetComponent<Text>();
        dataPersistenceManager.saveAsConfig(origin.text);
        origin.transform.parent.gameObject.GetComponent<InputField>().text = "TwinName";
        //textbox should be cleared
        dataPersistenceManager.SaveConfig();
        fileManager.Refresh();
        InteractionController.EnableMode("Main");
    }
    
    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        fileManager.Refresh();
        InteractionController.EnableMode("Main");
    }

}
