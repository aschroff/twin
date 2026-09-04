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

    
    public ItemPrompt getPromptObject(string label, ItemPrompt.PromptLevel level = ItemPrompt.PromptLevel.Unknown)
    {

        Transform top = gameObject.transform.parent;
        List<GameObject> objectsWithItemPrompt = getChildrenWithItemPrompt(top);

        foreach (GameObject child in objectsWithItemPrompt)
        {
            ItemPrompt itemPrompt = child.GetComponent<ItemPrompt>();
            if (itemPrompt.label == label)
            {
                if (level == ItemPrompt.PromptLevel.Unknown || child.GetComponent<ItemPrompt>().level == level)
                {
                    return child.GetComponent<ItemPrompt>();
                }
            }

        }

        if (label != "Default")
        {
            return getPromptObject("Default", level);
        }
        Debug.Log("Prompt not found: " + label + ", " + level);
        return null;
    }

    /// <summary>The prompt row whose <b>visible Label</b> reads <paramref name="labelText"/>.
    /// Unlike <see cref="getPromptObject"/> this tells rows apart that share label and level:
    /// three rows carry label "Medical Report" on level Version, two of them leftovers on the
    /// Meshcapade settings objects whose own Labels read differently. No fallback either - a
    /// caller that means one specific row must get that row or nothing.</summary>
    public ItemPrompt getPromptObjectByLabelText(string labelText,
        ItemPrompt.PromptLevel level = ItemPrompt.PromptLevel.Unknown)
    {
        Transform top = gameObject.transform.parent;
        foreach (GameObject child in getChildrenWithItemPrompt(top))
        {
            ItemPrompt itemPrompt = child.GetComponent<ItemPrompt>();
            if (itemPrompt.LabelText() != labelText) continue;
            if (level == ItemPrompt.PromptLevel.Unknown || itemPrompt.level == level)
            {
                return itemPrompt;
            }
        }

        Debug.Log("Prompt row not found by its label: " + labelText + ", " + level);
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
    
    public string getInput(string label)
    {
        

        GameObject FindChildByName(Transform current, string searchName)
        {
            foreach (Transform child in current)
            {
                if (child.name == searchName)
                    return child.gameObject;
                var found = FindChildByName(child, searchName);
                if (found != null)
                    return found;
            }
            return null;
        }

        GameObject target = FindChildByName(this.transform.parent, label);
        if (target != null)
        {
            var inputField = target.GetComponentInChildren<InputField>();
            if (inputField != null)
                return inputField.text;
        }
        return null;
    }


}
