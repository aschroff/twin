using UnityEngine;
using UnityEngine.UI;

public class ItemPrompt : MonoBehaviour, IDataPersistence
{
    public enum PromptLevel
    {
        Version,
        Part
    }

    [SerializeField] public PromptLevel level;
    private InputField inputField;
    
    public string promptResult = "";

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
        string label = GetComponentInChildren<Text>().text; //??
        if(data.prompts.ContainsKey(label))
        {
            data.prompts.Remove(label);
        }
        data.prompts.Add(label, inputField.text);
        if (level == PromptLevel.Version)
        {
            if (data.resultsVersion.ContainsKey(label))
            {
                data.resultsVersion.Remove(label);
            }
            data.resultsVersion.Add(label, promptResult);
        }
        else
        {
            if (data.partList.ContainsKey(label))
            {
                data.partList.Remove(label);
            }
        }

    }

    public void LoadData(ConfigData data)
    {
        if (inputField == null)
        {
            setPrompt();
        }
        Text label = this.GetComponentInChildren<Text>();
        string promptText;

        if(label != null)
        {
            data.prompts.TryGetValue(label.text, out promptText);
        
            if (promptText != null)
            {
                inputField.text = promptText;
            } 
            else if (inputField != null) 
            {
                inputField.text = "";
            }
            
            data.resultsVersion.TryGetValue(label.text, out promptResult);
        }

    }

}
