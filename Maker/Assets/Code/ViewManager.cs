using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Common;
using UnityEngine.UI;

public class ViewManager : MonoBehaviour, IDataPersistence
{
    [System.Serializable]
    public class View
    {
        public string name;
        public Vector3 positionCamera;
        public float sizeCamera;
        public float pitch;
        public float yaw;
        
    }
    
    [SerializeField] public GameObject prefab;
    [SerializeField] public List<View> views = new List<View>();
    [SerializeField] public GameObject mainCamera;
    [SerializeField] public GameObject body;
    
    public void build()
    {
        foreach (View view in views)
        {
            displayView(view);
        }
    }
    
    public void clear()
    {
        for (int j = 0; j < this.transform.childCount; j++) {
            GameObject child = this.transform.GetChild(j).gameObject;
            Destroy(child);
        }
    }

    public void rebuild()
    {
        clear();
        build();
    }
    
    public void displayView(View view)
    {
        GameObject viewObject = Instantiate(prefab);
        viewObject.transform.SetParent(this.transform, false);
        viewObject.transform.localScale = prefab.transform.localScale;
        viewObject.GetComponent<ViewLink>().link = view;
        viewObject.GetComponent<ViewLink>().manager = this;
        viewObject.GetComponentInChildren<InputField>().text = view.name;
    }
    public void createView()
    {
        View view = new View();
        LeanPitchYaw control = body.GetComponent<LeanPitchYaw>();
        view.yaw = control.Yaw;
        view.pitch = control.Pitch;
        view.positionCamera = mainCamera.transform.position;
        view.sizeCamera = mainCamera.GetComponent<Camera>().orthographicSize;
        views.Add(view);
        displayView(view);
    }                                                                            
    public void Show(bool visible)
    {
        this.gameObject.transform.parent.gameObject.SetActive(visible);
    }
    
    public void LoadData(ConfigData data)
    {
        
        var json = data.views;
        if (json == "")
        {
            views.Clear();
        }
        else
        {
            JsonUtility.FromJsonOverwrite(json, this);
        }
        
        rebuild();
    }

    public void SaveData(ConfigData data)
    {
        data.views = JsonUtility.ToJson(this);
    }
    
    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    
    public void delete(View view)
    {
        views.Remove(view);
        rebuild();
    }
    
    
    public void select(View view)
    {
        LeanPitchYaw control = body.GetComponent<LeanPitchYaw>();
        control.Yaw = view.yaw;
        control.Pitch =  view.pitch;
        mainCamera.transform.position = view.positionCamera;
        view.sizeCamera = mainCamera.GetComponent<Camera>().orthographicSize;
    }
    
}
