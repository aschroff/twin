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

    /// <summary>
    /// The twin whose versions this screen lists: the one that is actually loaded.
    /// </summary>
    /// <remarks>
    /// This used to read <c>fileManager.versionProfile</c> alone - the name the twin list
    /// remembered when it opened the menu. That is the same twin as long as the screen is only ever
    /// reached through the list, because <c>FileManager.Detail</c> loads the twin before it sets
    /// the name. It stopped being true when the main UI got a button that jumps straight here:
    /// on a fresh start nothing has set the name yet, and <c>Contains</c> on a null string is a
    /// NullReferenceException rather than an empty list.
    ///
    /// The loaded twin is the better answer in both routes - in the list route it is the same
    /// value, and it cannot go stale when the list switches twins without passing through the
    /// menu. The remembered name stays as the fallback for the case where nothing is loaded at all.
    /// </remarks>
    public override string GetProfile()
    {
        string name = dataManager != null ? dataManager.selectedProfileId : null;
        if (string.IsNullOrEmpty(name))
        {
            name = fileManager != null ? fileManager.versionProfile : null;
        }
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

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
