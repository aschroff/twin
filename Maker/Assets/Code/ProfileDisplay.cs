using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;
using UnityEngine.UI;

public class ProfileDisplay : ItemFile, IDataPersistence
{

    public override void handleChange(string profile)
    {
        Text text = this.gameObject.transform.GetComponent<Text>();
        text.text = "(" + profile +")";
    }

    public override void handleDelete(string profile)
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
