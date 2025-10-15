using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RotaryHeart.Lib.SerializableDictionary;


[System.Serializable]
public class InteractionModeDictionary : SerializableDictionaryBase<string, GameObject> { }

public class  InteractionController : Singleton<InteractionController>
{
    [SerializeField] InteractionModeDictionary interactionModes;
    [SerializeField] PartManager partManager;
    [SerializeField] private static string twin;
    [SerializeField] private static string version;
    [SerializeField] private static PartManager.GroupData groupdata;
    [SerializeField] private static PartManager.PartData partdata;

    public static string Twin
    {
        get { return twin; }
        set { twin = value; }
    }

    public static string Version
    {
        get { return version; }
        set { version = value; }
    }

    public static PartManager.GroupData Groupdata
    {
        get { return groupdata; }
        set { groupdata = value; }
    }

    public static PartManager.PartData Partdata
    {
        get { return partdata; }
        set { partdata = value; }
    }
    
    GameObject currentMode;
    
        
    public delegate void ModeChange(GameObject modeOld, GameObject modeNew);
    public static event ModeChange OnModeChange;

    private static void RaiseModeChange(GameObject modeOld, GameObject modeNew)
    {
        OnModeChange?.Invoke(modeOld, modeNew);
    }
    

    void Awake()
    {
        base.Awake();
        ResetAllModes();
    }

    public static void EnableMode(string name)
    {
        //Debug.Log(name + (Instance != null));
        Instance?._EnableMode(name);
    }

    void _EnableMode(string name)
    {
        Debug.Log("Enable Mode: " + name);
        GameObject modeObject;
        if (interactionModes.TryGetValue(name, out modeObject))
        {
            StartCoroutine(ChangeMode(modeObject));
        }
        else
        {
            Debug.LogError("Undefined mode named " + name);
        }
    }

    void ResetAllModes()
    {
        foreach (GameObject mode in interactionModes.Values)
        {
            mode.SetActive(false);
        }
    }

    IEnumerator ChangeMode(GameObject mode)
    {
        if (mode == currentMode)
            yield break;
        if (currentMode)
        {   
            RaiseModeChange(currentMode, mode);
            currentMode.SetActive(false);
            yield return null;
        }

        currentMode = mode;
        mode.SetActive(true);
            
    }

    private void Start()
    {
        _EnableMode("Main");
    }

}
