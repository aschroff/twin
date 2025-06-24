using System;
using UnityEngine;
using UnityEngine.UI;

public class ProfileDisplay : MonoBehaviour, ItemFile, IDataPersistence
{
    public int index; 

    public  void handleChange(string profile)
    {
        Text text = this.gameObject.transform.GetComponent<Text>();
        if (profile.Contains("."))
        {
            String[] splitProfile = profile.Split(".");
            if(index > 1) { index = 0; }
            text.text = splitProfile[index];
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

    }

    public void SaveData(ConfigData data)
    {

    }
}
