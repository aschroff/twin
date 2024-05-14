using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private FileManager fileManager;
    [SerializeField] private GameObject newNameOrigin;
    private TwinNameValidator nameValidator;
    public void newConfig()
    {
        Text origin = newNameOrigin.GetComponent<Text>();

        GameObject NewTwin = this.transform.parent.Find("New").gameObject;
        nameValidator = NewTwin.GetComponentInChildren<TwinNameValidator>();
     
        dataPersistenceManager.createNewConfig(origin.text);
        origin.transform.parent.gameObject.GetComponent<InputField>().text = "TwinName";

        //textbox should be cleared
        dataPersistenceManager.SaveConfig();
        fileManager.Refresh();
        InteractionController.EnableMode("Main");
    }
    
    public void saveAsConfig()
    {
        Text origin = newNameOrigin.GetComponent<Text>();
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
