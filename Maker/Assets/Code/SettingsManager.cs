using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private TMP_Dropdown dropdown;
    //[SerializeField] private InputField inputFieldSummaryPrompt;
    public string summaryPrompt = "The person is 1.60 m tall. " 
                                + "Describe the medical findings depicted on the body in the style of a medical report. " 
                                + "Include the size and shape of the findings, their location on the body including the relative position on the body part and the orientation, and any other relevant details.";

    private LanguageSelector languageSelector;

    void Start()
    {
        Debug.Log("Settings Manager started!");
    }

    public void OnEnable() {
        languageSelector = this.gameObject.GetComponent<LanguageSelector>();
        int languageID = languageSelector.GetLanguageID();
        dropdown.value = languageID;
        Debug.Log("Current languageID: " + languageID);
        DisplayPrompt();
    }

    public void ResetApp()
    {
        dataPersistenceManager.ResetApp();
        summaryPrompt = "The person is 1.60 m tall. " 
                                + "Describe the medical findings depicted on the body in the style of a medical report. " 
                                + "Include the size and shape of the findings, their location on the body including the relative position on the body part and the orientation, and any other relevant details.";
        DisplayPrompt();
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
        //summaryPrompt = inputFieldSummaryPrompt.text; 
    }

    public void DisplayPrompt()
    {
        //inputFieldSummaryPrompt.text = summaryPrompt; 
    }

}
