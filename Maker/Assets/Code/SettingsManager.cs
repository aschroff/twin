using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private DataPersistenceManager dataPersistenceManager;

    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        InteractionController.EnableMode("Main");
    }
}
