using System;
using UnityEngine;
using UnityEngine.UI;

public class ProfileDisplay : MonoBehaviour, ItemFile, IDataPersistence
{
    public int index; 

    public  void handleChange(string profile)
    {
        if (profile.Contains("."))
        {
            String[] splitProfile = profile.Split(".");
            if(index > 1) { index = 0; }
            display(splitProfile[index]);
        }

    }
    public  void handleCopyChange(string profile)
    {
        handleChange(profile);
    }

    public  void handleDelete(string profile)
    {
        
    }

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    public void LoadData(ConfigData data)
    {
        // Reset does not trigger handleChange, so take the name/version straight from the loaded config
        if (data == null)
        {
            return;
        }
        if(index > 1) { index = 0; }
        display(index == 0 ? data.name : data.version);
    }

    private void display(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }
        Text text = this.gameObject.transform.GetComponent<Text>();
        if (text == null)
        {
            return;
        }
        text.text = value;
    }

    public void SaveData(ConfigData data)
    {

    }
}
