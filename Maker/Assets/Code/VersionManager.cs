using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VersionManager : FileManager
{
    [SerializeField] public FileManager fileManager;
    protected override bool GetVersionButton()
    {
        return false;
    } 
    public override string GetProfile()
    {
        string name = fileManager.versionProfile;
        if ( name.Contains('.'))
        {
            name = name.Remove(name.LastIndexOf("."));
        }
        return name;
    }
    
    public override Dictionary<string, ConfigData> GetProfilesGameData()
    {
        return dataManager.GetAllVersionsGameData(GetProfile());
    }
}
