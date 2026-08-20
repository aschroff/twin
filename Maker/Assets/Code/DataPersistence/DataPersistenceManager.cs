using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using RotaryHeart.Lib.SerializableDictionary;
using PaintCore;
using Application = UnityEngine.Application;

[Serializable]
public class StickerFiles : SerializableDictionaryBase<string, Texture2D> { }
[Serializable]
public class Template
{
    public TextAsset configFile;
    public StickerFiles stickerFiles;
}
[Serializable]
public class Templates : SerializableDictionaryBase<string, Template> { }

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
    
    [Header("Templates")]
    [SerializeField] private Templates  templates;

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

        this.dataHandler = new FileDataHandler(DataPaths.PersistentDataPath, Path.Combine(Application.streamingAssetsPath, "templates"), fileName, useEncryption);

        InitializeSelectedProfileId();
    }

    private void Start()
    {
        this.dataHandler = new FileDataHandler(DataPaths.PersistentDataPath, Path.Combine(Application.streamingAssetsPath, "templates"), fileName, useEncryption);
        this.dataPersistenceObjects = FindAllDataPersistenceObjects();
        LoadConfig();
        initPersistentObjects();
    }

    

    public void ChangeSelectedProfileId(string newProfileId) 
    {
        SaveConfig();
        // update the profile to use for saving and loading
        this.selectedProfileId = newProfileId;
        // load the game, which will use that profile, updating our game data accordingly
        LoadConfig();
        initPersistentObjects();
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
    
    public void DeleteAllVersions(string profileId) 
    {
       
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
        this.selectedProfileId = dataHandler.GetMostRecentlyUpdatedProfileId() ?? "default.000";
    }

    public void NewConfig(string modelName, string modelVersion = "") 
    {
        this.configData = new ConfigData(modelName, modelVersion);
    }
    
    public void createNewConfig(String newProfile)
    {
        SaveConfig();
        string version = "";
        string name = "";
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
        initPersistentObjects();
    }

    public void saveAsConfig(String newProfile) 
    {
        SaveConfig();
        selectedProfileId = newProfile;
        string version = "";
        string name = "";
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
        this.configData.version = version;
        this.configData.name = name;
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
        selectedProfileId = "default-temp-for-deleting.000";
        foreach (KeyValuePair<string, ConfigData> profile in GetAllProfilesGameData()  )
        {
            DeleteProfileData(profile.Key);
        }
        foreach (KeyValuePair<string, Template> template in templates)
        {
            string content = template.Value.configFile.text;
            string path = Path.Combine(DataPaths.PersistentDataPath,template.Key);
            Directory.CreateDirectory(path);
            System.IO.File.WriteAllText(Path.Combine(DataPaths.PersistentDataPath,template.Key, "ConfigTwin"), content);
            foreach (KeyValuePair<string, Texture2D> stickerFile in template.Value.stickerFiles)
            {
                byte[] bytes = stickerFile.Value.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(DataPaths.PersistentDataPath,template.Key, stickerFile.Key + ".png"), bytes);
            }

            
        }
        NewConfig("default", "000");
        selectedProfileId = "default.000";
        initPersistentObjectsLoadOnly();
        SaveConfig();
        Debug.Log("End Reset");
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
    }
    
    public void LoadConfigFromTemplate()
    {
        // return right away if data persistence is disabled
        if (disableDataPersistence) 
        {
            return;
        }

        // load any saved data from a file using the data handler
        this.configData = dataHandler.LoadFromTemplate(selectedProfileId);

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
        initPersistentObjects();
    }

    private void initPersistentObjects()
    {
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
    
    private void initPersistentObjectsLoadOnly()
    {
        // push the loaded data to all other scripts that need it
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            dataPersistenceObj.LoadData(configData);
        }
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects) 
        {
            handlePostLoad(dataPersistenceObj);
        }
    }
    
    public void SaveConfig()
    {
        // return right away if data persistence is disabled
        if (disableDataPersistence) 
        {
            return;
        }

        bool flowControl = PrepareConfigStorage();
        if (!flowControl)
        {
            return;
        }

        // save that data to a file using the data handler
        dataHandler.Save(configData, selectedProfileId);
    }

    public void ExportConfig()
    {
        string zipFilePath = ExportConfigZip();
        if (zipFilePath == null)
        {
            return;
        }
        dataHandler.ExportFile(zipFilePath);
    }

    /*
    * Saves the current twin and packs it into a zip file. Returns the path of that zip file,
    * without handing it to the user - that is what ExportConfig adds on top.
    */
    public string ExportConfigZip()
    {
        bool flowControl = PrepareConfigStorage();
        if (!flowControl)
        {
            return null;
        }

        // the painted texture is only cached on this device, so it has to be written into the
        // twin directory to travel with the export
        foreach (Body body in FindObjectsOfType<Body>(true))
        {
            body.StoreTextureFile(selectedProfileId);
        }

        // save that data to a file using the data handler
        return dataHandler.ExportZip(configData, selectedProfileId);
    }

    /*
    * Coordinates Twin Configuration import and reloading the app to use the newly imported twin.
    * Picking the file is asynchronous, so the profile id of the imported twin is handed to
    * onImported once the import is through - that is the point at which the twin exists.
    */
    public async void ImportConfig( Action<string> onImported = null )
    {
        // delegating data import to responsible (File)DataHandler
        string pathToExtractedDirectory =  await dataHandler.ImportZipConfigAsync();
        string profileId = FinishImport( pathToExtractedDirectory );
        if ( onImported != null )
        {
            onImported( profileId );
        }
    }

    /*
    * Imports an already chosen zip file (no file picker involved).
    * Returns the profile id of the imported twin, or null if the import failed.
    */
    public string ImportConfig( string zipFilePath )
    {
        return FinishImport( dataHandler.ImportZipConfig( zipFilePath ) );
    }

    private string FinishImport( string pathToExtractedDirectory )
    {
        if ( string.IsNullOrEmpty( pathToExtractedDirectory ) )
        {
            Debug.Log( " Error: Config importation did not work. " );
            return null;
        }
        string profileId = Path.GetFileName( pathToExtractedDirectory );
        //even though we want to get the directory name we have to call GetFileName here to get the correct attribute back

        if ( !dataHandler.Exists( profileId ) )
        {
            Debug.Log( " Error: Config importation did not work. " );
            return null;
        }

        // The painted texture is cached per twin and that cache is local to this device, so
        // anything stored under the id of the imported twin belongs to a different twin.
        // Dropping it makes the app pick up the texture that came with the import.
        CwPaintableTexture.ClearSave( profileId );
        return profileId;
    }

    /*
    * Collects the saveable data
    */
    private bool PrepareConfigStorage()
    {
        // if we don't have any data to save, log a warning here
        if (this.configData == null)
        {
            Debug.LogWarning("No data was found. A New Game needs to be started before data can be saved.");
            return false;
        }

        // pass the data to other scripts so they can update it
        foreach (IDataPersistence dataPersistenceObj in dataPersistenceObjects)
        {
            dataPersistenceObj.SaveData(configData);
        }

        // timestamp the data so we know when it was last saved
        configData.lastUpdated = System.DateTime.Now.ToBinary();
        return true;
    }

    private void OnApplicationQuit() 
    {
        Debug.Log("Quit -> Saved Config");
        SaveConfig();
    }

    private List<IDataPersistence> FindAllDataPersistenceObjects() 
    {
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
    /*
    * One entry per twin name, for the twin list. A twin with several versions is represented by
    * the version that is currently open, and by its most recently updated version otherwise -
    * the open twin has to be the one the list marks as open.
    */
    public Dictionary<string, ConfigData> GetAllProfileNamesGameData()
    {
        Dictionary<string, ConfigData> profiles =  dataHandler.LoadAllProfiles();
        Dictionary<string, ConfigData> profileDictionary = new Dictionary<string, ConfigData>();
        Dictionary<string, string> representingProfileId = new Dictionary<string, string>();
        foreach (KeyValuePair<string, ConfigData> profile in profiles)
        {
            string name = profile.Value.name;
            if (!profileDictionary.ContainsKey(name))
            {
                profileDictionary.Add(name, profile.Value);
                representingProfileId.Add(name, profile.Key);
                continue;
            }
            bool representedByOpenVersion = representingProfileId[name] == selectedProfileId;
            bool isOpenVersion = profile.Key == selectedProfileId;
            bool isNewer = profileDictionary[name].lastUpdated < profile.Value.lastUpdated;
            if (isOpenVersion || (!representedByOpenVersion && isNewer))
            {
                profileDictionary[name] = profile.Value;
                representingProfileId[name] = profile.Key;
            }
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
            if(component is ItemHash) 
            {
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
            if(component is ItemFile) 
            {
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
            if(component is ItemFile) 
            {
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
            if(component is ItemFile) 
            {
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
