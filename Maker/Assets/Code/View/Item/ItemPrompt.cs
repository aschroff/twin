using UnityEngine;
using UnityEngine.UI;

public class ItemPrompt : MonoBehaviour, IDataPersistence
{
    public enum PromptLevel
    {
        Version,
        Part,
        Unknown,
        // new values go at the end - the level is stored as its number in the scene
        Document
    }

    [SerializeField] public PromptLevel level;
    [SerializeField] public string label;

    /// <summary>Key in TwinLocalTables of the text this prompt starts with. A twin stores the
    /// text the user edited; a twin that has never stored one gets this default, in the
    /// language of the app. Empty means the prompt starts empty, as it always did.</summary>
    [SerializeField] public string defaultKey;

    private InputField inputField;
    
    public string promptResult = "";
    
    private string getUniqueDescription()
    {
        return LabelText();
    }

    /// <summary>The row's visible Label. It is the key this prompt is stored under, and the only
    /// way to tell rows apart that share label and level - three rows carry label
    /// "Medical Report" on level Version, two of them leftovers whose Labels read differently.
    /// </summary>
    public string LabelText()
    {
        Transform label = this.gameObject.transform.Find("Label");
        Text text = label != null ? label.GetComponent<Text>() : null;
        return text != null ? text.text : "";
    }

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    private void setPrompt()
    {
        inputField = this.GetComponentInChildren<InputField>();
    }

    /// <summary>The prompt text as it currently stands - what a caller sends to the LLM.</summary>
    public string GetPromptText()
    {
        if (inputField == null)
        {
            setPrompt();
        }
        return inputField != null ? inputField.text : "";
    }

    /// <summary>The shipped text for this prompt, in the language of the app. Empty when no
    /// default is configured, or while the localization table is not loaded yet - in both
    /// cases the prompt simply starts empty.</summary>
    private string getDefaultText()
    {
        if (string.IsNullOrEmpty(defaultKey))
        {
            return "";
        }

        string localized = StringLocalizer.localizeString(defaultKey);
        // localizeString hands the key back when the table has no entry for it
        return localized == defaultKey ? "" : localized;
    }

    public void SaveData(ConfigData data)
    {
        string key = getUniqueDescription();
        if(data.prompts.ContainsKey(key))
        {
            data.prompts.Remove(key);
        }
        data.prompts.Add(key, inputField.text);
        if (level == PromptLevel.Version)
        {
            if (data.resultsVersion.ContainsKey(key))
            {
                data.resultsVersion.Remove(key);
            }
            data.resultsVersion.Add(key, promptResult);
        }
        else
        {
            if (data.partList.ContainsKey(key))
            {
                data.partList.Remove(key);
            }
        }

    }

    public void LoadData(ConfigData data)
    {
        string key = getUniqueDescription();
        if (inputField == null)
        {
            setPrompt();
        }
        string promptText;

        if(key != null)
        {
            data.prompts.TryGetValue(key, out promptText);
        
            if (promptText != null)
            {
                inputField.text = promptText;
            } 
            else if (inputField != null) 
            {
                // no text stored for this twin: start from the shipped default
                inputField.text = getDefaultText();
            }
            
            data.resultsVersion.TryGetValue(key, out promptResult);
        }

    }

}
