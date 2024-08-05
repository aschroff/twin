using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class DataPersistenceManager : MonoBehaviour
{
    [Header("Debugging")]
    [SerializeField] private bool disableDataPersistence = false;
    [SerializeField] private bool initializeDataIfNull = false;
    

    [Header("File Storage Config")]
    [SerializeField] private string fileName;
    [SerializeField] private bool useEncryption;
    [SerializeField] public string selectedProfileId = "default";

    [Header("Auto Saving Configuration")]
    [SerializeField] private float autoSaveTimeSeconds = 60f;

    private ConfigData configData;
    private List<IDataPersistence> dataPersistenceObjects;
    private FileDataHandler dataHandler;

    private Coroutine autoSaveCoroutine;

    public static DataPersistenceManager instance { get; private set; }

    private void Awake() 
    {
        if (instance != null) 
        {
            Debug.Log("Found more than one Data Persistence Manager in the scene. Destroying the newest one.");
            Destroy(this.gameObject);
            return;
        }
        instance = this;
        //DontDestroyOnLoad(this.gameObject);

        if (disableDataPersistence) 
        {
            Debug.LogWarning("Data Persistence is currently disabled!");
        }

        this.dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, useEncryption);

        InitializeSelectedProfileId();
    }

    private void Start()
    {
        this.dataHandler = new FileDataHandler(Application.persistentDataPath, fileName, useEncryption);
        this.dataPersistenceObjects = FindAllDataPersistenceObjects();
        LoadConfig();
    }

    

    public void ChangeSelectedProfileId(string newProfileId) 
    {
        SaveConfig();
        // update the profile to use for saving and loading
        this.selectedProfileId = newProfileId;
        // load the game, which will use that profile, updating our game data accordingly
        LoadConfig();
        // push the loaded data to all other scripts that need it
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            handleChange(dataPersistenceObj);
        }
    }

    public void DeleteProfileData(string profileId) 
    {
        if (profileId == selectedProfileId)
        {
            return;
        }
        
        // delete the data for this profile id
        dataHandler.Delete(profileId);
        
        
        // push the loaded data to all other scripts that need it
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            handleDelete(dataPersistenceObj, profileId);
        }
    }

    private void InitializeSelectedProfileId() 
    {
        this.selectedProfileId = dataHandler.GetMostRecentlyUpdatedProfileId();
    }

    public void NewConfig(string modelName, string modelVersion = "") 
    {
        this.configData = new ConfigData(modelName, modelVersion);
    }
    
    public void createNewConfig(String newProfile)
    {
        string version = "";
        string name = "";
        SaveConfig();
        if ((newProfile.Contains('.') == false))
        {
            version = "000";
            name = newProfile;
            newProfile = newProfile + "." + version;
        }
        else 
        {
            version = newProfile.Split('.').Last();
            name = newProfile.Remove(newProfile.LastIndexOf(".")) ;
        }
        NewConfig(name, version);
        selectedProfileId = newProfile;
        LoadConfig();
    }

    public void saveAsConfig(String newProfile) 
    {
        SaveConfig();
        selectedProfileId = newProfile;
        SaveConfig();
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            dataPersistenceObj.LoadData(configData);
        }
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            handleCopyChange(dataPersistenceObj);
        }
    }
    
    public void ResetApp()
    {
        selectedProfileId = "default-temp-for-deleting";
        foreach (KeyValuePair<string, ConfigData> profile in GetAllProfilesGameData()  )
        {
            DeleteProfileData(profile.Key);

        }
        NewConfig("default", "000");
        selectedProfileId = "default";
        LoadConfig();
        SaveConfig();
    }
    
    public void LoadConfig()
    {
        // return right away if data persistence is disabled
        if (disableDataPersistence) 
        {
            return;
        }

        // load any saved data from a file using the data handler
        this.configData = dataHandler.Load(selectedProfileId);

        // start a new game if the data is null and we're configured to initialize data for debugging purposes
        if (this.configData == null && initializeDataIfNull) 
        {
            NewConfig(selectedProfileId, "000");
        }

        // if no data can be loaded, don't continue
        if (this.configData == null) 
        {
            Debug.Log("No data was found. A New Game needs to be started before data can be loaded.");
            return;
        }
        this.configData.version = selectedProfileId.Split('.').Last();
        this.configData.name = selectedProfileId.Remove(selectedProfileId.LastIndexOf(".")) ;

        // push the loaded data to all other scripts that need it
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            dataPersistenceObj.LoadData(configData);
        }
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            handlePostLoad(dataPersistenceObj);
        }
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            handleChange(dataPersistenceObj);
        }
    }

    public void SaveConfig()
    {
        // return right away if data persistence is disabled
        if (disableDataPersistence) 
        {
            return;
        }

        // if we don't have any data to save, log a warning here
        if (this.configData == null) 
        {
            Debug.LogWarning("No data was found. A New Game needs to be started before data can be saved.");
            return;
        }

        // pass the data to other scripts so they can update it
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            dataPersistenceObj.SaveData(configData);
        }

        // timestamp the data so we know when it was last saved
        configData.lastUpdated = System.DateTime.Now.ToBinary();


        // save that data to a file using the data handler
        dataHandler.Save(configData, selectedProfileId);
    }

    private void OnApplicationQuit() 
    {
        Debug.Log("Quit -> Saved Config");
        SaveConfig();
    }

    private List<IDataPersistence> FindAllDataPersistenceObjects() 
    {
        // FindObjectsofType takes in an optional boolean to include inactive gameobjects
        IEnumerable<MonoBehaviour> temp = FindObjectsOfType<MonoBehaviour>(true);

        IEnumerable<IDataPersistence> dataPersistenceObjects = FindObjectsOfType<MonoBehaviour>(true)
            .OfType<IDataPersistence>();

        return new List<IDataPersistence>(dataPersistenceObjects);
    }

    public bool HasGameData() 
    {
        return configData != null;
    }

    public Dictionary<string, ConfigData> GetAllProfilesGameData() 
    {
        return dataHandler.LoadAllProfiles();
    }
    public Dictionary<string, ConfigData> GetAllProfileNamesGameData() 
    {
        Dictionary<string, ConfigData> profiles =  dataHandler.LoadAllProfiles();
        Dictionary<string, ConfigData> profileDictionary = new Dictionary<string, ConfigData>();
        foreach (KeyValuePair<string, ConfigData> profile in profiles)
        {
            if (profileDictionary.ContainsKey(profile.Value.name))
            {
                continue;
            }
            profileDictionary.Add(profile.Value.name, profile.Value);
        }

        return profileDictionary;
    }
    
    public Dictionary<string, ConfigData> GetAllVersionsGameData(string name) 
    {
        Dictionary<string, ConfigData> profiles =  dataHandler.LoadAllProfiles();
        Dictionary<string, ConfigData> profileDictionary = new Dictionary<string, ConfigData>();
        foreach (KeyValuePair<string, ConfigData> profile in profiles)
        {
            if (profile.Value.name == name)
            {
                profileDictionary.Add(profile.Value.version, profile.Value);
            }
        }

        return profileDictionary;
    }
          
    
    private IEnumerator AutoSave() 
    {
        while (true) 
        {
            yield return new WaitForSeconds(autoSaveTimeSeconds);
            SaveConfig();
            Debug.Log("Auto Saved Config");
        }
    }
    
    private void handlePostLoad(IDataPersistence persistentObject){
        MonoBehaviour[] components = persistentObject.relatedGameObject().GetComponents<MonoBehaviour>();
        foreach(MonoBehaviour component in components)
        {
            Debug.Log("Post processing for component");
            if(component is ItemHash) 
            {
                Debug.Log("Found a component that inherits from ItemHash.");
                ItemHash hashComponent = component as ItemHash;
                hashComponent.handleAwake();
                break;
            }
           
        }
    }

    private void handleChange(IDataPersistence persistentObject){
        MonoBehaviour[] components = persistentObject.relatedGameObject().GetComponents<MonoBehaviour>();
        foreach(MonoBehaviour component in components)
        {
            Debug.Log("Name change for components with profile dependent (file) name");
            if(component is ItemFile) 
            {
                Debug.Log("Found a component that inherits from ItemFile.");
                ItemFile fileComponent = component as ItemFile;
                fileComponent.handleChange(selectedProfileId);
                break;
            }
        }
    }   
    
    private void handleCopyChange(IDataPersistence persistentObject){
        MonoBehaviour[] components = persistentObject.relatedGameObject().GetComponents<MonoBehaviour>();
        foreach(MonoBehaviour component in components)
        {
            Debug.Log("Name change for components with profile dependent (file) name");
            if(component is ItemFile) 
            {
                Debug.Log("Found a component that inherits from ItemFile.");
                ItemFile fileComponent = component as ItemFile;
                fileComponent.handleCopyChange(selectedProfileId);
                break;
            }
        }
    }  
    private void handleDelete(IDataPersistence persistentObject, string deleteProfileID){
        MonoBehaviour[] components = persistentObject.relatedGameObject().GetComponents<MonoBehaviour>();
        foreach(MonoBehaviour component in components)
        {
            Debug.Log("Delete for components with profile dependent (file) name");
            if(component is ItemFile) 
            {
                Debug.Log("Found a component that inherits from ItemFile.");
                ItemFile fileComponent = component as ItemFile;
                fileComponent.handleDelete(deleteProfileID);
                break;
            }
        }
    }
    public bool ExistsProfileId(string profileId) {
        if (dataHandler == null) {
            return false;
        }
        return dataHandler.Exists(profileId);
    }
}
