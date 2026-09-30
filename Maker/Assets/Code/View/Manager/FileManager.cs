using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using System.Linq;
using Lean.Transition;
using UnityEngine.UI;
using System;
using Lean.Gui;

public class FileManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public DataPersistenceManager dataManager;
    [SerializeField] public GameObject inputFieldName;
    private Dictionary<string, string> profiles = new Dictionary<string, string>();
    [SerializeField] public GameObject inputFieldVersion;
    [SerializeField] public Recorder recorder;
    [SerializeField] public LeanPulse notification;
    public string versionProfile;

    protected virtual bool GetVersionButton()
    {
        return true;
    } 

    public virtual string GetProfile()
    {
        return inputFieldName.transform.Find("Text").GetComponent<Text>().text.ToString();
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
            buttonDelete.onClick.AddListener(() => { Remove(entry.Value.name+"."+entry.Value.version); });
            buttonSelect.gameObject.SetActive(false);
            buttonSelect.interactable = false;
            buttonUnselect.gameObject.SetActive(true);
            buttonUnselect.interactable = true;
            buttonUnselect.onClick.AddListener(() => { Select(entry.Value.name,entry.Value.version); });
        }
        buttonDetail.onClick.AddListener(() => { Detail(entry.Key, entry.Value.name,entry.Value.version); });
        buttonDetail.gameObject.SetActive(GetVersionButton());
        
        
        return entry.Value;
    }

    public virtual Dictionary<string, ConfigData> GetProfilesGameData()

    {
        return dataManager.GetAllProfileNamesGameData();
    }
    
    /// <summary>
    /// Loads a twin, with the busy panel up while it happens.
    /// </summary>
    /// <remarks>Switching a twin blocks the main thread for about two seconds, measured. Without
    /// the panel nothing on screen changes in that time, so there is no telling a slow app from a
    /// hung one - and an impatient second tap is delivered on the next frame and starts the whole
    /// thing again.</remarks>
    /// <param name="onDone">Runs once the twin is actually loaded. Callers that continue straight
    /// after selecting need it, because the work no longer happens inside this call.</param>
    private void Select(string profile, string version = "", System.Action onDone = null)
    {
        Debug.Log("Select: " + profile + "." + version);
        string profileId = profile + "." + version;

        BusyOverlay.RunBlocking(StringLocalizer.localizeString("LOADING_TWIN"), () =>
        {
            foreach (KeyValuePair<string, ConfigData> entry in dataManager.GetAllProfilesGameData())
            {
                if (entry.Key == profileId)
                {
                    dataManager.ChangeSelectedProfileId(profileId);
                    break;
                }
            }
            Refresh();
        }, onDone);
    }

    private void Remove(string profile)
    {
        dataManager.DeleteProfileData(profile);
        Refresh();
    }
    
    private void Detail(string profile, string profile_raw, string version = "")
    {
        if (dataManager.selectedProfileId != profile_raw + "." + version)
        {
            // the twin has to be loaded before the menu opens on it, and loading is no longer
            // finished by the time Select returns
            Select(profile_raw, version, () => OpenDetail(profile));
            return;
        }
        OpenDetail(profile);
    }

    private void OpenDetail(string profile)
    {
        versionProfile = profile;
        InteractionController.EnableMode("Menu");
    }
    
    public void RemoveCurrent()
    {
        string name = versionProfile;
        if ( name.Contains('.'))
        {
            name = name.Remove(name.LastIndexOf("."));
        }
        foreach (KeyValuePair<string, ConfigData> version in dataManager.GetAllVersionsGameData(name))
        {
            dataManager.DeleteProfileData(name + "." + version.Key);
        }
        Refresh();
        inputFieldName.GetComponent<TwinNameValidator>().ValidateInput();
        InteractionController.EnableMode("Save");
    }

    
    public void Screenshot()
    {
        recorder.name = dataManager.selectedProfileId;
        recorder.folder = dataManager.selectedProfileId;
        recorder.Screenshot(notification);
    }
    
    
}