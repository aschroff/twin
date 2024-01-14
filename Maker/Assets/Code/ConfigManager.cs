using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ConfigManager : MonoBehaviour
{

    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private GameObject newNameOrigin;
    public void newConfig()
    {
        Text origin = newNameOrigin.GetComponent<Text>();
        dataPersistenceManager.createNewConfig(origin.text);
        InteractionController.EnableMode("Main");
    }

}
