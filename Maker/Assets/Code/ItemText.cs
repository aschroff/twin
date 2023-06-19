using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Item : MonoBehaviour, IDataPersistence
{
    [SerializeField] private string id;

    [ContextMenu("Generate guid for id")]
    private void GenerateGuid()
    {
        id = System.Guid.NewGuid().ToString();
    }

    private GameObject visual;
    private string text = "";


    public void LoadData(ConfigData data)
    {
        data.itemTexts.TryGetValue(id, out text);
        if (text != "")
        {
            visual.gameObject.SetActive(false);
        }
    }

    public void SaveData(ConfigData data)
    {
        if (data.itemTexts.ContainsKey(id))
        {
            data.itemTexts.Remove(id);
        }
        data.itemTexts.Add(id, text);
    }

    

    private void SetText()
    {
        text = "";
        visual.gameObject.SetActive(false);
    }

}
