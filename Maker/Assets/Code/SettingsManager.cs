using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private TMP_Dropdown dropdown; 
    private bool CoroutineActive = false; //makes sure that coroutine is not called more than once

    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        InteractionController.EnableMode("Main");
    }

    public void GetSelectedLanguage()
    {
        //
        ChangeLocale(dropdown.value);
    }

    public void ChangeLocale(int localeID)
    {
        if (CoroutineActive == true)
        {
            return;
        }
        StartCoroutine(SetLocale(localeID));
    }

    IEnumerator SetLocale(int localeID)
    {
        CoroutineActive = true;
        yield return LocalizationSettings.InitializationOperation; //makes sure that localization is loaded and ready to use
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeID];
        CoroutineActive = false;

    }
}
