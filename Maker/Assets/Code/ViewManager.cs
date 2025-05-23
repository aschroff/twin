using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Common;
using Lean.Touch;
using UnityEngine.UI;

public class ViewManager : SceneManagement, IDataPersistence
{
    [SerializeField] public GameObject prefab;
    [SerializeReference] public PartManager partManager;
    
    private bool cameraEnabled = true;

    private void Start()
    {
        if (partManager == null)
        {
            partManager = this.transform.parent.parent.parent.GetComponentInParent<PartManager>();
        }
    }

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
    public View shootView()
    {
        View view = new View();
        LeanPitchYaw control = body.GetComponent<LeanPitchYaw>();
        view.yaw = control.Yaw;
        view.pitch = control.Pitch;
        view.positionCamera_x = mainCamera.transform.position.x;
        view.positionCamera_y = mainCamera.transform.position.y;
        view.positionCamera_z = mainCamera.transform.position.z;
        LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
        view.sizeCamera = camera.Zoom;
        view.initial = false;
        return view;
    }   
    
    public void createView()
    {
        View view = shootView();
        views.Add(view);
        displayView(view);
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
            GameObject prefab_save = prefab;
            GameObject mainCamera_save = this.mainCamera;
            GameObject body_save = body;
            PartManager partManagerTemp = partManager;

            JsonUtility.FromJsonOverwrite(json, this);
            
            prefab = prefab_save; 
            mainCamera = mainCamera_save;
            body = body_save;
            partManager = partManagerTemp;
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
        partManager.EnforceNewPart();
        LeanPitchYaw control = body.GetComponent<LeanPitchYaw>();
        control.Yaw = view.yaw;
        control.Pitch =  view.pitch;
        mainCamera.transform.position = new Vector3(view.positionCamera_x, view.positionCamera_y, view.positionCamera_z);
        LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
        cameraEnabled = camera.enabled;
        camera.enabled = true;
        camera.Zoom = view.sizeCamera;
    }
    
    public void setDefaultPosition()
    {
        partManager.EnforceNewPart();
        LeanPitchYaw control = body.GetComponent<LeanPitchYaw>();
        control.Yaw = 0.0f;
        control.Pitch =  0.0f;
        mainCamera.transform.position = new Vector3(x: 0.0f, y: 0.5f, z: 1.0f);
        LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
        camera.Zoom = 2.0f; 
    }

    void LateUpdate()
    {
        LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
        camera.enabled = cameraEnabled; 
    }
    
    
    public void Show(bool visible)
    {   
        RectTransform recttransform = this.gameObject.transform.parent.transform.parent.GetComponent<RectTransform>();
        float width = recttransform.rect.width;
        if (visible & recttransform.anchoredPosition.x  <= 0)
        {
            
        }
        else if (!visible  & recttransform.anchoredPosition.x  <= 0)
        {
            recttransform.anchoredPosition += new Vector2(width, 0);
        }  
        else if (!visible & recttransform.anchoredPosition.x  > 0)
        {
            
        } 
        else if (visible  & recttransform.anchoredPosition.x  > 0)
        {
            recttransform.anchoredPosition += new Vector2(-width, 0);
        }  
    }

    public void toggleShow()
    {
        RectTransform recttransform = this.gameObject.transform.parent.transform.parent.GetComponent<RectTransform>();
        bool newVisible = (recttransform.anchoredPosition.x > 0);
        Show(newVisible);
    }

    public void AddList()
    {
        StandardViewManager standards = this.transform.parent.transform.parent.GetComponent<StandardViewManager>();
        views.AddRange(standards.views);
        rebuild();
    }
    
}
