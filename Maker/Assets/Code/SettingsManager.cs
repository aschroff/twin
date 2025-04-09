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
    private LanguageSelector languageSelector;

    void Start()
    {
        Debug.Log("Settings Manager started!");
    }

    public void OnEnable() {
        languageSelector = this.gameObject.GetComponent<LanguageSelector>();
        int languageID = languageSelector.GetLanguageID();
        dropdown.value = languageID;
        //update drop menu here
        Debug.Log("Current languageID: " + languageID);
        DisplayPrompt();
    }

    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        InteractionController.EnableMode("Main");
    }

    public void GetSelectedLanguage() {
        //localeID is SetFontSize bu order of the languages  in the localization table
        // if options in drop down menu are in the same order as the languages in the localization table, we can directly parse the dopdown value as the localeID

        languageSelector = this.gameObject.GetComponent<LanguageSelector>();
        languageSelector.ChangeLocale(dropdown.value);
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
