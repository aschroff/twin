using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using UnityEngine;
using System.Linq;
using Lean.Transition;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using RotaryHeart.Lib.SerializableDictionary;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

[Serializable]
/// <summary>
/// Function definition for a button click event.
/// </summary>
public class ButtonClickedEvent : UnityEvent {}

[System.Serializable]
public class MenuAction 
{
    public Sprite icon;
    public string text;
    public ButtonClickedEvent onClick;
}
[System.Serializable]
public class MenuDictionary : SerializableDictionaryBase<string, MenuAction> { }

public class MenuManager : MonoBehaviour
{
    [SerializeField] public GameObject prefab;
    [SerializeField] public MenuDictionary  menu;
    

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
        LoadLocalizedEntry(text, action.text);
        //text.text = action.text;
        Button buttonAction = actionData.transform.Find("Icon").GetComponentInChildren<Button>();
        Image imageAction = actionData.transform.Find("Icon").GetComponentInChildren<Image>();
        imageAction.sprite = action.icon;
        buttonAction.gameObject.SetActive(true);
        buttonAction.interactable = true;
        buttonAction.onClick.AddListener(() => { action.onClick.Invoke(); });
    }

    private void LoadLocalizedEntry(Text text, String key)
    {
        String tableName = "TwinLocalTables";
        // Load the specific String Table by its name
        var stringTable = LocalizationSettings.StringDatabase.GetTable(tableName);


        if (stringTable != null)
        {

            // Retrieve the entry by key
            StringTableEntry entry = stringTable.GetEntry(key);

            if (entry != null)
            {
                // Access the localized value
                string localizedValue = entry.GetLocalizedString();
                text.text = localizedValue;
                Debug.Log("Localized value: " + localizedValue);
            }
            else
            {
                text.text = key;
                Debug.LogWarning("Entry not found for key: " + key);
            }
        }
        else
        {
            text.text = key;
            Debug.LogError("Failed to load table: " + tableName);
        }
    }
    

}