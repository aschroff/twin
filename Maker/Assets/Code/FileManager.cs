using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using System.Linq;
using Lean.Transition;
using UnityEngine.UI;
using System;

public class FileManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public DataPersistenceManager dataManager;
    [SerializeField] public GameObject inputFieldName;
    private Dictionary<string, string> profiles = new Dictionary<string, string>();
    [SerializeField] public GameObject inputFieldVersion;

    // Start is called before the first frame update
    void Start()
    {
        Create();
    }
    

    public void Refresh()
    {
        Delete();
        Create();
    }

    private void Create()
    {
        foreach (KeyValuePair<string, ConfigData> entry in GetProfilesGameData())
        {
            createConfigEntry(entry);
        }
    }

    private void Delete()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            Destroy(childTransform.gameObject);
        }
    }

    public ConfigData createConfigEntry(KeyValuePair<string, ConfigData> entry)
    {
        GameObject configData = Instantiate(prefab);
        configData.transform.SetParent(this.transform, false);
        configData.transform.localScale = prefab.transform.localScale;
        Text text = configData.transform.Find("Name").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        text.text = entry.Key;
        Text currentDate = configData.transform.Find("Date").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        currentDate.text = DateTime.Now.ToString("ddd, dd'.'MM'.'yy H:mm");  
        Button buttonDelete = configData.transform.Find("Delete").GetComponentInChildren<Button>();
        Button buttonSelect = configData.transform.Find("Select").GetComponentInChildren<Button>();
        Button buttonDetail = configData.transform.Find("DetailsMode").GetComponentInChildren<Button>();
        if (dataManager.selectedProfileId == entry.Key)
        {            
            buttonDelete.interactable = false ;
            buttonSelect.interactable = false ;
        }
        else
        {
            buttonDelete.onClick.AddListener(() => { Remove(entry.Key); });
            buttonSelect.onClick.AddListener(() => { Select(entry.Key); });
            buttonDetail.onClick.AddListener(() => { Detail(entry.Key); });
        }

        return entry.Value;
    }

    public virtual Dictionary<string, ConfigData> GetProfilesGameData()
    {
        return dataManager.GetAllProfileNamesGameData();
    }
    
    private void Select(string profile)
    {
        foreach (KeyValuePair<string, ConfigData> entry in GetProfilesGameData())
        {
            if (entry.Key == profile)
            {
                dataManager.ChangeSelectedProfileId(profile+"."+entry.Value.version);
                break;
            }
        }
        Refresh();
       }

    private void Remove(string profile)
    {
        dataManager.DeleteProfileData(profile);
        Refresh();
        inputFieldName.GetComponent<TwinNameValidator>().ValidateInput();
    }
    
    private void Detail(string profile)
    {
        InteractionController.EnableMode("Version");
    }
}