using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VersionManager : FileManager
{
    
    protected override bool GetVersionButton()
    {
        return false;
    } 
    private string GetProfile()
    {
        string name = dataManager.selectedProfileId;
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
