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
    public string versionProfile;

    protected virtual bool GetVersionButton()
    {
        return true;
    } 
    
    void OnDisable()
    {
        Debug.Log("PrintOnDisable: script was disabled");
    }

    void OnEnable()
    {
        Refresh();
    }

    
    // Start is called before the first frame update
    void Start()
    {
        //Create();
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
        text.text = entry.Value.name;
        Text version = configData.transform.Find("Version").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        version.text = entry.Value.version;
        Text date = configData.transform.Find("Date").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        date.text = entry.Value.updated;
        Button buttonDelete = configData.transform.Find("Delete").GetComponentInChildren<Button>();
        Button buttonSelect = configData.transform.Find("Select").GetComponentInChildren<Button>();
        Button buttonUnselect = configData.transform.Find("Unselect").GetComponentInChildren<Button>();
        Button buttonDetail = configData.transform.Find("DetailsMode").GetComponentInChildren<Button>();
        if (dataManager.selectedProfileId == entry.Value.name+"."+entry.Value.version)
        {            
            buttonDelete.interactable = false ;
            buttonSelect.gameObject.SetActive(true);
            buttonSelect.interactable = false;
            buttonUnselect.gameObject.SetActive(false);
            buttonUnselect.interactable = false;

        }
        else
        {
            buttonDelete.onClick.AddListener(() => { Remove(entry.Key); });
            buttonSelect.gameObject.SetActive(false);
            buttonSelect.interactable = false;
            buttonUnselect.gameObject.SetActive(true);
            buttonUnselect.interactable = true;
            buttonUnselect.onClick.AddListener(() => { Select(entry.Value.name,entry.Value.version); });
        }
        buttonDetail.onClick.AddListener(() => { Detail(entry.Key); });
        buttonDetail.gameObject.SetActive(GetVersionButton());
        
        
        return entry.Value;
    }

    public virtual Dictionary<string, ConfigData> GetProfilesGameData()

    {
        return dataManager.GetAllProfileNamesGameData();
    }
    
    private void Select(string profile, string version = "")
    {

        foreach (KeyValuePair<string, ConfigData> entry in dataManager.GetAllProfilesGameData())
        {
            if (entry.Key == profile+"."+version)
            {
                dataManager.ChangeSelectedProfileId(profile+"."+version);
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
        versionProfile = profile;
        InteractionController.EnableMode("Version");
    }
}