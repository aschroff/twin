using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Localization.Settings;


public class LanguageSelector : MonoBehaviour, IDataPersistence   
{
    [SerializeField] public bool persistent = true;
    private bool active = false; //makes sure that coroutine is not called more than once
    private int languageID;

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    public void ChangeLocale(int localeID)
    {
        if (active == true)
        {
            return;
        }
        StartCoroutine(SetLocale(localeID));
    }

    IEnumerator SetLocale(int localeID)
    {
        active = true;
        yield return LocalizationSettings.InitializationOperation; //makes sure that localization is loaded and ready to use
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeID];
        languageID = localeID;
        active = false;

    }
    
    public void LoadData(ConfigData data)
    {
        if (persistent == false) return;
        languageID = data.languageID;
        if (languageID == null)
        {
            languageID = 0;
        }
        StartCoroutine(SetLocale(languageID));//error here because SettingManager is inactiv!
    }

    public void SaveData(ConfigData data)
    {
        if (persistent == false) return;
        if (languageID == null)
        {
            languageID = 0;
        }
        data.languageID = languageID;
    }
}
