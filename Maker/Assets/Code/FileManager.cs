using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;

public class FileManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public DataPersistenceManager dataManager;
    private Dictionary<string, string> profiles = new Dictionary<string, string>();

    // Start is called before the first frame update
    void Start()
    {
        foreach (KeyValuePair<string, ConfigData> entry in dataManager.GetAllProfilesGameData())
        {
            createConfigEntry(entry);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (this.isActiveAndEnabled == true)
        {
            Debug.Log("__");
        } 
    }
    
    public ConfigData createConfigEntry(KeyValuePair<string, ConfigData> entry)
    {
        GameObject configData = Instantiate(prefab);
        configData.transform.SetParent(this.transform, false);
        configData.transform.localScale = prefab.transform.localScale;
        return entry.Value;
    }
}
