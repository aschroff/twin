using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using System.Linq;
using Lean.Transition;
using UnityEngine.UI;
using System;
using RotaryHeart.Lib.SerializableDictionary;

[System.Serializable]
public class MenuAction 
{
    public string action;
    public Sprite icon;
    public string text;
}
[System.Serializable]
public class MenuDictionary : SerializableDictionaryBase<string, MenuAction> { }

public class MenuManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public MenuDictionary  menu;

   
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
        Delete();
        foreach (MenuAction action in menu.Values)
        {
            createMenuEntry(action);
        }
        {
            
        }
    }
    
    private void Delete()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform childTransform = transform.GetChild(i);
            Destroy(childTransform.gameObject);
        }
    }

    public void  createMenuEntry(MenuAction action)
    {
        GameObject actionData = Instantiate(prefab);
        actionData.transform.SetParent(this.transform, false);
        actionData.transform.localScale = prefab.transform.localScale;
        Text text = actionData.transform.Find("Action").gameObject.transform.Find("Text").GetComponentInChildren<Text>();
        text.text = action.text;
        Button buttonAction = actionData.transform.Find("Icon").GetComponentInChildren<Button>();
        Image imageAction = actionData.transform.Find("Icon").GetComponentInChildren<Image>();
        imageAction.sprite = action.icon;
        buttonAction.gameObject.SetActive(true);
        buttonAction.interactable = true;
        buttonAction.onClick.AddListener(() => { Version(); });
    }
    
    private void Version()
    {
        InteractionController.EnableMode("Version");
    }

}