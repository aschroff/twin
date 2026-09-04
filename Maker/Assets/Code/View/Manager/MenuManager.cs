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

    /// <summary>Asked for each entry's text key before the entry is built; an entry it says no to
    /// is left out. The configured menu is the full set of what a panel *can* offer - this is how an
    /// entry that would do nothing right now stays off the screen instead of being offered and then
    /// refusing. Entries are rebuilt on every OnEnable, so the answer may change between openings.
    /// Null means every entry is offered.</summary>
    public Func<string, bool> entryAvailable;
    

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
            if (entryAvailable != null && !entryAvailable(action.text))
            {
                continue;
            }
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
        text.text = StringLocalizer.localizeString(action.text);
        //text.text = action.text;
        Button buttonAction = actionData.transform.Find("Icon").GetComponentInChildren<Button>();
        Image imageAction = actionData.transform.Find("Icon").GetComponentInChildren<Image>();
        imageAction.sprite = action.icon;
        buttonAction.gameObject.SetActive(true);
        buttonAction.interactable = true;
        buttonAction.onClick.AddListener(() => { action.onClick.Invoke(); });
    }
    

}