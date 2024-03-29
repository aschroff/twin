using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private FileManager fileManager;
    [SerializeField] private GameObject newNameOrigin;
    public void newConfig()
    {
        Text origin = newNameOrigin.GetComponent<Text>();
        dataPersistenceManager.createNewConfig(origin.text);
        dataPersistenceManager.SaveConfig();
        fileManager.Refresh();
        InteractionController.EnableMode("Main");
    }
    
    public void saveAsConfig()
    {
        Text origin = newNameOrigin.GetComponent<Text>();
        dataPersistenceManager.saveAsConfig(origin.text);
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
