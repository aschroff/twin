using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Common;
using Lean.Touch;
using UnityEngine.UI;

public class Views
{
    public List<SceneManagement.View> views;
}

public class StandardViewManager : SceneManagement
{
    [SerializeField] TextAsset ViewsFile;
    void Awake()
    {
        Views tempViews = new Views();
        string readstring = ReadFileToTextAsset(ViewsFile);
        tempViews = JsonUtility.FromJson<Views>(readstring);
        views = tempViews.views;
    }
    private static string ReadFileToTextAsset(TextAsset assetfile)
    {
        string content = assetfile.text;
        return content;

    }
}