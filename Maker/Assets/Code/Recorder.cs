using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.IO;


public class Recorder: MonoBehaviour
{

    public string name = new string("filename");
    public string folder = new string("foldername");

    public void Screenshot()
    {
        StartCoroutine(TakeAndSave());
    }
    
    private string get_path()
    {   
        return Path.Combine(Application.persistentDataPath,folder,
            "screenshot_" + name + ".png");
		
    }
    
    private IEnumerator TakeAndSave()
    {
        

        Texture2D ss = new Texture2D(Screen.width, Screen.height-1200, TextureFormat.RGB24, false);
        List<GameObject> ActiveChildren = new List<GameObject>();
        for (int i = this.transform.childCount - 1; i >= 1; i--)
        {
            GameObject child = this.transform.GetChild(i).gameObject;
            if (child.activeSelf)
            {
                ActiveChildren.Add(child);
            }
            child.SetActive(false);
        }
        yield return new WaitForEndOfFrame();
        ss.ReadPixels(new Rect(0, 900, Screen.width, Screen.height-1200), 0, 0);
        ss.Apply();
        foreach (GameObject activeChild in ActiveChildren)
        {
            activeChild.SetActive(true);
        }
        string fullPath = get_path();
        byte[] textureBytes = ss.EncodeToPNG();
        File.WriteAllBytes(fullPath, textureBytes);
        
    }
    
    

}
    
