using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RotaryHeart.Lib.SerializableDictionary;


[System.Serializable]
public class InteractionModeDictionary : SerializableDictionaryBase<string, GameObject> { }

public class  InteractionController : Singleton<InteractionController>
{
    [SerializeField] InteractionModeDictionary interactionModes;

    GameObject currentMode;

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
        Debug.Log("BUG 20: " + name);
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
