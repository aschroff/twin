using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Lean.Common;
using Lean.Touch;
using UnityEngine.UI;
using UnityEngine.Localization.Components;
using UnityEngine.Events;

public class ViewManager : SceneManagement, IDataPersistence
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public PartManager partManager;

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

        AddDefaultView();
        foreach (View view in views)
        {
            displayView(view);
        }
    }

    private GameObject AddDefaultView() {
        //create defaultView and instance of the prefab representing the defaultView in the UI
        View defaultView = getDefaultView();
        GameObject defaultViewToDisplay = displayView(defaultView);

        //to update the name from the DefaultView we need access to the localizeEvents
        Transform viewNameDefaultView = defaultViewToDisplay.transform.Find("ReadOnlyMode").Find("Text Background").Find("ViewName");

        viewNameDefaultView.GetComponent<LocalizeStringEvent>().enabled = true;

        return defaultViewToDisplay;
    }

    public View getDefaultView() {

        View defaultView = new View();
        defaultView.name = "Default View";
        defaultView.positionCamera_x = 0.0f;
        defaultView.positionCamera_y = 0.5f;
        defaultView.positionCamera_z = 1.0f;
        defaultView.sizeCamera = 2;
        defaultView.pitch = 0.0f;
        defaultView.yaw = 0.0f;
        defaultView.initial = false;
        return defaultView;
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
    
    public GameObject displayView(View view)
    {
        GameObject viewObject = Instantiate(prefab);
        viewObject.transform.SetParent(this.transform, false);
        viewObject.transform.localScale = prefab.transform.localScale;

        ViewLink viewLink = viewObject.transform.GetComponent<ViewLink>();
        viewLink.link = view;
        viewLink.manager = this;
        viewObject.transform.Find("ReadOnlyMode").Find("Text Background").Find("ViewName").GetComponent<Text>().text = view.name;
        return viewObject;
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
        GameObject viewObject = displayView(view);
        
        //to enable naming the view directly after creation we activate the EditMode of the Prefab
        Transform editGameObject = viewObject.transform.Find("EditMode");
        editGameObject.gameObject.SetActive(true);
        viewObject.transform.Find("ReadOnlyMode").gameObject.SetActive(false);

        // the user should see that he can name the new view direclty 
        InputField viewNameInputField = editGameObject.Find("InputField").GetComponent<InputField>();
        this.transform.GetComponentInParent<ScrollRect>().normalizedPosition =  new Vector2(0,0);
        viewNameInputField.Select();
        viewNameInputField.ActivateInputField();
        viewNameInputField.onEndEdit.AddListener(delegate { SetNameOfNewView(viewObject, view, viewNameInputField.text); });
             
    }

    private void SetNameOfNewView(GameObject viewObject, View view, string nameOfNewView) {
        //since the view overlay should only contains ReadOnly Views we switch back to ReadOnly-Mode of the prefabe as the user named the new view
        Transform editGameObject = viewObject.transform.Find("ReadOnlyMode");
        editGameObject.gameObject.SetActive(true);
        viewObject.transform.Find("EditMode").gameObject.SetActive(false);

        editGameObject.Find("Text Background").Find("ViewName").GetComponent<Text>().text = nameOfNewView;
        view.name = nameOfNewView;
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
