using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
    [SerializeField] private DataPersistenceManager dataPersistenceManager;
    [SerializeField] private TMP_Dropdown dropdown;

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


    public List<(string,string)> getPrompts()
    {
        List<(string, string)> list = new List<(string, string)>();

        Transform top = gameObject.transform.parent;
        List<GameObject> objectsWithItemPrompt = getChildrenWithItemPrompt(top);

        foreach (GameObject child in objectsWithItemPrompt)
        {
            string label = child.gameObject.GetComponentInChildren<Text>().text;
            string prompt = child.gameObject.GetComponentInChildren<InputField>().text;
            list.Add((label, prompt));   
        }

        return list; 
    }
    
    public ItemPrompt getPromptObject(string label)
    {
        List<(string, string)> list = new List<(string, string)>();

        Transform top = gameObject.transform.parent;
        List<GameObject> objectsWithItemPrompt = getChildrenWithItemPrompt(top);

        foreach (GameObject child in objectsWithItemPrompt)
        {
            if (child.gameObject.GetComponentInChildren<Text>().text == label)
            {
                return child.GetComponent<ItemPrompt>();
            }

        }
        return null;
    }

    private List<GameObject> getChildrenWithItemPrompt(Transform parent)
    {
        List<GameObject> childrenWithItemPrompt = new List<GameObject>();

        foreach(Transform child in parent)
        {
            if(child.gameObject.TryGetComponent<ItemPrompt>(out var _itemPrompt))
            {
                childrenWithItemPrompt.Add(child.gameObject);
            }
            childrenWithItemPrompt.AddRange(getChildrenWithItemPrompt(child));
        }
        return childrenWithItemPrompt;
    }

}
