using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System.IO;
using Lean.Gui;
using UnityEngine.UI;


public class Recorder: MonoBehaviour
{

    public string name = new string("filename");
    public string folder = new string("foldername");

    public void Screenshot(LeanPulse notification)
    {
        StartCoroutine(TakeAndSave(notification));
    }
    
    public string get_path()
    {   
        return Path.Combine(DataPaths.PersistentDataPath,folder,
            "screenshot_" + name + ".png");
		
    }
    
    public bool FileExists()
    {
        string fullPath = get_path();
        return File.Exists(fullPath);
    }
    
    private IEnumerator TakeAndSave(LeanPulse notification)
    {
        List<GameObject> listActive = Prepare();
        yield return new WaitForEndOfFrame();
        Do();
        Reset(listActive);
        Post(notification);
        
    }
    

    public List<GameObject> Prepare()
    {
        List<GameObject> ActiveChildren = new List<GameObject>();
        for (int i = this.transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = this.transform.GetChild(i).gameObject;
            if (child.activeSelf)
            {
                ActiveChildren.Add(child);
            }

            child.SetActive(false);
        }

        return ActiveChildren;
    }
    
    public void Do()
    {
        Texture2D ss = new Texture2D(Screen.width, Screen.height - 1200, TextureFormat.RGB24, false);
        ss.ReadPixels(new Rect(0, 900, Screen.width, Screen.height-1200), 0, 0);
        ss.Apply();
        string fullPath = get_path();
        byte[] textureBytes = ss.EncodeToPNG();
        File.WriteAllBytes(fullPath, textureBytes);
        NativeGallery.SaveImageToGallery(textureBytes, "twinAlbum", "screenshot_" + name + ".png", ( success, path ) => Debug.Log( "Media save result: " + success + " " + path ) );
    }

    public void Post(LeanPulse notification)
    {
                     
#if UNITY_EDITOR
        string message = "Screenshot(s) saved  in application data under ..." + Path.Combine(
            folder, "screenshot_" + name + ".png");
#else
			string message = "Screenshot saved in Pictures as " + "screenshot_" + name + ".png";
#endif
        foreach (Text text in notification.gameObject.GetComponentsInChildren<Text>())
        {
            text.text = message;
        }
        notification.Pulse();
    }

    public void Reset(List<GameObject> ActiveChildren)
    {

        foreach (GameObject activeChild in ActiveChildren)
        {
            activeChild.SetActive(true);
        }
    }

}
    
