using System.Collections;
using UnityEngine;

using UnityEngine.Localization.Settings;


public class LanguageSelector : MonoBehaviour, IDataPersistence   
{
    [SerializeField] public bool persistent = true;
    private bool isMyCoroutineActive = false; //makes sure that coroutine is not called more than once
    private int languageID;

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    public void ChangeLocale(int localeID)
    {
        if (isMyCoroutineActive == true)
        {
            return;
        }
        StartCoroutine(SetLocale(localeID));
    }

    IEnumerator SetLocale(int localeID)
    {
        isMyCoroutineActive = true;
        yield return LocalizationSettings.InitializationOperation; //makes sure that localization is loaded and ready to use
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[localeID];
        languageID = localeID;
        isMyCoroutineActive = false;

    }

    public void LoadData(ConfigData data)
    {
        if (persistent == false) return;
        languageID = data.languageID;
        if (data.languageID == null)
        {
            languageID = 0;
        } 
        
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[languageID];
        
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
