using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class Item : MonoBehaviour, IDataPersistence
{
    [SerializeField] public string id;
    [SerializeField] public bool persistent = true;

    [ContextMenu("Generate guid for id")]
    protected void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
        
    }

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    private InputField textItem;
    private string text;


    public string getId()
    {
        return this.id;

    }

    public int getHash()
    {
        return this.id.ToCharArray().Sum(x => x) % 100;

    }

    private void setText()
    {
        textItem = this.GetComponentInChildren<InputField>();

    }

    public void LoadData(ConfigData data)
    {
        if (persistent == false) return;
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
        if (persistent == false) return;
        if (data.itemTexts.ContainsKey(id))
        {
            data.itemTexts.Remove(id);
        }
        data.itemTexts.Add(id, textItem.text);
    }

    

 

}
