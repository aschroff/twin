using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using SFB;
using SimpleFileBrowser;
// using FileBrowser;
// using NativeFilePicker;


public class FileDataHandler
{
    private string dataDirPath = "";
    private string fileType = " ";
    private string templateDirPath = "";
    private string dataFileName = "";
    private bool useEncryption = false;
    private readonly string encryptionCodeWord = "word";
    private readonly string backupExtension = ".bak";

    public FileDataHandler(string dataDirPath, string templateDirPath, string dataFileName, bool useEncryption) 
    {
        this.dataDirPath = dataDirPath;
        this.templateDirPath = templateDirPath;
        this.dataFileName = dataFileName;
        this.useEncryption = useEncryption;
    }

    public ConfigData Load(string profileId, bool allowRestoreFromBackup = true) 
    {
        // base case - if the profileId is null, return right away
        if (profileId == null) 
        {
            return null;
        }

        // use Path.Combine to account for different OS's having different path separators
        string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
        ConfigData loadedData = null;
        if (File.Exists(fullPath)) 
        {
            try 
            {
                // load the serialized data from the file
                string dataToLoad = "";
                using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        dataToLoad = reader.ReadToEnd();
                    }
                }

                // optionally decrypt the data
                if (useEncryption) 
                {
                    dataToLoad = EncryptDecrypt(dataToLoad);
                }

                // deserialize the data from Json back into the C# object
                loadedData = JsonUtility.FromJson<ConfigData>(dataToLoad);
            }
            catch (Exception e) 
            {
                // since we're calling Load(..) recursively, we need to account for the case where
                // the rollback succeeds, but data is still failing to load for some other reason,
                // which without this check may cause an infinite recursion loop.
                if (allowRestoreFromBackup) 
                {
                    Debug.LogWarning("Failed to load data file. Attempting to roll back.\n" + e);
                    bool rollbackSuccess = AttemptRollback(fullPath);
                    if (rollbackSuccess)
                    {
                        // try to load again recursively
                        loadedData = Load(profileId, false);
                    }
                }
                // if we hit this else block, one possibility is that the backup file is also corrupt
                else 
                {
                    Debug.LogError("Error occured when trying to load file at path: " 
                        + fullPath  + " and backup did not work.\n" + e);
                }
            }
        }
        return loadedData;
    }
    
    public ConfigData LoadFromTemplate(string profileId, bool allowRestoreFromBackup = true) 
    {
        // base case - if the profileId is null, return right away
        if (profileId == null) 
        {
            return null;
        }

        // use Path.Combine to account for different OS's having different path separators
        string fullPath = Path.Combine(templateDirPath, profileId, dataFileName);
        ConfigData loadedData = null;
        if (File.Exists(fullPath)) 
        {
            try 
            {
                // load the serialized data from the file
                string dataToLoad = "";
                using (FileStream stream = new FileStream(fullPath, FileMode.Open))
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        dataToLoad = reader.ReadToEnd();
                    }
                }

                // optionally decrypt the data
                if (useEncryption) 
                {
                    dataToLoad = EncryptDecrypt(dataToLoad);
                }

                // deserialize the data from Json back into the C# object
                loadedData = JsonUtility.FromJson<ConfigData>(dataToLoad);
            }
            catch (Exception e) 
            {
                // since we're calling Load(..) recursively, we need to account for the case where
                // the rollback succeeds, but data is still failing to load for some other reason,
                // which without this check may cause an infinite recursion loop.
                if (allowRestoreFromBackup) 
                {
                    Debug.LogWarning("Failed to load data file. Attempting to roll back.\n" + e);
                    bool rollbackSuccess = AttemptRollback(fullPath);
                    if (rollbackSuccess)
                    {
                        // try to load again recursively
                        loadedData = Load(profileId, false);
                    }
                }
                // if we hit this else block, one possibility is that the backup file is also corrupt
                else 
                {
                    Debug.LogError("Error occured when trying to load file at path: " 
                        + fullPath  + " and backup did not work.\n" + e);
                }
            }
        }
        return loadedData;
    }

    public void Save(ConfigData data, string profileId) 
    {
        // base case - if the profileId is null, return right away
        if (profileId == null) 
        {
            return;
        }

        // use Path.Combine to account for different OS's having different path separators
        if ((data.version != "") && (profileId.Contains('.') == false))
        {
            profileId = profileId + "." + data.version;
        }
        else if ((data.version != "") && ( profileId.Contains('.')))
        {
            profileId = profileId.Remove(profileId.LastIndexOf(".")) + "." + data.version;
        }
        data.updated = DateTime.Now.ToString();
        string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
        string backupFilePath = fullPath + backupExtension;
        try 
        {
            // create the directory the file will be written to if it doesn't already exist
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // serialize the C# game data object into Json
            string dataToStore = JsonUtility.ToJson(data, true);

            // optionally encrypt the data
            if (useEncryption) 
            {
                dataToStore = EncryptDecrypt(dataToStore);
            }

            // write the serialized data to the file
            using (FileStream stream = new FileStream(fullPath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream)) 
                {
                    writer.Write(dataToStore);
                }
            }

            // verify the newly saved file can be loaded successfully
            ConfigData verifiedGameData = Load(profileId);
            // if the data can be verified, back it up
            if (verifiedGameData != null) 
            {
                File.Copy(fullPath, backupFilePath, true);
            }
            // otherwise, something went wrong and we should throw an exception
            else 
            {
                throw new Exception("Save file could not be verified and backup could not be created.");
            }

        }
        catch (Exception e) 
        {
            Debug.LogError("Error occured when trying to save data to file: " + fullPath + "\n" + e);
        }
    }

    public void Delete(string profileId) 
    {
        // base case - if the profileId is null, return right away
        if (profileId == null) 
        {
            return;
        }

        string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
        try 
        {
            // ensure the data file exists at this path before deleting the directory
            if (File.Exists(fullPath)) 
            {
                // delete the profile folder and everything within it
                Directory.Delete(Path.GetDirectoryName(fullPath), true);
            }
            else 
            {
                Debug.LogWarning("Tried to delete profile data, but data was not found at path: " + fullPath);
            }
        }
        catch (Exception e) 
        {
            Debug.LogError("Failed to delete profile data for profileId: " 
                + profileId + " at path: " + fullPath + "\n" + e);
        }
    }

    public Dictionary<string, ConfigData> LoadAllProfiles() 
    {
        Dictionary<string, ConfigData> profileDictionary = new Dictionary<string, ConfigData>();

        // loop over all directory names in the data directory path
        IEnumerable<DirectoryInfo> dirInfos = new DirectoryInfo(dataDirPath).EnumerateDirectories();
        foreach (DirectoryInfo dirInfo in dirInfos) 
        {
            string profileId = dirInfo.Name;

            // defensive programming - check if the data file exists
            // if it doesn't, then this folder isn't a profile and should be skipped
            string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
            if (!File.Exists(fullPath))
            {
                Debug.LogWarning("Skipping directory when loading all profiles because it does not contain data: "
                    + profileId);
                continue;
            }

            // load the game data for this profile and put it in the dictionary
            ConfigData profileData = Load(profileId);
            // defensive programming - ensure the profile data isn't null,
            // because if it is then something went wrong and we should let ourselves know
            if (profileData != null) 
            {
                profileDictionary.Add(profileId, profileData);
            }
            else 
            {
                Debug.LogError("Tried to load profile but something went wrong. ProfileId: " + profileId);
            }
        }
        return profileDictionary;
    }

    public string GetMostRecentlyUpdatedProfileId() 
    {
        string mostRecentProfileId = null;

        Dictionary<string, ConfigData> profilesGameData = LoadAllProfiles();
        foreach (KeyValuePair<string, ConfigData> pair in profilesGameData) 
        {
            string profileId = pair.Key;
            ConfigData gameData = pair.Value;

            // skip this entry if the gamedata is null
            if (gameData == null) 
            {
                continue;
            }

            // if this is the first data we've come across that exists, it's the most recent so far
            if (mostRecentProfileId == null) 
            {
                mostRecentProfileId = profileId;
            }
            // otherwise, compare to see which date is the most recent
            else 
            {
                DateTime mostRecentDateTime = DateTime.FromBinary(profilesGameData[mostRecentProfileId].lastUpdated);
                DateTime newDateTime = DateTime.FromBinary(gameData.lastUpdated);
                // the greatest DateTime value is the most recent
                if (newDateTime > mostRecentDateTime) 
                {
                    mostRecentProfileId = profileId;
                }
            }
        }
        return mostRecentProfileId;
    }

    // the below is a simple implementation of XOR encryption
    private string EncryptDecrypt(string data) 
    {
        string modifiedData = "";
        for (int i = 0; i < data.Length; i++) 
        {
            modifiedData += (char) (data[i] ^ encryptionCodeWord[i % encryptionCodeWord.Length]);
        }
        return modifiedData;
    }

    private bool AttemptRollback(string fullPath) 
    {
        bool success = false;
        string backupFilePath = fullPath + backupExtension;
        try 
        {
            // if the file exists, attempt to roll back to it by overwriting the original file
            if (File.Exists(backupFilePath))
            {
                File.Copy(backupFilePath, fullPath, true);
                success = true;
                Debug.LogWarning("Had to roll back to backup file at: " + backupFilePath);
            }
            // otherwise, we don't yet have a backup file - so there's nothing to roll back to
            else 
            {
                throw new Exception("Tried to roll back, but no backup file exists to roll back to.");
            }
        }
        catch (Exception e) 
        {
            Debug.LogError("Error occured when trying to roll back to backup file at: " 
                + backupFilePath + "\n" + e);
        }

        return success;
    }

    public bool Exists(string profileId)
    {
        // base case - if the profileId is null, return right away
        if (profileId == null)
        {
            return false;
        }

        string fullPath = Path.Combine(dataDirPath, profileId, dataFileName);
        //check for Existence
        return File.Exists(fullPath);
    }
    
    public void AllTemplates() 
    {

        // loop over all directory names in the data directory path
        IEnumerable<DirectoryInfo> dirInfos = new DirectoryInfo(templateDirPath).EnumerateDirectories();
        foreach (DirectoryInfo dirInfo in dirInfos) 
        {
            string profileId = dirInfo.Name;
            string fullPathSource = Path.Combine(templateDirPath, profileId, dataFileName);
            string fullPathSourceDir = Path.Combine(templateDirPath, profileId);
            string fullPathTargetDir = Path.Combine(dataDirPath, profileId);
            string fullPathTarget = Path.Combine(dataDirPath, profileId, dataFileName);
            string dataToLoad = "";
            using (FileStream stream = new FileStream(fullPathSource, FileMode.Open))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    dataToLoad = reader.ReadToEnd();
                }
            }
            Directory.CreateDirectory(fullPathTargetDir);
            System.IO.File.WriteAllText(fullPathTarget, dataToLoad);
            DirectoryInfo dirTemplate = new DirectoryInfo(fullPathSourceDir);
            foreach (var file in dirTemplate.GetFiles())
            {
                if (!file.Name.Contains(".meta") && file.Name.Contains(".png"))
                {
                    string fullPathSourceFilePng = Path.Combine(templateDirPath, profileId, file.Name);
                    string fullPathTargetFilePng = Path.Combine(dataDirPath, profileId, file.Name);
                    File.Copy(fullPathSourceFilePng, fullPathTargetFilePng, true);
                }
            }
        }
            
    }
    
    public void ExportData(ConfigData data, string profileId)
    {
        // pdfFileType = NativeFilePicker.ConvertExtensionToFileType( "pdf" ); // Returns "application/pdf" on Android and "com.adobe.pdf" on iOS
        fileType = NativeFilePicker.ConvertExtensionToFileType( " " ); // Returns "application/pdf" on Android and "com.adobe.pdf" on iOS
        Debug.Log( "Files MIME/UTI is: " + fileType );
        StartExportProcess(data, profileId);
        }

    // private void StartExportProcess(ConfigData data, string profileId) {
    // // Example 1: Show a save file dialog using callback approach
	// // 	onSuccess event: not registered (which means this dialog is pretty useless)
	// // 	onCancel event: not registered
	// // 	Save file/folder: file, Allow multiple selection: false
	// // 	Initial path: "C:\", Initial filename: "Screenshot.png"
	// // 	Title: "Save As", Submit button text: "Save"
	// 	// FileBrowser.ShowSaveDialog( null, null, FileBrowser.PickMode.Files, false, Application.persistentDataPath, "Screenshot.png", "Save As", "Save" );
        
    //     // Before exporting a twin make sure it is saved
    //     Save(data, profileId);

    //     FileBrowser.ShowLoadDialog( ( paths ) =>
    //     { 
    //         // Succes case : eventCall
    //         Debug.Log( "User selected folder: " + FileBrowser.Success + " : " + FileBrowser.Result[0] );
    //         string selecetPath = FileBrowser.Result[0];
    //         PerformExport(data, profileId, selecetPath);
    //     },
    //         // Cancel case : eventCall
    //     () => { Debug.Log( "Export canceled"); 
    //     },
    //     // TODO: include localization dependence 
    //     FileBrowser.PickMode.Folders, false, null, null, "Select Folder", "Select" );
    // }



    private void StartExportProcess(ConfigData data, string profileId) 
    {
    // //         // Don't attempt to import/export files if the file picker is already open
            if( NativeFilePicker.IsFilePickerBusy() )
                return;

    // //         // Pick a PDF file
    //         NativeFilePicker.PickFile( ( path ) =>
    // //         {
    //             if( path == null )
    //                 Debug.Log( "Operation cancelled" );
    //             else
    //                 Debug.Log( "Picked file: " + path );
    //                 PerformExport(data, profileId, path);
    //             }
    //             , new string[] {fileType};
    //         )

            // NativeFilePicker.ExportFile(  ,
            // ( path ) =>
            // {
            //     if( path == null )
            //         Debug.Log( "Operation cancelled" );
            //     else
            //         Debug.Log( "Picked file: " + path );
            //     PerformExport(data, profileId, path);
            // });
        
    // #if UNITY_ANDROID
    //             // Use MIMEs on Android
    //             string[] fileTypes = new string[] { "image/*", "video/*" };
    // #else
    //             // Use UTIs on iOS
    //             string[] fileTypes = new string[] { };
    //             // OR ?????????????????????????????????????????????????????????????????????????????????????????????????????
    //             // string[] fileTypes = new string[] { " " };
    // #endif

                // Pick image(s) and/or video(s)
                // NativeFilePicker.PickMultipleFiles( ( paths ) =>
                // {
                //     if( paths == null )
                //         Debug.Log( "Operation cancelled" );
                //     else
                //     {
                //         for( int i = 0; i < paths.Length; i++ )
                //             Debug.Log( "Picked file: " + paths[i] );
                //     }
                // }, fileTypes );

                // Create a dummy text file
			// string filePath = Path.Combine( Application.temporaryCachePath, profileId );
			// File.WriteAllText( filePath, "Hello world!" );
            
            string filePath = Path.Combine(Application.temporaryCachePath);

            // string dataToStore = JsonUtility.ToJson(data, true);

            // WriteIntoFile(profileId, fullPath, dataToStore);
			// Export the file
			NativeFilePicker.ExportFile( filePath, ( success ) => { 
                Debug.Log( "File exported: " + success );
                if (success) 
                    PerformExport (data, profileId, filePath);
                });
            // Debug.Log( "File exported: " + success ));
            // PerformExport (data, profileId, fullPath);
    }

    private void PerformExport(ConfigData data, string profileId, string exportDestinationPath)
    {
        // base case - if the profileId is null, return right away
        if (profileId == null)
        {
            return;
        }
            
        // use Path.Combine to account for different OS's having different path separators
        if ((data.version != "") && (profileId.Contains('.') == false))
        {
            profileId = profileId + "." + data.version;
        }
        else if ((data.version != "") && (profileId.Contains('.')))
        {
            profileId = profileId.Remove(profileId.LastIndexOf(".")) + "." + data.version;
        }
        data.updated = DateTime.Now.ToString();
        string fullPath = Path.Combine(exportDestinationPath, profileId, dataFileName);
        Debug.Log("Exporting Twin to: " + fullPath);
        // string backupFilePath = fullPath + backupExtension;
        try
        {
            // create the directory the file will be written to if it doesn't already exist
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            // serialize the C# game data object into Json
            string dataToStore = JsonUtility.ToJson(data, true);

            // optionally encrypt the data
            if (useEncryption)
            {
                dataToStore = EncryptDecrypt(dataToStore);
            }

            WriteIntoFile(profileId, fullPath, dataToStore);

        }
        catch (Exception e)
        {
            Debug.LogError("Error occured when trying to Export Twin: " + fullPath + "\n," + e);
        }
    }

    private void WriteIntoFile(string profileId, string fullPath, string dataToStore)
    {
        // write the serialized data to the file
        using (FileStream stream = new FileStream(fullPath, FileMode.Create))
        {
            using (StreamWriter writer = new StreamWriter(stream))
            {
                writer.Write(dataToStore);
            }
        }

        // verify the newly saved file can be loaded successfully
        ConfigData verifiedGameData = Load(profileId);
        // if the data can be verified, back it up
        if (verifiedGameData != null)
        {
            // File.Copy(fullPath, backupFilePath, true);
            Debug.Log("Exported Twin successfully to: " + fullPath);
        }
        // otherwise, something went wrong and we should throw an exception
        else
        {
            throw new Exception("Save file could not be verified and backup could not be created.");
        }
    }
}
