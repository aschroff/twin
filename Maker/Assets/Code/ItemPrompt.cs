using UnityEngine;
using UnityEngine.UI;

public class ItemPrompt : MonoBehaviour, IDataPersistence
{
    [SerializeField] public bool persistent = true;
    private InputField inputField;

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
        if(persistent == false) return;
        string label = GetComponentInChildren<Text>().text; //??
        if(data.prompts.ContainsKey(label))
        {
            data.prompts.Remove(label);
        }
        data.prompts.Add(label, inputField.text);

    }

    public void LoadData(ConfigData data)
    {
        if(persistent == false) return;
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
        }

    }

}
