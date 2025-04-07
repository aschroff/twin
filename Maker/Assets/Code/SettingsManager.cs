using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization.Settings;
using TMPro;
using UnityEngine.UI;
using Code.AI;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private TMP_Dropdown dropdown;
    [SerializeField] private InputField inputFieldPrompt;
    public string prompt = "The person is 1.60 m tall. " 
                                + "Describe the medical findings depicted on the body in the style of a medical report. " 
                                + "Include the size and shape of the findings, their location on the body including the relative position on the body part and the orientation, and any other relevant details.";

    private bool active = false; //makes sure that coroutine is not called more than once

    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        InteractionController.EnableMode("Main");
    }

    public void GetSelectedLanguage() {
        //localeID is SetFontSize bu order of the languages  in the localization table
        // if options in drop down menu are in the same order as the languages in the localization table, we can directly parse the dopdown value as the localeID
        ChangeLocale(dropdown.value);
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
        active = false;

    }

    public void OnEnable()
    {
        DisplayPrompt();
    }

    public void OnDisable()
    {
        prompt = inputFieldPrompt.text; 
    }

    public void DisplayPrompt()
    {
        inputFieldPrompt.text = prompt; 
    }

}
