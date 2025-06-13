using System.Collections;
using System.Collections.Generic;
using PaintIn3D;
using UnityEngine;
using UnityEngine.UI;

public class ProfileDisplay : MonoBehaviour, ItemFile, IDataPersistence
{

    public  void handleChange(string profile)
    {
        Text text = this.gameObject.transform.GetComponent<Text>();
        //accessing [0] field in array from split gets the twine name of the given profile ([1] is accessed in VersionDisplay)
        text.text = profile.Split(".")[0];
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
