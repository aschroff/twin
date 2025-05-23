using UnityEngine;
using System.Linq;
using Lean.Common;
using Lean.Touch;
using UnityEngine.UI;
using Lean.Gui;
using System.Collections.Generic;

/// <summary>
///This class works closely with the ViewManager which is responsible for managing the Views itself!
///It cares only about the ViewListUI and the GameObjects it contains (adding list of standard views, displaying all existing views)
/// </summary>
public class ViewListManager : SceneManagement, IDataPersistence
{
    [SerializeField] private ViewManager viewManager;
    [SerializeField] private GameObject prefab;

    private bool cameraEnabled = true;

    private void Start()
    {
        this.rebuild();
    }

    void OnEnable()
    {
        this.rebuild();
    }

    public void rebuild()
    {
        clear();
        build();
    }

    public void clear()
    {
        for (int j = 0; j < this.transform.childCount; j++)
        {
            GameObject child = this.transform.GetChild(j).gameObject;
            Destroy(child);
        }
    }

    public void build()
    {
        //get views from viewsManager so they are only stored there
        //Debug.Log("Number of Views in ViewManager on build: " + this.viewManager.views.Count);
        foreach (View view in this.viewManager.views)
        {
            displayView(view);
        }
    }

    /// <summary>
    /// Creates the prefab - using the viewManager as its parent since the viewManager should manage all views.
    /// The localScale, a link to the view, the viewManager itself and the name of the view will be set too.
    /// </summary>
    /// <param name="view"></param>
    public void displayView(View view)
    {
        GameObject viewObject = Instantiate(prefab);
        viewObject.transform.SetParent(this.transform, false);
        viewObject.transform.localScale = prefab.transform.localScale;
        viewObject.GetComponent<ViewLink>().link = view;
        viewObject.GetComponent<ViewLink>().manager = this.viewManager;
        viewObject.GetComponentInChildren<InputField>().text = view.name;
    }

    public void AddList()
    {
        this.viewManager.AddList();
        rebuild();
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
            ViewManager viewManagerTemp = this.viewManager;

            JsonUtility.FromJsonOverwrite(json, this);

            prefab = prefab_save;
            mainCamera = mainCamera_save;
            body = body_save;
            this.viewManager = viewManagerTemp;
        }

        this.rebuild();
    }

    public void SaveData(ConfigData data)
    {
        data.views = JsonUtility.ToJson(this);
    }

    public GameObject relatedGameObject()
    {
        return this.gameObject;
    }

    void LateUpdate()
    {
        LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
        camera.enabled = cameraEnabled;
    }

    //the following two functions should be possible in the future
    //but they come with another PR!

    //public void delete(View view)
    //{
    //    views.Remove(view);
    //    this.rebuild();
    //}

    //public void select(View view)
    //{
    //    partManager.EnforceNewPart();
    //    LeanPitchYaw control = body.GetComponent<LeanPitchYaw>();
    //    control.Yaw = view.yaw;
    //    control.Pitch = view.pitch;
    //    mainCamera.transform.position = new Vector3(view.positionCamera_x, view.positionCamera_y, view.positionCamera_z);
    //    LeanPinchCamera camera = mainCamera.GetComponent<LeanPinchCamera>();
    //    cameraEnabled = camera.enabled;
    //    camera.enabled = true;
    //    camera.Zoom = view.sizeCamera;
    //}

}
