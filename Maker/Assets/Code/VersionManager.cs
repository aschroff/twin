using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VersionManager : FileManager
{
    private string GetProfile()
    {
        return dataManager.selectedProfileId;
    }
    
    public override Dictionary<string, ConfigData> GetProfilesGameData()
    {
        return dataManager.GetAllVersionsGameData(GetProfile());
    }
}
