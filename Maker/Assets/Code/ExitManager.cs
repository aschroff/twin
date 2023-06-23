using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExitManager : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ExitMaker();
        }
    }

    public void ExitMaker()
    {
        Debug.Log("ExitMaker Application.Quit");
        #if UNITY_STANDALONE
                Application.Quit();
        #endif
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #endif

    }

    static void ExitProcessing()
    {
        Debug.Log("ExitProcessing");
        DataPersistenceManager manager = (DataPersistenceManager)FindObjectOfType(typeof(DataPersistenceManager));
        manager.SaveConfig();
    }

    [RuntimeInitializeOnLoadMethod]
    static void RunOnStart()
    {
        Application.quitting += ExitProcessing;
    }


}