using UnityEngine;
using UnityEngine.UI;

public class ItemPrompt : MonoBehaviour, IDataPersistence
{
    public enum PromptLevel
    {
        Version,
        Part,
        Unknown
    }

    [SerializeField] public PromptLevel level;
    [SerializeField] public string label;
    private InputField inputField;
    
    public string promptResult = "";
    
    private string getUniqueDescription()
    {
        return this.gameObject.transform.Find("Label").GetComponent<Text>().text;
    }

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    private void setPrompt()
    {
        inputField = this.GetComponentInChildren<InputField>();
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
                inputField.text = "";
            }
            
            data.resultsVersion.TryGetValue(key, out promptResult);
        }

    }

}
