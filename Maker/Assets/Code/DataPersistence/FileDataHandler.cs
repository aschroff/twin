using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using static NativeFilePicker;
using System.IO.Compression;
using System.Threading.Tasks;


public class FileDataHandler
{
    private string dataDirPath = "";
    private string templateDirPath = "";
    private string dataFileName = "";
    private bool useEncryption = false;
    private readonly string encryptionCodeWord = "word";
    private readonly string backupExtension = ".bak";
    private readonly string compressExtension = ".zip";
    private readonly string allowedExtractionFormats = "com.pkware.zip-archive, application/epub+zip";
    /// <summary>Where an import is unpacked before it is moved to its own directory.</summary>
    private readonly string importDirectory = "__import";
    /// <summary>Marks an imported twin whose name and version are taken already: V01, V02, ...</summary>
    private readonly string versionSuffix = "V";
    private const int maxImportedVersions = 100;
    private readonly string defaultVersion = "000";

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
        ExportFile(ExportZip(data, profileId));
    }

    /*
    * Saves the twin and packs its directory into a zip file next to it. Returns the path of
    * that zip file - handing it to the user is a separate step (ExportFile).
    */
    public string ExportZip(ConfigData data, string profileId)
    {
        // Saving file before exporting it
        Save(data, profileId);
        return CompressFolder(profileId);
    }

    private string CompressFolder(string profileId)
    {   
        string profileDirectoryPath = Path.Combine( dataDirPath, profileId );
        string zipFilePath = Path.Combine( dataDirPath, profileId ) + compressExtension;

        // manually overwrite an already existing version of a zip file with this name because we don't wand to provide a version history and to avoid conflicts with already existing files
        try 
        {
            if (File.Exists(zipFilePath))
            {
                File.Delete(zipFilePath);
            }
            ZipFile.CreateFromDirectory( profileDirectoryPath, zipFilePath);
            return zipFilePath;

        } catch (Exception e)
        {
            Debug.Log("Compressing Folder didn't work, throwing this Exception Message: " + e.Message);
            throw e;
        }
    }

    public void ExportFile(string filePath)
    {
        // Don't attempt to import/export files if the file picker is already open
        if( IsFilePickerBusy() )
        {
            return;
        }
        NativeFilePicker.ExportFile( filePath, ( success ) => Debug.Log( "File exported:" + success ) );
    }

    /*
    * Responsible method for calling method to pick twin config zip file (asynchronous) 
    * following with the call of the method to extract this zip file 
    */
    public async Task<string> ImportZipConfigAsync()
    {
        string zipFilePath = await PickFileAsync();
        if ( string.IsNullOrEmpty( zipFilePath ) || !File.Exists( zipFilePath ) )
        {
            Debug.Log( "Error: No file was selected or the file does not exist. Path: " + zipFilePath );
            return null;
        }
        return ImportZipConfig( zipFilePath );
    }

    /*
    * Imports an already chosen zip file (no file picker involved). Returns the path of the
    * extracted twin directory, or null if the zip could not be extracted.
    */
    public string ImportZipConfig( string zipFilePath )
    {
        return ExtractDirectory( zipFilePath );
    }

    private async Task<string> PickFileAsync()
    {
        TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();
        if ( IsFilePickerBusy() )
        {
            Debug.Log( "FilePicker is busy." );
            return null;
        }
        NativeFilePicker.PickFile(
            filePath => tcs.TrySetResult( filePath ),
            allowedExtractionFormats );
        return await tcs.Task;
    }

    /*
    * Unpacks the zip into a directory of its own. The twin is unpacked to a temporary place
    * first and only moved to its final directory once that worked, so an import can never
    * damage a twin that is already on this device.
    */
    private string ExtractDirectory( string zipFilePath )
    {
        if ( !File.Exists( zipFilePath ) )
        {
            Debug.Log( "Error: ZIP-File does not exist. Ensure the existence of the chosen file." );
            return null;
        }

        string extractPath = Path.Combine( dataDirPath, importDirectory );
        try
        {
            if ( Directory.Exists( extractPath ) )
            {
                Directory.Delete( extractPath, true );
            }
            ZipFile.ExtractToDirectory( zipFilePath, extractPath );

            string extractedProfileId = FindConfigDirectory( importDirectory );
            if ( extractedProfileId == null )
            {
                Debug.Log( "Error: The chosen file does not contain a twin (no " + dataFileName + " in it)." );
                return null;
            }

            ConfigData importedData = Load( extractedProfileId );
            string profileId = FreeProfileId( ProfileIdOf( importedData, zipFilePath ) );
            string profileDirectoryPath = Path.Combine( dataDirPath, profileId );

            // the only step that touches the data directory, and it only ever adds to it
            Directory.Move( Path.Combine( dataDirPath, extractedProfileId ), profileDirectoryPath );

            if ( importedData != null )
            {
                // the directory name is what identifies a twin in the app, and the twin list
                // reads name and version out of the config, so the two have to agree
                importedData.name = NameOf( profileId );
                importedData.version = VersionOf( profileId );
                // it arrived now, so it is the newest version of that twin on this device -
                // that is what the twin list and the twin loaded at startup go by
                importedData.lastUpdated = DateTime.Now.ToBinary();
                Save( importedData, profileId );
            }
            Debug.Log( "Imported twin " + profileId + "." );
            return profileDirectoryPath;
        }
        catch ( Exception e )
        {
            Debug.Log( "Error: Due to an exception the directory was not extracted: " + e.Message );
            return null;
        }
        finally
        {
            if ( Directory.Exists( extractPath ) )
            {
                Directory.Delete( extractPath, true );
            }
        }
    }

    /*
    * The twin can sit at the root of the zip or inside a single directory in it, depending on
    * how it was packed. Returns the profile id (relative to the data directory) of whichever
    * directory holds the config, or null if the zip holds no twin at all.
    */
    private string FindConfigDirectory( string profileId )
    {
        if ( File.Exists( Path.Combine( dataDirPath, profileId, dataFileName ) ) )
        {
            return profileId;
        }
        foreach ( DirectoryInfo dirInfo in new DirectoryInfo( Path.Combine( dataDirPath, profileId ) ).EnumerateDirectories() )
        {
            string candidate = Path.Combine( profileId, dirInfo.Name );
            if ( File.Exists( Path.Combine( dataDirPath, candidate, dataFileName ) ) )
            {
                return candidate;
            }
        }
        return null;
    }

    /*
    * A twin is identified by its name and version, and both come out of its config. The name
    * of the zip file is no help here: everything that carries the file around - mail clients,
    * file managers, a server - may rename it.
    */
    private string ProfileIdOf( ConfigData data, string zipFilePath )
    {
        if ( data == null || string.IsNullOrEmpty( data.name ) )
        {
            Debug.LogWarning( "The imported twin has no name in its config, using the file name instead." );
            return WithVersion( Path.GetFileNameWithoutExtension( zipFilePath ), defaultVersion );
        }
        return WithVersion( data.name, data.version );
    }

    private string WithVersion( string name, string version )
    {
        return name + "." + ( string.IsNullOrEmpty( version ) ? defaultVersion : version );
    }

    /*
    * Keeps the twins that are already on this device: an imported twin whose directory exists
    * gets a V01, V02, ... suffix on its version instead of replacing it.
    */
    private string FreeProfileId( string profileId )
    {
        if ( !IsTaken( profileId ) )
        {
            return profileId;
        }
        for ( int counter = 1; counter < maxImportedVersions; counter++ )
        {
            string candidate = profileId + versionSuffix + counter.ToString( "00" );
            if ( !IsTaken( candidate ) )
            {
                return candidate;
            }
        }
        throw new Exception( "Cannot import " + profileId + ", too many versions of it are stored already." );
    }

    private bool IsTaken( string profileId )
    {
        return Directory.Exists( Path.Combine( dataDirPath, profileId ) );
    }

    private string NameOf( string profileId )
    {
        return profileId.Contains( "." ) ? profileId.Remove( profileId.LastIndexOf( "." ) ) : profileId;
    }

    private string VersionOf( string profileId )
    {
        return profileId.Contains( "." ) ? profileId.Substring( profileId.LastIndexOf( "." ) + 1 ) : "";
    }
}
