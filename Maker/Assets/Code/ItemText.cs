using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Item : MonoBehaviour, IDataPersistence
{
    [SerializeField] private string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    private InputField textItem;
    private string text;


    public string getId()
    {
        return this.id;

    }


    private void setText()
    {
        textItem = this.GetComponentInChildren<InputField>();

    }

    public void LoadData(ConfigData data)
    {       
        if (textItem == null)
        {
            setText();
        }
        Debug.Log("Loading" + textItem.text + " - " + this.id);
        data.itemTexts.TryGetValue(id, out text);
        if (text != null)
       {
            textItem.text = text;
        }
    }

    public void SaveData(ConfigData data)
    {
        if (data.itemTexts.ContainsKey(id))
        {
            data.itemTexts.Remove(id);
        }
        data.itemTexts.Add(id, textItem.text);
    }

    

 

}
