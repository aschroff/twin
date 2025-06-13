using UnityEngine;
using UnityEngine.UI;

public class VersionDisplay : MonoBehaviour, ItemFile, IDataPersistence
{

    public void handleChange(string profile)
    {
        Text text = this.gameObject.transform.GetComponent<Text>();
        //accessing [1] field in array from split gets the version of the given profile ([0] is accessed in ProfileDisplay)
        text.text = profile.Split(".")[1];
    }
    public void handleCopyChange(string profile)
    {
        handleChange(profile);
    }

    public void handleDelete(string profile)
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
