using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Localization.Settings;

public class LanguageSelector : MonoBehaviour
{
    private bool active = false; //makes sure that coroutine is not called more than once

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
        active = false;

    }
}
