using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using System.Linq;
using Lean.Transition;
using UnityEngine.UI;
using System;

public class MenuManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;

   
    void OnDisable()
    {
        Debug.Log("PrintOnDisable: script was disabled");
    }

    void OnEnable()
    {
        Refresh();
    }

    
    // Start is called before the first frame update
    void Start()
    {
        Create();
    }
    

    public void Refresh()
    {
        Create();
    }

    private void Create()
    {
            createMenuEntry();
    }

    public void  createMenuEntry()
    {
        GameObject actionData = Instantiate(prefab);
        actionData.transform.SetParent(this.transform, false);
        actionData.transform.localScale = prefab.transform.localScale;
        Text text = actionData.transform.Find("Action").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        text.text = "Versions";
        Button buttonAction = actionData.transform.Find("Icon").GetComponentInChildren<Button>();
 
        buttonAction.gameObject.SetActive(false);
        buttonAction.interactable = false;
        buttonAction.onClick.AddListener(() => { Version(); });
    }
    
    private void Version()
    {
        InteractionController.EnableMode("Menu");
    }

}